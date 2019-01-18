#l "apps-uwp-variables.cake"

#addin "nuget:?package=MagicChunks&version=2.0.0.119"
#addin "nuget:?package=Newtonsoft.Json&version=11.0.2"
#addin "nuget:?package=WindowsAzure.Storage&version=9.1.1"
#addin "nuget:?package=Cake.WindowsAppStore&version=1.4.0"

//-------------------------------------------------------------

private void ValidateUwpAppsInput()
{
    // No validation required (yet)
}

//-------------------------------------------------------------

private bool HasUwpApps()
{
    return UwpApps != null && UwpApps.Count > 0;
}

//-------------------------------------------------------------

private async Task PrepareForUwpAppsAsync()
{
    if (!HasUwpApps())
    {
        return;
    }

    // Check whether projects should be processed, `.ToList()` 
    // is required to prevent issues with foreach
    foreach (var uwpApp in UwpApps.ToList())
    {
        if (!ShouldProcessProject(uwpApp))
        {
            UwpApps.Remove(uwpApp);
        }
    }
}

//-------------------------------------------------------------

public void UpdateAppxManifestVersion(string path, string version)
{
    Information("Updating AppxManifest version @ '{0}' to '{1}'", path, version);

    TransformConfig(path,
          new TransformationCollection {
            { "Package/Identity/@Version", version }
          });
}

//-------------------------------------------------------------

public string GetArtifactsDirectory(string outputRootDirectory)
{
    // 1 directory up since we want to turn "/output/release" into "/output/"
    var artifactsDirectoryString = string.Format("{0}/..", outputRootDirectory);
    var artifactsDirectory = MakeAbsolute(Directory(artifactsDirectoryString)).FullPath;

    return artifactsDirectory;
}

//-------------------------------------------------------------

public string GetAppxUploadFileName(string artifactsDirectory, string solutionName, string versionMajorMinorPatch)
{
    var appxUploadSearchPattern = artifactsDirectory + string.Format("/{0}_{1}.0_*.appxupload", solutionName, versionMajorMinorPatch);

    Information("Searching for appxupload using '{0}'", appxUploadSearchPattern);

    var filesToZip = GetFiles(appxUploadSearchPattern);

    Information("Found '{0}' files to upload", filesToZip.Count);

    var appxUploadFile = filesToZip.FirstOrDefault();
    if (appxUploadFile == null)
    {
        return null;
    }
    
    var appxUploadFileName = appxUploadFile.FullPath;
    return appxUploadFileName;
}

//-------------------------------------------------------------

private void UpdateInfoForUwpApps()
{
    if (!HasUwpApps())
    {
        return;
    }

    foreach (var uwpApp in UwpApps)
    {
        var appxManifestFile = string.Format("./src/{0}/Package.appxmanifest", uwpApp);
        UpdateAppxManifestVersion(appxManifestFile, string.Format("{0}.0", VersionMajorMinorPatch));
    }
}

//-------------------------------------------------------------

private void BuildUwpApps()
{
    if (!HasUwpApps())
    {
        return;
    }

    var platforms = new Dictionary<string, PlatformTarget>();
    //platforms["AnyCPU"] = PlatformTarget.MSIL;
    platforms["x86"] = PlatformTarget.x86;
    platforms["x64"] = PlatformTarget.x64;
    platforms["arm"] = PlatformTarget.ARM;

    // Important note: we only have to build for ARM, it will auto-build x86 / x64 as well
    var platform = platforms.First(x => x.Key == "arm");
    
    foreach (var uwpApp in UwpApps)
    {
        Information("Building UWP app '{0}'", uwpApp);

        var artifactsDirectory = GetArtifactsDirectory(OutputRootDirectory);
        var appxUploadFileName = GetAppxUploadFileName(artifactsDirectory, uwpApp, VersionMajorMinorPatch);

        // If already exists, skip for store upload debugging
        if (appxUploadFileName != null && FileExists(appxUploadFileName))
        {
            Information(string.Format("File '{0}' already exists, skipping build", appxUploadFileName));
            continue;
        }

        var msBuildSettings = new MSBuildSettings {
            Verbosity = Verbosity.Quiet, // Verbosity.Diagnostic
            ToolVersion = MSBuildToolVersion.Default,
            Configuration = ConfigurationName,
            MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
            PlatformTarget = platform.Value
        };

        var toolPath = GetVisualStudioPath(msBuildSettings.ToolVersion);
        if (!string.IsNullOrWhiteSpace(toolPath))
        {
            msBuildSettings.ToolPath = toolPath;
        }

        // Always disable SourceLink
        msBuildSettings.WithProperty("EnableSourceLink", "false");

        // See https://docs.microsoft.com/en-us/windows/uwp/packaging/auto-build-package-uwp-apps for all the details
        //msBuildSettings.Properties["UseDotNetNativeToolchain"] = new List<string>(new [] { "false" });
        //msBuildSettings.Properties["UapAppxPackageBuildMode"] = new List<string>(new [] { "StoreUpload" });
        msBuildSettings.Properties["UapAppxPackageBuildMode"] = new List<string>(new [] { "CI" });
        msBuildSettings.Properties["AppxBundlePlatforms"] = new List<string>(new [] { string.Join("|", platforms.Keys) });
        msBuildSettings.Properties["AppxBundle"] = new List<string>(new [] { "Always" });
        msBuildSettings.Properties["AppxPackageDir"] = new List<string>(new [] { artifactsDirectory });

        Information("Building project for platform {0}, artifacts directory is '{1}'", platform.Key, artifactsDirectory);

        var projectFileName = GetProjectFileName(uwpApp);

        // Note: if csproj doesn't work, use SolutionFileName instead
        //var projectFileName = SolutionFileName;
        MSBuild(projectFileName, msBuildSettings);

        // Recalculate!
        appxUploadFileName = GetAppxUploadFileName(artifactsDirectory, uwpApp, VersionMajorMinorPatch);
        if (appxUploadFileName == null)
        {
            throw new Exception(string.Format("Couldn't determine the appxupload file using base directory '{0}'", artifactsDirectory));
        }

        Information("Created appxupload file '{0}'", appxUploadFileName, artifactsDirectory);
    }
}

//-------------------------------------------------------------

private void PackageUwpApps()
{
    if (!HasUwpApps())
    {
        return;
    }
    
    // No specific implementation required for now, build already wraps it up
}

//-------------------------------------------------------------

private void DeployUwpApps()
{
    if (!HasUwpApps())
    {
        return;
    }
    
    foreach (var uwpApp in UwpApps)
    {
        if (!ShouldDeployProject(uwpApp))
        {
            Information("UWP app '{0}' should not be deployed", uwpApp);
            continue;
        }

        LogSeparator("Deploying UWP app '{0}'", uwpApp);

        var artifactsDirectory = GetArtifactsDirectory(OutputRootDirectory);
        var appxUploadFileName = GetAppxUploadFileName(artifactsDirectory, uwpApp, VersionMajorMinorPatch);

        Information("Creating Windows Store app submission");

        CreateWindowsStoreAppSubmission(appxUploadFileName, new WindowsStoreAppSubmissionSettings
        {
            ApplicationId = WindowsStoreAppId,
            ClientId = WindowsStoreClientId,
            ClientSecret = WindowsStoreClientSecret,
            TenantId = WindowsStoreTenantId
        });        
    }
}

//-------------------------------------------------------------

Task("UpdateInfoForUwpApps")
    .IsDependentOn("Clean")
    .Does(() =>
{
    UpdateSolutionAssemblyInfo();
    UpdateInfoForUwpApps();
});

//-------------------------------------------------------------

Task("BuildUwpApps")
    .IsDependentOn("UpdateInfoForUwpApps")
    .Does(() =>
{
    BuildUwpApps();
});

//-------------------------------------------------------------

Task("PackageUwpApps")
    .IsDependentOn("BuildUwpApps")
    .Does(() =>
{
    PackageUwpApps();
});

//-------------------------------------------------------------

Task("DeployUwpApps")
    .IsDependentOn("PackageUwpApps")
    .Does(() =>
{
    DeployUwpApps();
});