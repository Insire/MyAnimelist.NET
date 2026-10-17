using Build.Util;
using Cake.Common.Solution.Project.Properties;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;
using System;
using System.Globalization;

namespace Build.Tasks
{
    [TaskName("UpdateAssemblyInfo")]
    public sealed class UpdateAssemblyInfoTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            var assemblyInfoParseResult = context.ParseAssemblyInfo(Constants.AssemblyInfoPath);
            var version = context.GitVersion ?? throw new CakeException("The version hasn't been calculated.");

            var settings = new AssemblyInfoSettings()
            {
                Product = assemblyInfoParseResult.Product,
                Company = assemblyInfoParseResult.Company,
                Trademark = assemblyInfoParseResult.Trademark,
                Copyright = $"Copyright (c) {DateTime.Today.Year} Peter Vietense",

                // GenerateAssemblyInfo is off (see Directory.Build.targets), so this file is the only source of the version attributes
                Version = version.AssemblyVersion.ToString(),
                FileVersion = version.AssemblyFileVersion.ToString(),
                // the same version as the package (BuildAndPackTask), which the library sends in its User-Agent
                InformationalVersion = version.SemVer2,

                InternalsVisibleTo = assemblyInfoParseResult.InternalsVisibleTo,

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