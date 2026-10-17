using Build.Util;
using Cake.Common.Build;
using Cake.Common.Build.AzurePipelines.Data;
using Cake.Common.IO;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build.Tasks
{
    /// <summary>
    /// Publishes the .trx files of the test run to the Azure Pipelines Tests tab, also when tests failed.
    /// </summary>
    [TaskName("PublishTestResults")]
    [IsDependentOn(typeof(TestTask))]
    public sealed class PublishTestResultsTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            var files = context.GetFiles(System.IO.Path.Combine(context.CoberturaFolder.FullPath, "**", "*.trx"));

            context.AzurePipelines().Commands.PublishTestResults(new AzurePipelinesPublishTestResultsData
            {
                TestRunner = AzurePipelinesTestRunnerType.VSTest, // xUnit v3 writes VSTest's trx format
                TestResultsFiles = files.ToList(),
                Configuration = Constants.Configuration,
            });

            context.Log.Information("Published {0} test result file(s).", files.Count);

            base.Run(context);
        }

        public override bool ShouldRun(BuildContext context)
        {
            if (!context.BuildSystem().IsRunningOnAzurePipelines)
            {
                context.Log.Information($"Skipped {nameof(PublishTestResultsTask)}, since task is not running on Azure.");
                return false;
            }

            if (context.GetFiles(System.IO.Path.Combine(context.CoberturaFolder.FullPath, "**", "*.trx")).Count == 0)
            {
                context.Log.Information($"Skipped {nameof(PublishTestResultsTask)}, since there is no trx file in {context.CoberturaFolder.FullPath}");
                return false;
            }

            return base.ShouldRun(context);
        }
    }
}
