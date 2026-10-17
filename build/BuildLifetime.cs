using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Core;
using Cake.Frosting;
using Cake.GitVersioning;

namespace Build
{
    public sealed class BuildLifetime : FrostingLifetime<BuildContext>
    {
        public override void Setup(BuildContext context, ISetupContext info)
        {
            var versionOracle = context.GitVersioningGetVersion();
            context.GitVersion = versionOracle;
            // BuildingRef comes from the CI variables (BUILD_SOURCEBRANCH on Azure), since the checkout there is a detached HEAD
            context.Branch = versionOracle.BuildingRef?.Replace("refs/heads/", "");
            context.Commit = versionOracle.GitCommitId;

            // Nerdbank.GitVersioning matches the current ref against publicReleaseRefSpec in version.json (main and v<major>.<minor> branches)
            context.IsPublicRelease = versionOracle.PublicRelease;

            context.Information("Branch: {0}", context.Branch);

            if (context.IsPublicRelease)
            {
                context.Information("Building a {0} release.", "public");
            }
            else
            {
                context.Information("Building a {0}release.", "pre-");
            }

            context.Information($"{context.DotnetExeName} ({context.Tools.Resolve(context.DotnetExeName)}) {(context.FileExists(context.Tools.Resolve(context.DotnetExeName)) ? "was found" : "is missing")}.");
            context.Information($"dotnet-coverage ({context.Tools.Resolve("dotnet-coverage.dll")}) {(context.FileExists(context.Tools.Resolve("dotnet-coverage.dll")) ? "was found" : "is missing")}.");

            context.Information($"NUGETORG_APIKEY was{(string.IsNullOrEmpty(context.EnvironmentVariable("NUGETORG_APIKEY")) ? " not" : "")} set.");
            context.Information($"CODECOV_TOKEN was{(string.IsNullOrEmpty(context.EnvironmentVariable("CODECOV_TOKEN")) ? " not" : "")} set.");

            context.Information("reportsFolder: {0}", context.ReportsFolder.FullPath);
            context.Information("coberturaResultFile: {0}", context.CoberturaResultFile.FullPath);

            context.Information("dotnet tool: {0}", context.Tools.Resolve(context.DotnetExeName));
        }

        public override void Teardown(BuildContext context, ITeardownContext info)
        {
            if (context.TestsAreFailing)
            {
                throw new CakeException(1, "The build failed, because tests failed.");
            }
        }
    }
}