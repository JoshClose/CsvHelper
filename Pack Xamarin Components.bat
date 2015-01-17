if not exist Component mkdir Component

del /Q Component\*.*

.\src\.component\xamarin-component.exe package .\src
move .\src\*.xam .\Component

pause
