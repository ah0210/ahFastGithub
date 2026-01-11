@echo off
setlocal enabledelayedexpansion

echo.
echo ================================================
echo    FastGithub Unified Publish Script (Windows Only)
echo    Version 2.3.0
echo ================================================
echo.

REM Set output directories
set output=./publish
set winx64_with_dnscrypt=%output%/fastgithub_win-x64_with_dnscrypt
set winx64_without_dnscrypt=%output%/fastgithub_win-x64_without_dnscrypt

REM Clean old output directory
echo Cleaning old output directory...
if exist "%output%" (
    echo Deleting %output% directory
    rd /S /Q "%output%"
)

REM Create output directories
echo Creating output directories...
mkdir "%winx64_with_dnscrypt%"
mkdir "%winx64_without_dnscrypt%"

REM 1. Build version with DNSCrypt-Proxy for Windows x64
echo.
echo ==================================================
echo Building Windows x64 version with DNSCrypt-Proxy...
echo ==================================================
dotnet publish -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true --self-contained -r win-x64 -o "%winx64_with_dnscrypt%" ./FastGithub/FastGithub.csproj
if errorlevel 1 (
    echo Error: Build Windows x64 version with DNSCrypt-Proxy failed
    exit /b 1
)

REM 2. Build version without DNSCrypt-Proxy for Windows x64
echo.
echo ==================================================
echo Building Windows x64 version without DNSCrypt-Proxy...
echo ==================================================
dotnet publish -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true --self-contained -r win-x64 -o "%winx64_without_dnscrypt%" ./FastGithub/FastGithub.csproj
if errorlevel 1 (
    echo Error: Build Windows x64 version without DNSCrypt-Proxy failed
    exit /b 1
)

REM 3. Remove DNSCrypt-Proxy related files from the version without DNSCrypt-Proxy
echo.
echo ==================================================
echo Cleaning DNSCrypt-Proxy related files from version without DNSCrypt-Proxy...
echo ==================================================
if exist "%winx64_without_dnscrypt%\dnscrypt-proxy" (
    echo Deleting %winx64_without_dnscrypt%\dnscrypt-proxy directory
    rd /S /Q "%winx64_without_dnscrypt%\dnscrypt-proxy"
)

REM 4. Build UI project to all Windows version directories
echo.
echo ==================================================
echo Building UI project for Windows versions...
echo ==================================================
echo Building UI project to Windows x64 with DNSCrypt-Proxy...
dotnet publish -c Release -o "%winx64_with_dnscrypt%" ./FastGithub.UI/FastGithub.UI.csproj
if errorlevel 1 (
    echo Warning: Building UI project to Windows x64 with DNSCrypt-Proxy failed, but continue
)

echo Building UI project to Windows x64 without DNSCrypt-Proxy...
dotnet publish -c Release -o "%winx64_without_dnscrypt%" ./FastGithub.UI/FastGithub.UI.csproj
if errorlevel 1 (
    echo Warning: Building UI project to Windows x64 without DNSCrypt-Proxy failed, but continue
)

REM 5. Show build results
echo.
echo ==================================================
echo Build completed!
echo ==================================================
echo.
echo Output directories:
echo   Windows x64 with DNSCrypt-Proxy: %winx64_with_dnscrypt%
echo   Windows x64 without DNSCrypt-Proxy: %winx64_without_dnscrypt%
echo.
echo Both versions have been built successfully. Choose the appropriate version for your platform:
echo.
echo With DNSCrypt-Proxy versions:
echo   - Suitable for users who care about DNS privacy and security
echo   - Contains complete dnscrypt-proxy directory and related files
echo   - Supports secure encrypted DNS resolution using DNSCrypt-Proxy
echo.
echo Without DNSCrypt-Proxy versions:
echo   - Smaller size, faster startup
echo   - Suitable for users who focus on core functions and performance
echo   - Uses FallbackDns list in configuration file for DNS resolution
echo.
pause