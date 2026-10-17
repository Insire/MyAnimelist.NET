using Cake.Frosting;

namespace Build.Tasks
{
    [TaskName("TestAndUploadReport")]
    [IsDependentOn(typeof(PublishTestResultsTask))] // first, so the results are published even if a report task throws
    [IsDependentOn(typeof(HtmlReportTask))]
    [IsDependentOn(typeof(UploadCodecovReportTask))]
    public sealed class TestAndUploadReportTask : FrostingTask<BuildContext>
    {
    }
}
