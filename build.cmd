@echo off
setlocal
rem Builds PixelBuilder with the .NET Framework C# compiler (C# 5).
rem Output goes to bin\ and never overwrites the .exe files that already lie in this folder.
rem Usage: build.cmd          - build the editor, the test runner and the import probe
rem        build.cmd test     - build and run the tests
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo csc.exe not found
  exit /b 1
)
cd /d "%~dp0"
if not exist bin mkdir bin
set "REFS=/r:System.Windows.Forms.dll /r:System.Drawing.dll"
"%CSC%" /nologo /codepage:65001 /target:winexe /out:bin\PixelBuilder.exe %REFS% /r:System.Web.Extensions.dll /recurse:src\*.cs
if errorlevel 1 exit /b 1
"%CSC%" /nologo /codepage:65001 /target:exe /out:bin\PixelBuilder.Tests.exe /r:bin\PixelBuilder.exe %REFS% tests\Tests.cs tests\BuildingTests.cs
if errorlevel 1 exit /b 1
"%CSC%" /nologo /codepage:65001 /target:exe /out:bin\ImportProbe.exe /r:bin\PixelBuilder.exe %REFS% tests\ImportProbe.cs
if errorlevel 1 exit /b 1
echo BUILD OK
if /i "%~1"=="test" (
  bin\PixelBuilder.Tests.exe
  bin\ImportProbe.exe tests\legacy_bases
)
