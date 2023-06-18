using System;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.Pulumi;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Docker.DockerTasks;
using static Nuke.Common.Tools.Pulumi.PulumiTasks;
using Serilog;
using System.Collections.Generic;
using Nuke.Common.Git;
using Nuke.Common.CI.GitHubActions;

[GitHubActions("PR-Test",
    GitHubActionsImage.UbuntuLatest,
    On = new[] { GitHubActionsTrigger.PullRequest },
    InvokedTargets = new[] { nameof(Test) },
    ImportSecrets = new[] { nameof(REGISTRYURL), nameof(DIGITALOCEAN_TOKEN), nameof(PULUMI_ACCESS_TOKEN) })]
[GitHubActions("PR-DeployToLatest",
    GitHubActionsImage.UbuntuLatest,
    OnPushBranches = new[] { "master" },
    InvokedTargets = new[] { nameof(DeployToLatest) },
    ImportSecrets = new[] { nameof(REGISTRYURL), nameof(DIGITALOCEAN_TOKEN), nameof(PULUMI_ACCESS_TOKEN) })]
[GitHubActions("MAN-DeployLatestToStage",
    GitHubActionsImage.UbuntuLatest,
    On = new[] { GitHubActionsTrigger.WorkflowDispatch },
    InvokedTargets = new[] { nameof(DeployLatestToStage) },
    ImportSecrets = new[] { nameof(REGISTRYURL), nameof(DIGITALOCEAN_TOKEN), nameof(PULUMI_ACCESS_TOKEN) })]
class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("Url of the docker registry (without the https:// prefix)"), Secret]
    readonly string REGISTRYURL = null;

    [Parameter("Api Token for Digital Ocean deployments"), Secret]
    readonly string DIGITALOCEAN_TOKEN = null;

    [Parameter("Api Token for Pulumi"), Secret]
    readonly string PULUMI_ACCESS_TOKEN = null;

    [Parameter("Docker Tag for deployment")]
    string DockerTag = null;

    AbsolutePath PublishDirectory => Solution.Directory / "publish";

    AbsolutePath InfrastructureDirectory => Solution.Directory / "infrastructure";

    [Solution(GenerateProjects = true)]
    readonly Solution Solution;

    [GitRepository]
    readonly GitRepository Repository;

    protected override void OnBuildInitialized()
    {
        base.OnBuildInitialized();
        Environment.SetEnvironmentVariable("DIGITALOCEAN_TOKEN", DIGITALOCEAN_TOKEN);
        Environment.SetEnvironmentVariable("PULUMI_ACCESS_TOKEN", PULUMI_ACCESS_TOKEN);
    }

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

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(Solution.Web.QuestionsApp_Tests)
                .SetNoRestore(true)
                .SetVerbosity(DotNetVerbosity.Quiet));
        });

    Target Publish => _ => _
        .DependsOn(Clean, Restore, Test)
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
        .Requires(() => DIGITALOCEAN_TOKEN, () => REGISTRYURL)
        .Executes(() =>
        {
            Repository.Commit.NotNullOrEmpty();

            DockerLogger = (type, text) => Log.Debug(text);

            if (!string.IsNullOrEmpty(REGISTRYURL))
            {

                DockerLogin(a => a
                    .SetServer(REGISTRYURL)
                    .SetUsername(DIGITALOCEAN_TOKEN)
                    .SetPassword(DIGITALOCEAN_TOKEN));
            }

            DockerTag = $"{DateTime.Today:yy.MM.dd}.{Repository.Commit[..7]}-{DateTime.Now:ff}{Random.Shared.Next(0, 9)}";

            var tag = $"rigo-questions-app:{DockerTag}";
            var tags = new List<string>() { tag };
            var remoteTag = $"{REGISTRYURL}/{tag}";
            if (!string.IsNullOrEmpty(REGISTRYURL))
                tags.Add(remoteTag);

            DockerBuild(a => a
                .SetPath(PublishDirectory)
                .SetBuildArg($"build_tag={DockerTag}")
                .SetTag(tags));

            // push, if remoteurl is available
            if (!string.IsNullOrEmpty(REGISTRYURL))
                DockerPush(a => a.SetName(remoteTag));

            Log.Information("Docker image tag {tag}", tag);
        });

    Target DeployToLatest => _ => _
        .Requires(() => PULUMI_ACCESS_TOKEN)
        .DependsOn(PublishDocker)
        .Executes(() => DeployToAction("latest", DockerTag));

    Target DeployLatestToStage => _ => _
        .Requires(() => PULUMI_ACCESS_TOKEN)
        .Executes(() =>
        {
            PulumiStackSelect(a => a
                .SetStackName("latest")
                .SetCwd(InfrastructureDirectory));

            var dockerTag = PulumiStackOutput(a => a
                .SetPropertyName("dockerTag")
                .SetCwd(InfrastructureDirectory)).Select(q => q.Text).FirstOrDefault();

            dockerTag.NotNullOrEmpty();

            DeployToAction("stage", dockerTag);
        });

    Target DestroyCompleteDeployment => _ => _
        .Requires(() => PULUMI_ACCESS_TOKEN)
        .Executes(() =>
        {
            DestroyStack("stage");
            DestroyStack("latest");
            DestroyStack("common");
        });

    Target DeployCommon => _ => _
        .Requires(() => PULUMI_ACCESS_TOKEN)
        .Executes(() =>
        {
            PulumiUp(a => a
                .SetStack("common")
                .SetYes(true)
                .SetSkipPreview(true)
                .SetCwd(InfrastructureDirectory));
        });

    void DestroyStack(string stack)
    {
        PulumiDestroy(a => a
            .SetStack(stack)
            .SetYes(true)
            .SetSkipPreview(true)
            .SetCwd(InfrastructureDirectory));
    }


    void DeployToAction(string stack, string dockerTag)
    {
        dockerTag.NotNullOrEmpty();

        PulumiUp(a => a
            .SetYes(true)
            .SetStack(stack)
            .SetCwd(InfrastructureDirectory)
            .SetSkipPreview(true)
            .AddConfig($"dockerTag={dockerTag}"));
    }
}
