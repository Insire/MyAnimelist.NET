using System;
using Cake.Frosting;

namespace Build
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            return new CakeHost()
                .InstallTool(new Uri("nuget:?package=GitVersion.Tool&version=6.8.2"))
                .InstallTool(new Uri("nuget:?package=nuget.commandline&version=7.9.0"))
                .InstallTool(new Uri("nuget:?package=CodecovUploader&version=0.8.0"))
                .InstallTool(new Uri("nuget:?package=ReportGenerator&version=5.5.11"))
                .InstallTool(new Uri("dotnet:?package=dotnet-coverage&version=18.12.0"))
                .InstallTool(new Uri("dotnet:?package=NSwag.ConsoleCore&version=14.7.1"))
                .UseContext<BuildContext>()
                .UseLifetime<BuildLifetime>()
                .UseWorkingDirectory("..")
                .Run(args);
        }
    }
}