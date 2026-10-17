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
            var web = new HtmlWeb();
            var document = web.Load("https://myanimelist.net/apiconfig/references/api/v2");

            var nodes = document.DocumentNode.Descendants("script");

            var scriptNode = nodes.Last()?.InnerText
                .Replace("var container = document.getElementById('redoc');", "")
                .Replace("Redoc.hydrate(__redoc_state, container);", "");

            var openapiSpec = new Engine()
                .Execute(scriptNode!)
                .Evaluate("__redoc_state.spec.data").ToObject();

            var spec = JsonSerializer.SerializeToNode(openapiSpec, options: _options)!;
            FixPathParameterNames(context, spec);
            FixForumTopic(context, spec);

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