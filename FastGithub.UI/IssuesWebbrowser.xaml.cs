using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace FastGithub.UI
{
    /// <summary>
    /// IssuesWebbrowser.xaml 的交互逻辑
    /// </summary>
    public partial class IssuesWebbrowser : UserControl
    {
        public IssuesWebbrowser()
        {
            InitializeComponent();

            this.NavigateIssueHtml();
            this.webBrowser.AddHandler(KeyDownEvent, new RoutedEventHandler(WebBrowser_KeyDown), true);
            this.webBrowser.Navigating += WebBrowser_Navigating;
        }

        /// <summary>
        /// 拦截F5
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WebBrowser_KeyDown(object sender, RoutedEventArgs e)
        {
            var @event = (KeyEventArgs)e;
            if (@event.Key == Key.F5)
            {
                this.NavigateIssueHtml();
            }
        }

        private void NavigateIssueHtml()
        {
            try
            {
                var resource = Application.GetResourceStream(new Uri("Resource/issue.html", UriKind.Relative));
                this.webBrowser.NavigateToStream(resource.Stream);
            }
            catch (Exception)
            {
            }
        }

        private void WebBrowser_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            var uri = e.Uri;
            if (uri == null)
            {
                return;
            }

            if (uri.IsAbsoluteUri && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Cancel = true;
            }
        }
    }
}
