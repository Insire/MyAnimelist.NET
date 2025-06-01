using Cake.Frosting;

namespace Build.Tasks
{
    [TaskName("Push")]
    [IsDependentOn(typeof(PushRemoteTask))]
    [IsDependentOn(typeof(PushLocallyTask))]
    public sealed class PushTask : FrostingTask<BuildContext>
    {
    }
}
