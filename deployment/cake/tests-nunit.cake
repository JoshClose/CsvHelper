#tool "nuget:?package=NUnit.ConsoleRunner&version=3.9.0"

//-------------------------------------------------------------

private void RunTestsUsingNUnit(string projectName, string testTargetFramework, string testResultsDirectory)
{
    var testFile = string.Format("{0}/{1}/{2}.dll", GetProjectOutputDirectory(projectName), testTargetFramework, projectName);
    var resultsFile = string.Format("{0}testresults.xml", testResultsDirectory);

    // Note: although the docs say you can use without array initialization, you can't
    NUnit3(new string[] { testFile }, new NUnit3Settings
    {
        Results = new NUnit3Result[] 
        {
            new NUnit3Result
            {
                FileName = resultsFile,
                Format = "nunit3"
            }
        },
        NoHeader = true,
        NoColor = true,
        NoResults = false,
        X86 = string.Equals(TestProcessBit, "X86", StringComparison.OrdinalIgnoreCase)
        //Work = testResultsDirectory
    });

    Information("Verifying whether results file '{0}' exists", resultsFile);

    if (!FileExists(resultsFile))
    {
        throw new Exception(string.Format("Expected results file '{0}' does not exist", resultsFile));
    }
}