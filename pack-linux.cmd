@echo off
setlocal enabledelayedexpansion

echo.
echo ================================================
echo    FastGithub Linux Pack Script
echo    Version 1.0.0
echo ================================================
echo.

cd ./publish

REM linux-x64 with dnscrypt
echo.
echo Packing Linux x64 with DNSCrypt-Proxy...
if exist "fastgithub_linux-x64_with_dnscrypt" (
    if exist "fastgithub_linux-x64_with_dnscrypt.zip" del "fastgithub_linux-x64_with_dnscrypt.zip"
    powershell -Command "Compress-Archive -Path 'fastgithub_linux-x64_with_dnscrypt' -DestinationPath 'fastgithub_linux-x64_with_dnscrypt.zip'"
    echo Created: fastgithub_linux-x64_with_dnscrypt.zip
)

REM linux-x64 without dnscrypt
echo.
echo Packing Linux x64 without DNSCrypt-Proxy...
if exist "fastgithub_linux-x64_without_dnscrypt" (
    if exist "fastgithub_linux-x64_without_dnscrypt.zip" del "fastgithub_linux-x64_without_dnscrypt.zip"
    powershell -Command "Compress-Archive -Path 'fastgithub_linux-x64_without_dnscrypt' -DestinationPath 'fastgithub_linux-x64_without_dnscrypt.zip'"
    echo Created: fastgithub_linux-x64_without_dnscrypt.zip
)

REM linux-arm64 with dnscrypt
echo.
echo Packing Linux ARM64 with DNSCrypt-Proxy...
if exist "fastgithub_linux-arm64_with_dnscrypt" (
    if exist "fastgithub_linux-arm64_with_dnscrypt.zip" del "fastgithub_linux-arm64_with_dnscrypt.zip"
    powershell -Command "Compress-Archive -Path 'fastgithub_linux-arm64_with_dnscrypt' -DestinationPath 'fastgithub_linux-arm64_with_dnscrypt.zip'"
    echo Created: fastgithub_linux-arm64_with_dnscrypt.zip
)

REM linux-arm64 without dnscrypt
echo.
echo Packing Linux ARM64 without DNSCrypt-Proxy...
if exist "fastgithub_linux-arm64_without_dnscrypt" (
    if exist "fastgithub_linux-arm64_without_dnscrypt.zip" del "fastgithub_linux-arm64_without_dnscrypt.zip"
    powershell -Command "Compress-Archive -Path 'fastgithub_linux-arm64_without_dnscrypt' -DestinationPath 'fastgithub_linux-arm64_without_dnscrypt.zip'"
    echo Created: fastgithub_linux-arm64_without_dnscrypt.zip
)

cd ..

echo.
echo ================================================
echo    Pack completed!
echo ================================================
echo.
echo Created packages:
echo   fastgithub_linux-x64_with_dnscrypt.zip
echo   fastgithub_linux-x64_without_dnscrypt.zip
echo   fastgithub_linux-arm64_with_dnscrypt.zip
echo   fastgithub_linux-arm64_without_dnscrypt.zip
echo.
echo To use on Ubuntu/Linux:
echo   1. Download the appropriate zip file
echo   2. Extract to a directory
echo   3. Run: chmod +x fastgithub
echo   4. Run: ./fastgithub
echo.
pause
