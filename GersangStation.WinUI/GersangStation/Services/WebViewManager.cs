#define DEBUGGING

using Core;
using Core.Models;
using GersangStation.Diagnostics;
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

internal enum LoginAttemptStage
{
    None,
    Preparing,
    CredentialsSubmitted
}

public sealed partial class WebViewManager : IDisposable, INotifyPropertyChanged
{
    #region Gersang Homepage Controller
    private static readonly TimeSpan LaunchRetryCooldown = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan WindowActivationRefreshMinimumInterval = TimeSpan.FromSeconds(5);
    private static bool _roughLoginNoticeSuppressedForSession;
    private static bool _roughLoginNoticeShowing;
    private static Task? _roughLoginNoticeTask;
    private GameServer _cachedGameStartServer = GameServer.Korea_Live;
    private string _cachedGameStartId = string.Empty;
    private int _cachedGameStartClientIndex = -1;
    private int _launchAttemptVersion;
    private CancellationTokenSource? _launchSocketCancellation;

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

    private string _loginAttemptTargetId = string.Empty;
    private LoginAttemptStage _loginAttemptStage = LoginAttemptStage.None;
    private Exception? _lastCredentialVaultException;
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
    private const string Url_Gersang_IpBlockedFaq = "https://www.gersang.co.kr/customer/faq_view.gs?str_thread=00&str_sthread=&str_word=IP&cateUid=52&uid=373&page=1";
    private Uri? _pendingNavigationUri;
    private Uri? _pendingPostLoginEventUri;
    private string? _pendingHtmlContent;
    private bool _initialHomeNavigationCompleted;
    private bool _isDisplayingHtmlDocument;
    private DateTimeOffset _lastWindowActivationRefreshAt = DateTimeOffset.MinValue;

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
        _launchAttemptVersion++;
        _launchSocketCancellation?.Cancel();
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
    /// 현재 로그인 시도 대상을 같은 계정으로 이어가는 중인지 확인합니다.
    /// </summary>
    private bool IsCurrentLoginAttemptTarget(string id)
    {
        return !string.IsNullOrWhiteSpace(_loginAttemptTargetId)
            && LoginIdComparer.EqualsForComparison(_loginAttemptTargetId, id);
    }

    /// <summary>
    /// 로그인 시도 상태가 유지 중인지 확인합니다.
    /// </summary>
    private bool HasPendingLoginAttempt()
    {
        return TryingLogin && !string.IsNullOrWhiteSpace(_loginAttemptTargetId);
    }

    /// <summary>
    /// 현재 로그인 시도에서 아이디와 비밀번호 제출까지 완료했는지 확인합니다.
    /// </summary>
    private bool HasSubmittedLoginCredentials()
    {
        return _loginAttemptStage == LoginAttemptStage.CredentialsSubmitted;
    }

    /// <summary>
    /// 새 로그인 시도를 시작하거나, 같은 로그인 시도를 이어갈 준비 상태로 표시합니다.
    /// </summary>
    private void BeginLoginAttempt(string id)
    {
        _loginAttemptTargetId = id;
        _loginAttemptStage = LoginAttemptStage.Preparing;
        TryingLogin = true;
    }

    /// <summary>
    /// 현재 로그인 시도에서 로그인 폼 제출까지 완료되었음을 표시합니다.
    /// </summary>
    private void MarkLoginCredentialsSubmitted()
    {
        if (_loginAttemptStage != LoginAttemptStage.None)
            _loginAttemptStage = LoginAttemptStage.CredentialsSubmitted;
    }

    /// <summary>
    /// 현재 로그인 시도 상태를 정리합니다.
    /// </summary>
    private void ResetLoginAttemptState()
    {
        _loginAttemptTargetId = string.Empty;
        _loginAttemptStage = LoginAttemptStage.None;
        TryingLogin = false;
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

        ContentDialogResult result = await dialog.ShowManagedAsync();
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

        await dialog.ShowManagedAsync();

        if (_currentWindow is MainWindow mainWindow)
            mainWindow.NavigateToWebViewPage();
    }

    /// <summary>
    /// 현재 진행 중인 로그인/로그아웃/게임 실행 시도를 모두 취소하고 시작 슬롯을 원래 상태로 되돌립니다.
    /// </summary>
    public void CancelLaunchAttempt(GameServer server, int clientIndex, string reason = "사용자 취소")
    {
        _launchAttemptVersion++;
        _launchSocketCancellation?.Cancel();
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
        ClearPendingPostLoginEventPage();
        ResetLoginAttemptState();
        TryingLogout = false;
        TryingGameStart = false;
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
        try
        {
            return await TryGameStartCoreAsync(id, clientIndex);
        }
        catch
        {
            CancelPendingGameStart("브라우저 게임 실행 준비 중단");
            ResetLoginAttemptState();
            TryingLogout = false;
            throw;
        }
    }

    private async Task<bool> TryGameStartCoreAsync(string id, int clientIndex)
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

        int attemptVersion = _launchAttemptVersion;
        await InitializeAsync();
        _lifetime.Token.ThrowIfCancellationRequested();
        if (attemptVersion != _launchAttemptVersion)
            return false;

        // 게임 실행 시도는 현재 위치와 관계없이 거상 메인 페이지에서 다시 이어갑니다.
        if (!IsGersangMainPage(_webview.Source) || !_session.IsDocumentReady)
        {
            TryingGameStart = true;
            if (!IsGersangMainPage(_webview.Source))
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
                if (await CredentialVaultGuidanceDialog.TryShowAsync(_currentWindow.Content.XamlRoot, _lastCredentialVaultException))
                    return false;

                _ = App.ExceptionHandler.ShowRecoverableAsync(
                    new InvalidOperationException("윈도우 자격 증명 관리자에서 비밀번호를 읽지 못했습니다.", _lastCredentialVaultException),
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
        await InitializeAsync();
        _lifetime.Token.ThrowIfCancellationRequested();
        _isDisplayingHtmlDocument = false;
        TryingLogout = true;
        _session.Navigate(Url_Gersang_Logout);
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
    /// 계정 전환 후 복원할 수 있는 거상 이벤트 페이지인지 확인합니다.
    /// </summary>
    private static bool IsGersangEventPage(Uri? uri)
    {
        if (!IsGersangDomain(uri))
            return false;

        string path = uri!.AbsolutePath;
        return path.Equals("/event", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/event/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 브라우저 계정 전환 중이면 현재 이벤트 페이지를 로그인 완료 후 복원 대상으로 보관합니다.
    /// </summary>
    private void CapturePendingPostLoginEventPageIfNeeded()
    {
        if (TryingGameStart || _webview?.Source is not Uri source || !IsGersangEventPage(source))
            return;

        _pendingPostLoginEventUri = source;
    }

    /// <summary>
    /// 보관된 이벤트 페이지가 있으면 로그인 완료 후 다시 이동합니다.
    /// </summary>
    private bool TryNavigateToPendingPostLoginEventPage()
    {
        if (_webview is null || _pendingPostLoginEventUri is not Uri pendingUri)
            return false;

        _pendingPostLoginEventUri = null;
        _isDisplayingHtmlDocument = false;
        _session.Navigate(pendingUri.AbsoluteUri);
        return true;
    }

    private void ClearPendingPostLoginEventPage()
    {
        _pendingPostLoginEventUri = null;
    }

    /// <summary>
    /// 로그인 또는 게임 실행 전에 거상 공식 메인 페이지로 이동시킵니다.
    /// </summary>
    private void NavigateToGersangMain(string reason)
    {
        Debug.WriteLine($"{reason} 시도 전에 거상 메인 페이지로 이동합니다. CurrentSource: {_webview.Source}");
        _isDisplayingHtmlDocument = false;
        _session.Navigate(Url_Gersang_Main);
    }

    /// <summary>
    /// 현재 페이지 상태를 고려해 거상 로그인 과정을 진행합니다.
    /// </summary>
    public async Task<TryLoginResult> TryLogin(string id)
    {
        return await TryLoginCoreAsync(id, continueExistingAttempt: false);
    }

    /// <summary>
    /// 현재 보류 중인 로그인 시도를 이어서 진행합니다.
    /// </summary>
    private async Task<TryLoginResult> ContinuePendingLoginAttemptAsync()
    {
        if (!HasPendingLoginAttempt())
            return TryLoginResult.InvalidId;

        return await TryLoginCoreAsync(_loginAttemptTargetId, continueExistingAttempt: true);
    }

    /// <summary>
    /// 새 로그인 시도를 시작하거나, 로그아웃/메인 페이지 이동 뒤 동일 시도를 이어서 진행합니다.
    /// </summary>
    private async Task<TryLoginResult> TryLoginCoreAsync(string id, bool continueExistingAttempt)
    {
        await InitializeAsync();
        _lifetime.Token.ThrowIfCancellationRequested();

        bool isContinuingAttempt = continueExistingAttempt && HasPendingLoginAttempt() && IsCurrentLoginAttemptTarget(id);

        _lastCredentialVaultException = null;
        if (!isContinuingAttempt)
        {
            ResetLoginAttemptState();
            ClearPendingPostLoginEventPage();
        }

        if (string.IsNullOrWhiteSpace(id))
            return TryLoginResult.InvalidId;

        if (LoggedIn)
        {
            if (!string.IsNullOrWhiteSpace(LoggedInMemberId)
                && LoginIdComparer.EqualsForComparison(LoggedInMemberId, id))
            {
                // 로그인 하려는 아이디와 현재 로그인 된 아이디가 같으면 로그인 할 필요 없다
                ResetRoughLoginRecoveryState();
                ResetLoginAttemptState();
                Debug.WriteLine("이미 동일한 계정으로 로그인 되어 있으므로 로그인 과정을 스킵합니다.");
                return TryLoginResult.Success;
            }

            if (!string.IsNullOrWhiteSpace(LoggedInMemberId))
            {
                // 로그아웃 해야 한다
                Debug.WriteLine("다른 계정으로 로그인 되어 있으므로 로그아웃 후 로그인을 시도합니다.");
                CapturePendingPostLoginEventPageIfNeeded();
                BeginLoginAttempt(id);
                await TryLogout();
                return TryLoginResult.Success;
            }

            if (string.Equals(_roughLoginRecoveryTargetId, id, StringComparison.Ordinal)
                && !_roughLoginRecoveryBypassUsed)
            {
                _roughLoginRecoveryBypassUsed = true;
                ResetLoginAttemptState();
                Debug.WriteLine("[WebViewManager] rough 로그인 복구 후에도 memberID를 확인하지 못했습니다. 무한 로그인을 막기 위해 현재 세션으로 진행합니다.");
                TryNavigateToPendingPostLoginEventPage();
                return TryLoginResult.Success;
            }

            Debug.WriteLine("[WebViewManager] 로그인 상태는 확인됐지만 memberID 쿠키를 찾지 못했습니다. 선택된 계정으로 다시 로그인하기 위해 로그아웃합니다.");
            _roughLoginRecoveryTargetId = id;
            _roughLoginRecoveryBypassUsed = false;
            CapturePendingPostLoginEventPageIfNeeded();
            BeginLoginAttempt(id);
            await TryLogout();
            return TryLoginResult.Success;
        }

        PasswordVaultHelper.PasswordVaultReadResult passwordResult = PasswordVaultHelper.TryGetPassword(id);
        if (!passwordResult.Success)
        {
            _lastCredentialVaultException = passwordResult.Exception;
            ResetLoginAttemptState();
            ClearPendingPostLoginEventPage();
            return TryLoginResult.VaultUnavailable;
        }

        string? pw = passwordResult.HasCredential ? passwordResult.Password : null;
        if (string.IsNullOrWhiteSpace(pw))
        {
            ResetLoginAttemptState();
            ClearPendingPostLoginEventPage();
            return TryLoginResult.NotFoundPw;
        }

        if (!isContinuingAttempt)
            CapturePendingPostLoginEventPageIfNeeded();

        if (!isContinuingAttempt)
            BeginLoginAttempt(id);
        else
            TryingLogin = true;

        if (HasSubmittedLoginCredentials())
        {
            Debug.WriteLine("[WebViewManager] 현재 로그인 시도에서는 이미 로그인 폼을 제출했습니다. 자동 재제출은 건너뜁니다.");
            return TryLoginResult.Success;
        }

        // 로그인 시도는 현재 위치와 관계없이 거상 메인 페이지에서 다시 이어갑니다.
        if (!IsGersangMainPage(_webview.Source) || !_session.IsDocumentReady)
        {
            if (!IsGersangMainPage(_webview.Source))
                NavigateToGersangMain("로그인");
            return TryLoginResult.Success;
        }

        ulong navigationId = _session.NavigationId;
        await _session.ExecuteScriptAsync(InputIdScript(id));
        _session.EnsureCurrentDocument(navigationId);
        await _session.ExecuteScriptAsync(InputPwScript(pw));
        _session.EnsureCurrentDocument(navigationId);
        // click 직후 새 문서 이벤트가 도착해도 재제출하지 않도록 먼저 기록합니다.
        MarkLoginCredentialsSubmitted();
        await _session.ExecuteScriptAsync(TryLoginScript);

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
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, _session.LifetimeToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        _launchSocketCancellation = cts;

        Task<string?> receiveTask = ReceiveOnceViaWebSocket1818Async(cts.Token);

        string? payload;
        try
        {
            await _session.ExecuteScriptAsync(SocketStartScript(serverParam));
            Debug.WriteLine("SocketStartScript 실행");
            payload = await receiveTask;
            cts.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            _gameStarter.CancelStart(selectedServer, clientIndex, "브라우저 실행 대기 취소 또는 시간 초과");
            return;
        }
        finally
        {
            if (ReferenceEquals(_launchSocketCancellation, cts))
                _launchSocketCancellation = null;
            cts.Cancel();
            // 스크립트가 실패해도 리스너와 수신 Task를 남기지 않습니다.
            try { await receiveTask; }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                // 본문에서 발생한 원래 예외를 종료 정리 예외로 덮어쓰지 않습니다.
                Debug.WriteLine($"[WebViewManager] 소켓 수신 종료: {ex}");
            }
        }

        Debug.WriteLine("게임 실행 payload 수신 완료");

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

    /// <summary>
    /// 게임 실행 중 로그인 폼을 이미 한 번 제출했는데도 메인 페이지 비로그인 상태로 돌아오면 자동 재시도를 중단합니다.
    /// </summary>
    private void CancelRepeatedGameStartLoginAttempt()
    {
        const string message =
            "로그인 시도 후에도 로그인 완료 상태를 확인하지 못했습니다.\n" +
            "무한 재시도를 막기 위해 게임 실행을 중단했습니다.\n" +
            "브라우저 탭에서 로그인 상태를 확인한 뒤 다시 시도해 주세요.";

        Debug.WriteLine("[WebViewManager] 게임 실행 로그인 재시도 루프를 감지하여 자동 재시도를 중단합니다.");
        CancelLaunchAttemptWithRetryCooldown("로그인 상태 확인 실패");
        QueueLaunchFailureDialog(message);
    }

    private async Task UpdateLoginStateByCookieAsync()
    {
        if (!_session.IsReady || _disposed)
            return;

        var core = _session.GetReadyCore();
        ulong navigationId = _session.NavigationId;

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

        _session.EnsureCurrentDocument(navigationId);

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

        _session.EnsureCurrentDocument(navigationId);

        bool isOnGersangMainPage =
            Uri.TryCreate(_currentSource, UriKind.Absolute, out Uri? currentUri)
            && IsGersangMainPage(currentUri);

        if (LoggedIn)
        {
            bool shouldRestoreEventPage = !TryingGameStart && _pendingPostLoginEventUri is not null;
            ResetLoginAttemptState();

            if (TryingGameStart)
            {
                if (isOnGersangMainPage)
                {
                    await TryGameStart(_cachedGameStartId, _cachedGameStartClientIndex);
                }
                else
                {
                    NavigateToGersangMain("게임 실행");
                }
            }
            else if (shouldRestoreEventPage)
            {
                TryNavigateToPendingPostLoginEventPage();
            }
        }
        else
        {
            if (TryingLogout)
            {
                TryingLogout = false;
                if (HasPendingLoginAttempt())
                    await ContinuePendingLoginAttemptAsync();
            }
            else if (HasPendingLoginAttempt())
            {
                if (HasSubmittedLoginCredentials())
                {
                    if (isOnGersangMainPage)
                    {
                        if (TryingGameStart)
                            CancelRepeatedGameStartLoginAttempt();
                        else
                        {
                            Debug.WriteLine("[WebViewManager] 로그인 폼은 이미 한 번 제출되었습니다. 메인 페이지에서 자동 재시도 없이 로그인 시도를 종료합니다.");
                            ResetLoginAttemptState();
                            ClearPendingPostLoginEventPage();
                        }

                        return;
                    }
                }
                else
                {
                    await ContinuePendingLoginAttemptAsync();
                }
            }
            else if (TryingGameStart
                && isOnGersangMainPage
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
        ulong navigationId = _session.NavigationId;

        foreach (string lookupUri in BuildCookieLookupUris(_currentSource))
        {
            IReadOnlyList<CoreWebView2Cookie> cookies = await core.CookieManager.GetCookiesAsync(lookupUri)
                .AsTask().WaitAsync(_session.LifetimeToken);
            _session.EnsureCurrentDocument(navigationId);
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
            string scriptResult = await _session.ExecuteScriptAsync(DetectAuthenticatedDomStateScript);
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
    private readonly BrowserSession _session;
    private Task? _initialization;
    private bool _disposed;
    private CoreWebView2? _subscribedCore;
    private readonly CancellationTokenSource _lifetime = new();
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

    internal WebViewManager(BrowserSession session, Window window, GameStarter gameStarter)
    {
        _currentWindow = window;
        _session = session;
        _webview = session.View;
        _gameStarter = gameStarter;
        _session.Failed += OnSessionFailed;
    }

    public void Dispose()
    {
        _session.VerifyAccess();
        if (_disposed)
            return;
        _disposed = true;
        _lifetime.Cancel();
        Debug.WriteLine($"[WebViewManager::Dispose]");
        _session.Failed -= OnSessionFailed;
        CancelPendingGameStart("브라우저 종료");
        ResetLoginAttemptState();
        TryingLogout = false;
        UnsubscribeWebViewEvents();
        UnsubscribeCoreEvents();
        // 컨트롤의 Close는 소유자인 MainWindow의 BrowserSession에서 한 번만 수행합니다.
    }

    /// <summary>
    /// 브라우저 페이지가 전면에 있을 때 WebView 메모리 타깃을 일반 수준으로 되돌립니다.
    /// </summary>
    internal void SetActiveMemoryMode()
    {
        _session.SetMemoryTarget(CoreWebView2MemoryUsageTargetLevel.Normal);
    }

    /// <summary>
    /// 브라우저 페이지가 비활성일 때 WebView 메모리 타깃을 낮춰 working set 축소를 유도합니다.
    /// </summary>
    internal void SetInactiveMemoryMode()
    {
        _session.SetMemoryTarget(CoreWebView2MemoryUsageTargetLevel.Low);
    }

    /// <summary>
    /// WebView2 환경을 초기화하고 기본 홈페이지로 이동합니다.
    /// </summary>
    internal Task InitializeAsync()
    {
        _session.VerifyAccess();
        if (_disposed)
            return Task.FromCanceled(_lifetime.Token);
        return (_initialization ??= InitializeCoreAsync()).WaitAsync(_session.LifetimeToken);
    }

    private async Task InitializeCoreAsync()
    {
        await _session.InitializeAsync();
        _lifetime.Token.ThrowIfCancellationRequested();
        SubscribeWebViewEvents();
        SubscribeCoreEvents();
        // 페이지 인스턴스와 무관하게 로그인 세션 동기화를 위한 최초 탐색을 한 번만 시작합니다.
        _session.Navigate(Url_Gersang_Main);
    }

    private void OnSessionFailed(object? sender, EventArgs args)
    {
        CancelPendingGameStart("브라우저 프로세스 종료");
        ResetLoginAttemptState();
        TryingLogout = false;
        IsBusy = false;
        LoggedIn = false;
        LoggedInMemberId = string.Empty;
        CanGoBack = CanGoForward = false;
        _lifetime.Cancel();
        LoggedInChanged?.Invoke(this, EventArgs.Empty);
        Exception failure = _session.Failure ?? new InvalidOperationException("브라우저 세션이 종료되었습니다.");
        // WebView2 콜백 내부에서 모달 UI를 열지 않습니다.
        _webview.DispatcherQueue.TryEnqueueHandled(
            () => App.ExceptionHandler.ShowRecoverableAsync(failure, "BrowserSession.ProcessFailed"),
            "BrowserSession.ProcessFailed.Report");
    }

    private void SubscribeWebViewEvents()
    {
        if (_webview is null) return;

        _webview.NavigationStarting += OnNavigationStarting;
        _webview.NavigationCompleted += OnNavigationCompleted;
        _webview.WebMessageReceived += OnWebMessageReceived;
    }

    private void UnsubscribeWebViewEvents()
    {
        if (_webview is null) return;

        _webview.NavigationStarting -= OnNavigationStarting;
        _webview.NavigationCompleted -= OnNavigationCompleted;
        _webview.WebMessageReceived -= OnWebMessageReceived;
    }

    private void SubscribeCoreEvents()
    {
        var core = _webview?.CoreWebView2;
        if (core is null) return;

        _subscribedCore = core;
        core.SourceChanged += OnSourceChanged;
        core.HistoryChanged += OnHistoryChanged;
        core.DOMContentLoaded += OnDOMContentLoaded;
        core.ScriptDialogOpening += OnScriptDialogOpening;
        core.NotificationReceived += OnNotificationReceived;
    }

    private void UnsubscribeCoreEvents()
    {
        var core = _subscribedCore;
        if (core is null) return;

        _subscribedCore = null;
        core.SourceChanged -= OnSourceChanged;
        core.HistoryChanged -= OnHistoryChanged;
        core.DOMContentLoaded -= OnDOMContentLoaded;
        core.ScriptDialogOpening -= OnScriptDialogOpening;
        core.NotificationReceived -= OnNotificationReceived;
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
        => await SafeExecution.RunHandledAsync(() => UpdateOtpCountdownAsync(sender), "WebViewManager.OtpCountdown");

    private async Task UpdateOtpCountdownAsync(object sender)
    {
        if (sender is not ContentDialog dialog)
            return;

        for (int seconds = 5; seconds >= 1; --seconds)
        {
            dialog.PrimaryButtonText = $"확인({seconds})";
            dialog.IsPrimaryButtonEnabled = false;
            await Task.Delay(1000, _lifetime.Token);
        }

        dialog.PrimaryButtonText = "확인";
        dialog.IsPrimaryButtonEnabled = true;
    }

    private async Task ShowOtpDialogAsync(bool reEnter = false)
    {
        ulong navigationId = _session.NavigationId;
        _session.EnsureCurrentDocument(navigationId);
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
            SecondaryButtonText = "직접 입력",
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
            
        var result = await dlg.ShowManagedAsync();
        dlg.Loaded -= ContentDialog_Loaded;
        _session.EnsureCurrentDocument(navigationId);
        if (result == ContentDialogResult.Primary)
        {
            await _session.ExecuteScriptAsync(InputOtpScript(inputTextBox.Text));
            _session.EnsureCurrentDocument(navigationId);
            await _session.ExecuteScriptAsync(SubmitOtpScript);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            ResetLoginAttemptState();
            ClearPendingPostLoginEventPage();
            TryingLogout = false;
            if (TryingGameStart || (_cachedGameStartClientIndex >= 0 && _cachedGameStartClientIndex < 3))
                CancelPendingGameStart("OTP 직접 입력");

            if (_currentWindow is MainWindow mainWindow)
                mainWindow.NavigateToWebViewPage();
        }
        else
        {
            ResetLoginAttemptState();
            ClearPendingPostLoginEventPage();
            TryingLogout = false;
            if (TryingGameStart || (_cachedGameStartClientIndex >= 0 && _cachedGameStartClientIndex < 3))
                CancelPendingGameStart("OTP 입력 취소");

            _isDisplayingHtmlDocument = false;
            _session.Navigate(Url_Gersang_Main);
        }
    }

    private static bool IsCredentialFailureMessage(string message)
        => !string.IsNullOrWhiteSpace(message)
            && (message.Contains("아이디 또는 비밀번호 오류", StringComparison.OrdinalIgnoreCase)
                || message.Contains("아이디 또는 패스워드 오류", StringComparison.OrdinalIgnoreCase)
                || message.Contains("아이디 혹은 비밀번호 오류", StringComparison.OrdinalIgnoreCase)
                || message.Contains("아이디 혹은 패스워드 오류", StringComparison.OrdinalIgnoreCase));

    private static bool IsPasswordChangeRequiredMessage(string message)
        => !string.IsNullOrWhiteSpace(message)
            && message.Contains("연속", StringComparison.OrdinalIgnoreCase)
            && message.Contains("5회", StringComparison.OrdinalIgnoreCase)
            && message.Contains("오류", StringComparison.OrdinalIgnoreCase);

    private static bool IsIpBlockedMessage(string message)
        => !string.IsNullOrWhiteSpace(message)
            && message.Contains("IP", StringComparison.OrdinalIgnoreCase)
            && message.Contains("차단", StringComparison.OrdinalIgnoreCase)
            && message.Contains("고객센터", StringComparison.OrdinalIgnoreCase);

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
        _lifetime.Token.ThrowIfCancellationRequested();
        var dlg = new ContentDialog
        {
            XamlRoot = _currentWindow.Content.XamlRoot,
            Title = "OTP 인증 실패",
            Content = message,
            PrimaryButtonText = "확인",
            DefaultButton = ContentDialogButton.Primary
        };

        await dlg.ShowManagedAsync();

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
    /// IP 로그인 차단 안내가 표시되면 시도를 중단하고 FAQ 페이지를 브라우저 탭으로 엽니다.
    /// </summary>
    private void QueueIpBlockedFaqNavigation()
    {
        _ = _webview.DispatcherQueue.TryEnqueue(() =>
        {
            if (_currentWindow is MainWindow mainWindow)
            {
                mainWindow.NavigateToWebViewPage(Url_Gersang_IpBlockedFaq);
                return;
            }

            Navigate(new Uri(Url_Gersang_IpBlockedFaq));
        });
    }

    /// <summary>
    /// 로그인 실패/재시도 제한 알림을 표시하고 필요 시 계정 설정 페이지로 이동합니다.
    /// </summary>
    private async Task ShowLaunchFailureDialogAsync(string message)
    {
        _lifetime.Token.ThrowIfCancellationRequested();
        var dlg = new ContentDialog
        {
            XamlRoot = _currentWindow.Content.XamlRoot,
            Title = "게임 실행 취소",
            Content = message,
            PrimaryButtonText = "확인",
            SecondaryButtonText = "계정 다시 설정",
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dlg.ShowManagedAsync();
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
        ResetLoginAttemptState();
        ClearPendingPostLoginEventPage();

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

                    await dlg.ShowManagedAsync();
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

                    var result = await dlg.ShowManagedAsync();
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

                    var result = await dlg.ShowManagedAsync();
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
        => await SafeExecution.RunHandledAsync(() => HandleDocumentLoadedAsync(sender, args), "WebViewManager.DOMContentLoaded");

    private async Task HandleDocumentLoadedAsync(CoreWebView2 sender, CoreWebView2DOMContentLoadedEventArgs args)
    {
        if (_disposed || !_session.IsCurrentDocument(args.NavigationId))
            return;
#if DEBUGGING
        Debug.WriteLine($"[WebView::OnDOMContentLoaded]");
        Debug.WriteLine($"\t- NavigationId: {args.NavigationId}");
        Debug.WriteLine($"\t- CoreWebView2.Source: {sender.Source}");
        Debug.WriteLine($"\t- CoreWebView2.DocumentTitle: {sender.DocumentTitle}");
        Debug.WriteLine($"\t- CoreWebView2.StatusBarText: {sender.StatusBarText}");
#endif
        await UpdateLoginStateByCookieAsync();
        _session.EnsureCurrentDocument(args.NavigationId);
        if (TryingLogin && _currentSource.Contains(Url_Gersang_Otp))
        {
            await _webview!.DispatcherQueue.RunOrEnqueueAsync(() => ShowOtpDialogAsync(false));
        }

        _session.EnsureCurrentDocument(args.NavigationId);

        if (sender.DocumentTitle.Contains("점검"))
        {
            TryingLogout = false;
            ResetLoginAttemptState();
            ClearPendingPostLoginEventPage();
            TryingGameStart = false;
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

        _session.EnsureCurrentDocument(args.NavigationId);

        if (!_initialHomeNavigationCompleted
            && Uri.TryCreate(sender.Source, UriKind.Absolute, out Uri? domainUri)
            && IsGersangDomain(domainUri))
        {
            _initialHomeNavigationCompleted = true;

            if (DispatchPendingNavigation())
                return;
        }

        IsBusy = false;
    }

    private async void OnScriptDialogOpening(CoreWebView2 sender, CoreWebView2ScriptDialogOpeningEventArgs args)
        => await SafeExecution.RunHandledAsync(() => HandleScriptDialogEventAsync(sender, args), "WebViewManager.ScriptDialogOpening");

    private async Task HandleScriptDialogEventAsync(CoreWebView2 sender, CoreWebView2ScriptDialogOpeningEventArgs args)
    {
        if (_disposed || !_session.IsReady)
            return;
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
        bool isPasswordChangeRequired = IsPasswordChangeRequiredMessage(message);
        bool isIpBlocked = IsIpBlockedMessage(message);
        bool isRetryBlocked = IsRetryBlockedMessage(message);

        if (isOtpFailure)
        {
            args.Accept();
            QueueOtpFailureRecovery(message);
            return;
        }

        if (isIpBlocked)
        {
            args.Accept();
            CancelLaunchAttemptWithRetryCooldown(message);
            QueueIpBlockedFaqNavigation();
            return;
        }

        if (isCredentialFailure || isPasswordChangeRequired || isRetryBlocked)
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
        if (_disposed || !_session.IsCurrentDocument(args.NavigationId) || args.IsSuccess)
            return;

        // DOMContentLoaded가 발생하지 않는 네트워크 오류에도 busy/실행 슬롯을 해제합니다.
        CancelPendingGameStart($"브라우저 탐색 실패: {args.WebErrorStatus}");
        ResetLoginAttemptState();
        TryingLogout = false;
        IsBusy = false;
        if (!_initialHomeNavigationCompleted)
        {
            // 공식 홈페이지 접속 실패가 도움말/로컬 HTML 표시까지 막지 않게 합니다.
            _initialHomeNavigationCompleted = true;
            DispatchPendingNavigation();
        }
    }

    internal void GoBack()
    {
        if (!_disposed && _session.IsReady && CanGoBack)
            _session.GetReadyCore().GoBack();
    }

    internal void GoForward()
    {
        if (!_disposed && _session.IsReady && CanGoForward)
            _session.GetReadyCore().GoForward();
    }

    internal void Refresh()
    {
        RefreshAsync().FireAndForgetHandled("WebViewManager.Refresh");
    }

    private async Task RefreshAsync()
    {
        await InitializeAsync();
        _lifetime.Token.ThrowIfCancellationRequested();
        _session.Reload();
    }

    /// <summary>
    /// 창 포커스가 돌아왔을 때 오래된 WebView 세션을 복구하기 위해 현재 페이지를 조건부로 새로고침합니다.
    /// </summary>
    internal void RefreshAfterWindowActivation()
    {
        if (TryingLogin || TryingGameStart || TryingLogout || IsBusy || _isDisplayingHtmlDocument)
            return;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now - _lastWindowActivationRefreshAt < WindowActivationRefreshMinimumInterval)
            return;

        if (TryReload())
            _lastWindowActivationRefreshAt = now;
    }

    /// <summary>
    /// CoreWebView2 초기화가 완료된 경우에만 현재 문서를 다시 불러옵니다.
    /// 창 활성화는 비동기 초기화보다 먼저 발생할 수 있으므로, 준비 전에는 호출을 건너뜁니다.
    /// </summary>
    private bool TryReload()
    {
        if (_disposed || !_session.IsReady || !_session.IsDocumentReady
            || _initialization?.IsCompletedSuccessfully != true)
            return false;

        _session.Reload();
        return true;
    }

    internal void GoHome()
    {
        Navigate(new Uri(Url_Gersang_Main));
    }

    /// <summary>
    /// 앱 내부 브라우저를 지정한 절대 URL로 이동시키되, 초기 거상 메인 진입 전이면 완료 후 이어서 이동합니다.
    /// </summary>
    internal void Navigate(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (_disposed)
            return;

        _pendingHtmlContent = null;
        _pendingNavigationUri = uri;
        DispatchWhenInitializedAsync().FireAndForgetHandled("WebViewManager.Navigate");
    }

    /// <summary>
    /// WebView에 정적 HTML 문서를 직접 표시합니다.
    /// </summary>
    internal void NavigateToHtmlDocument(string htmlContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlContent);

        if (_disposed)
            return;

        _pendingNavigationUri = null;
        _pendingHtmlContent = htmlContent;
        DispatchWhenInitializedAsync().FireAndForgetHandled("WebViewManager.NavigateToHtmlDocument");
    }

    private async Task DispatchWhenInitializedAsync()
    {
        await InitializeAsync();
        _lifetime.Token.ThrowIfCancellationRequested();
        DispatchPendingNavigation();
    }

    /// <summary>초기화 중 들어온 화면 이동은 가장 최근 요청 한 개만 실행합니다.</summary>
    private bool DispatchPendingNavigation()
    {
        if (_disposed || !_session.IsReady || !_initialHomeNavigationCompleted)
            return false;
        if (_pendingNavigationUri is Uri uri)
        {
            _pendingNavigationUri = null;
            _isDisplayingHtmlDocument = false;
            _session.Navigate(uri.AbsoluteUri);
            return true;
        }
        if (_pendingHtmlContent is string html)
        {
            _pendingHtmlContent = null;
            _isDisplayingHtmlDocument = true;
            _session.NavigateToHtml(html);
            return true;
        }
        return false;
    }
    #endregion WebViewManager Core
}
