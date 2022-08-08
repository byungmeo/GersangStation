using MaterialSkin.Controls;
using Microsoft.Web.WebView2.WinForms;

namespace GersangStation {
    public partial class Form_Browser : Form {
        WebView2 webView = null;
        public Form_Browser(WebView2 webView) {
            InitializeComponent();

            this.webView = webView;
            this.webView.Dock = DockStyle.Bottom;
            this.webView.Size = new Size(webView.Width, this.ClientSize.Height - (addressBar.Height + addressBar.Location.Y * 2));
            this.addressBar.Text = this.webView.Source.ToString();
            Controls.Add(webView);

            InitializeAsync();
        }
        async void InitializeAsync() {
            webView.NavigationStarting += WebView_NavigationStarting;
            
            await webView.EnsureCoreWebView2Async(null);

            webView.WebMessageReceived += WebView_WebMessageReceived;

            string id = await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.chrome.webview.postMessage(window.document.URL);");
            //string id2 = await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.chrome.webview.addEventListener(\'message\', event => alert(event.data));");

            this.FormClosing += (sender, e) => {
                webView.CoreWebView2.RemoveScriptToExecuteOnDocumentCreated(id);
                //webView.CoreWebView2.RemoveScriptToExecuteOnDocumentCreated(id2);
            };
        }

        private void WebView_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e) {
            String uri = e.TryGetWebMessageAsString();
            addressBar.Text = uri;
            webView.CoreWebView2.PostWebMessageAsString(uri);
        }

        private void WebView_NavigationStarting(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e) {
            String uri = e.Uri;
            if (!uri.StartsWith("https://") && !uri.StartsWith("http://")) {
                webView.CoreWebView2.ExecuteScriptAsync($"alert('올바른 경로를 입력해주세요.\n(http:// 또는 https://로 시작해야 합니다.')");
                e.Cancel = true;
            }
        }

        private void Form_Browser_Resize(object sender, EventArgs e) {
            webView.Size = new Size(webView.Width, this.ClientSize.Height - (addressBar.Height + addressBar.Location.Y));
            goButton.Left = this.ClientSize.Width - goButton.Width;
            addressBar.Width = goButton.Left - addressBar.Left;
        }

        private void goButton_Click(object sender, EventArgs e) {
            if (webView != null && webView.CoreWebView2 != null) {
                webView.CoreWebView2.Navigate(addressBar.Text);
            }
        }
    }
}
