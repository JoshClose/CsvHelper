dotnet nuget push NuGet\Pack\*.nupkg -s nuget.org
dotnet nuget push NuGet\Symbols\*.symbols.nupkg -s https://nuget.smbsrc.net/

pause
