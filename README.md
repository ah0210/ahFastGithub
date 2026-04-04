## 1 写在前面
* **fastgithub不具备"翻墙"功能,也不是翻墙工具** 
* **只提供对 github.com 等技术网址的DNS修正功能** 
* **fastgithub 最高支持Windows 11** 
* **fastgithub不是加速工具** 

## 2 版本信息
FastGithub 2.4.0

### 安全增强 (2.4.0)
- **代理访问限制**：代理服务仅允许本地回环地址（127.0.0.1 / ::1）连接，防止被滥用为开放代理
- **CA 私钥加密**：CA 私钥使用 AES 加密存储，并设置严格的文件访问权限
- **证书安装范围**：CA 证书仅安装到当前用户，不影响系统全局或其他用户
- **Git 信任限制**：仅为 GitHub 域名（github.com、gist.github.com、api.github.com）禁用 SSL 验证
- **SSRF 防护**：隧道代理拒绝连接到内网地址（10.x、172.16-31.x、192.168.x 等）
- **请求体限制**：设置合理的请求体大小上限（100MB）和最小数据速率，防止慢速攻击
- **密钥升级**：RSA 密钥从 2048 位升级到 4096 位
- **证书有效期**：CA 证书有效期从 10 年缩短到 2 年

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
* `./fastgithub`
* 设置系统自动代理为`http://127.0.0.1:38457`，或手动代理http/https为`127.0.0.1:38457`
  
### 7.4 Linux-x64服务
* `./fastgithub start` // 以systemd服务安装并启动
* `./fastgithub stop` // 以systemd服务卸载并删除
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

FastGithub 启动时会自动为 GitHub 相关域名（`github.com`、`gist.github.com`、`api.github.com`）禁用 SSL 证书验证，不会影响其他 Git 仓库。

如果仍有 `SSL certificate problem` 提示，可选择以下一种方式：

**方式一：仅对 GitHub 禁用 SSL 验证（推荐）**
```bash
git config --global http.https://github.com/.sslverify false
git config --global http.https://gist.github.com/.sslverify false
```

**方式二：指定 FastGithub 的 CA 证书（更安全）**
```bash
git config --global http.https://github.com/.sslCAInfo <path-to-fastgithub>/cacert/fastgithub.cer
```

### 9.2 Firefox
firefox提示`连接有潜在的安全问题`</br>
设置->隐私与安全->证书->查看证书->证书颁发机构，导入cacert/fastgithub.cer，勾选"信任由此证书颁发机构来标识网站"

### 9.3 其他浏览器
其他浏览器也需要导入cacert/fastgithub.cer证书并设置信任。

## 10 常见问题

### 10.1 浏览器提示"您的连接不是私密连接"（ERR_CERT_AUTHORITY_INVALID）

**原因**：FastGithub 的 CA 证书未被系统信任。

**解决方法**：

**Windows 11 手动安装证书**：
1. 打开 FastGithub 目录下的 `cacert` 文件夹
2. 双击 `fastgithub.cer`
3. 点击 **"安装证书..."**
4. 存储位置选择 **"当前用户"**，点下一步
5. 选择 **"将所有的证书都放入下列存储"**，点 **"浏览"**
6. 选择 **"受信任的根证书颁发机构"**，点确定
7. 点 **下一步** → **完成**
8. 弹出安全警告点 **"是"**

**Windows 11 删除旧证书**（如之前安装过旧版）：
1. `Win + R` 输入 `certmgr.msc` 回车
2. 展开 **"受信任的根证书颁发机构"** → **"证书"**
3. 找到颁发给 `FastGithub` 的证书，右键 **删除**
4. 重启 FastGithub，它会自动重新安装

### 10.2 升级 2.4.0 后证书不生效

**原因**：2.4.0 版本将 CA 私钥从明文格式改为 AES 加密存储。旧版 `cacert/fastgithub.key` 是明文 PEM 格式，新版本会自动检测并兼容迁移。

**解决方法**：
1. 删除 `cacert` 文件夹（包含旧的 `fastgithub.cer` 和 `fastgithub.key`）
2. 重新启动 FastGithub，会自动生成新的加密密钥和证书
3. 按 10.1 步骤重新安装新证书到系统信任存储

### 10.3 Git 操作提示 SSL/TLS 连接失败

**原因**：Git 的 `sslCAInfo` 配置指向了 FastGithub 的自签名 CA，导致 Git 不再信任 GitHub 的真实 CA（如 DigiCert）。

**解决方法**：
```bash
# 清除错误的 sslCAInfo 配置
git config --global --unset http.https://github.com/.sslCAInfo
git config --global --unset http.https://gist.github.com/.sslCAInfo
git config --global --unset http.https://api.github.com/.sslCAInfo

# 改为禁用验证（仅针对 GitHub 域名）
git config --global http.https://github.com/.sslverify false
git config --global http.https://gist.github.com/.sslverify false
git config --global http.https://api.github.com/.sslverify false
```

## 11 安全性说明

### 11.1 代理访问限制
FastGithub 的代理服务仅允许来自本地回环地址（127.0.0.1 / ::1）的连接，拒绝来自局域网或其他外部来源的请求，防止被滥用为开放代理。

### 11.2 CA 私钥保护
- CA 私钥使用 AES 加密存储，密钥派生基于机器名和用户名
- 私钥文件设置了严格的访问权限（Windows ACL / Linux chmod 600）
- 请勿将 `cacert/` 目录下的文件分享给他人

### 11.3 证书安装范围
- Windows：CA 证书仅安装到当前用户的信任根存储，不影响其他用户
- Linux：CA 证书仅安装到用户目录（`~/.local/share/ca-certificates/`），无需 root 权限
- macOS：需手动安装并设置信任

### 11.4 Git 配置
FastGithub 仅为 GitHub 域名（`github.com`、`gist.github.com`、`api.github.com`）配置证书信任，不会影响其他 Git 仓库的 SSL 验证。

## 12 合法性说明
《国际联网暂行规定》第六条规定："计算机信息网络直接进行国际联网，必须使用邮电部国家公用电信网提供的国际出入口信道。任何单位和个人不得自行建立或者使用其他信道进行国际联网。"
FastGithub本地代理使用的都是"公用电信网提供的国际出入口信道"，从国外Github服务器到国内用户电脑上FastGithub程序的流量，使用的是正常流量通道，其间未对流量进行任何额外加密（仅有网页原有的TLS加密，区别于VPN的流量加密），而FastGithub获取到网页数据之后发生的整个代理过程完全在国内，不再适用国际互联网相关之规定。
