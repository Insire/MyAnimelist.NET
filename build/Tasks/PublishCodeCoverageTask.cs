using Build.Util;
using Cake.Common.Build;
using Cake.Common.Build.AzurePipelines.Data;
using Cake.Common.IO;
using Cake.Common.Tools.ReportGenerator;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build.Tasks
{
    /// <summary>
    /// Publishes the Cobertura report to the Azure Pipelines Code Coverage tab, also when tests failed.
    /// The tab shows an HTML report built with ReportGenerator's Azure Pipelines variant, which inlines its scripts and styles.
    /// </summary>
    [TaskName("PublishCodeCoverage")]
    [IsDependentOn(typeof(CoberturaReportTask))]
    public sealed class PublishCodeCoverageTask : FrostingTask<BuildContext>
    {
        private const string AzureHtmlSubFolder = "html-azure";

        public override void Run(BuildContext context)
        {
            var reportDirectory = context.ReportsFolder.Combine(AzureHtmlSubFolder);

            context.GenerateReport(context.CoberturaResultFile, ReportGeneratorReportType.HtmlInline_AzurePipelines, AzureHtmlSubFolder);

            context.AzurePipelines().Commands.PublishCodeCoverage(new AzurePipelinesPublishCodeCoverageData
            {
                CodeCoverageTool = AzurePipelinesCodeCoverageToolType.Cobertura,
                SummaryFileLocation = context.CoberturaResultFile,
                ReportDirectory = reportDirectory,
            });

            context.Log.Information("Published the code coverage of {0}.", context.CoberturaResultFile);

            base.Run(context);
        }

        public override bool ShouldRun(BuildContext context)
        {
            if (!context.BuildSystem().IsRunningOnAzurePipelines)
            {
                context.Log.Information($"Skipped {nameof(PublishCodeCoverageTask)}, since task is not running on Azure.");
                return false;
            }

            if (!context.FileExists(context.CoberturaResultFile))
            {
                context.Log.Information($"Skipped {nameof(PublishCodeCoverageTask)}, since {context.CoberturaResultFile} wasn't created.");
                return false;
            }

            return base.ShouldRun(context);
        }
    }
}
