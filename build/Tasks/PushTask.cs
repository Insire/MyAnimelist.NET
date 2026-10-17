using Cake.Frosting;

namespace Build.Tasks
{
    [TaskName("Push")]
    [IsDependentOn(typeof(PublishArtifactsTask))] // before the pushes, so a failed push still leaves the artifacts
    [IsDependentOn(typeof(PushRemoteTask))]
    [IsDependentOn(typeof(TagReleaseTask))]
    [IsDependentOn(typeof(PushLocallyTask))]
    public sealed class PushTask : FrostingTask<BuildContext>
    {
    }
}
