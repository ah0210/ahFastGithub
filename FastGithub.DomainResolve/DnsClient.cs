using DNS.Client;
using DNS.Client.RequestResolver;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using FastGithub.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FastGithub.DomainResolve
{
    sealed class DnsClient : IDisposable
    {
        private const int DNS_PORT = 53;
        private const string LOCALHOST = "localhost";

        private readonly DnscryptProxy dnscryptProxy;
        private readonly FastGithubConfig fastGithubConfig;
        private readonly ILogger<DnsClient> logger;

        private readonly ConcurrentDictionary<string, SemaphoreSlim> semaphoreSlims = new();
        private readonly IMemoryCache dnsStateCache;
        private readonly IMemoryCache dnsLookupCache;

        private readonly TimeSpan stateExpiration = TimeSpan.FromMinutes(10d);
        private readonly TimeSpan minTimeToLive = TimeSpan.FromMinutes(1d);
        private readonly TimeSpan maxTimeToLive = TimeSpan.FromMinutes(30d);

        private readonly int resolveTimeout = (int)TimeSpan.FromSeconds(3d).TotalMilliseconds;
        private static readonly TimeSpan tcpConnectTimeout = TimeSpan.FromSeconds(1.5d);

        private const int MaxCacheSize = 1000;
        private const int MaxSemaphoreCount = 2000;

        private record LookupResult(IList<IPAddress> Addresses, TimeSpan TimeToLive);

        public DnsClient(
            DnscryptProxy dnscryptProxy,
            FastGithubConfig fastGithubConfig,
            ILogger<DnsClient> logger)
        {
            this.dnscryptProxy = dnscryptProxy;
            this.fastGithubConfig = fastGithubConfig;
            this.logger = logger;

            var stateCacheOptions = Options.Create(new MemoryCacheOptions
            {
                SizeLimit = 1000,
                ExpirationScanFrequency = TimeSpan.FromMinutes(5)
            });
            this.dnsStateCache = new MemoryCache(stateCacheOptions);

            var lookupCacheOptions = Options.Create(new MemoryCacheOptions
            {
                SizeLimit = 5000,
                ExpirationScanFrequency = TimeSpan.FromMinutes(5)
            });
            this.dnsLookupCache = new MemoryCache(lookupCacheOptions);
        }

        public async IAsyncEnumerable<IPAddress> ResolveAsync(DnsEndPoint endPoint, bool fastSort, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var hashSet = new HashSet<IPAddress>();
            var overallTimeout = TimeSpan.FromSeconds(30d);
            using var overallCts = new CancellationTokenSource(overallTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, overallCts.Token);

            await foreach (var dns in this.GetDnsServersAsync(linkedCts.Token))
            {
                var addresses = await this.LookupAsync(dns, endPoint, fastSort, linkedCts.Token);
                foreach (var address in addresses)
                {
                    if (hashSet.Add(address) == true)
                    {
                        yield return address;
                    }
                }
            }
        }

        private async IAsyncEnumerable<IPEndPoint> GetDnsServersAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var cryptDns = this.dnscryptProxy.LocalEndPoint;
            if (cryptDns != null)
            {
                yield return cryptDns;
            }
            else
            {
                var sortedDnsServers = await this.GetSortedFallbackDnsAsync(cancellationToken);
                foreach (var dns in sortedDnsServers)
                {
                    yield return dns;
                }
            }

            if (cryptDns == null && (this.fastGithubConfig.FallbackDns == null || this.fastGithubConfig.FallbackDns.Length == 0))
            {
                yield return new IPEndPoint(IPAddress.Parse("8.8.8.8"), DNS_PORT);
            }
        }

        private SemaphoreSlim GetOrCreateSemaphore(string key)
        {
            if (this.semaphoreSlims.TryGetValue(key, out var semaphore))
                return semaphore;

            semaphore = this.semaphoreSlims.GetOrAdd(key, _ =>
            {
                if (this.semaphoreSlims.Count > MaxSemaphoreCount)
                {
                    var keyToRemove = this.semaphoreSlims.Keys.FirstOrDefault();
                    if (keyToRemove != null)
                    {
                        this.semaphoreSlims.TryRemove(keyToRemove, out var _removed);
                    }
                }
                return new SemaphoreSlim(1, 1);
            });
            return semaphore;
        }

        private async ValueTask<bool> IsDnsAvailableAsync(IPEndPoint dns, CancellationToken cancellationToken)
        {
            if (dns.Port != DNS_PORT)
            {
                return true;
            }

            if (this.dnsStateCache.TryGetValue<bool>(dns, out var available))
            {
                return available;
            }

            var key = dns.ToString();
            var semaphore = this.GetOrCreateSemaphore(key);
            await semaphore.WaitAsync(CancellationToken.None);

            try
            {
                using var timeoutTokenSource = new CancellationTokenSource(tcpConnectTimeout);
                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, cancellationToken);
                using var socket = new Socket(dns.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(dns, linkedTokenSource.Token);
                return this.dnsStateCache.Set(dns, true, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = this.stateExpiration,
                    Size = 1
                });
            }
            catch (Exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return this.dnsStateCache.Set(dns, false, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = this.stateExpiration,
                    Size = 1
                });
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task<IList<IPAddress>> LookupAsync(IPEndPoint dns, DnsEndPoint endPoint, bool fastSort, CancellationToken cancellationToken = default)
        {
            var key = $"{dns}/{endPoint}";
            var semaphore = this.GetOrCreateSemaphore(key);
            await semaphore.WaitAsync(CancellationToken.None);

            try
            {
                if (this.dnsLookupCache.TryGetValue<IList<IPAddress>>(key, out var value))
                {
                    return value;
                }
                var result = await this.LookupCoreAsync(dns, endPoint, fastSort, cancellationToken);
                return this.dnsLookupCache.Set(key, result.Addresses, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = result.TimeToLive,
                    Size = Math.Max(1, result.Addresses.Count)
                });
            }
            catch (OperationCanceledException)
            {
                return Array.Empty<IPAddress>();
            }
            catch (Exception ex)
            {
                this.logger.LogWarning($"{endPoint.Host}@{dns}->{ex.Message}");
                var expiration = IsSocketException(ex) ? this.maxTimeToLive : this.minTimeToLive;
                return this.dnsLookupCache.Set(key, Array.Empty<IPAddress>(), new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration,
                    Size = 1
                });
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task<List<IPEndPoint>> GetSortedFallbackDnsAsync(CancellationToken cancellationToken)
        {
            if (this.fastGithubConfig.FallbackDns == null || this.fastGithubConfig.FallbackDns.Length == 0)
            {
                return new List<IPEndPoint>();
            }

            var tasks = this.fastGithubConfig.FallbackDns.Select(async dns =>
            {
                var startTime = DateTime.Now;
                try
                {
                    if (await this.IsDnsAvailableAsync(dns, cancellationToken))
                    {
                        var endTime = DateTime.Now;
                        var latency = endTime - startTime;
                        return (dns, latency, available: true);
                    }
                }
                catch (Exception)
                {
                    return (dns, latency: TimeSpan.MaxValue, available: false);
                }
                return (dns, latency: TimeSpan.MaxValue, available: false);
            });

            var results = await Task.WhenAll(tasks);
            return results
                .Where(r => r.available)
                .OrderBy(r => r.latency)
                .Select(r => r.dns)
                .ToList();
        }

        private static bool IsSocketException(Exception ex)
        {
            if (ex is SocketException)
            {
                return true;
            }

            var inner = ex.InnerException;
            return inner != null && IsSocketException(inner);
        }

        private async Task<LookupResult> LookupCoreAsync(IPEndPoint dns, DnsEndPoint endPoint, bool fastSort, CancellationToken cancellationToken = default)
        {
            if (endPoint.Host == LOCALHOST)
            {
                var loopbacks = new List<IPAddress>();
                if (Socket.OSSupportsIPv4 == true)
                {
                    loopbacks.Add(IPAddress.Loopback);
                }
                if (Socket.OSSupportsIPv6 == true)
                {
                    loopbacks.Add(IPAddress.IPv6Loopback);
                }
                return new LookupResult(loopbacks, TimeSpan.MaxValue);
            }

            var resolver = dns.Port == DNS_PORT
                ? (IRequestResolver)new TcpRequestResolver(dns)
                : new UdpRequestResolver(dns, new TcpRequestResolver(dns), this.resolveTimeout);

            var addressRecords = await GetAddressRecordsAsync(resolver, endPoint.Host, cancellationToken);
            var addresses = (IList<IPAddress>)addressRecords
                .Where(item => IPAddress.IsLoopback(item.IPAddress) == false)
                .Select(item => item.IPAddress)
                .ToArray();

            if (addresses.Count == 0)
            {
                return new LookupResult(addresses, this.minTimeToLive);
            }

            if (fastSort == true)
            {
                addresses = await OrderByConnectAnyAsync(addresses, endPoint.Port, cancellationToken);
            }

            var timeToLive = addressRecords.Min(item => item.TimeToLive);
            if (timeToLive <= TimeSpan.Zero)
            {
                timeToLive = this.minTimeToLive;
            }
            else if (timeToLive > this.maxTimeToLive)
            {
                timeToLive = this.maxTimeToLive;
            }

            return new LookupResult(addresses, timeToLive);
        }

        private static async Task<IList<IPAddressResourceRecord>> GetAddressRecordsAsync(IRequestResolver resolver, string domain, CancellationToken cancellationToken)
        {
            var addressRecords = new List<IPAddressResourceRecord>();
            if (Socket.OSSupportsIPv4 == true)
            {
                var records = await GetRecordsAsync(RecordType.A);
                addressRecords.AddRange(records);
            }

            if (Socket.OSSupportsIPv6 == true)
            {
                var records = await GetRecordsAsync(RecordType.AAAA);
                addressRecords.AddRange(records);
            }
            return addressRecords;

            async Task<IEnumerable<IPAddressResourceRecord>> GetRecordsAsync(RecordType recordType)
            {
                var request = new Request
                {
                    RecursionDesired = true,
                    OperationCode = OperationCode.Query
                };

                request.Questions.Add(new Question(new Domain(domain), recordType));
                var clientRequest = new ClientRequest(resolver, request);
                var response = await clientRequest.Resolve(cancellationToken);
                return response.AnswerRecords.OfType<IPAddressResourceRecord>();
            }
        }

        private static async Task<IList<IPAddress>> OrderByConnectAnyAsync(IList<IPAddress> addresses, int port, CancellationToken cancellationToken)
        {
            if (addresses.Count <= 1)
            {
                return addresses;
            }

            using var controlTokenSource = new CancellationTokenSource(tcpConnectTimeout);
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, controlTokenSource.Token);

            var connectTasks = addresses.Select(address => ConnectAsync(address, port, linkedTokenSource.Token));
            var fastestAddress = await await Task.WhenAny(connectTasks);
            controlTokenSource.Cancel();

            if (fastestAddress == null || addresses.First().Equals(fastestAddress))
            {
                return addresses;
            }

            var list = new List<IPAddress> { fastestAddress };
            foreach (var address in addresses)
            {
                if (address.Equals(fastestAddress) == false)
                {
                    list.Add(address);
                }
            }
            return list;
        }

        private static async Task<IPAddress?> ConnectAsync(IPAddress address, int port, CancellationToken cancellationToken)
        {
            try
            {
                using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(address, port, cancellationToken);
                return address;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void Dispose()
        {
            foreach (var semaphore in this.semaphoreSlims.Values)
            {
                semaphore?.Dispose();
            }
            this.semaphoreSlims.Clear();

            this.dnsStateCache?.Dispose();
            this.dnsLookupCache?.Dispose();
        }
    }
}
