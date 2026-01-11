@echo off
echo 验证配置文件格式...

echo.
echo 检查主配置文件...
powershell -Command "try { Get-Content '.\publish\fastgithub_win-x64_without_dnscrypt\appsettings.json' -Raw | ConvertFrom-Json; Write-Host '主配置文件格式正确' -ForegroundColor Green } catch { Write-Host '主配置文件格式错误: ' $_.Exception.Message -ForegroundColor Red }"

echo.
echo 检查Google配置文件...
powershell -Command "try { Get-Content '.\publish\fastgithub_win-x64_without_dnscrypt\appsettings\appsettings.google.json' -Raw | ConvertFrom-Json; Write-Host 'Google配置文件格式正确' -ForegroundColor Green } catch { Write-Host 'Google配置文件格式错误: ' $_.Exception.Message -ForegroundColor Red }"

echo.
echo 检查GitHub配置文件...
powershell -Command "try { Get-Content '.\publish\fastgithub_win-x64_without_dnscrypt\appsettings\appsettings.github.json' -Raw | ConvertFrom-Json; Write-Host 'GitHub配置文件格式正确' -ForegroundColor Green } catch { Write-Host 'GitHub配置文件格式错误: ' $_.Exception.Message -ForegroundColor Red }"

echo.
echo 配置清理完成！
echo - 已清理主配置文件中的无用示例配置
echo - 已更新google.json为google.com域名配置
echo - 保留了核心DNS服务器和GitHub相关配置