using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FastGithub.HttpServer.Certs.CaCertInstallers
{
    abstract class CaCertInstallerOfLinux : ICaCertInstaller
    {
        private readonly ILogger logger;

        /// <summary>
        /// 更新工具文件名
        /// </summary>
        protected abstract string CaCertUpdatePath { get; }

        /// <summary>
        /// 证书根目录
        /// </summary>
        protected abstract string CaCertStorePath { get; }

        public CaCertInstallerOfLinux(ILogger logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// 是否支持
        /// </summary>
        /// <returns></returns>
        public bool IsSupported()
        {
            return OperatingSystem.IsLinux() && File.Exists(CaCertUpdatePath);
        }

        /// <summary>
        /// 安装ca证书
        /// </summary>
        /// <param name="caCertFilePath">证书文件路径</param>
        public void Install(string caCertFilePath)
        {
            var userCertDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "ca-certificates");
            var destCertFilePath = Path.Combine(userCertDir, Path.GetFileName(caCertFilePath));
            if (File.Exists(destCertFilePath) && File.ReadAllBytes(caCertFilePath).SequenceEqual(File.ReadAllBytes(destCertFilePath)))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(userCertDir);
                foreach (var item in Directory.GetFiles(userCertDir, "fastgithub.*"))
                {
                    File.Delete(item);
                }
                File.Copy(caCertFilePath, destCertFilePath, overwrite: true);

                if (File.Exists(CaCertUpdatePath))
                {
                    Process.Start(CaCertUpdatePath).WaitForExit();
                }

                logger.LogInformation($"已自动向用户目录安装CA证书{caCertFilePath}");
            }
            catch (Exception ex)
            {
                File.Delete(destCertFilePath);
                logger.LogWarning(ex.Message, "自动安装CA证书异常");
            }
        }
    }
}
