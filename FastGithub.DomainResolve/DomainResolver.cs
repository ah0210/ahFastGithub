using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FastGithub.DomainResolve
{
    /// <summary>
    /// 域名解析器
    /// </summary> 
    sealed class DomainResolver : IDomainResolver
    {
        private const int MAX_IP_COUNT = 3;
        private readonly DnsClient dnsClient;
        private readonly PersistenceService persistence;
        private readonly IPAddressService addressService;
        private readonly ILogger<DomainResolver> logger;
        
        // 按需测速缓存：存储已测速的IP地址，包含测速时间和是否过期的信息
        private readonly ConcurrentDictionary<DnsEndPoint, (IPAddress[] addresses, DateTime lastUpdateTime, TimeSpan cacheDuration)> speedTestCache = new();
        
        // 正在进行测速的锁，防止同一域名重复测速
        private readonly ConcurrentDictionary<DnsEndPoint, SemaphoreSlim> speedTestLocks = new();

        /// <summary>
        /// 域名解析器
        /// </summary>
        /// <param name="dnsClient"></param>
        /// <param name="persistence"></param>
        /// <param name="addressService"></param>
        /// <param name="logger"></param>
        public DomainResolver(
            DnsClient dnsClient,
            PersistenceService persistence,
            IPAddressService addressService,
            ILogger<DomainResolver> logger)
        {
            this.dnsClient = dnsClient;
            this.persistence = persistence;
            this.addressService = addressService;
            this.logger = logger;

            // 从持久化存储加载已知的DNS端点，但不再预加载测速结果
            foreach (var endPoint in persistence.ReadDnsEndPoints())
            {
                this.speedTestCache.TryAdd(endPoint, (Array.Empty<IPAddress>(), DateTime.MinValue, TimeSpan.Zero));
            }
        }

        /// <summary>
        /// 解析域名
        /// </summary>
        /// <param name="endPoint">节点</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async IAsyncEnumerable<IPAddress> ResolveAsync(DnsEndPoint endPoint, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // 检查缓存中是否有有效的测速结果
            if (this.speedTestCache.TryGetValue(endPoint, out var cachedResult))
            {
                // 检查缓存是否仍然有效（未过期）
                if (DateTime.Now.Subtract(cachedResult.lastUpdateTime) < cachedResult.cacheDuration && 
                    cachedResult.addresses.Length > 0)
                {
                    // 返回缓存的测速结果
                    foreach (var address in cachedResult.addresses)
                    {
                        yield return address;
                    }
                    yield break; // 直接结束，不再执行下面的逻辑
                }
            }
            
            // 如果没有有效缓存或缓存已过期，获取测速锁以防止重复测速
            var semaphore = this.speedTestLocks.GetOrAdd(endPoint, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);
            
            try
            {
                // 双重检查：在获得锁之后再次检查缓存
                if (this.speedTestCache.TryGetValue(endPoint, out var doubleCheckResult))
                {
                    if (DateTime.Now.Subtract(doubleCheckResult.lastUpdateTime) < doubleCheckResult.cacheDuration && 
                        doubleCheckResult.addresses.Length > 0)
                    {
                        // 如果另一个线程已经完成了测速，直接返回结果
                        foreach (var address in doubleCheckResult.addresses)
                        {
                            yield return address;
                        }
                        yield break;
                    }
                }
                
                // 添加端点到持久化存储（如果尚未存在）
                if (this.speedTestCache.ContainsKey(endPoint) == false)
                {
                    this.speedTestCache.TryAdd(endPoint, (Array.Empty<IPAddress>(), DateTime.MinValue, TimeSpan.Zero));
                    await this.persistence.WriteDnsEndPointsAsync(this.speedTestCache.Keys, cancellationToken);
                }

                // 对当前端点执行按需测速
                var oldAddresses = this.speedTestCache.TryGetValue(endPoint, out var oldResult) ? oldResult.addresses : Array.Empty<IPAddress>();
                var newAddresses = await this.addressService.GetAddressesAsync(endPoint, oldAddresses, cancellationToken);
                
                // 更新缓存
                var cacheDuration = TimeSpan.FromMinutes(30); // 缓存30分钟
                this.speedTestCache[endPoint] = (newAddresses, DateTime.Now, cacheDuration);

                var oldSegmentums = oldAddresses.Take(MAX_IP_COUNT);
                var newSegmentums = newAddresses.Take(MAX_IP_COUNT);
                if (oldSegmentums.SequenceEqual(newSegmentums) == false)
                {
                    var addressArray = string.Join(", ", newSegmentums.Select(item => item.ToString()));
                    this.logger.LogInformation($"{endPoint.Host}:{endPoint.Port}->[{addressArray}]");
                }

                // 返回测速后的结果
                foreach (var address in newAddresses)
                {
                    yield return address;
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// 对所有已缓存的节点进行测速（主要用于主动刷新缓存）
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task TestSpeedAsync(CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            
            // 只对缓存中存在的DNS端点进行测速
            foreach (var keyValue in this.speedTestCache.OrderBy(item => item.Value.addresses.Length))
            {
                var dnsEndPoint = keyValue.Key;
                var oldAddresses = keyValue.Value.addresses;

                // 创建异步任务进行测速，但不立即等待
                tasks.Add(ProcessDnsEndpointAsync(dnsEndPoint, oldAddresses, cancellationToken));
            }

            // 等待所有测速任务完成
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 处理单个DNS端点的测速
        /// </summary>
        /// <param name="dnsEndPoint"></param>
        /// <param name="oldAddresses"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task ProcessDnsEndpointAsync(DnsEndPoint dnsEndPoint, IPAddress[] oldAddresses, CancellationToken cancellationToken)
        {
            // 获取测速锁以防止重复测速
            var semaphore = this.speedTestLocks.GetOrAdd(dnsEndPoint, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);
            
            try
            {
                var newAddresses = await this.addressService.GetAddressesAsync(dnsEndPoint, oldAddresses, cancellationToken);
                
                // 更新缓存
                var cacheDuration = TimeSpan.FromMinutes(30); // 缓存30分钟
                this.speedTestCache[dnsEndPoint] = (newAddresses, DateTime.Now, cacheDuration);

                var oldSegmentums = oldAddresses.Take(MAX_IP_COUNT);
                var newSegmentums = newAddresses.Take(MAX_IP_COUNT);
                if (oldSegmentums.SequenceEqual(newSegmentums) == false)
                {
                    var addressArray = string.Join(", ", newSegmentums.Select(item => item.ToString()));
                    this.logger.LogInformation($"{dnsEndPoint.Host}:{dnsEndPoint.Port}->[{addressArray}]");
                }
            }
            finally
            {
                semaphore.Release();
            }
        }
        
        /// <summary>
        /// 强制刷新指定端点的测速缓存
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task RefreshCacheAsync(DnsEndPoint endPoint, CancellationToken cancellationToken)
        {
            // 获取测速锁以防止重复测速
            var semaphore = this.speedTestLocks.GetOrAdd(endPoint, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);
            
            try
            {
                var oldAddresses = this.speedTestCache.TryGetValue(endPoint, out var oldResult) ? oldResult.addresses : Array.Empty<IPAddress>();
                var newAddresses = await this.addressService.GetAddressesAsync(endPoint, oldAddresses, cancellationToken);
                
                // 更新缓存
                var cacheDuration = TimeSpan.FromMinutes(30); // 缓存30分钟
                this.speedTestCache[endPoint] = (newAddresses, DateTime.Now, cacheDuration);

                var oldSegmentums = oldAddresses.Take(MAX_IP_COUNT);
                var newSegmentums = newAddresses.Take(MAX_IP_COUNT);
                if (oldSegmentums.SequenceEqual(newSegmentums) == false)
                {
                    var addressArray = string.Join(", ", newSegmentums.Select(item => item.ToString()));
                    this.logger.LogInformation($"{endPoint.Host}:{endPoint.Port}->[{addressArray}]");
                }
            }
            finally
            {
                semaphore.Release();
            }
        }
        
        /// <summary>
        /// 清理过期的缓存项
        /// </summary>
        /// <param name="maxCacheAge">最大缓存时间，超过此时间的缓存将被清理</param>
        public void CleanupExpiredCache(TimeSpan maxCacheAge)
        {
            var now = DateTime.Now;
            var expiredKeys = new List<DnsEndPoint>();
            
            foreach (var kvp in this.speedTestCache)
            {
                var (addresses, lastUpdateTime, cacheDuration) = kvp.Value;
                
                // 如果缓存时间超过最大缓存时间，则标记为过期
                if (now.Subtract(lastUpdateTime) > maxCacheAge)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }
            
            // 移除过期的缓存项
            foreach (var key in expiredKeys)
            {
                this.speedTestCache.TryRemove(key, out _);
                this.logger.LogDebug($"已清理过期缓存: {key.Host}:{key.Port}");
            }
        }
        
        /// <summary>
        /// 清理所有缓存
        /// </summary>
        public void ClearAllCache()
        {
            var count = this.speedTestCache.Count;
            this.speedTestCache.Clear();
            this.logger.LogDebug($"已清理所有缓存，共 {count} 项");
        }
    }
}
