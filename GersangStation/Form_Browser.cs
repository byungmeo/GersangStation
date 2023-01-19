using MaterialSkin.Controls;
using Microsoft.Web.WebView2.WinForms;
using System.Text;

namespace GersangStation {
    public partial class Form_Browser : Form {
        WebView2 webView;

        public Form_Browser(WebView2 webView) {
            this.webView = webView; //Win10에서 InitializeComponent 뒤에 넣으면 Resize에서 NullException 발생

            InitializeComponent();
            
            this.webView.Dock = DockStyle.Bottom;
            this.webView.Size = new Size(webView.Width, this.ClientSize.Height - (addressBar.Height + addressBar.Location.Y * 2));
            this.addressBar.Text = this.webView.Source.ToString();
            Controls.Add(webView);

            InitializeAsync();
        }
        async void InitializeAsync() {       
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

        private void Form_Browser_Resize(object sender, EventArgs e) {
            try {
                webView.Size = new Size(webView.Width, this.ClientSize.Height - (addressBar.Height + addressBar.Location.Y));
                
            } catch (NullReferenceException) {
            }

            goButton.Left = this.ClientSize.Width - goButton.Width;
            addressBar.Width = goButton.Left - addressBar.Left;
        }

        private void goButton_Click(object sender, EventArgs e) {
            if (webView != null && webView.CoreWebView2 != null) {
                try {
                    if (!addressBar.Text.Contains("https://www") && !addressBar.Text.Contains("http://www")) {
                        addressBar.Text = "https://www." + addressBar.Text;
                    }
                    webView.CoreWebView2.Navigate(addressBar.Text);
                } catch (Exception ex) {
                    webView.CoreWebView2.ExecuteScriptAsync($"alert('{ex.Message}')");
                }
            }
        }

        private void addressBar_KeyDown(object sender, KeyEventArgs e) {
            if(e.KeyCode== Keys.Enter) { goButton.PerformClick(); }
        }

        private void button_saveShortcut_Click(object sender, EventArgs e) {
            string[] titles = ConfigManager.getConfig("shortcut_name").Split(';');
            string slot1_title = titles[0];
            string slot2_title = titles[1];
            string slot3_title = titles[2];
            string slot4_title = titles[3];
            string slot1_link = ConfigManager.getConfig("shortcut_1");
            string slot2_link = ConfigManager.getConfig("shortcut_2");
            string slot3_link = ConfigManager.getConfig("shortcut_3");
            string slot4_link = ConfigManager.getConfig("shortcut_4");

            Form form_saveShortcut = new Form() {
                TopMost = true,
                StartPosition = FormStartPosition.CenterParent
            };
            Label label_link = new Label() {
                Text = "링크 : " + addressBar.Text,
                AutoSize = true,
                Location = new Point(12, 9)
            };
            Label label_title = new Label() {
                Text = "바로가기 제목",
                AutoSize = true,
                Location = new Point(label_link.Location.X, label_link.Location.Y + 28)
            };
            TextBox textBox_title = new TextBox() {
                MaxLength = 6,
                Location = new Point(label_title.Location.X + 87, label_title.Location.Y)
            };
            Button button_slot1 = new Button() {
                Text = "1번슬롯에 저장",
                AutoSize = true,
                Location = new Point(label_title.Location.X, textBox_title.Location.Y + 40)
            };
            Button button_slot2 = new Button() {
                Text = "2번슬롯에 저장",
                AutoSize = true,
                Location = new Point(button_slot1.Location.X, button_slot1.Location.Y + 28)
            };
            Button button_slot3 = new Button() {
                Text = "3번슬롯에 저장",
                AutoSize = true,
                Location = new Point(button_slot2.Location.X, button_slot2.Location.Y + 28)
            };
            Button button_slot4 = new Button() {
                Text = "4번슬롯에 저장",
                AutoSize = true,
                Location = new Point(button_slot3.Location.X, button_slot3.Location.Y + 28)
            };

            Action<string, string> save = (key, link) => {
                if (textBox_title.Text == "") {
                    MessageBox.Show("제목을 입력해주세요.");
                }
                else {
                    slot1_title = textBox_title.Text;
                    slot1_link = addressBar.Text;
                    ConfigManager.setConfig(key, link);
                    StringBuilder sb = new StringBuilder();
                    sb.Append(slot1_title + ';');
                    sb.Append(slot2_title + ';');
                    sb.Append(slot3_title + ';');
                    sb.Append(slot4_title);
                    ConfigManager.setConfig("shortcut_name", sb.ToString());
                    this.Close();
                }
            };

            button_slot1.Click += (sender, e) => save("shortcut_1", slot1_link);
            button_slot2.Click += (sender, e) => save("shortcut_2", slot2_link);
            button_slot3.Click += (sender, e) => save("shortcut_3", slot3_link);
            button_slot4.Click += (sender, e) => save("shortcut_4", slot4_link);

            Label label_current1 = new Label() {
                AutoSize = true,
                Location = new Point(button_slot1.Location.X + 104, button_slot1.Location.Y),
                Text = (slot1_link == "") ? "지정하지않음" : "현재 : " + slot1_title
            };
            Label label_current2 = new Label() {
                AutoSize = true,
                Location = new Point(button_slot2.Location.X + 104, button_slot2.Location.Y),
                Text = (slot2_link == "") ? "지정하지않음" : "현재 : " + slot2_title
            };
            Label label_current3 = new Label() {
                AutoSize = true,
                Location = new Point(button_slot3.Location.X + 104, button_slot3.Location.Y),
                Text = (slot3_link == "") ? "지정하지않음" : "현재 : " + slot3_title
            };
            Label label_current4 = new Label() {
                AutoSize = true,
                Location = new Point(button_slot4.Location.X + 104, button_slot4.Location.Y),
                Text = (slot4_link == "") ? "지정하지않음" : "현재 : " + slot4_title
            };

            form_saveShortcut.Controls.Add(label_link);
            form_saveShortcut.Controls.Add(label_title);
            form_saveShortcut.Controls.Add(textBox_title);
            form_saveShortcut.Controls.Add(button_slot1);
            form_saveShortcut.Controls.Add(button_slot2);
            form_saveShortcut.Controls.Add(button_slot3);
            form_saveShortcut.Controls.Add(button_slot4);
            form_saveShortcut.Controls.Add(label_current1);
            form_saveShortcut.Controls.Add(label_current2);
            form_saveShortcut.Controls.Add(label_current3);
            form_saveShortcut.Controls.Add(label_current4);
            form_saveShortcut.Size = new Size(form_saveShortcut.Size.Width, button_slot4.Location.Y + 74);
            form_saveShortcut.ShowDialog(this);
        }
    }
}
