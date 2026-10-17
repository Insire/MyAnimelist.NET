using Build.Util;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build.Tasks
{
    /// <summary>
    /// Tags the released commit as v{version} and pushes the tag, so the next release notes start after it.
    /// </summary>
    [TaskName("TagRelease")]
    [IsDependentOn(typeof(PushRemoteTask))]
    public sealed class TagReleaseTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            var tag = $"v{context.GitVersion?.SemVer2}";

            // a lightweight tag, since annotated tags need a git identity, which build agents don't have
            context.RunGitOrThrow($"tag {tag}");
            context.RunGitOrThrow($"push origin {tag}"); // needs persistCredentials: true on the checkout step

            context.Log.Information("Tagged and pushed {0}.", tag);

            base.Run(context);
        }

        public override bool ShouldRun(BuildContext context)
        {
            if (!context.PackagesPushedToNuGet)
            {
                context.Log.Information($"Skipped {nameof(TagReleaseTask)}, since no package was pushed to nuget.org.");
                return false;
            }

            return base.ShouldRun(context);
        }
    }
}
