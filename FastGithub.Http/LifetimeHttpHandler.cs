using FastGithub.Configuration;
using FastGithub.DomainResolve;
using System;
using System.Net.Http;
using System.Threading;

namespace FastGithub.Http
{
    sealed class LifetimeHttpHandler : DelegatingHandler
    {
        private readonly Timer timer;

        public LifeTimeKey LifeTimeKey { get; }

        public LifetimeHttpHandler(IDomainResolver domainResolver, FastGithubConfig fastGithubConfig, LifeTimeKey lifeTimeKey, TimeSpan lifeTime, Action<LifetimeHttpHandler> deactivateAction)
        {
            this.LifeTimeKey = lifeTimeKey;
            this.InnerHandler = new HttpClientHandler(lifeTimeKey.DomainConfig, fastGithubConfig, domainResolver);
            this.timer = new Timer(this.OnTimerCallback, deactivateAction, lifeTime, Timeout.InfiniteTimeSpan);
        }

        /// <summary>
        /// timer触发时
        /// </summary>
        /// <param name="state"></param>
        private void OnTimerCallback(object? state)
        {
            this.timer.Dispose();
            ((Action<LifetimeHttpHandler>)(state!))(this);
        }

        /// <summary>
        /// 这里不释放资源
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
        }
    }
}
