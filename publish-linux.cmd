@echo off
setlocal enabledelayedexpansion

echo.
echo ================================================
echo    FastGithub Linux Publish Script
echo    Version 1.0.0
echo ================================================
echo.

REM Set output directories
set output=./publish
set linuxx64_with_dnscrypt=%output%/fastgithub_linux-x64_with_dnscrypt
set linuxx64_without_dnscrypt=%output%/fastgithub_linux-x64_without_dnscrypt
set linuxarm64_with_dnscrypt=%output%/fastgithub_linux-arm64_with_dnscrypt
set linuxarm64_without_dnscrypt=%output%/fastgithub_linux-arm64_without_dnscrypt

REM Clean old output directory
echo Cleaning old output directory...
if exist "%linuxx64_with_dnscrypt%" (
    echo Deleting %linuxx64_with_dnscrypt% directory
    rd /S /Q "%linuxx64_with_dnscrypt%"
)
if exist "%linuxx64_without_dnscrypt%" (
    echo Deleting %linuxx64_without_dnscrypt% directory
    rd /S /Q "%linuxx64_without_dnscrypt%"
)
if exist "%linuxarm64_with_dnscrypt%" (
    echo Deleting %linuxarm64_with_dnscrypt% directory
    rd /S /Q "%linuxarm64_with_dnscrypt%"
)
if exist "%linuxarm64_without_dnscrypt%" (
    echo Deleting %linuxarm64_without_dnscrypt% directory
    rd /S /Q "%linuxarm64_without_dnscrypt%"
)

REM Create output directories
echo Creating output directories...
mkdir "%linuxx64_with_dnscrypt%"
mkdir "%linuxx64_without_dnscrypt%"
mkdir "%linuxarm64_with_dnscrypt%"
mkdir "%linuxarm64_without_dnscrypt%"

REM 1. Build version with DNSCrypt-Proxy for Linux x64
echo.
echo ==================================================
echo Building Linux x64 version with DNSCrypt-Proxy...
echo ==================================================
dotnet publish -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true --self-contained -r linux-x64 -o "%linuxx64_with_dnscrypt%" ./FastGithub/FastGithub.csproj
if errorlevel 1 (
    echo Error: Build Linux x64 version with DNSCrypt-Proxy failed
    exit /b 1
)

REM 2. Build version without DNSCrypt-Proxy for Linux x64
echo.
echo ==================================================
echo Building Linux x64 version without DNSCrypt-Proxy...
echo ==================================================
dotnet publish -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true --self-contained -r linux-x64 -o "%linuxx64_without_dnscrypt%" ./FastGithub/FastGithub.csproj
if errorlevel 1 (
    echo Error: Build Linux x64 version without DNSCrypt-Proxy failed
    exit /b 1
)

REM 3. Build version with DNSCrypt-Proxy for Linux ARM64
echo.
echo ==================================================
echo Building Linux ARM64 version with DNSCrypt-Proxy...
echo ==================================================
dotnet publish -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true --self-contained -r linux-arm64 -o "%linuxarm64_with_dnscrypt%" ./FastGithub/FastGithub.csproj
if errorlevel 1 (
    echo Error: Build Linux ARM64 version with DNSCrypt-Proxy failed
    exit /b 1
)

REM 4. Build version without DNSCrypt-Proxy for Linux ARM64
echo.
echo ==================================================
echo Building Linux ARM64 version without DNSCrypt-Proxy...
echo ==================================================
dotnet publish -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true --self-contained -r linux-arm64 -o "%linuxarm64_without_dnscrypt%" ./FastGithub/FastGithub.csproj
if errorlevel 1 (
    echo Error: Build Linux ARM64 version without DNSCrypt-Proxy failed
    exit /b 1
)

REM 5. Remove DNSCrypt-Proxy related files from versions without DNSCrypt-Proxy
echo.
echo ==================================================
echo Cleaning DNSCrypt-Proxy related files from versions without DNSCrypt-Proxy...
echo ==================================================
if exist "%linuxx64_without_dnscrypt%\dnscrypt-proxy" (
    echo Deleting %linuxx64_without_dnscrypt%\dnscrypt-proxy directory
    rd /S /Q "%linuxx64_without_dnscrypt%\dnscrypt-proxy"
)
if exist "%linuxarm64_without_dnscrypt%\dnscrypt-proxy" (
    echo Deleting %linuxarm64_without_dnscrypt%\dnscrypt-proxy directory
    rd /S /Q "%linuxarm64_without_dnscrypt%\dnscrypt-proxy"
)

REM 6. Copy dnscrypt-proxy files to versions with DNSCrypt-Proxy
echo.
echo ==================================================
echo Copying DNSCrypt-Proxy files to versions with DNSCrypt-Proxy...
echo ==================================================
if exist "@dnscrypt-proxy\linux-x86_64\dnscrypt-proxy" (
    if not exist "%linuxx64_with_dnscrypt%\dnscrypt-proxy" mkdir "%linuxx64_with_dnscrypt%\dnscrypt-proxy"
    copy /Y "@dnscrypt-proxy\linux-x86_64\dnscrypt-proxy" "%linuxx64_with_dnscrypt%\dnscrypt-proxy\"
    copy /Y "@dnscrypt-proxy\dnscrypt-proxy.toml" "%linuxx64_with_dnscrypt%\dnscrypt-proxy\"
    echo Copied DNSCrypt-Proxy files to Linux x64 with DNSCrypt-Proxy
)
if exist "@dnscrypt-proxy\linux-arm64\dnscrypt-proxy" (
    if not exist "%linuxarm64_with_dnscrypt%\dnscrypt-proxy" mkdir "%linuxarm64_with_dnscrypt%\dnscrypt-proxy"
    copy /Y "@dnscrypt-proxy\linux-arm64\dnscrypt-proxy" "%linuxarm64_with_dnscrypt%\dnscrypt-proxy\"
    copy /Y "@dnscrypt-proxy\dnscrypt-proxy.toml" "%linuxarm64_with_dnscrypt%\dnscrypt-proxy\"
    echo Copied DNSCrypt-Proxy files to Linux ARM64 with DNSCrypt-Proxy
)

REM 7. Remove duplicate LICENSE files from dnscrypt-proxy directories
echo.
echo ==================================================
echo Removing duplicate LICENSE files from dnscrypt-proxy directories...
echo ==================================================
if exist "%linuxx64_with_dnscrypt%\dnscrypt-proxy\LICENSE" (
    del "%linuxx64_with_dnscrypt%\dnscrypt-proxy\LICENSE"
    echo Removed duplicate LICENSE from Linux x64 with DNSCrypt-Proxy
)
if exist "%linuxarm64_with_dnscrypt%\dnscrypt-proxy\LICENSE" (
    del "%linuxarm64_with_dnscrypt%\dnscrypt-proxy\LICENSE"
    echo Removed duplicate LICENSE from Linux ARM64 with DNSCrypt-Proxy
)

REM 8. Update dnscrypt-proxy.toml to enable IPv6
echo.
echo ==================================================
echo Updating dnscrypt-proxy.toml to enable IPv6...
echo ==================================================
if exist "%linuxx64_with_dnscrypt%\dnscrypt-proxy\dnscrypt-proxy.toml" (
    powershell -Command "(Get-Content '%linuxx64_with_dnscrypt%\dnscrypt-proxy\dnscrypt-proxy.toml' -replace 'ipv6_servers = false', 'ipv6_servers = true' | Set-Content '%linuxx64_with_dnscrypt%\dnscrypt-proxy\dnscrypt-proxy.toml'"
    echo Updated IPv6 setting in Linux x64 with DNSCrypt-Proxy
)
if exist "%linuxarm64_with_dnscrypt%\dnscrypt-proxy\dnscrypt-proxy.toml" (
    powershell -Command "(Get-Content '%linuxarm64_with_dnscrypt%\dnscrypt-proxy\dnscrypt-proxy.toml' -replace 'ipv6_servers = false', 'ipv6_servers = true' | Set-Content '%linuxarm64_with_dnscrypt%\dnscrypt-proxy\dnscrypt-proxy.toml'"
    echo Updated IPv6 setting in Linux ARM64 with DNSCrypt-Proxy
)

REM 9. Show build results
echo.
echo ==================================================
echo Build completed!
echo ==================================================
echo.
echo Output directories:
echo   Linux x64 with DNSCrypt-Proxy: %linuxx64_with_dnscrypt%
echo   Linux x64 without DNSCrypt-Proxy: %linuxx64_without_dnscrypt%
echo   Linux ARM64 with DNSCrypt-Proxy: %linuxarm64_with_dnscrypt%
echo   Linux ARM64 without DNSCrypt-Proxy: %linuxarm64_without_dnscrypt%
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
echo To use on Ubuntu/Linux:
echo   1. Extract the directory to your desired location
echo   2. Run: chmod +x fastgithub
echo   3. Run: ./fastgithub
echo.
pause
