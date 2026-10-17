using Cake.Common.Build;
using Cake.Common.IO;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build.Tasks
{
    /// <summary>
    /// Uploads the packages and the release notes as artifacts of the Azure Pipelines run, and shows the Markdown release notes on its summary page.
    /// Runs before the push, so a failed push still leaves them for inspection.
    /// </summary>
    [TaskName("PublishArtifacts")]
    [IsDependentOn(typeof(BuildAndPackTask))]
    public sealed class PublishArtifactsTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            var commands = context.AzurePipelines().Commands;

            commands.UploadArtifactDirectory(context.PackagesFolder, "packages"); // .nupkg and .snupkg
            commands.UploadArtifactDirectory(context.ReleaseNotesFolder, "release-notes"); // release-notes.md and release-notes.txt
            commands.UploadTaskSummary(context.ReleaseNotesMarkdownFile);

            context.Log.Information("Published the packages and release notes artifacts.");

            base.Run(context);
        }

        public override bool ShouldRun(BuildContext context)
        {
            if (!context.BuildSystem().IsRunningOnAzurePipelines)
            {
                context.Log.Information($"Skipped {nameof(PublishArtifactsTask)}, since task is not running on Azure.");
                return false;
            }

            return base.ShouldRun(context);
        }
    }
}
