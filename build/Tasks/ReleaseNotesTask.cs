using Build.Util;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Core;
using Cake.Frosting;

namespace Build.Tasks
{
    /// <summary>
    /// Writes release notes for the commits since the previous release tag (v*).
    /// The Markdown file is meant for a GitHub release; the text file becomes PackageReleaseNotes.
    /// </summary>
    [TaskName("ReleaseNotes")]
    public sealed class ReleaseNotesTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            var version = context.GitVersion?.SemVer2 ?? throw new CakeException("The version hasn't been calculated.");

            // --exclude skips the tag of this very version, in case the build of a tagged commit runs again
            var (exitCode, output) = context.RunGit($"describe --tags --abbrev=0 --match \"v[0-9]*\" --exclude \"v{version}\"");
            var previousTag = exitCode == 0 ? output.Trim() : null;

            var range = previousTag is null ? "HEAD" : $"{previousTag}..HEAD";
            var (logExitCode, log) = context.RunGit($"-c i18n.logOutputEncoding=UTF-8 log --no-merges --reverse --format=%H%x1f%s%x1f%b%x1e {range}");
            if (logExitCode != 0)
            {
                throw new CakeException(logExitCode, $"git log failed with exit code {logExitCode}.");
            }

            var commits = new List<ReleaseCommit>();
            foreach (var record in log.Split('\x1e', StringSplitOptions.RemoveEmptyEntries))
            {
                var fields = record.Trim('\n', '\r').Split('\x1f');
                if (fields.Length < 3)
                {
                    continue;
                }

                commits.Add(ReleaseNotes.Parse(fields[0], fields[1], fields[2]));
            }

            context.EnsureDirectoryExists(context.ResultsFolder);
            File.WriteAllText(context.ReleaseNotesMarkdownFile.FullPath, ReleaseNotes.ToMarkdown(version, DateTime.UtcNow, previousTag, commits, Constants.RepositoryUrl));
            File.WriteAllText(context.ReleaseNotesTextFile.FullPath, ReleaseNotes.ToText(version, previousTag, commits, Constants.RepositoryUrl));

            context.Information("Release notes for {0}: {1} commits since {2}, {3} breaking.", version, commits.Count, previousTag ?? "the first commit", commits.Count(c => c.IsBreaking));
            context.Information("Written to {0}", context.ReleaseNotesMarkdownFile.FullPath);

            base.Run(context);
        }
    }
}