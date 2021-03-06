@echo off
setlocal

set PROJECT_DIR=%~1
set DOTNET_CMD=%~2
set TOOLRUNTIME_DIR=%~3
IF [%BUILDTOOLS_TARGET_RUNTIME%]==[] set BUILDTOOLS_TARGET_RUNTIME=win7-x64
set BUILDTOOLS_PACKAGE_DIR=%~dp0
set MSBUILD_CONTENT_JSON={"dependencies": {"Microsoft.Portable.Targets": "0.1.1-dev"},"frameworks": {"dnxcore50": {},"net46": {}}}

if not exist "%PROJECT_DIR%" (
  echo ERROR: Cannot find project root path at [%PROJECT_DIR%]. Please pass in the source directory as the 1st parameter.
  exit /b 1
)

if not exist "%DOTNET_CMD%" (
  echo ERROR: Cannot find dotnet cli at [%DOTNET_CMD%]. Please pass in the path to dotnet.exe as the 2nd parameter.
  exit /b 1
)

ROBOCOPY "%BUILDTOOLS_PACKAGE_DIR%\." "%TOOLRUNTIME_DIR%" /E

cd "%BUILDTOOLS_PACKAGE_DIR%\tool-runtime\"
call "%DOTNET_CMD%" restore --source https://www.myget.org/F/dotnet-core/ --source https://www.myget.org/F/dotnet-buildtools/ --source https://www.nuget.org/api/v2/
call "%DOTNET_CMD%" publish -f dnxcore50 -r %BUILDTOOLS_TARGET_RUNTIME% -o "%TOOLRUNTIME_DIR%"

:: Copy Portable Targets Over to ToolRuntime
mkdir "%BUILDTOOLS_PACKAGE_DIR%\portableTargets"
echo %MSBUILD_CONTENT_JSON% > "%BUILDTOOLS_PACKAGE_DIR%\portableTargets\project.json"
cd "%BUILDTOOLS_PACKAGE_DIR%\portableTargets\"
call "%DOTNET_CMD%" restore --source https://www.myget.org/F/dotnet-buildtools/ --packages "%BUILDTOOLS_PACKAGE_DIR%\portableTargets\packages\"
Robocopy "%BUILDTOOLS_PACKAGE_DIR%\portableTargets\packages\Microsoft.Portable.Targets\0.1.1-dev\contentFiles\any\any\." "%TOOLRUNTIME_DIR%\." /E

exit /b %ERRORLEVEL%