using Cake.Frosting;

namespace Build
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            return new CakeHost()
                .InstallTool(new Uri("dotnet:?package=dotnet-reportgenerator-globaltool&version=5.5.11"))
                .InstallTool(new Uri("dotnet:?package=dotnet-coverage&version=18.12.0"))
                .InstallTool(new Uri("dotnet:?package=NSwag.ConsoleCore&version=14.7.1"))
                .UseContext<BuildContext>()
                .UseLifetime<BuildLifetime>()
                .UseWorkingDirectory("..")
                .Run(args);
        }
    }
}