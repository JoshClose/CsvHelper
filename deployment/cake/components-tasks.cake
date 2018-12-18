#l "components-variables.cake"

#addin "nuget:?package=Cake.FileHelpers&version=3.0.0"

using System.Xml.Linq;

//-------------------------------------------------------------

private string GetComponentNuGetRepositoryUrl(string projectName)
{
    // Allow per project overrides via "NuGetRepositoryUrlFor[ProjectName]"
    return GetProjectSpecificConfigurationValue(projectName, "NuGetRepositoryUrlFor", NuGetRepositoryUrl);
}

//-------------------------------------------------------------

private string GetComponentNuGetRepositoryApiKey(string projectName)
{
    // Allow per project overrides via "NuGetRepositoryApiKeyFor[ProjectName]"
    return GetProjectSpecificConfigurationValue(projectName, "NuGetRepositoryApiKeyFor", NuGetRepositoryApiKey);
}

//-------------------------------------------------------------

private void ValidateComponentsInput()
{
    // No validation required (yet)
}

//-------------------------------------------------------------

private bool HasComponents()
{
    return Components != null && Components.Count > 0;
}

//-------------------------------------------------------------

private async Task PrepareForComponentsAsync()
{
    if (!HasComponents())
    {
        return;
    }

    // Check whether projects should be processed, `.ToList()` 
    // is required to prevent issues with foreach
    foreach (var component in Components.ToList())
    {
        if (!ShouldProcessProject(component))
        {
            Components.Remove(component);
        }
    }

    if (IsLocalBuild && Target.ToLower().Contains("packagelocal"))
    {
        foreach (var component in Components)
        {
            var cacheDirectory = Environment.ExpandEnvironmentVariables(string.Format("%userprofile%/.nuget/packages/{0}/{1}", component, VersionNuGet));

            Information("Checking for existing local NuGet cached version at '{0}'", cacheDirectory);

            var retryCount = 3;

            while (retryCount > 0)
            {
                if (!DirectoryExists(cacheDirectory))
                {
                    break;
                }

                Information("Deleting already existing NuGet cached version from '{0}'", cacheDirectory);
                
                DeleteDirectory(cacheDirectory, new DeleteDirectorySettings()
                {
                    Force = true,
                    Recursive = true
                });

                await System.Threading.Tasks.Task.Delay(1000);

                retryCount--;
            }            
        }
    }
}

//-------------------------------------------------------------

private void UpdateInfoForComponents()
{
    if (!HasComponents())
    {
        return;
    }

    foreach (var component in Components)
    {
        Information("Updating version for component '{0}'", component);

        var projectFileName = GetProjectFileName(component);

        TransformConfig(projectFileName, new TransformationCollection 
        {
            { "Project/PropertyGroup/PackageVersion", VersionNuGet }
        });
    }
}

//-------------------------------------------------------------

private void BuildComponents()
{
    if (!HasComponents())
    {
        return;
    }
    
    foreach (var component in Components)
    {
        LogSeparator("Building component '{0}'", component);

        var projectFileName = GetProjectFileName(component);
        
        var msBuildSettings = new MSBuildSettings {
            Verbosity = Verbosity.Quiet,
            //Verbosity = Verbosity.Diagnostic,
            ToolVersion = MSBuildToolVersion.Default,
            Configuration = ConfigurationName,
            MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
            PlatformTarget = PlatformTarget.MSIL
        };

        var toolPath = GetVisualStudioPath(msBuildSettings.ToolVersion);
        if (!string.IsNullOrWhiteSpace(toolPath))
        {
            msBuildSettings.ToolPath = toolPath;
        }

        // msBuildSettings.AddFileLogger(new MSBuildFileLogger
        // {
        //     //Verbosity = msBuildSettings.Verbosity,
        //     Verbosity = Verbosity.Diagnostic,
        //     LogFile = System.IO.Path.Combine(OutputRootDirectory, string.Format(@"MsBuild_{0}_build.log", component))
        // });

        // Note: we need to set OverridableOutputPath because we need to be able to respect
        // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
        // are properties passed in using the command line)
        var outputDirectory = string.Format("{0}/{1}/", OutputRootDirectory, component);
        Information("Output directory: '{0}'", outputDirectory);
        msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
        msBuildSettings.WithProperty("PackageOutputPath", OutputRootDirectory);

        // SourceLink specific stuff
        var repositoryUrl = RepositoryUrl;
        if (!IsLocalBuild && !string.IsNullOrWhiteSpace(repositoryUrl))
        {       
            Information("Repository url is specified, enabling SourceLink to commit '{0}/commit/{1}'", repositoryUrl, RepositoryCommitId);

            // TODO: For now we are assuming everything is git, we might need to change that in the future
            // See why we set the values at https://github.com/dotnet/sourcelink/issues/159#issuecomment-427639278
            msBuildSettings.WithProperty("EnableSourceLink", "true");
            msBuildSettings.WithProperty("EnableSourceControlManagerQueries", "false");
            msBuildSettings.WithProperty("PublishRepositoryUrl", "true");
            msBuildSettings.WithProperty("RepositoryType", "git");
            msBuildSettings.WithProperty("RepositoryUrl", repositoryUrl);
            msBuildSettings.WithProperty("RevisionId", RepositoryCommitId);

            // For SourceLink to work, the .csproj should contain something like this:
            // <PackageReference Include="Microsoft.SourceLink.GitHub" Version="1.0.0-beta-63127-02" PrivateAssets="all" />
            var projectFileContents = System.IO.File.ReadAllText(projectFileName);
            if (!projectFileContents.Contains("Microsoft.SourceLink.GitHub"))
            {
                Warning("No SourceLink reference found, automatically injecting SourceLink package reference now");

                //const string MSBuildNS = (XNamespace) "http://schemas.microsoft.com/developer/msbuild/2003";

                var xmlDocument = XDocument.Parse(projectFileContents);
                var projectElement = xmlDocument.Root;

                // Item group with package reference
                var referencesItemGroup = new XElement("ItemGroup");
                var sourceLinkPackageReference = new XElement("PackageReference");
                sourceLinkPackageReference.Add(new XAttribute("Include", "Microsoft.SourceLink.GitHub"));
                sourceLinkPackageReference.Add(new XAttribute("Version", "1.0.0-beta-63127-02"));
                sourceLinkPackageReference.Add(new XAttribute("PrivateAssets", "all"));

                referencesItemGroup.Add(sourceLinkPackageReference);
                projectElement.Add(referencesItemGroup);

                // Item group with source root
                // <SourceRoot Include="{repository root}" RepositoryUrl="{repository url}"/>
                var sourceRootItemGroup = new XElement("ItemGroup");
                var sourceRoot = new XElement("SourceRoot");

                // Required to end with a \
                var sourceRootValue = RootDirectory;
                if (!sourceRootValue.EndsWith("\\"))
                {
                    sourceRootValue += "\\";
                };

                sourceRoot.Add(new XAttribute("Include", sourceRootValue));
                sourceRoot.Add(new XAttribute("RepositoryUrl", repositoryUrl));

                // Note: since we are not allowing source control manager queries (we don't want to require a .git directory),
                // we must specify the additional information below
                sourceRoot.Add(new XAttribute("SourceControl", "git"));
                sourceRoot.Add(new XAttribute("RevisionId", RepositoryCommitId));

                sourceRootItemGroup.Add(sourceRoot);
                projectElement.Add(sourceRootItemGroup);

                xmlDocument.Save(projectFileName);

                // Restore packages again for the dynamic package
                RestoreNuGetPackages(projectFileName);
            }
        }

        MSBuild(projectFileName, msBuildSettings);
    }
}

//-------------------------------------------------------------

private void PackageComponents()
{
    if (!HasComponents())
    {
        return;
    }

    foreach (var component in Components)
    {
        LogSeparator("Packaging component '{0}'", component);

        var projectDirectory = string.Format("./src/{0}", component);
        var projectFileName = string.Format("{0}/{1}.csproj", projectDirectory, component);

        var projectFileContents = FileReadText(projectFileName);
        if (!string.IsNullOrWhiteSpace(projectFileContents))
        {
            if (projectFileContents.ToLower().Contains("uap10.0"))
            {
                Warning("UAP 10.0 is detected as one of the target frameworks, make sure to install the latest version of .NET Core in order to pack UAP 10.0 assemblies. See https://github.com/dotnet/cli/issues/9303 for more info");
            }
        }

        var outputDirectory = string.Format("{0}/{1}/", OutputRootDirectory, component);
        Information("Output directory: '{0}'", outputDirectory);

        // Step 1: remove intermediate files to ensure we have the same results on the build server, somehow NuGet 
        // targets tries to find the resource assemblies in [ProjectName]\obj\Release\net46\de\[ProjectName].resources.dll',
        // we won't run a clean on the project since it will clean out the actual output (which we still need for packaging)

        Information("Cleaning intermediate files for component '{0}'", component);

        var binFolderPattern = string.Format("{0}/bin/{1}/**.dll", projectDirectory, ConfigurationName);

        Information("Deleting 'bin' directory contents using '{0}'", binFolderPattern);

        var binFiles = GetFiles(binFolderPattern);
        DeleteFiles(binFiles);

        var objFolderPattern = string.Format("{0}/obj/{1}/**.dll", projectDirectory, ConfigurationName);

        Information("Deleting 'bin' directory contents using '{0}'", objFolderPattern);

        var objFiles = GetFiles(objFolderPattern);
        DeleteFiles(objFiles);

        Information(string.Empty);

        // Step 2: Go packaging!

        Information("Using 'dotnet pack' to package '{0}'", component);

        var msBuildSettings = new DotNetCoreMSBuildSettings
        {
            //Verbosity = DotNetCoreVerbosity.Diagnostic // DotNetCoreVerbosity.Minimal,
        };

        // Note: we need to set OverridableOutputPath because we need to be able to respect
        // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
        // are properties passed in using the command line)
        msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
        msBuildSettings.WithProperty("PackageOutputPath", OutputRootDirectory);
        msBuildSettings.WithProperty("ConfigurationName", ConfigurationName);
        msBuildSettings.WithProperty("PackageVersion", VersionNuGet);

        // SourceLink specific stuff
        var repositoryUrl = RepositoryUrl;
        if (!IsLocalBuild && !string.IsNullOrWhiteSpace(repositoryUrl))
        {       
            Information("Repository url is specified, adding commit specific data to package");

            // TODO: For now we are assuming everything is git, we might need to change that in the future
            // See why we set the values at https://github.com/dotnet/sourcelink/issues/159#issuecomment-427639278
            msBuildSettings.WithProperty("PublishRepositoryUrl", "true");
            msBuildSettings.WithProperty("RepositoryType", "git");
            msBuildSettings.WithProperty("RepositoryUrl", repositoryUrl);
            msBuildSettings.WithProperty("RevisionId", RepositoryCommitId);
        }

        // Fix for .NET Core 3.0, see https://github.com/dotnet/core-sdk/issues/192, it
        // uses obj/release instead of [outputdirectory]
        msBuildSettings.WithProperty("DotNetPackIntermediateOutputPath", outputDirectory);

        // msBuildSettings.AddFileLogger(new MSBuildFileLoggerSettings
        // {
        //     Verbosity = DotNetCoreVerbosity.Diagnostic,
        //     LogFile = System.IO.Path.Combine(OutputRootDirectory, string.Format(@"MsBuild_{0}_pack.log", component))
        // });

        var packSettings = new DotNetCorePackSettings
        {
            MSBuildSettings = msBuildSettings,
            OutputDirectory = OutputRootDirectory,
            Configuration = ConfigurationName,
            NoBuild = true,
            Verbosity = msBuildSettings.Verbosity
        };

        DotNetCorePack(projectFileName, packSettings);
        
        LogSeparator();
    }

    var codeSign = (!IsCiBuild && !string.IsNullOrWhiteSpace(CodeSignCertificateSubjectName));
    if (codeSign)
    {
        // For details, see https://docs.microsoft.com/en-us/nuget/create-packages/sign-a-package
        // nuget sign MyPackage.nupkg -CertificateSubjectName <MyCertSubjectName> -Timestamper <TimestampServiceURL>
        var filesToSign = GetFiles(string.Format("{0}/*.nupkg", OutputRootDirectory));

        foreach (var fileToSign in filesToSign)
        {
            Information("Signing NuGet package '{0}' using certificate subject '{1}'", fileToSign, CodeSignCertificateSubjectName);

            var exitCode = StartProcess(NuGetExe, new ProcessSettings
            {
                Arguments = string.Format("sign \"{0}\" -CertificateSubjectName \"{1}\" -Timestamper \"{2}\"", fileToSign, CodeSignCertificateSubjectName, CodeSignTimeStampUri)
            });

            Information("Signing NuGet package exited with '{0}'", exitCode);
        }
    }
}

//-------------------------------------------------------------

private void DeployComponents()
{
    if (!HasComponents())
    {
        return;
    }

    foreach (var component in Components)
    {
        if (!ShouldDeployProject(component))
        {
            Information("Component '{0}' should not be deployed", component);
            continue;
        }

        LogSeparator("Deploying component '{0}'", component);

        var packageToPush = string.Format("{0}/{1}.{2}.nupkg", OutputRootDirectory, component, VersionNuGet);
        var nuGetRepositoryUrl = GetComponentNuGetRepositoryUrl(component);
        var nuGetRepositoryApiKey = GetComponentNuGetRepositoryApiKey(component);

        if (string.IsNullOrWhiteSpace(nuGetRepositoryUrl))
        {
            throw new Exception("NuGet repository is empty, as a protection mechanism this must *always* be specified to make sure packages aren't accidentally deployed to the default public NuGet feed");
        }

        NuGetPush(packageToPush, new NuGetPushSettings
        {
            Source = nuGetRepositoryUrl,
            ApiKey = nuGetRepositoryApiKey
        });
    }
}

//-------------------------------------------------------------

Task("UpdateInfoForComponents")
    .IsDependentOn("Clean")
    .Does(() =>
{
    UpdateSolutionAssemblyInfo();
    UpdateInfoForComponents();
});

//-------------------------------------------------------------

Task("BuildComponents")
    .IsDependentOn("UpdateInfoForComponents")
    .Does(() =>
{
    BuildComponents();
});

//-------------------------------------------------------------

Task("PackageComponents")
    .IsDependentOn("BuildComponents")
    .Does(() =>
{
    PackageComponents();
});

//-------------------------------------------------------------

Task("DeployComponents")
    .IsDependentOn("PackageComponents")
    .Does(() =>
{
    DeployComponents();
});