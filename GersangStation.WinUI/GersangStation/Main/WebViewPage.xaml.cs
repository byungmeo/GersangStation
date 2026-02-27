using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace GersangStation.Main
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WebViewPage : Page, IDisposable
    {
        bool _initialized = false;
        private WebViewManager? _webviewManager;

        public WebViewPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (_initialized)
                return;
            _initialized = true;

            if (e.Parameter is MainWindow window)
            {
                _webviewManager = new WebViewManager(webview: WebView, window) ?? throw new NullReferenceException();
                window.RegisterWebViewManager(_webviewManager);
            }
        }

        public void Dispose()
        {
            _webviewManager?.Dispose();
            _webviewManager = null;
        }

        private void Button_WebPreview_Back_Click(object sender, RoutedEventArgs e)
        {
            if (_webviewManager is not null && _webviewManager.CanGoBack)
            {
                _webviewManager.GoBack();
            }
        }

        private void Button_WebPreview_Forward_Click(object sender, RoutedEventArgs e)
        {
            if (_webviewManager is not null && _webviewManager.CanGoForward)
            {
                _webviewManager.GoForward();
            }
        }

        private void Button_WebPreview_Refresh_Click(object sender, RoutedEventArgs e)
        {
            _webviewManager?.Refresh();
        }

        private void Button_WebPreview_Home_Click(object sender, RoutedEventArgs e)
        {
            _webviewManager?.GoHome();
        }
    }
}
