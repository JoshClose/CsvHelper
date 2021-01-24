// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.Npm;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.ChangeLog.ChangelogTasks;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Npm.NpmTasks;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(x => x.Test);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    [Parameter("NuGet server URL.")]
	readonly string NugetSource = "https://api.nuget.org/v3/index.json";
    [Parameter("API Key for the NuGet server.")]
	readonly string NugetApiKey;
	[Parameter("Version to use for package.")]
	readonly string Version;
	[Parameter("NuGet package version.")]
	readonly string PackageVersion;

    [Solution]
	readonly Solution Solution;
    [GitRepository]
	readonly GitRepository GitRepository;
    [GitVersion]
	readonly GitVersion GitVersion;
	
    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
	AbsolutePath DocsDirectory => RootDirectory / "docs";
	AbsolutePath DocsSourceDirectory => RootDirectory / "docs-src" / "docs";
	AbsolutePath DocsArtifactsDirectory => DocsSourceDirectory / "dist";

    Project CsvHelperProject => Solution.GetProject("CsvHelper");

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution)
			);
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .EnableNoRestore()
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
			);

            DotNetPublish(s => s
				.EnableNoRestore()
				.EnableNoBuild()
				.SetConfiguration(Configuration)
				.SetAssemblyVersion(GitVersion.AssemblySemVer)
				.SetFileVersion(GitVersion.AssemblySemFileVer)
				.SetInformationalVersion(GitVersion.InformationalVersion)
				.CombineWith(
					from project in new[] { CsvHelperProject }
					from framework in project.GetTargetFrameworks()
                    select new { project, framework }, (cs, v) => cs
						.SetProject(v.project)
						.SetFramework(v.framework)
				)
			);
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .EnableNoBuild()
            );
        });

    Target Pack => _ => _
        .DependsOn(Clean, Test)
		.Requires(() => Configuration == Configuration.Release)
        .Executes(() =>
        {
            DotNetPack(s => s
                .EnableNoRestore()
                .EnableNoBuild()
				.SetProject(CsvHelperProject)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(ArtifactsDirectory)
				.SetProperty("PackageVersion", PackageVersion ?? GitVersion.NuGetVersionV2)
            );
        });

    Target Push => _ => _
        .Executes(() =>
        {
            DotNetNuGetPush(s => s
				.SetSource(NugetSource)
				.SetApiKey(NugetApiKey)
				.CombineWith(ArtifactsDirectory.GlobFiles("*.nupkg"), (s, v) => s
					.SetTargetPath(v)
				)
            );
        });

	Target DocsBuild => _ => _
		.Executes(() =>
		{
			NpmInstall(s => s
				.SetWorkingDirectory(DocsSourceDirectory)
			);

			NpmRun(s => s
				.SetWorkingDirectory(DocsSourceDirectory)
				.SetCommand("build")
			);
		});

	Target DocsPublish => _ => _
		.DependsOn(DocsBuild)
		.Executes(() =>
		{
			EnsureExistingDirectory(DocsDirectory);
			EnsureCleanDirectory(DocsDirectory);
			CopyDirectoryRecursively(DocsArtifactsDirectory, DocsDirectory, DirectoryExistsPolicy.Merge);
		});
}
