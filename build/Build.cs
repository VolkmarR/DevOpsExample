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
using Nuke.Common.Tools.Pulumi;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Docker.DockerTasks;
using static Nuke.Common.Tools.Pulumi.PulumiTasks;
using Serilog;
using System.Security.Policy;
using System.Collections.Generic;
using System.Threading;
using Nuke.Common.Git;
using Nuke.Common.CI.GitHubActions;

[GitHubActions("PR-DeployToLatest",
    GitHubActionsImage.UbuntuLatest,
    On = new[] { GitHubActionsTrigger.PullRequest },
    InvokedTargets = new[] { nameof(DeployToLatest) },
    ImportSecrets = new[] { nameof(RegistryUrl), nameof(DigitalOcean_Token) })]
class Build : NukeBuild
{


    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("Url of the docker registry (without the https:// prefix)"), Secret]
    readonly string RegistryUrl = null;

    [Parameter("Api Token for Docker push"), Secret]
    readonly string DigitalOcean_Token = null;

    [Parameter("Docker Tag for deployment")]
    string DockerTag = null;

    AbsolutePath PublishDirectory => Solution.Directory / "publish";

    AbsolutePath InfrastructureDirectory => Solution.Directory / "infrastructure";

    [Solution(GenerateProjects = true)]
    readonly Solution Solution;

    [GitRepository]
    readonly GitRepository Repository;

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
        .Requires(() => DigitalOcean_Token, () => RegistryUrl)
        .Executes(() =>
        {
            Repository.Commit.NotNullOrEmpty();

            DockerLogger = (type, text) => Log.Debug(text);

            if (!string.IsNullOrEmpty(RegistryUrl))
            {

                DockerLogin(a => a
                    .SetServer(RegistryUrl)
                    .SetUsername(DigitalOcean_Token)
                    .SetPassword(DigitalOcean_Token));
            }

            DockerTag = $"{DateTime.Today:yy.MM.dd}.{Repository.Commit[..7]}-{DateTime.Now:ff}{Random.Shared.Next(0, 9)}";

            var tag = $"rigo-questions-app:{DockerTag}";
            var tags = new List<string>() { tag };
            var remoteTag = $"{RegistryUrl}/{tag}";
            if (!string.IsNullOrEmpty(RegistryUrl))
                tags.Add(remoteTag);

            DockerBuild(a => a
                .SetPath(PublishDirectory)
                .SetBuildArg($"build_tag={DockerTag}")
                .SetTag(tags));

            // push, if remoteurl is available
            if (!string.IsNullOrEmpty(RegistryUrl))
                DockerPush(a => a.SetName(remoteTag));

            Log.Information("Docker image tag {tag}", tag);
        });

    Target DeployToLatest => _ => _
        .DependsOn(PublishDocker)
        .Executes(() => DeployToAction("latest", DockerTag));

    Target DeployLatestToStage => _ => _
        .Executes(() =>
        {
            PulumiStackSelect(a => a
                .SetStackName("latest")
                .SetCwd(InfrastructureDirectory));

            var dockerTag = PulumiConfigGet(a => a
                .SetKey("dockerTag")
                .SetCwd(InfrastructureDirectory)).Select(q => q.Text).FirstOrDefault();

            dockerTag.NotNullOrEmpty();

            DeployToAction("stage", dockerTag);
        });

    Target DestroyCompleteDeployment => _ => _
        .Executes(() =>
        {
            DestroyStack("stage");
            DestroyStack("latest");
            DestroyStack("common");
        });

    Target DeployCommon => _ => _
        .Executes(() =>
        {
            PulumiStackSelect(a => a.SetStackName("common").SetCwd(InfrastructureDirectory));

            PulumiUp(a => a
                .SetYes(true)
                .SetCwd(InfrastructureDirectory));
        });

    void DestroyStack(string stack)
    {
        PulumiStackSelect(a => a
            .SetStackName("latest")
            .SetCwd(InfrastructureDirectory));

        PulumiDestroy(a => a
            .SetYes(true)
            .SetCwd(InfrastructureDirectory));
    }


    void DeployToAction(string stack, string dockerTag)
    {
        dockerTag.NotNullOrEmpty();

        PulumiStackSelect(a => a.SetStackName(stack).SetCwd(InfrastructureDirectory));

        PulumiUp(a => a
            .SetYes(true)
            .SetCwd(InfrastructureDirectory)
            .AddConfig($"dockerTag={dockerTag}"));
    }
}
