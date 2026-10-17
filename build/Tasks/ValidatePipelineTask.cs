using Build.Util;
using Cake.Common.IO;
using Cake.Common.Net;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Tasks
{
    /// <summary>
    /// Checks azure-pipelines.yml against the Azure Pipelines JSON schema, so a broken pipeline shows up before it is pushed.
    /// The schema only covers the file's structure: Azure's own check (Validate in the pipeline editor) also resolves templates, variable groups and service connections.
    /// The pinned schema is downloaded once into tools/ and checked against its SHA-256.
    /// </summary>
    [TaskName("ValidatePipeline")]
    public sealed class ValidatePipelineTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            var schemaPath = GetSchema(context);
            var pipelinePath = context.MakeAbsolute(new FilePath(Constants.AzurePipelinesPath));

            var errors = PipelineSchema.Validate(File.ReadAllText(schemaPath.FullPath), File.ReadAllText(pipelinePath.FullPath));
            if (errors.Count > 0)
            {
                // The schema tries every pipeline layout (steps, jobs, stages, extends), so most messages come from layouts the file doesn't use.
                // The messages closest to the root usually name the mistake, the full list shows with --verbosity=diagnostic.
                var depth = errors.Where(error => !error.StartsWith("/:", StringComparison.Ordinal)).Select(Depth).DefaultIfEmpty(0).Min();
                foreach (var error in errors)
                {
                    if (Depth(error) == depth)
                    {
                        context.Log.Error(error);
                    }
                    else
                    {
                        context.Log.Debug(error);
                    }
                }

                throw new CakeException($"{Constants.AzurePipelinesPath} doesn't match the Azure Pipelines schema. Run with --verbosity=diagnostic for all {errors.Count} messages.");
            }

            context.Log.Information("{0} matches the Azure Pipelines schema.", Constants.AzurePipelinesPath);

            base.Run(context);
        }

        // the number of segments in the instance path of a message like "/jobs/0/steps: ..."
        private static int Depth(string error) => error[..error.IndexOf(": ", StringComparison.Ordinal)].Count(c => c == '/');

        private static FilePath GetSchema(BuildContext context)
        {
            var path = context.MakeAbsolute(new DirectoryPath("./tools/azure-pipelines-schema").Combine(Constants.AzurePipelinesSchemaCommit).CombineWithFilePath("service-schema.json"));

            if (!context.FileExists(path) || BuildContextExtensions.GetSha256(path) != Constants.AzurePipelinesSchemaSha256)
            {
                context.EnsureDirectoryExists(path.GetDirectory());
                context.DownloadFile($"https://raw.githubusercontent.com/microsoft/azure-pipelines-vscode/{Constants.AzurePipelinesSchemaCommit}/service-schema.json", path);

                var actualSha256 = BuildContextExtensions.GetSha256(path);
                if (actualSha256 != Constants.AzurePipelinesSchemaSha256)
                {
                    context.DeleteFile(path);
                    throw new CakeException($"The Azure Pipelines schema download has the SHA-256 {actualSha256}, expected {Constants.AzurePipelinesSchemaSha256}.");
                }
            }

            return path;
        }
    }
}
