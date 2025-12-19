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

        /// <summary>
        /// 程序集加载失败时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private static Assembly? OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name;
            if (name.EndsWith(".resources"))
            {
                return default;
            }

            var stream = Application.GetResourceStream(new Uri($"Resource/{name}.dll", UriKind.Relative)).Stream;
            var buffer = new byte[stream.Length];
            stream.Read(buffer, 0, buffer.Length);
            return Assembly.Load(buffer);
        }

        /// <summary>
        /// 设置浏览器版本
        /// </summary>
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
            var value = int.Parse($"{webBrowser.Version.Major}000");
            registryKey.SetValue(name, value, RegistryValueKind.DWord);
        }

        /// <summary>
        /// 设置浏览器DPI
        /// </summary>
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

        /// <summary>
        /// 启动fastgithub
        /// </summary>
        /// <returns></returns>
        private static void StartFastGithub()
        {
            // 尝试在不同位置查找fastgithub.exe
            var potentialPaths = new[]
            {
                FASTGITHUB_PATH, // 当前目录
                Path.Combine("..", "FastGithub", "bin", "Debug", "net10.0", "win-x64", FASTGITHUB_PATH), // 调试目录
                Path.Combine("..", "..", "FastGithub", "bin", "Debug", "net10.0", "win-x64", FASTGITHUB_PATH), // 发布目录
                Path.Combine("FastGithub", FASTGITHUB_PATH) // 同级目录
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
                // 如果找不到，尝试构建主程序
                try
                {
                    var buildStartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = @"build ..\FastGithub -c Debug -r win-x64",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(buildStartInfo)?.WaitForExit();

                    // 再次尝试查找
                    foreach (var path in potentialPaths)
                    {
                        if (File.Exists(path))
                        {
                            fastgithubPath = path;
                            break;
                        }
                    }
                }
                catch (Exception)
                {
                    // 忽略构建异常
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
                WorkingDirectory = Path.GetDirectoryName(fastgithubPath) // 设置工作目录为主程序所在目录
            };
            Process.Start(startInfo);
        }
    }
}
