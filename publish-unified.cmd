@echo off
setlocal enabledelayedexpansion

echo.
echo ================================================
echo    FastGithub Unified Publish Script
echo    Version 2.3.0
echo ================================================
echo.

REM Set output directories
set output=./publish
set winx64_with_dnscrypt=%output%/fastgithub_win-x64_with_dnscrypt
set winx64_without_dnscrypt=%output%/fastgithub_win-x64_without_dnscrypt
set linuxx64_with_dnscrypt=%output%/fastgithub_linux-x64_with_dnscrypt
set linuxx64_without_dnscrypt=%output%/fastgithub_linux-x64_without_dnscrypt
set linuxarm64_with_dnscrypt=%output%/fastgithub_linux-arm64_with_dnscrypt
set linuxarm64_without_dnscrypt=%output%/fastgithub_linux-arm64_without_dnscrypt
set osxx64_with_dnscrypt=%output%/fastgithub_osx-x64_with_dnscrypt
set osxx64_without_dnscrypt=%output%/fastgithub_osx-x64_without_dnscrypt
set osxarm64_with_dnscrypt=%output%/fastgithub_osx-arm64_with_dnscrypt
set osxarm64_without_dnscrypt=%output%/fastgithub_osx-arm64_without_dnscrypt

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
mkdir "%linuxx64_with_dnscrypt%"
mkdir "%linuxx64_without_dnscrypt%"
mkdir "%linuxarm64_with_dnscrypt%"
mkdir "%linuxarm64_without_dnscrypt%"
mkdir "%osxx64_with_dnscrypt%"
mkdir "%osxx64_without_dnscrypt%"
mkdir "%osxarm64_with_dnscrypt%"
mkdir "%osxarm64_without_dnscrypt%"

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

REM 3. Build version with DNSCrypt-Proxy for Linux x64
echo.
echo ==================================================
echo Building Linux x64 version with DNSCrypt-Proxy...
echo ==================================================
dotnet publish -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true --self-contained -r linux-x64 -o "%linuxx64_with_dnscrypt%" ./FastGithub/FastGithub.csproj
if errorlevel 1 (
    echo Error: Build Linux x64 version with DNSCrypt-Proxy failed
    exit /b 1
)

REM 4. Build version without DNSCrypt-Proxy for Linux x64
echo.
echo ==================================================
echo Building Linux x64 version without DNSCrypt-Proxy...
echo ==================================================
dotnet publish -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true --self-contained -r linux-x64 -o "%linuxx64_without_dnscrypt%" ./FastGithub/FastGithub.csproj
if errorlevel 1 (
    echo Error: Build Linux x64 version without DNSCrypt-Proxy failed
    exit /b 1
)

REM 5. Build version with DNSCrypt-Proxy for Linux ARM64
echo.
echo ==================================================
echo Building Linux ARM64 version with DNSCrypt-Proxy...
echo ==================================================
dotnet publish -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true --self-contained -r linux-arm64 -o "%linuxarm64_with_dnscrypt%" ./FastGithub/FastGithub.csproj
if errorlevel 1 (
    echo Error: Build Linux ARM64 version with DNSCrypt-Proxy failed
    exit /b 1
)

REM 6. Build version without DNSCrypt-Proxy for Linux ARM64
echo.
echo ==================================================
echo Building Linux ARM64 version without DNSCrypt-Proxy...
echo ==================================================
dotnet publish -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true --self-contained -r linux-arm64 -o "%linuxarm64_without_dnscrypt%" ./FastGithub/FastGithub.csproj
if errorlevel 1 (
    echo Error: Build Linux ARM64 version without DNSCrypt-Proxy failed
    exit /b 1
)

REM 7. Build version with DNSCrypt-Proxy for macOS x64
echo.
echo ==================================================
echo Building macOS x64 version with DNSCrypt-Proxy...
echo ==================================================
dotnet publish -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true --self-contained -r osx-x64 -o "%osxx64_with_dnscrypt%" ./FastGithub/FastGithub.csproj
if errorlevel 1 (
    echo Error: Build macOS x64 version with DNSCrypt-Proxy failed
    exit /b 1
)

REM 8. Build version without DNSCrypt-Proxy for macOS x64
echo.
echo ==================================================
echo Building macOS x64 version without DNSCrypt-Proxy...
echo ==================================================
dotnet publish -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true --self-contained -r osx-x64 -o "%osxx64_without_dnscrypt%" ./FastGithub/FastGithub.csproj
if errorlevel 1 (
    echo Error: Build macOS x64 version without DNSCrypt-Proxy failed
    exit /b 1
)

REM 9. Build version with DNSCrypt-Proxy for macOS ARM64
echo.
echo ==================================================
echo Building macOS ARM64 version with DNSCrypt-Proxy...
echo ==================================================
dotnet publish -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true --self-contained -r osx-arm64 -o "%osxarm64_with_dnscrypt%" ./FastGithub/FastGithub.csproj
if errorlevel 1 (
    echo Error: Build macOS ARM64 version with DNSCrypt-Proxy failed
    exit /b 1
)

REM 10. Build version without DNSCrypt-Proxy for macOS ARM64
echo.
echo ==================================================
echo Building macOS ARM64 version without DNSCrypt-Proxy...
echo ==================================================
dotnet publish -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true --self-contained -r osx-arm64 -o "%osxarm64_without_dnscrypt%" ./FastGithub/FastGithub.csproj
if errorlevel 1 (
    echo Error: Build macOS ARM64 version without DNSCrypt-Proxy failed
    exit /b 1
)

REM 11. Remove DNSCrypt-Proxy related files from all versions without DNSCrypt-Proxy
echo.
echo ==================================================
echo Cleaning DNSCrypt-Proxy related files from versions without DNSCrypt-Proxy...
echo ==================================================
if exist "%winx64_without_dnscrypt%\dnscrypt-proxy" (
    echo Deleting %winx64_without_dnscrypt%\dnscrypt-proxy directory
    rd /S /Q "%winx64_without_dnscrypt%\dnscrypt-proxy"
)
if exist "%linuxx64_without_dnscrypt%\dnscrypt-proxy" (
    echo Deleting %linuxx64_without_dnscrypt%\dnscrypt-proxy directory
    rd /S /Q "%linuxx64_without_dnscrypt%\dnscrypt-proxy"
)
if exist "%linuxarm64_without_dnscrypt%\dnscrypt-proxy" (
    echo Deleting %linuxarm64_without_dnscrypt%\dnscrypt-proxy directory
    rd /S /Q "%linuxarm64_without_dnscrypt%\dnscrypt-proxy"
)
if exist "%osxx64_without_dnscrypt%\dnscrypt-proxy" (
    echo Deleting %osxx64_without_dnscrypt%\dnscrypt-proxy directory
    rd /S /Q "%osxx64_without_dnscrypt%\dnscrypt-proxy"
)
if exist "%osxarm64_without_dnscrypt%\dnscrypt-proxy" (
    echo Deleting %osxarm64_without_dnscrypt%\dnscrypt-proxy directory
    rd /S /Q "%osxarm64_without_dnscrypt%\dnscrypt-proxy"
)

REM 12. Build UI project to all Windows version directories
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

REM 13. Show build results
echo.
echo ==================================================
echo Build completed!
echo ==================================================
echo.
echo Output directories:
echo   Windows x64 with DNSCrypt-Proxy: %winx64_with_dnscrypt%
echo   Windows x64 without DNSCrypt-Proxy: %winx64_without_dnscrypt%
echo   Linux x64 with DNSCrypt-Proxy: %linuxx64_with_dnscrypt%
echo   Linux x64 without DNSCrypt-Proxy: %linuxx64_without_dnscrypt%
echo   Linux ARM64 with DNSCrypt-Proxy: %linuxarm64_with_dnscrypt%
echo   Linux ARM64 without DNSCrypt-Proxy: %linuxarm64_without_dnscrypt%
echo   macOS x64 with DNSCrypt-Proxy: %osxx64_with_dnscrypt%
echo   macOS x64 without DNSCrypt-Proxy: %osxx64_without_dnscrypt%
echo   macOS ARM64 with DNSCrypt-Proxy: %osxarm64_with_dnscrypt%
echo   macOS ARM64 without DNSCrypt-Proxy: %osxarm64_without_dnscrypt%
echo.
echo All versions have been built successfully. Choose the appropriate version for your platform:
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