using Build.Util;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Tasks
{
    [TaskName("NuGetRestore")]
    public sealed class NuGetRestoreTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            var settings = new ProcessSettings()
                .UseWorkingDirectory(".")
                .WithArguments(builder => builder
                    .Append("restore")
                    .AppendQuoted(Constants.SolutionPath)
                    .AppendSwitchQuoted("--configfile", Constants.NuGetConfigPath)
                    .Append($"-p:Configuration={Constants.Configuration}")
                    .Append("--force")
                    .Append("--force-evaluate")
                );

            context.RunProcess(context.Tools.Resolve(context.DotnetExeName), settings);

            base.Run(context);
        }
    }
}