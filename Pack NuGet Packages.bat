rmdir /S /Q NuGet

dotnet pack src\CsvHelper -c release -o ..\..\NuGet --include-symbols -p:SymbolPackageFormat=snupkg

pause
