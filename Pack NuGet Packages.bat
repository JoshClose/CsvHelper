if not exist NuGet mkdir NuGet

del /Q NuGet\*.*

.\src\.nuget\NuGet.exe pack .\src\CsvHelper.nuspec -OutputDirectory NuGet

pause
