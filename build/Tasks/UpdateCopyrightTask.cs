using Build.Util;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Build.Tasks
{
    /// <summary>
    /// Sets the year in the Copyright property of Directory.Build.props to the current year. The package and the assemblies (UpdateAssemblyInfo) both take their copyright from there.
    /// </summary>
    [TaskName("UpdateCopyright")]
    public sealed partial class UpdateCopyrightTask : FrostingTask<BuildContext>
    {
        private static readonly Regex CopyrightYear = CopyrightRegex();

        public override void Run(BuildContext context)
        {
            var path = context.MakeAbsolute(new FilePath(Constants.DirectoryBuildPropsPath)).FullPath;
            var content = File.ReadAllText(path);

            if (!CopyrightYear.IsMatch(content))
            {
                throw new CakeException($"{Constants.DirectoryBuildPropsPath} has no Copyright property with a year.");
            }

            var year = DateTime.Today.Year.ToString(CultureInfo.InvariantCulture);
            var updated = CopyrightYear.Replace(content, year, 1);

            if (updated == content)
            {
                context.Log.Information("The copyright year is already {0}.", year);
            }
            else
            {
                // only the year changes, so the file keeps its line endings
                File.WriteAllText(path, updated);
                context.Log.Information("Set the copyright year to {0}.", year);
            }

            base.Run(context);
        }

        // the year inside <Copyright>, e.g. 2025 in "<Copyright>Copyright (c) 2025 Peter Vietense</Copyright>"
        [GeneratedRegex(@"(?<=<Copyright>[^<]*?)\d{4}(?=[^<]*</Copyright>)")]
        private static partial Regex CopyrightRegex();
    }
}