using Build.Util;
using Cake.Common;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Tasks
{
    [TaskName("Test")]
    [IsDependentOn(typeof(CleanSolutionTask))]
    public sealed class TestTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            var settings = new ProcessSettings()
                .UseWorkingDirectory(".")
                .WithArguments(builder => builder
                    .Append("test")
                    .AppendQuoted(Constants.SolutionPath)
                    .Append($"--configuration {Constants.Configuration}")
                    .Append("--no-restore")
                    .Append("--nologo")
                    .Append("--filter \"TestCategory=Unit-Test|TestCategory=Integration-Test\"")
                    .AppendSwitchQuoted("--results-directory", context.CoberturaFolder.FullPath)
                    .Append("-p:DebugType=full") // required for opencover codecoverage and sourcelinking
                    .Append("-p:DebugSymbols=true") // required for opencover codecoverage
                    .AppendSwitchQuoted("--collect", ":", "\"\"Code Coverage\"\"")
                    .Append($"--logger:trx;LogFileName={context.ResultsFolder.CombineWithFilePath("vsTestResults.trx").FullPath}")
                );

            context.StartProcess(context.Tools.Resolve("dotnet.exe"), settings);
        }
    }
}