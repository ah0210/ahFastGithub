using FastGithub.Configuration;
using FastGithub.DomainResolve;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FastGithub.HttpServer.TcpMiddlewares
{
    /// <summary>
    /// 隧道中间件
    /// </summary>
    sealed class TunnelMiddleware
    {
        private readonly FastGithubConfig fastGithubConfig;
        private readonly IDomainResolver domainResolver;
        private readonly TimeSpan connectTimeout = TimeSpan.FromSeconds(10d);

        /// <summary>
        /// 隧道中间件
        /// </summary>
        /// <param name="fastGithubConfig"></param>
        /// <param name="domainResolver"></param> 
        public TunnelMiddleware(
            FastGithubConfig fastGithubConfig,
            IDomainResolver domainResolver)
        {
            this.fastGithubConfig = fastGithubConfig;
            this.domainResolver = domainResolver;
        }

        /// <summary>
        /// 执行中间件
        /// </summary>
        /// <param name="next"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task InvokeAsync(ConnectionDelegate next, ConnectionContext context)
        {
            var remoteAddress = (context.RemoteEndPoint as IPEndPoint)?.Address;
            if (remoteAddress != null && IsLocalhost(remoteAddress) == false)
            {
                context.Abort();
                return;
            }

            var proxyFeature = context.Features.Get<IHttpProxyFeature>();
            if (proxyFeature == null || // 非代理
                proxyFeature.ProxyProtocol != ProxyProtocol.TunnelProxy || //非隧道代理
                context.Features.Get<ITlsConnectionFeature>() != null) // 经过隧道的https
            {
                await next(context);
            }
            else
            {
                var transport = context.Features.Get<IConnectionTransportFeature>()?.Transport;
                if (transport != null)
                {
                    var cancellationToken = context.ConnectionClosed;
                    using var connection = await this.CreateConnectionAsync(proxyFeature.ProxyHost, cancellationToken);

                    var task1 = connection.CopyToAsync(transport.Output, cancellationToken);
                    var task2 = transport.Input.CopyToAsync(connection, cancellationToken);
                    await Task.WhenAny(task1, task2);
                }
            }
        }


        /// <summary>
        /// 创建连接
        /// </summary>
        /// <param name="host"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="AggregateException"></exception>
        private async Task<Stream> CreateConnectionAsync(HostString host, CancellationToken cancellationToken)
        {
            var innerExceptions = new List<Exception>();
            await foreach (var endPoint in this.GetUpstreamEndPointsAsync(host, cancellationToken))
            {
                if (endPoint is IPEndPoint ipEndPoint && IsPrivateAddress(ipEndPoint.Address))
                {
                    innerExceptions.Add(new InvalidOperationException($"拒绝连接到内网地址: {ipEndPoint.Address}"));
                    continue;
                }

                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    using var timeoutTokenSource = new CancellationTokenSource(this.connectTimeout);
                    using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutTokenSource.Token);
                    await socket.ConnectAsync(endPoint, linkedTokenSource.Token);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (Exception ex)
                {
                    socket.Dispose();
                    cancellationToken.ThrowIfCancellationRequested();
                    innerExceptions.Add(ex);
                }
            }
            throw new AggregateException($"无法连接到{host}", innerExceptions);
        }

        /// <summary>
        /// 获取目标终节点
        /// </summary>
        /// <param name="host"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async IAsyncEnumerable<EndPoint> GetUpstreamEndPointsAsync(HostString host, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            const int HTTPS_PORT = 443;
            var targetHost = host.Host;
            var targetPort = host.Port ?? HTTPS_PORT;

            if (IPAddress.TryParse(targetHost, out var address) == true)
            {
                yield return new IPEndPoint(address, targetPort);
            }
            else if (this.fastGithubConfig.IsMatch(targetHost) == false)
            {
                yield return new DnsEndPoint(targetHost, targetPort);
            }
            else
            {
                var dnsEndPoint = new DnsEndPoint(targetHost, targetPort);
                await foreach (var item in this.domainResolver.ResolveAsync(dnsEndPoint, cancellationToken))
                {
                    yield return new IPEndPoint(item, targetPort);
                }
            }
        }

        private static bool IsLocalhost(IPAddress address)
        {
            return IPAddress.IsLoopback(address);
        }

        private static bool IsPrivateAddress(IPAddress address)
        {
            if (IPAddress.IsLoopback(address))
                return true;

            var bytes = address.GetAddressBytes();
            if (address.IsIPv4MappedToIPv6)
            {
                var ipv4 = address.MapToIPv4().GetAddressBytes();
                return IsPrivateIPv4(ipv4);
            }

            if (bytes.Length == 4)
            {
                return IsPrivateIPv4(bytes);
            }

            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal;
        }

        private static bool IsPrivateIPv4(byte[] bytes)
        {
            return bytes[0] == 10 ||
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168) ||
                   (bytes[0] == 127) ||
                   (bytes[0] == 169 && bytes[1] == 254);
        }
    }
}
