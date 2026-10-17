using Json.Schema;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Build.Util
{
    /// <summary>
    /// Checks a pipeline YAML file against the Azure Pipelines JSON schema (service-schema.json from microsoft/azure-pipelines-vscode).
    /// </summary>
    internal static partial class PipelineSchema
    {
        /// <summary>
        /// Returns no errors for a valid pipeline, otherwise the schema's messages as "instance path: message".
        /// </summary>
        public static IReadOnlyList<string> Validate(string schemaJson, string pipelineYaml)
        {
            var schema = JsonSchema.FromText(schemaJson);

            var yaml = new YamlStream();
            yaml.Load(new StringReader(pipelineYaml));
            var root = yaml.Documents.Count == 0 ? null : ToJson(yaml.Documents[0].RootNode);
            if (root is null)
            {
                return ["The pipeline file is empty."];
            }

            // Azure reads every YAML scalar as a string and converts it where a boolean or number is expected, while the schema types some
            // values as strings (timeoutInMinutes, variable values) and others as booleans (most task inputs). So a scalar the schema
            // rejects only for its type is converted to the type it asks for, and the file is checked again, until nothing changes.
            for (var pass = 0; ; pass++)
            {
                var result = schema.Evaluate(JsonDocument.Parse(root.ToJsonString()).RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
                if (result.IsValid)
                {
                    return [];
                }

                var errors = (result.Details ?? [])
                    .Where(detail => detail.Errors is not null)
                    .SelectMany(detail => detail.Errors!.Select(error => (Path: detail.InstanceLocation.ToString(), Message: error.Value)))
                    .ToList();

                var changed = false;
                foreach (var (path, message) in errors)
                {
                    var match = TypeMismatch().Match(message);
                    if (match.Success && Find(root, path) is JsonValue value && Coerce(value, match.Groups["expected"].Value))
                    {
                        changed = true;
                    }
                }

                if (!changed || pass == 20)
                {
                    return errors.Select(error => $"{(error.Path.Length == 0 ? "/" : error.Path)}: {error.Message}").Distinct().ToList();
                }
            }
        }

        // a plain YAML scalar becomes a boolean, number or null where it looks like one, a quoted one stays a string
        private static JsonNode? ToJson(YamlNode node) => node switch
        {
            YamlMappingNode mapping => new JsonObject(mapping.Children.Select(child => KeyValuePair.Create(((YamlScalarNode)child.Key).Value ?? "", ToJson(child.Value)))),
            YamlSequenceNode sequence => new JsonArray(sequence.Children.Select(ToJson).ToArray()),
            YamlScalarNode { Style: ScalarStyle.Plain, Value: null or "" or "~" or "null" } => null,
            YamlScalarNode { Style: ScalarStyle.Plain, Value: "true" or "True" } => JsonValue.Create(true),
            YamlScalarNode { Style: ScalarStyle.Plain, Value: "false" or "False" } => JsonValue.Create(false),
            YamlScalarNode { Style: ScalarStyle.Plain } scalar when long.TryParse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => JsonValue.Create(number),
            YamlScalarNode scalar => JsonValue.Create(scalar.Value),
            _ => null,
        };

        private static bool Coerce(JsonValue value, string expected)
        {
            JsonNode? replacement = null;
            if (expected == "string")
            {
                replacement = value.GetValueKind() == JsonValueKind.String ? null : JsonValue.Create(value.ToJsonString());
            }
            else if (value.TryGetValue<string>(out var text))
            {
                replacement = expected switch
                {
                    "boolean" when bool.TryParse(text, out var flag) => JsonValue.Create(flag),
                    "integer" or "number" when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => JsonValue.Create(number),
                    _ => null,
                };
            }

            if (replacement is null)
            {
                return false;
            }

            switch (value.Parent)
            {
                case JsonObject parent:
                    parent[value.GetPropertyName()] = replacement;
                    return true;

                case JsonArray parent:
                    parent[value.GetElementIndex()] = replacement;
                    return true;

                default:
                    return false;
            }
        }

        // resolves a JSON pointer like /jobs/0/steps/1/env
        private static JsonNode? Find(JsonNode root, string pointer)
        {
            JsonNode? node = root;
            foreach (var segment in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                var key = segment.Replace("~1", "/").Replace("~0", "~");
                node = node switch
                {
                    JsonArray array when int.TryParse(key, CultureInfo.InvariantCulture, out var index) && index < array.Count => array[index],
                    JsonObject obj when obj.TryGetPropertyValue(key, out var child) => child,
                    _ => null,
                };

                if (node is null)
                {
                    return null;
                }
            }

            return node;
        }

        [GeneratedRegex("""Value is "[a-z]+" but should be "(?<expected>[a-z]+)"$""")]
        private static partial Regex TypeMismatch();
    }
}
