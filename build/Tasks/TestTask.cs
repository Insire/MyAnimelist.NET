using Build.Util;
using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Tasks
{
    [TaskName("Test")]
    [IsDependentOn(typeof(CleanSolutionTask))]
    [IsDependentOn(typeof(NuGetRestoreTask))] // the clean removes obj/, and "dotnet test --no-restore" finds no test projects without it
    public sealed class TestTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            var settings = new ProcessSettings()
                .UseWorkingDirectory(".")
                .WithArguments(builder => builder
                    // global.json opts dotnet test into Microsoft.Testing.Platform, so these are MTP/xUnit v3 options
                    .Append("test")
                    .AppendSwitchQuoted("--solution", Constants.SolutionPath)
                    .Append($"--configuration {Constants.Configuration}")
                    .Append("--no-restore")
                    .AppendSwitchQuoted("--results-directory", context.CoberturaFolder.FullPath)
                    .Append("-p:DebugType=full") // required for codecoverage and sourcelinking
                    .Append("-p:DebugSymbols=true") // required for codecoverage
                    .AppendSwitchQuoted("--filter-trait", "TestCategory=Unit-Test")
                    .AppendSwitchQuoted("--filter-trait", "TestCategory=Integration-Test")
                    .Append("--coverage") // writes *.coverage files, merged by ConvertCoverageTask
                    .Append("--report-xunit-trx")
                );

            var exitCode = context.StartProcess(context.Tools.Resolve(context.DotnetExeName), settings);
            if (exitCode != 0)
            {
                // keep going so the coverage reports are still generated; uploads and pushes check this flag, and teardown fails the build
                context.TestsAreFailing = true;
                context.Error($"Tests failed with exit code {exitCode}.");
            }
        }
    }
}