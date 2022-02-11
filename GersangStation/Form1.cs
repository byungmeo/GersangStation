using MaterialSkin;
using MaterialSkin.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace GersangStation {
    public partial class Form1 : MaterialForm {
        private const int WM_ACTIVATEAPP = 0x001C;

        protected override void WndProc(ref Message m) {
            base.WndProc(ref m);
            if (WindowState == FormWindowState.Minimized && m.Msg == WM_ACTIVATEAPP && m.WParam == IntPtr.Zero) {
                BringToFront();
            }
        }

        public enum State {
            LoggedIn, LoginOther, None
        }

        public enum Client {
            None = 0,
            Client1 = 1,
            Client2 = 2,
            Client3 = 3
        }

        private State currentState = State.None;
        private Client currentClient = Client.None;

        private const string url_main = "http://www.gersang.co.kr/main/index.gs?";
        private const string url_logout = "http://www.gersang.co.kr/member/logoutProc.gs";
        private const string url_installStarter = "http://akgersang.xdn.kinxcdn.com//PatchFile/Gersang_Web/GersangStarterSetup.exe";
        private const string url_search = "https://search.naver.com/search.naver?&query=�Ż�";
        private const string url_search_gersang = "http://www.gersang.co.kr/main.gs";

        private bool isWebFunctionDeactivated = false;
        private bool isSearch = false;
        private bool isGameStartLogin = false;
        private string previousUrl = "";

        WebView2? webView_main = null;

        public Form1() {
            InitializeComponent();

            // Initialize MaterialSkinManager
            var materialSkinManager = MaterialSkinManager.Instance;

            // Set this to false to disable backcolor enforcing on non-materialSkin components
            // This HAS to be set before the AddFormToManage()
            materialSkinManager.EnforceBackcolorOnAllComponents = false;

            // MaterialSkinManager properties
            materialSkinManager.AddFormToManage(this);
            materialSkinManager.Theme = MaterialSkinManager.Themes.LIGHT;
            materialSkinManager.ColorScheme = new ColorScheme(Primary.BlueGrey800, Primary.BlueGrey900, Primary.BlueGrey500, Accent.LightBlue200, TextShade.WHITE);
        }

        private async void Form1_Load(object sender, EventArgs e) {
            webView_main = new WebView2() {
                Visible = true,
                Dock = DockStyle.Fill,
                Source = new Uri("https://www.gersang.co.kr/main/index.gs")
            };

            try {
                //webView_main.CoreWebView2InitializationCompleted += WebView_main_CoreWebView2InitializationCompleted;
                await webView_main.EnsureCoreWebView2Async(null); //������ CoreWebView2InitializationCompleted �����ʸ� ���� �� �����ؾ� ��.
                webView_main.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
                webView_main.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false; //Alert ���� �޽���â�� �����ʰ� ScriptDialogOpening �̺�Ʈ�� ���� ������ �� �ֵ��� �մϴ�.
                webView_main.CoreWebView2.ScriptDialogOpening += CoreWebView2_ScriptDialogOpening;
            } catch (WebView2RuntimeNotFoundException ex) {
                Trace.WriteLine(ex.StackTrace);
                DialogResult dr = MessageBox.Show("��Ŭ�� �����̼��� �̿��ϱ� ���ؼ�\nWebView2 ��Ÿ���� �ݵ�� ��ġ�ϼž� �մϴ�.\n��ġ �Ͻðڽ��ϱ�? (��ġ ��ũ�� �ڵ����� �����մϴ�.)", "��Ÿ�� ��ġ �ʿ�", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (dr == DialogResult.Yes) {
                    Process.Start(new ProcessStartInfo("https://go.microsoft.com/fwlink/p/?LinkId=2124703") { UseShellExecute = true });
                }
                Application.Exit();
                return;
            }

            webView_main.NavigationStarting += webView_main_NavigationStarting;
            webView_main.NavigationCompleted += webView_main_NavigationCompleted;

            webView_main.Source = new Uri("https://www.gersang.co.kr/main/index.gs");
            LoadRadioButton();
            LoadAccountComboBox();
            LoadShortcut();
        }

        private void CoreWebView2_NewWindowRequested(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NewWindowRequestedEventArgs e) {
            if (sender != null) e.NewWindow = (CoreWebView2)sender;
            //e.Handled = true;
        }

        private void CoreWebView2_ScriptDialogOpening(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2ScriptDialogOpeningEventArgs e) {
            string message = e.Message;
            Trace.WriteLine(message);

            this.BeginInvoke(async () => {
                //message�� ��Ȯ�� "5�� �Ŀ� ��α��� �����մϴ�." �� ���, ����ڰ� �α��� ���� �� 5�� �̳��� �α����� �õ��� ����Դϴ�.
                if (message.Equals("5�� �Ŀ� ��α��� �����մϴ�.")) {
                    Trace.WriteLine("�α��� ���� �� 5�� �ȿ� �α��� �õ� ����");
                    MessageBox.Show("���� 5�ʰ� ������ �ʾҽ��ϴ�. 5�� �Ŀ� �ٽ� �α����� �õ����ּ���.");
                    currentState = State.None;
                    currentClient = Client.None;
                }

                //otp ������ȣ�� Ʋ�� �� �ٽ� �Է��ϵ��� �մϴ�.
                else if (message.Contains("������ȣ�� �ٸ��ϴ�")) {
                    Trace.WriteLine("�߸��� OTP �ڵ� �Է� ����");
                    MessageBox.Show("�߸��� OTP �ڵ带 �Է��Ͽ����ϴ�. �ٽ� �Է����ּ���.");
                    string? otpCode = showDialogOtp();

                    Trace.WriteLine("otpCode : " + otpCode);
                    if (otpCode == null) {
                        MessageBox.Show("OTP �ڵ带 �Է����� �ʾҽ��ϴ�.");
                    } else {
                        await webView_main.ExecuteScriptAsync("document.getElementById('GSotpNo').value = '" + otpCode + "'");
                        await webView_main.ExecuteScriptAsync("document.getElementById('btn_Send').click()");
                    }
                }

                //�˻� ���� ���� �޽���
                else if (message.Contains("�������� ���޵Ǿ����ϴ�.") || message.Contains("�̹� �������� �����ϼ̽��ϴ�.")) {
                    if (MessageBox.Show(message) == DialogResult.OK) { 
                        webView_main.CoreWebView2.Navigate(url_main); 
                    }
                }

                //���̵� �Ǵ� ��й�ȣ�� Ʋ�� ����Դϴ�.
                else if (message.Contains("���̵� �Ǵ� ��й�ȣ ����")) {
                    currentState = State.None;
                    currentClient = Client.None;
                    Trace.WriteLine("�α��� ���� ����");
                    MessageBox.Show("�α��ο� �����Ͽ����ϴ�. 5���Ŀ� �ٽ� �α��� ���ּ���.");
                } 
                
                else {
                    Trace.WriteLine("���ܷ� ó������ ���� �޽��� ����");
                    MessageBox.Show(message);
                }
            });
        }
        private void webView_main_NavigationStarting(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e) {
            Trace.WriteLine("NavigationStarting : " + e.Uri.ToString());
            Trace.WriteLine("NavigationStarting Previous URL : " + webView_main.Source);
            previousUrl = webView_main.CoreWebView2.Source;
            deactivateWebSideFunction();
        }

        private void webView_main_NavigationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e) {
            /*
                1. ����ġ�� �α��� �ϴ� ���
                2. ����ġ�� �α׾ƿ� �ϴ� ���
                3. ����ġ�� �α׾ƿ� -> �α��� �ϴ� ��� (2)
                4. (�α��� �� ���¿���) �˻���ư���� �˻����� ����
                5. (�α��� �� ���¿���) �����ư���� ����
            */

            if (sender == null) {
                this.BeginInvoke(() => { MessageBox.Show("NavigationFailed : sender is NULL"); });
                return;
            }

            if (!e.IsSuccess) {
                deactivateWebSideFunction(); //WebView2 Ȱ�� ��� ��Ȱ��ȭ ó��
                this.BeginInvoke(() => { handleWebError(e.WebErrorStatus); });
                return;
            } else { if (isWebFunctionDeactivated) { activateWebSideFunction(); } } //WebView2 Ȱ�� ����� ��Ȱ��ȭ ������ ��� Ȱ��ȭ ó��

            string? url = ((WebView2)sender).Source.ToString();
            Trace.WriteLine("NavigationCompleted : " + url);

            if (url.Contains("pw_reset.gs")) {
                doPwReset();
                return;
            }

            if (url.Contains("otp.gs")) {
                this.BeginInvoke(() => {
                    string? otpCode = showDialogOtp();

                    Trace.WriteLine("otpCode : " + otpCode);
                    if (otpCode == null) {
                        MessageBox.Show("OTP �ڵ带 �Է����� �ʾҽ��ϴ�.");
                    } else {
                        doOtpInput(otpCode);
                    }
                });
                return;
            }

            if (url.Contains("search.naver")) {
                doNavigateGersangSite();
                return;
            }

            if (url.Contains("attendance")) {
                if (isSearch) { doGetEventItem(); }
                //else { webView_main.CoreWebView2.Navigate(url_main); }
                return;
            }

            if (url.Contains("main/index.gs")) {
                if(isSearch && previousUrl.Contains("search.naver")) {
                    doNavigateAttendancePage();
                    return;
                }

                if(currentState == State.LoginOther) { doLoginOther(); } 
                else if (currentState == State.LoggedIn) { doCheckLogin(); }
                return;
            }
        }

        private async void doGetEventItem() {
            isSearch = false;

            /* �˻����� ���� �ð��뺰 ���� ��
            1-> 00:05 ~05:55
            2-> 06:05 ~11:55
            3-> 12:05 ~17:55
            4-> 18:05 ~23:55
            */
            int arg; //event_Search_Use ��ũ��Ʈ ���� ����

            int koreaHour = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Korea Standard Time").Hour;
            if (koreaHour >= 0 && koreaHour <= 5) {
                arg = 1;
            } else if (koreaHour >= 6 && koreaHour <= 11) {
                arg = 2;
            } else if (koreaHour >= 12 && koreaHour <= 17) {
                arg = 3;
            } else {
                arg = 4;
            }

            await webView_main.ExecuteScriptAsync("event_Search_Use(" + arg + ");");
        }

        private async void doNavigateAttendancePage() {
            await webView_main.ExecuteScriptAsync(@"document.querySelector(""a[href *= '" + "attendance" + @"']"").click();");
        }

        private async void doNavigateGersangSite() {
            await webView_main.ExecuteScriptAsync(@"document.querySelector(""a[href = '" + url_search_gersang + @"']"").click();");
        }

        private void deactivateWebSideFunction() {
            //materialSwitch_login_1.Enabled = false;
            //materialSwitch_login_2.Enabled = false;
            //materialSwitch_login_3.Enabled = false;
            materialButton_naver_1.Enabled = false;
            materialButton_naver_2.Enabled = false;
            materialButton_naver_3.Enabled = false;
            materialButton_start_1.Enabled = false;
            materialButton_start_2.Enabled = false;
            materialButton_start_3.Enabled = false;
            materialButton_shortcut_1.Enabled = false;
            materialButton_shortcut_2.Enabled = false;
            materialButton_shortcut_3.Enabled = false;
            materialButton_shortcut_4.Enabled = false;
            Trace.WriteLine("���� ���� ��ɵ��� ��Ȱ��ȭ �Ǿ����ϴ�.");
            isWebFunctionDeactivated = true;
        }

        private void activateWebSideFunction() {
            //materialSwitch_login_1.Enabled = true;
            //materialSwitch_login_2.Enabled = true;
            //materialSwitch_login_3.Enabled = true;
            materialButton_naver_1.Enabled = true;
            materialButton_naver_2.Enabled = true;
            materialButton_naver_3.Enabled = true;
            materialButton_start_1.Enabled = true;
            materialButton_start_2.Enabled = true;
            materialButton_start_3.Enabled = true;
            materialButton_shortcut_1.Enabled = true;
            materialButton_shortcut_2.Enabled = true;
            materialButton_shortcut_3.Enabled = true;
            materialButton_shortcut_4.Enabled = true;
            Trace.WriteLine("���� ���� ��ɵ��� �ٽ� Ȱ��ȭ �Ǿ����ϴ�.");
            isWebFunctionDeactivated = false;
        }

        private void handleWebError(CoreWebView2WebErrorStatus webErrorStatus) {
            Trace.WriteLine("NavigationFailed - WebErrorStatus : " + webErrorStatus);
            Trace.WriteLine("NavigationFailed - DocumentTitle : " + webView_main.CoreWebView2.DocumentTitle);

            //�۾��� ��ҵǾ����� ��Ÿ���ϴ�. (MS DOC)
            if (webErrorStatus == CoreWebView2WebErrorStatus.OperationCanceled) { return; }

            //������ �����Ǿ����� ��Ÿ���ϴ�. (MS DOC)
            if (webErrorStatus == CoreWebView2WebErrorStatus.ConnectionAborted) { return; }

            this.BeginInvoke(() => {
                //�� �� ���� ������ �߻������� ��Ÿ���ϴ�. (MS DOC)
                if (webErrorStatus == CoreWebView2WebErrorStatus.Unknown) {
                    string title = webView_main.CoreWebView2.DocumentTitle;
                    if (title != null) {
                        if (title.Contains("����")) {
                            MessageBox.Show("���� �Ż� Ȩ�������� ���� ���Դϴ�.\n������ ���� �Ŀ� �ֿ� ��� �̿��� �����մϴ�.");
                            return;
                        }
                        MessageBox.Show("�� �� ���� ������ ���� �Ż� Ȩ������ ���ӿ� �����Ͽ����ϴ�.\nDocumentTitle : " + webView_main.CoreWebView2.DocumentTitle);
                    } else {
                        MessageBox.Show("�� �� ���� ������ ���� �Ż� Ȩ������ ���ӿ� �����Ͽ����ϴ�.\nDocumentTitle : NULL");
                    }
                    return;
                }

                //���ͳ� ������ ���������� ��Ÿ���ϴ�. (MS DOC)
                if (webErrorStatus == CoreWebView2WebErrorStatus.Disconnected) {
                    MessageBox.Show("�Ż� Ȩ������ ���ӿ� �����Ͽ����ϴ�.\n���ͳ� ������ Ȯ�� ���ּ���.");
                    return;
                }

                MessageBox.Show("�Ż� Ȩ������ ���ӿ� �����Ͽ����ϴ�.\n���� ���� : " + webErrorStatus + "\n���� �ٶ��ϴ�.");
                return;
            });
        }

        private async void doCheckLogin() {
            var logout_btn = await webView_main.ExecuteScriptAsync(@"document.querySelector(""a[href = '" + "/member/logoutProc.gs" + @"']"")");
            if (logout_btn != null) {
                Trace.WriteLine("üũ�Ͽ����� �α����� �Ǿ�����.");
                switch (currentClient) {
                    case Client.Client1:
                        materialSwitch_login_1.CheckState = CheckState.Checked;
                        break;
                    case Client.Client2:
                        materialSwitch_login_2.CheckState = CheckState.Checked;
                        break;
                    case Client.Client3:
                        materialSwitch_login_3.CheckState = CheckState.Checked;
                        break;
                    default:
                        break;
                }
            } else {
                Trace.WriteLine("üũ�Ͽ����� �α����� �ȵǾ��ִ� ����.");
                currentState = State.None;
                materialSwitch_login_1.CheckState = CheckState.Unchecked;
                materialSwitch_login_2.CheckState = CheckState.Unchecked;
                materialSwitch_login_3.CheckState = CheckState.Unchecked;
            }

            if (true == isSearch) { webView_main.CoreWebView2.Navigate(url_search); } //���̹� �˻�
            else if (true == isGameStartLogin) { GameStart(); }
        }

        private async void doLoginOther() {
            var button_login = await webView_main.ExecuteScriptAsync("document.getElementById('btn_Login')");
            var textBox_id = await webView_main.ExecuteScriptAsync("document.getElementById('GSuserID')");
            var textBox_pw = await webView_main.ExecuteScriptAsync("document.getElementById('GSuserPW')");

            string? tag = null;
            string[] account;
            switch (currentClient) {
                case Client.Client1:
                    tag = materialSwitch_login_1.Tag.ToString();
                    break;
                case Client.Client2:
                    tag = materialSwitch_login_2.Tag.ToString();
                    break;
                case Client.Client3:
                    tag = materialSwitch_login_3.Tag.ToString();
                    break;
                default:
                    break;
            }

            if (tag != null) { account = tag.Split(';'); } else { return; }

            if (button_login != null && textBox_id != null && textBox_pw != null) {
                Login(account[0], account[1]);
            }
        }

        private async void doOtpInput(string otpCode) {
            await webView_main.ExecuteScriptAsync("document.getElementById('GSotpNo').value = '" + otpCode + "'");
            await webView_main.ExecuteScriptAsync("document.getElementById('btn_Send').click()");
        }

        private async void doPwReset() {
            await webView_main.ExecuteScriptAsync(@"document.querySelector(""a[href = '" + url_main + @"']"").click()");
        }

        private string? showDialogOtp() {
            Form backgroundForm = new Form() {
                StartPosition = FormStartPosition.Manual,
                FormBorderStyle = FormBorderStyle.None,
                Opacity = .50d,
                BackColor = Color.Black,
                Location = this.Location,
                Size = this.Size,
                ShowInTaskbar = false,
                Owner = this
            };
            backgroundForm.Show();

            MaterialForm dialog_otp = new MaterialForm() {
                FormStyle = FormStyles.ActionBar_None,
                Sizable = false,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(200, 150),
                Text = "OTP",
                MaximizeBox = false,
                MinimizeBox = false,
                TopMost = true,
                ShowInTaskbar = false,
                Owner = this
            };

            MaterialTextBox2 textBox_otp = new MaterialTextBox2() {
                Hint = "OTP �ڵ� �Է�(8��)",
                UseAccent = false,
                MaxLength = 8,
                Size = new Size(170, 50),
                Location = new Point(15, 40),
            };
            dialog_otp.Controls.Add(textBox_otp);

            MaterialButton button_confirm = new MaterialButton() {
                Text = "Ȯ��",
                AutoSize = false,
                Size = new Size(64, 36),
                Location = new Point(68, 100),
            };
            button_confirm.Click += (sender, e) => {
                if(textBox_otp.Text.Length != 8) {
                    MessageBox.Show("OTP �ڵ�� 8�ڸ� �Դϴ�.\n�ٽ� �Է����ּ���.");
                    return;
                }

                if(!Regex.IsMatch(textBox_otp.Text, @"^[0-9]+$")) {
                    MessageBox.Show("OTP �ڵ�� ���ڷθ� �̷������ �մϴ�.\n�ٽ� �Է����ּ���.");
                    return;
                }

                dialog_otp.DialogResult = DialogResult.OK;
            };

            dialog_otp.Controls.Add(button_confirm);
            dialog_otp.AcceptButton = button_confirm; //���� ��ư�� ������ �� ��ư�� Ŭ���մϴ�.

            if (dialog_otp.ShowDialog() == DialogResult.OK) {
                backgroundForm.Dispose();
                return textBox_otp.Text;
            } else {
                backgroundForm.Dispose();
                return null;
            }
        }

        private void materialButton_start_Click(object sender, EventArgs e) {
            /*
            if (currentState == State.None) {
                MessageBox.Show("�α����� �� �� ������ ���� ���ּ���.");
                return;
            }
            */
            MaterialButton startButton = (MaterialButton)sender;
            MaterialSwitch? loginSwitch = null;

            if (startButton.Equals(materialButton_start_1)) { loginSwitch = materialSwitch_login_1; } 
            else if (startButton.Equals(materialButton_start_2)) { loginSwitch = materialSwitch_login_2; } 
            else { loginSwitch = materialSwitch_login_3; }

            if (true == loginSwitch.Checked) { GameStart(); } 
            else { 
                isGameStartLogin = true; 
                SwitchClick(loginSwitch); 
            }
        }

        private async void GameStart() {
            isGameStartLogin = false;

            string client_path;
            switch (currentClient) {
                case Client.Client1:
                    client_path = ConfigManager.getConfig("client_path_1");
                    break;
                case Client.Client2:
                    client_path = ConfigManager.getConfig("client_path_2");
                    break;
                case Client.Client3:
                    client_path = ConfigManager.getConfig("client_path_3");
                    break;
                default:
                    client_path = "";
                    break;
            }

            if (client_path == "") {
                MessageBox.Show("Ŭ���̾�Ʈ ��ΰ� �����Ǿ� ���� �ʽ��ϴ�. ����â���� Ŭ���̾�Ʈ ��θ� �������ּ���.");
                return;
            }

            try {
                //�ش� Ŭ���̾�Ʈ�� ��θ� ������Ʈ���� ��Ͻ�ŵ�ϴ�.
                RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\JOYON\Gersang\Korean", RegistryKeyPermissionCheck.ReadWriteSubTree);
                if (registryKey != null) {
                    registryKey.SetValue("InstallPath", client_path);
                    registryKey.Close();
                }
            } catch (Exception ex) {
                Trace.WriteLine(ex.Message);
                Trace.WriteLine(ex.StackTrace);
            }

            string? value = null;
            try {
                //Gersang Game Starter ��ΰ� ����Ǿ��ִ� ������Ʈ�� ��ο� �����Ͽ� ��θ� ����ϴ�.
                value = Registry.ClassesRoot.OpenSubKey("Gersang").OpenSubKey("shell").OpenSubKey("open").OpenSubKey("command").GetValue("").ToString();
            } catch (Exception ex) {
                Trace.WriteLine(ex.Message);
                Trace.WriteLine(ex.StackTrace);
                value = null;
            }

            string starterPath; //�Ż� ��Ÿ���� ��θ� ����
            //GameStarter ��θ� ã�� �� ���� ���, Starter �ٿ�ε� �ȳ�â�� ���ϴ�.
            if (value == null) {
                OpenGersangStarterInstallDialog();
                return;
            } else {
                starterPath = value.Replace(@"""", String.Empty);
            }

            //������Ʈ���� ����� GameStarter�� ��ο� ������ Starter�� ��ġ�Ǿ� ���� ���� ���, �ٿ�ε� �ȳ�â�� ���ϴ�.
            if (!File.Exists(starterPath)) {
                OpenGersangStarterInstallDialog();
                return;
            } else {
                await webView_main.ExecuteScriptAsync(@"startRetry = setTimeout(""socketStart('main')"", 2000);"); //������ ���ϴ�.
                Process.Start(value.ToString()); //�Ż� ��Ÿ�͸� �����մϴ�.
            }
        }

        private void OpenGersangStarterInstallDialog() {
            if (MessageBox.Show("GersangGameStarter�� ��ġ�Ǿ� ���� �ʽ��ϴ�.\n�ٿ�ε� �����ðڽ��ϱ�? (�Ż� ���� �ٿ�ε� ��ũ�Դϴ�)", "���� ���� ����", MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes) {
                try {
                    Process.Start(new ProcessStartInfo(url_installStarter) { UseShellExecute = true });
                } catch (Exception ex2) {
                    Trace.WriteLine(ex2.Message);
                    Trace.WriteLine(ex2.StackTrace);
                }
            } else {
                MessageBox.Show("�Ż� Ȩ������ -> �ڷ�� -> Ŭ���̾�Ʈ -> GersangGameStarter ���� ��ġ\n�� ��ο��� �Ż� ����⸦ �ٿ�ε� �����ž� ���� ������ �����մϴ�.", "���� ���� ����", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void materialSwitch_login_Click(object sender, EventArgs e) {
            SwitchClick((MaterialSwitch)sender);
        }

        private void SwitchClick(MaterialSwitch sender) {
            if (isWebFunctionDeactivated) {
                Trace.WriteLine("�� �ε� �� ����ġ �۵��� �Ұ��� �մϴ�.");
                return;
            }

            MaterialSwitch materialSwitch = sender;
            string name = materialSwitch.Name;
            currentClient = (Client)Byte.Parse(name.Substring(name.Length - 1));

            //�α׾ƿ�
            if (materialSwitch.Checked) {
                Trace.WriteLine("�α׾ƿ� �մϴ�.");
                webView_main.CoreWebView2.Navigate(url_logout);
                materialSwitch.CheckState = CheckState.Unchecked;
                currentState = State.None;
                currentClient = Client.None;
                return;
            }

            if (this.currentState == State.LoggedIn) {
                Trace.WriteLine("�ٸ� ������ �α��� �ϱ� ���� �α׾ƿ� �մϴ�.");

                materialSwitch_login_1.CheckState = CheckState.Unchecked;
                materialSwitch_login_2.CheckState = CheckState.Unchecked;
                materialSwitch_login_3.CheckState = CheckState.Unchecked;

                currentState = State.LoginOther;

                webView_main.CoreWebView2.Navigate(url_logout);
                return;
            }

            if (materialSwitch.Tag == null || materialSwitch.Tag.ToString().Length == 0 || materialSwitch.Tag.ToString().Contains("���þ���")) {
                MessageBox.Show("�α��� �� ������ ���õ��� �ʾҽ��ϴ�.");
                return;
            }

            string[] account = materialSwitch.Tag.ToString().Split(';');
            Login(account[0], account[1]);
        }

        private void materialButton_shortcut_Click(object sender, EventArgs e) {
            MaterialButton button = (MaterialButton)sender;
            string? url = ConfigManager.getConfig("shortcut_" + button.Name.Substring(button.Name.Length - 1, 1));

            if (url == null || url.Equals("")) {
                return;
            }

            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute)) {
                MessageBox.Show("�߸��� ��ũ ���� �Դϴ�. �ٽ� �������ּ���.");
                return;
            }

            MaterialForm form = new MaterialForm() {
                Size = new Size(1500, 1000),
                FormStyle = FormStyles.ActionBar_None,
                StartPosition = FormStartPosition.CenterParent
            };

            form.Controls.Add(webView_main);
            form.Load += (sender, e) => {
                webView_main.CoreWebView2.Navigate(ConfigManager.getConfig("shortcut_" + button.Name.Substring(button.Name.Length - 1, 1)));
            };
            form.FormClosed += (sender, e) => {
                webView_main.CoreWebView2.Navigate(url_main);
                form.Controls.Clear();
            };
            form.ShowDialog();
        }

        private void materialComboBox_account_SelectedIndexChanged(object sender, EventArgs e) {
            MaterialComboBox comboBox = (MaterialComboBox)sender;

            if (Byte.Parse(comboBox.Name.Substring(comboBox.Name.Length - 1, 1)) == (byte)currentClient && currentState == State.LoggedIn) {
                Trace.WriteLine("���� �α����� Ŭ���̾�Ʈ�� ������ �����Ͽ����Ƿ�, �α׾ƿ� �մϴ�.");
                webView_main.CoreWebView2.Navigate(url_logout);
                materialSwitch_login_1.CheckState = CheckState.Unchecked;
                materialSwitch_login_2.CheckState = CheckState.Unchecked;
                materialSwitch_login_3.CheckState = CheckState.Unchecked;
                currentState = State.None;
                currentClient = Client.None;
                return;
            }

            string id = comboBox.Text;
            string switchTag;
            //Trace.WriteLine(id);

            if(id.Contains("���þ���")) { switchTag = id; }
            else { switchTag = id + ";" + ConfigManager.getConfig(comboBox.Text); }
            //Trace.WriteLine(switchTag);

            byte current_preset = Byte.Parse(ConfigManager.getConfig("current_preset"));
            int[] temp = Array.ConvertAll(ConfigManager.getConfig("current_comboBox_index_preset_" + current_preset).Split(';'), s => int.Parse(s));

            if (comboBox.Equals(materialComboBox_account_1)) { 
                materialSwitch_login_1.Tag = switchTag;
                temp[0] = comboBox.SelectedIndex;
            } else if(comboBox.Equals(materialComboBox_account_2)) { 
                materialSwitch_login_2.Tag = switchTag;
                temp[1] = comboBox.SelectedIndex;
            } else { 
                materialSwitch_login_3.Tag = switchTag;
                temp[2] = comboBox.SelectedIndex;
            }

            StringBuilder sb = new StringBuilder();
            foreach (var item in temp) {
               sb.Append(item.ToString() + ';');
            }
            sb.Remove(sb.Length - 1, 1);
            ConfigManager.setConfig("current_comboBox_index_preset_" + current_preset, sb.ToString());
        }

        private void LoadAccountComboBox() {
            materialComboBox_account_1.Items.Clear();
            materialComboBox_account_2.Items.Clear();
            materialComboBox_account_3.Items.Clear();

            string temp = ConfigManager.getConfig("account_list");
            string[] accountList;
            if (temp.Length != 0) {
                accountList = temp.Remove(temp.Length - 1, 1).Split(';');
            } else {
                accountList = new string[0];
            }

            materialComboBox_account_1.Items.Add("���þ���");
            materialComboBox_account_2.Items.Add("���þ���");
            materialComboBox_account_3.Items.Add("���þ���");

            foreach (var item in accountList) {
                materialComboBox_account_1.Items.Add(item);
                materialComboBox_account_2.Items.Add(item);
                materialComboBox_account_3.Items.Add(item);
            }

            byte current_preset = Byte.Parse(ConfigManager.getConfig("current_preset"));
            int[] index = Array.ConvertAll(ConfigManager.getConfig("current_comboBox_index_preset_" + current_preset).Split(';'), s => int.Parse(s));
            materialComboBox_account_1.SelectedIndex = index[0];
            materialComboBox_account_2.SelectedIndex = index[1];
            materialComboBox_account_3.SelectedIndex = index[2];
            materialComboBox_account_1.Refresh();
            materialComboBox_account_2.Refresh();
            materialComboBox_account_3.Refresh();
        }

        private void materialButton1_Click(object sender, EventArgs e) {
            MaterialForm form = new MaterialForm() {
                Size = new Size(1500, 1000),
                FormStyle = FormStyles.ActionBar_None,
                StartPosition = FormStartPosition.CenterScreen
            };

            form.Controls.Add(webView_main);
            form.FormClosed += (sender, e) => {
                form.Controls.Clear();
            };
            
            form.Show();
        }

        private void LoadShortcut() {
            string[] names = ConfigManager.getConfig("shortcut_name").Split(';');
            materialButton_shortcut_1.Text = names[0];
            materialButton_shortcut_2.Text = names[1];
            materialButton_shortcut_3.Text = names[2];
            materialButton_shortcut_4.Text = names[3];
        }

        private void materialButton_naver_Click(object sender, EventArgs e) {
            MaterialButton searchButton = (MaterialButton)sender;
            MaterialSwitch? loginSwitch = null;

            if(searchButton.Equals(materialButton_naver_1)) { loginSwitch = materialSwitch_login_1; } 
            else if(searchButton.Equals(materialButton_naver_2)) { loginSwitch = materialSwitch_login_2; }
            else { loginSwitch = materialSwitch_login_3; }

            isSearch = true;
            if (true == loginSwitch.Checked) {
                webView_main.CoreWebView2.Navigate(url_search); //���̹� �˻�
            } else {
                SwitchClick(loginSwitch);
            }
        }

        private void radio_preset_CheckedChanged(object sender, EventArgs e) {
            MaterialRadioButton radio = (MaterialRadioButton)sender;
            string? value = radio.Tag.ToString();
            if(value == null) {
                MessageBox.Show("RadioButton�� Tag�� Null�Դϴ�.");
                return;
            }

            ConfigManager.setConfig("current_preset", value);
            LoadAccountComboBox();
        }

        private void LoadRadioButton() {
            byte current_preset = Byte.Parse(ConfigManager.getConfig("current_preset"));
            switch (current_preset) {
                case 1 :
                    radio_preset_1.PerformClick();
                    break;
                case 2 :
                    radio_preset_2.PerformClick();
                    break;
                case 3 :
                    radio_preset_3.PerformClick();
                    break;
                case 4 :
                    radio_preset_4.PerformClick();
                    break;
                default:
                    MessageBox.Show("LoadRadioButton���� ���� �߻�");
                    break;
            }
        }

        private void Form1_Resize(object sender, EventArgs e) {
            Trace.WriteLine("Form Resize! : " + this.WindowState);
            if(this.WindowState == FormWindowState.Minimized) {
                //this.WindowState = FormWindowState.Normal;
            }
        }

        private async void Login(string id, string protected_pw) {
            try {
                await webView_main.ExecuteScriptAsync("document.getElementById('GSuserID').value = '" + id + "'");
                await webView_main.ExecuteScriptAsync("document.getElementById('GSuserPW').value = '" + EncryptionSupporter.Unprotect(protected_pw) + "'");
            } catch (CryptographicException e) {
                //����ڰ� ��ȣȭ�� �н����尡 ���Ե� ���������� Ÿ PC�� ���� �� ��� �� �߻��ϴ� ����
                Trace.WriteLine(e.Message);
                MessageBox.Show("���������� Ÿ PC�� �ű� ������ Ȯ�εǾ����ϴ�.\n���� ���� ���� ������ ���� ��� ���� ������ �ʱ�ȭ �մϴ�.", "�н����� ��ȣȭ ����", MessageBoxButtons.OK, MessageBoxIcon.Error);
                currentClient = Client.None;
                currentState = State.None;
                //clearPassword();
                return;
            }

            await webView_main.ExecuteScriptAsync("document.getElementById('btn_Login').click()");
            currentState = State.LoggedIn;
        }

        private void materialButton_setting_Click(object sender, EventArgs e) {
            MaterialButton button = (MaterialButton)sender;
            if (button.Equals(materialButton_setting_account)) {
                OpenAccountSettingDialog();
            } else if (button.Equals(materialButton_setting_client)) {
                OpenClientSettingDialog();
            } else if (button.Equals(materialButton_setting_shortcut)) {
                OpenShortcuttSettingDialog();
            }
        }

        private void OpenShortcuttSettingDialog() {
            Form backgroundForm = new Form() {
                StartPosition = FormStartPosition.Manual,
                FormBorderStyle = FormBorderStyle.None,
                Opacity = .50d,
                BackColor = Color.Black,
                Location = this.Location,
                Size = this.Size,
                ShowInTaskbar = false,
                Owner = this
            };

            Form_ShortcutSetting dialog_shortcutSetting = new Form_ShortcutSetting() {
                Owner = this
            };

            try {
                backgroundForm.Show();
                dialog_shortcutSetting.ShowDialog();
                LoadShortcut();
            } catch (Exception ex) {
                Trace.WriteLine(ex.StackTrace);
            } finally {
                backgroundForm.Dispose();
            }
        }

        private void OpenClientSettingDialog() {
            Form backgroundForm = new Form() {
                StartPosition = FormStartPosition.Manual,
                FormBorderStyle = FormBorderStyle.None,
                Opacity = .50d,
                BackColor = Color.Black,
                Location = this.Location,
                Size = this.Size,
                ShowInTaskbar = false,
                Owner = this
            };

            Form_ClientSetting dialog_clientSetting = new Form_ClientSetting() {
                Owner = this
            };

            try {
                backgroundForm.Show();
                dialog_clientSetting.ShowDialog();
            } catch (Exception ex) {
                Trace.WriteLine(ex.StackTrace);
            } finally {
                backgroundForm.Dispose();
            }
        }

        private void OpenAccountSettingDialog() {
            Form backgroundForm = new Form() {
                StartPosition = FormStartPosition.Manual,
                FormBorderStyle = FormBorderStyle.None,
                Opacity = .50d,
                BackColor = Color.Black,
                Location = this.Location,
                Size = this.Size,
                ShowInTaskbar = false,
                Owner = this
            };

            Form_AccountSetting dialog_accountSetting = new Form_AccountSetting() {
                Owner = this
            };

            try {
                backgroundForm.Show();
                dialog_accountSetting.ShowDialog();
                LoadAccountComboBox();
            } catch (Exception ex) {
                Trace.WriteLine(ex.StackTrace);
            } finally {
                backgroundForm.Dispose();
            }
        }
    }
}