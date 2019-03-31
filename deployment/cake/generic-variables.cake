#l "buildserver.cake"

//-------------------------------------------------------------

GitVersion _gitVersionContext;

public GitVersion GitVersionContext
{
    get
    {
        if (_gitVersionContext is null)
        {
            _gitVersionContext = GitVersion(new GitVersionSettings 
            {
                UpdateAssemblyInfo = false
            });
        }

        return _gitVersionContext;
    }
}

//-------------------------------------------------------------

// Target
var Target = GetBuildServerVariable("Target", "Default", showValue: true);

// Copyright info
var Company = GetBuildServerVariable("Company", showValue: true);
var StartYear = GetBuildServerVariable("StartYear", showValue: true);

// Versioning
var VersionMajorMinorPatch = GetBuildServerVariable("GitVersion_MajorMinorPatch", "unknown", showValue: true);
var VersionFullSemVer = GetBuildServerVariable("GitVersion_FullSemVer", "unknown", showValue: true);
var VersionNuGet = GetBuildServerVariable("GitVersion_NuGetVersion", "unknown", showValue: true);

// NuGet
var NuGetPackageSources = GetBuildServerVariable("NuGetPackageSources", showValue: false);
var NuGetExe = "./tools/nuget.exe";
var NuGetLocalPackagesDirectory = "c:\\source\\_packages";

// Solution / build info
var SolutionName = GetBuildServerVariable("SolutionName", showValue: true);
var SolutionAssemblyInfoFileName = "./src/SolutionAssemblyInfo.cs";
var SolutionFileName = string.Format("./src/{0}", string.Format("{0}.sln", SolutionName));
var IsCiBuild = bool.Parse(GetBuildServerVariable("IsCiBuild", "False", showValue: true));
var IsAlphaBuild = bool.Parse(GetBuildServerVariable("IsAlphaBuild", "False", showValue: true));
var IsBetaBuild = bool.Parse(GetBuildServerVariable("IsBetaBuild", "False", showValue: true));
var IsOfficialBuild = bool.Parse(GetBuildServerVariable("IsOfficialBuild", "False", showValue: true));
var IsLocalBuild = Target.ToLower().Contains("local");
var ConfigurationName = GetBuildServerVariable("ConfigurationName", "Release", showValue: true);

// If local, we want full pdb, so do a debug instead
if (IsLocalBuild)
{
    Warning("Enforcing configuration 'Debug' because this is seems to be a local build, do not publish this package!");
    ConfigurationName = "Debug";
}

var RootDirectory = System.IO.Path.GetFullPath(".");
var OutputRootDirectory = GetBuildServerVariable("OutputRootDirectory", string.Format("./output/{0}", ConfigurationName), showValue: true);

// Code signing
var CodeSignWildCard = GetBuildServerVariable("CodeSignWildcard", showValue: true);
var CodeSignCertificateSubjectName = GetBuildServerVariable("CodeSignCertificateSubjectName", Company, showValue: true);
var CodeSignTimeStampUri = GetBuildServerVariable("CodeSignTimeStampUri", "http://timestamp.comodoca.com/authenticode", showValue: true);

// Repository info
var RepositoryUrl = GetBuildServerVariable("RepositoryUrl", showValue: true);
var RepositoryBranchName = GetBuildServerVariable("RepositoryBranchName", showValue: true);
var RepositoryCommitId = GetBuildServerVariable("RepositoryCommitId", showValue: true);

// SonarQube
var SonarDisabled = bool.Parse(GetBuildServerVariable("SonarDisabled", "False", showValue: true));
var SonarUrl = GetBuildServerVariable("SonarUrl", showValue: true);
var SonarUsername = GetBuildServerVariable("SonarUsername", showValue: false);
var SonarPassword = GetBuildServerVariable("SonarPassword", showValue: false);
var SonarProject = GetBuildServerVariable("SonarProject", SolutionName, showValue: true);

// Visual Studio
var UseVisualStudioPrerelease = bool.Parse(GetBuildServerVariable("UseVisualStudioPrerelease", "False", showValue: true));

// Testing
var TestProcessBit = GetBuildServerVariable("TestProcessBit", "X86", showValue: true);

// Includes / Excludes
var Include = GetBuildServerVariable("Include", string.Empty, showValue: true);
var Exclude = GetBuildServerVariable("Exclude", string.Empty, showValue: true);

//-------------------------------------------------------------

// Update some variables (like expanding paths, etc)

if (VersionNuGet == "unknown")
{
    Information("No version info specified, falling back to GitVersion");

    var gitVersion = GitVersionContext;
    
    VersionMajorMinorPatch = gitVersion.MajorMinorPatch;
    VersionFullSemVer = gitVersion.FullSemVer;
    VersionNuGet = gitVersion.NuGetVersionV2;
}

if (string.IsNullOrWhiteSpace(RepositoryCommitId))
{
    Information("No commit id specified, falling back to GitVersion");

    var gitVersion = GitVersionContext;

    RepositoryCommitId = gitVersion.Sha;
}

OutputRootDirectory = System.IO.Path.GetFullPath(OutputRootDirectory);

//-------------------------------------------------------------

List<string> _includes;

public List<string> Includes
{
    get 
    {
        if (_includes is null)
        {
            var value = Include;
            var list = _includes = new List<string>();
            
            if (!string.IsNullOrWhiteSpace(value))
            {
                var splitted = value.Split(new [] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var split in splitted)
                {
                    list.Add(split.Trim());
                }
            }
        }

        return _includes;
    }
}

//-------------------------------------------------------------

List<string> _excludes;

public List<string> Excludes
{
    get 
    {
        if (_excludes is null)
        {
            var value = Exclude;
            var list = _excludes = new List<string>();
            
            if (!string.IsNullOrWhiteSpace(value))
            {
                var splitted = value.Split(new [] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var split in splitted)
                {
                    list.Add(split.Trim());
                }
            }
        }

        return _excludes;
    }
}

//-------------------------------------------------------------

List<string> _testProjects;

public List<string> TestProjects
{
    get 
    {
        if (_testProjects is null)
        {
            _testProjects = new List<string>();
        }

        return _testProjects;
    }
}