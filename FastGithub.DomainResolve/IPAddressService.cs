using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FastGithub.DomainResolve
{
    /// <summary>
    /// IP服务
    /// 域名IP关系缓存10分钟
    /// IPEndPoint时延缓存5分钟
    /// IPEndPoint连接超时5秒
    /// </summary>
    sealed class IPAddressService
    {
        private record DomainAddress(string Domain, IPAddress Address);
        private readonly TimeSpan domainAddressExpiration = TimeSpan.FromMinutes(10d);
        private readonly IMemoryCache domainAddressCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));

        private record AddressElapsed(IPAddress Address, TimeSpan Elapsed);
        private readonly TimeSpan problemElapsedExpiration = TimeSpan.FromMinutes(1d);
        private readonly TimeSpan normalElapsedExpiration = TimeSpan.FromMinutes(5d);
        private readonly TimeSpan connectTimeout = TimeSpan.FromSeconds(15d);
        private readonly IMemoryCache addressElapsedCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));

        private readonly DnsClient dnsClient;
        private readonly ILogger<IPAddressService> logger;

        /// <summary>
        /// IP服务
        /// </summary>
        /// <param name="dnsClient"></param>
        /// <param name="logger"></param>
        public IPAddressService(DnsClient dnsClient, ILogger<IPAddressService> logger)
        {
            this.dnsClient = dnsClient;
            this.logger = logger;
        }

        /// <summary>
        /// 并行获取可连接的IP
        /// </summary>
        /// <param name="dnsEndPoint"></param>
        /// <param name="oldAddresses"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IPAddress[]> GetAddressesAsync(DnsEndPoint dnsEndPoint, IEnumerable<IPAddress> oldAddresses, CancellationToken cancellationToken)
        {
            this.logger.LogInformation($"正在获取 {dnsEndPoint.Host}:{dnsEndPoint.Port} 的可连接IP...");
            var ipEndPoints = new HashSet<IPEndPoint>();

            // 历史未过期的IP节点
            foreach (var address in oldAddresses)
            {
                var domainAddress = new DomainAddress(dnsEndPoint.Host, address);
                if (this.domainAddressCache.TryGetValue(domainAddress, out _))
                {
                    ipEndPoints.Add(new IPEndPoint(address, dnsEndPoint.Port));
                }
            }

            // 新解析出的IP节点
            await foreach (var address in this.dnsClient.ResolveAsync(dnsEndPoint, fastSort: false, cancellationToken))
            {
                ipEndPoints.Add(new IPEndPoint(address, dnsEndPoint.Port));
                var domainAddress = new DomainAddress(dnsEndPoint.Host, address);
                this.domainAddressCache.Set(domainAddress, default(object), this.domainAddressExpiration);
            }

            this.logger.LogInformation($"为 {dnsEndPoint.Host} 解析到 {ipEndPoints.Count} 个IP地址");
            if (ipEndPoints.Count == 0)
            {
                this.logger.LogWarning($"未解析到 {dnsEndPoint.Host} 的任何IP地址");
                return Array.Empty<IPAddress>();
            }

            var addressElapsedTasks = ipEndPoints.Select(item => this.GetAddressElapsedAsync(item, cancellationToken));
            var addressElapseds = await Task.WhenAll(addressElapsedTasks);

            var availableAddresses = addressElapseds
                .Where(item => item.Elapsed < TimeSpan.MaxValue)
                .OrderBy(item => item.Elapsed)
                .Select(item => item.Address)
                .ToArray();

            this.logger.LogInformation($"{dnsEndPoint.Host} 可用IP地址: {string.Join(", ", availableAddresses)}");
            return availableAddresses;
        }


        /// <summary>
        /// 获取IP节点的时延
        /// </summary> 
        /// <param name="endPoint"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<AddressElapsed> GetAddressElapsedAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
        {
            if (this.addressElapsedCache.TryGetValue<AddressElapsed>(endPoint, out var addressElapsed))
            {
                this.logger.LogDebug($"从缓存获取 {endPoint.Address} 的连接时延: {addressElapsed.Elapsed.TotalMilliseconds}ms");
                return addressElapsed;
            }

            var stopWatch = Stopwatch.StartNew();
            try
            {
                this.logger.LogDebug($"正在测试连接到 {endPoint.Address}:{endPoint.Port} (超时: {this.connectTimeout.TotalSeconds}s)...");
                using var timeoutTokenSource = new CancellationTokenSource(this.connectTimeout);
                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutTokenSource.Token);
                using var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(endPoint, linkedTokenSource.Token);

                addressElapsed = new AddressElapsed(endPoint.Address, stopWatch.Elapsed);
                this.logger.LogInformation($"成功连接到 {endPoint.Address}:{endPoint.Port}，耗时: {addressElapsed.Elapsed.TotalMilliseconds:F2}ms");
                return this.addressElapsedCache.Set(endPoint, addressElapsed, this.normalElapsedExpiration);
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();

                addressElapsed = new AddressElapsed(endPoint.Address, TimeSpan.MaxValue);
                var expiration = IsLocalNetworkProblem(ex) ? this.problemElapsedExpiration : this.normalElapsedExpiration;
                this.logger.LogWarning($"连接到 {endPoint.Address}:{endPoint.Port} 失败: {ex.Message}");
                return this.addressElapsedCache.Set(endPoint, addressElapsed, expiration);
            }
            finally
            {
                stopWatch.Stop();
            }
        }

        /// <summary>
        /// 是否为本机网络问题
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        private static bool IsLocalNetworkProblem(Exception ex)
        {
            if (ex is not SocketException socketException)
            {
                return false;
            }

            var code = socketException.SocketErrorCode;
            return code == SocketError.NetworkDown || code == SocketError.NetworkUnreachable;
        }
    }
}
