using Build.Util;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Tasks
{
    [TaskName("BuildAndPack")]
    [IsDependentOn(typeof(UpdateAssemblyInfoTask))] // the package's assembly gets its version attributes from SharedAssemblyInfo.cs
    [IsDependentOn(typeof(CleanSolutionAgainTask))]
    [IsDependentOn(typeof(NuGetRestoreTask))]
    [IsDependentOn(typeof(ReleaseNotesTask))]
    public sealed class BuildAndPackTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            var settings = new ProcessSettings()
                .UseWorkingDirectory(".")
                .WithArguments(builder => builder
                    .Append("pack")
                    .AppendQuoted(Constants.ProjectPath)
                    .Append($"-c {Constants.Configuration}")
                    .Append($"--output \"{Constants.PackagesPath}\"")
                    .Append($"-p:PackageVersion={context.GitVersion?.SemVer2}")
                    .Append($"-p:PublicRelease={context.IsPublicRelease}") // Nerdbank.GitVersioning - omit git commit ID
                    .Append($"-p:PackageReleaseNotesFile=\"{context.ReleaseNotesTextFile.FullPath}\"") // read by src/SoftThorn.MAL.ApiClient.csproj

                    // Creating symbol packages
                    .Append("-p:IncludeSymbols=true")
                    .Append("-p:SymbolPackageFormat=snupkg")

                    // enable source linking
                    .Append("-p:PublishRepositoryUrl=true")

                    // Deterministic Builds
                    .Append("-p:EmbedUntrackedSources=true")
                );

            context.RunProcess(context.Tools.Resolve(context.DotnetExeName), settings);

            base.Run(context);
        }
    }
}