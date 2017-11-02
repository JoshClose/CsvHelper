rmdir /S /Q NuGet

dotnet pack src\CsvHelper -c release -o ..\..\NuGet\Pack
dotnet pack src\CsvHelper -c release -o ..\..\NuGet\Symbols --include-symbols

pause
