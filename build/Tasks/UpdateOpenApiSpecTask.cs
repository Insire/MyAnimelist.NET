using Build.Util;
using Cake.Common;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;
using HtmlAgilityPack;
using Jint;
using System.Text.Json;

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

            var json = JsonSerializer.Serialize(openapiSpec, options: _options);

            File.WriteAllText(Constants.OpenApiSpecPath, json);

            var dotnetExe = context.Tools.Resolve(context.DotnetExeName);
            var nswagExe = context.Tools.Resolve("dotnet-nswag.dll");

            var settings = new ProcessSettings()
                .UseWorkingDirectory(".")
                .WithArguments(builder => builder
                    .AppendQuoted(nswagExe.FullPath)
                    .Append("run")
                    .AppendQuoted(Constants.NswagConfigFilePath)
                );

            context.StartProcess(dotnetExe.FullPath, settings);
        }
    }
}