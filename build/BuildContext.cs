using Build.Util;
using Cake.Common.Build;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Common.Tools.ReportGenerator;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;
using Nerdbank.GitVersioning;

namespace Build
{
    public class BuildContext : FrostingContext
    {
        public string DotnetExeName { get; }

        public DirectoryPath ReportsFolder { get; }
        public DirectoryPath ResultsFolder { get; }
        public DirectoryPath PackagesFolder { get; }
        public DirectoryPath CoberturaFolder { get; }
        public FilePath CoberturaResultFile { get; }
        public DirectoryPath ReleaseNotesFolder { get; }
        public FilePath ReleaseNotesMarkdownFile { get; }
        public FilePath ReleaseNotesTextFile { get; }

        public ReportGeneratorSettings ReportGeneratorSettings { get; }

        public VersionOracle? GitVersion { get; internal set; }
        public string? Branch { get; internal set; }
        public string? Commit { get; internal set; }

        public bool TestsAreFailing { get; internal set; }
        public bool IntegrationTestsSkipped { get; internal set; }
        public bool IsPublicRelease { get; internal set; }
        public bool PackagesPushedToNuGet { get; internal set; }

        public BuildContext(ICakeContext context)
            : base(context)
        {
            ReportsFolder = this.MakeAbsolute(new DirectoryPath(Constants.ReportsPath));
            ResultsFolder = this.MakeAbsolute(new DirectoryPath(Constants.ResultsPath));
            PackagesFolder = this.MakeAbsolute(new DirectoryPath(Constants.PackagesPath));
            CoberturaFolder = this.MakeAbsolute(new DirectoryPath(Constants.CoberturaPath));

            CoberturaResultFile = CoberturaFolder.CombineWithFilePath("Cobertura.xml");
            ReleaseNotesFolder = ResultsFolder.Combine("release-notes"); // published as the "release-notes" pipeline artifact
            ReleaseNotesMarkdownFile = ReleaseNotesFolder.CombineWithFilePath("release-notes.md");
            ReleaseNotesTextFile = ReleaseNotesFolder.CombineWithFilePath("release-notes.txt");

            this.Information($"Provider: {this.BuildSystem().Provider}");
            this.Information($"Platform: {Environment.Platform.Family} ({(Environment.Platform.Is64Bit ? "x64" : "x86")})");

            ReportGeneratorSettings = new ReportGeneratorSettings()
            {
                AssemblyFilters = new[]
                {
                    "-SoftThorn.MAL.ApiClient.*Tests*",
                    "-xunit*"
                },
                ClassFilters = new[]
                {
                    "-System*",
                    "-Microsoft*",
                }
            };

            DotnetExeName = Environment.Platform.IsWindows() ? "dotnet.exe" : "dotnet";
        }
    }
}