using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace FastGithub.UI
{
    public partial class MainWindow : Window
    {
        private readonly System.Windows.Forms.NotifyIcon notifyIcon;
        private const string FASTGITHUB_UI = "FastGithub.UI";
        private const string SUPPORT_URL = "https://www.17you.com";
        private const string RELEASES_URL = "https://github.com/ah0210/ahFastGithub";

        public MainWindow()
        {
            InitializeComponent();

            var contextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            contextMenuStrip.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            var supportItem = contextMenuStrip.Items.Add("升级支持");
            supportItem.Click += (s, e) =>
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = SUPPORT_URL,
                    UseShellExecute = true
                });
            };
            var exitItem = contextMenuStrip.Items.Add("关闭应用(&C)");
            exitItem.Click += (s, e) => this.Close();

            var version = this.GetType().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            this.Title = $"{FASTGITHUB_UI} v{version}";

            var icon = LoadAppIcon();
            if (icon != null)
            {
                this.Icon = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            this.notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Visible = true,
                Text = FASTGITHUB_UI,
                ContextMenuStrip = contextMenuStrip,
                Icon = icon
            };

            this.notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    this.Show();
                    this.Activate();
                    this.WindowState = WindowState.Normal;
                }
            };
        }


        private static Icon? LoadAppIcon()
        {
            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resource", "githubfast.png");
                if (File.Exists(iconPath))
                {
                    using var bitmap = new Bitmap(iconPath);
                    var size = new System.Drawing.Size(48, 48);
                    using var scaled = new Bitmap(bitmap, size);
                    return System.Drawing.Icon.FromHandle(scaled.GetHicon());
                }
            }
            catch
            {
            }
            return null;
        }

        /// <summary>
        /// 拦截最小化事件
        /// </summary>
        /// <param name="e"></param>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            hwndSource.AddHook(WndProc);

            IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
            {
                const int WM_SYSCOMMAND = 0x112;
                const int SC_MINIMIZE = 0xf020;
                const int SC_CLOSE = 0xf060;

                if (msg == WM_SYSCOMMAND)
                {
                    if (wParam.ToInt32() == SC_MINIMIZE || wParam.ToInt32() == SC_CLOSE)
                    {
                        this.Hide();
                        handled = true;
                    }
                }
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// 关闭时
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosed(EventArgs e)
        {
            this.notifyIcon.Icon = null;
            this.notifyIcon.Dispose();
            base.OnClosed(e);
        }
    }
}
