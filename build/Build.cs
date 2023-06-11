using System;
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

    AbsolutePath PublishDirectory => Solution.Directory / "publish";
    AbsolutePath PublishReverseProxyDirectory => PublishDirectory / "reverseproxy";
    AbsolutePath PublishQuestionsAppDirectory => PublishDirectory / "questionsapp";

    [Solution(GenerateProjects = true)]
    readonly Solution Solution;

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
            static void PublishProject(Project project, AbsolutePath path)
            {
                path.CreateOrCleanDirectory();

                DotNetPublish(a => a
                    .SetNoRestore(true)
                    .SetProject(project)
                    .SetVerbosity(DotNetVerbosity.Quiet)
                    .SetOutput(path)
                    .SetConfiguration(Configuration.Release));

                (path / "appsettings.Development.json").DeleteFile();
            }

            // Build ReverseProxy
            PublishProject(Solution.Gateway.ReverseProxy, PublishReverseProxyDirectory);

            // Build QuestionsApp
            PublishProject(Solution.Web.QuestionsApp_Web, PublishQuestionsAppDirectory);
        });

    Target PublishDocker => _ => _
        .DependsOn(Publish)
        .Executes(() =>
        {
            DockerLogger = (type, text) => Log.Debug(text);

            static void PublishProject(AbsolutePath path, string tag)
            {
                DockerBuild(a => a.SetPath(path).SetTag(tag));
            }

            // Build ReverseProxy
            PublishProject(PublishReverseProxyDirectory, "rigo-reverse-proxy");

            // Build QuestionsApp
            PublishProject(PublishQuestionsAppDirectory, "rigo-questions-app");
        });


}
