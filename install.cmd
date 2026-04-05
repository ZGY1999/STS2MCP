@echo off
setlocal EnableExtensions DisableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
set "GAME_DIR="
set "CONFIGURATION=Release"
set "SKIP_BUILD=0"
set "BACKUP_EXISTING=0"
set "NO_PAUSE=0"
set "EXITCODE=0"

call :parse_args %*
if errorlevel 1 (
    set "EXITCODE=1"
    goto finish
)

if not defined GAME_DIR if defined STS2_GAME_DIR set "GAME_DIR=%STS2_GAME_DIR%"

if not defined GAME_DIR (
    echo.
    set /P "GAME_DIR=Enter your Slay the Spire 2 install folder: "
)

if not defined GAME_DIR (
    call :fail "Game directory not specified."
    set "EXITCODE=1"
    goto finish
)

if not exist "%GAME_DIR%" (
    call :fail "Game directory does not exist: %GAME_DIR%"
    set "EXITCODE=1"
    goto finish
)

set "DLL_DIR=%GAME_DIR%\data_sts2_windows_x86_64"
if not exist "%DLL_DIR%\sts2.dll" (
    call :fail "Could not find sts2.dll in %DLL_DIR%. Make sure the path points to the game install root."
    set "EXITCODE=1"
    goto finish
)

where dotnet >nul 2>nul
if errorlevel 1 (
    call :fail "dotnet was not found. Install the .NET 9 SDK first."
    set "EXITCODE=1"
    goto finish
)

for /f %%I in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss"') do set "TIMESTAMP=%%I"

set "BUILD_SCRIPT=%SCRIPT_DIR%build.ps1"
set "OUT_DIR=%SCRIPT_DIR%out\STS2_MCP"
set "BUILT_DLL=%OUT_DIR%\STS2_MCP.dll"
set "MANIFEST_PATH=%SCRIPT_DIR%mod_manifest.json"
set "ASSET_OUT_DIR=%OUT_DIR%\STS2_MCP.assets"
set "MODS_DIR=%GAME_DIR%\mods"
set "INSTALLED_DLL=%MODS_DIR%\STS2_MCP.dll"
set "INSTALLED_MANIFEST=%MODS_DIR%\STS2_MCP.json"
set "INSTALLED_ASSETS=%MODS_DIR%\STS2_MCP.assets"

if not exist "%BUILD_SCRIPT%" (
    call :fail "Build script not found: %BUILD_SCRIPT%"
    set "EXITCODE=1"
    goto finish
)

if not exist "%MANIFEST_PATH%" (
    call :fail "Manifest file not found: %MANIFEST_PATH%"
    set "EXITCODE=1"
    goto finish
)

if "%SKIP_BUILD%"=="0" (
    call :step "Building STS2_MCP"
    powershell -NoProfile -ExecutionPolicy Bypass -File "%BUILD_SCRIPT%" -GameDir "%GAME_DIR%" -Configuration "%CONFIGURATION%"
    if errorlevel 1 (
        call :fail "Build failed."
        set "EXITCODE=1"
        goto finish
    )
) else (
    call :step "Skipping build"
    echo Using existing files in: %OUT_DIR%
)

if not exist "%BUILT_DLL%" (
    call :fail "Built mod DLL not found: %BUILT_DLL%"
    set "EXITCODE=1"
    goto finish
)

call :step "Installing to game"
if not exist "%MODS_DIR%" mkdir "%MODS_DIR%"

call :backup_if_requested "%INSTALLED_DLL%"
if errorlevel 1 (
    set "EXITCODE=1"
    goto finish
)

call :backup_if_requested "%INSTALLED_MANIFEST%"
if errorlevel 1 (
    set "EXITCODE=1"
    goto finish
)

copy /Y "%BUILT_DLL%" "%INSTALLED_DLL%" >nul
if errorlevel 1 (
    call :fail "Failed to install %INSTALLED_DLL%"
    set "EXITCODE=1"
    goto finish
)

copy /Y "%MANIFEST_PATH%" "%INSTALLED_MANIFEST%" >nul
if errorlevel 1 (
    call :fail "Failed to install %INSTALLED_MANIFEST%"
    set "EXITCODE=1"
    goto finish
)

echo Installed: %INSTALLED_DLL%
echo Installed: %INSTALLED_MANIFEST%

if exist "%ASSET_OUT_DIR%" (
    call :install_assets "%ASSET_OUT_DIR%" "%INSTALLED_ASSETS%"
    if errorlevel 1 (
        set "EXITCODE=1"
        goto finish
    )
)

call :step "Install complete"
echo Game directory : %GAME_DIR%
echo Mods directory : %MODS_DIR%
echo.
echo You can launch the game now.
goto finish

:parse_args
if "%~1"=="" exit /b 0

if /I "%~1"=="-GameDir" (
    if "%~2"=="" (
        call :fail "Missing value for -GameDir."
        exit /b 1
    )
    set "GAME_DIR=%~2"
    shift
    shift
    goto parse_args
)

if /I "%~1"=="-Configuration" (
    if "%~2"=="" (
        call :fail "Missing value for -Configuration."
        exit /b 1
    )
    if /I not "%~2"=="Debug" if /I not "%~2"=="Release" (
        call :fail "Invalid configuration '%~2'. Use Debug or Release."
        exit /b 1
    )
    set "CONFIGURATION=%~2"
    shift
    shift
    goto parse_args
)

if /I "%~1"=="-SkipBuild" (
    set "SKIP_BUILD=1"
    shift
    goto parse_args
)

if /I "%~1"=="-BackupExisting" (
    set "BACKUP_EXISTING=1"
    shift
    goto parse_args
)

if /I "%~1"=="-NoPause" (
    set "NO_PAUSE=1"
    shift
    goto parse_args
)

if /I "%~1"=="-Help" (
    call :usage
    exit /b 1
)

if "%~1"=="/?" (
    call :usage
    exit /b 1
)

if not defined GAME_DIR (
    set "GAME_DIR=%~1"
    shift
    goto parse_args
)

call :fail "Unknown argument: %~1"
exit /b 1

:backup_if_requested
set "TARGET=%~1"

if not "%BACKUP_EXISTING%"=="1" exit /b 0
if not exist "%TARGET%" exit /b 0

move /Y "%TARGET%" "%TARGET%.bak-%TIMESTAMP%" >nul
if errorlevel 1 (
    call :fail "Failed to back up %TARGET%"
    exit /b 1
)

echo Backed up: %TARGET%.bak-%TIMESTAMP%
exit /b 0

:install_assets
set "SOURCE=%~1"
set "DEST=%~2"

call :backup_if_requested "%DEST%"
if errorlevel 1 exit /b 1

if "%BACKUP_EXISTING%"=="0" if exist "%DEST%" rmdir /S /Q "%DEST%"
if not exist "%DEST%" mkdir "%DEST%"

robocopy "%SOURCE%" "%DEST%" /E /NFL /NDL /NJH /NJS /NC /NS >nul
set "ROBOCODE=%ERRORLEVEL%"
if %ROBOCODE% GEQ 8 (
    call :fail "Failed to install assets into %DEST%"
    exit /b 1
)

echo Installed: %DEST%
exit /b 0

:step
echo.
echo == %~1 ==
exit /b 0

:fail
echo ERROR: %~1
exit /b 0

:usage
echo Usage:
echo   install.cmd -GameDir "C:\Path\To\Slay the Spire 2" [-Configuration Debug^|Release] [-SkipBuild] [-BackupExisting] [-NoPause]
echo.
echo If you launch install.cmd by double-clicking it, the script will prompt for the game directory and stay open when it finishes.
exit /b 0

:finish
echo.
if "%NO_PAUSE%"=="1" exit /b %EXITCODE%
pause
exit /b %EXITCODE%
