rmdir /S /Q NuGet

dotnet pack src\CsvHelper --include-symbols --configuration release --output ..\..\NuGet

pause
