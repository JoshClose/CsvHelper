// Customize this file when using a different test framework
#l "tests-nunit.cake"
#l "buildserver.cake"

var TestFramework = GetBuildServerVariable("TestFramework", "nunit", showValue: true);
var TestTargetFramework = GetBuildServerVariable("TestTargetFramework", "net46", showValue: true);

//-------------------------------------------------------------

private void RunUnitTests(string projectName)
{
    var testResultsDirectory = string.Format("{0}/testresults/{1}/", OutputRootDirectory, projectName);

    CreateDirectory(testResultsDirectory);

    var ranTests = false;
    var failed = false;

    try
    {
        if (IsDotNetCoreProject(projectName))
        {
            Information("Project '{0}' is a .NET core project, using 'dotnet test' to run the unit tests", projectName);

            var projectFileName = GetProjectFileName(projectName);

            DotNetCoreTest(projectFileName, new DotNetCoreTestSettings
            {
                Configuration = ConfigurationName,
                NoRestore = true,
                NoBuild = true,
                OutputDirectory = string.Format("{0}/{1}", GetProjectOutputDirectory(projectName), TestTargetFramework),
                ResultsDirectory = testResultsDirectory
            });

            // Information("Project '{0}' is a .NET core project, using 'dotnet vstest' to run the unit tests", projectName); 

            // var testFile = string.Format("{0}/{1}/{2}.dll", GetProjectOutputDirectory(projectName), TestTargetFramework, projectName);

            // DotNetCoreVSTest(testFile, new DotNetCoreVSTestSettings
            // {
            //     //Platform = TestFramework
            //     ResultsDirectory = testResultsDirectory
            // });

            ranTests = true;
        }
        else
        {
            Information("Project '{0}' is a .NET project, using '{1} runner' to run the unit tests", projectName, TestFramework);

            if (TestFramework.ToLower().Equals("nunit"))
            {
                RunTestsUsingNUnit(projectName, TestTargetFramework, testResultsDirectory);

                ranTests = true;
            }
        }
    }
    catch (Exception ex)
    {
        Warning("An exception occurred: {0}", ex.Message);

        failed = true;   
    }

    if (ranTests)
    {
        Information("Results are available in '{0}'", testResultsDirectory);
    }
    else if (failed)
    {
        throw new Exception("Unit test execution failed");
    }
    else
    {
        Warning("No tests were executed, check whether the used test framework '{0}' is available", TestFramework);
    }
}

//-------------------------------------------------------------