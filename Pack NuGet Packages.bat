rmdir /S /Q NuGet

dotnet pack src\CsvHelper --configuration release --output NuGet

pause
