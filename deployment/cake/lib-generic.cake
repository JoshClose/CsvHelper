var _dotNetCoreCache = new Dictionary<string, bool>();

//-------------------------------------------------------------

private void LogSeparator(string messageFormat, params object[] args)
{
    Information("");
    Information("----------------------------------------");
    Information(messageFormat, args);
    Information("----------------------------------------");
    Information("");
}

//-------------------------------------------------------------

private void LogSeparator()
{
    Information("");
    Information("----------------------------------------");
    Information("");
}

//-------------------------------------------------------------

private void RestoreNuGetPackages(Cake.Core.IO.FilePath solutionOrProjectFileName)
{
    Information("Restoring packages for {0}", solutionOrProjectFileName);
    
    try
    {
        var nuGetRestoreSettings = new NuGetRestoreSettings
        {
        };

        if (!string.IsNullOrWhiteSpace(NuGetPackageSources))
        {
            var sources = new List<string>();

            foreach (var splitted in NuGetPackageSources.Split(new [] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                sources.Add(splitted);
            }
            
            if (sources.Count > 0)
            {
                nuGetRestoreSettings.Source = sources;
            }
        }

        NuGetRestore(solutionOrProjectFileName, nuGetRestoreSettings);
    }
    catch (Exception)
    {
        // Ignore
    }
}

//-------------------------------------------------------------

private void ConfigureMsBuild(MSBuildSettings msBuildSettings, string projectName, 
    string action = "build", bool? allowVsPrerelease = null)
{
    var toolPath = GetVisualStudioPath(allowVsPrerelease);
    if (!string.IsNullOrWhiteSpace(toolPath))
    {
        msBuildSettings.ToolPath = toolPath;
    }

    // Use as much CPU as possible
    msBuildSettings.MaxCpuCount = 0;
    
    // Enable for file logging
    msBuildSettings.AddFileLogger(new MSBuildFileLogger
    {
        Verbosity = msBuildSettings.Verbosity,
        //Verbosity = Verbosity.Diagnostic,
        LogFile = System.IO.Path.Combine(OutputRootDirectory, string.Format(@"MsBuild_{0}_{1}.log", projectName, action))
    });

    // Enable for bin logging
    msBuildSettings.BinaryLogger = new MSBuildBinaryLogSettings
    {
        Enabled = true,
        Imports = MSBuildBinaryLogImports.Embed,
        FileName = System.IO.Path.Combine(OutputRootDirectory, string.Format(@"MsBuild_{0}_{1}.binlog", projectName, action))
    };
}

//-------------------------------------------------------------

private void ConfigureMsBuildForDotNetCore(DotNetCoreMSBuildSettings msBuildSettings, string projectName, 
    string action = "build", bool? allowVsPrerelease = null)
{
    var toolPath = GetVisualStudioPath(allowVsPrerelease);
    if (!string.IsNullOrWhiteSpace(toolPath))
    {
        msBuildSettings.ToolPath = toolPath;
    }

    // Use as much CPU as possible
    msBuildSettings.MaxCpuCount = 0;
    
    // Enable for file logging
    msBuildSettings.AddFileLogger(new MSBuildFileLoggerSettings
    {
        Verbosity = msBuildSettings.Verbosity,
        //Verbosity = Verbosity.Diagnostic,
        LogFile = System.IO.Path.Combine(OutputRootDirectory, string.Format(@"MsBuild_{0}_{1}.log", projectName, action))
    });

    // Enable for bin logging
    //msBuildSettings.BinaryLogger = new MSBuildBinaryLogSettings
    //{
    //    Enabled = true,
    //    Imports = MSBuildBinaryLogImports.Embed,
    //    FileName = System.IO.Path.Combine(OutputRootDirectory, string.Format(@"MsBuild_{0}_{1}.binlog", projectName, action))
    //};
    
    // Note: this only works for direct .net core msbuild usage, not when this is
    // being wrapped in a tool (such as 'dotnet pack')
    var binLogArgs = string.Format("-bl:\"{0}\";ProjectImports=Embed", 
        System.IO.Path.Combine(OutputRootDirectory, string.Format(@"MsBuild_{0}_{1}.binlog", projectName, action)));

    msBuildSettings.ArgumentCustomization = args => args.Append(binLogArgs);
}

//-------------------------------------------------------------

private string GetVisualStudioPath(bool? allowVsPrerelease = null)
{
    if ((allowVsPrerelease ?? true) && UseVisualStudioPrerelease)
    {
        Debug("Checking for installation of Visual Studio 2019 preview");

        var pathFor2019Preview = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\MSBuild\Current\Bin\msbuild.exe";
        if (System.IO.File.Exists(pathFor2019Preview))
        {
           Information("Using Visual Studio 2019 preview, note that SonarQube will be disabled since it's not (yet) compatible with VS2019");
           SonarDisabled = true;
           return pathFor2019Preview;
        }

        Debug("Checking for installation of Visual Studio 2017 preview");

        var pathFor2017Preview = @"C:\Program Files (x86)\Microsoft Visual Studio\Preview\Professional\MSBuild\15.0\Bin\msbuild.exe";
        if (System.IO.File.Exists(pathFor2017Preview))
        {
            Information("Using Visual Studio 2017 preview");
            return pathFor2017Preview;
        }
    }
    
    Debug("Checking for installation of Visual Studio 2019");

    var pathFor2019 = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\MSBuild\Current\Bin\msbuild.exe";
    if (System.IO.File.Exists(pathFor2019))
    {
       Information("Using Visual Studio 2019");
       return pathFor2019;
    }

    Debug("Checking for installation of Visual Studio 2017");

    var pathFor2017 = @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\msbuild.exe";
    if (System.IO.File.Exists(pathFor2017))
    {
        Information("Using Visual Studio 2017");
        return pathFor2017;
    }

    // Failed
    return null;
}

//-------------------------------------------------------------

private string GetProjectDirectory(string projectName)
{
    var projectDirectory = string.Format("./src/{0}/", projectName);
    return projectDirectory;
}

//-------------------------------------------------------------

private string GetProjectOutputDirectory(string projectName)
{
    var projectDirectory = string.Format("{0}/{1}", OutputRootDirectory, projectName);
    return projectDirectory;
}

//-------------------------------------------------------------

private string GetProjectFileName(string projectName)
{
    var fileName = string.Format("{0}{1}.csproj", GetProjectDirectory(projectName), projectName);
    return fileName;
}

//-------------------------------------------------------------

private string GetProjectSlug(string projectName)
{
    var slug = projectName.Replace(".", "").Replace(" ", "");
    return slug;
}

//-------------------------------------------------------------

private string GetProjectSpecificConfigurationValue(string projectName, string configurationPrefix, string fallbackValue)
{
    // Allow per project overrides via "[configurationPrefix][projectName]"
    var slug = GetProjectSlug(projectName);
    var keyToCheck = string.Format("{0}{1}", configurationPrefix, slug);

    var value = GetBuildServerVariable(keyToCheck, fallbackValue);
    return value;
}

//-------------------------------------------------------------

private bool IsDotNetCoreProject(string projectName)
{
    var projectFileName = GetProjectFileName(projectName);

    if (!_dotNetCoreCache.TryGetValue(projectFileName, out var isDotNetCore))
    {
        isDotNetCore = false;

        var lines = System.IO.File.ReadAllLines(projectFileName);
        foreach (var line in lines)
        {
            // Match both *TargetFramework* and *TargetFrameworks* 
            var lowerCase = line.ToLower();
            if (lowerCase.Contains("targetframework"))
            {
                if (lowerCase.Contains("netcore"))
                {
                    isDotNetCore = true;
                    break;
                }
            }
        }

        _dotNetCoreCache[projectFileName] = isDotNetCore;
    }

    return _dotNetCoreCache[projectFileName];
}

//-------------------------------------------------------------

private bool ShouldProcessProject(string projectName)
{
    // Includes > Excludes
    var includes = Includes;
    if (includes.Count > 0)
    {
        var process = includes.Any(x => string.Equals(x, projectName, StringComparison.OrdinalIgnoreCase));

        if (!process)
        {
            Warning("Project '{0}' should not be processed, removing from projects to process", projectName);
        }

        return process;
    }

    var excludes = Excludes;
    if (excludes.Count > 0)
    {
        var process = !excludes.Any(x => string.Equals(x, projectName, StringComparison.OrdinalIgnoreCase));

        if (!process)
        {
            Warning("Project '{0}' should not be processed, removing from projects to process", projectName);
        }

        return process;
    }

    return true;
}

//-------------------------------------------------------------

private bool ShouldDeployProject(string projectName)
{
    // Allow the build server to configure this via "Deploy[ProjectName]"
    var slug = GetProjectSlug(projectName);
    var keyToCheck = string.Format("Deploy{0}", slug);

    var value = GetBuildServerVariable(keyToCheck, "True");
    
    Information("Value for '{0}': {1}", keyToCheck, value);
    
    var shouldDeploy = bool.Parse(value);
    return shouldDeploy;
}