using Cake.Frosting;

namespace Build.Tasks
{
    [TaskName("TestAndUploadReport")]
    [IsDependentOn(typeof(HtmlReportTask))]
    [IsDependentOn(typeof(UploadCodecovReportTask))]
    public sealed class TestAndUploadReportTask : FrostingTask<BuildContext>
    {
    }
}
