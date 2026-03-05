#define DEBUGGING

using Core;
using Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace GersangStation;

public enum TryLoginResult
{
    Success,
    InvalidId,
    NotFoundPw,
    NullWebview
}

public sealed partial class WebViewManager : IDisposable, INotifyPropertyChanged
{
    #region Gersang Homepage Controller
    private string _cachedGameStartId = string.Empty;
    private int _cachedGameStartClientIndex = -1;

    private string _cachedInstallPath = "";
    private bool _tryingGameStart = false;
    public bool TryingGameStart
    {
        get => _tryingGameStart;
        private set
        {
            if (_tryingGameStart != value)
            {
                _tryingGameStart = value;
                IsBusy |= value;
                OnPropertyChanged(nameof(TryingGameStart));
            }
        }
    }

    private bool _tryingLogin = false;
    public bool TryingLogin
    {
        get => _tryingLogin;
        private set
        {
            if (_tryingLogin != value)
            {
                _tryingLogin = value;
                IsBusy |= value;
                OnPropertyChanged(nameof(TryingLogin));
            }
        }
    }

    private string _cachedLoginId = string.Empty;
    private bool _tryingLogout = false;
    public bool TryingLogout
    {
        get => _tryingLogout;
        private set
        {
            if (_tryingLogout != value)
            {
                _tryingLogout = value;
                IsBusy |= value;
                OnPropertyChanged(nameof(TryingLogout));
            }
        }
    }

    private bool _loggedIn = false;
    public bool LoggedIn
    {
        get => _loggedIn;
        private set
        {
            if (_loggedIn != value)
            {
                _loggedIn = value;
                OnPropertyChanged(nameof(LoggedIn));
            }
        }
    }

    public event EventHandler? LoggedInChanged;
    private string _loggedInMemberId = "";
    public string LoggedInMemberId
    {
        get => _loggedInMemberId;
        private set
        {
            if (_loggedInMemberId != value)
            {
                _loggedInMemberId = value;
                OnPropertyChanged(nameof(LoggedInMemberId));
            }
        }
    }

    private const string Url_Gersang_Main = "https://www.gersang.co.kr";
    private const string Url_Gersang_Otp = "https://www.gersang.co.kr/member/otp.gs";
    private const string Url_Gersang_Logout = "https://www.gersang.co.kr/member/logoutProc.gs";

    private const string TryLoginScript = $"document.getElementById('btn_Login').click()";
    private const string SubmitOtpScript = $"document.querySelector('form[action=\"otpProc.gs\"]').submit()";
    private static string InputIdScript(string id) => $"document.getElementById('GSuserID').value = '{id}'";
    private static string InputPwScript(string pw) => $"document.getElementById('GSuserPW').value = '{pw}'";
    private static string InputOtpScript(string otpCode) => $"document.getElementById('GSotpNo').value = '{otpCode}'";
    private static string SocketStartScript(string serverParam) => $"startRetry = setTimeout(\"socketStart('{serverParam}')\", 2000);";

    public async Task<bool> TryGameStart(string id, int clientIndex)
    {
        TryingGameStart = false;

        if (_webview is null || 3 <= clientIndex || clientIndex < 0)
            return false;

        ClientSettings settings = AppDataManager.LoadServerClientSettings(AppDataManager.SelectedServer);
        string installPath = clientIndex switch
        {
            0 => settings.InstallPath,
            1 => settings.Client2Path,
            2 => settings.Client3Path,
            _ => throw new ArgumentOutOfRangeException(nameof(clientIndex), clientIndex, null),
        };

        // 재진입/재시도에 필요한 정보는 먼저 캐시
        _cachedGameStartId = id;
        _cachedGameStartClientIndex = clientIndex;
        _cachedInstallPath = installPath;

        // 거상 도메인이 아니면 먼저 거상 메인으로 이동하고,
        // DOMContentLoaded 이후 다시 TryGameStart를 타게 한다.
        if (!IsGersangDomain(_webview.Source))
        {
            Debug.WriteLine("거상 도메인이 아니므로 거상 메인으로 이동 후 게임 실행을 재시도합니다.");
            TryingGameStart = true;
            _webview.Source = new Uri(Url_Gersang_Main);
            return true;
        }

        TryLoginResult tryLoginResult = await TryLogin(id);
        switch (tryLoginResult)
        {
            case TryLoginResult.Success:
                break;
            case TryLoginResult.InvalidId:
                TryingGameStart = false;
                return false;
            case TryLoginResult.NotFoundPw:
                TryingGameStart = false;
                return false;
            case TryLoginResult.NullWebview:
                TryingGameStart = false;
                return false;
            default:
                throw new ArgumentOutOfRangeException(nameof(tryLoginResult), tryLoginResult, null);
        }

        TryingGameStart = true;
        _cachedInstallPath = installPath;
        Debug.WriteLine($"TryGameStart id:{id}, _cachedInstallPath: {_cachedInstallPath}");

        // 이 시점에 로그인을 하고 있지 않다면 이미 로그인 되어있고 실행 가능한 상태
        if (!TryingLogin)
        {
            await GameStart(_cachedInstallPath);
            _cachedInstallPath = string.Empty;
            _cachedGameStartId = string.Empty;
            _cachedGameStartClientIndex = -1;
        }

        return true;
    }

    public async Task TryLogout()
    {
        _webview.Source = new Uri(Url_Gersang_Logout);
        TryingLogout = true;
    }

    private static bool IsGersangDomain(Uri? uri)
    {
        return uri is not null
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Equals("www.gersang.co.kr", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<TryLoginResult> TryLogin(string id)
    {
        if (_webview is null)
            return TryLoginResult.NullWebview;

        // True 상태로 TryLogin 함수에 진입한 경우를 대비
        TryingLogin = false;

        if (LoggedIn)
        {
            if (LoggedInMemberId == id )
            {
                // 로그인 하려는 아이디와 현재 로그인 된 아이디가 같으면 로그인 할 필요 없다
                Debug.WriteLine("이미 동일한 계정으로 로그인 되어 있으므로 로그인 과정을 스킵합니다.");
                return TryLoginResult.Success;
            }
            else
            {
                // 로그아웃 해야 한다
                Debug.WriteLine("다른 계정으로 로그인 되어 있으므로 로그아웃 후 로그인을 시도합니다.");
                TryingLogin = true;
                _cachedLoginId = id;
                await TryLogout();
                return TryLoginResult.Success;
            }
        }
            
        if (string.IsNullOrWhiteSpace(id))
            return TryLoginResult.InvalidId;

        string? pw = PasswordVaultHelper.GetPassword(id);
        if (string.IsNullOrWhiteSpace(pw))
            return TryLoginResult.NotFoundPw;

        TryingLogin = true;
        // 거상 홈페이지가 아니면 스크립트를 실행하지 않고 우선 거상 공홈에 접속한다
        if (!IsGersangDomain(_webview.Source))
        {
            Debug.WriteLine($"거상 홈페이지가 아닌 곳에서 로그인 시도");
            _cachedLoginId = id;
            _webview.Source = new Uri(Url_Gersang_Main);
            return TryLoginResult.Success;
        }

        await _webview.ExecuteScriptAsync(InputIdScript(id));
        await _webview.ExecuteScriptAsync(InputPwScript(pw));
        await _webview.ExecuteScriptAsync(TryLoginScript);

        return TryLoginResult.Success;
    }

    private static async Task<string?> ReceiveOnceViaWebSocket1818Async(CancellationToken cancellationToken)
    {
        using HttpListener listener = new();
        listener.Prefixes.Add("http://127.0.0.1:1818/");
        listener.Start();

        try
        {
            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                try
                {
                    listener.Stop();
                }
                catch
                {
                }
            });

            HttpListenerContext context = await listener.GetContextAsync();

            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                return null;
            }

            HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(null);
            using WebSocket webSocket = wsContext.WebSocket;

            byte[] buffer = new byte[8192];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                cancellationToken);

            if (result.MessageType != WebSocketMessageType.Text)
                return null;

            string payload = Encoding.UTF8.GetString(buffer, 0, result.Count);

            byte[] okBytes = Encoding.UTF8.GetBytes("OK");
            await webSocket.SendAsync(
                new ArraySegment<byte>(okBytes),
                WebSocketMessageType.Text,
                true,
                cancellationToken);

            return payload;
        }
        catch (HttpListenerException)
        {
            return null;
        }
    }

    private async Task StartGameThroughLocalSocketAsync(string serverParam, string clientInstallPath)
    {
        string runExePath = Path.Combine(clientInstallPath, "Run.exe");
        Debug.WriteLine($"runExePath: {runExePath}");

        if (!File.Exists(runExePath))
            return;

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));

        Task<string?> receiveTask = ReceiveOnceViaWebSocket1818Async(cts.Token);

        await _webview.ExecuteScriptAsync(SocketStartScript(serverParam));
        Debug.WriteLine("SocketStartScript 실행");

        string? payload;
        try
        {
            payload = await receiveTask;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("WebSocket 수신 시간 초과");
            return;
        }

        Debug.WriteLine($"payload: {payload}");

        if (string.IsNullOrWhiteSpace(payload))
            return;

        string[] parts = payload.Split('\t');
        if (parts.Length < 3)
            return;

        string id = parts[1];
        string pw = parts[2];
        string? accountId = parts.Length >= 4 ? parts[3] : null;

        Process process = new();
        process.StartInfo.FileName = runExePath;
        process.StartInfo.Arguments = accountId is not null
            ? $"{id}\t{pw}\t{accountId}"
            : $"{id}\t{pw}";
        process.StartInfo.WorkingDirectory = clientInstallPath;
        process.StartInfo.UseShellExecute = true;
        process.StartInfo.Verb = "runas";

        process.Start();
        Debug.WriteLine("Run.exe 시작");
        await process.WaitForExitAsync();
        Debug.WriteLine("Run.exe 종료");
    }

    private async Task GameStart(string clientInstallPath)
    {
        if (!TryingGameStart)
            return;
        TryingGameStart = false;

        GameServer selectedServer = AppDataManager.SelectedServer;

        // 1. 클라이언트 경로 유효성 검사
        // TODO: 
        Debug.WriteLine("[PASS] 클라이언트 경로 유효성 검사");

        // 2. 클라이언트 패치 가능 여부 검사
        // TODO: 
        Debug.WriteLine("[PASS] 클라이언트 패치 가능 여부 검사");

        // 3. 거상 실행
        string param = GameServerHelper.GetGameStartParam(selectedServer);
        await StartGameThroughLocalSocketAsync(param, clientInstallPath);

        // TODO: 방금 켜진 거상 프로세스 추적
    }

    private async Task UpdateLoginStateByCookieAsync()
    {
        if (_webview is null)
            return;

        var core = _webview.CoreWebView2;
        if (core is null)
            return;

        bool isLoggedInMemberIdChanged = false;
        var cookies = await core.CookieManager.GetCookiesAsync(Url_Gersang_Main);
        foreach (var c in cookies)
        {
            if (string.Equals(c.Name, "memberID", StringComparison.OrdinalIgnoreCase))
            {
                if(LoggedInMemberId != c.Value)
                {
                    isLoggedInMemberIdChanged = true;
                    LoggedInMemberId = c.Value;
                }
                break;
            }
        }

        LoggedIn = !string.IsNullOrWhiteSpace(LoggedInMemberId);
        if (isLoggedInMemberIdChanged)
            LoggedInChanged?.Invoke(this, EventArgs.Empty);
        Debug.WriteLine($"TryingLogin: {TryingLogin}, IsLoggedIn: {LoggedIn}, LoggedInMemberId: {LoggedInMemberId}");
        if (LoggedIn)
        {
            TryingLogin = false;

            if (TryingGameStart)
            {
                if (IsGersangDomain(new Uri(_currentSource)))
                {
                    await TryGameStart(_cachedGameStartId, _cachedGameStartClientIndex);
                }
                else
                {
                    Debug.WriteLine("로그인 쿠키는 있지만 거상 도메인이 아니므로 거상 메인으로 이동 후 게임 실행을 재시도합니다.");
                    _webview.Source = new Uri(Url_Gersang_Main);
                }
            }
        }
        else
        {
            LoggedInMemberId = "";
            if (TryingLogout)
            {
                TryingLogout = false;
                if (TryingLogin)
                {
                    await TryLogin(_cachedLoginId);
                    _cachedLoginId = string.Empty;
                }
            }
            else if (TryingLogin && !string.IsNullOrWhiteSpace(_cachedLoginId))
            {
                await TryLogin(_cachedLoginId);
                _cachedLoginId = string.Empty;
            }
        }
    }
    #endregion Gersang Homepage Controller


    #region WebViewManager Core
    private readonly WebView2 _webview;
    private readonly Window _currentWindow;

    private bool _isBusy = false;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
            }
        }
    }

    private string _currentSource = "";
    public string CurrentSource
    {
        get => _currentSource;
        private set
        {
            if (_currentSource != value)
            {
                _currentSource = value;
                OnPropertyChanged(nameof(CurrentSource));
            }
        }
    }

    private string _currentTitle = "";
    public string CurrentTitle
    {
        get => _currentTitle;
        private set
        {
            if (_currentTitle != value)
            {
                _currentTitle = value;
                OnPropertyChanged(nameof(CurrentTitle));
            }
        }
    }

    private bool _canGoBack = false;
    public bool CanGoBack
    {
        get => _canGoBack;
        private set
        {
            if (_canGoBack != value)
            {
                _canGoBack = value;
                OnPropertyChanged(nameof(CanGoBack));
            }
        }
    }

    private bool _canGoForward = false;
    public bool CanGoForward
    {
        get => _canGoForward;
        private set
        {
            if (_canGoForward != value)
            {
                _canGoForward = value;
                OnPropertyChanged(nameof(CanGoForward));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public WebViewManager(WebView2 webview, Window window)
    {
        _currentWindow = window;
        _webview = webview;
        InitWebView();
    }

    public void Dispose()
    {
        Debug.WriteLine($"[WebViewManager::Dispose]");
        UnsubscribeWebViewEvents();
        UnsubscribeCoreEvents();
        _webview.Close();
    }

    private async void InitWebView()
    {
        if (_webview is null) return;

        SubscribeWebViewEvents();

        CoreWebView2EnvironmentOptions environmentOptions = new CoreWebView2EnvironmentOptions();
        environmentOptions.AdditionalBrowserArguments = "--disable-features=msSmartScreenProtection";
        string? browserFolder = null; // Use null to get default browser folder
        string? userDataFolder = null; // Use null to get default user data folder
        CoreWebView2Environment environment = await CoreWebView2Environment.CreateWithOptionsAsync(
            browserFolder, userDataFolder, environmentOptions);
        await _webview.EnsureCoreWebView2Async(environment);
        // https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft_web_webview2_core/corewebview2settings?view=webview2-winrt-1.0.3719.77#aredefaultscriptdialogsenabled
        _webview.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
        // https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft_web_webview2_core/corewebview2settings?view=webview2-winrt-1.0.3719.77#ispasswordautosaveenabled
        _webview.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
        SubscribeCoreEvents();
        _webview.Source = new Uri(Url_Gersang_Main);
    }

    private void SubscribeWebViewEvents()
    {
        if (_webview is null) return;

        _webview.CoreWebView2Initialized += OnCoreWebView2Initialized;
        _webview.NavigationStarting += OnNavigationStarting;
        _webview.NavigationCompleted += OnNavigationCompleted;
        _webview.WebMessageReceived += OnWebMessageReceived;
    }

    private void UnsubscribeWebViewEvents()
    {
        if (_webview is null) return;

        _webview.CoreWebView2Initialized -= OnCoreWebView2Initialized;
        _webview.NavigationStarting -= OnNavigationStarting;
        _webview.NavigationCompleted -= OnNavigationCompleted;
        _webview.WebMessageReceived -= OnWebMessageReceived;
    }

    private void SubscribeCoreEvents()
    {
        var core = _webview?.CoreWebView2;
        if (core is null) return;

        core.SourceChanged += OnSourceChanged;
        core.HistoryChanged += OnHistoryChanged;
        core.DOMContentLoaded += OnDOMContentLoaded;
        core.ScriptDialogOpening += OnScriptDialogOpening;
        core.NotificationReceived += OnNotificationReceived;
    }

    private void UnsubscribeCoreEvents()
    {
        var core = _webview?.CoreWebView2;
        if (core is null) return;

        core.SourceChanged -= OnSourceChanged;
        core.HistoryChanged -= OnHistoryChanged;
        core.DOMContentLoaded -= OnDOMContentLoaded;
        core.ScriptDialogOpening -= OnScriptDialogOpening;
        core.NotificationReceived -= OnNotificationReceived;
    }

    private void OnCoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
    {
#if DEBUGGING
        Debug.WriteLine($"[WebView::OnCoreWebView2Initialized]");
        if (args.Exception is null)
        {
            Debug.WriteLine("\t- Initialization succeeded.");
            return;
        }
        Debug.WriteLine($"\t- Exception.Message: {args.Exception.Message}");
        Debug.WriteLine($"\t- Exception.StackTrace: {args.Exception.StackTrace}");
        Debug.WriteLine($"\t- Exception.Source: {args.Exception.Source}");
        Debug.WriteLine($"\t- Exception.Data");
        foreach (var key in args.Exception.Data.Keys)
        {
            Debug.WriteLine($"\t\t- [{key}]: {args.Exception.Data[key]}");
        }
#endif
    }

    private void OnWebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
#if DEBUGGING
        Debug.WriteLine($"[WebView::OnWebMessageReceived]");
        Debug.WriteLine($"\t- Source: {args.Source}");
        Debug.WriteLine($"\t- AdditionalObjects.Count: {args.AdditionalObjects.Count}");
        Debug.WriteLine($"\t- WebMessageAsJson: {args.WebMessageAsJson}");
#endif
    }

    private void OnNotificationReceived(CoreWebView2 sender, CoreWebView2NotificationReceivedEventArgs args)
    {
#if DEBUGGING
        Debug.WriteLine($"[WebView::OnNotificationReceived]");
        Debug.WriteLine($"\t- SenderOrigin: {args.SenderOrigin}");
        Debug.WriteLine($"\t- Notification");
        Debug.WriteLine($"\t\t- Timestamp: {args.Notification.Timestamp}");
        Debug.WriteLine($"\t\t- Tag: {args.Notification.Tag}");
        Debug.WriteLine($"\t\t- Title: {args.Notification.Title}");
        Debug.WriteLine($"\t\t- Body: {args.Notification.Body}");
        Debug.WriteLine($"\t\t- Language: {args.Notification.Language}");
        Debug.WriteLine($"\t\t- ShouldRenotify: {args.Notification.ShouldRenotify}");
        Debug.WriteLine($"\t\t- IsSilent: {args.Notification.IsSilent}");
        Debug.WriteLine($"\t\t- RequiresInteraction: {args.Notification.RequiresInteraction}");
        Debug.WriteLine($"\t\t- VibrationPattern.Count: {args.Notification.VibrationPattern.Count}");
        Debug.WriteLine($"\t\t- IconUri: {args.Notification.IconUri}");
        Debug.WriteLine($"\t\t- BodyImageUri: {args.Notification.BodyImageUri}");
        Debug.WriteLine($"\t\t- BadgeUri: {args.Notification.BadgeUri}");
#endif
    }

    public event TypedEventHandler<CoreWebView2, CoreWebView2SourceChangedEventArgs> SourceChanged;
    private void OnSourceChanged(CoreWebView2 sender, CoreWebView2SourceChangedEventArgs args)
    {
#if DEBUGGING
        Debug.WriteLine($"[WebView::OnSourceChanged]");
        Debug.WriteLine($"\t- IsNewDocument: {args.IsNewDocument}");
        Debug.WriteLine($"\t- Source: {sender.Source}");
#endif
        CurrentTitle = sender.DocumentTitle;
        CurrentSource = sender.Source;

        SourceChanged?.Invoke(sender, args);
    }

    private void OnHistoryChanged(CoreWebView2 sender, object args)
    {
#if DEBUGGING
        Debug.WriteLine($"[WebView::OnHistoryChanged]");
        Debug.WriteLine($"\t- CanGoBack: {sender.CanGoBack}");
        Debug.WriteLine($"\t- CanGoForward: {sender.CanGoForward}");
#endif
        CanGoBack = sender.CanGoBack;
        CanGoForward = sender.CanGoForward;
    }

    private async void ContentDialog_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ContentDialog dialog)
            return;

        for (int seconds = 5; seconds >= 1; --seconds)
        {
            dialog.PrimaryButtonText = $"확인({seconds})";
            dialog.IsPrimaryButtonEnabled = false;
            await Task.Delay(1000);
        }

        dialog.PrimaryButtonText = "확인";
        dialog.IsPrimaryButtonEnabled = true;
    }

    private async Task ShowOtpDialogAsync(bool reEnter = false)
    {
        var inputTextBox = new TextBox
        {
            Text = "",
            PlaceholderText = "OTP 코드 8자리를 입력해주세요."
        };
        var errorTextBox = new TextBlock
        {
            Visibility = Visibility.Collapsed
        };
        inputTextBox.BeforeTextChanging += (s, e) =>
        {
            foreach (char c in e.NewText)
            {
                if (c < '0' || c > '9')
                {
                    errorTextBox.Text = "숫자만 입력 가능합니다.";
                    errorTextBox.Visibility = Visibility.Visible;
                    e.Cancel = true;
                    return;
                }
            }
            errorTextBox.Visibility = Visibility.Collapsed;
        };

        var dlg = new ContentDialog
        {
            XamlRoot = _currentWindow.Content.XamlRoot,
            Title = "거상 OTP 인증번호",
            Content = new StackPanel
            {
                Spacing = 8,
                Children = { inputTextBox, errorTextBox }
            },
            PrimaryButtonText = "확인",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Primary
        };
        dlg.PrimaryButtonClick += (s, e) =>
        {
            string input = inputTextBox.Text;
            bool invalid = string.IsNullOrEmpty(input) || input.Length != 8;
            if (invalid)
            {
                errorTextBox.Text = "OTP 코드는 8자리입니다.";
                errorTextBox.Visibility = Visibility.Visible;
                e.Cancel = true;
            }
        };

        if (reEnter)
        {
            // 5초 동안 확인 버튼 잠금
            dlg.Loaded += ContentDialog_Loaded;
        }
            
        var result = await dlg.ShowAsync();
        dlg.Loaded -= ContentDialog_Loaded;
        if (result == ContentDialogResult.Primary)
        {
            await _webview.ExecuteScriptAsync(InputOtpScript(inputTextBox.Text));
            await _webview.ExecuteScriptAsync(SubmitOtpScript);
        } 
        else
        {
            TryingLogin = false;
            TryingGameStart = false;
            _webview.Source = new Uri(Url_Gersang_Main);
        }
    }

    private async Task HandleScriptDialogAsync(CoreWebView2ScriptDialogOpeningEventArgs args)
    {
        switch (args.Kind)
        {
            case CoreWebView2ScriptDialogKind.Alert:
                {
                    var dlg = new ContentDialog
                    {
                        XamlRoot = _currentWindow.Content.XamlRoot,
                        Title = "알림",
                        Content = args.Message,
                        CloseButtonText = "확인",
                        DefaultButton = ContentDialogButton.Close
                    };

                    await dlg.ShowAsync();
                    args.Accept();
                    break;
                }

            case CoreWebView2ScriptDialogKind.Confirm:
                {
                    var dlg = new ContentDialog
                    {
                        XamlRoot = _currentWindow.Content.XamlRoot,
                        Title = "확인",
                        Content = args.Message,
                        PrimaryButtonText = "확인",
                        CloseButtonText = "취소",
                        DefaultButton = ContentDialogButton.Primary
                    };

                    var result = await dlg.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                        args.Accept(); // true
                                       // else: false (Accept 안함)
                    break;
                }

            case CoreWebView2ScriptDialogKind.Prompt:
                {
                    var input = new TextBox
                    {
                        Text = args.DefaultText ?? "",
                        PlaceholderText = ""
                    };

                    var dlg = new ContentDialog
                    {
                        XamlRoot = _currentWindow.Content.XamlRoot,
                        Title = "입력",
                        Content = input,
                        PrimaryButtonText = "확인",
                        CloseButtonText = "취소",
                        DefaultButton = ContentDialogButton.Primary
                    };

                    var result = await dlg.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        args.ResultText = input.Text; // prompt 결과 문자열
                        args.Accept();
                    }
                    break;
                }
        }
    }

    private async void OnDOMContentLoaded(CoreWebView2 sender, CoreWebView2DOMContentLoadedEventArgs args)
    {
#if DEBUGGING
        Debug.WriteLine($"[WebView::OnDOMContentLoaded]");
        Debug.WriteLine($"\t- NavigationId: {args.NavigationId}");
        Debug.WriteLine($"\t- CoreWebView2.Source: {sender.Source}");
        Debug.WriteLine($"\t- CoreWebView2.DocumentTitle: {sender.DocumentTitle}");
        Debug.WriteLine($"\t- CoreWebView2.StatusBarText: {sender.StatusBarText}");
#endif
        await UpdateLoginStateByCookieAsync();
        if (TryingLogin && _currentSource.Contains(Url_Gersang_Otp))
        {
            await _webview!.DispatcherQueue.RunOrEnqueueAsync(() => ShowOtpDialogAsync(false));
        }

        // TODO: sender.DocumentTitle.Contains("점검")
        // -> 거상 홈페이지 조작 관련 기능 비활성화 및 안내

        // TODO: url.Contains("member/convert.gs")
        // -> 메인 주소로 이동 (주민등록번호 파기로 인한 인증정보 없는 계정들 전용)

        // TODO: url.Contains("loginCertUp.gs")
        // -> 휴대폰 본인 인증 안내 및 브라우저탭으로 이동

        // TODO: url.Contains("pw_reset.gs")
        // -> 필요하면 메인 주소로 이동 처리 (딱히 해야 할 이유는 없어보임)

        IsBusy = false;
    }

    private async void OnScriptDialogOpening(CoreWebView2 sender, CoreWebView2ScriptDialogOpeningEventArgs args)
    {
#if DEBUGGING
        Debug.WriteLine($"[WebView::OnScriptDialogOpening]");
        Debug.WriteLine($"\t- Kind: {args.Kind}");
        Debug.WriteLine($"\t- Message: {args.Message}");
        Debug.WriteLine($"\t- DefaultText: {args.DefaultText}");
        Debug.WriteLine($"\t- ResultText: {args.ResultText}");
        Debug.WriteLine($"\t- Uri: {args.Uri}");
#endif

        // TODO: "아이디 또는 비밀번호 오류" 포함
        // -> 아이디와 패스워드 재확인 후 재시도 안내 + 5초간 버튼 비활성화

        // TODO: 해당IP는 로그인이 차단되었습니다. 고객센터에 문의하세요.
        // -> 사용자에게 고객센터 문의 안내 (또는 고객센터 페이지 이동 후 브라우저탭으로 전환)

        // TODO: 5초 후에 재로그인 가능합니다.
        // -> 아이디와 패스워드 재확인 후 재시도 안내 + 5초간 버튼 비활성화

        var deferral = args.GetDeferral();
        try
        {
            await _webview!.DispatcherQueue.RunOrEnqueueAsync(() => HandleScriptDialogAsync(args));
        }
        finally
        {
            deferral.Complete();
        }

        if (args.Message.Contains("인증번호가 다릅니다"))
        {
            await _webview!.DispatcherQueue.RunOrEnqueueAsync(() => ShowOtpDialogAsync(true));
            return;
        }
    }

    private void OnNavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
#if DEBUGGING
        Debug.WriteLine($"[WebView::OnNavigationStarting]");
        Debug.WriteLine($"\t- NavigationId: {args.NavigationId}");
        Debug.WriteLine($"\t- NavigationKind: {args.NavigationKind}");
        Debug.WriteLine($"\t- Uri: {args.Uri}");
        Debug.WriteLine($"\t- IsRedirected: {args.IsRedirected}");
        Debug.WriteLine($"\t- IsUserInitiated: {args.IsUserInitiated}");
        Debug.WriteLine($"\t- RequestHeaders");
        foreach (var kvp in args.RequestHeaders)
        {
            Debug.WriteLine($"\t\t- [{kvp.Key}, {kvp.Value}]");
        }
#endif
        IsBusy = true;
    }

    private void OnNavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
#if DEBUGGING
        Debug.WriteLine($"[WebView::OnNavigationCompleted]");
        Debug.WriteLine($"\t- NavigationId: {args.NavigationId}");
        Debug.WriteLine($"\t- IsSuccess: {args.IsSuccess}");
        Debug.WriteLine($"\t- HttpStatusCode: {args.HttpStatusCode}");
        Debug.WriteLine($"\t- WebErrorStatus: {args.WebErrorStatus}");
#endif
    }

    internal void GoBack()
    {
        _webview?.GoBack();
    }

    internal void GoForward()
    {
        _webview?.GoForward();
    }

    internal void Refresh()
    {
        _webview?.Reload();
    }

    internal void GoHome()
    {
        if (_webview is not null)
        {
            _webview.Source = new Uri(Url_Gersang_Main);
        }
    }
    #endregion WebViewManager Core
}
