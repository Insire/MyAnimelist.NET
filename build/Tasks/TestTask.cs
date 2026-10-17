using Build.Util;
using Cake.Common;
using Cake.Common.Build;
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
            var runIntegrationTests = CanRunIntegrationTests(context);
            context.IntegrationTestsSkipped = !runIntegrationTests;

            var settings = new ProcessSettings()
                .UseWorkingDirectory(".")
                .WithArguments(builder => builder
                    // global.json opts dotnet test into Microsoft.Testing.Platform, so these are MTP/xUnit v3 options
                    .Append("test")
                    // a filter that matches no test in a project fails the run (exit code 8), so skipping the integration tests means leaving out their project
                    .AppendSwitchQuoted(runIntegrationTests ? "--solution" : "--project", runIntegrationTests ? Constants.SolutionPath : Constants.UnitTestProjectPath)
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

        /// <summary>
        /// On Azure, the integration tests get MAL's credentials from secret variables, which builds of forks don't receive.
        /// Without them every integration test fails at login, so such a build runs the unit tests only and says so in a warning.
        /// </summary>
        private static bool CanRunIntegrationTests(BuildContext context)
        {
            if (!context.BuildSystem().IsRunningOnAzurePipelines)
            {
                return true; // locally the credentials come from user secrets
            }

            var missing = Constants.IntegrationTestSecrets.Where(name => context.GetSecretEnvironmentVariable(name) is null).ToArray();
            if (missing.Length == 0)
            {
                return true;
            }

            var message = $"Skipped the integration tests, since these environment variables are missing or unresolved: {string.Join(", ", missing)}. Run them locally before approving the pull request.";
            context.Warning(message);
            context.AzurePipelines().Commands.WriteWarning(message);
            return false;
        }
    }
}