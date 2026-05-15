@echo off
setlocal enabledelayedexpansion

for /f %%v in ('powershell -NoProfile -Command "(Select-Xml -Path \"Directory.Build.props\" -XPath \"/Project/PropertyGroup/Version\").Node.InnerText"') do set version=%%v
if "%version%"=="" set version=0.0.0

echo.
echo ================================================
echo    FastGithub Unified Publish Script (Windows Only)
echo    Version %version%
echo ================================================
echo.

REM Set output directories
set output=./publish/%version%
set winx64_with_dnscrypt=%output%/fastgithub_win-x64_with_dnscrypt
set winx64_without_dnscrypt=%output%/fastgithub_win-x64_without_dnscrypt

REM Create output directories
echo Creating output directories...
if not exist "%output%" mkdir "%output%"
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

REM 5. Clean up unnecessary files
echo.
echo ==================================================
echo Cleaning unnecessary files...
echo ==================================================
for %%d in ("%winx64_with_dnscrypt%" "%winx64_without_dnscrypt%") do (
    REM UI project binding redirects
    if exist "%%d\*.dll.config" (
        echo Deleting %%d\*.dll.config
        del /Q "%%d\*.dll.config"
    )
    REM Static web assets (not used)
    if exist "%%d\*.staticwebassets.endpoints.json" (
        echo Deleting %%d\*.staticwebassets.endpoints.json
        del /Q "%%d\*.staticwebassets.endpoints.json"
    )
    REM IIS hosting module (not used)
    if exist "%%d\aspnetcorev2_inprocess.dll" (
        echo Deleting %%d\aspnetcorev2_inprocess.dll
        del /Q "%%d\aspnetcorev2_inprocess.dll"
    )
    REM Source code references (copied from project references)
    if exist "%%d\README.md" (
        echo Deleting %%d\README.md
        del /Q "%%d\README.md"
    )
    if exist "%%d\LICENSE" (
        echo Deleting %%d\LICENSE
        del /Q "%%d\LICENSE"
    )
    REM UI project deps and runtime config (not needed for end users)
    if exist "%%d\FastGithub.UI.deps.json" (
        echo Deleting %%d\FastGithub.UI.deps.json
        del /Q "%%d\FastGithub.UI.deps.json"
    )
    if exist "%%d\FastGithub.UI.runtimeconfig.json" (
        echo Deleting %%d\FastGithub.UI.runtimeconfig.json
        del /Q "%%d\FastGithub.UI.runtimeconfig.json"
    )
    REM PDB debug symbols
    if exist "%%d\*.pdb" (
        echo Deleting %%d\*.pdb
        del /Q "%%d\*.pdb"
    )
    REM XML documentation files
    if exist "%%d\*.xml" (
        echo Deleting %%d\*.xml
        del /Q "%%d\*.xml"
    )
)

REM 6. Show build results
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
echo Creating versioned zip packages...
powershell -Command "Compress-Archive -Path '%winx64_with_dnscrypt%' -DestinationPath '%output%\fastgithub_win-x64_with_dnscrypt_v%version%.zip' -Force"
powershell -Command "Compress-Archive -Path '%winx64_without_dnscrypt%' -DestinationPath '%output%\fastgithub_win-x64_without_dnscrypt_v%version%.zip' -Force"
echo Zip packages:
echo   %output%\fastgithub_win-x64_with_dnscrypt_v%version%.zip
echo   %output%\fastgithub_win-x64_without_dnscrypt_v%version%.zip
echo.
pause
