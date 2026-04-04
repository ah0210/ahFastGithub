using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;

namespace FastGithub.HttpServer.Certs
{
    sealed class CertService
    {
        private const string CACERT_PATH = "cacert";
        private readonly IMemoryCache serverCertCache;
        private readonly IEnumerable<ICaCertInstaller> certInstallers;
        private readonly ILogger<CertService> logger;
        private X509Certificate2? caCert;

        public string CaCerFilePath { get; } = OperatingSystem.IsLinux() ? $"{CACERT_PATH}/fastgithub.crt" : $"{CACERT_PATH}/fastgithub.cer";

        public string CaKeyFilePath { get; } = $"{CACERT_PATH}/fastgithub.key";

        public CertService(
            IMemoryCache serverCertCache,
            IEnumerable<ICaCertInstaller> certInstallers,
            ILogger<CertService> logger)
        {
            this.serverCertCache = serverCertCache;
            this.certInstallers = certInstallers;
            this.logger = logger;
            Directory.CreateDirectory(CACERT_PATH);
        }

        public bool CreateCaCertIfNotExists()
        {
            if (File.Exists(this.CaCerFilePath) && File.Exists(this.CaKeyFilePath))
            {
                return false;
            }

            File.Delete(this.CaCerFilePath);
            File.Delete(this.CaKeyFilePath);

            var notBefore = DateTimeOffset.Now.AddDays(-1);
            var notAfter = DateTimeOffset.Now.AddYears(2);

            var subjectName = new X500DistinguishedName($"CN={nameof(FastGithub)}");
            this.caCert = CertGenerator.CreateCACertificate(subjectName, notBefore, notAfter);

            var privateKeyPem = this.caCert.GetRSAPrivateKey()?.ExportRSAPrivateKeyPem();
            var privateKeyBytes = Encoding.ASCII.GetBytes(new string(privateKeyPem));

            var encryptedKey = EncryptKey(privateKeyBytes);
            File.WriteAllBytes(this.CaKeyFilePath, encryptedKey);

            RestrictKeyFilePermissions(this.CaKeyFilePath);

            var certPem = this.caCert.ExportCertificatePem();
            File.WriteAllText(this.CaCerFilePath, new string(certPem), Encoding.ASCII);

            return true;
        }

        private static byte[] EncryptKey(byte[] plainBytes)
        {
            using var aes = Aes.Create();
            aes.Key = DeriveKey();
            aes.IV = new byte[16];
            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            ms.Write(aes.IV, 0, aes.IV.Length);
            using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
            cs.Write(plainBytes, 0, plainBytes.Length);
            cs.FlushFinalBlock();
            return ms.ToArray();
        }

        private static byte[] DecryptKey(byte[] encryptedBytes)
        {
            using var aes = Aes.Create();
            aes.Key = DeriveKey();
            var iv = new byte[16];
            Array.Copy(encryptedBytes, 0, iv, 0, 16);
            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(encryptedBytes, 16, encryptedBytes.Length - 16);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var output = new MemoryStream();
            cs.CopyTo(output);
            return output.ToArray();
        }

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

        private static bool IsPlainTextKey(byte[] data)
        {
            if (data.Length < 27) return false;
            return data[0] == (byte)'-' &&
                   data[1] == (byte)'-' &&
                   data[2] == (byte)'-' &&
                   data[3] == (byte)'-' &&
                   data[4] == (byte)'-' &&
                   data[5] == (byte)'B' &&
                   data[6] == (byte)'E' &&
                   data[7] == (byte)'G' &&
                   data[8] == (byte)'I' &&
                   data[9] == (byte)'N' &&
                   data[10] == (byte)' ' &&
                   data[11] == (byte)'R' &&
                   data[12] == (byte)'S' &&
                   data[13] == (byte)'A';
        }

        private static void RestrictKeyFilePermissions(string filePath)
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    var accessControl = fileInfo.GetAccessControl();
                    accessControl.SetAccessRuleProtection(true, false);
                    var currentUser = WindowsIdentity.GetCurrent();
                    if (currentUser.User != null)
                    {
                        var rule = new FileSystemAccessRule(
                            currentUser.User,
                            FileSystemRights.Read,
                            AccessControlType.Allow);
                        accessControl.AddAccessRule(rule);
                    }
                    fileInfo.SetAccessControl(accessControl);
                }
                catch (Exception)
                {
                }
            }
            else
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"600 {filePath}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
                catch (Exception)
                {
                }
            }
        }

        public void InstallAndTrustCaCert()
        {
            var installer = this.certInstallers.FirstOrDefault(item => item.IsSupported());
            if (installer != null)
            {
                installer.Install(this.CaCerFilePath);
            }
            else
            {
                this.logger.LogWarning($"请根据你的系统平台手动安装和信任CA证书{this.CaCerFilePath}");
            }

            GitConfigSslverifyForGitHub();
        }

        public static void GitConfigSslverifyForGitHub()
        {
            var githubUrls = new[]
            {
                "http.https://github.com/.sslverify",
                "http.https://gist.github.com/.sslverify",
                "http.https://api.github.com/.sslverify"
            };

            foreach (var url in githubUrls)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = $"config --global {url} false",
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                }
                catch (Exception)
                {
                }
            }
        }

        public X509Certificate2 GetOrCreateServerCert(string? domain)
        {
            if (this.caCert == null)
            {
                var protectedKey = File.ReadAllBytes(this.CaKeyFilePath);

                string privateKeyPem;
                if (IsPlainTextKey(protectedKey))
                {
                    privateKeyPem = Encoding.ASCII.GetString(protectedKey);
                    var reEncrypted = EncryptKey(protectedKey);
                    File.WriteAllBytes(this.CaKeyFilePath, reEncrypted);
                }
                else
                {
                    privateKeyPem = Encoding.ASCII.GetString(DecryptKey(protectedKey));
                }

                using var rsa = RSA.Create();
                rsa.ImportFromPem(privateKeyPem);
                this.caCert = new X509Certificate2(this.CaCerFilePath).CopyWithPrivateKey(rsa);
            }

            var key = $"{nameof(CertService)}:{domain}";
            var endCert = this.serverCertCache.GetOrCreate(key, GetOrCreateCert);
            return endCert!;

            X509Certificate2 GetOrCreateCert(ICacheEntry entry)
            {
                var notBefore = DateTimeOffset.Now.AddDays(-1);
                var notAfter = DateTimeOffset.Now.AddYears(1);
                entry.SetAbsoluteExpiration(notAfter);

                var extraDomains = GetExtraDomains();

                var subjectName = new X500DistinguishedName($"CN={domain}");
                var endCert = CertGenerator.CreateEndCertificate(this.caCert, subjectName, extraDomains, notBefore, notAfter);

                return new X509Certificate2(endCert.Export(X509ContentType.Pfx));
            }
        }

        private static IEnumerable<string> GetExtraDomains()
        {
            yield return Environment.MachineName;
            yield return IPAddress.Loopback.ToString();
            yield return IPAddress.IPv6Loopback.ToString();
        }
    }
}
