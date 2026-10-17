using Build.Util;
using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;
using HtmlAgilityPack;
using Jint;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Build.Tasks
{
    [TaskName("UpdateOpenApiSpec")]
    public class UpdateOpenApiSpecTask : FrostingTask<BuildContext>
    {
        private readonly JsonSerializerOptions _options = new JsonSerializerOptions()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public override void Run(BuildContext context)
        {
            // --skip-download reapplies the fixes to the committed spec and regenerates, without fetching MAL's page
            var spec = context.HasArgument("skip-download")
                ? JsonNode.Parse(File.ReadAllText(Constants.OpenApiSpecPath))!
                : DownloadSpec();

            FixPathParameterNames(context, spec);
            FixForumTopic(context, spec);
            AddUserListFields(context, spec);
            FixQueryParameters(context, spec);
            AddAnimeDetailsFields(context, spec);
            MakeDatesStrings(context, spec);
            FlattenSingleAllOf(context, spec);
            RenameSchemas(context, spec);
            SetOperationIds(context, spec);
            SetParameterVariableNames(context, spec);
            PromoteInlineSchemas(context, spec);
            UnifyPaging(context, spec);
            FixListStatusPatches(context, spec);
            AddEnums(context, spec);
            SetRequiredProperties(context, spec);
            SetPropertyNames(context, spec);
            MarkBaseClasses(context, spec);
            RemoveDocumentation(context, spec);
            AddDocumentation(context, spec);

            File.WriteAllText(Constants.OpenApiSpecPath, spec.ToJsonString(_options));

            var dotnetExe = context.Tools.Resolve(context.DotnetExeName);
            // NSwag.ConsoleCore ships one build per framework; the .nswag config requires the net10.0 one (runtime Net100)
            var nswagExe = context.GetFiles("./tools/.store/nswag.consolecore/**/tools/net10.0/any/dotnet-nswag.dll").Single();

            var settings = new ProcessSettings()
                .UseWorkingDirectory(".")
                .WithArguments(builder => builder
                    .AppendQuoted(nswagExe.FullPath)
                    .Append("run")
                    .AppendQuoted(Constants.NswagConfigFilePath)
                );

            context.RunProcess(dotnetExe, settings);
        }

        // reads the spec out of the Redoc page's script; run this by hand only (see "MyAnimeList API License Agreement" in CLAUDE.md)
        private JsonNode DownloadSpec()
        {
            var web = new HtmlWeb();
            var document = web.Load("https://myanimelist.net/apiconfig/references/api/v2");

            var nodes = document.DocumentNode.Descendants("script");

            var scriptNode = nodes.Last()?.InnerText
                .Replace("var container = document.getElementById('redoc');", "")
                .Replace("Redoc.hydrate(__redoc_state, container);", "");

            var openapiSpec = new Engine()
                .Execute(scriptNode!)
                .Evaluate("__redoc_state.spec.data").ToObject();

            return JsonSerializer.SerializeToNode(openapiSpec, options: _options)!;
        }

        // MAL's spec declares path parameters that don't match the path template (e.g. "user_id" for "/users/{user_name}"),
        // which makes NSwag drop the parameter from the generated url. Rename them to the placeholder they belong to.
        private static void FixPathParameterNames(BuildContext context, JsonNode spec)
        {
            foreach (var (path, pathItem) in spec["paths"]!.AsObject())
            {
                var placeholders = Regex.Matches(path, "{([^}]+)}").Select(match => match.Groups[1].Value).ToArray();

                // only operations declare their path parameters here; a path-level "parameters" array is not an object
                foreach (var (_, operation) in pathItem!.AsObject())
                {
                    if (operation is not JsonObject || operation["parameters"] is not JsonArray parameters)
                    {
                        continue;
                    }

                    var pathParameters = parameters
                        .Where(parameter => (string?)parameter?["in"] == "path")
                        .ToArray();

                    var unmatchedPlaceholders = placeholders
                        .Where(placeholder => pathParameters.All(parameter => (string?)parameter!["name"] != placeholder))
                        .ToArray();

                    var unmatchedParameters = pathParameters
                        .Where(parameter => !placeholders.Contains((string?)parameter!["name"]))
                        .ToArray();

                    if (unmatchedParameters.Length == 1 && unmatchedPlaceholders.Length == 1)
                    {
                        context.Information($"Renaming path parameter '{unmatchedParameters[0]!["name"]}' of '{path}' to '{unmatchedPlaceholders[0]}'");
                        unmatchedParameters[0]!["name"] = unmatchedPlaceholders[0];
                    }
                }
            }
        }

        // GET /forum/topic/{topic_id} doesn't match MAL's spec: "data" and "data.poll" are single objects, not arrays,
        // and the poll's "close" flag is sent as "closed"
        private static void FixForumTopic(BuildContext context, JsonNode spec)
        {
            var schemas = spec["components"]?["schemas"];

            ReplaceArrayWithItem(context, schemas?["ForumTopic"]?["allOf"]?[0]?["properties"], "data");
            ReplaceArrayWithItem(context, schemas?["ForumTopicData"]?["allOf"]?[0]?["properties"], "poll");

            if (schemas?["ForumTopicPoll"]?["allOf"]?[0]?["properties"] is JsonObject pollProperties
                && pollProperties.IndexOf("close") is var index and >= 0)
            {
                context.Information("Renaming ForumTopicPoll.close to closed");
                pollProperties.Remove("close", out var close);
                pollProperties.Insert(index, "closed", close);
            }
        }

        // MAL's spec gives the user list endpoints no "fields" parameter, but the live API accepts one, and without it
        // each entry only has the node's id, title and main_picture and no list_status
        private static void AddUserListFields(BuildContext context, JsonNode spec)
        {
            foreach (var path in new[] { "/users/{user_name}/animelist", "/users/{user_name}/mangalist" })
            {
                if (spec["paths"]?[path]?["get"]?["parameters"] is not JsonArray parameters
                    || parameters.Any(parameter => (string?)parameter?["name"] == "fields"))
                {
                    continue;
                }

                context.Information($"Adding the fields parameter to '{path}'");
                parameters.Add(new JsonObject
                {
                    ["name"] = "fields",
                    ["in"] = "query",
                    ["schema"] = new JsonObject { ["type"] = "string" },
                });
            }
        }

        // MAL's spec marks the search term as optional, has no nsfw parameter, and gives limit and offset few bounds. Checked live
        // on 2026-10-17: searches without q or with fewer than 3 characters fail with 400; nsfw=true adds entries rated "gray"
        // to searches, seasonal anime and user lists (the rankings include them anyway; the suggestions showed no difference);
        // larger limits than these, limit=0 and offset=-1 fail with 400. NSwag doesn't check the bounds, they only document the spec.
        private static readonly string[] SearchPaths = ["/anime", "/manga"];

        private static readonly string[] NsfwPaths = ["/anime", "/manga", "/anime/season/{year}/{season}", "/users/{user_name}/animelist", "/users/{user_name}/mangalist"];

        private static readonly Dictionary<string, int> LimitMaximums = new()
        {
            ["/anime/ranking"] = 500,
            ["/manga/ranking"] = 500,
            ["/anime/season/{year}/{season}"] = 500,
            ["/users/{user_name}/animelist"] = 1000,
            ["/users/{user_name}/mangalist"] = 1000,
            ["/forum/topics"] = 100,
            ["/forum/topic/{topic_id}"] = 100,
        };

        private static void FixQueryParameters(BuildContext context, JsonNode spec)
        {
            foreach (var (path, pathItem) in spec["paths"]!.AsObject())
            {
                if (pathItem?["get"]?["parameters"] is not JsonArray parameters)
                {
                    continue;
                }

                var byName = parameters.OfType<JsonObject>().ToDictionary(parameter => (string)parameter["name"]!);

                if (SearchPaths.Contains(path) && byName.TryGetValue("q", out var q))
                {
                    q["required"] = true;
                    q["schema"]!["minLength"] = 3;
                }

                if (NsfwPaths.Contains(path) && !byName.ContainsKey("nsfw"))
                {
                    context.Information($"Adding the nsfw parameter to '{path}'");
                    parameters.Add(new JsonObject
                    {
                        ["name"] = "nsfw",
                        ["in"] = "query",
                        ["schema"] = new JsonObject { ["type"] = "boolean" },
                    });
                }

                if (byName.TryGetValue("limit", out var limit))
                {
                    limit["schema"]!["minimum"] = 1;
                    if (LimitMaximums.TryGetValue(path, out var maximum))
                    {
                        limit["schema"]!["maximum"] = maximum;
                    }
                }

                if (byName.TryGetValue("offset", out var offset))
                {
                    offset["schema"]!["minimum"] = 0;
                }
            }

            context.Information("Fixed the search term, nsfw, limit and offset parameters");
        }

        // MAL sends these fields of anime details when they are requested, but its spec doesn't declare them (checked live on
        // 2026-10-17). The video timestamps are Unix times in milliseconds, unlike every other timestamp of the API.
        private static void AddAnimeDetailsFields(BuildContext context, JsonNode spec)
        {
            var schemas = spec["components"]!["schemas"]!.AsObject();

            schemas["AnimeTheme"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["id"] = new JsonObject { ["type"] = "integer" },
                    ["anime_id"] = new JsonObject { ["type"] = "integer" },
                    ["text"] = new JsonObject { ["type"] = "string" },
                },
            };

            schemas["AnimeVideo"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["id"] = new JsonObject { ["type"] = "integer" },
                    ["title"] = new JsonObject { ["type"] = "string" },
                    ["url"] = new JsonObject { ["type"] = "string", ["format"] = "uri" },
                    ["created_at"] = new JsonObject { ["type"] = "integer", ["format"] = "int64" },
                    ["updated_at"] = new JsonObject { ["type"] = "integer", ["format"] = "int64" },
                    ["thumbnail"] = new JsonObject { ["type"] = "string", ["format"] = "uri" },
                },
            };

            var properties = FindPropertyOwner(schemas["AnimeForDetails"]!.AsObject(), "pictures")!["properties"]!.AsObject();
            foreach (var (name, schema) in new[] { ("opening_themes", "AnimeTheme"), ("ending_themes", "AnimeTheme"), ("videos", "AnimeVideo") })
            {
                properties[name] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["$ref"] = $"#/components/schemas/{schema}" },
                };
            }

            context.Information("Added opening_themes, ending_themes and videos to AnimeForDetails");
        }

        // MAL's spec declares some dates as "format: date" (list status start/finish dates, birthdays), which NSwag turns into
        // DateTimeOffset read with a culture-dependent DateTimeOffset.Parse. MAL sends partial dates ("2017-10", "2017") there,
        // which that rejects or rounds to the first of the month, so keep them as the strings MAL sends, like WorkForList.start_date.
        private static void MakeDatesStrings(BuildContext context, JsonNode spec)
        {
            var changed = 0;
            Visit(spec["components"]);

            context.Information($"Changed {changed} date properties to plain strings");

            void Visit(JsonNode? node)
            {
                switch (node)
                {
                    case JsonObject jsonObject:
                        if ((string?)jsonObject["type"] == "string" && (string?)jsonObject["format"] == "date")
                        {
                            jsonObject.Remove("format");
                            changed++;
                        }

                        foreach (var (_, child) in jsonObject.ToArray())
                        {
                            Visit(child);
                        }

                        break;

                    case JsonArray jsonArray:
                        foreach (var child in jsonArray)
                        {
                            Visit(child);
                        }

                        break;
                }
            }
        }

        // MAL's schema names leak its internals ("AggregationEdgeBase") or only differ by an "s" ("ForumTopicsData", "ForumTopicData")
        private static readonly (string From, string To)[] SchemaNames =
        [
            ("AnimeRecommendationAggregationEdgeBase", "AnimeRecommendation"),
            ("MangaRecommendationAggregationEdgeBase", "MangaRecommendation"),
            ("ForumTopics", "ForumTopicList"),
            ("ForumTopicsData", "ForumTopicSummary"),
            ("ForumTopicsCreatedBy", "ForumUser"),
            ("ForumTopicData", "ForumTopicDetails"),
            ("ForumTopicPostCreatedBy", "ForumPostAuthor"),
        ];

        private static void RenameSchemas(BuildContext context, JsonNode spec)
        {
            var schemas = spec["components"]!["schemas"]!.AsObject();

            foreach (var (from, to) in SchemaNames)
            {
                // already renamed when the committed spec is reused (--skip-download)
                if (schemas.IndexOf(from) is var index and >= 0)
                {
                    context.Information($"Renaming the schema '{from}' to '{to}'");
                    schemas.Remove(from, out var schema);
                    schemas.Insert(index, to, schema);
                    ReplaceReferences(spec, from, to);
                }
            }
        }

        // The operationIds name the generated clients and methods ("Catalog_SearchAnime" becomes ICatalogClient.SearchAnimeAsync,
        // see operationGenerationMode in the .nswag config). MAL's own ids ("anime_anime_id_my_list_status_put") would give names
        // like My_list_statusPatchAsync. An operation missing here fails the task, so a new endpoint gets a deliberate name.
        private static readonly Dictionary<string, string> OperationIds = new()
        {
            ["GET /anime"] = "Catalog_SearchAnime",
            ["GET /anime/{anime_id}"] = "Catalog_GetAnime",
            ["GET /manga"] = "Catalog_SearchManga",
            ["GET /manga/{manga_id}"] = "Catalog_GetManga",
            ["GET /users/{user_name}"] = "Users_GetUser",
            ["PATCH /anime/{anime_id}/my_list_status"] = "Anime_UpdateListStatus",
            ["DELETE /anime/{anime_id}/my_list_status"] = "Anime_DeleteListStatus",
            ["GET /anime/ranking"] = "Anime_GetRanking",
            ["GET /anime/season/{year}/{season}"] = "Anime_GetSeasonal",
            ["GET /anime/suggestions"] = "Anime_GetSuggestions",
            ["PATCH /manga/{manga_id}/my_list_status"] = "Manga_UpdateListStatus",
            ["DELETE /manga/{manga_id}/my_list_status"] = "Manga_DeleteListStatus",
            ["GET /manga/ranking"] = "Manga_GetRanking",
            ["GET /forum/boards"] = "Forum_GetBoards",
            ["GET /forum/topic/{topic_id}"] = "Forum_GetTopic",
            ["GET /forum/topics"] = "Forum_GetTopics",
            ["GET /users/{user_name}/animelist"] = "Users_GetAnimeList",
            ["GET /users/{user_name}/mangalist"] = "Users_GetMangaList",
        };

        private static void SetOperationIds(BuildContext context, JsonNode spec)
        {
            foreach (var (path, pathItem) in spec["paths"]!.AsObject())
            {
                foreach (var (method, operation) in pathItem!.AsObject())
                {
                    if (operation is not JsonObject operationObject || method == "parameters")
                    {
                        continue;
                    }

                    var key = $"{method.ToUpperInvariant()} {path}";
                    operationObject["operationId"] = OperationIds.TryGetValue(key, out var operationId)
                        ? operationId
                        : throw new InvalidOperationException($"No operationId for '{key}'; add one to {nameof(OperationIds)}");
                }
            }

            context.Information($"Set {OperationIds.Count} operationIds");
        }

        // NSwag names parameter variables after x-originalName when it is set, and otherwise keeps MAL's snake_case
        // ("anime_id"). The query and path still use "name", so only the C# name changes (animeId).
        private static void SetParameterVariableNames(BuildContext context, JsonNode spec)
        {
            foreach (var (_, pathItem) in spec["paths"]!.AsObject())
            {
                // parameters are declared per operation, or for all operations of a path in a path-level "parameters" array
                foreach (var (key, node) in pathItem!.AsObject())
                {
                    var parameters = key == "parameters" ? node as JsonArray : node?["parameters"] as JsonArray;
                    foreach (var parameter in parameters ?? [])
                    {
                        parameter!["x-originalName"] = ToLowerCamelCase((string)parameter["name"]!);
                    }
                }
            }

            context.Information("Set the parameter variable names");

            static string ToLowerCamelCase(string name)
            {
                var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
                return parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
            }
        }

        // MAL's spec types the parameters and body fields that take a fixed set of values as plain strings. These named enums
        // replace them; x-enumNames sets the C# member names and the values are what MAL expects on the wire (checked against
        // the live API on 2026-10-17: unknown ranking types fail with 400, unknown sorts are ignored).
        // Response fields stay strings, so a value MAL adds later doesn't make the whole response fail to deserialize.
        private static readonly (string Name, (string Value, string Member)[] Values, string[] Targets)[] Enums =
        [
            ("AnimeRankingType",
                [("all", "All"), ("airing", "Airing"), ("upcoming", "Upcoming"), ("tv", "Tv"), ("ova", "Ova"), ("movie", "Movie"), ("special", "Special"), ("bypopularity", "ByPopularity"), ("favorite", "Favorite")],
                ["GET /anime/ranking ranking_type"]),
            ("MangaRankingType",
                [("all", "All"), ("manga", "Manga"), ("novels", "Novels"), ("lightnovels", "LightNovels"), ("oneshots", "OneShots"), ("doujin", "Doujin"), ("manhwa", "Manhwa"), ("manhua", "Manhua"), ("bypopularity", "ByPopularity"), ("favorite", "Favorite")],
                ["GET /manga/ranking ranking_type"]),
            ("SeasonalAnimeSort",
                [("anime_score", "Score"), ("anime_num_list_users", "NumListUsers")],
                ["GET /anime/season/{year}/{season} sort"]),
            ("ForumTopicsSort",
                [("recent", "Recent")],
                ["GET /forum/topics sort"]),
            ("AnimeWatchStatus",
                [("watching", "Watching"), ("completed", "Completed"), ("on_hold", "OnHold"), ("dropped", "Dropped"), ("plan_to_watch", "PlanToWatch")],
                ["GET /users/{user_name}/animelist status", "AnimeListStatusPatch.status"]),
            ("MangaReadStatus",
                [("reading", "Reading"), ("completed", "Completed"), ("on_hold", "OnHold"), ("dropped", "Dropped"), ("plan_to_read", "PlanToRead")],
                ["GET /users/{user_name}/mangalist status", "MangaListStatusPatch.status"]),
            ("UserAnimeListSort",
                [("list_score", "ListScore"), ("list_updated_at", "ListUpdatedAt"), ("anime_title", "AnimeTitle"), ("anime_start_date", "AnimeStartDate")],
                ["GET /users/{user_name}/animelist sort"]),
            ("UserMangaListSort",
                [("list_score", "ListScore"), ("list_updated_at", "ListUpdatedAt"), ("manga_title", "MangaTitle"), ("manga_start_date", "MangaStartDate")],
                ["GET /users/{user_name}/mangalist sort"]),
        ];

        // runs after PromoteInlineSchemas, so the PATCH bodies are named schemas ("AnimeListStatusPatch.status")
        private static void AddEnums(BuildContext context, JsonNode spec)
        {
            var schemas = spec["components"]!["schemas"]!.AsObject();

            foreach (var (name, values, targets) in Enums)
            {
                schemas[name] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray([.. values.Select(value => JsonValue.Create(value.Value))]),
                    ["x-enumNames"] = new JsonArray([.. values.Select(value => JsonValue.Create(value.Member))]),
                };

                foreach (var target in targets)
                {
                    var reference = new JsonObject { ["$ref"] = $"#/components/schemas/{name}" };

                    if (target.Split(' ') is [var method, var path, var parameterName])
                    {
                        var parameter = (spec["paths"]?[path]?[method.ToLowerInvariant()]?["parameters"] as JsonArray)?
                            .OfType<JsonObject>()
                            .FirstOrDefault(parameter => (string?)parameter["name"] == parameterName)
                            ?? throw new InvalidOperationException($"'{target}' for the enum '{name}' isn't in the spec");

                        parameter["schema"] = reference;
                    }
                    else
                    {
                        var (schemaName, propertyName) = (target.Split('.')[0], target.Split('.')[1]);
                        var properties = schemas[schemaName]?["properties"] as JsonObject;
                        if (properties?.ContainsKey(propertyName) != true)
                        {
                            throw new InvalidOperationException($"'{target}' for the enum '{name}' isn't in the spec");
                        }

                        properties[propertyName] = reference;
                    }
                }

                context.Information($"Added the enum '{name}' for {string.Join(", ", targets)}");
            }

            // the season parameter takes the same values as AnimeSeason.season, which PromoteInlineSchemas named Season
            var seasonParameter = spec["paths"]!["/anime/season/{year}/{season}"]!["get"]!["parameters"]!.AsArray()
                .OfType<JsonObject>()
                .Single(parameter => (string?)parameter["name"] == "season");
            seasonParameter["schema"] = new JsonObject { ["$ref"] = "#/components/schemas/Season" };

            // AnimeSeason.season itself is a response field, so it stays a string like the others, although MAL's spec gives it
            // the enum; MalJsonOptions logs a warning for a value outside Season instead of failing the whole response
            schemas["AnimeSeason"]!["properties"]!["season"] = new JsonObject { ["type"] = "string", ["nullable"] = false };
            context.Information("Kept AnimeSeason.season a string");
        }

        // MAL's spec wraps many schemas in an allOf with a single inline object (the forum schemas), or with a $ref plus an
        // object that only says "nullable" (e.g. AnimeForDetails.statistics). NSwag turns each of them into a base class
        // ("Anonymous", "Anonymous2", ...) or a subclass ("Statistics : AnimeStatistics"), so replace them with what they wrap.
        private static void FlattenSingleAllOf(BuildContext context, JsonNode spec)
        {
            var flattened = 0;

            Visit(spec["components"]);
            Visit(spec["paths"]);

            context.Information($"Flattened {flattened} allOf wrappers");

            void Visit(JsonNode? node)
            {
                switch (node)
                {
                    case JsonObject jsonObject:
                        if (jsonObject["allOf"] is JsonArray allOf)
                        {
                            var parts = allOf.Where(part => !IsNullableMarker(part)).ToArray();
                            if (parts.Length == 1 && parts[0] is JsonObject part)
                            {
                                jsonObject.Remove("allOf");
                                foreach (var (key, value) in part.ToArray())
                                {
                                    jsonObject[key] = value?.DeepClone();
                                }

                                flattened++;
                            }
                        }

                        foreach (var (_, child) in jsonObject.ToArray())
                        {
                            Visit(child);
                        }

                        break;

                    case JsonArray jsonArray:
                        foreach (var child in jsonArray)
                        {
                            Visit(child);
                        }

                        break;
                }
            }

            // every property is optional and therefore nullable in the generated code anyway
            static bool IsNullableMarker(JsonNode? part)
                => part is JsonObject jsonObject && jsonObject.All(property => property.Key is "nullable" or "type");
        }

        // NSwag names the classes of inline objects after their property ("Data", "Data2", "Anime_statistics") and gives
        // them no XML documentation, so move them into named schemas. "[]" stands for the property's array items.
        private static readonly (string Path, string Name)[] InlineSchemas =
        [
            ("AnimeList.data[]", "AnimeListEdge"),
            ("AnimeListForRanking.data[]", "AnimeRankingEdge"),
            ("MangaList.data[]", "MangaListEdge"),
            ("MangaListForRanking.data[]", "MangaRankingEdge"),
            ("List.paging", "Paging"),
            ("WorkForList.alternative_titles", "AlternativeTitles"),
            ("AnimeForList.broadcast", "Broadcast"),
            ("AnimeSeason.season", "Season"),
            ("AnimeStatistics.status", "AnimeStatusStatistics"),
            ("User.anime_statistics", "UserAnimeStatistics"),
        ];

        private static void PromoteInlineSchemas(BuildContext context, JsonNode spec)
        {
            var schemas = spec["components"]!["schemas"]!.AsObject();

            foreach (var (path, name) in InlineSchemas)
            {
                var segments = path.Split('.');
                var isItems = segments[1].EndsWith("[]", StringComparison.Ordinal);
                var propertyName = segments[1].TrimEnd('[', ']');

                var owner = FindPropertyOwner(schemas[segments[0]]!.AsObject(), propertyName)?["properties"]!.AsObject()
                    ?? throw new InvalidOperationException($"'{path}' isn't in the spec");

                var parent = isItems ? owner[propertyName]!["items"]!.AsObject() : null;
                var inline = parent ?? owner[propertyName]!.AsObject();
                if (inline.ContainsKey("$ref"))
                {
                    continue;
                }

                context.Information($"Moving '{path}' into the schema '{name}'");
                schemas[name] = inline.DeepClone();

                var reference = new JsonObject { ["$ref"] = $"#/components/schemas/{name}" };
                if (isItems)
                {
                    owner[propertyName]!["items"] = reference;
                }
                else
                {
                    owner[propertyName] = reference;
                }
            }

            // the form bodies of the list status PATCHes would become "Body" and "Body2"
            foreach (var (operation, name) in RequestBodySchemas)
            {
                var (method, path) = (operation.Split(' ')[0].ToLowerInvariant(), operation.Split(' ')[1]);
                foreach (var (_, content) in spec["paths"]![path]![method]!["requestBody"]!["content"]!.AsObject())
                {
                    if (content!["schema"] is JsonObject body && !body.ContainsKey("$ref"))
                    {
                        context.Information($"Moving the request body of '{operation}' into the schema '{name}'");
                        schemas[name] = body.DeepClone();
                        content["schema"] = new JsonObject { ["$ref"] = $"#/components/schemas/{name}" };
                    }
                }
            }
        }

        private static readonly (string Operation, string Name)[] RequestBodySchemas =
        [
            ("PATCH /anime/{anime_id}/my_list_status", "AnimeListStatusPatch"),
            ("PATCH /manga/{manga_id}/my_list_status", "MangaListStatusPatch"),
        ];

        // the forum's ForumTopicPaging has the same fields as the lists' paging, which PromoteInlineSchemas named Paging
        private static void UnifyPaging(BuildContext context, JsonNode spec)
        {
            var schemas = spec["components"]!["schemas"]!.AsObject();
            if (schemas.Remove("ForumTopicPaging"))
            {
                context.Information("Replacing ForumTopicPaging with Paging");
                ReplaceReferences(spec, "ForumTopicPaging", "Paging");
            }
        }

        // MAL's spec makes the PATCH bodies optional and types their tags as one string, while responses have an array of tags.
        // The live API takes the tags comma-separated, which is how MalFormUrlEncoder sends an array.
        // The bodies also lack start_date and finish_date, which the live API accepts (checked 2026-10-17): full or partial dates
        // (YYYY-MM-DD, YYYY-MM, YYYY), an empty value removes the date, anything else gets 400. Strings, like in the responses.
        // x-name names the body parameter (NSwag calls it "body" otherwise), and x-position puts it after the id.
        private static void FixListStatusPatches(BuildContext context, JsonNode spec)
        {
            var schemas = spec["components"]!["schemas"]!.AsObject();

            foreach (var (operation, name) in RequestBodySchemas)
            {
                var (method, path) = (operation.Split(' ')[0].ToLowerInvariant(), operation.Split(' ')[1]);
                var requestBody = spec["paths"]![path]![method]!["requestBody"]!;
                requestBody["required"] = true;
                requestBody["x-name"] = "listStatus";
                // a required body would come before the id, which has no position
                requestBody["x-position"] = 1;

                var properties = schemas[name]!["properties"]!.AsObject();
                properties["tags"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" },
                };

                foreach (var date in (string[])["start_date", "finish_date"])
                {
                    properties[date] ??= new JsonObject { ["type"] = "string" };
                }
            }

            context.Information("Made the list status bodies required, their tags arrays, and added their dates");
        }

        // MAL's spec has no required properties at all (it marks fields with "nullable: false" instead, which doesn't mean they
        // are always sent), so every generated property would be nullable. These are the properties MAL always sends, whatever
        // "fields" asks for (checked live on 2026-10-17). They become non-nullable, but nothing checks them while deserializing,
        // so a field MAL stops sending can't make a whole response fail.
        private static readonly Dictionary<string, string[]> RequiredProperties = new()
        {
            ["WorkBase"] = ["id", "title"],
            ["Picture"] = ["medium"],
            ["Genre"] = ["id", "name"],
            ["AnimeStudio"] = ["id", "name"],
            ["Magazine"] = ["id", "name"],
            ["PersonBase"] = ["id", "first_name", "last_name"],
            ["PersonRoleEdge"] = ["node", "role"],
            // role is missing on every manga checked, although the spec marks it "nullable: false"
            ["MangaMagazineRelationEdge"] = ["node"],
            ["AlternativeTitles"] = ["synonyms", "en", "ja"],
            ["AnimeSeason"] = ["year", "season"],
            ["Broadcast"] = ["day_of_the_week"],
            ["RankingInfo"] = ["rank"],
            ["AnimeListEdge"] = ["node"],
            ["MangaListEdge"] = ["node"],
            ["AnimeRankingEdge"] = ["node", "ranking"],
            ["MangaRankingEdge"] = ["node", "ranking"],
            ["UserAnimeListEdge"] = ["node"],
            ["UserMangaListEdge"] = ["node"],
            ["RelatedAnimeEdge"] = ["node", "relation_type", "relation_type_formatted"],
            ["RelatedMangaEdge"] = ["node", "relation_type", "relation_type_formatted"],
            ["AnimeRecommendation"] = ["node", "num_recommendations"],
            ["MangaRecommendation"] = ["node", "num_recommendations"],
            ["AnimeStatistics"] = ["num_list_users", "status"],
            ["AnimeStatusStatistics"] = ["watching", "completed", "on_hold", "dropped", "plan_to_watch"],
            ["AnimeListStatus"] = ["status", "score", "num_episodes_watched", "is_rewatching", "updated_at"],
            ["MangaListStatus"] = ["status", "score", "num_volumes_read", "num_chapters_read", "is_rereading", "updated_at"],
            ["AnimeTheme"] = ["id", "anime_id", "text"],
            ["AnimeVideo"] = ["id", "title", "url", "created_at", "updated_at", "thumbnail"],
            ["UserBase"] = ["id", "name"],
            ["ForumBoards"] = ["categories"],
            ["ForumCategory"] = ["title", "boards"],
            ["ForumBoard"] = ["id", "title", "description", "subboards"],
            ["ForumSubboard"] = ["id", "title"],
            ["ForumTopic"] = ["data", "paging"],
            ["ForumTopicDetails"] = ["title", "posts"],
            ["ForumTopicPoll"] = ["id", "question", "closed", "options"],
            ["ForumTopicPollOption"] = ["id", "text", "votes"],
            ["ForumTopicPost"] = ["id", "number", "created_at", "created_by", "body", "signature"],
            ["ForumPostAuthor"] = ["id", "name", "forum_avator"],
            ["ForumTopicList"] = ["data", "paging"],
            ["ForumTopicSummary"] = ["id", "title", "created_at", "created_by", "number_of_posts", "last_post_created_at", "last_post_created_by", "is_locked"],
            ["ForumUser"] = ["id", "name"],
        };

        private static void SetRequiredProperties(BuildContext context, JsonNode spec)
        {
            var schemas = spec["components"]!["schemas"]!.AsObject();

            // rebuilt from the table on every run, so a removed entry doesn't linger in the committed spec
            foreach (var (_, schema) in schemas)
            {
                schema!.AsObject().Remove("required");
                foreach (var part in schema["allOf"]?.AsArray().OfType<JsonObject>() ?? [])
                {
                    part.Remove("required");
                }
            }

            foreach (var (name, properties) in RequiredProperties)
            {
                var schema = schemas[name]?.AsObject() ?? throw new InvalidOperationException($"The schema '{name}' isn't in the spec");
                foreach (var property in properties)
                {
                    var owner = FindPropertyOwner(schema, property) ?? throw new InvalidOperationException($"'{name}.{property}' isn't in the spec");
                    if (owner["required"] is not JsonArray required)
                    {
                        owner["required"] = required = [];
                    }

                    required.Add(property);
                }
            }

            context.Information($"Marked the required properties of {RequiredProperties.Count} schemas");
        }

        // C# names that differ from the PascalCase form of the JSON name, read by Class.liquid; the JSON names stay MAL's.
        // forum_avator is MAL's spelling, and the PATCH body calls the episode count num_watched_episodes while responses call
        // it num_episodes_watched.
        private const string CSharpNameExtension = "x-csharp-name";

        private static readonly (string Path, string Name)[] PropertyNames =
        [
            ("ForumPostAuthor.forum_avator", "ForumAvatar"),
            ("AnimeListStatusPatch.num_watched_episodes", "NumEpisodesWatched"),
        ];

        private static void SetPropertyNames(BuildContext context, JsonNode spec)
        {
            var schemas = spec["components"]!["schemas"]!.AsObject();

            foreach (var (path, name) in PropertyNames)
            {
                var (schemaName, propertyName) = (path.Split('.')[0], path.Split('.')[1]);
                var owner = FindPropertyOwner(schemas[schemaName]!.AsObject(), propertyName) ?? throw new InvalidOperationException($"'{path}' isn't in the spec");
                owner["properties"]![propertyName]![CSharpNameExtension] = name;
            }

            context.Information($"Set {PropertyNames.Length} C# property names");
        }

        // The schemas that others extend through allOf; Class.liquid seals every other class, and these can't be sealed
        private const string BaseClassExtension = "x-base-class";

        private static void MarkBaseClasses(BuildContext context, JsonNode spec)
        {
            var schemas = spec["components"]!["schemas"]!.AsObject();

            var baseClasses = schemas
                .SelectMany(schema => schema.Value?["allOf"]?.AsArray() ?? [])
                .Select(part => (string?)part?["$ref"])
                .OfType<string>()
                .Select(reference => reference.Split('/').Last())
                .Distinct()
                .ToArray();

            foreach (var (name, schema) in schemas)
            {
                if (baseClasses.Contains(name))
                {
                    schema![BaseClassExtension] = true;
                }
                else
                {
                    schema!.AsObject().Remove(BaseClassExtension);
                }
            }

            context.Information($"Base classes: {string.Join(", ", baseClasses)}");
        }

        // Our own descriptions for the XML documentation (assets/SoftThorn.MAL.ApiClient_Documentation.json), added after
        // RemoveDocumentation dropped MAL's. Operations and parameters are looked up by "METHOD /path" and parameter name,
        // schemas by name, and properties by their path from the schema ("AnimeForList.start_season.year"), trying the
        // longest match first, so "status" can mean one thing in a list status and another one on an anime.
        private const string RemarksExtension = "x-remarks";

        private static void AddDocumentation(BuildContext context, JsonNode spec)
        {
            var documentation = JsonNode.Parse(File.ReadAllText(Constants.OpenApiDocumentationPath))!;
            var operations = documentation["operations"]!.AsObject();
            var parameters = documentation["parameters"]!.AsObject();
            var schemas = documentation["schemas"]!.AsObject();
            var properties = documentation["properties"]!.AsObject();
            // longer notes for single properties, keyed by their full path from the schema (no fallback to the bare name);
            // Class.liquid renders them as <remarks>
            var remarks = documentation["remarks"]!.AsObject();
            var missing = new List<string>();

            foreach (var (path, pathItem) in spec["paths"]!.AsObject())
            {
                foreach (var (method, operation) in pathItem!.AsObject())
                {
                    if (operation is not JsonObject operationObject || method == "parameters")
                    {
                        continue;
                    }

                    var key = $"{method.ToUpperInvariant()} {path}";
                    if (operations[key] is not JsonObject operationDocumentation)
                    {
                        missing.Add($"operation {key}");
                        continue;
                    }

                    operationObject["summary"] = (string?)operationDocumentation["summary"];

                    foreach (var parameter in operationObject["parameters"]?.AsArray() ?? [])
                    {
                        var name = (string)parameter!["name"]!;
                        var description = (string?)operationDocumentation["parameters"]?[name] ?? (string?)parameters[name];
                        if (description is null)
                        {
                            missing.Add($"parameter {name} of {key}");
                            continue;
                        }

                        parameter["description"] = description;
                    }
                }
            }

            foreach (var (name, schema) in spec["components"]!["schemas"]!.AsObject())
            {
                var schemaObject = schema!.AsObject();

                if ((string?)schemas[name] is { } description)
                {
                    schemaObject["description"] = description;
                }
                else
                {
                    missing.Add($"schema {name}");
                }

                DescribeProperties(schemaObject, name);
            }

            foreach (var entry in missing)
            {
                context.Warning($"No documentation for {entry}");
            }

            // inline objects become classes of their own, which take their description from the property (or array item)
            void DescribeProperties(JsonObject schema, string path)
            {
                foreach (var part in schema["allOf"]?.AsArray().OfType<JsonObject>() ?? [])
                {
                    DescribeProperties(part, path);
                }

                foreach (var (name, property) in schema["properties"]?.AsObject() ?? [])
                {
                    if (property is not JsonObject propertyObject)
                    {
                        continue;
                    }

                    var propertyPath = $"{path}.{name}";
                    var description = FindPropertyDescription(propertyPath);
                    if (description is null)
                    {
                        missing.Add($"property {propertyPath}");
                    }
                    else
                    {
                        propertyObject["description"] = description;
                    }

                    if ((string?)remarks[propertyPath] is { } remark)
                    {
                        propertyObject[RemarksExtension] = remark;
                    }

                    var item = propertyObject["items"] as JsonObject;
                    if (item?["properties"] is not null && description is not null)
                    {
                        item["description"] = description;
                    }

                    DescribeProperties(propertyObject, propertyPath);
                    if (item is not null)
                    {
                        DescribeProperties(item, propertyPath);
                    }
                }
            }

            string? FindPropertyDescription(string propertyPath)
            {
                var segments = propertyPath.Split('.');
                for (var start = 0; start < segments.Length; start++)
                {
                    if ((string?)properties[string.Join('.', segments[start..])] is { } description)
                    {
                        return description;
                    }
                }

                return null;
            }
        }

        // MAL's API License Agreement (3(a)(i)-(ii)) counts the API documentation as part of the API, which may not be
        // copied, modified or published. Keep only the structure (paths, parameters, schemas, enums) and drop MAL's prose
        // and sample content, so neither the committed spec nor the generated XML docs carry it.
        private static readonly string[] DocumentationKeys = ["description", "summary", "example", "examples", "x-code-samples", "x-logo", RemarksExtension];

        private static void RemoveDocumentation(BuildContext context, JsonNode spec)
        {
            var removed = RemoveDocumentation(spec, isPropertyMap: false);

            // OpenAPI requires a description on every response and scope, so keep the keys with empty values
            foreach (var (_, pathItem) in spec["paths"]!.AsObject())
            {
                foreach (var (_, operation) in pathItem!.AsObject())
                {
                    if (operation is JsonObject && operation["responses"] is JsonObject responses)
                    {
                        foreach (var (_, response) in responses)
                        {
                            if (response is JsonObject responseObject)
                            {
                                responseObject["description"] = "";
                            }
                        }
                    }
                }
            }

            foreach (var (_, scheme) in spec["components"]?["securitySchemes"]?.AsObject() ?? [])
            {
                foreach (var (_, flow) in scheme?["flows"]?.AsObject() ?? [])
                {
                    if (flow?["scopes"] is JsonObject scopes)
                    {
                        foreach (var name in scopes.Select(scope => scope.Key).ToArray())
                        {
                            scopes[name] = "";
                        }
                    }
                }
            }

            context.Information($"Removed {removed} documentation entries from the spec");
        }

        private static int RemoveDocumentation(JsonNode? node, bool isPropertyMap)
        {
            var removed = 0;

            switch (node)
            {
                case JsonObject jsonObject:
                    // in a schema's "properties" map the keys are field names, so a field called "description" must stay
                    if (!isPropertyMap)
                    {
                        foreach (var key in DocumentationKeys)
                        {
                            if (jsonObject.Remove(key))
                            {
                                removed++;
                            }
                        }
                    }

                    foreach (var (key, child) in jsonObject)
                    {
                        removed += RemoveDocumentation(child, isPropertyMap: !isPropertyMap && key == "properties");
                    }

                    break;

                case JsonArray jsonArray:
                    foreach (var child in jsonArray)
                    {
                        removed += RemoveDocumentation(child, isPropertyMap: false);
                    }

                    break;
            }

            return removed;
        }

        // the schema itself or the allOf part whose "properties" declare the property
        private static JsonObject? FindPropertyOwner(JsonObject schema, string propertyName)
            => new[] { schema }
                .Concat(schema["allOf"]?.AsArray().OfType<JsonObject>() ?? [])
                .FirstOrDefault(part => part["properties"] is JsonObject properties && properties.ContainsKey(propertyName));

        private static void ReplaceReferences(JsonNode? node, string from, string to)
        {
            switch (node)
            {
                case JsonObject jsonObject:
                    if ((string?)jsonObject["$ref"] == $"#/components/schemas/{from}")
                    {
                        jsonObject["$ref"] = $"#/components/schemas/{to}";
                    }

                    foreach (var (_, child) in jsonObject)
                    {
                        ReplaceReferences(child, from, to);
                    }

                    break;

                case JsonArray jsonArray:
                    foreach (var child in jsonArray)
                    {
                        ReplaceReferences(child, from, to);
                    }

                    break;
            }
        }

        private static void ReplaceArrayWithItem(BuildContext context, JsonNode? properties, string name)
        {
            if ((string?)properties?[name]?["type"] != "array")
            {
                return;
            }

            context.Information($"Changing '{name}' from an array to a single item");
            properties![name] = properties[name]!["items"]!.DeepClone();
        }
    }
}