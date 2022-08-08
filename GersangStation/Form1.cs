using MaterialSkin;
using MaterialSkin.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;
using Octokit;
using System.Diagnostics;
using System.Reflection;
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

        private const string url_main_vsn = @"http://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/" + @"Client_Patch_File/" + @"Online/vsn.dat.gsz";
        private const string url_test_vsn = @"http://akgersang.xdn.kinxcdn.com/Gersang/Patch/Test_Server/" + @"Client_Patch_File/" + @"Online/vsn.dat.gsz";

        private const string url_release = "https://github.com/byungmeo/GersangStation/releases/latest";

        private bool isWebFunctionDeactivated = false;
        private bool isSearch = false;
        private bool isGameStartLogin = false;
        private string previousUrl = "";
        private bool isGetSearchItem = false;
        private bool isExceptSearch = false; //2022-04-26 �Ż� Ȩ������ �˻� �� �̺�Ʈ �������� �ٷ� �Ѿ�� ���

        WebView2? webView_main = null;

        public Form1() {
            Logger.Log("Log : " + "�� ������ ����");
            Logger.DeleteOldLogFile();
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
            Logger.Log("Log : " + "���� �ε��");
            webView_main = new WebView2() {
                Visible = true,
                Dock = DockStyle.Fill,
                Source = new Uri("https://www.gersang.co.kr/main/index.gs")
            };

            try {
                //webView_main.CoreWebView2InitializationCompleted += WebView_main_CoreWebView2InitializationCompleted;
                //await webView_main.EnsureCoreWebView2Async(null); //������ CoreWebView2InitializationCompleted �����ʸ� ���� �� �����ؾ� ��.
                await InitializeAsync();
                //webView_main.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested; //Edge ���� ������Ʈ�� ���� NewWindow ���� ����
                webView_main.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false; //Alert ���� �޽���â�� �����ʰ� ScriptDialogOpening �̺�Ʈ�� ���� ������ �� �ֵ��� �մϴ�.
                webView_main.CoreWebView2.ScriptDialogOpening += CoreWebView2_ScriptDialogOpening;
            } catch (WebView2RuntimeNotFoundException ex) {
                Logger.Log("Exception : " + "WebView2��Ÿ���� ã�� �� ���� ��ġ �ȳ��޽��� ���");
                Trace.WriteLine(ex.StackTrace);
                DialogResult dr = MessageBox.Show("��Ŭ�� �����̼��� �̿��ϱ� ���ؼ�\nWebView2 ��Ÿ���� �ݵ�� ��ġ�ϼž� �մϴ�.\n��ġ �Ͻðڽ��ϱ�? (��ġ ��ũ�� �ڵ����� �����մϴ�.)", "��Ÿ�� ��ġ �ʿ�", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (dr == DialogResult.Yes) {
                    Process.Start(new ProcessStartInfo("https://go.microsoft.com/fwlink/p/?LinkId=2124703") { UseShellExecute = true });
                }
                System.Windows.Forms.Application.Exit();
                return;
            }

            webView_main.NavigationStarting += webView_main_NavigationStarting;
            webView_main.NavigationCompleted += webView_main_NavigationCompleted;

            webView_main.Source = new Uri("https://www.gersang.co.kr/main/index.gs");

            LoadComponent();
        }

        private async Task InitializeAsync() {
            Trace.WriteLine("InitializeAsync");
            await webView_main.EnsureCoreWebView2Async(null);
            Trace.WriteLine("WebView2 Runtime version: " + webView_main.CoreWebView2.Environment.BrowserVersionString);
            Logger.Log("Log : " + "WebView2 ��Ÿ�� ���� Ȯ�� �Ϸ�");
        }


        private void LoadComponent() {
            ConfigManager.Validation();
            LoadCheckBox();
            LoadRadioButton();
            LoadAccountComboBox();
            LoadShortcut();
            SetToolTip();
            CheckAccount();
            CheckProgramUpdate();
            LoadAnnouncements();
        }

        private async void LoadAnnouncements() {
            try {
                GitHubClient client = new GitHubClient(new ProductHeaderValue("Byungmeo"));
                IReadOnlyList<Release> releases = await client.Repository.Release.GetAll("byungmeo", "GersangStation");
                Readme r = await client.Repository.Content.GetReadme("byungmeo", "GersangStation");
                string content = r.Content;
                string[] announcements = content.Substring(content.LastIndexOf("# ��������")).Split('\n');
                if (announcements.Length <= 1) {
                    linkLabel_announcement.Text = "���������� �����ϴ�";
                } else {
                    string latestAnnouncement = announcements[1];
                    linkLabel_announcement.Text = latestAnnouncement.Split('{')[0];
                    linkLabel_announcement.Click += (sender, e) => {
                        string announcementPage = latestAnnouncement.Substring(latestAnnouncement.LastIndexOf('{') + 1, 1);
                        Process.Start(new ProcessStartInfo("https://github.com/byungmeo/GersangStation/discussions/" + announcementPage) { UseShellExecute = true });
                    };
                }
            } catch (Exception e) {
                Logger.Log("Exception: " + "���������� �ҷ����� �� ���ܰ� �߻��Ͽ����ϴ�. -> " + e.Message);
                linkLabel_announcement.Text = "���������� �ҷ����µ� �����Ͽ����ϴ�";
            }
        }

        private void SetToolTip() {
            Logger.Log("Log : " + "����ȭ�� ������Ʈ�� ������ ����");
            toolTip1.Active = true;
            /**
             * <-- ����ȭ�� -->
             */
            toolTip1.SetToolTip(button_tray, "Ʈ���̿� �����");
            toolTip1.SetToolTip(radio_preset_1, "1�� ����");
            toolTip1.SetToolTip(radio_preset_2, "2�� ����");
            toolTip1.SetToolTip(radio_preset_3, "3�� ����");
            toolTip1.SetToolTip(radio_preset_4, "4�� ����");
            toolTip1.SetToolTip(materialCheckbox_testServer, "Ȱ��ȭ �� �׽�Ʈ ������ �����մϴ�.\n(��ġ ����)");
            toolTip1.SetToolTip(materialButton_debugging, "�۵��ϴ� ������ ���� ����");
            toolTip1.SetToolTip(materialComboBox_account_1, "��Ŭ�� ���� ����");
            toolTip1.SetToolTip(materialComboBox_account_2, "2Ŭ�� ���� ����");
            toolTip1.SetToolTip(materialComboBox_account_3, "3Ŭ�� ���� ����");
            toolTip1.SetToolTip(materialSwitch_login_1, "��Ŭ�� Ȩ������ �α���");
            toolTip1.SetToolTip(materialSwitch_login_2, "2Ŭ�� Ȩ������ �α���");
            toolTip1.SetToolTip(materialSwitch_login_3, "3Ŭ�� Ȩ������ �α���");
            toolTip1.SetToolTip(materialButton_search_1, "��Ŭ�� �˻����� ����");
            toolTip1.SetToolTip(materialButton_search_2, "2Ŭ�� �˻����� ����");
            toolTip1.SetToolTip(materialButton_search_3, "3Ŭ�� �˻����� ����");
            toolTip1.SetToolTip(materialButton_start_1, "��Ŭ�� ���� ����");
            toolTip1.SetToolTip(materialButton_start_2, "2Ŭ�� ���� ����");
            toolTip1.SetToolTip(materialButton_start_3, "3Ŭ�� ���� ����");
            string shortcut_1 = ConfigManager.getConfig("shortcut_1");
            if (shortcut_1 == "") shortcut_1 = "��ũ�� �������� �ʾҽ��ϴ�.";
            string shortcut_2 = ConfigManager.getConfig("shortcut_2");
            if (shortcut_2 == "") shortcut_2 = "��ũ�� �������� �ʾҽ��ϴ�.";
            string shortcut_3 = ConfigManager.getConfig("shortcut_3");
            if (shortcut_3 == "") shortcut_3 = "��ũ�� �������� �ʾҽ��ϴ�.";
            string shortcut_4 = ConfigManager.getConfig("shortcut_4");
            if (shortcut_4 == "") shortcut_4 = "��ũ�� �������� �ʾҽ��ϴ�.";
            toolTip1.SetToolTip(materialButton_shortcut_1, shortcut_1);
            toolTip1.SetToolTip(materialButton_shortcut_2, shortcut_2);
            toolTip1.SetToolTip(materialButton_shortcut_3, shortcut_3);
            toolTip1.SetToolTip(materialButton_shortcut_4, shortcut_4);
        }

        private async void CheckProgramUpdate() {
            Logger.Log("Log : " + "����꿡�� �ֽ� ���α׷� ����� �����ϴ��� Ȯ�� �õ�");
            //���� ������Ʈ �� Properties -> AssemblyInfo.cs �� AssemblyVersion�� AssemblyFileVersion�� �ٲ��ּ���.
            string version_current = Assembly.GetExecutingAssembly().GetName().Version.ToString().Substring(0, 5);
            Trace.WriteLine(version_current);

            string version_latest;

            try {
                //����꿡�� ��� ������ ������ �޾ƿɴϴ�.
                GitHubClient client = new GitHubClient(new ProductHeaderValue("Byungmeo"));
                IReadOnlyList<Release> releases = await client.Repository.Release.GetAll("byungmeo", "GersangStation");
                version_latest = releases[0].TagName;
                label_version_current.Text = label_version_current.Text.Replace("00000", version_current);
                label_version_latest.Text = label_version_latest.Text.Replace("00000", version_latest);

                //����꿡 �Խõ� ������ ������ ���� ������ �ʱ�ȭ �մϴ�.
                //Version latestGitHubVersion = new Version(releases[0].TagName);
                Version latestGitHubVersion = new Version(version_latest);
                Version localVersion = new Version(version_current);
                Trace.WriteLine("����꿡 ���������� �Խõ� ���� : " + latestGitHubVersion);
                Trace.WriteLine("���� ������Ʈ ���� : " + localVersion);

                //���� ��
                int versionComparison = localVersion.CompareTo(latestGitHubVersion);
                if (versionComparison < 0) {
                    Logger.Log("Log : " + "���������� �Ǵ��Ͽ� ������Ʈ �ȳ��޽����� ���");
                    Trace.WriteLine("�������Դϴ�! ������Ʈ �޽����ڽ��� ����մϴ�!");

                    DialogResult dr = MessageBox.Show(releases[0].Body + "\n\n������Ʈ �Ͻðڽ��ϱ�? (GitHub ����)",
                        "������Ʈ �ȳ�", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                    if (dr == DialogResult.Yes) {
                        Process.Start(new ProcessStartInfo(url_release) { UseShellExecute = true });
                    }
                } else if (versionComparison > 0) {
                    Logger.Log("Log : " + "����꿡 �Խõ� ������ �������� �ֽŹ����̶�� �Ǵ�");
                    Trace.WriteLine("����꿡 ������� �������� �ֽ��Դϴ�!");
                } else {
                    Logger.Log("Log : " + "����꿡 �Խõ� ������ ������ ������ �ֽŹ����̶�� �Ǵ�");
                    Trace.WriteLine("���� ������ �ֽŹ����Դϴ�!");
                }
            } catch (Exception ex) {
                Logger.Log("Exception : " + "���α׷� ������Ʈ Ȯ�� �� ���ܰ� �߻�\r\n  :" + ex.Message);
                MessageBox.Show(this, "���α׷� ������Ʈ Ȯ�� ���� ������ �߻��Ͽ����ϴ�.\n���� �޽����� ĸ���ϰ�, ���� ��Ź�帳�ϴ�.", "������Ʈ Ȯ�� ����", MessageBoxButtons.OK, MessageBoxIcon.Error);
                MessageBox.Show(this, "���� �޽���1 : \n" + ex.Message);
                MessageBox.Show(this, "���� �޽���2 : \n" + ex.ToString());
                Trace.WriteLine(ex.Message);
            }
        }

        private void CheckAccount() {
            Logger.Log("Log : " + "����� ������ �ϳ��� ������ Ȯ��");
            if (materialComboBox_account_1.Items.Count <= 1) {
                Logger.Log("Log : " + "����� ������ �ϳ��� ���ٰ� �Ǵ��Ͽ� ���� ���� ȭ������ �̵����� ���θ� ���� �޽��� ���");
                DialogResult dr = MessageBox.Show("���� ����� ������ �ϳ��� �����ϴ�.\n���� ���� ȭ������ �̵��Ͻðڽ��ϱ�?", "���� ����", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                if (dr == DialogResult.OK) {
                    OpenAccountSettingDialog();
                }
            }
        }

        private void LoadCheckBox() {
            Logger.Log("Log : " + "�׽�Ʈ���� ���� üũ�ڽ� ���� �ε�");
            this.materialCheckbox_testServer.Checked = bool.Parse(ConfigManager.getConfig("is_test_server"));
        }

        /* Edge ���� ������Ʈ�� ���� ���� ����
        private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e) {
            if (sender != null) e.NewWindow = (CoreWebView2)sender; //WebView2�� �װ� ���ο� â�� �ߴ� ��� WebView2���� ��� ���� ����
            //e.Handled = true; //true�� ���ο� â�� �ߴ� �� ���
        }
        */

        private async void CoreWebView2_ScriptDialogOpening(object? sender, CoreWebView2ScriptDialogOpeningEventArgs e) {
            string message = e.Message;
            Logger.Log("Event : " + "WebView2_ScriptDialogOpening\r\n  :" + message + "\r\n  :" + e.Kind);
            Trace.WriteLine(message);
            Trace.WriteLine("��ȭâ ���� : " + e.Kind);

            if (e.Kind == CoreWebView2ScriptDialogKind.Confirm) {
                Trace.WriteLine("�������� �ִ� ��ȭ���� ����");
                DialogResult dr = DialogResult.None;
                var task = Task.Run(() => {
                    dr = MessageBox.Show(message, "����", MessageBoxButtons.YesNo, MessageBoxIcon.Information, 
                        MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                });
                await task;
                if (dr == DialogResult.Yes) {
                    e.Accept();
                }

                return;
            }

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
                else if (message.Contains("�������� ���޵Ǿ����ϴ�.") || message.Contains("�̹� �������� �����ϼ̽��ϴ�.") || message.Contains("���� �ð��� �ƴմϴ�.")) {
                    MessageBox.Show(message);
                    if (e.Uri.Contains("attendance") && true == isGetSearchItem) {
                        webView_main.CoreWebView2.Navigate(url_main);
                        isGetSearchItem = false;
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
        private void webView_main_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e) {
            Logger.Log("Event : " + "WebView2 NavigationStarting\r\n  :" + e.Uri.ToString() + "\r\n  :" + "(previous)" + webView_main.Source);
            Trace.WriteLine("NavigationStarting : " + e.Uri.ToString());
            Trace.WriteLine("NavigationStarting Previous URL : " + webView_main.Source);
            previousUrl = webView_main.CoreWebView2.Source;
            deactivateWebSideFunction();
        }

        private void webView_main_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) {
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
            Logger.Log("Event : " + "WebView2 NavigationCompleted\r\n  :" + url);
            Trace.WriteLine("NavigationCompleted : " + url);

            if (url.Contains("pw_reset.gs")) {
                doPwReset();
                return;
            }

            if (url.Contains("otp.gs")) {
                this.BeginInvoke(() => {
                    string? otpCode = showDialogOtp();

                    if (otpCode == null) { 
                        MessageBox.Show("OTP �ڵ带 �Է����� �ʾҽ��ϴ�.");
                        isSearch = false;
                    } 
                    else { doOtpInput(otpCode); }
                });
                return;
            }

            if (url.Contains("search.naver")) {
                if (isSearch) { doNavigateGersangSite(); }
                return;
            }

            if (url.Contains("attendance")) {
                if (isSearch) { doGetEventItem(); }
                //else { webView_main.CoreWebView2.Navigate(url_main); }
                return;
            }

            if (url.Contains("main/index.gs")) {
                if(isSearch) {
                    if(previousUrl.Contains("search.naver")) {
                        doNavigateAttendancePage();
                        return;
                    }

                    if(previousUrl.Contains("event") && isExceptSearch) {
                        isExceptSearch = false;
                        doNavigateAttendancePage();
                        return;
                    }
                }

                if(currentState == State.LoginOther) { doLoginOther(); } 
                else if (currentState == State.LoggedIn) { doCheckLogin(); }
                return;
            } else if (url.Contains("event")) {
                if(isSearch && previousUrl.Contains("search.naver")) {
                    isExceptSearch = true;
                    webView_main.CoreWebView2.Navigate(url_main);
                    return;
                }
            }
        }

        private async void doGetEventItem() {
            Logger.Log("Log : " + "�˻� ���� ���� �ð��� Ȯ�� �õ�");
            isSearch = false;

            /* �˻����� ���� �ð��뺰 ���� ��
            1-> 00:05 ~05:55
            2-> 06:05 ~11:55
            3-> 12:05 ~17:55
            4-> 18:05 ~23:55
            */
            int arg; //event_Search_Use ��ũ��Ʈ ���� ����

            int koreaHour = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Korea Standard Time").Hour;
            if (koreaHour >= 0 && koreaHour <= 5) { arg = 1; } 
            else if (koreaHour >= 6 && koreaHour <= 11) { arg = 2; } 
            else if (koreaHour >= 12 && koreaHour <= 17) { arg = 3; } 
            else { arg = 4; }

            isGetSearchItem = true;
            Logger.Log("Log : " + "�˻� ���� ���� �ð��� : " + arg);
            await webView_main.ExecuteScriptAsync("event_Search_Use(" + arg + ");");
        }

        private async void doNavigateAttendancePage() {
            Logger.Log("Log : " + "�Ż� �������������� �⼮üũ �̺�Ʈ �������� �̵��ϴ� a�±׸� ã�� Ŭ�� �õ�");
            Trace.WriteLine("(�˻�) ���� -> �⼮������");
            await webView_main.ExecuteScriptAsync(@"document.querySelector('[href *= ""attendance""]').click();");
        }

        private async void doNavigateGersangSite() {
            Logger.Log("Log : " + "�˻���������Ʈ���� �Ż� ������������ �̵��ϴ� a�±׸� ã�� Ŭ�� �õ�");
            Trace.WriteLine("(�˻�) ���̹� -> �Ż�");

            //���ο� â�� ���� �ʵ��� a�±׿� target �Ӽ��� ����
            await webView_main.ExecuteScriptAsync(@"document.querySelector('[href *= """ + url_search_gersang + @"""]').removeAttribute(""target"");");

            //target �Ӽ��� ���ŵ� a�±׸� Ŭ��
            await webView_main.ExecuteScriptAsync(@"document.querySelector('[href *= """ + url_search_gersang + @"""]').click();");
        }

        private void deactivateWebSideFunction() {
            Logger.Log("Log : " + @"WebView2�� ������ ������Ʈ���� ""��Ȱ��ȭ""��");
            //materialSwitch_login_1.Enabled = false;
            //materialSwitch_login_2.Enabled = false;
            //materialSwitch_login_3.Enabled = false;
            materialButton_search_1.Enabled = false;
            materialButton_search_2.Enabled = false;
            materialButton_search_3.Enabled = false;
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
            Logger.Log("Log : " + @"WebView2�� ������ ������Ʈ���� ""Ȱ��ȭ""��");
            //materialSwitch_login_1.Enabled = true;
            //materialSwitch_login_2.Enabled = true;
            //materialSwitch_login_3.Enabled = true;
            materialButton_search_1.Enabled = true;
            materialButton_search_2.Enabled = true;
            materialButton_search_3.Enabled = true;
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

        private void activateWebSideFunction_invoke() {
            DelegateActivateWebSideFunction d = () => {
                activateWebSideFunction();
            };
            this.Invoke(d);
        }

        private delegate void DelegateActivateWebSideFunction();

        private void handleWebError(CoreWebView2WebErrorStatus webErrorStatus) {
            Logger.Log("Error : " + "WebView2���� ������ �߻��Ͽ� �ڵ鸵 -> " + webErrorStatus);
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
            Logger.Log("Log : " + "�α����� �Ǿ����� Ȯ�� �õ�");
            var logout_btn = await webView_main.ExecuteScriptAsync(@"document.querySelector(""a[href = '" + "/member/logoutProc.gs" + @"']"")");
            if (logout_btn != null) {
                Logger.Log("Log : " + "�α����� �Ǿ��ִ� ����");
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
                Logger.Log("Log : " + "�α����� �ȵ��ִ� ����");
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
            Logger.Log("Log : " + "doLoginOther() -> �α׾ƿ� �� ���� Ȩ�������� �α����� ���� ������Ʈ�� �ε��Ǿ����� Ȯ���ϰ� �α����� �õ�");
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

            if (button_login != null && textBox_id != null && textBox_pw != null) { Login(account[0], account[1]); }
        }

        private async void doOtpInput(string otpCode) {
            Logger.Log("Log : " + "�Է��� OTP�ڵ带 �̿��� OTP�α��� �õ�");
            await webView_main.ExecuteScriptAsync("document.getElementById('GSotpNo').value = '" + otpCode + "'");
            await webView_main.ExecuteScriptAsync("document.getElementById('btn_Send').click()");
        }

        private async void doPwReset() {
            Logger.Log("Log : " + "�Ż� �н����� ���� ���� ���������� ���� Ȩ�������� �̵���");
            await webView_main.ExecuteScriptAsync(@"document.querySelector(""a[href = '" + url_main + @"']"").click()");
        }

        private string? showDialogOtp() {
            Logger.Log("Log : " + "OTP���� ���");
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
                FormStyle = FormStyles.ActionBar_40,
                Sizable = false,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(202, 136),
                Text = "OTP �Է�",
                MaximizeBox = false,
                MinimizeBox = false,
                TopMost = true,
                ShowInTaskbar = false,
                Owner = this
            };

            MaterialTextBox2 textBox_otp = new MaterialTextBox2() {
                MaxLength = 8,
                Location = new Point(17, 82),
                Size = new Size(111, 36),
                UseTallSize = false
            };
            dialog_otp.Controls.Add(textBox_otp);

            MaterialButton button_confirm = new MaterialButton() {
                Text = "Ȯ��",
                Location = new Point(135, 82),
                AutoSize = false,
                Size = new Size(50, 36)
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
            MaterialButton startButton = (MaterialButton)sender;
            Logger.Log("Click : " + startButton.Name);
            StartClick(startButton);
        }

        private void StartClick(MaterialButton startButton) {
            MaterialSwitch? loginSwitch;

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

            string configKey;
            string regKey;
            string server;
            string url_vsn;
            if (materialCheckbox_testServer.Checked) {
                configKey = "client_path_test_";
                regKey = "TestPath";
                server = "test";
                url_vsn = url_test_vsn;
            } 
            else { 
                configKey = "client_path_";
                regKey = "InstallPath";
                server = "main";
                url_vsn = url_main_vsn;
            }

            string client_path;
            switch (currentClient) {
                case Client.Client1:
                    client_path = ConfigManager.getConfig(configKey + '1');
                    break;
                case Client.Client2:
                    client_path = ConfigManager.getConfig(configKey + '2');
                    break;
                case Client.Client3:
                    client_path = ConfigManager.getConfig(configKey + '3');
                    break;
                default:
                    client_path = "";
                    break;
            }

            if (client_path == "") {
                Logger.Log("Log : " + "Ŭ���̾�Ʈ�� ��ΰ� �����Ǿ������ʾ� ���� â���� �̵����� ���θ� ���� �޽��� ���");
                DialogResult dr = MessageBox.Show(this, "Ŭ���̾�Ʈ ��ΰ� �����Ǿ� ���� �ʽ��ϴ�.\n���� â���� �̵��Ͻðڽ��ϱ�?", "��� ������", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                if (dr == DialogResult.OK) { OpenClientSettingDialog(); }
                return;
            }

            if (false == ValidationPath(client_path, server)) return;
            
            string version_current = Form_Patcher.GetCurrentVersion(this, ConfigManager.getConfig(configKey + '1'));
            string version_latest = Form_Patcher.GetLatestVersion_Safe(this, url_vsn);
            if (version_current != version_latest) {
                DialogResult dr = DialogResult.No;
                bool update = false;
                if (!bool.Parse(ConfigManager.getConfig("is_auto_update"))) {
                    Logger.Log("Log : " + "�Ż� ������Ʈ�� �����Ͽ� ���α׷��� ��ġ ����� ����Ͽ� ��ġ�� ������ ���θ� ���� �޽��� ���");
                    dr = MessageBox.Show(this, "�Ż� ������Ʈ�� �����մϴ�! (" + version_current + "->" + version_latest + ")\n���α׷� ����� ����Ͽ� ������Ʈ �Ͻðڽ��ϱ�?\n�Ż� �����̼��� ���� ��ġ ���α׷�����\n�� ������ ������Ʈ �����մϴ�.",
                        "�Ż� ��ġ", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                } else {
                    update = true;
                }

                if (dr == DialogResult.Yes || update == true) {
                    Logger.Log("Log : " + "���α׷��� ��ġ ����� ����Ͽ� ��ġ�Ѵٰ� ����(�Ǵ� �ڵ�) �Ͽ� ��ġ���� ���");
                    this.BeginInvoke(() => {
                        Form backgroundForm = InitBackgroundForm(this);

                        bool isTest = (server == "test") ? true : false;
                        Form_Patcher_v2 form_Patcher = new Form_Patcher_v2(isTest) {
                            Owner = this
                        };

                        try {
                            backgroundForm.Show();
                            form_Patcher.ShowDialog();
                        } catch (Exception ex) {
                            Trace.WriteLine(ex.StackTrace);
                        } finally {
                            backgroundForm.Dispose();
                        }
                    });
                    return;
                }
                Logger.Log("Log : " + "����ڰ� �Ϲ� ��ġ�� ������");
                Trace.WriteLine("�Ϲ� ��ġ�� �����Ͽ����ϴ�.");
            }
            
            try {
                //�ش� Ŭ���̾�Ʈ�� ��θ� ������Ʈ���� ��Ͻ�ŵ�ϴ�.
                RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\JOYON\Gersang\Korean", RegistryKeyPermissionCheck.ReadWriteSubTree);
                if (registryKey != null) {
                    registryKey.SetValue(regKey, client_path);
                    registryKey.Close();
                }
            } catch (Exception ex) {
                Logger.Log("Exception : " + "�Ż� Ŭ���̾�Ʈ ��θ� ������Ʈ���� ����ϴ� �� ���ܰ� �߻�\r\n  :" + ex.Message);
                Trace.WriteLine(ex.Message);
                Trace.WriteLine(ex.StackTrace);
            }

            string? value = null;
            try {
                //Gersang Game Starter ��ΰ� ����Ǿ��ִ� ������Ʈ�� ��ο� �����Ͽ� ��θ� ����ϴ�.
                value = Registry.ClassesRoot.OpenSubKey("Gersang").OpenSubKey("shell").OpenSubKey("open").OpenSubKey("command").GetValue("").ToString();
            } catch (Exception ex) {
                Logger.Log("Exception : " + "���ӽ�Ÿ���� ��θ� ������Ʈ������ �޾ƿ��� �� ���ܰ� �߻�\r\n  :" + ex.Message);
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
                Logger.Log("Log : " + "�Ż� ������ ���� �� �Ż� ��Ÿ�͸� ����");
                await webView_main.ExecuteScriptAsync(@"startRetry = setTimeout(""socketStart('" + server + @"')"", 2000);"); //������ ���ϴ�.
                Process starter = new Process();
                starter.StartInfo.FileName = value.ToString();
                starter.EnableRaisingEvents = true;
                starter.Exited += (sender, e) => {
                    Trace.WriteLine("���� ��Ÿ�Ͱ� �����.");
                    activateWebSideFunction_invoke();
                };
                deactivateWebSideFunction();
                starter.Start();
            }
        }

        private bool ValidationPath(string client_path, string server) {
            Logger.Log("Log : " + "�Ż� ��� ��ȿ�� �˻縦 �õ�");
            string iniName;
            if (server == "main") { iniName = "GerSangKR.ini"; } 
            else { iniName = "GerSangKRTest.ini"; }

            if (!File.Exists(client_path + "\\" + "Gersang.exe")) {
                this.BeginInvoke(() => {
                    Logger.Log("Log : " + "�Ż� ��ΰ� ��ȿ���� �ʴٰ� �Ǵ��Ͽ� �ȳ��޽��� ��� (Gersang.exe ������ ����)");
                    MessageBox.Show(this, "�Ż� ��θ� �ٽ� �������ּ���.\n���� : Gersang.exe ������ �����ϴ�.", "���� �Ұ�", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                }); 
                return false;
            }

            if (!File.Exists(client_path + "\\" + iniName)) {
                string message;
                if (server == "main") { message = "���� ��ΰ� �ƴմϴ�."; }
                else { message = "�׼� ��ΰ� �ƴմϴ�."; }
                this.BeginInvoke(() => {
                    Logger.Log("Log : " + "�Ż� ��ΰ� ��ȿ���� �ʴٰ� �Ǵ��Ͽ� �ȳ��޽��� ��� (" + message + ")");
                    MessageBox.Show(this, "�Ż� ��θ� �ٽ� �������ּ���.\n���� : " + message, "���� �Ұ�", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                });
                return false;
            }

            return true;
        }

        private void OpenGersangStarterInstallDialog() {
            Logger.Log("MessageBox : " + "���ӽ�Ÿ�Ͱ� ��ġ�Ǿ����� �ʴٰ� �����ϰ�, �ٿ�ε� ��ũ�� �ȳ�");
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
            MaterialSwitch @switch = (MaterialSwitch)sender;
            Logger.Log("Click : " + @switch.Name);
            SwitchClick(@switch);
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
                Logger.Log("Log : " + "�ܼ� �α׾ƿ� �õ�");
                Trace.WriteLine("�α׾ƿ� �մϴ�.");
                webView_main.CoreWebView2.Navigate(url_logout);
                materialSwitch.CheckState = CheckState.Unchecked;
                currentState = State.None;
                currentClient = Client.None;
                return;
            }

            if (this.currentState == State.LoggedIn) {
                Logger.Log("Log : " + "Ÿ���� �α����� ���� ���� ������ �α׾ƿ� �õ�");
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
            Logger.Log("Click : " + button.Name);
            string? url = ConfigManager.getConfig("shortcut_" + button.Name.Substring(button.Name.Length - 1, 1));

            if (url == null || url.Equals("")) {
                DialogResult dr = MessageBox.Show("������ �ٷΰ��� ��ũ�� �����Ǿ� ���� �ʽ��ϴ�.\n����ȭ������ �̵��Ͻðڽ��ϱ�?", "�ٷΰ��� �̼���", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                if (dr == DialogResult.OK) { OpenShortcuttSettingDialog(); }
                return;
            }

            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute)) {
                Logger.Log("MessageBox : " + "������ �ٷΰ��� ��ũ�� �߸��� �������� �Ǵ��Ͽ� �ȳ��޽��� ���\r\n  :" + url);
                MessageBox.Show("�߸��� ��ũ ���� �Դϴ�. �ٽ� �������ּ���.");
                return;
            }

            webView_main.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;

            Form_Browser form = new Form_Browser(webView_main);
            form.Load += (sender, e) => {
                webView_main.CoreWebView2.Navigate(ConfigManager.getConfig("shortcut_" + button.Name.Substring(button.Name.Length - 1, 1)));
            };
            form.FormClosed += (sender, e) => {
                webView_main.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                webView_main.CoreWebView2.Navigate(url_main);
                form.Controls.Clear();
            };
            form.ShowDialog();
        }

        private void materialComboBox_account_SelectedIndexChanged(object sender, EventArgs e) {
            MaterialComboBox comboBox = (MaterialComboBox)sender;
            Logger.Log("CheckedChanged : " + comboBox.Name + "->" + comboBox.SelectedIndex);

            string id = ConfigManager.getKeyByValue(comboBox.Text).Replace("_nickname", string.Empty);
            if (id == "") id = comboBox.Text;
            string switchTag;
            Trace.WriteLine(id);
            if(id.Contains("���þ���")) { switchTag = id; }
            else { switchTag = id + ";" + ConfigManager.getConfig(id); }

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
        }

        private void LoadAccountComboBox() {
            Logger.Log("Log : " + "���� �޺��ڽ� �ε�");
            materialComboBox_account_1.Items.Clear();
            materialComboBox_account_2.Items.Clear();
            materialComboBox_account_3.Items.Clear();

            string temp = ConfigManager.getConfig("account_list");
            string[] accountList;
            if (temp.Length != 0) {
                accountList = temp.Remove(temp.Length - 1, 1).Split(';');
            } else {
                accountList = Array.Empty<string>();
            }

            materialComboBox_account_1.Items.Add("���þ���");
            materialComboBox_account_2.Items.Add("���þ���");
            materialComboBox_account_3.Items.Add("���þ���");

            foreach (var item in accountList) {
                string id = ConfigManager.getConfig(item + "_nickname");
                if (id == "") id = item;
                materialComboBox_account_1.Items.Add(id);
                materialComboBox_account_2.Items.Add(id);
                materialComboBox_account_3.Items.Add(id);
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

        private void materialButton_debugging_Click(object sender, EventArgs e) {
            Logger.Log("Click : " + materialButton_debugging.Name);

            webView_main.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;

            Form_Browser form = new Form_Browser(webView_main);
            form.FormClosed += (sender, e) => {
                webView_main.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                webView_main.CoreWebView2.Navigate(url_main);
                form.Controls.Clear();
            };

            form.Show();
        }

        private void LoadShortcut() {
            Logger.Log("Log : " + "������ �ٷΰ��� ���� �ε�");
            string[] names = ConfigManager.getConfig("shortcut_name").Split(';');
            materialButton_shortcut_1.Text = names[0];
            materialButton_shortcut_2.Text = names[1];
            materialButton_shortcut_3.Text = names[2];
            materialButton_shortcut_4.Text = names[3];
        }

        private void materialButton_naver_Click(object sender, EventArgs e) {
            MaterialButton searchButton = (MaterialButton)sender;
            Logger.Log("Click : " + searchButton.Name);
            SearchClick(searchButton);
        }

        private void SearchClick(MaterialButton searchButton) {
            MaterialSwitch loginSwitch;

            if (searchButton.Equals(materialButton_search_1)) { loginSwitch = materialSwitch_login_1; } 
            else if (searchButton.Equals(materialButton_search_2)) { loginSwitch = materialSwitch_login_2; } 
            else { loginSwitch = materialSwitch_login_3; }

            isSearch = true;
            if (true == loginSwitch.Checked) { webView_main.CoreWebView2.Navigate(url_search); } //���̹� �˻�
            else { SwitchClick(loginSwitch); }
        }

        private void radio_preset_CheckedChanged(object sender, EventArgs e) {
            MaterialRadioButton radio = (MaterialRadioButton)sender;
            Logger.Log("CheckedChanged : " + radio.Name + "->" + radio.Checked);
            if (radio.Checked == false) { return; }
            string? value = radio.Tag.ToString();
            if (value == null) {
                MessageBox.Show("RadioButton�� Tag�� Null�Դϴ�.");
                return;
            }

            ConfigManager.setConfig("current_preset", value);
            LoadAccountComboBox();
        }

        private void LoadRadioButton() {
            Logger.Log("Log : " + "���� ��ư �ε�");
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
            Logger.Log("Event : " + "Resize -> " + this.WindowState);
            Trace.WriteLine("Form Resize! : " + this.WindowState);
            if(this.WindowState == FormWindowState.Minimized) {
                //this.WindowState = FormWindowState.Normal;
            }
        }

        private async void Login(string id, string protected_pw) {
            Logger.Log("Log : " + "�α��� �õ�");
            try {
                await webView_main.ExecuteScriptAsync("document.getElementById('GSuserID').value = '" + id + "'");
                await webView_main.ExecuteScriptAsync("document.getElementById('GSuserPW').value = '" + EncryptionSupporter.Unprotect(protected_pw) + "'");
            } catch (CryptographicException e) {
                //����ڰ� ��ȣȭ�� �н����尡 ���Ե� ���������� Ÿ PC�� ���� �� ��� �� �߻��ϴ� ����
                Trace.WriteLine(e.Message);
                MessageBox.Show("���������� Ÿ PC�� �ű� ������ Ȯ�εǾ����ϴ�.\n���� ���� ���� ������ ���� ��� ���� ������ �ʱ�ȭ �մϴ�.", "�н����� ��ȣȭ ����", MessageBoxButtons.OK, MessageBoxIcon.Error);
                currentClient = Client.None;
                currentState = State.None;
                ClearAccount();
                return;
            }

            await webView_main.ExecuteScriptAsync("document.getElementById('btn_Login').click()");
            currentState = State.LoggedIn;
        }

        private void ClearAccount() {
            Logger.Log("Log : " + "���� �ʱ�ȭ �õ�");
            string temp = ConfigManager.getConfig("account_list");
            string[] account_list = temp.Remove(temp.Length - 1, 1).Split(';');
            foreach (var item in account_list) {
                ConfigManager.removeConfig(item);
                ConfigManager.removeConfig(item + "_nickname");
            }
            ConfigManager.setConfig("account_list", "");

            ConfigManager.setConfig("current_comboBox_index_preset_1", "0;0;0");
            ConfigManager.setConfig("current_comboBox_index_preset_2", "0;0;0");
            ConfigManager.setConfig("current_comboBox_index_preset_3", "0;0;0");
            ConfigManager.setConfig("current_comboBox_index_preset_4", "0;0;0");
            LoadAccountComboBox();
        }

        private void materialButton_setting_Click(object sender, EventArgs e) {
            CustomButton button = (CustomButton)sender;
            Logger.Log("Click : " + button.Name);
            if (button.Equals(materialButton_setting_account)) {
                OpenAccountSettingDialog();
            } else if (button.Equals(materialButton_setting_client)) {
                OpenClientSettingDialog();
            } else if (button.Equals(materialButton_setting_shortcut)) {
                OpenShortcuttSettingDialog();
            } else if (button.Equals(materialButton_setting_advanced)) {
                OpenAdvancedSettingDialog();
            }
        }

        private void OpenAdvancedSettingDialog() {
            Form backgroundForm = InitBackgroundForm(this);

            Form_AdvancedSetting dialog_shortcutSetting = new Form_AdvancedSetting() {
                Owner = this
            };

            try {
                backgroundForm.Show();
                dialog_shortcutSetting.ShowDialog();
            } catch (Exception ex) {
                Trace.WriteLine(ex.StackTrace);
            } finally {
                backgroundForm.Dispose();
            }
        }

        private void OpenShortcuttSettingDialog() {
            Form backgroundForm = InitBackgroundForm(this);

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
            Form backgroundForm = InitBackgroundForm(this);

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
            Form backgroundForm = InitBackgroundForm(this);

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

        private void materialCheckbox_testServer_CheckedChanged(object sender, EventArgs e) {
            MaterialCheckbox checkbox = (MaterialCheckbox)sender;
            Logger.Log("CheckedChanged : " + checkbox.Name + "->" + checkbox.Checked);
            ConfigManager.setConfig("is_test_server", ((MaterialCheckbox)sender).Checked.ToString());
        }

        public static Form InitBackgroundForm(Form owner) {
            Form backgroundForm = new Form() {
                StartPosition = FormStartPosition.Manual,
                FormBorderStyle = FormBorderStyle.None,
                Opacity = .50d,
                BackColor = Color.Black,
                Location = owner.Location,
                Size = owner.Size,
                ShowInTaskbar = false,
                TopMost = true,
                Owner = owner
            };
            return backgroundForm;
        }

        private void materialButton_kakao_Click(object sender, EventArgs e) {
            Logger.Log("Click : " + materialButton_kakao.Name);
            Process.Start(new ProcessStartInfo("https://open.kakao.com/o/sXJQ1qPd") { UseShellExecute = true });
        }

        private void materialButton_patchNote_Click(object sender, EventArgs e) {
            Logger.Log("Click : " + materialButton_patchNote.Name);
            Process.Start(new ProcessStartInfo(url_release) { UseShellExecute = true });
        }

        private void materialButton_blog_Click(object sender, EventArgs e) {
            Logger.Log("Click : " + materialButton_blog.Name);
            Process.Start(new ProcessStartInfo("https://blog.naver.com/kog5071/222644960946") { UseShellExecute = true });
        }

        private void materialButton_gitHub_Click(object sender, EventArgs e) {
            Logger.Log("Click : " + materialButton_gitHub.Name);
            Process.Start(new ProcessStartInfo("https://github.com/byungmeo/GersangStation") { UseShellExecute = true });
        }

        private void button_tray_Click(object sender, EventArgs e) {
            Logger.Log("Click : " + button_tray.Name);
            notifyIcon1.Visible = true;
            notifyIcon1.BalloonTipTitle = "�˸�";
            notifyIcon1.BalloonTipText = "���α׷��� Ʈ���̷� �̵��Ǿ����ϴ�.";
            notifyIcon1.ShowBalloonTip(5000);
            this.Hide();
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e) {
            Logger.Log("DoubleClick : " + "notifyIcon1");
            notifyIcon1.Visible = false;
            this.Show();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            Logger.Log("Click : " + linkLabel1.Name);
            Process.Start(new ProcessStartInfo("https://logomakr.com/app") { UseShellExecute = true });
        }

        private void contextMenuStrip_tray_ItemClicked(object sender, ToolStripItemClickedEventArgs e) {
            ToolStripItem item = e.ClickedItem;
            if (item.Equals(toolStripMenuItem_open)) {
                Logger.Log("ItemClicked : " + "(contextMenuStrip_tray) " + "toolStripMenuItem_open");
                notifyIcon1.Visible = false;
                this.Show();
            } else if (item.Equals(toolStripMenuItem_exit)) {
                Logger.Log("ItemClicked : " + "(contextMenuStrip_tray) " + "toolStripMenuItem_exit");
                System.Windows.Forms.Application.Exit();
            }
        }

        private void toolStripMenuItem_client_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
            ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
            ToolStripItem item = e.ClickedItem;
            Logger.Log("ItemClicked : " + "(" + menuItem.Name + ") " + item.Name);

            if (menuItem.Equals(toolStripMenuItem_client_1)) {
                if (item.Equals(toolStripMenuItem_search_1)) { SearchClick(materialButton_search_1); } 
                else if (item.Equals(toolStripMenuItem_start_1)) { StartClick(materialButton_start_1); }
            } else if (menuItem.Equals(toolStripMenuItem_client_2)) {
                if (item.Equals(toolStripMenuItem_search_2)) { SearchClick(materialButton_search_2); } 
                else if (item.Equals(toolStripMenuItem_start_2)) { StartClick(materialButton_start_2); }
            } else if ( menuItem.Equals(toolStripMenuItem_client_3)) {
                if (item.Equals(toolStripMenuItem_search_3)) { SearchClick(materialButton_search_3); } 
                else if (item.Equals(toolStripMenuItem_start_3)) { StartClick(materialButton_start_3); }
            }
        }
    } //Form1
} //namespace