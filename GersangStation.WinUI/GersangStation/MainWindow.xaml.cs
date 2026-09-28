using Core;
using GersangStation.Controls;
using GersangStation.Diagnostics;
using GersangStation.Main.Setting;
using GersangStation.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Data.Xml.Dom;
using Windows.Services.Store;
using Windows.System;
using Windows.UI.Notifications;
using WinRT.Interop;

namespace GersangStation.Main;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    private enum MainShellSection
    {
        Station,
        Browser,
        Setting
    }

    private enum StoreUpdateTeachingTipAction
    {
        None,
        Install
    }

    public WebViewManager WebViewManager { get; }
    internal BrowserSession BrowserSession { get; }
    public GameStarter GameStarter { get; } = new();
    public ClipMouseService ClipMouseService { get; } = new(AppDataManager.IsMouseConfinementEnabled);
    public WindowSwitchService WindowSwitchService { get; }

    private readonly SystemTrayService _systemTrayService;
    private bool _hasHandledInitialNavigation;
    private bool _allowForceClose;
    private bool _hasShownFirstRunPrompt;
    private bool _isCloseConfirmationPending;
    private bool _isWindowActive = true;
    private bool _hasStartedStartupStoreUpdateCheck;
    private bool _isStartupFlowRunning;
    private bool _skipDefaultInitialNavigation;
    private MainShellSection _activeSection = MainShellSection.Station;
    private StoreContext? _storeContext;
    private IReadOnlyList<StorePackageUpdate> _availableStoreUpdates = [];
    private Task? _storeUpdateAvailabilityTask;
    private bool _hasSimulatedStoreUpdateAvailable;
    private bool _isStoreUpdateDownloadInProgress;
    private bool _isStoreUpdateReady;
    private bool _isStoreUpdateManualInstallAvailable;
    private bool _isStoreUpdateInstallInProgress;
    private StoreUpdateTeachingTipAction _storeUpdateTeachingTipAction;

    public string CurrentAppVersionText { get; } = CreateCurrentVersionText();
    public bool HasAvailableStoreUpdate => _availableStoreUpdates.Count > 0 || _hasSimulatedStoreUpdateAvailable;
    public bool ShouldShowStoreUpdateButton => _isStoreUpdateReady || _isStoreUpdateManualInstallAvailable;
    public bool StoreUpdateButtonEnabled
        => (_isStoreUpdateReady || _isStoreUpdateManualInstallAvailable) && !_isStoreUpdateInstallInProgress;
    public event EventHandler? StoreUpdateStateChanged;

    public MainWindow()
    {
        InitializeComponent();

        BrowserSession = new BrowserSession(BrowserWebView);
        WebViewManager = new WebViewManager(BrowserSession, this, GameStarter);

        Activated += OnActivated;
        Root.Loaded += OnRootLoaded;
        Closed += OnClosed;
        AppWindow.Closing += OnAppWindowClosing;
        AppDataManager.MouseConfinementEnabledChanged += OnMouseConfinementEnabledChanged;
        AppDataManager.WindowSwitchingEnabledChanged += OnWindowSwitchingEnabledChanged;
        WindowSwitchService = new WindowSwitchService(GameStarter, AppDataManager.IsWindowSwitchingEnabled);
        WindowSwitchService.BrowsingStateChanged += OnWindowSwitchBrowsingStateChanged;
        _systemTrayService = new SystemTrayService(
            this,
            () => AppDataManager.MinimizeBehavior == AppDataManager.WindowMinimizeBehavior.HideToSystemTray,
            ShowMinimizedToTrayNotification,
            RestoreFromTray,
            ExitFromTray);

        InitializeShellFrames();
    }

    /// <summary>
    /// 메인 셸에서 사용하는 루트 페이지들을 한 번만 생성해 유지합니다.
    /// </summary>
    private void InitializeShellFrames()
    {
        StationFrame.Navigate(typeof(StationPage), this);
        SettingFrame.Navigate(typeof(SettingPage));
        if (StationFrame.Content is StationPage stationPage)
            Sidebar.SetHomePage(stationPage);
        ShowSection(MainShellSection.Station);
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Activated -= OnActivated;
        Root.Loaded -= OnRootLoaded;
        AppWindow.Closing -= OnAppWindowClosing;
        AppDataManager.MouseConfinementEnabledChanged -= OnMouseConfinementEnabledChanged;
        AppDataManager.WindowSwitchingEnabledChanged -= OnWindowSwitchingEnabledChanged;
        WindowSwitchService.BrowsingStateChanged -= OnWindowSwitchBrowsingStateChanged;
        _systemTrayService.Dispose();
        WindowSwitchService.Dispose();
        ClipMouseService.Dispose();
        (BrowserFrame.Content as WebViewPage)?.Dispose();
        try
        {
            WebViewManager.Dispose();
        }
        finally
        {
            try { BrowserSession.Dispose(); }
            finally { GameStarter.Dispose(); }
        }
    }

    /// <summary>
    /// 저장된 마우스 가두기 설정이 바뀌면 즉시 감시 상태에 반영합니다.
    /// </summary>
    private void OnMouseConfinementEnabledChanged(object? sender, bool isEnabled)
    {
        ClipMouseService.SetEnabled(isEnabled);
    }

    /// <summary>
    /// 저장된 창 전환 설정이 바뀌면 즉시 감시 상태에 반영합니다.
    /// </summary>
    private void OnWindowSwitchingEnabledChanged(object? sender, bool isEnabled)
    {
        WindowSwitchService.SetEnabled(isEnabled);

        if (!isEnabled)
            ClipMouseService.SetExternalSuspended(false);
    }

    /// <summary>
    /// 창 탐색 중에는 마우스 가두기를 잠시 해제해 사용자가 창을 선택할 수 있게 합니다.
    /// </summary>
    private void OnWindowSwitchBrowsingStateChanged(object? sender, bool isBrowsing)
    {
        ClipMouseService.SetExternalSuspended(isBrowsing);
    }

    /// <summary>
    /// 창 활성 상태를 추적하고 WebView 메모리 정책을 갱신합니다.
    /// </summary>
    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        bool wasWindowActive = _isWindowActive;
        _isWindowActive = args.WindowActivationState != WindowActivationState.Deactivated;
        UpdateWebViewMemoryMode();

        if (!wasWindowActive && _isWindowActive)
        {
            SafeExecution.RunHandledAsync(
                RefreshAfterWindowActivationAsync,
                $"{nameof(MainWindow)}.{nameof(OnActivated)}.{nameof(RefreshAfterWindowActivationAsync)}")
                .FireAndForgetHandled($"{nameof(MainWindow)}.{nameof(OnActivated)}");
        }
    }

    /// <summary>
    /// 창이 다시 활성화되면 현재 셸 섹션에서 복귀 갱신이 필요한 콘텐츠를 갱신합니다.
    /// </summary>
    private async Task RefreshAfterWindowActivationAsync()
    {
        WebViewManager?.RefreshAfterWindowActivation();

        if (_activeSection == MainShellSection.Station && StationFrame.Content is StationPage stationPage)
            await stationPage.RefreshEventsAfterWindowActivationAsync();
    }

    /// <summary>
    /// 루트가 시각 트리에 연결되면 시작 시 필요한 안내와 초기 탐색을 순차적으로 처리합니다.
    /// </summary>
    private async void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        if (Root.XamlRoot is null || _isStartupFlowRunning)
            return;

        _isStartupFlowRunning = true;
        try
        {
            WebViewManager.InitializeAsync().FireAndForgetHandled("MainWindow.InitializeBrowserSession");
            if (!await HandleStartupAdministratorPromptAsync())
                return;

            await HandleStartupFirstRunPromptAsync();
            HandleInitialNavigation();
            EnsureStartupStoreUpdateAsync()
                .FireAndForgetHandled($"{nameof(MainWindow)}.{nameof(EnsureStartupStoreUpdateAsync)}");
        }
        finally
        {
            _isStartupFlowRunning = false;
        }
    }

    /// <summary>
    /// 최초 실행 안내를 표시하고 설정 페이지 이동 여부를 처리합니다.
    /// </summary>
    private async Task ShowFirstRunPromptAsync()
    {
        if (_hasShownFirstRunPrompt || Root.XamlRoot is null)
            return;

        _hasShownFirstRunPrompt = true;

        AppDataManager.IsSetupCompleted = true;
        await ShowInitialSettingPromptAsync();
    }

    /// <summary>
    /// 관리자 권한 안내를 시작 플로우 안에서 한 번만 표시합니다.
    /// </summary>
    private async Task<bool> HandleStartupAdministratorPromptAsync()
    {
        if (App.IsRunningAsAdministrator || !AppDataManager.IsStartupAdminPromptEnabled || Root.XamlRoot is null)
            return true;

        ContentDialog dialog = new()
        {
            XamlRoot = Content.XamlRoot,
            Title = "관리자 권한으로 실행하지 않음",
            Content = "현재 앱이 관리자 권한으로 실행되지 않았습니다.\n관리자 권한이 없으면 일부 기능이 동작하지 않을 수 있습니다.\n그래도 계속 하시겠습니까?",
            PrimaryButtonText = "방법 확인 후 종료",
            SecondaryButtonText = "다음부터 안내 무시",
            CloseButtonText = "무시하고 진행",
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowManagedAsync();
        if (result == ContentDialogResult.Secondary)
        {
            _skipDefaultInitialNavigation = true;
            NavigateToSettingPage(SettingSection.Notification);
            return true;
        }

        if (result != ContentDialogResult.Primary)
            return true;

        await Launcher.LaunchUriAsync(App.LinkManager.ResolveNavigation("help.permission.multi-client").Uri);
        Application.Current.Exit();
        return false;
    }

    /// <summary>
    /// 최초 실행 안내를 시작 플로우 안에서 순차적으로 표시합니다.
    /// </summary>
    private async Task HandleStartupFirstRunPromptAsync()
    {
        if (_hasShownFirstRunPrompt || AppDataManager.IsSetupCompleted)
            return;

        await ShowFirstRunPromptAsync();
    }

    /// <summary>
    /// 시작 시 최초 한 번만 Station 또는 릴리즈 노트 화면으로 이동합니다.
    /// </summary>
    private void HandleInitialNavigation()
    {
        if (_hasHandledInitialNavigation)
            return;

        _hasHandledInitialNavigation = true;

        if (_skipDefaultInitialNavigation)
            return;

        string[] versionParts = AppDataManager.PrevVersion.Split('.');
        PackageVersion prevVersion = versionParts.Length == 4
            ? new PackageVersion(
                ushort.Parse(versionParts[0]),
                ushort.Parse(versionParts[1]),
                ushort.Parse(versionParts[2]),
                ushort.Parse(versionParts[3]))
            : new PackageVersion(1, 0, 0, 0);

        PackageVersion currentVersion = Package.Current.Id.Version;
        if (_hasShownFirstRunPrompt)
        {
            // 최초 실행자에게 굳이 업데이트 노트를 보여주지는 않는다.
            AppDataManager.PrevVersion = $"{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}.{currentVersion.Revision}";
            prevVersion = currentVersion;
        }

        if (PackageVersionComparer.IsNewer(currentVersion, prevVersion))
        {
            AppDataManager.PrevVersion = $"{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}.{currentVersion.Revision}";
            NavigateToWebViewPageByLinkKey("help.update.release-note");
            return;
        }

        ShowSection(MainShellSection.Station);
    }

    /// <summary>
    /// 시작 시 Store 업데이트를 확인하고, 가능한 경우 사용자 입력 없이 다운로드를 준비합니다.
    /// </summary>
    private async Task EnsureStartupStoreUpdateAsync()
    {
        if (_hasStartedStartupStoreUpdateCheck || Root.XamlRoot is null)
            return;

        _hasStartedStartupStoreUpdateCheck = true;

        // Loaded 직후 한 프레임 양보해 첫 렌더링과 입력 반응이 Store 확인에 막히지 않게 합니다.
        await Task.Yield();
        await EnsureStoreUpdateAvailabilityLoadedAsync();

        if (HasAvailableStoreUpdate)
        {
            await PrepareStoreUpdateAsync();
            return;
        }

    }

    /// <summary>
    /// 최초 실행 시 필요한 초기 안내를 표시합니다.
    /// </summary>
    private async Task ShowInitialSettingPromptAsync()
    {
        if (Root.XamlRoot is null)
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = "최초 실행 안내",
            Content = "거상스테이션을 처음 실행하셨습니다. 사용자 설명서를 읽으시겠습니까?",
            PrimaryButtonText = "예",
            CloseButtonText = "아니오",
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowManagedAsync();
        if (result == ContentDialogResult.Primary)
        {
            _skipDefaultInitialNavigation = true;
            NavigateToWebViewPageByLinkKey(AppLinkKeys.HelpUserGuide);
        }
    }

    /// <summary>
    /// 창 닫기 시 저장된 기본 동작에 따라 트레이 숨김 또는 종료 확인을 처리합니다.
    /// </summary>
    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowForceClose)
            return;

        args.Cancel = true;

        if (AppDataManager.CloseBehavior == AppDataManager.WindowCloseBehavior.HideToSystemTray)
        {
            _systemTrayService.HideWindowToTray();
            return;
        }

        if (_isCloseConfirmationPending)
            return;

        // AppWindowClosingEventArgs는 deferral을 제공하지 않으므로
        // 일단 종료를 막고 확인 결과에 따라 명시적으로 다시 닫습니다.
        _isCloseConfirmationPending = true;

        try
        {
            bool canClose = GetActiveRootPage() switch
            {
                IConfirmLeave confirm => await confirm.ConfirmLeaveAsync(LeaveReason.AppExit),
                _ when Root.XamlRoot is not null => await ExitConfirmationDialog.ShowAsync(Root.XamlRoot),
                _ => true
            };

            if (!canClose)
                return;

            _allowForceClose = true;
            Close();
        }
        finally
        {
            _isCloseConfirmationPending = false;
        }
    }

    private void Sidebar_HomeRequested(object sender, EventArgs e)
        => ShowSection(MainShellSection.Station)
            .FireAndForgetHandled($"{nameof(MainWindow)}.{nameof(Sidebar_HomeRequested)}");

    private void Sidebar_BrowserRequested(object sender, EventArgs e)
        => NavigateToWebViewPage();

    private async void Sidebar_BrowserLoginRequested(object sender, SidebarBrowserLoginRequestedEventArgs e)
        => await SafeExecution.RunHandledAsync(async () =>
        {
            await ShowSectionAsync(MainShellSection.Browser);
            if (BrowserFrame.Content is WebViewPage browserPage)
                await browserPage.LoginAccountAsync(e.Account);
        }, $"{nameof(MainWindow)}.{nameof(Sidebar_BrowserLoginRequested)}");

    private void Sidebar_SettingRequested(object sender, SidebarSettingRequestedEventArgs e)
        => NavigateToSettingPage(e.Section);

    /// <summary>
    /// 메인 셸에서 기본 설정 페이지를 엽니다.
    /// </summary>
    public void NavigateToSettingPage()
    {
        NavigateToSettingPage(SettingSection.Account);
    }

    /// <summary>
    /// 메인 셸에서 설정 페이지를 열고 대상 섹션 및 초기 페이지 파라미터를 전달합니다.
    /// </summary>
    public void NavigateToSettingPage(SettingSection section, object? pageParameter = null)
    {
        if (SettingFrame.Content is SettingPage settingPage)
            settingPage.NavigateToSection(section, pageParameter);

        ShowSection(MainShellSection.Setting)
            .FireAndForgetHandled($"{nameof(MainWindow)}.{nameof(NavigateToSettingPage)}");
    }

    /// <summary>
    /// 트레이에 숨겨진 창을 다시 표시하거나 일반 최소화 상태를 복원합니다.
    /// </summary>
    public void EnsureWindowVisible()
    {
        _systemTrayService.RestoreWindow();
    }

    /// <summary>
    /// 현재 창을 시스템 트레이로 숨깁니다.
    /// </summary>
    public void HideWindowToTray()
    {
        _systemTrayService.HideWindowToTray();
    }

    /// <summary>
    /// 메인 셸에서 현재 브라우저 상태를 유지한 채 브라우저 페이지로 전환합니다.
    /// </summary>
    public void NavigateToWebViewPage()
    {
        ShowSection(MainShellSection.Browser)
            .FireAndForgetHandled($"{nameof(MainWindow)}.{nameof(NavigateToWebViewPage)}");
    }

    /// <summary>
    /// 메인 셸에서 브라우저 페이지를 열고 지정한 URL로 바로 이동합니다.
    /// </summary>
    public void NavigateToWebViewPage(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? targetUri))
            WebViewManager?.Navigate(targetUri);

        NavigateToWebViewPage();
    }

    /// <summary>
    /// 메타데이터 매니페스트의 링크 key를 해석해 브라우저 페이지로 엽니다.
    /// </summary>
    public void NavigateToWebViewPageByLinkKey(string linkKey)
    {
        if (string.IsNullOrWhiteSpace(linkKey))
            return;

        LinkNavigationTarget target = App.LinkManager.ResolveNavigation(linkKey);
        if (target.Uri is Uri uri)
        {
            NavigateToWebViewPage(uri.AbsoluteUri);
            return;
        }

        if (!string.IsNullOrWhiteSpace(target.HtmlContent))
            NavigateToWebViewPageHtml(target.HtmlContent);
    }

    /// <summary>
    /// 메인 셸에서 브라우저 페이지를 열고 지정한 HTML 문서를 바로 표시합니다.
    /// </summary>
    internal void NavigateToWebViewPageHtml(string htmlContent)
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
            return;

        NavigateToWebViewPage();
        WebViewManager?.NavigateToHtmlDocument(htmlContent);
    }

    /// <summary>
    /// 현재 창 활성 상태와 표시 중인 페이지를 기준으로 WebView 메모리 타깃을 조정합니다.
    /// </summary>
    private void UpdateWebViewMemoryMode()
    {
        if (WebViewManager is null)
            return;

        bool isWebViewVisible = _activeSection == MainShellSection.Browser;
        if (_isWindowActive && isWebViewVisible)
            WebViewManager.SetActiveMemoryMode();
        else
            WebViewManager.SetInactiveMemoryMode();
    }

    /// <summary>
    /// 현재 표시 중인 메인 섹션에 해당하는 루트 페이지를 반환합니다.
    /// </summary>
    private Page? GetActiveRootPage()
        => _activeSection switch
        {
            MainShellSection.Station => StationFrame.Content as Page,
            MainShellSection.Browser => BrowserFrame.Content as Page,
            MainShellSection.Setting => SettingFrame.Content as Page,
            _ => null
        };

    /// <summary>
    /// 지정한 메인 섹션만 보이도록 전환합니다.
    /// </summary>
    private Task ShowSection(MainShellSection section)
        => ShowSectionAsync(section);

    /// <summary>
    /// 지정한 메인 섹션을 활성화하고, 해당 섹션의 표시 수명주기 훅을 실행합니다.
    /// </summary>
    private async Task ShowSectionAsync(MainShellSection section)
    {
        // 브라우저 명령은 창의 세션을 사용하므로 도구 모음 페이지는 실제 진입 시에만 생성합니다.
        if (section == MainShellSection.Browser && BrowserFrame.Content is null)
            BrowserFrame.Navigate(typeof(WebViewPage), this);

        if (_activeSection == section)
        {
            UpdateSidebarMode(section);
            await ActivateSectionAsync(section);
            return;
        }

        DeactivateSection(_activeSection);
        _activeSection = section;
        StationFrame.Visibility = section == MainShellSection.Station ? Visibility.Visible : Visibility.Collapsed;
        BrowserSurface.Visibility = section == MainShellSection.Browser ? Visibility.Visible : Visibility.Collapsed;
        SettingFrame.Visibility = section == MainShellSection.Setting ? Visibility.Visible : Visibility.Collapsed;
        UpdateSidebarMode(section);
        UpdateWebViewMemoryMode();
        await ActivateSectionAsync(section);
    }

    private void UpdateSidebarMode(MainShellSection section)
    {
        if (section == MainShellSection.Setting)
        {
            Sidebar.ShowSettings();
            return;
        }

        Sidebar.ShowControl(browserSelected: section == MainShellSection.Browser);
    }

    /// <summary>
    /// 활성 섹션에 맞는 페이지 재동기화 로직을 실행합니다.
    /// </summary>
    private Task ActivateSectionAsync(MainShellSection section)
    {
        return section switch
        {
            MainShellSection.Station when StationFrame.Content is StationPage stationPage
                => stationPage.OnShellActivatedAsync(this),
            MainShellSection.Browser when BrowserFrame.Content is WebViewPage webViewPage
                => webViewPage.OnShellActivatedAsync(this),
            _ => Task.CompletedTask
        };
    }

    /// <summary>
    /// 비활성화되는 섹션의 정리 로직을 실행합니다.
    /// </summary>
    private void DeactivateSection(MainShellSection section)
    {
        switch (section)
        {
            case MainShellSection.Station when StationFrame.Content is StationPage stationPage:
                stationPage.OnShellDeactivated();
                break;
            case MainShellSection.Browser when BrowserFrame.Content is WebViewPage webViewPage:
                webViewPage.OnShellDeactivated();
                break;
        }
    }

    /// <summary>
    /// StationPage 등에서 현재 Store 업데이트 상태를 즉시 동기화할 수 있도록 이벤트를 발생시킵니다.
    /// </summary>
    internal void NotifyStoreUpdateStateChanged()
        => StoreUpdateStateChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// 현재 창에 연결된 StoreContext를 생성합니다.
    /// </summary>
    private StoreContext CreateStoreContext()
    {
        StoreContext context = StoreContext.GetDefault();
        InitializeWithWindow.Initialize(context, WindowNative.GetWindowHandle(this));
        return context;
    }

    /// <summary>
    /// 앱 패키지의 현재 버전 문자열을 생성합니다.
    /// </summary>
    private static string CreateCurrentVersionText()
    {
        PackageVersion version = Package.Current.Id.Version;
        return $"v{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }

    /// <summary>
    /// Store 업데이트 가능 여부를 한 번만 확인하고 결과를 캐시합니다.
    /// </summary>
    internal async Task EnsureStoreUpdateAvailabilityLoadedAsync()
    {
        _storeUpdateAvailabilityTask ??= LoadStoreUpdateAvailabilityAsync();
        await _storeUpdateAvailabilityTask;
    }

    /// <summary>
    /// Microsoft Store에서 현재 앱의 업데이트 가능 여부를 조회합니다.
    /// </summary>
    private async Task LoadStoreUpdateAvailabilityAsync()
    {
#if DEV
        _availableStoreUpdates = [];
        _hasSimulatedStoreUpdateAvailable = true;
        NotifyStoreUpdateStateChanged();
        await Task.CompletedTask;
        return;
#else
        try
        {
            _storeContext ??= CreateStoreContext();
            _availableStoreUpdates = await _storeContext.GetAppAndOptionalStorePackageUpdatesAsync();
            _hasSimulatedStoreUpdateAvailable = false;
        }
        catch (Exception ex)
        {
            _availableStoreUpdates = [];
            _hasSimulatedStoreUpdateAvailable = false;
            Debug.WriteLine($"[MainWindow] Store update availability check failed.{Environment.NewLine}{ex}");
        }
        finally
        {
            NotifyStoreUpdateStateChanged();
        }
#endif
    }

    /// <summary>
    /// Store 업데이트를 사용자 입력 없이 백그라운드에서 다운로드하고, 준비 완료 시 TeachingTip으로 알립니다.
    /// </summary>
    private async Task PrepareStoreUpdateAsync()
    {
        if (!HasAvailableStoreUpdate || _isStoreUpdateDownloadInProgress || _isStoreUpdateReady || _isStoreUpdateInstallInProgress)
            return;

        _isStoreUpdateDownloadInProgress = true;
        _isStoreUpdateManualInstallAvailable = false;
        NotifyStoreUpdateStateChanged();

        try
        {
#if DEV
            await SimulateStoreUpdateDownloadAsync();
#else
            _storeContext ??= CreateStoreContext();
            if (!_storeContext.CanSilentlyDownloadStorePackageUpdates)
            {
                _isStoreUpdateManualInstallAvailable = true;
                return;
            }

            StorePackageUpdateResult result = await _storeContext
                .TrySilentDownloadStorePackageUpdatesAsync(_availableStoreUpdates)
                .AsTask();

            if (!IsStoreUpdateCompleted(result))
            {
                _isStoreUpdateManualInstallAvailable = true;
                return;
            }
#endif

            _isStoreUpdateManualInstallAvailable = false;
            _isStoreUpdateReady = true;
            NotifyStoreUpdateStateChanged();
            ShowStoreUpdateReadyTeachingTip();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainWindow] Silent Store update download failed.{Environment.NewLine}{ex}");
            _isStoreUpdateManualInstallAvailable = true;
        }
        finally
        {
            _isStoreUpdateDownloadInProgress = false;
            NotifyStoreUpdateStateChanged();
        }
    }

    /// <summary>
    /// 준비된 Store 업데이트를 조용히 설치하고, 설치 요청이 완료되면 앱을 종료합니다.
    /// </summary>
    internal Task InstallStoreUpdateAsync()
        => DispatcherQueue.RunOrEnqueueAsync(InstallStoreUpdateCoreAsync);

    private async Task InstallStoreUpdateCoreAsync()
    {
        if ((!_isStoreUpdateReady && !_isStoreUpdateManualInstallAvailable)
            || _isStoreUpdateInstallInProgress
            || !HasAvailableStoreUpdate)
            return;

        _isStoreUpdateInstallInProgress = true;
        _isStoreUpdateReady = false;
        _isStoreUpdateManualInstallAvailable = false;
        StoreUpdateTeachingTip.IsOpen = false;
        _storeUpdateTeachingTipAction = StoreUpdateTeachingTipAction.None;
        NotifyStoreUpdateStateChanged();

        try
        {
#if DEV
            await Task.Delay(700);
#else
            _storeContext ??= CreateStoreContext();
            StorePackageUpdateResult result = await _storeContext
                .RequestDownloadAndInstallStorePackageUpdatesAsync(_availableStoreUpdates)
                .AsTask();

            if (!IsStoreUpdateCompleted(result))
            {
                _isStoreUpdateManualInstallAvailable = true;
                return;
            }
#endif

            _availableStoreUpdates = [];
            _hasSimulatedStoreUpdateAvailable = false;
            NotifyStoreUpdateStateChanged();
            ForceCloseForInstalledStoreUpdate();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainWindow] Store update installation request failed.{Environment.NewLine}{ex}");
            _isStoreUpdateManualInstallAvailable = true;
        }
        finally
        {
            _isStoreUpdateInstallInProgress = false;
            NotifyStoreUpdateStateChanged();
        }
    }

    private async void StoreUpdateTeachingTip_ActionButtonClick(TeachingTip sender, object args)
    {
        StoreUpdateTeachingTipAction action = _storeUpdateTeachingTipAction;
        _storeUpdateTeachingTipAction = StoreUpdateTeachingTipAction.None;
        sender.IsOpen = false;

        switch (action)
        {
            case StoreUpdateTeachingTipAction.Install:
                await InstallStoreUpdateAsync();
                break;
        }
    }

    private void ShowStoreUpdateReadyTeachingTip()
    {
        ShowStoreUpdateTeachingTip(
            "앱 업데이트 준비 완료",
            "지금 바로 업데이트 하시겠습니까?",
            "업데이트",
            StoreUpdateTeachingTipAction.Install);
    }

    private void ShowStoreUpdateTeachingTip(
        string title,
        string subtitle,
        string actionButtonContent,
        StoreUpdateTeachingTipAction action)
    {
        if (Root.XamlRoot is null)
            return;

        _storeUpdateTeachingTipAction = action;
        StoreUpdateTeachingTip.Title = title;
        StoreUpdateTeachingTip.Subtitle = subtitle;
        StoreUpdateTeachingTip.ActionButtonContent = actionButtonContent;
        StoreUpdateTeachingTip.CloseButtonContent = "나중에";
        StoreUpdateTeachingTip.IsLightDismissEnabled = false;
        StoreUpdateTeachingTip.IsOpen = true;
    }

    private static bool IsStoreUpdateCompleted(StorePackageUpdateResult result)
        => string.Equals(result.OverallState.ToString(), "Completed", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// DEV 구성에서 Store 업데이트 다운로드를 사용자 입력 없이 시뮬레이션합니다.
    /// </summary>
    private static async Task SimulateStoreUpdateDownloadAsync()
    {
        await Task.Delay(350);
        await Task.Delay(350);
        await Task.Delay(350);
    }

    /// <summary>
    /// 업데이트 설치 완료 후 종료 확인을 건너뛰고 앱을 닫습니다.
    /// </summary>
    private void ForceCloseForInstalledStoreUpdate()
    {
        _allowForceClose = true;
        Application.Current.Exit();
    }

    /// <summary>
    /// 트레이 아이콘 더블 클릭 시 숨겨진 창을 복원하고 전면으로 가져옵니다.
    /// </summary>
    private void RestoreFromTray()
    {
        _systemTrayService.RestoreWindow();
        App.BringCurrentWindowToForeground();
    }

    /// <summary>
    /// 트레이 우클릭 메뉴의 종료 명령으로 앱을 즉시 종료합니다.
    /// </summary>
    private void ExitFromTray()
    {
        _allowForceClose = true;
        Application.Current.Exit();
    }

    /// <summary>
    /// 최소화 시 트레이로 이동했음을 Windows 알림으로 안내합니다.
    /// </summary>
    private static void ShowMinimizedToTrayNotification()
    {
        var toastXml = new XmlDocument();
        toastXml.LoadXml(
            """
            <toast>
              <visual>
                <binding template="ToastGeneric">
                  <text>시스템 트레이로 이동하였습니다.</text>
                  <text>창 최소화 시 기본 동작은 설정 - 동작에서 변경하실 수 있습니다.</text>
                </binding>
              </visual>
            </toast>
            """);

        ToastNotifier toastNotifier = ToastNotificationManager.CreateToastNotifier(Package.Current.Id.FamilyName + "!App");
        ToastNotification toastNotification = new(toastXml);
        toastNotifier.Show(toastNotification);
    }
}
