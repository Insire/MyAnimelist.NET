using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Solution;
using Cake.Common.Tools.ReportGenerator;
using Cake.Core;
using Cake.Core.IO;
using Cake.Incubator.Project;
using System.Xml.Linq;

namespace Build.Util
{
    internal static class BuildContextExtensions
    {
        /// <summary>
        /// Starts a process and fails the task when it exits with a non-zero exit code.
        /// </summary>
        public static void RunProcess(this BuildContext context, FilePath fileName, ProcessSettings settings)
        {
            var exitCode = context.StartProcess(fileName, settings);
            if (exitCode != 0)
            {
                throw new CakeException(exitCode, $"{fileName.GetFilename()} failed with exit code {exitCode}.");
            }
        }

        /// <summary>
        /// Runs git from the repository root and returns its exit code and standard output.
        /// </summary>
        public static (int ExitCode, string Output) RunGit(this BuildContext context, string arguments)
        {
            var settings = new ProcessSettings()
            {
                Arguments = arguments,
                WorkingDirectory = ".",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var exitCode = context.StartProcess("git", settings, out var output);
            return (exitCode, string.Join("\n", output));
        }

        public static void RunGitOrThrow(this BuildContext context, string arguments)
        {
            context.RunProcess("git", new ProcessSettings().UseWorkingDirectory(".").WithArguments(builder => builder.Append(arguments)));
        }

        /// <summary>
        /// Returns a property from Directory.Build.props, the single source of the package and assembly metadata.
        /// </summary>
        public static string GetDirectoryBuildProperty(this BuildContext context, string name)
        {
            var document = XDocument.Load(context.MakeAbsolute(new FilePath(Constants.DirectoryBuildPropsPath)).FullPath);

            // every element is in MSBuild's namespace, so match the local name only
            return document.Descendants().FirstOrDefault(element => element.Name.LocalName == name)?.Value
                ?? throw new CakeException($"{Constants.DirectoryBuildPropsPath} has no {name} property.");
        }

        public static void MergeReports(this BuildContext context, FilePathCollection files, ReportGeneratorReportType type, string subFolder)
        {
            context.ReportGeneratorSettings.ReportTypes = new[]
            {
                type
            };

            context.ReportGeneratorSettings.ToolPath ??= context.GetReportGeneratorPath();

            context.ReportGenerator(files, context.ReportsFolder.Combine(subFolder), context.ReportGeneratorSettings);
        }

        public static void GenerateReport(this BuildContext context, FilePath inputFile, ReportGeneratorReportType type, string subFolder)
        {
            context.ReportGeneratorSettings.ReportTypes = new[]
            {
                type
            };

            context.ReportGeneratorSettings.ToolPath ??= context.GetReportGeneratorPath();

            context.ReportGenerator(inputFile, context.ReportsFolder.Combine(subFolder), context.ReportGeneratorSettings);
        }

        /// <summary>
        /// Returns the launcher of the ReportGenerator dotnet tool. Cake would otherwise pick ReportGenerator.exe, the .NET Framework build that only runs on Windows.
        /// </summary>
        private static FilePath GetReportGeneratorPath(this BuildContext context)
        {
            var fileName = context.Environment.Platform.IsWindows() ? "reportgenerator.exe" : "reportgenerator";
            return context.Tools.Resolve(fileName) ?? throw new CakeException($"{fileName} wasn't installed.");
        }

        public static void Clean(this BuildContext context, bool cleanBin, bool cleanObj, bool cleanOutput, bool cleanMisc)
        {
            var solution = context.ParseSolution(Constants.SolutionPath);

            foreach (var project in solution.Projects)
            {
                // check solution items and exclude solution folders, since they are virtual
                if (project.Name == "Solution Items")
                    continue;

                var projectFile = project.Path; // FilePath
                if (cleanBin)
                {
                    var binFolder = projectFile.GetDirectory().Combine("bin");
                    if (context.DirectoryExists(binFolder))
                    {
                        context.CleanDirectory(binFolder);
                    }
                }

                if (cleanObj)
                {
                    var objFolder = projectFile.GetDirectory().Combine("obj");
                    if (context.DirectoryExists(objFolder))
                    {
                        context.CleanDirectory(objFolder);
                    }
                }

                if (cleanOutput && project.Path.HasExtension)
                {
                    var customProject = context.ParseProject(project.Path, configuration: Constants.Configuration, platform: Constants.Platform);
                    foreach (var path in customProject.OutputPaths)
                    {
                        context.CleanDirectory(path.FullPath);
                    }
                }
            }

            if (cleanMisc)
            {
                var folders = new[]
                {
                    new DirectoryPath(Constants.PackagesPath),
                    new DirectoryPath(Constants.ResultsPath),
                    new DirectoryPath(Constants.ReportsPath),
                    new DirectoryPath(Constants.CoberturaPath),
                };

                foreach (var folder in folders)
                {
                    context.EnsureDirectoryExists(folder);
                    context.CleanDirectory(folder, (file) => !file.Path.Segments.Last().Contains(".gitignore"));
                }
            }
        }
    }
}