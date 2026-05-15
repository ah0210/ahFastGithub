using FastGithub.Configuration;
using FastGithub.DomainResolve;
using System;
using System.Collections.Concurrent;

namespace FastGithub.Http
{
    sealed class HttpClientFactory : IHttpClientFactory
    {
        private readonly IDomainResolver domainResolver;
        private readonly FastGithubConfig fastGithubConfig;

        private readonly TimeSpan firstLiftTime = TimeSpan.FromSeconds(10d);

        private readonly TimeSpan nextLifeTime = TimeSpan.FromSeconds(100d);

        private readonly LifetimeHttpHandlerCleaner httpHandlerCleaner = new();

        private readonly ConcurrentDictionary<LifeTimeKey, Lazy<LifetimeHttpHandler>> httpHandlerLazyCache = new();

        public HttpClientFactory(IDomainResolver domainResolver, FastGithubConfig fastGithubConfig)
        {
            this.domainResolver = domainResolver;
            this.fastGithubConfig = fastGithubConfig;
        }

        public HttpClient CreateHttpClient(string domain, DomainConfig domainConfig)
        {
            var lifeTimeKey = new LifeTimeKey(domain, domainConfig);
            var lifetimeHttpHandler = this.httpHandlerLazyCache.GetOrAdd(lifeTimeKey, CreateLifetimeHttpHandlerLazy).Value;
            return new HttpClient(lifetimeHttpHandler, disposeHandler: false);

            Lazy<LifetimeHttpHandler> CreateLifetimeHttpHandlerLazy(LifeTimeKey lifeTimeKey)
            {
                return new Lazy<LifetimeHttpHandler>(() => this.CreateLifetimeHttpHandler(lifeTimeKey, this.firstLiftTime), true);
            }
        }

        private LifetimeHttpHandler CreateLifetimeHttpHandler(LifeTimeKey lifeTimeKey, TimeSpan lifeTime)
        {
            return new LifetimeHttpHandler(this.domainResolver, this.fastGithubConfig, lifeTimeKey, lifeTime, this.OnLifetimeHttpHandlerDeactivate);
        }

        private void OnLifetimeHttpHandlerDeactivate(LifetimeHttpHandler lifetimeHttpHandler)
        {
            var lifeTimeKey = lifetimeHttpHandler.LifeTimeKey;
            this.httpHandlerLazyCache[lifeTimeKey] = CreateLifetimeHttpHandlerLazy(lifeTimeKey);
            this.httpHandlerCleaner.Add(lifetimeHttpHandler);

            Lazy<LifetimeHttpHandler> CreateLifetimeHttpHandlerLazy(LifeTimeKey lifeTimeKey)
            {
                return new Lazy<LifetimeHttpHandler>(() => this.CreateLifetimeHttpHandler(lifeTimeKey, this.nextLifeTime), true);
            }
        }
    }
}
