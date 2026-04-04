# FastGithub 2.4.0 发布说明

## 安全增强

### 代理访问限制
- 代理服务仅允许本地回环地址（127.0.0.1 / ::1）连接
- 拒绝来自局域网或其他外部来源的请求，防止被滥用为开放代理
- 非本地连接返回 403 Forbidden

### CA 私钥加密保护
- CA 私钥使用 AES 加密存储，密钥派生基于机器名和用户名
- 私钥文件设置严格的访问权限（Windows ACL / Linux chmod 600）
- 请勿将 `cacert/` 目录下的文件分享给他人

### 证书安装范围缩小
- Windows：CA 证书仅安装到当前用户（CurrentUser\Root），不再安装到系统级（LocalMachine\Root）
- Linux：CA 证书仅安装到用户目录（`~/.local/share/ca-certificates/`），无需 root 权限
- macOS：需手动安装并设置信任（无变化）

### Git SSL 信任限制
- 仅为 GitHub 域名（github.com、gist.github.com、api.github.com）禁用 SSL 验证
- 不再全局禁用 `http.sslverify`，不影响其他 Git 仓库

### SSRF 防护
- 隧道代理拒绝连接到内网地址（10.x、172.16-31.x、192.168.x、127.x、169.254.x）
- 防止代理被用于访问内网服务

### 请求体限制
- 设置请求体大小上限为 100MB
- 设置最小数据速率（100 bytes/s，5 秒宽限期），防止慢速攻击（Slowloris）

### 密码学升级
- RSA 密钥从 2048 位升级到 4096 位
- CA 证书有效期从 10 年缩短到 2 年

## 兼容性
- 保持与原有功能的完全兼容
- 不影响现有的配置和使用方式
- 无缝升级

## 版本号
- 从 2.3.0 升级至 2.4.0
