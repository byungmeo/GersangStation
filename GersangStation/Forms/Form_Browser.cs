using GersangStation.Modules;
using Microsoft.Web.WebView2.WinForms;
using System.Text;

namespace GersangStation; 
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
        webView.WebMessageReceived += WebView_WebMessageReceived;

        string id = await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.chrome.webview.postMessage(window.document.URL);");
        //string id2 = await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.chrome.webview.addEventListener(\'message\', event => alert(event.data));");

        this.FormClosing += (sender, e) => {
            webView.CoreWebView2.RemoveScriptToExecuteOnDocumentCreated(id);
            webView.WebMessageReceived -= WebView_WebMessageReceived;
            //webView.CoreWebView2.RemoveScriptToExecuteOnDocumentCreated(id2);
        };
    }

    private void WebView_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e) {
        String uri = e.TryGetWebMessageAsString();
        addressBar.Text = uri;
        webView.CoreWebView2.PostWebMessageAsString(uri);
    }

    private void Form_Browser_Resize(object sender, EventArgs e) {
        webView.Size = new Size(webView.Width, this.ClientSize.Height - (addressBar.Height + addressBar.Location.Y));
        int diff = (ClientSize.Width - button_saveShortcut.Width - 6) - button_saveShortcut.Left;
        button_saveShortcut.Left = ClientSize.Width - button_saveShortcut.Width - 6;
        goButton.Left += diff;
        addressBar.Width += diff;
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
        string[] slot_title = new string[titles.Length];
        string[] slot_link = new string[4];
        for(int i = 0; i < titles.Length; i++) slot_title[i] = titles[i];
        for(int i = 1; i <= 4; i++) { slot_link[i-1] = ConfigManager.getConfig("shortcut_" + i); }

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

        Button[] button_slot = new Button[4];
        button_slot[0] = new Button() {
            Text = "1번슬롯에 저장",
            AutoSize = true,
            Location = new Point(label_title.Location.X, textBox_title.Location.Y + 40)
        };
        for(int i = 1; i < 4; i++) {
            button_slot[i] = new Button() {
                Text = (i+1) + "번슬롯에 저장",
                AutoSize = true,
                Location = new Point(button_slot[i-1].Location.X, button_slot[i-1].Location.Y + 28)
            };
        }

        Label[] label_current = new Label[4];
        label_current[0] = new Label() {
            AutoSize = true,
            Location = new Point(button_slot[0].Location.X + 104, button_slot[0].Location.Y),
            Text = (slot_link[0] == "") ? "지정하지않음" : "현재 : " + slot_title[0],
            Tag = slot_title[0]
        };
        for(int i = 1; i < 4; i++) {
            label_current[i] = new Label() {
                AutoSize = true,
                Location = new Point(button_slot[i].Location.X + 104, button_slot[i].Location.Y),
                Text = (slot_link[i] == "") ? "지정하지않음" : "현재 : " + slot_title[i],
                Tag = slot_title[i]
            };
        }

        Action<string, Label> save = (key, label) => {
            if (textBox_title.Text == "") {
                MessageBox.Show("제목을 입력해주세요.");
            } else {
                label.Tag = textBox_title.Text;
                string link = addressBar.Text;
                ConfigManager.setConfig(key, link);
                StringBuilder sb = new StringBuilder();
                foreach (Label l in label_current) sb.Append(l.Tag.ToString() + ';');
                sb.Remove(sb.Length - 1, 1);
                ConfigManager.setConfig("shortcut_name", sb.ToString());
                this.Close();
            }
        };

        button_slot[0].Click += (sender, e) => {
            label_current[0].Tag = label_title.Text;
            save("shortcut_1", label_current[0]);
        };
        button_slot[1].Click += (sender, e) => {
            label_current[1].Tag = label_title.Text;
            save("shortcut_2", label_current[1]);
        };
        button_slot[2].Click += (sender, e) => {
            label_current[2].Tag = label_title.Text;
            save("shortcut_3", label_current[2]);
        };
        button_slot[3].Click += (sender, e) => {
            label_current[3].Tag = label_title.Text;
            save("shortcut_4", label_current[3]);
        };

        form_saveShortcut.Controls.Add(label_link);
        form_saveShortcut.Controls.Add(label_title);
        form_saveShortcut.Controls.Add(textBox_title);
        foreach(Button button in button_slot) form_saveShortcut.Controls.Add(button);
        foreach(Label label in label_current) form_saveShortcut.Controls.Add(label);
        form_saveShortcut.Size = new Size(form_saveShortcut.Size.Width, button_slot[button_slot.Length-1].Location.Y + 74);
        form_saveShortcut.ShowDialog(this);
    }
}
