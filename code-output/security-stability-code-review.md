# FastGithub 项目安全、稳定性与代码质量报告

**版本**: 2.4.1
**日期**: 2026-05-15
**范围**: 全项目代码审查（8 个项目，70+ 源文件）

---

## 目录

1. [安全性评估](#1-安全性评估)
2. [稳定性评估](#2-稳定性评估)
3. [代码质量评估](#3-代码质量评估)
4. [优化建议优先级](#4-优化建议优先级)

---

## 1. 安全性评估

### 1.1 🔴 高危问题

#### 1.1.1 CA 私钥加密密钥派生太弱

- **文件**: `FastGithub.HttpServer/Certs/CertService.cs:99-108`
- **风险等级**: 🔴 高危

```csharp
private static byte[] DeriveKey()
{
    var machineName = Encoding.UTF8.GetBytes(Environment.MachineName);
    var userName = Encoding.UTF8.GetBytes(Environment.UserName);
    using var sha256 = SHA256.Create();
    var combined = new byte[machineName.Length + userName.Length];
    Buffer.BlockCopy(machineName, 0, combined, 0, machineName.Length);
    Buffer.BlockCopy(userName, 0, combined, machineName.Length, userName.Length);
    return sha256.ComputeHash(combined);
}
```

**问题**: 密钥仅通过 `SHA256(MachineName + UserName)` 派生：
- 无盐值（salt），同一用户在同一机器上始终产生相同密钥
- 无迭代次数，可被 GPU/ASIC 快速暴力破解
- MachineName 和 UserName 很容易通过社工或本地信息泄露获取

**建议**: 改用 `Rfc2898DeriveBytes`（PBKDF2），添加随机盐值并存储到加密文件头部，迭代次数不低于 600,000。

---

#### 1.1.2 AES 初始化向量 (IV) 固定为零

- **文件**: `FastGithub.HttpServer/Certs/CertService.cs:74`
- **风险等级**: 🔴 高危

```csharp
aes.IV = new byte[16]; // 全零 IV
```

**问题**: CBC 模式的 IV 固定为全零，导致：
- 相同的私钥明文产生完全相同的密文（确定性加密）
- 失去语义安全性，攻击者可推断不同版本之间的密钥变化
- 即使加密后的文件头部写了 IV，也毫无意义

**建议**: 使用随机 IV：
```csharp
aes.GenerateIV(); // 随机生成 IV
// 将 IV 写入输出流头部
ms.Write(aes.IV, 0, aes.IV.Length);
```

---

#### 1.1.3 所有空 catch 块无日志

- **涉及文件**: 多处，包括：
  - `HttpProxyMiddleware.cs:39`
  - `CertService.cs:149`、`CertService.cs:165`
  - `MainWindow.xaml.cs:73` (`LoadAppIcon`)
  - `DnsClient.cs:463`
  - `TunnelMiddleware.cs:92`
- **风险等级**: 🔴 高危（调试/维护角度）

**问题**: 大量 `catch (Exception) { }` 完全静默吞掉异常，导致：
- 线上故障完全无法定位
- 安全问题被隐藏（如证书文件权限限制失败、DNS 解析失败）
- 开发调试需要大量时间排查

**建议**: 至少写 `Debug.WriteLine` 或 `logger.LogWarning`，关键操作需要记录完整异常信息。

---

#### 1.1.4 Git SSL 配置静默失败

- **文件**: `CertService.cs:197-211`
- **风险等级**: 🟡 中危

```csharp
catch (Exception)
{
    // 完全静默
}
```

**问题**: `git config --global` 执行失败时完全无提示，用户可能不知道 SSL 验证未配置，导致 git 操作失败。

**建议**: 至少日志警告，并在 UI 显示提示。

---

### 1.2 🟡 中危问题

#### 1.2.1 进程启动无超时等待

- **文件**: `FastGithub.UI/Program.cs:141`
- **风险等级**: 🟡 中危

```csharp
Process.Start(buildStartInfo)?.WaitForExit(); // 无超时
```

**问题**: 如果 `dotnet build` 卡住（如 NuGet 源不可用、VS 占用文件），UI 进程永远阻塞。且此功能在发布版本中不应该存在（用户不应从 UI 触发构建）。

**建议**: 添加超时参数 `WaitForExit(30000)`，并在发布版本中移除构建功能。

#### 1.2.2 Registry 写入无校验

- **文件**: `FastGithub.UI/Program.cs:70-102`
- **风险等级**: 🟡 中危

```csharp
registryKey.SetValue(name, value, RegistryValueKind.DWord);
```

**问题**: 将浏览器版本字符串解析为整数后直接写入注册表，没有范围校验。极端的 Edge 版本号可能导致溢出。

**建议**: 对 `value` 做范围限制（如 11000~90000）。

#### 1.2.3 纯文本密钥检测可能误判

- **文件**: `CertService.cs:110-127`
- **风险等级**: 🟡 中危

```csharp
private static bool IsPlainTextKey(byte[] data)
{
    if (data.Length < 27) return false;
    return data[0] == (byte)'-' && ...;
}
```

**问题**: 仅通过检查文件是否以 `-----BEGIN RSA` 开头来判断。加密后的数据（尤其是 AES 输出）以极小概率也可能以 `-` 开头。

**建议**: 改用更可靠的标识，如加密文件头部写入特定魔数（magic number）和版本号。

---

### 1.3 🟢 低危问题

#### 1.3.1 SSL 验证跳过域名硬编码

- **文件**: `FastGithub.Http/HttpClientHandler.cs:189-190`
- **风险等级**: 🟢 低危

```csharp
if (domain.EndsWith(".github.com") || domain == "github.com" || ...)
{
    return true; // 完全跳过证书验证
}
```

**问题**: 对 `github.com`、`huggingface.co` 等域名完全跳过 SSL 证书验证。虽然这是该工具的核心设计（MITM 代理），但这些硬编码域名从配置文件中移除后，这里的代码不会同步更新。

**建议**: 将需要跳过验证的域名列表也纳入 `appsettings.json` 配置。

#### 1.3.2 代理仅限 localhost，但无认证

- **文件**: `HttpProxyMiddleware.cs:32`、`TunnelMiddleware.cs:50`
- **风险等级**: 🟢 低危

**问题**: 代理限制仅 localhost 可访问，但如果用户使用 SSH 隧道或端口转发将代理暴露到外部，则可能被滥用。

**建议**: 需在文档中说明安全限制。

---

## 2. 稳定性评估

### 2.1 🔴 高危问题

#### 2.1.1 SemaphoreSlim 清理竞态条件

- **文件**: `FastGithub.DomainResolve/DnsClient.cs:442-467`
- **风险等级**: 🔴 高危

```csharp
private void CleanupSemaphoreSlims(object? state)
{
    if (this.semaphoreSlims.Count > MaxCacheSize)
    {
        var keys = this.semaphoreSlims.Keys.Take(this.semaphoreSlims.Count - MaxCacheSize / 2).ToList();
        foreach (var key in keys)
        {
            if (this.semaphoreSlims.TryRemove(key, out var semaphore))
            {
                semaphore?.Dispose(); // ⚠️ 可能正在被 WaitAsync 持有
            }
        }
    }
}
```

**问题**:
1. `semaphore.Dispose()` 在另一个线程可能正在执行 `semaphore.WaitAsync()` 时调用，抛出 `ObjectDisposedException`
2. 清理策略（移除超过半数的条目）过于激进，可能导致频繁重建
3. 此方法被 Timer 每隔 10 分钟执行，但无重入保护

**建议**:
- 改用 `new SemaphoreSlim(1, 1)` 配合 `SafeSemaphore` 包装类，或使用 `ConcurrentDictionary` 的 `TryRemove` 前检查引用计数
- 降低清理频率或改用惰性清理（GC 友好方案）

---

#### 2.1.2 Socket 可能的双重释放

- **文件**: `FastGithub.HttpServer/TcpMiddlewares/TunnelMiddleware.cs:97-111`
- **风险等级**: 🔴 高危

```csharp
var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
try
{
    await socket.ConnectAsync(endPoint, linkedTokenSource.Token);
    return new NetworkStream(socket, ownsSocket: true); // socket 所有权转移
}
catch (Exception ex)
{
    socket.Dispose(); // ⚠️ 如果连接成功但 NetworkStream 构造函数抛异常（极低概率），这里也会 double dispose
    cancellationToken.ThrowIfCancellationRequested();
    innerExceptions.Add(ex);
}
```

**问题**: 正常路径 `socket` 所有权转移给 `NetworkStream`（`ownsSocket: true`）。异常路径手动 `Dispose`。如果 `ConnectAsync` 部分成功但后续代码异常，可能：
1. `NetworkStream` 构建成功但网络流使用中传入异常路径
2. 实际上问题概率极低，但代码路径不够清晰

**建议**: 重构为 using 模式，统一释放路径。

---

#### 2.1.3 Task.WhenAny 中另一个任务异常被静默

- **文件**: `FastGithub.HttpServer/TcpMiddlewares/TunnelMiddleware.cs:71-73`
- **风险等级**: 🟡 中危

```csharp
var task1 = connection.CopyToAsync(transport.Output, cancellationToken);
var task2 = transport.Input.CopyToAsync(connection, cancellationToken);
await Task.WhenAny(task1, task2);
```

**问题**: `Task.WhenAny` 只等待第一个完成的任务。另一个任务可能因异常失败，但异常被静默吞噬（未观察）。当 `Task` 被 GC 回收时，未观察的异常将触发 `TaskScheduler.UnobservedTaskException`。

**建议**: 添加 `ContinueWith` 或使用 `Task.WhenAll` 配合 `CancellationToken`：

```csharp
var task1 = connection.CopyToAsync(transport.Output, cancellationToken);
var task2 = transport.Input.CopyToAsync(connection, cancellationToken);
_ = task1.ContinueWith(_ => transport.Input.CancelPendingRead(), TaskContinuationOptions.NotOnRanToCompletion);
_ = task2.ContinueWith(_ => connection.Dispose(), TaskContinuationOptions.NotOnRanToCompletion);
```

---

### 2.2 🟡 中危问题

#### 2.2.1 AsyncEnumerable DNS 解析无超时保护

- **文件**: `FastGithub.DomainResolve/DnsClient.cs:88-101`
- **风险等级**: 🟡 中危

```csharp
public async IAsyncEnumerable<IPAddress> ResolveAsync(DnsEndPoint endPoint, bool fastSort, ...)
```

**问题**: `ResolveAsync` 迭代多个 DNS 服务器，但整体无超时。如果所有 DNS 都不可用，调用方可能等待过久（每个 DNS 超时 3s × 18 个 FallbackDns = 54s）。

**建议**: 添加整体超时控制，或限制最大尝试次数。

#### 2.2.2 MemoryCache 空闲时未释放内存

- **文件**: `FastGithub.DomainResolve/DnsClient.cs:65-75`
- **风险等级**: 🟡 中危

**问题**: `MemoryCache` 仅在 `Dispose()` 时释放，长空闲期可能持有大量过期缓存条目占用内存。

**建议**: 使用 `MemoryCacheOptions.ExpirationScanFrequency` 控制过期扫描频率，或在配置中启用 `Compact`。

#### 2.2.3 Timer 回调未等待完成

- **文件**: `FastGithub.DomainResolve/DnsClient.cs:473-474`
- **风险等级**: 🟡 中危

```csharp
public void Dispose()
{
    this.cleanupTimer?.Dispose(); // 不等待正在执行的回调
}
```

**问题**: `Timer.Dispose()` 不保证正在执行的回调已完成。如果 `CleanupSemaphoreSlims` 正在执行中，其内部访问的 `semaphoreSlims` 可能被后续 `Dispose()` 中的 `semaphoreSlims.Clear()` 影响。

**建议**: 使用 `Timer.Dispose(WaitHandle)` 或使用 `ManualResetEvent` 确保回调完成。

---

### 2.3 🟢 低危问题

#### 2.3.1 缺少 CancellationToken 传播

- **涉及文件**: `HttpProxyMiddleware.cs:50`、`DomainResolver.cs` 多处
- **风险等级**: 🟢 低危

**问题**: `HttpProxyMiddleware.InvokeAsync` 的 while 循环检查 `context.ConnectionClosed` 但未传递给 `input.ReadAsync()`。`TunnelMiddleware` 也存在类似问题。

**建议**: 统一传递 CancellationToken。

#### 2.3.2 PowerShell 压缩命令在处理大量文件时可能失败

- **文件**: `publish-windows-only.cmd:148-149`
- **风险等级**: 🟢 低危

```cmd
powershell -Command "Compress-Archive -Path '...' -DestinationPath '...' -Force"
```

**问题**: `Compress-Archive` 有 2GB 文件大小限制（PowerShell 5.1），且对长路径支持不佳。

**建议**: 考虑使用 `7z` 或 .NET 的 `ZipFile.CreateFromDirectory`。

---

## 3. 代码质量评估

### 3.1 命名与一致性问题

#### 3.1.1 拼写错误

| 文件 | 行 | 错误 | 正确 |
|------|----|------|------|
| `FastGithub.FlowAnalyze/FlowType.cs` | 5 | `Wirte` | `Write` |

影响范围：公共 API 的拼写错误，一旦发布难以修正。且 `DelegatingStream`、`FlowAnalyzeStream` 等文件中引用了此枚举。

#### 3.1.2 "Wire" 拼写错误

- `FastGithub.FlowAnalyze/DelegatingDuplexPipe.cs` 中 `InnerWirteStream`/`OuterWirteStream`（文件名推测）
- 若确为拼写错误应统一修正

---

### 3.2 重复代码

#### 3.2.1 `IsLocalhost` 两处实现不一致

| 文件 | 行 | 实现的地址集 |
|------|----|-------------|
| `HttpProxyMiddleware.cs:101-107` | `IsLoopback` + `IPv6Loopback` + `IPAddress.Any` + `IPv6Any` |
| `TunnelMiddleware.cs:145-147` | 仅 `IsLoopback` |

**问题**: 两处对"本地地址"的定义不一致，`HttpProxyMiddleware` 允许 `0.0.0.0`（任意地址），`TunnelMiddleware` 拒绝。应在公共类库中统一定义。

#### 3.2.2 Socket 连接逻辑重复

`TunnelMiddleware.CreateConnectionAsync` 和 `HttpClientHandler.ConnectCallback` 中都实现了：
1. 创建 Socket
2. 设置超时
3. 连接到 IPEndPoint
4. 异常处理

**建议**: 提取公共的 `SocketConnector` 辅助类。

---

### 3.3 架构问题

#### 3.3.1 平台特定代码混合

`CertService.cs` 中同时包含 Windows ACL、Linux chmod 等平台代码，应该使用策略模式或依赖注入分离。

**现状**:
```csharp
if (OperatingSystem.IsWindows()) { /* ACL */ }
else { /* chmod */ }
```

**建议**: 提取 `IFilePermissionService` 接口，平台特定的实现通过 DI 注入。

#### 3.3.2 配置覆盖依赖文件系统顺序

- **文件**: `Startup.cs:68-75`

```csharp
foreach (var file in Directory.GetFiles(APPSETTINGS, "appsettings.*.json"))
```

**问题**: `Directory.GetFiles` 的顺序不保证（依赖于文件系统），配置覆盖结果不确定。

**建议**: 为 appsettings 目录下的文件添加明确的加载顺序，如 `appsettings.01.github.json`、`appsettings.02.custom.json`，并按文件名排序加载。

#### 3.3.3 UI 与主程序耦合过紧

**问题**: `FastGithub.UI` 依赖：
1. 硬编码的相对路径查找 `fastgithub.exe`
2. 构建项目的能力（`dotnet build`）
3. 主进程的 UDP 日志端口

这些耦合导致 UI 必须在特定目录结构下才能工作，无法独立分发。

**建议**: 打包后通过注册表或配置文件指定主程序路径，移除构建功能。

---

### 3.4 其他代码问题

#### 3.4.1 缺少 ConfigureAwait(false)

**影响范围**: 几乎所有 `.cs` 文件中的 `await` 调用

**严重程度**: 🟢 低危

**说明**: ASP.NET Core 应用中上下文同步开销较小，但类库项目（Configuration、DomainResolve 等）推荐使用 `ConfigureAwait(false)` 以减少死锁风险。

#### 3.4.2 文件/目录路径使用字符串拼接

**文件**: `CertService.cs:25-27`

```csharp
public string CaCerFilePath { get; } = OperatingSystem.IsLinux() 
    ? $"{CACERT_PATH}/fastgithub.crt" 
    : $"{CACERT_PATH}/fastgithub.cer";
```

**建议**: 使用 `Path.Combine(CACERT_PATH, "fastgithub.cer")`。

#### 3.4.3 Process.Start 使用 UseShellExecute 不一致

一些地方使用 `UseShellExecute = true`（如 `CertService.cs:203` 的 git 配置），另一些使用 `UseShellExecute = false`（如 `Program.cs:138` 的 dotnet build）。

**建议**: 统一定义，非交互命令使用 `UseShellExecute = false` + `CreateNoWindow = true`。

---

## 4. 优化建议优先级

| 优先级 | 类别 | 位置 | 建议 |
|--------|------|------|------|
| **P0** | 安全 | `CertService.cs` | CA 密钥派生改为 PBKDF2 + 随机盐 |
| **P0** | 安全 | `CertService.cs` | AES IV 改为随机生成 |
| **P0** | 稳定 | `DnsClient.cs` | 修复 SemaphoreSlim 清理竞态 |
| **P0** | 调试 | 全项目 | 空 catch 块至少加日志 |
| **P1** | 稳定 | `TunnelMiddleware.cs` | 修复未观察的任务异常 |
| **P1** | 安全 | `Program.cs (UI)` | 进程启动添加超时 |
| **P1** | 稳定 | `DnsClient.cs` | 修复 Timer 回调未等待问题 |
| **P1** | 质量 | `FlowType.cs` | 修复 `Wirte` 拼写 |
| **P2** | 质量 | `HttpProxyMiddleware.cs` / `TunnelMiddleware.cs` | 提取共享 `IsLocalhost` |
| **P2** | 架构 | `CertService.cs` | 提取 `IFilePermissionService` |
| **P2** | 架构 | `Startup.cs` | 配置覆盖按文件名排序 |
| **P2** | 安全 | `HttpClientHandler.cs` | 跳过验证域名列表纳入配置 |
| **P3** | 质量 | 全项目 | 添加 `ConfigureAwait(false)` |
| **P3** | 架构 | `Program.cs (UI)` | 移除构建功能，改用配置定位 |
| **P3** | 架构 | 发布脚本 | 替换 `Compress-Archive` 为可靠方案 |

---

## 总结

FastGithub 作为一个网络代理工具，整体架构设计合理，安全基础（localhost-only 代理、SSRF 保护、证书加密存储）已基本到位。需要重点关注的是：

1. **加密实现细节** — 密钥派生和 IV 管理存在明显的密码学使用缺陷（P0）
2. **并发安全** — SemaphoreSlim 清理和未观察的任务异常是潜在的运行时崩溃点（P0-P1）
3. **可观测性** — 空 catch 块使得问题诊断极为困难（P0）
4. **代码一致性** — 重复代码和拼写错误虽不影响功能但影响维护成本（P2）

前三项建议在下一个版本中优先修复。
