namespace Build.Util
{
    internal static class Constants
    {
        public const string Configuration = "Release";
        public const string Platform = "AnyCPU";

        public const string SolutionPath = "./MyAnimelist.NET.slnx";
        public const string NuGetConfigPath = "./nuget.config";
        public const string AssemblyInfoPath = "./SharedAssemblyInfo.cs";

        public const string PackagesPath = "./tmp_build/packages";
        public const string ResultsPath = "./tmp_build/results";
        public const string ReportsPath = "./tmp_build/results/reports";
        public const string CoberturaPath = "./tmp_build/results/reports/cobertura";

        public const string RepositoryUrl = "https://github.com/Insire/MyAnimelist.NET";

        public const string LocalNugetDirectory = @"E:\Drop\NuGet";

        public const string OpenApiSpecPath = "./assets/MyAnimelist.NET_OpenApiSpec.json";
        public const string NswagConfigFilePath = "./assets/MyAnimelist.NET_Config.nswag";
        public const string ProjectPath = "./src//MyAnimelist.NET.csproj";
    }
}