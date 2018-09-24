setlocal

set NUGET_BINARY=%~dp0lib\NuGet.exe
%~dp0lib\fossa.exe build
%~dp0lib\fossa.exe

endlocal

pause
