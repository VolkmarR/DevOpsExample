using System;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Docker.DockerTasks;
using Serilog;
using System.Security.Policy;
using System.Collections.Generic;
using System.Threading;

class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("Url of the docker registry (without the https:// prefix)")]
    readonly string RegistryUrl = null;

    [Parameter("Api Token for Docker push")]
    readonly string DigitalOceanApiToken = null;

    AbsolutePath PublishDirectory => Solution.Directory / "publish";

    [Solution(GenerateProjects = true)]
    readonly Solution Solution;

    Target InitLocalDB => _ => _
        .Executes(() =>
        {
            var dockerPostgresPasswordPath = Solution.Directory / "docker-postgres-password.txt";
            // If Docker Compose was startet before InitLocalDB, then docker-postgres-password.txt is wrongly created as directory
            if (dockerPostgresPasswordPath.DirectoryExists())
                dockerPostgresPasswordPath.DeleteDirectory();

            Assert.False(dockerPostgresPasswordPath.FileExists(), "Password was already initialized");

            var rnd = new Random();
            var password = "";
            while (password.Length < 20)
                password += "*" + rnd.Next(int.MaxValue);

            File.WriteAllText(dockerPostgresPasswordPath, password);

            DotNet($"user-secrets set DB:Password \"{password}\" --project {Solution.Web.QuestionsApp_Web}");
        });

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            DotNetClean(s => s.SetVerbosity(DotNetVerbosity.Quiet));
            PublishDirectory.CreateOrCleanDirectory();
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution)
                .SetVerbosity(DotNetVerbosity.Quiet));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetNoRestore(true)
                .SetConfiguration(Configuration)
                .SetVerbosity(DotNetVerbosity.Quiet));
        });

    Target Publish => _ => _
        .DependsOn(Clean, Restore)
        .Executes(() =>
        {
            PublishDirectory.CreateOrCleanDirectory();

            DotNetPublish(a => a
                .SetNoRestore(true)
                .SetProject(Solution.Web.QuestionsApp_Web)
                .SetVerbosity(DotNetVerbosity.Quiet)
                .SetOutput(PublishDirectory)
                .SetConfiguration(Configuration.Release));

            (PublishDirectory / "appsettings.Development.json").DeleteFile();
        });

    Target PublishDocker => _ => _
        .DependsOn(Publish)
        .Executes(() =>
        {
            DockerLogger = (type, text) => Log.Debug(text);

            if (!string.IsNullOrEmpty(RegistryUrl))
            {
                DigitalOceanApiToken.NotNullOrEmpty();

                DockerLogin(a => a
                    .SetServer(RegistryUrl)
                    .SetUsername(DigitalOceanApiToken)
                    .SetPassword(DigitalOceanApiToken));
            }

            var tag = "rigo-questions-app";
            var tags = new List<string>() { tag };
            var remoteTag = $"{RegistryUrl}/{tag}";
            if (!string.IsNullOrEmpty(RegistryUrl))
                tags.Add(remoteTag);

            DockerBuild(a => a.SetPath(PublishDirectory).SetTag(tags));

            // push, if remoteurl is available
            if (!string.IsNullOrEmpty(RegistryUrl))
                DockerPush(a => a.SetName(remoteTag));
        });

}
