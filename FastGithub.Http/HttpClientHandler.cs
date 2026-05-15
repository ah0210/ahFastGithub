using FastGithub.Configuration;
using FastGithub.DomainResolve;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace FastGithub.Http
{
    class HttpClientHandler : DelegatingHandler
    {
        private readonly DomainConfig domainConfig;
        private readonly FastGithubConfig fastGithubConfig;
        private readonly IDomainResolver domainResolver;
        private readonly TimeSpan connectTimeout = TimeSpan.FromSeconds(10d);

        public HttpClientHandler(DomainConfig domainConfig, FastGithubConfig fastGithubConfig, IDomainResolver domainResolver)
        {
            this.domainConfig = domainConfig;
            this.fastGithubConfig = fastGithubConfig;
            this.domainResolver = domainResolver;
            this.InnerHandler = this.CreateSocketsHttpHandler();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri;
            if (uri == null)
            {
                throw new FastGithubException("必须指定请求的URI");
            }

            var isHttps = uri.Scheme == Uri.UriSchemeHttps;
            var tlsSniValue = this.domainConfig.GetTlsSniPattern().WithDomain(uri.Host).WithRandom();
            request.SetRequestContext(new RequestContext(isHttps, tlsSniValue));

            request.Headers.Host = uri.Host;
            request.RequestUri = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttp }.Uri;

            if (this.domainConfig.Timeout != null)
            {
                using var timeoutTokenSource = new CancellationTokenSource(this.domainConfig.Timeout.Value);
                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutTokenSource.Token);
                return await base.SendAsync(request, linkedTokenSource.Token);
            }
            return await base.SendAsync(request, cancellationToken);
        }

        private SocketsHttpHandler CreateSocketsHttpHandler()
        {
            return new SocketsHttpHandler
            {
                Proxy = null,
                UseProxy = false,
                UseCookies = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                ConnectCallback = this.ConnectCallback
            };
        }

        private async ValueTask<Stream> ConnectCallback(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            var innerExceptions = new List<Exception>();
            var ipEndPoints = this.GetIPEndPointsAsync(context.DnsEndPoint, cancellationToken);

            await foreach (var ipEndPoint in ipEndPoints)
            {
                try
                {
                    using var timeoutTokenSource = new CancellationTokenSource(this.connectTimeout);
                    using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, cancellationToken);
                    return await this.ConnectAsync(context, ipEndPoint, linkedTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    innerExceptions.Add(new HttpConnectTimeoutException(ipEndPoint.Address));
                }
                catch (Exception ex)
                {
                    innerExceptions.Add(ex);
                }
            }

            try
            {
                await this.domainResolver.RefreshCacheAsync(context.DnsEndPoint, cancellationToken);

                innerExceptions.Clear();
                var refreshedIpEndPoints = this.GetIPEndPointsAsync(context.DnsEndPoint, cancellationToken);

                await foreach (var ipEndPoint in refreshedIpEndPoints)
                {
                    try
                    {
                        using var timeoutTokenSource = new CancellationTokenSource(this.connectTimeout);
                        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, cancellationToken);
                        return await this.ConnectAsync(context, ipEndPoint, linkedTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        innerExceptions.Add(new HttpConnectTimeoutException(ipEndPoint.Address));
                    }
                    catch (Exception ex)
                    {
                        innerExceptions.Add(ex);
                    }
                }
            }
            catch (Exception refreshEx)
            {
                System.Diagnostics.Debug.WriteLine($"刷新域名缓存失败: {refreshEx.Message}");
            }

            throw new AggregateException("找不到任何可成功连接的IP", innerExceptions);
        }

        private async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, IPEndPoint ipEndPoint, CancellationToken cancellationToken)
        {
            var socket = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(ipEndPoint, cancellationToken);
            var stream = new NetworkStream(socket, ownsSocket: true);

            var requestContext = context.InitialRequestMessage.GetRequestContext();
            if (requestContext.IsHttps == false)
            {
                return stream;
            }

            var tlsSniValue = requestContext.TlsSniValue.WithIPAddress(ipEndPoint.Address);
            var sslStream = new SslStream(stream, leaveInnerStreamOpen: false);
            await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = tlsSniValue.Value,
                RemoteCertificateValidationCallback = ValidateServerCertificate
            }, cancellationToken);

            return sslStream;

            bool ValidateServerCertificate(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors)
            {
                var domain = context.DnsEndPoint.Host;
                if (this.fastGithubConfig.IsMatch(domain))
                {
                    return true;
                }

                var seriousErrors = errors & ~(SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch);
                if (seriousErrors != SslPolicyErrors.None)
                {
                    return false;
                }

                if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
                {
                    if (this.domainConfig.TlsIgnoreNameMismatch == true)
                    {
                        return true;
                    }

                    var dnsNames = ReadDnsNames(cert);
                    return dnsNames.Any(dns => IsMatch(dns, domain));
                }

                return true;
            }
        }

        private async IAsyncEnumerable<IPEndPoint> GetIPEndPointsAsync(DnsEndPoint dnsEndPoint, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (IPAddress.TryParse(dnsEndPoint.Host, out var address))
            {
                yield return new IPEndPoint(address, dnsEndPoint.Port);
            }
            else
            {
                if (this.domainConfig.IPAddress != null)
                {
                    yield return new IPEndPoint(this.domainConfig.IPAddress, dnsEndPoint.Port);
                }

                await foreach (var item in this.domainResolver.ResolveAsync(dnsEndPoint, cancellationToken))
                {
                    yield return new IPEndPoint(item, dnsEndPoint.Port);
                }
            }
        }

        private static IEnumerable<string> ReadDnsNames(X509Certificate? cert)
        {
            if (cert is X509Certificate2 x509)
            {
                var extension = x509.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
                if (extension != null)
                {
                    return extension.EnumerateDnsNames();
                }
            }
            return Array.Empty<string>();
        }

        private static bool IsMatch(string dnsName, string? domain)
        {
            if (domain == null)
            {
                return false;
            }
            if (dnsName == domain)
            {
                return true;
            }
            if (dnsName[0] == '*')
            {
                return domain.EndsWith(dnsName[1..]);
            }
            return false;
        }
    }
}
