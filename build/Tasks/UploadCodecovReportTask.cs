using Build.Util;
using Cake.Common;
using Cake.Common.Build;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Common.Net;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Build.Tasks
{
    /// <summary>
    /// Uploads the coverage report with the Codecov CLI, which has no NuGet package, so the pinned binary is downloaded from cli.codecov.io and checked against its SHA-256.
    /// </summary>
    [TaskName("UploadCodecovReport")]
    [IsDependentOn(typeof(CoberturaReportTask))]
    public sealed class UploadCodecovReportTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            var cli = GetCodecovCli(context);

            var settings = new ProcessSettings()
                .UseWorkingDirectory(".")
                .WithArguments(builder => builder
                    .Append("--verbose")
                    .Append("upload-coverage")
                    // the CLI reads CODECOV_TOKEN from the environment, so the token never shows up in the command line
                    .Append("--fail-on-error")
                    .Append("--disable-search")
                    .AppendSwitch("--plugin", "noop") // don't look for gcov, xcrun or coverage.py
                    .AppendSwitchQuoted("--file", context.CoberturaResultFile.FullPath)
                    .AppendSwitch("--git-service", "github")
                );

            // a Codecov outage shouldn't stop the release, which runs after this task
            var exitCode = context.StartProcess(cli, settings);
            if (exitCode != 0)
            {
                context.Warning($"The Codecov upload failed with exit code {exitCode}.");
            }

            base.Run(context);
        }

        public override bool ShouldRun(BuildContext context)
        {
            if (!context.FileExists(context.CoberturaResultFile))
            {
                context.Log.Information($"Skipped {nameof(UploadCodecovReportTask)}, since {context.CoberturaResultFile} wasn't created.");
                return false;
            }

            if (!context.BuildSystem().IsRunningOnAzurePipelines)
            {
                context.Log.Information($"Skipped {nameof(UploadCodecovReportTask)}, since task is not running on AzurePipelines (Hosted).");
                return false;
            }

            if (string.IsNullOrEmpty(context.Environment.GetEnvironmentVariable("CODECOV_TOKEN")))
            {
                context.Log.Information($"Skipped {nameof(UploadCodecovReportTask)}, since environment variable CODECOV_TOKEN missing or empty.");
                return false;
            }

            if (context.TestsAreFailing)
            {
                context.Log.Information($"Skipped {nameof(UploadCodecovReportTask)}, since we got failing tests.");
                return false;
            }

            if (GetPlatform() is null)
            {
                context.Log.Information($"Skipped {nameof(UploadCodecovReportTask)}, since Codecov CLI {Constants.CodecovCliVersion} has no binary for {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture}).");
                return false;
            }

            return base.ShouldRun(context);
        }

        /// <summary>
        /// Returns the cli.codecov.io platform folder, the file name and the expected SHA-256 of the binary for the current OS.
        /// </summary>
        private static (string Folder, string FileName, string Sha256)? GetPlatform()
        {
            if (OperatingSystem.IsWindows() && RuntimeInformation.OSArchitecture == Architecture.X64)
            {
                return ("windows", "codecov.exe", Constants.CodecovCliSha256Windows);
            }

            if (OperatingSystem.IsLinux() && RuntimeInformation.OSArchitecture == Architecture.X64)
            {
                return ("linux", "codecov", Constants.CodecovCliSha256Linux);
            }

            if (OperatingSystem.IsMacOS())
            {
                return ("macos", "codecov", Constants.CodecovCliSha256MacOS);
            }

            return null;
        }

        private static FilePath GetCodecovCli(BuildContext context)
        {
            var (folder, fileName, expectedSha256) = GetPlatform()!.Value;
            var path = context.MakeAbsolute(new DirectoryPath("./tools/codecov-cli").Combine(Constants.CodecovCliVersion).CombineWithFilePath(fileName));

            if (!context.FileExists(path) || GetSha256(path) != expectedSha256)
            {
                context.EnsureDirectoryExists(path.GetDirectory());
                context.DownloadFile($"https://cli.codecov.io/{Constants.CodecovCliVersion}/{folder}/{fileName}", path);

                var actualSha256 = GetSha256(path);
                if (actualSha256 != expectedSha256)
                {
                    context.DeleteFile(path);
                    throw new CakeException($"The Codecov CLI download has the SHA-256 {actualSha256}, expected {expectedSha256}.");
                }
            }

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path.FullPath, File.GetUnixFileMode(path.FullPath) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
            }

            return path;
        }

        private static string GetSha256(FilePath path)
        {
            using var stream = File.OpenRead(path.FullPath);
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        }
    }
}