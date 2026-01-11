

## 构建与发布

### 更新项目依赖
```bash
dotnet-outdated FastGithub.sln -u
```
* 建议定期运行 dotnet-outdated 工具检查依赖更新，以保持项目依赖的安全性和性能。

### 构建项目
```bash
dotnet build FastGithub.sln
```

### 发布脚本

- 包含DNSCrypt-Proxy版本 ：适合注重DNS隐私和安全性的用户
- 不包含DNSCrypt-Proxy版本 ：体积更小，启动更快，适合更关注核心功能和性能的用户

**版本 2.3.0 新增功能**：
- 按需测速：只对用户实际访问的域名进行DNS解析和IP测速
- 智能缓存：测速结果缓存30分钟，有缓存的域名可立即返回最优IP
- 资源优化：减少不必要的网络探测和计算资源消耗

```bash
# 发布默认版本（包含DNSCrypt-Proxy）
./publish.cmd

# 发布两个版本 只 win-x86（/publish 目录下，包含和不包含DNSCrypt-Proxy）
./publish-without-dnscrypt.cmd
```

### 发布执行
执行了项目根目录下的发布脚本，成功为以下平台生成了发布文件：

- Windows x64 ( fastgithub_win-x64 )
- Linux x64 ( fastgithub_linux-x64 )
- Linux ARM64 ( fastgithub_linux-arm64 )
- macOS x64 ( fastgithub_osx-x64 )
- macOS ARM64 ( fastgithub_osx-arm64 )

### 发布文件内容
每个平台的发布目录都包含：

- 主程序文件 ( fastgithub 或 fastgithub.exe )
- UI界面程序 ( FastGithub.UI.exe - 仅Windows平台)
- 配置文件目录 ( appsettings/ )
- 许可证文件 ( LICENSE )
- 文档文件 ( README.md , README.html )

### 两个版本区别

1. **包含DNSCrypt-Proxy的版本**：
   - 目录：`fastgithub_win-x64_with_dnscrypt`
   - 包含完整的 `dnscrypt-proxy` 目录和相关文件
   - 支持使用 DNSCrypt-Proxy 进行安全加密的 DNS 解析
   - 适合注重 DNS 隐私和安全性的用户

2. **不包含DNSCrypt-Proxy的版本**：
   - 目录：`fastgithub_win-x64_without_dnscrypt`
   - 已移除 `dnscrypt-proxy` 目录和相关文件
   - 使用配置文件中的 `FallbackDns` 列表进行 DNS 解析
   - 体积更小，启动更快
   - 适合更关注核心功能和性能的用户

### 注意事项
- 所有发布文件都已包含必要的依赖，无需额外安装.NET运行时
- 首次使用时可能需要配置系统代理为 http://127.0.0.1:38457
- Git操作可能需要关闭证书验证： `git config --global http.sslverify false`

## 1 写在前面
* **fastgithub不具备“翻墙”功能,也没有相关的计划**
* **fastgithub不支持Windows7等已被发行方停止支持的操作系统，并且也不会主动提供支持**
* **fastgithub不能为您的游戏加速**
* **fastgithub没有主动在github之外的任何渠道发布**
  
### 2 部署方式
#### 2.1 windows-x64桌面
* 双击运行FastGithub.UI.exe 

#### 2.2 windows-x64服务 
* `fastgithub.exe start` // 以windows服务安装并启动
* `fastgithub.exe stop` // 以windows服务卸载并删除

#### 2.3 linux-x64终端
* `sudo ./fastgithub`
* 设置系统自动代理为`http://127.0.0.1:38457`，或手动代理http/https为`127.0.0.1:38457`
  
#### 2.4 linux-x64服务
* `sudo ./fastgithub start` // 以systemd服务安装并启动
* `sudo ./fastgithub stop` // 以systemd服务卸载并删除
* 设置系统自动代理为`http://127.0.0.1:38457`，或手动代理http/https为`127.0.0.1:38457`

#### 2.5 macOS-x64
* 双击运行fastgithub
* 安装cacert/fastgithub.cer并设置信任
* 设置系统自动代理为`http://127.0.0.1:38457`，或手动代理http/https为`127.0.0.1:38457`
* [具体配置详情](https://github.com/creazyboyone/FastGithub/blob/master/MacOSXConfig.md)

#### 2.6 docker-compose一键部署
* 准备好docker 18.09, docker-compose.
* 在源码目录下，有一个docker-compose.yaml 文件，专用于在实际项目中，临时使用github.com源码，而做的demo配置。
* 根据自己的需要更新docker-compose.yaml中的sample和build镜像即可完成拉github.com源码加速，并基于源码做后续的操作。
 
### 3 软件功能 
* 提供域名的纯净IP解析；
* 提供IP测速并选择最快的IP；
* 提供域名的tls连接自定义配置；
* google的CDN资源替换，解决大量国外网站无法加载js和css的问题；
* **新增按需测速功能**：只对用户实际访问的域名进行DNS解析和IP测速，提高响应速度；
* **新增智能缓存机制**：测速结果缓存30分钟，有缓存的域名可立即返回最优IP；
* **优化资源使用**：减少不必要的网络探测和计算资源消耗；
  
### 4 证书验证
#### 4.1 git
git操作提示`SSL certificate problem`</br>
需要关闭git的证书验证：`git config --global http.sslverify false`

#### 4.2 firefox
firefox提示`连接有潜在的安全问题`</br>
设置->隐私与安全->证书->查看证书->证书颁发机构，导入cacert/fastgithub.cer，勾选“信任由此证书颁发机构来标识网站”
  

### 5 安全性说明
FastGithub为每台不同的主机生成自颁发CA证书，保存在cacert文件夹下。客户端设备需要安装和无条件信任自颁发的CA证书，请不要将证书私钥泄露给他人，以免造成损失。

### 6 合法性说明
《国际联网暂行规定》第六条规定：“计算机信息网络直接进行国际联网，必须使用邮电部国家公用电信网提供的国际出入口信道。任何单位和个人不得自行建立或者使用其他信道进行国际联网。”
FastGithub本地代理使用的都是“公用电信网提供的国际出入口信道”，从国外Github服务器到国内用户电脑上FastGithub程序的流量，使用的是正常流量通道，其间未对流量进行任何额外加密（仅有网页原有的TLS加密，区别于VPN的流量加密），而FastGithub获取到网页数据之后发生的整个代理过程完全在国内，不再适用国际互联网相关之规定。