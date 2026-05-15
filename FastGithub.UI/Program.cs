using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace FastGithub.UI
{
    class Program
    {
        private const string MUTEX_NAME = "Global\\FastGithub.UI";
        private const string MAIN_WINDOWS = "MainWindow.xaml";
        private const string FASTGITHUB_PATH = "fastgithub.exe";

        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            using var mutex = new Mutex(true, MUTEX_NAME, out var isFirstInstance);
            if (isFirstInstance == false)
            {
                return;
            }

            StartFastGithub();
            SetWebBrowserDPI();
            SetWebBrowserVersion();

            var app = new Application();
            app.StartupUri = new Uri(MAIN_WINDOWS, UriKind.Relative);
            app.Run();
        }

        private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            if (string.IsNullOrEmpty(args.Name))
            {
                return null;
            }

            var name = new AssemblyName(args.Name).Name;
            if (string.IsNullOrEmpty(name) || name.EndsWith(".resources"))
            {
                return default;
            }

            var resourceInfo = Application.GetResourceStream(new Uri($"Resource/{name}.dll", UriKind.Relative));
            if (resourceInfo == null)
            {
                return null;
            }

            using var stream = resourceInfo.Stream;
            var buffer = new byte[stream.Length];
            stream.ReadExactly(buffer, 0, buffer.Length);
            return Assembly.Load(buffer);
        }

        private static void SetWebBrowserVersion()
        {
            const string subKey = @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION";
            var registryKey = Registry.CurrentUser.OpenSubKey(subKey, true);
            if (registryKey == null)
            {
                registryKey = Registry.CurrentUser.CreateSubKey(subKey);
            }
            var name = $"{Process.GetCurrentProcess().ProcessName}.exe";
            using var webBrowser = new System.Windows.Forms.WebBrowser();
            var version = webBrowser.Version;
            if (version == null)
            {
                return;
            }
            var value = Math.Clamp(int.Parse($"{version.Major}000"), 11000, 90000);
            registryKey.SetValue(name, value, RegistryValueKind.DWord);
        }

        private static void SetWebBrowserDPI()
        {
            const string subKey = @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_96DPI_PIXEL";
            var registryKey = Registry.CurrentUser.OpenSubKey(subKey, true);
            if (registryKey == null)
            {
                registryKey = Registry.CurrentUser.CreateSubKey(subKey);
            }
            var name = $"{Process.GetCurrentProcess().ProcessName}.exe";
            registryKey.SetValue(name, 1, RegistryValueKind.DWord);
        }

        private static void StartFastGithub()
        {
            var potentialPaths = new[]
            {
                FASTGITHUB_PATH,
                Path.Combine("..", "FastGithub", "bin", "Debug", "net10.0", "win-x64", FASTGITHUB_PATH),
                Path.Combine("..", "..", "FastGithub", "bin", "Debug", "net10.0", "win-x64", FASTGITHUB_PATH),
                Path.Combine("FastGithub", FASTGITHUB_PATH)
            };

            string? fastgithubPath = null;
            foreach (var path in potentialPaths)
            {
                if (File.Exists(path))
                {
                    fastgithubPath = path;
                    break;
                }
            }

            if (fastgithubPath == null)
            {
                MessageBox.Show($"无法找到 {FASTGITHUB_PATH}，请确保主程序已构建。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = fastgithubPath,
                Arguments = $"ParentProcessId={Process.GetCurrentProcess().Id} UdpLoggerPort={UdpLogger.Port}",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(fastgithubPath)
            };
            Process.Start(startInfo);
        }
    }
}
