#define DEBUGGING

using Core;
using Core.Models;
using GersangStation.Main;
using GersangStation.Main.Setting;
using GersangStation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace GersangStation.Services;

public enum TryLoginResult
{
    Success,
    InvalidId,
    NotFoundPw,
    VaultUnavailable,
    NullWebview
}

public sealed partial class WebViewManager : IDisposable, INotifyPropertyChanged
{
    #region Gersang Homepage Controller
    private static readonly TimeSpan LaunchRetryCooldown = TimeSpan.FromSeconds(5);
    private static bool _roughLoginNoticeSuppressedForSession;
    private static bool _roughLoginNoticeShowing;
    private static Task? _roughLoginNoticeTask;
    private GameServer _cachedGameStartServer = GameServer.Korea_Live;
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
    private string _roughLoginRecoveryTargetId = string.Empty;
    private bool _roughLoginRecoveryBypassUsed;
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
    private bool _wasRoughLoggedIn;
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
    private const string Url_Gersang_Main_Apex = "https://gersang.co.kr";
    private const string Url_Gersang_Otp = "https://www.gersang.co.kr/member/otp.gs";
    private const string Url_Gersang_Logout = "https://www.gersang.co.kr/member/logoutProc.gs";
    private Uri? _pendingNavigationUri;
    private string? _pendingHtmlContent;
    private bool _initialHomeNavigationCompleted;

    private const string TryLoginScript = $"document.getElementById('btn_Login').click()";
    private const string SubmitOtpScript = $"document.querySelector('form[action=\"otpProc.gs\"]').submit()";
    private static string InputIdScript(string id) => $"document.getElementById('GSuserID').value = '{id}'";
    private static string InputPwScript(string pw) => $"document.getElementById('GSuserPW').value = '{pw}'";
    private static string InputOtpScript(string otpCode) => $"document.getElementById('GSotpNo').value = '{otpCode}'";
    private static string SocketStartScript(string serverParam) => $"startRetry = setTimeout(\"socketStart('{serverParam}')\", 2000);";
    private const string DetectAuthenticatedDomStateScript =
        """
        (() => {
            const headerMember = document.querySelector('.top_wrap .member');
            const hasHeaderLoginLink = !!headerMember?.querySelector('a[href*="/member/login.gs"]');
            const hasHeaderLogoutLink = !!headerMember?.querySelector('a[href*="logoutProc.gs"]');
            const hasHeaderMyPageLink = !!headerMember?.querySelector('a[href*="/mypage/information.gs"]');

            return {
                HasHeaderLoginLink: hasHeaderLoginLink,
                HasHeaderLogoutLink: hasHeaderLogoutLink,
                HasHeaderMyPageLink: hasHeaderMyPageLink,
                LocationHref: window.location.href
            };
        })()
        """;

    private sealed record DomLoginState(
        bool HasHeaderLoginLink,
        bool HasHeaderLogoutLink,
        bool HasHeaderMyPageLink,
        string? LocationHref)
    {
        public bool LooksAuthenticated => HasHeaderLogoutLink || HasHeaderMyPageLink;
    }

    /// <summary>
    /// 예약만 된 게임 시작 세션을 취소하고 WebView 측 시작 상태를 정리합니다.
    /// </summary>
    private void CancelPendingGameStart(string reason)
    {
        if (_cachedGameStartClientIndex >= 0 && _cachedGameStartClientIndex < 3)
            _gameStarter.CancelStart(_cachedGameStartServer, _cachedGameStartClientIndex, reason);

        _cachedGameStartId = string.Empty;
        _cachedGameStartClientIndex = -1;
        _cachedInstallPath = string.Empty;
        ResetRoughLoginRecoveryState();
        TryingGameStart = false;
    }

    /// <summary>
    /// memberID 쿠키를 모르는 rough 로그인 복구 상태를 초기화합니다.
    /// </summary>
    private void ResetRoughLoginRecoveryState()
    {
        _roughLoginRecoveryTargetId = string.Empty;
        _roughLoginRecoveryBypassUsed = false;
    }

    /// <summary>
    /// 러프 로그인 상태에 진입했을 때 안내 다이얼로그를 표시하고 사용자가 닫을 때까지 기다립니다.
    /// </summary>
    private async Task WaitForRoughLoginNoticeAsync()
    {
        if (_roughLoginNoticeSuppressedForSession)
            return;

        Task? existingTask = _roughLoginNoticeTask;
        if (existingTask is not null && !existingTask.IsCompleted)
        {
            await existingTask;
            return;
        }

        _roughLoginNoticeTask = _webview.DispatcherQueue.RunOrEnqueueAsync(async () =>
        {
            if (_roughLoginNoticeSuppressedForSession || _roughLoginNoticeShowing)
                return;

            _roughLoginNoticeShowing = true;
            try
            {
                await ShowRoughLoginNoticeDialogAsync();
            }
            finally
            {
                _roughLoginNoticeShowing = false;
            }
        });

        try
        {
            await _roughLoginNoticeTask;
        }
        finally
        {
            if (_roughLoginNoticeTask?.IsCompleted == true)
                _roughLoginNoticeTask = null;
        }
    }

    /// <summary>
    /// 러프 로그인 감지 시 안내 및 제보 요청 다이얼로그를 표시합니다.
    /// </summary>
    private async Task ShowRoughLoginNoticeDialogAsync()
    {
        ContentDialog dialog = new()
        {
            XamlRoot = _currentWindow.Content.XamlRoot,
            Title = "로그인 ID 확인 실패",
            Content =
                "로그인에 성공하였지만 홈페이지에 로그인 된 ID를 확인하는데 실패하였습니다.\n" +
                "현재 이 상황과 관련하여 정보를 수집하고 있습니다.\n" +
                "잠시 시간 내주시어 문의 채널을 통해 말씀주시면 테스트 방법을 알려드리겠습니다.\n" +
                "프로그램을 킨 동안 이 메시지를 표시하지 않으시려면 \"다음부터 표시하지 않음\" 버튼을 눌러주세요.",
            PrimaryButtonText = "확인",
            SecondaryButtonText = "다음부터 표시하지 않음",
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Secondary)
            _roughLoginNoticeSuppressedForSession = true;
    }

    /// <summary>
    /// 휴대폰 본인 인증이 필요한 경우 브라우저 탭으로 유도합니다.
    /// </summary>
    private async Task ShowPhoneVerificationGuideAsync()
    {
        var dialog = new ContentDialog
        {
            XamlRoot = _currentWindow.Content.XamlRoot,
            Title = "휴대폰 본인 인증 필요",
            Content = "게임 실행 전에 거상 웹페이지에서 휴대폰 본인 인증을 완료해주세요. 브라우저 탭으로 이동합니다.",
            CloseButtonText = "확인",
            DefaultButton = ContentDialogButton.Close
        };

        await dialog.ShowAsync();

        if (_currentWindow is MainWindow mainWindow)
            mainWindow.NavigateToWebViewPage();
    }

    /// <summary>
    /// 현재 진행 중인 로그인/로그아웃/게임 실행 시도를 모두 취소하고 시작 슬롯을 원래 상태로 되돌립니다.
    /// </summary>
    public void CancelLaunchAttempt(GameServer server, int clientIndex, string reason = "사용자 취소")
    {
        try
        {
            _webview?.CoreWebView2?.Stop();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WebViewManager] WebView 중단 실패: {ex}");
        }

        GameServer cachedServer = _cachedGameStartServer;
        int cachedClientIndex = _cachedGameStartClientIndex;

        if (clientIndex >= 0 && clientIndex < 3)
            _gameStarter.CancelStart(server, clientIndex, reason);

        if (cachedClientIndex >= 0 && cachedClientIndex < 3
            && (cachedServer != server || cachedClientIndex != clientIndex))
        {
            _gameStarter.CancelStart(cachedServer, cachedClientIndex, reason);
        }

        _pendingNavigationUri = null;
        _pendingHtmlContent = null;
        TryingLogin = false;
        TryingLogout = false;
        TryingGameStart = false;
        _cachedLoginId = string.Empty;
        _cachedGameStartId = string.Empty;
        _cachedGameStartClientIndex = -1;
        _cachedInstallPath = string.Empty;
        ResetRoughLoginRecoveryState();
        IsBusy = false;

        Debug.WriteLine($"[WebViewManager] 실행 취소. server:{server}, clientIndex:{clientIndex}, reason:{reason}");
    }

    /// <summary>
    /// 계정 로그인과 게임 실행 준비를 묶어 처리합니다.
    /// </summary>
    public async Task<bool> TryGameStart(string id, int clientIndex)
    {
        TryingGameStart = false;

        if (_webview is null || 3 <= clientIndex || clientIndex < 0)
            return false;

        GameServer selectedServer = AppDataManager.SelectedServer;
        ClientSettings settings = AppDataManager.LoadServerClientSettings(selectedServer);
        string installPath = clientIndex switch
        {
            0 => settings.InstallPath,
            1 => settings.Client2Path,
            2 => settings.Client3Path,
            _ => throw new ArgumentOutOfRangeException(nameof(clientIndex), clientIndex, null),
        };

        // 재진입/재시도 중 서버 선택이 바뀌어도, 사용자가 버튼을 누른 순간의 서버/경로를 끝까지 유지합니다.
        _cachedGameStartServer = selectedServer;
        _cachedGameStartId = id;
        _cachedGameStartClientIndex = clientIndex;
        _cachedInstallPath = installPath;

        if (!_gameStarter.TryBeginStart(selectedServer, clientIndex, installPath, id))
            return false;

        // 게임 실행 시도는 현재 위치와 관계없이 거상 메인 페이지에서 다시 이어갑니다.
        if (!IsGersangMainPage(_webview.Source))
        {
            TryingGameStart = true;
            NavigateToGersangMain("게임 실행");
            return true;
        }

        TryLoginResult tryLoginResult = await TryLogin(id);
        switch (tryLoginResult)
        {
            case TryLoginResult.Success:
                break;
            case TryLoginResult.InvalidId:
                CancelPendingGameStart("아이디가 비어 있음");
                return false;
            case TryLoginResult.NotFoundPw:
                CancelPendingGameStart("비밀번호 없음");
                return false;
            case TryLoginResult.VaultUnavailable:
                CancelPendingGameStart("비밀번호 저장소 접근 실패");
                _ = App.ExceptionHandler.ShowRecoverableAsync(
                    new InvalidOperationException("윈도우 자격 증명 관리자에서 비밀번호를 읽지 못했습니다."),
                    "WebViewManager.TryGameStart");
                return false;
            case TryLoginResult.NullWebview:
                CancelPendingGameStart("WebView 없음");
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

    /// <summary>
    /// 현재 로그인 세션을 로그아웃 페이지로 이동시켜 정리합니다.
    /// </summary>
    public async Task TryLogout()
    {
        _webview.Source = new Uri(Url_Gersang_Logout);
        TryingLogout = true;
    }

    private static bool IsGersangHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        return host.Equals("gersang.co.kr", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".gersang.co.kr", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGersangDomain(Uri? uri)
    {
        return uri is not null
            && uri.Scheme == Uri.UriSchemeHttps
            && IsGersangHost(uri.Host);
    }

    /// <summary>
    /// 현재 URI가 거상 공식 메인 페이지인지 확인합니다.
    /// </summary>
    private static bool IsGersangMainPage(Uri? uri)
    {
        if (!IsGersangDomain(uri))
            return false;

        string absoluteUri = uri!.AbsoluteUri;
        string path = uri.AbsolutePath;
        return string.IsNullOrEmpty(path)
            || path == "/"
            || absoluteUri.Contains("main/index.gs", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 로그인 또는 게임 실행 전에 거상 공식 메인 페이지로 이동시킵니다.
    /// </summary>
    private void NavigateToGersangMain(string reason)
    {
        Debug.WriteLine($"{reason} 시도 전에 거상 메인 페이지로 이동합니다. CurrentSource: {_webview.Source}");
        _webview.Source = new Uri(Url_Gersang_Main);
    }

    /// <summary>
    /// 현재 페이지 상태를 고려해 거상 로그인 과정을 진행합니다.
    /// </summary>
    public async Task<TryLoginResult> TryLogin(string id)
    {
        if (_webview is null)
            return TryLoginResult.NullWebview;

        // True 상태로 TryLogin 함수에 진입한 경우를 대비
        TryingLogin = false;

        if (LoggedIn)
        {
            if (!string.IsNullOrWhiteSpace(LoggedInMemberId)
                && LoginIdComparer.EqualsForComparison(LoggedInMemberId, id))
            {
                // 로그인 하려는 아이디와 현재 로그인 된 아이디가 같으면 로그인 할 필요 없다
                ResetRoughLoginRecoveryState();
                Debug.WriteLine("이미 동일한 계정으로 로그인 되어 있으므로 로그인 과정을 스킵합니다.");
                return TryLoginResult.Success;
            }

            if (!string.IsNullOrWhiteSpace(LoggedInMemberId))
            {
                // 로그아웃 해야 한다
                Debug.WriteLine("다른 계정으로 로그인 되어 있으므로 로그아웃 후 로그인을 시도합니다.");
                TryingLogin = true;
                _cachedLoginId = id;
                await TryLogout();
                return TryLoginResult.Success;
            }

            if (string.Equals(_roughLoginRecoveryTargetId, id, StringComparison.Ordinal)
                && !_roughLoginRecoveryBypassUsed)
            {
                _roughLoginRecoveryBypassUsed = true;
                Debug.WriteLine("[WebViewManager] rough 로그인 복구 후에도 memberID를 확인하지 못했습니다. 무한 로그인을 막기 위해 현재 세션으로 진행합니다.");
                return TryLoginResult.Success;
            }

            Debug.WriteLine("[WebViewManager] 로그인 상태는 확인됐지만 memberID 쿠키를 찾지 못했습니다. 선택된 계정으로 다시 로그인하기 위해 로그아웃합니다.");
            _roughLoginRecoveryTargetId = id;
            _roughLoginRecoveryBypassUsed = false;
            TryingLogin = true;
            _cachedLoginId = id;
            await TryLogout();
            return TryLoginResult.Success;
        }
            
        if (string.IsNullOrWhiteSpace(id))
            return TryLoginResult.InvalidId;

        PasswordVaultHelper.PasswordVaultReadResult passwordResult = PasswordVaultHelper.TryGetPassword(id);
        if (!passwordResult.Success)
            return TryLoginResult.VaultUnavailable;

        string? pw = passwordResult.HasCredential ? passwordResult.Password : null;
        if (string.IsNullOrWhiteSpace(pw))
            return TryLoginResult.NotFoundPw;

        TryingLogin = true;

        // 로그인 시도는 현재 위치와 관계없이 거상 메인 페이지에서 다시 이어갑니다.
        if (!IsGersangMainPage(_webview.Source))
        {
            _cachedLoginId = id;
            NavigateToGersangMain("로그인");
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

    /// <summary>
    /// local socket payload를 외부 런처 인자 모델로 변환합니다.
    /// </summary>
    private static GameStartPayload? ParseGameStartPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        string[] parts = payload.Split('\t');
        if (parts.Length < 3)
            return null;

        string id = parts[1];
        string pw = parts[2];
        string? accountId = parts.Length >= 4 ? parts[3] : null;
        return new GameStartPayload(id, pw, accountId);
    }

    /// <summary>
    /// socketStart 스크립트를 실행하고 payload를 수신한 뒤 GameStarter로 실행을 위임합니다.
    /// </summary>
    private async Task StartGameThroughLocalSocketAsync(GameServer selectedServer, int clientIndex, string serverParam, string clientInstallPath)
    {
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
            _gameStarter.CancelStart(selectedServer, clientIndex, "WebSocket 수신 시간 초과");
            return;
        }

        Debug.WriteLine($"payload: {payload}");

        GameStartPayload? gameStartPayload = payload is null ? null : ParseGameStartPayload(payload);
        if (gameStartPayload is null)
        {
            _gameStarter.CancelStart(selectedServer, clientIndex, "payload 파싱 실패");
            return;
        }

        bool started = await _gameStarter.StartAsync(
            selectedServer,
            clientIndex,
            clientInstallPath,
            _cachedGameStartId,
            gameStartPayload);
        Debug.WriteLine($"[WebViewManager] GameStarter.StartAsync result:{started}");
    }

    /// <summary>
    /// 로그인 이후 실제 게임 실행 직전 검사를 수행하고 선택 당시 서버 기준으로 실행을 이어갑니다.
    /// </summary>
    private async Task GameStart(string clientInstallPath)
    {
        if (!TryingGameStart)
            return;
        TryingGameStart = false;

        // 서버 콤보박스가 나중에 바뀌어도, 이미 눌린 실행 요청은 원래 서버 기준으로 끝까지 처리합니다.
        GameServer selectedServer = _cachedGameStartServer;

        string param = GameServerHelper.GetGameStartParam(selectedServer);
        await StartGameThroughLocalSocketAsync(selectedServer, _cachedGameStartClientIndex, param, clientInstallPath);
    }

    private async Task UpdateLoginStateByCookieAsync()
    {
        if (_webview is null)
            return;

        var core = _webview.CoreWebView2;
        if (core is null)
            return;

        bool previousLoggedIn = LoggedIn;
        string previousLoggedInMemberId = LoggedInMemberId;

        (string updatedLoggedInMemberId, string cookieDebugSummary) =
            await TryGetLoggedInMemberIdFromCookiesAsync(core);
        DomLoginState? domLoginState = null;
        bool isRoughLoggedIn = false;
        if (string.IsNullOrWhiteSpace(updatedLoggedInMemberId)
            && (TryingLogin || TryingGameStart))
        {
            domLoginState = await TryDetectLoggedInFromDomAsync();
            isRoughLoggedIn = domLoginState?.LooksAuthenticated == true;
        }

        bool enteredRoughLogin = isRoughLoggedIn && !_wasRoughLoggedIn;
        _wasRoughLoggedIn = isRoughLoggedIn;

        bool updatedLoggedIn = !string.IsNullOrWhiteSpace(updatedLoggedInMemberId) || isRoughLoggedIn;
        bool isLoggedInMemberIdChanged = !string.Equals(previousLoggedInMemberId, updatedLoggedInMemberId, StringComparison.Ordinal);
        bool isLoggedInChanged = previousLoggedIn != updatedLoggedIn;
        LoggedInMemberId = updatedLoggedInMemberId;
        LoggedIn = updatedLoggedIn;
        if (!string.IsNullOrWhiteSpace(updatedLoggedInMemberId))
            ResetRoughLoginRecoveryState();
        if (isLoggedInMemberIdChanged || isLoggedInChanged)
            LoggedInChanged?.Invoke(this, EventArgs.Empty);
        Debug.WriteLine($"TryingLogin: {TryingLogin}, IsLoggedIn: {LoggedIn}, LoggedInMemberId: {LoggedInMemberId}, RoughLoggedIn: {isRoughLoggedIn}");

        if (string.IsNullOrWhiteSpace(updatedLoggedInMemberId))
        {
            Debug.WriteLine($"[WebViewManager] memberID 쿠키를 찾지 못했습니다. CurrentSource: {_currentSource}, CookieLookups: {cookieDebugSummary}");
            if (domLoginState is not null)
            {
                Debug.WriteLine(
                    $"[WebViewManager] DOM 로그인 판정. LooksAuthenticated:{domLoginState.LooksAuthenticated}, HasHeaderLoginLink:{domLoginState.HasHeaderLoginLink}, HasHeaderLogoutLink:{domLoginState.HasHeaderLogoutLink}, HasHeaderMyPageLink:{domLoginState.HasHeaderMyPageLink}, Location:{domLoginState.LocationHref}");
            }
        }

        if (enteredRoughLogin)
            await WaitForRoughLoginNoticeAsync();

        if (LoggedIn)
        {
            TryingLogin = false;

            if (TryingGameStart)
            {
                if (Uri.TryCreate(_currentSource, UriKind.Absolute, out Uri? currentUri)
                    && IsGersangMainPage(currentUri))
                {
                    await TryGameStart(_cachedGameStartId, _cachedGameStartClientIndex);
                }
                else
                {
                    NavigateToGersangMain("게임 실행");
                }
            }
        }
        else
        {
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
            else if (TryingGameStart
                && Uri.TryCreate(_currentSource, UriKind.Absolute, out Uri? currentUri)
                && IsGersangMainPage(currentUri)
                && !string.IsNullOrWhiteSpace(_cachedGameStartId)
                && _cachedGameStartClientIndex is >= 0 and < 3)
            {
                await TryGameStart(_cachedGameStartId, _cachedGameStartClientIndex);
            }
        }
    }

    /// <summary>
    /// 현재 페이지와 거상 루트 도메인 후보들에서 로그인 식별 쿠키를 조회합니다.
    /// </summary>
    private async Task<(string MemberId, string DebugSummary)> TryGetLoggedInMemberIdFromCookiesAsync(CoreWebView2 core)
    {
        List<string> cookieDebugEntries = [];

        foreach (string lookupUri in BuildCookieLookupUris(_currentSource))
        {
            IReadOnlyList<CoreWebView2Cookie> cookies = await core.CookieManager.GetCookiesAsync(lookupUri);
            cookieDebugEntries.Add(
                $"{lookupUri} => [{string.Join(", ", cookies.Select(cookie => $"{cookie.Name}@{cookie.Domain}{cookie.Path}"))}]");

            foreach (CoreWebView2Cookie cookie in cookies)
            {
                if (string.Equals(cookie.Name, "memberID", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(cookie.Value))
                {
                    return (cookie.Value, string.Join(" | ", cookieDebugEntries));
                }
            }
        }

        return (string.Empty, string.Join(" | ", cookieDebugEntries));
    }

    /// <summary>
    /// 쿠키가 비어 있을 때 현재 DOM이 인증 완료 상태처럼 보이는지 거칠게 판정합니다.
    /// </summary>
    private async Task<DomLoginState?> TryDetectLoggedInFromDomAsync()
    {
        try
        {
            string scriptResult = await _webview.ExecuteScriptAsync(DetectAuthenticatedDomStateScript);
            if (string.IsNullOrWhiteSpace(scriptResult) || string.Equals(scriptResult, "null", StringComparison.Ordinal))
                return null;

            return JsonSerializer.Deserialize<DomLoginState>(scriptResult);
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[WebViewManager] DOM 로그인 판정 JSON 파싱 실패: {ex}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WebViewManager] DOM 로그인 판정 실패: {ex}");
            return null;
        }
    }

    /// <summary>
    /// 현재 위치와 대표 URL을 조합해 memberID 쿠키가 저장될 수 있는 조회 후보를 만듭니다.
    /// </summary>
    private static IReadOnlyList<string> BuildCookieLookupUris(string? currentSource)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<string> candidates = [];

        void Add(string? rawUri)
        {
            if (!Uri.TryCreate(rawUri, UriKind.Absolute, out Uri? uri))
                return;

            if (uri.Scheme != Uri.UriSchemeHttps || !IsGersangHost(uri.Host))
                return;

            string normalizedUri = uri.GetLeftPart(UriPartial.Path);
            if (seen.Add(normalizedUri))
                candidates.Add(normalizedUri);
        }

        Add(currentSource);
        Add(Url_Gersang_Main);
        Add(Url_Gersang_Main_Apex);
        Add(Url_Gersang_Otp);

        return candidates;
    }
    #endregion Gersang Homepage Controller


    #region WebViewManager Core
    private readonly WebView2 _webview;
    private readonly Window _currentWindow;
    private readonly GameStarter _gameStarter;

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

    public WebViewManager(WebView2 webview, Window window, GameStarter gameStarter)
    {
        _currentWindow = window;
        _webview = webview;
        _gameStarter = gameStarter;
        _ = InitWebViewAsync();
    }

    public void Dispose()
    {
        Debug.WriteLine($"[WebViewManager::Dispose]");
        UnsubscribeWebViewEvents();
        UnsubscribeCoreEvents();
        _webview.Close();
    }

    /// <summary>
    /// 브라우저 페이지가 전면에 있을 때 WebView 메모리 타깃을 일반 수준으로 되돌립니다.
    /// </summary>
    internal void SetActiveMemoryMode()
    {
        SetMemoryUsageTargetLevel(CoreWebView2MemoryUsageTargetLevel.Normal);
    }

    /// <summary>
    /// 브라우저 페이지가 비활성일 때 WebView 메모리 타깃을 낮춰 working set 축소를 유도합니다.
    /// </summary>
    internal void SetInactiveMemoryMode()
    {
        SetMemoryUsageTargetLevel(CoreWebView2MemoryUsageTargetLevel.Low);
    }

    /// <summary>
    /// WebView2 환경을 초기화하고 기본 홈페이지로 이동합니다.
    /// </summary>
    private async Task InitWebViewAsync()
    {
        if (_webview is null) return;

        try
        {
            _initialHomeNavigationCompleted = false;
            _pendingNavigationUri = null;
            _pendingHtmlContent = null;
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
        catch (Exception ex)
        {
            Debug.WriteLine($"[WebViewManager] WebView 초기화 실패: {ex}");
        }
    }

    /// <summary>
    /// 현재 WebView의 메모리 사용 목표를 설정합니다.
    /// </summary>
    private void SetMemoryUsageTargetLevel(CoreWebView2MemoryUsageTargetLevel targetLevel)
    {
        var core = _webview?.CoreWebView2;
        if (core is null || core.MemoryUsageTargetLevel == targetLevel)
            return;

        try
        {
            core.MemoryUsageTargetLevel = targetLevel;
            Debug.WriteLine($"[WebViewManager] MemoryUsageTargetLevel => {targetLevel}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WebViewManager] MemoryUsageTargetLevel 변경 실패: {ex}");
        }
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

    public event TypedEventHandler<CoreWebView2, CoreWebView2SourceChangedEventArgs>? SourceChanged;
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
            TryingLogout = false;
            _cachedLoginId = string.Empty;
            if (TryingGameStart || (_cachedGameStartClientIndex >= 0 && _cachedGameStartClientIndex < 3))
                CancelPendingGameStart("OTP 입력 취소");

            _webview.Source = new Uri(Url_Gersang_Main);
        }
    }

    private static bool IsCredentialFailureMessage(string message)
        => !string.IsNullOrWhiteSpace(message)
            && (message.Contains("아이디 또는 비밀번호 오류", StringComparison.OrdinalIgnoreCase)
                || message.Contains("아이디 또는 패스워드 오류", StringComparison.OrdinalIgnoreCase)
                || message.Contains("아이디 혹은 비밀번호 오류", StringComparison.OrdinalIgnoreCase)
                || message.Contains("아이디 혹은 패스워드 오류", StringComparison.OrdinalIgnoreCase));

    private static bool IsRetryBlockedMessage(string message)
        => !string.IsNullOrWhiteSpace(message)
            && message.Contains("5초 후에 재로그인 가능합니다", StringComparison.OrdinalIgnoreCase);

    private static bool IsOtpFailureMessage(string message)
        => !string.IsNullOrWhiteSpace(message)
            && (message.Contains("인증번호가 다릅니다", StringComparison.OrdinalIgnoreCase)
                || message.Contains("잘못된 암호입니다", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// OTP 오류 안내를 먼저 보여준 뒤 재입력 다이얼로그로 이어집니다.
    /// </summary>
    private async Task ShowOtpFailureDialogAsync(string message)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = _currentWindow.Content.XamlRoot,
            Title = "OTP 인증 실패",
            Content = message,
            PrimaryButtonText = "확인",
            DefaultButton = ContentDialogButton.Primary
        };

        await dlg.ShowAsync();

        await ShowOtpDialogAsync(true);
    }

    /// <summary>
    /// WebView script dialog를 즉시 닫은 뒤 OTP 실패 안내와 재입력을 UI 스레드에서 이어갑니다.
    /// </summary>
    private void QueueOtpFailureRecovery(string message)
    {
        _ = _webview.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await ShowOtpFailureDialogAsync(message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewManager] OTP 실패 복구 처리 중 예외: {ex}");
            }
        });
    }

    /// <summary>
    /// WebView script dialog를 즉시 닫은 뒤 로그인 실패 안내를 UI 스레드에서 표시합니다.
    /// </summary>
    private void QueueLaunchFailureDialog(string message)
    {
        _ = _webview.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await ShowLaunchFailureDialogAsync(message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewManager] 실행 실패 안내 표시 중 예외: {ex}");
            }
        });
    }

    /// <summary>
    /// 로그인 실패/재시도 제한 알림을 표시하고 필요 시 계정 설정 페이지로 이동합니다.
    /// </summary>
    private async Task ShowLaunchFailureDialogAsync(string message)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = _currentWindow.Content.XamlRoot,
            Title = "게임 실행 취소",
            Content = message,
            PrimaryButtonText = "확인",
            SecondaryButtonText = "계정 다시 설정",
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dlg.ShowAsync();
        if (result == ContentDialogResult.Secondary
            && _currentWindow is MainWindow mainWindow)
        {
            mainWindow.NavigateToSettingPage(SettingSection.Account);
        }
    }

    /// <summary>
    /// 인증 실패나 재시도 제한으로 게임 실행을 중단하고 슬롯 재시도 쿨다운을 시작합니다.
    /// </summary>
    private void CancelLaunchAttemptWithRetryCooldown(string reason)
    {
        GameServer server = _cachedGameStartServer;
        int clientIndex = _cachedGameStartClientIndex;
        bool hasPendingGameStart = clientIndex >= 0 && clientIndex < 3;

        TryingLogout = false;
        TryingLogin = false;
        _cachedLoginId = string.Empty;

        if (hasPendingGameStart)
        {
            CancelPendingGameStart(reason);
            _gameStarter.StartRetryCooldown(server, clientIndex, LaunchRetryCooldown, reason);
            return;
        }

        TryingGameStart = false;
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

        if (sender.DocumentTitle.Contains("점검"))
        {
            TryingLogout = TryingLogin = TryingGameStart = false;
            CancelPendingGameStart("점검 페이지 진입");
        }

        if (Uri.TryCreate(sender.Source, UriKind.Absolute, out Uri? currentUri))
        {
            string absoluteUri = currentUri.AbsoluteUri;

            if (absoluteUri.Contains("member/convert.gs", StringComparison.OrdinalIgnoreCase))
            {
                NavigateToGersangMain("계정 전환 페이지");
                return;
            }

            if (absoluteUri.Contains("loginCertUp.gs", StringComparison.OrdinalIgnoreCase))
            {
                await ShowPhoneVerificationGuideAsync();
            }
        }

        if (!_initialHomeNavigationCompleted
            && Uri.TryCreate(sender.Source, UriKind.Absolute, out Uri? domainUri)
            && IsGersangDomain(domainUri))
        {
            _initialHomeNavigationCompleted = true;

            if (_pendingNavigationUri is Uri pendingUri)
            {
                _pendingNavigationUri = null;
                _webview.Source = pendingUri;
                return;
            }

            if (!string.IsNullOrWhiteSpace(_pendingHtmlContent))
            {
                string pendingHtmlContent = _pendingHtmlContent;
                _pendingHtmlContent = null;
                _webview.CoreWebView2?.NavigateToString(pendingHtmlContent);
                return;
            }
        }

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

        string message = args.Message ?? string.Empty;
        bool isOtpFailure = IsOtpFailureMessage(message);
        bool isCredentialFailure = IsCredentialFailureMessage(message);
        bool isRetryBlocked = IsRetryBlockedMessage(message);

        if (isOtpFailure)
        {
            args.Accept();
            QueueOtpFailureRecovery(message);
            return;
        }

        if (isCredentialFailure || isRetryBlocked)
        {
            args.Accept();
            CancelLaunchAttemptWithRetryCooldown(message);
            QueueLaunchFailureDialog(message);
            return;
        }

        var deferral = args.GetDeferral();
        try
        {
            await _webview!.DispatcherQueue.RunOrEnqueueAsync(() => HandleScriptDialogAsync(args));
        }
        finally
        {
            deferral.Complete();
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
            NavigateToUri(new Uri(Url_Gersang_Main));
        }
    }

    /// <summary>
    /// 앱 내부 브라우저를 지정한 절대 URL로 이동시키되, 초기 거상 메인 진입 전이면 완료 후 이어서 이동합니다.
    /// </summary>
    internal void Navigate(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (_webview is null)
            return;

        if (!_initialHomeNavigationCompleted)
        {
            _pendingHtmlContent = null;
            _pendingNavigationUri = uri;
            return;
        }

        _pendingHtmlContent = null;
        _pendingNavigationUri = null;
        NavigateToUri(uri);
    }

    /// <summary>
    /// WebView에 정적 HTML 문서를 직접 표시합니다.
    /// </summary>
    internal void NavigateToHtmlDocument(string htmlContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlContent);

        if (_webview is null)
            return;

        if (!_initialHomeNavigationCompleted)
        {
            _pendingNavigationUri = null;
            _pendingHtmlContent = htmlContent;
            return;
        }

        _pendingNavigationUri = null;
        _pendingHtmlContent = null;
        _webview.CoreWebView2?.NavigateToString(htmlContent);
    }

    /// <summary>
    /// WebView2가 초기화된 경우 CoreWebView2.Navigate를 우선 사용해 fragment 이동도 명시적으로 반영합니다.
    /// </summary>
    private void NavigateToUri(Uri uri)
    {
        if (_webview is null)
            return;

        if (_webview.CoreWebView2 is CoreWebView2 core)
        {
            core.Navigate(uri.AbsoluteUri);
            return;
        }

        _webview.Source = uri;
    }
    #endregion WebViewManager Core
}
