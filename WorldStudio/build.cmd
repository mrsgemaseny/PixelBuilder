@echo off
setlocal
rem WorldStudio: build with the .NET Framework C# compiler (C# 5, .NET 4.5+), same toolchain as PixelBuilder.
rem Output: WorldStudio\bin\ (WorldStudio.exe, WorldStudio.Core.dll, data\). PixelBuilder files are only read.
rem Usage: build.cmd        - build the editor and the test runner
rem        build.cmd test   - build and run the tests (and the compatibility probe if PixelBuilder is built)
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo csc.exe not found
  exit /b 1
)
cd /d "%~dp0"
if not exist bin mkdir bin
if not exist bin\data mkdir bin\data
copy /y data\*.json bin\data\ >nul

rem Core is compiled WITHOUT System.Windows.Forms: domain, serialization and validation cannot depend on UI.
"%CSC%" /nologo /codepage:65001 /warnaserror+ /target:library /out:bin\WorldStudio.Core.dll /r:System.dll /r:System.Core.dll /r:System.Xml.dll /r:System.IO.Compression.dll /recurse:src\Core\*.cs
if errorlevel 1 exit /b 1
"%CSC%" /nologo /codepage:65001 /target:winexe /out:bin\WorldStudio.exe /r:bin\WorldStudio.Core.dll /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /recurse:src\UI\*.cs
if errorlevel 1 exit /b 1
"%CSC%" /nologo /codepage:65001 /warnaserror+ /target:exe /out:bin\WorldStudio.Tests.exe /r:bin\WorldStudio.Core.dll tests\Tests.cs tests\Tests.World.cs
if errorlevel 1 exit /b 1

rem Compatibility probe needs the real old editor: ..\bin\PixelBuilder.exe (build it with ..\build.cmd).
set "COMPAT=0"
if exist "..\bin\PixelBuilder.exe" (
  copy /y "..\bin\PixelBuilder.exe" bin\PixelBuilder.exe >nul
  "%CSC%" /nologo /codepage:65001 /target:exe /out:bin\WorldStudio.CompatProbe.exe /r:bin\WorldStudio.Core.dll /r:bin\PixelBuilder.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll tests\CompatProbe.cs
  if errorlevel 1 exit /b 1
  set "COMPAT=1"
) else (
  echo SKIP compat probe: ..\bin\PixelBuilder.exe not found
)
echo BUILD OK

if /i "%~1"=="test" (
  if not exist bin\export mkdir bin\export
  bin\WorldStudio.Tests.exe tests\fixtures ..\tests\legacy_bases bin\export
  if errorlevel 1 exit /b 1
  if "%COMPAT%"=="1" (
    bin\WorldStudio.CompatProbe.exe tests\fixtures ..\tests\legacy_bases bin\export
    if errorlevel 1 exit /b 1
  )
)
