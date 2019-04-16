#pragma warning disable 1998

#l "docker-variables.cake"
#l "lib-octopusdeploy.cake"

#addin "nuget:?package=Cake.FileHelpers&version=3.0.0"
#addin "nuget:?package=Cake.Docker&version=0.9.9"

//-------------------------------------------------------------

private string GetDockerRegistryUrl(string projectName)
{
    // Allow per project overrides via "DockerRegistryUrlFor[ProjectName]"
    return GetProjectSpecificConfigurationValue(projectName, "DockerRegistryUrlFor", DockerRegistryUrl);
}

//-------------------------------------------------------------

private string GetDockerRegistryUserName(string projectName)
{
    // Allow per project overrides via "DockerRegistryUserNameFor[ProjectName]"
    return GetProjectSpecificConfigurationValue(projectName, "DockerRegistryUserNameFor", DockerRegistryUserName);
}

//-------------------------------------------------------------

private string GetDockerRegistryPassword(string projectName)
{
    // Allow per project overrides via "DockerRegistryPasswordFor[ProjectName]"
    return GetProjectSpecificConfigurationValue(projectName, "DockerRegistryPasswordFor", DockerRegistryPassword);
}

//-------------------------------------------------------------

private string GetDockerImageName(string projectName)
{
    var name = projectName.Replace(".", "-");
    return name.ToLower();
}

//-------------------------------------------------------------

private string GetDockerImageTag(string projectName, string version)
{
    var dockerRegistryUrl = GetDockerRegistryUrl(projectName);

    var tag = string.Format("{0}/{1}:{2}", dockerRegistryUrl, GetDockerImageName(projectName), version);
    return tag.ToLower();
}

//-------------------------------------------------------------

private void ConfigureDockerSettings(AutoToolSettings dockerSettings)
{
    var engineUrl = DockerEngineUrl;
    if (!string.IsNullOrWhiteSpace(engineUrl))
    {
        Information("Using remote docker engine: '{0}'", engineUrl);

        dockerSettings.ArgumentCustomization = args => args.Prepend($"-H {engineUrl}");
        //dockerSettings.BuildArg = new [] { $"DOCKER_HOST={engineUrl}" };
    }
}

//-------------------------------------------------------------

private void ValidateDockerImagesInput()
{
    // No validation required (yet)
}

//-------------------------------------------------------------

private bool HasDockerImages()
{
    return DockerImages != null && DockerImages.Count > 0;
}

//-------------------------------------------------------------

private async Task PrepareForDockerImagesAsync()
{
    if (!HasDockerImages())
    {
        return;
    }

    // Check whether projects should be processed, `.ToList()` 
    // is required to prevent issues with foreach
    foreach (var dockerImage in DockerImages.ToList())
    {
        if (!ShouldProcessProject(dockerImage))
        {
            DockerImages.Remove(dockerImage);
        }
    }
}

//-------------------------------------------------------------

private void UpdateInfoForDockerImages()
{
    if (!HasDockerImages())
    {
        return;
    }

    // Doesn't seem neccessary yet
    // foreach (var dockerImage in DockerImages)
    // {
    //     Information("Updating version for docker image '{0}'", dockerImage);

    //     var projectFileName = GetProjectFileName(dockerImage);

    //     TransformConfig(projectFileName, new TransformationCollection 
    //     {
    //         { "Project/PropertyGroup/PackageVersion", VersionNuGet }
    //     });
    // }
}

//-------------------------------------------------------------

private void BuildDockerImages()
{
    if (!HasDockerImages())
    {
        return;
    }
    
    foreach (var dockerImage in DockerImages)
    {
        LogSeparator("Building docker image '{0}'", dockerImage);

        var projectFileName = GetProjectFileName(dockerImage);
        
        var msBuildSettings = new MSBuildSettings {
            Verbosity = Verbosity.Quiet, // Verbosity.Diagnostic
            ToolVersion = MSBuildToolVersion.Default,
            Configuration = ConfigurationName,
            MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
            PlatformTarget = PlatformTarget.MSIL
        };

        ConfigureMsBuild(msBuildSettings, dockerImage);

        // Always disable SourceLink
        msBuildSettings.WithProperty("EnableSourceLink", "false");

        // Note: we need to set OverridableOutputPath because we need to be able to respect
        // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
        // are properties passed in using the command line)
        var outputDirectory = string.Format("{0}/{1}/", OutputRootDirectory, dockerImage);
        Information("Output directory: '{0}'", outputDirectory);
        msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
        msBuildSettings.WithProperty("PackageOutputPath", OutputRootDirectory);

        MSBuild(projectFileName, msBuildSettings);
    }
}

//-------------------------------------------------------------

private void PackageDockerImages()
{
    if (!HasDockerImages())
    {
        return;
    }

    foreach (var dockerImage in DockerImages)
    {
        LogSeparator("Packaging docker image '{0}'", dockerImage);

        var projectFileName = string.Format("./src/{0}/{0}.csproj", dockerImage);
        var dockerImageSpecificationFileName = string.Format("./deployment/docker/{0}/{0}", dockerImage);

        var outputDirectory = string.Format("{0}/{1}/", OutputRootDirectory, dockerImage);
        Information("Output directory: '{0}'", outputDirectory);

        Information("1) Using 'dotnet publish' to package '{0}'", dockerImage);

        var msBuildSettings = new DotNetCoreMSBuildSettings();

        // Note: we need to set OverridableOutputPath because we need to be able to respect
        // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
        // are properties passed in using the command line)
        msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
        msBuildSettings.WithProperty("PackageOutputPath", outputDirectory);
        msBuildSettings.WithProperty("ConfigurationName", ConfigurationName);
        msBuildSettings.WithProperty("PackageVersion", VersionNuGet);

        var publishSettings = new DotNetCorePublishSettings
        {
            MSBuildSettings = msBuildSettings,
            OutputDirectory = outputDirectory,
            Configuration = ConfigurationName
        };

        DotNetCorePublish(projectFileName, publishSettings);
        
        Information("2) Using 'docker build' to package '{0}'", dockerImage);

        // docker build ..\..\output\Release\platform -f .\Dockerfile

        // From the docs (https://docs.microsoft.com/en-us/azure/app-service/containers/tutorial-custom-docker-image#use-a-docker-image-from-any-private-registry-optional), 
        // we need something like this:
        // docker tag <azure-container-registry-name>.azurecr.io/mydockerimage
        var dockerRegistryUrl = GetDockerRegistryUrl(dockerImage);

        // Note: to prevent all output & source files to be copied to the docker context, we will set the
        // output directory as context (to keep the footprint as small as possible)

        var dockerSettings = new DockerImageBuildSettings
        {
            NoCache = true, // Don't use cache, always make sure to fetch the right images
            File = dockerImageSpecificationFileName,
            Platform = "linux",
            Tag = new string[] { GetDockerImageTag(dockerImage, VersionNuGet) }
        };

        ConfigureDockerSettings(dockerSettings);

        DockerBuild(dockerSettings, outputDirectory);

        LogSeparator();
    }
}

//-------------------------------------------------------------

private void DeployDockerImages()
{
    if (!HasDockerImages())
    {
        return;
    }

    foreach (var dockerImage in DockerImages)
    {
        if (!ShouldDeployProject(dockerImage))
        {
            Information("Docker image '{0}' should not be deployed", dockerImage);
            continue;
        }

        LogSeparator("Deploying docker image '{0}'", dockerImage);

        var dockerRegistryUrl = GetDockerRegistryUrl(dockerImage);
        var dockerRegistryUserName = GetDockerRegistryUserName(dockerImage);
        var dockerRegistryPassword = GetDockerRegistryPassword(dockerImage);
        var dockerImageName = GetDockerImageName(dockerImage);
        var dockerImageTag = GetDockerImageTag(dockerImage, VersionNuGet);
        var octopusRepositoryUrl = GetOctopusRepositoryUrl(dockerImage);
        var octopusRepositoryApiKey = GetOctopusRepositoryApiKey(dockerImage);
        var octopusDeploymentTarget = GetOctopusDeploymentTarget(dockerImage);

        if (string.IsNullOrWhiteSpace(dockerRegistryUrl))
        {
            throw new Exception("Docker registry url is empty, as a protection mechanism this must *always* be specified to make sure packages aren't accidentally deployed to some default public registry");
        }

        // Note: we are logging in each time because the registry might be different per container
        Information("Logging in to docker @ '{0}'", dockerRegistryUrl);

        var dockerLoginSettings = new DockerRegistryLoginSettings
        {
            Username = dockerRegistryUserName,
            Password = dockerRegistryPassword
        };

        ConfigureDockerSettings(dockerLoginSettings);

        DockerLogin(dockerLoginSettings, dockerRegistryUrl);

        try
        {
            Information("Pushing docker images with tag '{0}' to '{1}'", dockerImageTag, dockerRegistryUrl);

            var dockerImagePushSettings = new DockerImagePushSettings
            {
            };

            ConfigureDockerSettings(dockerImagePushSettings);

            DockerPush(dockerImagePushSettings, dockerImageTag);

            if (string.IsNullOrWhiteSpace(octopusRepositoryUrl))
            {
                Warning("Octopus Deploy url is not specified, skipping deployment to Octopus Deploy");
                continue;
            }

            Information("Creating release '{0}' in Octopus Deploy", VersionNuGet);

            OctoCreateRelease(dockerImage, new CreateReleaseSettings 
            {
                Server = octopusRepositoryUrl,
                ApiKey = octopusRepositoryApiKey,
                ReleaseNumber = VersionNuGet,
                DefaultPackageVersion = VersionNuGet,
                IgnoreExisting = true,
                Packages = new Dictionary<string, string>
                {
                    { dockerImageName, VersionNuGet }
                }
            });

            Information("Deploying release '{0}' via Octopus Deploy", VersionNuGet);

            OctoDeployRelease(octopusRepositoryUrl, octopusRepositoryApiKey, dockerImage, octopusDeploymentTarget, 
                VersionNuGet, new OctopusDeployReleaseDeploymentSettings
            {
                ShowProgress = true,
                WaitForDeployment = true,
                DeploymentTimeout = TimeSpan.FromMinutes(5),
                CancelOnTimeout = true,
                GuidedFailure = true,
                Force = true,
                NoRawLog = true,
            });
        }
        finally
        {
            Information("Logging out of docker @ '{0}'", dockerRegistryUrl);

            var dockerLogoutSettings = new DockerRegistryLogoutSettings
            {
            };

            ConfigureDockerSettings(dockerLogoutSettings);

            DockerLogout(dockerLogoutSettings, dockerRegistryUrl);
        }
    }
}

//-------------------------------------------------------------

Task("UpdateInfoForDockerImages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    UpdateSolutionAssemblyInfo();
    UpdateInfoForDockerImages();
});

//-------------------------------------------------------------

Task("BuildDockerImages")
    .IsDependentOn("UpdateInfoForDockerImages")
    .Does(() =>
{
    BuildDockerImages();
});

//-------------------------------------------------------------

Task("PackageDockerImages")
    .IsDependentOn("BuildDockerImages")
    .Does(() =>
{
    PackageDockerImages();
});

//-------------------------------------------------------------

Task("DeployDockerImages")
    .IsDependentOn("PackageDockerImages")
    .Does(() =>
{
    DeployDockerImages();
});