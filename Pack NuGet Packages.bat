rmdir /S /Q NuGet

@rem This currently doesn't work with net20 and net35.
@rem dotnet pack src\CsvHelper --configuration release --output NuGet

set msbuild="C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\msbuild"
%msbuild% src\CsvHelper\ /t:Pack /p:Configuration=Release;PackageOutputPath=..\..\NuGet

pause
