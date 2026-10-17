using System.Text;
using System.Text.RegularExpressions;

namespace Build.Util
{
    internal sealed record ReleaseCommit(
        string Sha,
        string? Type,
        string? Scope,
        string Description,
        bool IsBreaking,
        IReadOnlyList<string> BreakingChanges,
        IReadOnlyList<string> Highlights);

    /// <summary>
    /// Builds release notes from conventional commits (https://www.conventionalcommits.org).
    /// </summary>
    internal static partial class ReleaseNotes
    {
        public const string HighlightTrailer = "Release-Note";

        private static readonly (string Title, Func<ReleaseCommit, bool> Matches, bool IsInternal)[] Sections =
        [
            ("Features", c => c.Type == "feat", false),
            ("Bug fixes", c => c.Type == "fix", false),
            ("Performance", c => c.Type == "perf", false),
            ("Dependencies", c => c.Type == "deps" || c.Scope == "deps", false),
            ("Documentation", c => c.Type == "docs", false),
            ("Internal changes", c => c.Type is "build" or "ci" or "test" or "refactor" or "style" or "chore", true),
            ("Other changes", c => true, false), // commits without a conventional prefix
        ];

        [GeneratedRegex(@"^(?<type>[a-zA-Z]+)(\((?<scope>[^)]+)\))?(?<breaking>!)?:\s*(?<description>.+)$")]
        private static partial Regex SubjectRegex();

        [GeneratedRegex(@"^(?<key>BREAKING[ -]CHANGE|[A-Za-z][A-Za-z-]*):\s*(?<value>.*)$")]
        private static partial Regex TrailerRegex();

        public static ReleaseCommit Parse(string sha, string subject, string body)
        {
            var breakingChanges = new List<string>();
            var highlights = new List<string>();

            // a trailer value continues on the following lines until a blank line or the next trailer
            List<string>? current = null;
            var value = new StringBuilder();

            void Flush()
            {
                if (current is not null && value.Length > 0)
                {
                    current.Add(value.ToString());
                }

                current = null;
                value.Clear();
            }

            foreach (var rawLine in body.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                var trailer = TrailerRegex().Match(line);

                if (trailer.Success)
                {
                    Flush();

                    var key = trailer.Groups["key"].Value;
                    if (key.StartsWith("BREAKING", StringComparison.Ordinal))
                    {
                        current = breakingChanges;
                    }
                    else if (key.Equals(HighlightTrailer, StringComparison.OrdinalIgnoreCase))
                    {
                        current = highlights;
                    }

                    value.Append(trailer.Groups["value"].Value.Trim());
                }
                else if (string.IsNullOrWhiteSpace(line))
                {
                    Flush();
                }
                else if (current is not null)
                {
                    value.Append(' ').Append(line.Trim());
                }
            }

            Flush();

            var match = SubjectRegex().Match(subject);
            if (!match.Success)
            {
                return new ReleaseCommit(sha, null, null, subject, breakingChanges.Count > 0, breakingChanges, highlights);
            }

            return new ReleaseCommit(
                sha,
                match.Groups["type"].Value.ToLowerInvariant(),
                match.Groups["scope"].Success ? match.Groups["scope"].Value : null,
                match.Groups["description"].Value,
                match.Groups["breaking"].Success || breakingChanges.Count > 0,
                breakingChanges,
                highlights);
        }

        public static string ToMarkdown(string version, DateTime date, string? previousTag, IReadOnlyList<ReleaseCommit> commits, string repositoryUrl)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"# SoftThorn.MAL.ApiClient {version} ({date:yyyy-MM-dd})");
            builder.AppendLine();
            builder.AppendLine(Summary(previousTag, commits));

            string Line(ReleaseCommit c) => $"- {Scope(c)}{c.Description} ([{c.Sha[..7]}]({repositoryUrl}/commit/{c.Sha}))";

            var breaking = commits.Where(c => c.IsBreaking).ToList();
            if (breaking.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## ⚠ Breaking changes");
                builder.AppendLine();
                foreach (var commit in breaking)
                {
                    builder.AppendLine(Line(commit));
                    foreach (var text in commit.BreakingChanges)
                    {
                        builder.AppendLine($"  {text}");
                    }
                }
            }

            var highlights = commits.SelectMany(c => c.Highlights).ToList();
            if (highlights.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Highlights");
                builder.AppendLine();
                foreach (var highlight in highlights)
                {
                    builder.AppendLine($"- {highlight}");
                }
            }

            foreach (var (title, items) in Group(commits))
            {
                builder.AppendLine();
                builder.AppendLine($"## {title}");
                builder.AppendLine();
                foreach (var commit in items)
                {
                    builder.AppendLine(Line(commit));
                }
            }

            if (previousTag is not null)
            {
                builder.AppendLine();
                builder.AppendLine($"Full changelog: {repositoryUrl}/compare/{previousTag}...v{version}");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Plain text for PackageReleaseNotes, since nuget.org doesn't render Markdown there. Leaves out internal changes.
        /// </summary>
        public static string ToText(string version, string? previousTag, IReadOnlyList<ReleaseCommit> commits, string repositoryUrl)
        {
            var builder = new StringBuilder();

            builder.AppendLine(Summary(previousTag, commits));

            void Section(string title, IEnumerable<string> lines)
            {
                builder.AppendLine();
                builder.AppendLine($"{title}:");
                foreach (var line in lines)
                {
                    builder.AppendLine($"- {line}");
                }
            }

            var breaking = commits.Where(c => c.IsBreaking).ToList();
            if (breaking.Count > 0)
            {
                Section("BREAKING CHANGES", breaking.Select(c => string.Join(" - ", new[] { Scope(c, markdown: false) + c.Description }.Concat(c.BreakingChanges))));
            }

            var highlights = commits.SelectMany(c => c.Highlights).ToList();
            if (highlights.Count > 0)
            {
                Section("Highlights", highlights);
            }

            foreach (var (title, items) in Group(commits))
            {
                if (Sections.Single(s => s.Title == title).IsInternal)
                {
                    continue;
                }

                Section(title, items.Select(c => Scope(c, markdown: false) + c.Description));
            }

            builder.AppendLine();
            builder.AppendLine(previousTag is null ? $"All changes: {repositoryUrl}/commits/v{version}" : $"All changes: {repositoryUrl}/compare/{previousTag}...v{version}");

            return builder.ToString();
        }

        private static string Summary(string? previousTag, IReadOnlyList<ReleaseCommit> commits)
        {
            var since = previousTag is null ? "since the first commit" : $"since {previousTag}";
            if (commits.Count == 0)
            {
                return $"No changes {since}.";
            }

            var parts = new List<string>();
            void Count(int count, string singular, string plural)
            {
                if (count > 0)
                {
                    parts.Add($"{count} {(count == 1 ? singular : plural)}");
                }
            }

            Count(commits.Count(c => c.IsBreaking), "breaking change", "breaking changes");
            Count(commits.Count(c => c.Type == "feat"), "feature", "features");
            Count(commits.Count(c => c.Type == "fix"), "bug fix", "bug fixes");

            var commitCount = $"{commits.Count} {(commits.Count == 1 ? "commit" : "commits")}";
            return parts.Count == 0
                ? $"This release contains {commitCount} {since}."
                : $"This release contains {commitCount} {since}: {string.Join(", ", parts)}.";
        }

        private static IEnumerable<(string Title, List<ReleaseCommit> Items)> Group(IReadOnlyList<ReleaseCommit> commits)
        {
            return commits
                .GroupBy(c => Sections.First(s => s.Matches(c)).Title)
                .OrderBy(g => Array.FindIndex(Sections, s => s.Title == g.Key))
                .Select(g => (g.Key, g.ToList()));
        }

        private static string Scope(ReleaseCommit commit, bool markdown = true)
        {
            if (commit.Scope is null || commit.Scope == "deps")
            {
                return string.Empty;
            }

            return markdown ? $"**{commit.Scope}:** " : $"{commit.Scope}: ";
        }
    }
}