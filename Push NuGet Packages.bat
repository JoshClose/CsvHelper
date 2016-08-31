:: .\src\.nuget\NuGet.exe push .\NuGet\CsvHelper.*.symbols.nupkg -Source https://www.nuget.org/api/v2/package

@echo off

if [%1]==[] goto USAGE

.\src\.nuget\NuGet.exe push %1 -source nuget.org

goto DONE

:USAGE

@echo Usage: %0 ^<package.nupkg^>

:DONE
