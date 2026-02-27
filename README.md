## 1 写在前面
* **fastgithub不具备"翻墙"功能,也不是翻墙工具** 
* **只提供对 github.com 等技术网址的DNS修正功能** 
* **fastgithub 最高支持Windows 11** 
* **fastgithub不是加速工具** 

## 2 版本信息
FastGithub 2.3.0

### 新增功能
- **按需测速**：只对用户实际访问的域名进行DNS解析和IP测速，提高响应速度 
- **智能缓存**：测速结果缓存30分钟，有缓存的域名可立即返回最优IP 
- **资源优化**：减少不必要的网络探测和计算资源消耗 

## 3 支持平台
- Windows x64  >=win11 
- Linux x64 
- Linux ARM64 
- macOS x64 
- macOS ARM64 

## 4 版本类型
提供两种版本以满足不同用户需求：

### 4.1 包含DNSCrypt-Proxy版本
- DNSCrypt-Proxy项目地址：`https://github.com/DNSCrypt/dnscrypt-proxy`
- 请查看开源项目自行判断是否使用 
- 包含完整的 `dnscrypt-proxy` 目录和相关文件 
- 支持使用 DNSCrypt-Proxy 进行安全加密的 DNS 解析 

### 4.2 不包含DNSCrypt-Proxy版本
- 体积更小，启动更快 
- 适合更关注核心功能和性能的用户 
- 使用配置文件中的 `FallbackDns` 列表进行 DNS 解析 

## 5 源码/升级与技术支持
`https://www.17you.com`

## 6 发布脚本

项目提供了一系列自动化脚本，用于构建和打包不同平台的发行版本。版本号会自动从 `Directory.Build.props` 中读取。

### 6.1 Windows 平台
运行以下脚本构建 Windows x64 版本（包含和不包含 DNSCrypt-Proxy）：
```bash
./publish-windows-only.cmd
```

### 6.2 Linux 平台
运行以下脚本构建 Linux (x64 & ARM64) 版本（包含和不包含 DNSCrypt-Proxy）：
```bash
./publish-linux.cmd
```

### 6.3 全平台构建
运行以下脚本构建所有平台（Windows, Linux, macOS）的版本：
```bash
./publish-unified.cmd
```

### 6.4 打包产物说明
所有脚本执行完成后，产物将存放在 `publish/{version}` 目录下：
- **解压版目录**：位于 `publish/{version}/fastgithub_{platform}_{type}`
- **压缩包产物**：位于 `publish/{version}/fastgithub_{platform}_{type}_v{version}.zip`

## 7 部署方式

### 7.1 Windows-x64桌面
* 双击运行FastGithub.UI.exe 

### 7.2 Windows-x64服务 
* `fastgithub.exe start` // 以windows服务安装并启动
* `fastgithub.exe stop` // 以windows服务卸载并删除

### 7.3 Linux-x64终端
* `sudo ./fastgithub`
* 设置系统自动代理为`http://127.0.0.1:38457`，或手动代理http/https为`127.0.0.1:38457`
  
### 7.4 Linux-x64服务
* `sudo ./fastgithub start` // 以systemd服务安装并启动
* `sudo ./fastgithub stop` // 以systemd服务卸载并删除
* 设置系统自动代理为`http://127.0.0.1:38457`，或手动代理http/https为`127.0.0.1:38457`

### 7.5 macOS-x64
* 双击运行fastgithub
* 安装cacert/fastgithub.cer并设置信任
* 设置系统自动代理为`http://127.0.0.1:38457`，或手动代理http/https为`127.0.0.1:38457`
* [具体配置详情](https://github.com/creazyboyone/FastGithub/blob/master/MacOSXConfig.md)

### 7.6 Docker-Compose一键部署
* 准备好docker 18.09, docker-compose.
* 在源码目录下，有一个docker-compose.yaml 文件，专用于在实际项目中，临时使用github.com源码，而做的demo配置。
* 根据自己的需要更新docker-compose.yaml中的sample和build镜像即可完成拉github.com源码加速，并基于源码做后续的操作。

## 8 软件功能 
* 提供域名的纯净IP解析；
* 提供IP测速并选择最快的IP；
* 提供域名的tls连接自定义配置；
* google的CDN资源替换，解决大量国外网站无法加载js和css的问题；
* **新增按需测速功能**：只对用户实际访问的域名进行DNS解析和IP测速，提高响应速度；
* **新增智能缓存机制**：测速结果缓存30分钟，有缓存的域名可立即返回最优IP；
* **优化资源使用**：减少不必要的网络探测和计算资源消耗；

## 9 证书验证
### 9.1 Git
git操作提示`SSL certificate problem`</br>
需要关闭git的证书验证：`git config --global http.sslverify false`

### 9.2 Firefox
firefox提示`连接有潜在的安全问题`</br>
设置->隐私与安全->证书->查看证书->证书颁发机构，导入cacert/fastgithub.cer，勾选"信任由此证书颁发机构来标识网站"

### 9.3 其他浏览器
其他浏览器也需要导入cacert/fastgithub.cer证书并设置信任。

## 10 安全性说明
FastGithub为每台不同的主机生成自颁发CA证书，保存在cacert文件夹下。客户端设备需要安装和无条件信任自颁发的CA证书，请不要将证书私钥泄露给他人，以免造成损失。

## 11 合法性说明
《国际联网暂行规定》第六条规定："计算机信息网络直接进行国际联网，必须使用邮电部国家公用电信网提供的国际出入口信道。任何单位和个人不得自行建立或者使用其他信道进行国际联网。"
FastGithub本地代理使用的都是"公用电信网提供的国际出入口信道"，从国外Github服务器到国内用户电脑上FastGithub程序的流量，使用的是正常流量通道，其间未对流量进行任何额外加密（仅有网页原有的TLS加密，区别于VPN的流量加密），而FastGithub获取到网页数据之后发生的整个代理过程完全在国内，不再适用国际互联网相关之规定。
