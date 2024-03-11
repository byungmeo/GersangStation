﻿using GersangStation.Modules;
using MaterialSkin;
using MaterialSkin.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;
using Octokit;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using File = System.IO.File;

namespace GersangStation
{
    public partial class Form1 : MaterialForm {
        private const int WM_ACTIVATEAPP = 0x001C;
        private const int WM_HOTKEY = 0x0312;

        protected override void WndProc(ref Message m) {
            if(m.Msg == WM_HOTKEY && bool.Parse(ConfigManager.getConfig("use_clip_toggle_hotkey"))) {
                if(m.WParam == (IntPtr)ClipMouse.GetHotKeyId()) {
                    if(ClipMouse.isRunning()) {
                        ClipMouse.Stop(false);
                        ConfigManager.setConfig("use_clip_mouse", false.ToString());
                    } else {
                        ClipMouse.Run();
                        ConfigManager.setConfig("use_clip_mouse", true.ToString());
                    }

                    if(materialCheckbox_mouseClip != null)
                        materialCheckbox_mouseClip.Checked = bool.Parse(ConfigManager.getConfig("use_clip_mouse"));
                }
            }

            base.WndProc(ref m);
            if(WindowState == FormWindowState.Minimized && m.Msg == WM_ACTIVATEAPP && m.WParam == IntPtr.Zero) {
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

        private const string url_main = "https://www.gersang.co.kr/main/index.gs?";
        private const string url_logout = "https://www.gersang.co.kr/member/logoutProc.gs";
        private const string url_installStarter = "https://akgersang.xdn.kinxcdn.com//PatchFile/Gersang_Web/GersangStarterSetup.exe";

        // 검색 이벤트 종료로 인해 주석 처리
        /*
        private const string url_search = "https://search.naver.com/search.naver?&query=거상";
        private const string url_search_gersang = "http://www.gersang.co.kr/main.gs";
        private bool isSearch = false;
        private bool isGetSearchItem = false;
        private bool isExceptSearch = false; //2022-04-26 거상 홈페이지 검색 시 이벤트 페이지로 바로 넘어가는 경우
        private string previousUrl = "";
        */

        private const string url_main_vsn = @"https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/" + @"Client_Patch_File/" + @"Online/vsn.dat.gsz";
        private const string url_test_vsn = @"https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Test_Server/" + @"Client_Patch_File/" + @"Online/vsn.dat.gsz";

        private const string url_release = "https://github.com/byungmeo/GersangStation/releases/latest";

        private bool isWebFunctionDeactivated = false;
        private bool isGameStartLogin = false;

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
            };
            webView_main.CoreWebView2InitializationCompleted += webView_main_CoreWebView2InitializationCompleted;

            var path = Path.Combine(Path.GetTempPath(), $"GersangStation");
            try {
                Logger.Log($"Webivew2 UserDataFolder {path}");
                var env = await CoreWebView2Environment.CreateAsync(userDataFolder: path);
                await webView_main.EnsureCoreWebView2Async(env);
            } catch(WebView2RuntimeNotFoundException ex) {
                DialogResult dr = MessageBox.Show("다클라 스테이션을 이용하기 위해선\nWebView2 런타임을 반드시 설치하셔야 합니다.\n설치 하시겠습니까? (설치 링크에 자동으로 접속합니다.)", "런타임 설치 필요", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if(dr == DialogResult.Yes) {
                    Process.Start(new ProcessStartInfo("https://go.microsoft.com/fwlink/p/?LinkId=2124703") { UseShellExecute = true });
                }
                System.Windows.Forms.Application.Exit();
                return;
            }
        }

        // EnsureCoreWebView2Async의 결과값이 null이 아니라면 CoreWebView2 초기화가 완료되기 직전 이 이벤트가 발생합니다.
        private void webView_main_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e) {
            if(e.IsSuccess) {
                //webView_main.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested; //Edge 보안 업데이트로 인해 NewWindow 로직 제거
                webView_main.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false; //Alert 등의 메시지창이 뜨지않고 ScriptDialogOpening 이벤트를 통해 제어할 수 있도록 합니다.
                webView_main.CoreWebView2.ScriptDialogOpening += CoreWebView2_ScriptDialogOpening;

                webView_main.NavigationStarting += webView_main_NavigationStarting;
                webView_main.NavigationCompleted += webView_main_NavigationCompleted;
                webView_main.CoreWebView2.Navigate(url_main);

                LoadComponent();
            } else {
                Trace.WriteLine(e.InitializationException.StackTrace);
                if(e.InitializationException is DllNotFoundException) {
                    DialogResult dr = MessageBox.Show("실행 파일의 위치가 잘못되었습니다.\n확인 버튼을 누르면 열리는 홈페이지를 참고해주세요.", "잘못된 실행 파일 위치", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    if(dr == DialogResult.OK) {
                        Process.Start(new ProcessStartInfo("https://github.com/byungmeo/GersangStation/discussions/2") { UseShellExecute = true });
                    }
                } else if(e.InitializationException is WebView2RuntimeNotFoundException) {
                    DialogResult dr = MessageBox.Show("다클라 스테이션을 이용하기 위해선\nWebView2 런타임을 반드시 설치하셔야 합니다.\n설치 하시겠습니까? (설치 링크에 자동으로 접속합니다.)", "런타임 설치 필요", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if(dr == DialogResult.Yes) {
                        Process.Start(new ProcessStartInfo("https://go.microsoft.com/fwlink/p/?LinkId=2124703") { UseShellExecute = true });
                    }
                } else {
                    Logger.Log("WebView2 초기화 실패", e.InitializationException);
                    DialogResult dr = MessageBox.Show("WebView2 초기화 중 오류 발생하였습니다. 문의해주세요.\n" + e.InitializationException.Message, "WebView2 초기화 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    if(dr == DialogResult.OK) {
                        Process.Start(new ProcessStartInfo("https://github.com/byungmeo/GersangStation/discussions/3") { UseShellExecute = true });
                    }
                }
                System.Windows.Forms.Application.Exit();
                return;
            }
        }

        private async void LoadComponent() {
            ConfigManager.Validation();
            LoadCheckBox();
            LoadRadioButton();
            LoadAccountComboBox();
            LoadShortcut();
            SetToolTip();
            CheckAccount();
            LoadClipMouse();

#if DEBUG
            linkLabel_announcement.Text = "디버그 모드";
#else
            // 깃허브에 있는 공지사항 및 릴리즈 정보 등을 가져옴
            try
            {
                GitHubClient client = new GitHubClient(new ProductHeaderValue("Byungmeo"));
                IReadOnlyList<Release> releases = await client.Repository.Release.GetAll("byungmeo", "GersangStation");
                Readme r = await client.Repository.Content.GetReadme("byungmeo", "GersangStation");

                CheckProgramUpdate(releases);
                LoadAnnouncements(r);
                LoadSponsors(r);
            }
            catch (Exception ex)
            {
                linkLabel_announcement.Text = "공지사항을 불러오는데 실패하였습니다";
                MessageBox.Show(this, "프로그램 업데이트 확인 도중 에러가 발생하였습니다.\n에러 메시지를 캡쳐하고, 문의 부탁드립니다.", "업데이트 확인 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                MessageBox.Show(this, "에러 메시지1 : \n" + ex.Message);
                MessageBox.Show(this, "에러 메시지2 : \n" + ex.ToString());
                Trace.WriteLine(ex.Message);
            }
#endif

        }

        private void LoadClipMouse() {
            // 단축키 TextBox도 함께 초기화 
            string hotKey = ConfigManager.getConfig("clip_toggle_hotkey");
            if(hotKey.Contains(',')) {
                string[] comb = hotKey.Split(',');
                string mod = comb[0];
                string key = ((Keys)int.Parse(comb[1])).ToString();
                textBox_clipToggleHotKey.Text = mod + " + " + key;
            } else textBox_clipToggleHotKey.Text = ((Keys)int.Parse(hotKey)).ToString();

            ClipMouse.icon = notifyIcon2;
            ClipMouse.RegisterHotKey(this.Handle, hotKey);
        }

        private void LoadSponsors(Readme r) {
            string content = r.Content;
            string[] sponsors = content.Substring(content.LastIndexOf("<summary>후원해주신 분들</summary>")).Split("<br>");

            // 첫 번째와 마지막은 태그라 무시
            for(int i = 1; i < sponsors.Length - 1; i++) {
                materialListBox_sponsor.AddItem(new MaterialListBoxItem(sponsors[i]));
            }
            materialListBox_sponsor.AddItem(new MaterialListBoxItem("감사합니다"));
        }

        private void LoadAnnouncements(Readme r) {
            string content = r.Content;
            string[] announcements = content.Substring(content.LastIndexOf("# 공지사항")).Split('\n');
            if(announcements.Length <= 1) {
                linkLabel_announcement.Text = "공지사항이 없습니다";
            } else {
                try {
                    string latestAnnouncement = announcements[1];
                    string title = latestAnnouncement.Split('{')[0];
                    int startIndex = latestAnnouncement.LastIndexOf('{') + 1;
                    int length = latestAnnouncement.LastIndexOf('}') - startIndex;
                    string pageNumber = latestAnnouncement.Substring(startIndex, length);
                    string url = "https://github.com/byungmeo/GersangStation/discussions/" + pageNumber;
                    linkLabel_announcement.Text = title;
                    linkLabel_announcement.Click += (sender, e) => {
                        Trace.Write(pageNumber + "번 공지사항 접속");
                        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    };

                    // 만약 이전과 다른 공지사항이 새롭게 게시되었다면 사용자에게 메시지를 출력합니다.
                    string prevLink = ConfigManager.getConfig("prev_announcement");
                    if(prevLink == "" || prevLink != url) {
                        ConfigManager.setConfig("prev_announcement", url);
                        DialogResult dr = MessageBox.Show($"새로운 공지사항이 게시되었습니다.\n공지제목 :{title.Substring(title.LastIndexOf(']') + 1)}\n확인하시겠습니까?", "새로운 공지사항", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        if(dr == DialogResult.Yes) {
                            Trace.Write(pageNumber + "번 공지사항 접속");
                            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                        }
                    }
                } catch {
                    linkLabel_announcement.Text = "공지사항 로딩 실패";
                }
            }
        }

        private void SetToolTip() {
            toolTip1.Active = true;
            /**
             * <-- 메인화면 -->
             */
            toolTip1.SetToolTip(button_tray, "트레이에 숨기기");
            toolTip1.SetToolTip(radio_preset_1, "1번 세팅");
            toolTip1.SetToolTip(radio_preset_2, "2번 세팅");
            toolTip1.SetToolTip(radio_preset_3, "3번 세팅");
            toolTip1.SetToolTip(radio_preset_4, "4번 세팅");
            toolTip1.SetToolTip(materialCheckbox_testServer, "활성화 시 테스트 서버로 실행합니다.\n(설치 별도)");
            toolTip1.SetToolTip(materialButton_debugging, "작동하는 브라우저 직접 보기");
            toolTip1.SetToolTip(materialComboBox_account_1, "본클라 계정 선택");
            toolTip1.SetToolTip(materialComboBox_account_2, "2클라 계정 선택");
            toolTip1.SetToolTip(materialComboBox_account_3, "3클라 계정 선택");
            toolTip1.SetToolTip(materialSwitch_login_1, "본클라 홈페이지 로그인");
            toolTip1.SetToolTip(materialSwitch_login_2, "2클라 홈페이지 로그인");
            toolTip1.SetToolTip(materialSwitch_login_3, "3클라 홈페이지 로그인");
            /* 검색 이벤트 종료
            toolTip1.SetToolTip(materialButton_search_1, "본클라 검색보상 수령");
            toolTip1.SetToolTip(materialButton_search_2, "2클라 검색보상 수령");
            toolTip1.SetToolTip(materialButton_search_3, "3클라 검색보상 수령");
            */
            toolTip1.SetToolTip(materialButton_start_1, "본클라 게임 실행");
            toolTip1.SetToolTip(materialButton_start_2, "2클라 게임 실행");
            toolTip1.SetToolTip(materialButton_start_3, "3클라 게임 실행");
            string shortcut_1 = ConfigManager.getConfig("shortcut_1");
            if(shortcut_1 == "") shortcut_1 = "링크가 설정되지 않았습니다.";
            string shortcut_2 = ConfigManager.getConfig("shortcut_2");
            if(shortcut_2 == "") shortcut_2 = "링크가 설정되지 않았습니다.";
            string shortcut_3 = ConfigManager.getConfig("shortcut_3");
            if(shortcut_3 == "") shortcut_3 = "링크가 설정되지 않았습니다.";
            string shortcut_4 = ConfigManager.getConfig("shortcut_4");
            if(shortcut_4 == "") shortcut_4 = "링크가 설정되지 않았습니다.";
            toolTip1.SetToolTip(materialButton_shortcut_1, shortcut_1);
            toolTip1.SetToolTip(materialButton_shortcut_2, shortcut_2);
            toolTip1.SetToolTip(materialButton_shortcut_3, shortcut_3);
            toolTip1.SetToolTip(materialButton_shortcut_4, shortcut_4);

            toolTip1.SetToolTip(img_help_clip,
                "창모드 환경에서 마우스 가두기를 하였음에도 마우스 커서가 밖으로 삐져나오는 현상을 개선합니다." +
                "\n\n※ 게임 내 마우스 가두기 기능을 OFF 하시고 사용하셔야 합니다." +
                "\n\n(기본값)F11: 마우스 가두기 ON, OFF\nAlt: 일시적으로 OFF");

            toolTip1.SetToolTip(img_help_integrity,
                "거상 설치 폴더에 손상되거나 누락된 파일이 있는지 확인합니다." +
                "\n거상 실행 시 오류가 발생하는 경우 유용합니다.");
        }

        private void CheckProgramUpdate(IReadOnlyList<Release> releases) {
            //버전 업데이트 시 Properties -> AssemblyInfo.cs 의 AssemblyVersion과 AssemblyFileVersion을 바꿔주세요.
            string version_current = Assembly.GetExecutingAssembly().GetName().Version.ToString().Substring(0, 5);
            Trace.WriteLine(version_current);

            int ver_idx;
            for(ver_idx = 0; ver_idx < releases.Count; ++ver_idx) {
                if(releases[ver_idx].Prerelease == false) break;
            }
            Release release = releases[ver_idx];
            string version_latest = release.TagName;
            label_version_current.Text = label_version_current.Text.Replace("0.0.0", version_current);
            label_version_latest.Text = label_version_latest.Text.Replace("0.0.0", version_latest);

            //깃허브에 게시된 마지막 버전과 현재 버전을 초기화 합니다.
            //Version latestGitHubVersion = new Version(releases[0].TagName);
            Version latestGitHubVersion = new Version(version_latest);
            Version localVersion = new Version(version_current);
            Trace.WriteLine("깃허브에 마지막으로 게시된 버전 : " + latestGitHubVersion);
            Trace.WriteLine("현재 프로젝트 버전 : " + localVersion);

            // 업데이트 알림
            string msg = release.Body;
            if(msg.Contains("<!--DIALOG-->") && msg.Contains("<!--END-->")) {
                // <!--DIALOG-->와 <!--END--> 사이의 내용만 가져옵니다.
                int start = msg.IndexOf("<!--DIALOG-->") + "<!--DIALOG-->".Length + 2;
                int end = msg.IndexOf("<!--END-->") - start;
                msg = msg.Substring(start, end);
            }

            int versionComparison = localVersion.CompareTo(latestGitHubVersion);
            if(versionComparison < 0) {
                Trace.WriteLine("구버전입니다! 업데이트 메시지박스를 출력합니다!");

                DialogResult dr = MessageBox.Show(msg + "\n\n업데이트 하시겠습니까? (GitHub 접속)",
                    "업데이트 안내", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if(dr == DialogResult.Yes) {
                    Process.Start(new ProcessStartInfo(url_release) { UseShellExecute = true });
                }
            } else if(versionComparison > 0) {
                Trace.WriteLine("깃허브에 릴리즈된 버전보다 최신입니다!");
            } else {
                Trace.WriteLine("현재 버전은 최신버전입니다!");
            }
        }

        private void CheckAccount() {
            if(materialComboBox_account_1.Items.Count <= 1) {
                DialogResult dr = MessageBox.Show("현재 저장된 계정이 하나도 없습니다.\n계정 설정 화면으로 이동하시겠습니까?", "계정 없음", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                if(dr == DialogResult.OK) {
                    OpenAccountSettingDialog();
                }
            }
        }

        private void LoadCheckBox() {
            // 메인 탭
            materialCheckbox_testServer.Checked = bool.Parse(ConfigManager.getConfig("is_test_server"));

            // 추가 기능 탭
            materialCheckbox_mouseClip.Checked = bool.Parse(ConfigManager.getConfig("use_clip_mouse"));
            checkBox_clipDisableHotKey.Checked = bool.Parse(ConfigManager.getConfig("use_clip_disable_hotkey"));
            checkBox_clipToggleHotKey.Checked = bool.Parse(ConfigManager.getConfig("use_clip_toggle_hotkey"));
            checkBox_onlyFirstClip.Checked = bool.Parse(ConfigManager.getConfig("use_clip_only_first"));
        }

        /* Edge 보안 업데이트로 인해 로직 제거
        private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e) {
            if (sender != null) e.NewWindow = (CoreWebView2)sender; //WebView2가 죽고 새로운 창이 뜨는 대신 WebView2에서 모든 것을 진행
            //e.Handled = true; //true면 새로운 창이 뜨는 걸 취소
        }
        */

        private async void CoreWebView2_ScriptDialogOpening(object? sender, CoreWebView2ScriptDialogOpeningEventArgs e) {
            string message = e.Message;
            Trace.WriteLine(message);
            Trace.WriteLine("대화창 종류 : " + e.Kind);

            if(e.Kind == CoreWebView2ScriptDialogKind.Confirm) {
                Trace.WriteLine("선택지가 있는 대화상자 판정");
                DialogResult dr = DialogResult.None;
                var task = Task.Run(() => {
                    dr = MessageBox.Show(message, "선택", MessageBoxButtons.YesNo, MessageBoxIcon.Information,
                        MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                });
                await task;
                if(dr == DialogResult.Yes) {
                    e.Accept();
                }

                return;
            }

            this.BeginInvoke(async () => {
                //message가 정확히 "5초 후에 재로그인 가능합니다." 일 경우, 사용자가 로그인 실패 후 5초 이내에 로그인을 시도한 경우입니다.
                if(message.Equals("5초 후에 재로그인 가능합니다.")) {
                    Trace.WriteLine("로그인 실패 후 5초 안에 로그인 시도 판정");
                    MessageBox.Show("로그인 실패 후 아직 5초가 지나지 않았습니다.\n5초 후에 다시 로그인을 시도해주세요.");
                    currentState = State.None;
                    currentClient = Client.None;
                    webView_main.Source = new Uri("https://www.gersang.co.kr/main/index.gs");
                }

                //otp 인증번호가 틀릴 시 다시 입력하도록 합니다.
                else if(message.Contains("인증번호가 다릅니다")) {
                    Trace.WriteLine("잘못된 OTP 코드 입력 판정");
                    MessageBox.Show("잘못된 OTP 코드를 입력하였습니다. 다시 입력해주세요.");
                    string? otpCode = showDialogOtp();

                    Trace.WriteLine("otpCode : " + otpCode);
                    if(otpCode == null) {
                        MessageBox.Show("OTP 코드를 입력하지 않았습니다.");
                    } else {
                        await webView_main.ExecuteScriptAsync("document.getElementById('GSotpNo').value = '" + otpCode + "'");
                        await webView_main.ExecuteScriptAsync("document.getElementById('btn_Send').click()");
                    }
                }

                // 검색 보상 이벤트 종료로 인해 주석처리
                /*
                //검색 보상 지급 메시지
                else if (message.Contains("아이템이 지급되었습니다.") || message.Contains("이미 아이템을 수령하셨습니다.") || message.Contains("참여 시간이 아닙니다.")) {
                    MessageBox.Show(message);
                    if (e.Uri.Contains("attendance") && true == isGetSearchItem) {
                        webView_main.CoreWebView2.Navigate(url_main);
                        isGetSearchItem = false;
                    }
                }
                */

                //아이디 또는 비밀번호가 틀린 경우입니다.
                else if(message.Contains("아이디 또는 비밀번호 오류")) {
                    currentState = State.None;
                    currentClient = Client.None;
                    webView_main.Source = new Uri("https://www.gersang.co.kr/main/index.gs");
                    Trace.WriteLine("로그인 실패 판정");
                    MessageBox.Show("로그인에 실패하였습니다. \nID/PW 재확인 후 다시 로그인 해주세요.\n(잘 되던 계정이 갑자기 안되면 계정 설정을 다시 해보세요.)");
                } else {
                    Trace.WriteLine("예외로 처리되지 않은 메시지 판정");
                    MessageBox.Show($"{message}");
                }
            });
        }
        private void webView_main_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e) {
            Trace.WriteLine("NavigationStarting : " + e.Uri.ToString());
            Trace.WriteLine("NavigationStarting Previous URL : " + webView_main.Source);
            //previousUrl = webView_main.CoreWebView2.Source; // 검색 이벤트 종료
            deactivateWebSideFunction();
        }

        private void webView_main_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) {
            /*
                1. 스위치로 로그인 하는 경우
                2. 스위치로 로그아웃 하는 경우
                3. 스위치로 로그아웃 -> 로그인 하는 경우 (2)
                4. (로그인 된 상태에서) 검색버튼으로 검색보상 수령
                5. (로그인 된 상태에서) 실행버튼으로 실행
            */

            if(sender == null) {
                this.BeginInvoke(() => { MessageBox.Show("NavigationFailed : sender is NULL"); });
                return;
            }

            if(!e.IsSuccess) {
                deactivateWebSideFunction(); //WebView2 활용 기능 비활성화 처리
                this.BeginInvoke(() => { handleWebError(e.WebErrorStatus); });
                return;
            } else { if(isWebFunctionDeactivated) { activateWebSideFunction(); } } //WebView2 활용 기능이 비활성화 상태인 경우 활성화 처리

            string? url = ((WebView2)sender).Source.ToString();
            Trace.WriteLine("NavigationCompleted : " + url);

            // 비밀번호 변경 안내 페이지라면, "다음에 변경하기" 클릭
            if(url.Contains("pw_reset.gs")) {
                // https://www.gersang.co.kr/member/pw_reset.gs?returnUrl=www.gersang.co.kr/main/index.gs
                string returnUrl = url.Substring(url.IndexOf("returnUrl=") + 10);
                doPwReset(returnUrl);
                return;
            }

            // 휴대폰 본인인증 페이지라면, 본인인증 안내창 띄우기
            if(url.Contains("loginCertUp.gs")) {
                this.BeginInvoke(() => {
                    MessageBox.Show("휴대폰 본인 인증이 필요합니다.\n크롬 또는 엣지 등의 웹브라우저를 통해\n거상 로그인 후 본인인증을 완료해주세요.");
                });
                return;
            }

            if(url.Contains("otp.gs")) {
                this.BeginInvoke(() => {
                    string? otpCode = showDialogOtp();

                    if(otpCode == null) {
                        MessageBox.Show("OTP 코드를 입력하지 않았습니다.");
                        //isSearch = false; // 검색 이벤트 종료
                    } else { doOtpInput(otpCode); }
                });
                return;
            }

            // 검색 이벤트 종료
            /*
            if (url.Contains("search.naver")) {
                if (isSearch) { doNavigateGersangSite(); }
                return;
            }

            if (url.Contains("attendance")) {
                if (isSearch) { doGetEventItem(); }
                //else { webView_main.CoreWebView2.Navigate(url_main); }
                return;
            }
            */

            if(url.Contains("main/index.gs")) {
                // 검색 이벤트 종료
                /*
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
                */

                if(currentState == State.LoginOther) { doLoginOther(); } else if(currentState == State.LoggedIn) { doCheckLogin(); } else {
                    // 로그인 되어있지 않은 상태인데 스위치가 켜져있다면 다시 끈다
                    if(materialSwitch_login_1.Checked) materialSwitch_login_1.Checked = false;
                    if(materialSwitch_login_2.Checked) materialSwitch_login_2.Checked = false;
                    if(materialSwitch_login_3.Checked) materialSwitch_login_3.Checked = false;
                }
                return;
            }
            // 검색 이벤트 종료
            /*
            else if (url.Contains("event")) {
                if(isSearch && previousUrl.Contains("search.naver")) {
                    isExceptSearch = true;
                    webView_main.CoreWebView2.Navigate(url_main);
                    return;
                }
            }
            */
        }

        // 검색 이벤트 종료
        /*
        private async void doGetEventItem() {
            isSearch = false;

            // 검색보상 수령 시간대별 인자 값
            // 1-> 00:05 ~05:55
            // 2-> 06:05 ~11:55
            // 3-> 12:05 ~17:55
            // 4-> 18:05 ~23:55
            
            int arg; //event_Search_Use 스크립트 실행 인자

            int koreaHour = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Korea Standard Time").Hour;
            if (koreaHour >= 0 && koreaHour <= 5) { arg = 1; } 
            else if (koreaHour >= 6 && koreaHour <= 11) { arg = 2; } 
            else if (koreaHour >= 12 && koreaHour <= 17) { arg = 3; } 
            else { arg = 4; }

            isGetSearchItem = true;
            await webView_main.ExecuteScriptAsync("event_Search_Use(" + arg + ");");
        }
        */

        // 검색 이벤트 종료
        /*
        private async void doNavigateAttendancePage() {
            Trace.WriteLine("(검색) 메인 -> 출석페이지");
            await webView_main.ExecuteScriptAsync(@"document.querySelector('[href *= ""attendance""]').click();");
        }

        private async void doNavigateGersangSite() {
            Trace.WriteLine("(검색) 네이버 -> 거상");

            //새로운 창이 뜨지 않도록 a태그에 target 속성을 제거
            await webView_main.ExecuteScriptAsync(@"document.querySelector('[href *= ""gersang.co.kr""]').removeAttribute(""target"");");

            //target 속성이 제거된 a태그를 클릭
            await webView_main.ExecuteScriptAsync(@"document.querySelector('[href *= ""gersang.co.kr""]').click();");
        }
        */

        private void deactivateWebSideFunction() {
            //materialSwitch_login_1.Enabled = false;
            //materialSwitch_login_2.Enabled = false;
            //materialSwitch_login_3.Enabled = false;

            /* 검색 이벤트 종료
            materialButton_search_1.Enabled = false;
            materialButton_search_2.Enabled = false;
            materialButton_search_3.Enabled = false;
            */

            materialButton_start_1.Enabled = false;
            materialButton_start_2.Enabled = false;
            materialButton_start_3.Enabled = false;
            materialButton_shortcut_1.Enabled = false;
            materialButton_shortcut_2.Enabled = false;
            materialButton_shortcut_3.Enabled = false;
            materialButton_shortcut_4.Enabled = false;
            Trace.WriteLine("웹뷰 관련 기능들이 비활성화 되었습니다.");
            isWebFunctionDeactivated = true;
        }

        private void activateWebSideFunction() {
            //materialSwitch_login_1.Enabled = true;
            //materialSwitch_login_2.Enabled = true;
            //materialSwitch_login_3.Enabled = true;

            /* 검색 이벤트 종료
            materialButton_search_1.Enabled = true;
            materialButton_search_2.Enabled = true;
            materialButton_search_3.Enabled = true;
            */

            materialButton_start_1.Enabled = true;
            materialButton_start_2.Enabled = true;
            materialButton_start_3.Enabled = true;
            materialButton_shortcut_1.Enabled = true;
            materialButton_shortcut_2.Enabled = true;
            materialButton_shortcut_3.Enabled = true;
            materialButton_shortcut_4.Enabled = true;
            Trace.WriteLine("웹뷰 관련 기능들이 다시 활성화 되었습니다.");
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
            Trace.WriteLine("NavigationFailed - WebErrorStatus : " + webErrorStatus);
            Trace.WriteLine("NavigationFailed - DocumentTitle : " + webView_main.CoreWebView2.DocumentTitle);

            //작업이 취소되었음을 나타냅니다. (MS DOC)
            if(webErrorStatus == CoreWebView2WebErrorStatus.OperationCanceled) { return; }

            //연결이 중지되었음을 나타냅니다. (MS DOC)
            if(webErrorStatus == CoreWebView2WebErrorStatus.ConnectionAborted) { return; }

            this.BeginInvoke(() => {
                //알 수 없는 오류가 발생했음을 나타냅니다. (MS DOC)
                if(webErrorStatus == CoreWebView2WebErrorStatus.Unknown) {
                    string title = webView_main.CoreWebView2.DocumentTitle;
                    if(title != null) MessageBox.Show("거상 홈페이지 접속에 실패하였습니다.\n원인 : " + title);
                    else MessageBox.Show("알 수 없는 오류로 인해 거상 홈페이지 접속에 실패하였습니다.\nDocumentTitle : NULL");
                }

                //인터넷 연결이 끊어졌음을 나타냅니다. (MS DOC)
                else if(webErrorStatus == CoreWebView2WebErrorStatus.Disconnected) {
                    MessageBox.Show("거상 홈페이지 접속에 실패하였습니다.\n인터넷 연결을 확인 해주세요.");
                }

                else {
                    MessageBox.Show($"거상 홈페이지 접속에 실패하였습니다. ({webErrorStatus})\n" +
                    $"1.인터넷 상태를 확인 해보세요.\n" +
                    $"2.거상 홈페이지가 터졌거나 점검 중일 수 있습니다.\n" +
                    $"계속해서 문제가 발생하면 문의 바랍니다.\n");
                }
            });
        }

        private async void doCheckLogin() {
            var logout_btn = await webView_main.ExecuteScriptAsync(@"document.querySelector(""a[href = '" + "/member/logoutProc.gs" + @"']"")");
            if(logout_btn != null) {
                Trace.WriteLine("체크하였더니 로그인이 되어있음.");
                switch(currentClient) {
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
                Trace.WriteLine("체크하였더니 로그인이 안되어있는 상태.");
                currentState = State.None;
                materialSwitch_login_1.CheckState = CheckState.Unchecked;
                materialSwitch_login_2.CheckState = CheckState.Unchecked;
                materialSwitch_login_3.CheckState = CheckState.Unchecked;
            }

            //if (true == isSearch) { webView_main.CoreWebView2.Navigate(url_search); } // 검색 이벤트 종료
            if(true == isGameStartLogin) { GameStart(); }
        }

        private async void doLoginOther() {
            var button_login = await webView_main.ExecuteScriptAsync("document.getElementById('btn_Login')");
            var textBox_id = await webView_main.ExecuteScriptAsync("document.getElementById('GSuserID')");
            var textBox_pw = await webView_main.ExecuteScriptAsync("document.getElementById('GSuserPW')");

            string? tag = null;
            string[] account;
            switch(currentClient) {
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

            if(tag != null) { account = tag.Split(';'); } else { return; }

            if(button_login != null && textBox_id != null && textBox_pw != null) { Login(account[0], account[1]); }
        }

        private async void doOtpInput(string otpCode) {
            await webView_main.ExecuteScriptAsync("document.getElementById('GSotpNo').value = '" + otpCode + "'");
            await webView_main.ExecuteScriptAsync("document.getElementById('btn_Send').click()");
        }

        private async void doPwReset(string returnUrl) {
            await webView_main.ExecuteScriptAsync(@"document.querySelector(""a[href *= '" + returnUrl + @"']"").click()");
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
                Owner = this,
            };
            backgroundForm.Show();

            MaterialForm dialog_otp = new MaterialForm() {
                FormStyle = FormStyles.ActionBar_40,
                Sizable = false,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(202, 136),
                Text = "OTP 입력",
                MaximizeBox = false,
                MinimizeBox = false,
                TopMost = true,
                ShowInTaskbar = false,
                Owner = this,
            };

            MaterialTextBox2 textBox_otp = new MaterialTextBox2() {
                MaxLength = 8,
                Location = new Point(17, 82),
                Size = new Size(111, 36),
                UseTallSize = false,
                ImeMode = ImeMode.Disable // 전각으로 전환되지 않도록 비활성화
            };
            dialog_otp.Controls.Add(textBox_otp);

            MaterialButton button_confirm = new MaterialButton() {
                Text = "확인",
                Location = new Point(135, 82),
                AutoSize = false,
                Size = new Size(50, 36),
            };
            button_confirm.Click += (sender, e) => {
                if(textBox_otp.Text.Length != 8) {
                    return;
                }

                if(!Regex.IsMatch(textBox_otp.Text, @"^[0-9]+$")) {
                    return;
                }

                dialog_otp.DialogResult = DialogResult.OK;
            };

            dialog_otp.Controls.Add(button_confirm);
            dialog_otp.AcceptButton = button_confirm;

            if(dialog_otp.ShowDialog() == DialogResult.OK) {
                backgroundForm.Dispose();
                return textBox_otp.Text;
            } else {
                backgroundForm.Dispose();
                return null;
            }
        }

        private void materialButton_start_Click(object sender, EventArgs e) {
            MaterialButton startButton = (MaterialButton)sender;
            StartClick(startButton);
        }

        private void StartClick(MaterialButton startButton) {
            MaterialSwitch? loginSwitch;

            if(startButton.Equals(materialButton_start_1)) { loginSwitch = materialSwitch_login_1; } else if(startButton.Equals(materialButton_start_2)) { loginSwitch = materialSwitch_login_2; } else { loginSwitch = materialSwitch_login_3; }

            if(true == loginSwitch.Checked) { GameStart(); } else {
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
            if(materialCheckbox_testServer.Checked) {
                configKey = "client_path_test_";
                regKey = "TestPath";
                server = "test";
                url_vsn = url_test_vsn;
            } else {
                configKey = "client_path_";
                regKey = "InstallPath";
                server = "main";
                url_vsn = url_main_vsn;
            }

            string client_path;
            switch(currentClient) {
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

            if(client_path == "") {
                DialogResult dr = MessageBox.Show(this, "클라이언트 경로가 지정되어 있지 않습니다.\n설정 창으로 이동하시겠습니까?", "경로 미지정", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                if(dr == DialogResult.OK) { OpenClientSettingDialog(); }
                return;
            }

            if(false == ValidationPath(client_path, server)) return;

            string version_current = VersionChecker.GetCurrentVersion(this, ConfigManager.getConfig(configKey + '1'));
            string version_latest = VersionChecker.GetLatestVersion(this, url_vsn);
            if(version_current != version_latest) {
                DialogResult dr = DialogResult.No;
                bool update = false;
                if(!bool.Parse(ConfigManager.getConfig("is_auto_update"))) {
                    dr = MessageBox.Show(this, "거상 업데이트가 가능합니다! (" + version_current + "->" + version_latest + ")\n프로그램 기능을 사용하여 업데이트 하시겠습니까?\n거상 스테이션은 공식 패치 프로그램보다\n더 빠르게 업데이트 가능합니다.",
                        "거상 패치", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                } else {
                    update = true;
                }

                if(dr == DialogResult.Yes || update == true) {
                    this.BeginInvoke(() => {
                        Form backgroundForm = InitBackgroundForm(this);

                        bool isTest = (server == "test") ? true : false;
                        Form_Patcher form_Patcher = new Form_Patcher(isTest) {
                            Owner = this
                        };

                        try {
                            backgroundForm.Show();
                            form_Patcher.ShowDialog();
                        } catch(Exception ex) {
                            Trace.WriteLine(ex.StackTrace);
                        } finally {
                            backgroundForm.Dispose();
                        }
                    });
                    return;
                }
                Trace.WriteLine("일반 패치를 선택하였습니다.");
            }

            try {
                //해당 클라이언트의 경로를 레지스트리에 등록시킵니다.
                RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\JOYON\Gersang\Korean", RegistryKeyPermissionCheck.ReadWriteSubTree);
                if(registryKey != null) {
                    registryKey.SetValue(regKey, client_path);
                    registryKey.Close();
                }
            } catch(Exception ex) {
                Trace.WriteLine(ex.Message);
                Trace.WriteLine(ex.StackTrace);
            }

            string? value = null;
            try {
                //Gersang Game Starter 경로가 저장되어있는 레지스트리 경로에 접근하여 경로를 얻습니다.
                value = Registry.ClassesRoot.OpenSubKey("Gersang").OpenSubKey("shell").OpenSubKey("open").OpenSubKey("command").GetValue("").ToString();
            } catch(Exception ex) {
                Trace.WriteLine(ex.Message);
                Trace.WriteLine(ex.StackTrace);
                value = null;
            }

            string starterPath; //거상 스타터의 경로를 저장
            //GameStarter 경로를 찾을 수 없는 경우, Starter 다운로드 안내창을 띄웁니다.
            if(value == null) {
                OpenGersangStarterInstallDialog();
                return;
            } else {
                starterPath = value.Replace(@"""", String.Empty);
            }

            //레지스트리에 저장된 GameStarter의 경로에 실제로 Starter가 설치되어 있지 않은 경우, 다운로드 안내창을 띄웁니다.
            if(!File.Exists(starterPath)) {
                OpenGersangStarterInstallDialog();
                return;
            } else {
                await webView_main.ExecuteScriptAsync(@"startRetry = setTimeout(""socketStart('" + server + @"')"", 2000);"); //소켓을 엽니다.
                Process starter = new Process();
                starter.StartInfo.FileName = value.ToString();
                starter.EnableRaisingEvents = true;
                starter.Exited += (sender, e) => {
                    Trace.WriteLine("게임 스타터가 종료됨.");
                    activateWebSideFunction_invoke();
                };
                deactivateWebSideFunction();
                starter.Start();
            }
        }

        private bool ValidationPath(string client_path, string server) {
            string iniName;
            if(server == "main") { iniName = "GerSangKR.ini"; } else { iniName = "GerSangKRTest.ini"; }

            if(!File.Exists(client_path + "\\" + "Gersang.exe")) {
                this.BeginInvoke(() => {
                    MessageBox.Show(this, "거상 경로를 다시 설정해주세요.\n원인 : Gersang.exe 파일이 없습니다.", "실행 불가", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                });
                return false;
            }

            if(!File.Exists(client_path + "\\" + iniName)) {
                string message;
                if(server == "main") { message = "본섭 경로가 아닙니다."; } else { message = "테섭 경로가 아닙니다."; }
                this.BeginInvoke(() => {
                    MessageBox.Show(this, "거상 경로를 다시 설정해주세요.\n원인 : " + message, "실행 불가", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                });
                return false;
            }

            return true;
        }

        private void OpenGersangStarterInstallDialog() {
            if(MessageBox.Show("GersangGameStarter가 설치되어 있지 않습니다.\n다운로드 받으시겠습니까? (거상 공식 다운로드 링크입니다)", "게임 실행 실패", MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes) {
                try {
                    Process.Start(new ProcessStartInfo(url_installStarter) { UseShellExecute = true });
                } catch(Exception ex2) {
                    Trace.WriteLine(ex2.Message);
                    Trace.WriteLine(ex2.StackTrace);
                }
            } else {
                MessageBox.Show("거상 홈페이지 -> 자료실 -> 클라이언트 -> GersangGameStarter 수동 설치\n위 경로에서 거상 실행기를 다운로드 받으셔야 게임 실행이 가능합니다.", "게임 실행 실패", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void materialSwitch_login_Click(object sender, EventArgs e) {
            MaterialSwitch @switch = (MaterialSwitch)sender;
            SwitchClick(@switch);
        }

        private void SwitchClick(MaterialSwitch sender) {
            if(isWebFunctionDeactivated) {
                Trace.WriteLine("웹 로딩 중 스위치 작동이 불가능 합니다.");
                return;
            }

            MaterialSwitch materialSwitch = sender;
            string name = materialSwitch.Name;
            currentClient = (Client)Byte.Parse(name.Substring(name.Length - 1));

            //로그아웃
            if(materialSwitch.Checked) {
                Trace.WriteLine("로그아웃 합니다.");
                webView_main.CoreWebView2.Navigate(url_logout);
                materialSwitch.CheckState = CheckState.Unchecked;
                currentState = State.None;
                currentClient = Client.None;
                return;
            }

            if(this.currentState == State.LoggedIn) {
                Trace.WriteLine("다른 계정에 로그인 하기 위해 로그아웃 합니다.");

                materialSwitch_login_1.CheckState = CheckState.Unchecked;
                materialSwitch_login_2.CheckState = CheckState.Unchecked;
                materialSwitch_login_3.CheckState = CheckState.Unchecked;

                currentState = State.LoginOther;

                webView_main.CoreWebView2.Navigate(url_logout);
                return;
            }

            if(materialSwitch.Tag == null || materialSwitch.Tag.ToString().Length == 0 || materialSwitch.Tag.ToString().Contains("선택안함")) {
                MessageBox.Show("로그인 할 계정이 선택되지 않았습니다.");
                return;
            }

            if(materialSwitch.Tag.ToString().Contains("SNS_")) {
                SNS_Login(materialSwitch.Tag.ToString()!);
                return;
            }

            string[] account = materialSwitch.Tag.ToString().Split(';');
            Login(account[0], account[1]);
        }

        private async void SNS_Login(string platform) {
            if(platform == "SNS_네이버") await webView_main.ExecuteScriptAsync("document.getElementById('btn_naver').click()");
            else if(platform == "SNS_카카오") await webView_main.ExecuteScriptAsync("document.getElementById('btn_kakao').click()");
            else if(platform == "SNS_구글") await webView_main.ExecuteScriptAsync("document.getElementById('btn_google').click()");
            else {
                MessageBox.Show("SNS 로그인에 실패하였습니다. 관리자에게 문의해주세요.", "SNS 로그인 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            currentState = State.LoggedIn;
        }

        private void materialButton_shortcut_Click(object sender, EventArgs e) {
            MaterialButton button = (MaterialButton)sender;
            string? url = ConfigManager.getConfig("shortcut_" + button.Name.Substring(button.Name.Length - 1, 1));

            if(url == null || url.Equals("")) {
                DialogResult dr = MessageBox.Show("나만의 바로가기 링크가 설정되어 있지 않습니다.\n설정화면으로 이동하시겠습니까?", "바로가기 미설정", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                if(dr == DialogResult.OK) { OpenShortcuttSettingDialog(); }
                return;
            }

            if(!Uri.IsWellFormedUriString(url, UriKind.Absolute)) {
                MessageBox.Show("잘못된 링크 형식 입니다. 다시 설정해주세요.");
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
                LoadShortcut();
            };
            form.Show();
        }

        private void materialComboBox_account_SelectedIndexChanged(object sender, EventArgs e) {
            MaterialComboBox comboBox = (MaterialComboBox)sender;

            string id = ConfigManager.getKeyByValue(comboBox.Text).Replace("_nickname", string.Empty);
            if(id == "") id = comboBox.Text;
            string switchTag;
            Trace.WriteLine(id);
            if(id.Contains("선택안함") || id.Contains("SNS_")) { switchTag = comboBox.Text; } else { switchTag = id + ";" + ConfigManager.getConfig(id); }

            byte current_preset = Byte.Parse(ConfigManager.getConfig("current_preset"));
            int[] temp = Array.ConvertAll(ConfigManager.getConfig("current_comboBox_index_preset_" + current_preset).Split(';'), s => int.Parse(s));

            if(comboBox.Equals(materialComboBox_account_1)) {
                materialSwitch_login_1.Tag = switchTag;
                temp[0] = comboBox.SelectedIndex;
            } else if(comboBox.Equals(materialComboBox_account_2)) {
                materialSwitch_login_2.Tag = switchTag;
                temp[1] = comboBox.SelectedIndex;
            } else {
                materialSwitch_login_3.Tag = switchTag;
                temp[2] = comboBox.SelectedIndex;
            }

            ConfigManager.setConfig("current_comboBox_index_preset_" + current_preset, string.Join(';', temp));

            if(Byte.Parse(comboBox.Name.Substring(comboBox.Name.Length - 1, 1)) == (byte)currentClient && currentState == State.LoggedIn) {
                Trace.WriteLine("현재 로그인한 클라이언트의 계정을 변경하였으므로, 로그아웃 합니다.");
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
            materialComboBox_account_1.Items.Clear();
            materialComboBox_account_2.Items.Clear();
            materialComboBox_account_3.Items.Clear();

            string temp = ConfigManager.getConfig("account_list");
            string[] accountList;
            if(temp.Length != 0) {
                accountList = temp.Remove(temp.Length - 1, 1).Split(';');
            } else {
                accountList = Array.Empty<string>();
            }

            materialComboBox_account_1.Items.Add("선택안함");
            materialComboBox_account_2.Items.Add("선택안함");
            materialComboBox_account_3.Items.Add("선택안함");

            foreach(var item in accountList) {
                string id = ConfigManager.getConfig(item + "_nickname");
                if(id == "") id = item;
                materialComboBox_account_1.Items.Add(id);
                materialComboBox_account_2.Items.Add(id);
                materialComboBox_account_3.Items.Add(id);
            }

            materialComboBox_account_1.Items.Add("SNS_네이버");
            materialComboBox_account_2.Items.Add("SNS_네이버");
            materialComboBox_account_3.Items.Add("SNS_네이버");
            materialComboBox_account_1.Items.Add("SNS_카카오");
            materialComboBox_account_2.Items.Add("SNS_카카오");
            materialComboBox_account_3.Items.Add("SNS_카카오");
            materialComboBox_account_1.Items.Add("SNS_구글");
            materialComboBox_account_2.Items.Add("SNS_구글");
            materialComboBox_account_3.Items.Add("SNS_구글");

            byte current_preset = Byte.Parse(ConfigManager.getConfig("current_preset"));
            int[] index = Array.ConvertAll(ConfigManager.getConfig("current_comboBox_index_preset_" + current_preset).Split(';'), s => int.Parse(s));

            try {
                materialComboBox_account_1.SelectedIndex = index[0];
                materialComboBox_account_2.SelectedIndex = index[1];
                materialComboBox_account_3.SelectedIndex = index[2];
            } catch(ArgumentOutOfRangeException) {
                materialComboBox_account_1.SelectedIndex = 0;
                materialComboBox_account_2.SelectedIndex = 0;
                materialComboBox_account_3.SelectedIndex = 0;
            }

            materialComboBox_account_1.Refresh();
            materialComboBox_account_2.Refresh();
            materialComboBox_account_3.Refresh();
        }

        private void materialButton_debugging_Click(object sender, EventArgs e) {

            webView_main.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;

            Form_Browser form = new Form_Browser(webView_main);
            form.FormClosed += (sender, e) => {
                webView_main.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                webView_main.CoreWebView2.Navigate(url_main);
                form.Controls.Clear();
                LoadShortcut();
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
            SearchClick(searchButton);
        }

        private void SearchClick(MaterialButton searchButton) {
            // 검색 보상 이벤트가 더 이상 진행되지 않아 주석처리
            /*
            MaterialSwitch loginSwitch;

            if (searchButton.Equals(materialButton_search_1)) { loginSwitch = materialSwitch_login_1; } 
            else if (searchButton.Equals(materialButton_search_2)) { loginSwitch = materialSwitch_login_2; } 
            else { loginSwitch = materialSwitch_login_3; }

            isSearch = true;
            if (true == loginSwitch.Checked) { webView_main.CoreWebView2.Navigate(url_search); } //네이버 검색
            else { SwitchClick(loginSwitch); }
            */
        }

        private void radio_preset_CheckedChanged(object sender, EventArgs e) {
            MaterialRadioButton radio = (MaterialRadioButton)sender;
            if(radio.Checked == false) { return; }
            string? value = radio.Tag.ToString();
            if(value == null) {
                MessageBox.Show("RadioButton의 Tag가 Null입니다.");
                return;
            }

            ConfigManager.setConfig("current_preset", value);
            LoadAccountComboBox();
        }

        private void LoadRadioButton() {
            byte current_preset = Byte.Parse(ConfigManager.getConfig("current_preset"));
            switch(current_preset) {
                case 1:
                    radio_preset_1.PerformClick();
                    break;
                case 2:
                    radio_preset_2.PerformClick();
                    break;
                case 3:
                    radio_preset_3.PerformClick();
                    break;
                case 4:
                    radio_preset_4.PerformClick();
                    break;
                default:
                    MessageBox.Show("LoadRadioButton에서 오류 발생");
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
            } catch(CryptographicException e) {
                //사용자가 암호화된 패스워드가 포함된 설정파일을 타 PC로 복사 후 사용 시 발생하는 오류
                Trace.WriteLine(e.Message);
                MessageBox.Show("계정정보를 타 PC로 옮긴 것으로 확인되었습니다.\n계정 정보 유출 방지를 위해 모든 계정 정보를 초기화 합니다.", "패스워드 복호화 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                currentClient = Client.None;
                currentState = State.None;
                ClearAccount();
                return;
            }

            await webView_main.ExecuteScriptAsync("document.getElementById('btn_Login').click()");
            currentState = State.LoggedIn;
        }

        private void ClearAccount() {
            string temp = ConfigManager.getConfig("account_list");
            string[] account_list = temp.Remove(temp.Length - 1, 1).Split(';');
            foreach(var item in account_list) {
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
            MaterialButton button = (MaterialButton)sender;
            if(button.Equals(materialButton_setting_account)) {
                OpenAccountSettingDialog();
            } else if(button.Equals(materialButton_setting_client)) {
                OpenClientSettingDialog();
            } else if(button.Equals(materialButton_setting_shortcut)) {
                OpenShortcuttSettingDialog();
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
            } catch(Exception ex) {
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
            } catch(Exception ex) {
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
            } catch(Exception ex) {
                Trace.WriteLine(ex.StackTrace);
            } finally {
                backgroundForm.Dispose();
            }
        }

        private void materialCheckbox_testServer_CheckedChanged(object sender, EventArgs e) {
            MaterialCheckbox checkbox = (MaterialCheckbox)sender;
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

        private void materialButton_patchNote_Click(object sender, EventArgs e) {
            Process.Start(new ProcessStartInfo(url_release) { UseShellExecute = true });
        }

        private void materialButton_blog_Click(object sender, EventArgs e) {
            Process.Start(new ProcessStartInfo("https://blog.naver.com/kog5071/222644960946") { UseShellExecute = true });
        }

        private void materialButton_gitHub_Click(object sender, EventArgs e) {
            Process.Start(new ProcessStartInfo("https://github.com/byungmeo/GersangStation") { UseShellExecute = true });
        }

        private void button_tray_Click(object sender, EventArgs e) {
            notifyIcon1.Visible = true;
            notifyIcon1.BalloonTipTitle = "알림";
            notifyIcon1.BalloonTipText = "프로그램이 트레이로 이동되었습니다.";
            notifyIcon1.ShowBalloonTip(5000);
            this.Hide();
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e) {
            notifyIcon1.Visible = false;
            this.Show();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            Process.Start(new ProcessStartInfo("https://logomakr.com/app") { UseShellExecute = true });
        }

        private void contextMenuStrip_tray_ItemClicked(object sender, ToolStripItemClickedEventArgs e) {
            ToolStripItem item = e.ClickedItem;
            if(item.Equals(toolStripMenuItem_open)) {
                notifyIcon1.Visible = false;
                this.Show();
            } else if(item.Equals(toolStripMenuItem_exit)) {
                System.Windows.Forms.Application.Exit();
            }
        }

        private void toolStripMenuItem_client_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
            ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
            ToolStripItem item = e.ClickedItem;

            if(menuItem.Equals(toolStripMenuItem_client_1)) {
                // if (item.Equals(toolStripMenuItem_search_1)) { SearchClick(materialButton_search_1); } // 검색 이벤트 종료
                if(item.Equals(toolStripMenuItem_start_1)) { StartClick(materialButton_start_1); }
            } else if(menuItem.Equals(toolStripMenuItem_client_2)) {
                // if (item.Equals(toolStripMenuItem_search_2)) { SearchClick(materialButton_search_2); } // 검색 이벤트 종료
                if(item.Equals(toolStripMenuItem_start_2)) { StartClick(materialButton_start_2); }
            } else if(menuItem.Equals(toolStripMenuItem_client_3)) {
                // if (item.Equals(toolStripMenuItem_search_3)) { SearchClick(materialButton_search_3); } // 검색 이벤트 종료
                if(item.Equals(toolStripMenuItem_start_3)) { StartClick(materialButton_start_3); }
            }
        }

        private void materialButton_license_Click(object sender, EventArgs e) {
            Form backgroundForm = InitBackgroundForm(this);

            Form_License dialog_license = new Form_License() {
                Owner = this
            };

            try {
                backgroundForm.Show();
                dialog_license.ShowDialog();
            } catch(Exception ex) {
                Trace.WriteLine(ex.StackTrace);
            } finally {
                backgroundForm.Dispose();
            }
        }

        private void materialButton_sponsor_Click(object sender, EventArgs e) {
            Process.Start(new ProcessStartInfo("https://github.com/byungmeo/GersangStation/discussions/26") { UseShellExecute = true });
        }

        private void materialButton_question_kakao_Click(object sender, EventArgs e) {
            Process.Start(new ProcessStartInfo("https://open.kakao.com/o/sXJQ1qPd") { UseShellExecute = true });
        }

        private void materialButton_question_naver_Click(object sender, EventArgs e) {
            Process.Start(new ProcessStartInfo("https://blog.naver.com/kog5071/222644960946") { UseShellExecute = true });
        }

        private void materialCheckbox_mouseClip_CheckedChanged(object sender, EventArgs e) {
            ConfigManager.setConfig("use_clip_mouse", materialCheckbox_mouseClip.Checked.ToString());

            if(materialCheckbox_mouseClip.Checked == true) {
                ClipMouse.Run();
                checkBox_clipToggleHotKey.Enabled = true;
                checkBox_clipDisableHotKey.Enabled = true;
                textBox_clipToggleHotKey.Enabled = true;
                checkBox_onlyFirstClip.Enabled = true;
            } else {
                ClipMouse.Stop(false);
                checkBox_clipToggleHotKey.Enabled = false;
                checkBox_clipDisableHotKey.Enabled = false;
                textBox_clipToggleHotKey.Enabled = false;
                checkBox_onlyFirstClip.Enabled = false;
            }
        }

        private void textBox_hotKey_KeyDown(object sender, KeyEventArgs e) {
            TextBox textBox = (TextBox)sender;
            string org = textBox.Text;

            // 조합 단축키 지원
            string comb = string.Empty;
            if(e.Control) comb = "Ctrl";
            else if(e.Alt) comb = "Alt";
            else if(e.Shift) comb = "Shift";

            if(e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.Menu || e.KeyCode == Keys.ShiftKey) {
                textBox.Text = comb;
                ConfigManager.setConfig("clip_toggle_hotkey", ((int)e.KeyCode).ToString());
            } else if(comb != string.Empty && e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.Menu && e.KeyCode != Keys.ShiftKey) {
                textBox.Text = comb + " + " + e.KeyCode.ToString();
                ConfigManager.setConfig("clip_toggle_hotkey", comb + "," + ((int)e.KeyCode).ToString());
            } else {
                textBox.Text = e.KeyCode.ToString();
                ConfigManager.setConfig("clip_toggle_hotkey", ((int)e.KeyCode).ToString());
            }

            if(org != textBox.Text) {
                ClipMouse.UnregisterHotKey(this.Handle);
                ClipMouse.RegisterHotKey(this.Handle, ConfigManager.getConfig("clip_toggle_hotkey"));
            }

            // Alt 키를 누르면 포커스 풀리는 현상 방지
            if(e.KeyCode == Keys.Menu) e.SuppressKeyPress = true;
        }

        private void checkBox_clipDisableHotKey_CheckedChanged(object sender, EventArgs e) {
            ConfigManager.setConfig("use_clip_disable_hotkey", ((CheckBox)sender).Checked.ToString());
        }

        private void checkBox_clipToggleHotKey_CheckedChanged(object sender, EventArgs e) {
            ConfigManager.setConfig("use_clip_toggle_hotkey", ((CheckBox)sender).Checked.ToString());
        }

        private void checkBox_onlyFirstClip_CheckedChanged(object sender, EventArgs e) {
            ConfigManager.setConfig("use_clip_only_first", ((CheckBox)sender).Checked.ToString());
            ClipMouse.firstGameHandle = IntPtr.Zero;
            ClipMouse.isOnlyFirstClip = ((CheckBox)sender).Checked;
        }

        private void linkLabel_clipInformation_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            Process.Start(new ProcessStartInfo("https://github.com/byungmeo/GersangStation/discussions/37") { UseShellExecute = true });
        }

        private void materialButton_integrity_Click(object sender, EventArgs e) {
            OpenIntegrityCheckDialog();
        }

        private void OpenIntegrityCheckDialog() {
            Form backgroundForm = InitBackgroundForm(this);

            Form_IntegrityCheck dialog_integrityCheck = new Form_IntegrityCheck() {
                Owner = this
            };

            try {
                backgroundForm.Show();
                dialog_integrityCheck.ShowDialog();
            } catch(Exception ex) {
                Trace.WriteLine(ex.StackTrace);
            } finally {
                backgroundForm.Dispose();
            }
        }
    } //Form1
} //namespace