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
var Target = GetBuildServerVariable("Target", "Default");

// Copyright info
var Company = GetBuildServerVariable("Company");
var StartYear = GetBuildServerVariable("StartYear");

// Versioning
var VersionMajorMinorPatch = GetBuildServerVariable("GitVersion_MajorMinorPatch", "unknown");
var VersionFullSemVer = GetBuildServerVariable("GitVersion_FullSemVer", "unknown");
var VersionNuGet = GetBuildServerVariable("GitVersion_NuGetVersion", "unknown");

// NuGet
var NuGetPackageSources = GetBuildServerVariable("NuGetPackageSources");
var NuGetExe = "./tools/nuget.exe";
var NuGetLocalPackagesDirectory = "c:\\source\\_packages";

// Solution / build info
var SolutionName = GetBuildServerVariable("SolutionName");
var SolutionAssemblyInfoFileName = "./src/SolutionAssemblyInfo.cs";
var SolutionFileName = string.Format("./src/{0}", string.Format("{0}.sln", SolutionName));
var IsCiBuild = bool.Parse(GetBuildServerVariable("IsCiBuild", "False"));
var IsAlphaBuild = bool.Parse(GetBuildServerVariable("IsAlphaBuild", "False"));
var IsBetaBuild = bool.Parse(GetBuildServerVariable("IsBetaBuild", "False"));
var IsOfficialBuild = bool.Parse(GetBuildServerVariable("IsOfficialBuild", "False"));
var IsLocalBuild = Target.ToLower().Contains("local");
var ConfigurationName = GetBuildServerVariable("ConfigurationName", "Release");

// If local, we want full pdb, so do a debug instead
if (IsLocalBuild)
{
    Warning("Enforcing configuration 'Debug' because this is seems to be a local build, do not publish this package!");
    ConfigurationName = "Debug";
}

var RootDirectory = System.IO.Path.GetFullPath(".");
var OutputRootDirectory = GetBuildServerVariable("OutputRootDirectory", string.Format("./output/{0}", ConfigurationName));

// Code signing
var CodeSignWildCard = GetBuildServerVariable("CodeSignWildcard");
var CodeSignCertificateSubjectName = GetBuildServerVariable("CodeSignCertificateSubjectName", Company);
var CodeSignTimeStampUri = GetBuildServerVariable("CodeSignTimeStampUri", "http://timestamp.comodoca.com/authenticode");

// Repository info
var RepositoryUrl = GetBuildServerVariable("RepositoryUrl");
var RepositoryBranchName = GetBuildServerVariable("RepositoryBranchName");
var RepositoryCommitId = GetBuildServerVariable("RepositoryCommitId");

// SonarQube
var SonarDisabled = bool.Parse(GetBuildServerVariable("SonarDisabled", "False"));
var SonarUrl = GetBuildServerVariable("SonarUrl");
var SonarUsername = GetBuildServerVariable("SonarUsername");
var SonarPassword = GetBuildServerVariable("SonarPassword");
var SonarProject = GetBuildServerVariable("SonarProject", SolutionName);

// Visual Studio
var UseVisualStudioPrerelease = bool.Parse(GetBuildServerVariable("UseVisualStudioPrerelease", "False"));

// Testing
var TestProcessBit = GetBuildServerVariable("TestProcessBit", "X86");

// Includes / Excludes
var Include = GetBuildServerVariable("Include", string.Empty);
var Exclude = GetBuildServerVariable("Exclude", string.Empty);

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