@echo off
echo Removing existing releases...
RMDIR /S/Q release\
echo Removed
echo Building win-x64 single EXE...
dotnet add package Microsoft.DotNet.ILCompiler -v 1.0.0-alpha-* 
dotnet publish -r win-x64 -c WinX64
dotnet remove package Microsoft.DotNet.ILCompiler
echo win-x64 single EXE complete
echo Building portable release...
dotnet publish -c release
echo Portable release complete
mkdir release
mkdir release\portable\
mkdir release\win-x64\
xcopy /s bin\Release\netcoreapp2.1\publish release\portable\
xcopy /s bin\WinX64\netcoreapp2.1\win-x64\publish release\win-x64\ /exclude:build-exclude.txt