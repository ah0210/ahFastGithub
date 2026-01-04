@echo off
setlocal enabledelayedexpansion

REM 设置输出目录
set output=./publish
set winx64_with_dnscrypt=%output%/fastgithub_win-x64_with_dnscrypt
set winx64_without_dnscrypt=%output%/fastgithub_win-x64_without_dnscrypt

REM 清理旧的输出目录
if exist "%output%" rd /S /Q "%output%"

REM 创建输出目录
mkdir "%winx64_with_dnscrypt%"
mkdir "%winx64_without_dnscrypt%"

REM 1. 构建包含DNSCrypt-Proxy的版本（默认配置）
echo Building win-x64 with DNSCrypt-Proxy...
dotnet publish -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true --self-contained -r win-x64 -o "%winx64_with_dnscrypt%" ./FastGithub/FastGithub.csproj

REM 2. 构建不包含DNSCrypt-Proxy的版本
REM    首先构建默认版本，然后删除dnscrypt-proxy相关文件
echo Building win-x64 without DNSCrypt-Proxy...
dotnet publish -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true --self-contained -r win-x64 -o "%winx64_without_dnscrypt%" ./FastGithub/FastGithub.csproj

REM 删除不包含DNSCrypt-Proxy版本中的相关文件
echo Removing DNSCrypt-Proxy files from without-dnscrypt version...
if exist "%winx64_without_dnscrypt%/dnscrypt-proxy" rd /S /Q "%winx64_without_dnscrypt%/dnscrypt-proxy"

REM 3. 构建UI项目
echo Building UI project...
dotnet publish -c Release -o "%winx64_with_dnscrypt%" ./FastGithub.UI/FastGithub.UI.csproj
dotnet publish -c Release -o "%winx64_without_dnscrypt%" ./FastGithub.UI/FastGithub.UI.csproj

echo Build completed!
 echo Output directories:
 echo   With DNSCrypt-Proxy: %winx64_with_dnscrypt%
 echo   Without DNSCrypt-Proxy: %winx64_without_dnscrypt%
