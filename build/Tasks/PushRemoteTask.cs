using Build.Util;
using Cake.Common;
using Cake.Common.Build;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Tasks
{
    [TaskName("PushRemote")]
    [IsDependentOn(typeof(BuildAndPackTask))]
    public sealed class PushRemoteTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            foreach (var path in context.GetFiles(System.IO.Path.Combine(context.PackagesFolder.FullPath, "*.nupkg")))
            {
                var settings = new ProcessSettings()
                    .UseWorkingDirectory(".")
                    .WithArguments(builder => builder
                        .Append("nuget push")
                        .AppendQuoted(path.FullPath)
                        .AppendSwitchSecret("--api-key", context.EnvironmentVariable("NUGETORG_APIKEY"))
                        .AppendSwitchQuoted("--source", "https://api.nuget.org/v3/index.json")
                    );

                context.RunProcess(context.Tools.Resolve(context.DotnetExeName), settings);
            }

            context.PackagesPushedToNuGet = true;

            base.Run(context);
        }

        public override bool ShouldRun(BuildContext context)
        {
            if (!context.BuildSystem().IsRunningOnAzurePipelines)
            {
                context.Log.Information($"Skipped {nameof(PushRemoteTask)}, since task is not running on Azure.");
                return false;
            }

            if (string.IsNullOrEmpty(context.EnvironmentVariable("NUGETORG_APIKEY")))
            {
                context.Log.Information($"Skipped {nameof(PushRemoteTask)}, since environment variable NUGETORG_APIKEY missing or empty.");
                return false;
            }

            if (context.GetFiles(System.IO.Path.Combine(context.PackagesFolder.FullPath, "*.nupkg")).Count == 0)
            {
                context.Log.Information($"Skipped {nameof(PushRemoteTask)}, since there is no nupkg file in {context.PackagesFolder.FullPath}");
                return false;
            }

            if (context.TestsAreFailing)
            {
                context.Log.Information($"Skipped {nameof(PushRemoteTask)}, since we got failing tests.");
                return false;
            }

            return base.ShouldRun(context);
        }
    }
}
