using Build.Util;
using Cake.Common.Solution.Project.Properties;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;
using System;
using System.Globalization;

namespace Build.Tasks
{
    /// <summary>
    /// Generates SharedAssemblyInfo.cs (not tracked), which Directory.Build.targets compiles into every project when it exists.
    /// </summary>
    [TaskName("UpdateAssemblyInfo")]
    [IsDependentOn(typeof(UpdateCopyrightTask))]
    public sealed class UpdateAssemblyInfoTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            var version = context.GitVersion ?? throw new CakeException("The version hasn't been calculated.");

            var settings = new AssemblyInfoSettings()
            {
                // the same values as the package metadata
                Product = context.GetDirectoryBuildProperty("Product"),
                Company = context.GetDirectoryBuildProperty("Company"),
                Copyright = context.GetDirectoryBuildProperty("Copyright"),

                // GenerateAssemblyInfo is off (see Directory.Build.targets), so this file is the only source of the version attributes
                Version = version.AssemblyVersion.ToString(),
                FileVersion = version.AssemblyFileVersion.ToString(),
                // the same version as the package (BuildAndPackTask), which the library sends in its User-Agent
                InformationalVersion = version.SemVer2,

                MetaDataAttributes = new[]
                {
                    new AssemblyInfoMetadataAttribute()
                    {
                        Key = "Platform",
                        Value = Constants.Platform,
                    },
                    new AssemblyInfoMetadataAttribute()
                    {
                        Key = "CompileDate",
                        Value = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    },
                    new AssemblyInfoMetadataAttribute()
                    {
                        Key = "PublicRelease",
                        Value = context.IsPublicRelease.ToString(),
                    },
                    new AssemblyInfoMetadataAttribute()
                    {
                        Key = "Branch",
                        Value = context.Branch,
                    },
                    new AssemblyInfoMetadataAttribute()
                    {
                        Key = "Commit",
                        Value = context.Commit,
                    },
                    new AssemblyInfoMetadataAttribute()
                    {
                        Key = "Version",
                        Value = version.SemVer2,
                    },
                }
            };

            // the build project itself always runs in Debug (build.ps1), so this must not depend on its configuration
            context.CreateAssemblyInfo(new FilePath(Constants.AssemblyInfoPath), settings);

            base.Run(context);
        }
    }
}