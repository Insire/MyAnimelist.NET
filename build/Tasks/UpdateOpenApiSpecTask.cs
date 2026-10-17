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
            RemoveDocumentation(context, spec);

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

        // MAL's API License Agreement (3(a)(i)-(ii)) counts the API documentation as part of the API, which may not be
        // copied, modified or published. Keep only the structure (paths, parameters, schemas, enums) and drop MAL's prose
        // and sample content, so neither the committed spec nor the generated XML docs carry it.
        private static readonly string[] DocumentationKeys = ["description", "summary", "example", "examples", "x-code-samples", "x-logo"];

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