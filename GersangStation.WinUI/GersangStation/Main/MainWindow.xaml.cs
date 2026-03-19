using Core;
using GersangStation.Diagnostics;
using GersangStation.Main.Setting;
using GersangStation.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
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

    private sealed class StoreUpdateDialogProgressView
    {
        public required TextBlock MessageTextBlock { get; init; }
        public required ProgressBar ProgressBar { get; init; }
        public required TextBlock StatusTextBlock { get; init; }
        public required TextBlock ProgressTextBlock { get; init; }
    }

    public WebViewManager? WebViewManager { get; private set; }
    public GameStarter GameStarter { get; } = new();
    public ClipMouseService ClipMouseService { get; } = new(AppDataManager.IsMouseConfinementEnabled);
    public WindowSwitchService WindowSwitchService { get; }

    private readonly SystemTrayService _systemTrayService;
    private bool _hasHandledInitialNavigation;
    private bool _allowForceClose;
    private bool _hasShownFirstRunPrompt;
    private bool _isCloseConfirmationPending;
    private bool _isWindowActive = true;
    private bool _isStoreUpdateDialogOpen;
    private bool _hasShownStartupStoreUpdateDialog;
    private bool _isStartupFlowRunning;
    private bool _skipDefaultInitialNavigation;
    private MainShellSection _activeSection = MainShellSection.Station;
    private bool _suppressNavSelectionChanged = false;
    private SelectorBarItem _previousSelectedItem;
    private StoreContext? _storeContext;
    private IReadOnlyList<StorePackageUpdate> _availableStoreUpdates = [];
    private Task? _storeUpdateAvailabilityTask;

    public string CurrentAppVersionText { get; } = CreateCurrentVersionText();
    public bool HasAvailableStoreUpdate => _availableStoreUpdates.Count > 0;
    public bool StoreUpdateButtonEnabled => !_isStoreUpdateDialogOpen && HasAvailableStoreUpdate;
    public event EventHandler? StoreUpdateStateChanged;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
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
        _previousSelectedItem = MainSelectorBar.SelectedItem;
    }

    internal void RegisterWebViewManager(WebViewManager webviewManager)
    {
        WebViewManager = webviewManager;
        UpdateWebViewMemoryMode();
    }

    /// <summary>
    /// 메인 셸에서 사용하는 루트 페이지들을 한 번만 생성해 유지합니다.
    /// </summary>
    private void InitializeShellFrames()
    {
        StationFrame.Navigate(typeof(StationPage), this);
        BrowserFrame.Navigate(typeof(WebViewPage), this);
        SettingFrame.Navigate(typeof(SettingPage));
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
        GameStarter.Dispose();
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
    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        _isWindowActive = args.WindowActivationState != WindowActivationState.Deactivated;
        UpdateWebViewMemoryMode();

        if (args.WindowActivationState == WindowActivationState.Deactivated)
            return;
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
            if (!await HandleStartupAdministratorPromptAsync())
                return;

            await HandleStartupFirstRunPromptAsync();
            HandleInitialNavigation();

            await EnsureStartupStoreUpdateDialogAsync();
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
        if (App.IsRunningAsAdministrator || Root.XamlRoot is null)
            return true;

        ContentDialog dialog = new()
        {
            XamlRoot = Content.XamlRoot,
            Title = "관리자 권한으로 실행하지 않음",
            Content = "현재 앱이 관리자 권한으로 실행되지 않았습니다.\n관리자 권한이 없으면 일부 기능이 동작하지 않을 수 있습니다.\n그래도 계속 하시겠습니까?",
            PrimaryButtonText = "해결 방법 확인한 뒤 종료",
            CloseButtonText = "네, 계속 하겠습니다.",
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowManagedAsync();
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
        SyncShellSelection(SelectorBarItem_Browser, isSelected: false);
    }

    /// <summary>
    /// 현재 Store 업데이트 상태를 보장하고, 시작 시 한 번만 설치 여부를 묻는 대화 상자를 표시합니다.
    /// </summary>
    private async Task EnsureStartupStoreUpdateDialogAsync()
    {
        if (_hasShownStartupStoreUpdateDialog || Root.XamlRoot is null)
            return;

        _hasShownStartupStoreUpdateDialog = true;

#if DEV
        return;
#else
        await EnsureStoreUpdateAvailabilityLoadedAsync();
        if (HasAvailableStoreUpdate)
            await ShowStoreUpdateDialogAsync();
#endif
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
            Content = "거상스테이션을 처음 실행하셨습니다. 설정 페이지로 이동하시겠습니까?",
            PrimaryButtonText = "예",
            CloseButtonText = "아니오",
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowManagedAsync();
        if (result == ContentDialogResult.Primary)
        {
            _skipDefaultInitialNavigation = true;
            NavigateToSettingPage();
        }
    }

    /// <summary>
    /// 창 닫기 시 현재 페이지의 종료 확인 로직을 한 번만 실행하고, 승인되면 명시적으로 창을 닫습니다.
    /// </summary>
    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowForceClose)
            return;

        // AppWindowClosingEventArgs는 deferral을 제공하지 않으므로
        // 일단 종료를 막고 확인 결과에 따라 명시적으로 다시 닫습니다.
        args.Cancel = true;

        if (_isCloseConfirmationPending)
            return;

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

    private async void MainSelectorBar_SelectionChanged(Microsoft.UI.Xaml.Controls.SelectorBar sender, Microsoft.UI.Xaml.Controls.SelectorBarSelectionChangedEventArgs args)
    {
        if (_suppressNavSelectionChanged)
            return;

        SelectorBarItem selectedItem = sender.SelectedItem;
        MainShellSection targetSection = GetSectionFromSelectorItem(selectedItem);
        if (targetSection == _activeSection)
            return;

        if (GetActiveRootPage() is IConfirmLeave confirm)
        {
            bool canLeave = await confirm.ConfirmLeaveAsync();
            if (!canLeave)
            {
                _suppressNavSelectionChanged = true;
                sender.SelectedItem = _previousSelectedItem;
                _suppressNavSelectionChanged = false;
                return;
            }
        }

        await ShowSectionAsync(targetSection);
        _previousSelectedItem = sender.SelectedItem;
    }

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
        SyncShellSelection(SelectorBarItem_Setting, isSelected: true);
    }

    /// <summary>
    /// 트레이에 숨겨진 창을 다시 표시하거나 일반 최소화 상태를 복원합니다.
    /// </summary>
    public void EnsureWindowVisible()
    {
        _systemTrayService.RestoreWindow();
    }

    /// <summary>
    /// 메인 셸에서 현재 브라우저 상태를 유지한 채 브라우저 페이지로 전환합니다.
    /// </summary>
    public void NavigateToWebViewPage()
    {
        ShowSection(MainShellSection.Browser)
            .FireAndForgetHandled($"{nameof(MainWindow)}.{nameof(NavigateToWebViewPage)}");
        SyncShellSelection(SelectorBarItem_Browser, isSelected: true);
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
        if (_activeSection == section)
        {
            await ActivateSectionAsync(section);
            return;
        }

        DeactivateSection(_activeSection);
        _activeSection = section;
        StationFrame.Visibility = section == MainShellSection.Station ? Visibility.Visible : Visibility.Collapsed;
        BrowserFrame.Visibility = section == MainShellSection.Browser ? Visibility.Visible : Visibility.Collapsed;
        SettingFrame.Visibility = section == MainShellSection.Setting ? Visibility.Visible : Visibility.Collapsed;
        UpdateWebViewMemoryMode();
        await ActivateSectionAsync(section);
    }

    /// <summary>
    /// 선택된 메인 탭 상태를 실제 섹션과 맞춥니다.
    /// </summary>
    private void SyncShellSelection(SelectorBarItem item, bool isSelected)
    {
        _suppressNavSelectionChanged = true;
        MainSelectorBar.SelectedItem = isSelected ? item : MainSelectorBar.Items[0];
        _suppressNavSelectionChanged = false;
        _previousSelectedItem = MainSelectorBar.SelectedItem;
    }

    /// <summary>
    /// SelectorBar 항목을 메인 셸 섹션으로 변환합니다.
    /// </summary>
    private static MainShellSection GetSectionFromSelectorItem(SelectorBarItem item)
    {
        return item.Text switch
        {
            "메인" => MainShellSection.Station,
            "브라우저" => MainShellSection.Browser,
            "설정" => MainShellSection.Setting,
            _ => throw new InvalidOperationException($"Unknown main shell selector item: {item.Text}")
        };
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
        return $"현재 버전: v{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
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
        try
        {
            _storeContext ??= CreateStoreContext();
            _availableStoreUpdates = await _storeContext.GetAppAndOptionalStorePackageUpdatesAsync();
        }
        catch (Exception ex)
        {
            _availableStoreUpdates = [];
            await App.ExceptionHandler.ShowRecoverableAsync(ex, "MainWindow.LoadStoreUpdateAvailabilityAsync");
        }
        finally
        {
            NotifyStoreUpdateStateChanged();
        }
    }

    /// <summary>
    /// 시작 대화 상자나 StationPage 버튼에서 공용 Store 업데이트 설치 대화 상자를 엽니다.
    /// </summary>
    public async Task ShowStoreUpdateDialogAsync()
    {
        if (_isStoreUpdateDialogOpen || Root.XamlRoot is null)
            return;

        await EnsureStoreUpdateAvailabilityLoadedAsync();
        if (!HasAvailableStoreUpdate)
            return;

        _isStoreUpdateDialogOpen = true;
        NotifyStoreUpdateStateChanged();

        bool shouldExitAfterConfirmation = false;
        bool isWaitingForCompletionConfirmation = false;
        bool allowClose = true;
        StoreUpdateDialogProgressView progressView = CreateStoreUpdateDialogProgressView();

        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = "업데이트 설치",
            Content = CreateStoreUpdateDialogContent(progressView),
            PrimaryButtonText = "설치",
            CloseButtonText = "나중에",
            DefaultButton = ContentDialogButton.Primary
        };

        dialog.PrimaryButtonClick += async (sender, args) =>
        {
            if (isWaitingForCompletionConfirmation)
            {
                shouldExitAfterConfirmation = true;
                return;
            }

            ContentDialogButtonClickDeferral deferral = args.GetDeferral();
            args.Cancel = true;

            try
            {
                allowClose = false;
                ConfigureStoreUpdateDialogForInstall(dialog, progressView);

                StorePackageUpdateResult result = await InstallStoreUpdatesAsync(progress =>
                {
                    _ = DispatcherQueue.TryEnqueue(() => UpdateStoreUpdateDialogProgress(progressView, progress));
                });

                if (string.Equals(result.OverallState.ToString(), "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    _availableStoreUpdates = [];
                    NotifyStoreUpdateStateChanged();

                    isWaitingForCompletionConfirmation = true;
                    allowClose = true;
                    ConfigureStoreUpdateDialogForCompletion(dialog, progressView);
                    return;
                }

                allowClose = true;
                ConfigureStoreUpdateDialogForRetry(
                    dialog,
                    progressView,
                    string.Equals(result.OverallState.ToString(), "Canceled", StringComparison.OrdinalIgnoreCase)
                        ? "업데이트 설치가 취소되었습니다. 다시 시도하거나 나중에 설치할 수 있습니다."
                        : $"업데이트 설치를 완료하지 못했습니다. 상태: {result.OverallState}");
            }
            catch (Exception ex)
            {
                allowClose = true;
                ConfigureStoreUpdateDialogForRetry(
                    dialog,
                    progressView,
                    "업데이트를 설치하는 중 문제가 발생했습니다. 다시 시도하거나 나중에 설치해 주세요.");

                await App.ExceptionHandler.ShowRecoverableAsync(ex, "MainWindow.ShowStoreUpdateDialogAsync.Install");
            }
            finally
            {
                deferral.Complete();
            }
        };

        dialog.Closing += (sender, args) =>
        {
            if (!allowClose)
                args.Cancel = true;
        };

        try
        {
            ContentDialogResult result = await dialog.ShowManagedAsync();
            if (shouldExitAfterConfirmation && result == ContentDialogResult.Primary)
                ForceCloseForInstalledStoreUpdate();
        }
        finally
        {
            _isStoreUpdateDialogOpen = false;
            NotifyStoreUpdateStateChanged();
        }
    }

    /// <summary>
    /// Store 업데이트 다운로드와 설치를 요청하고 진행 상황을 콜백으로 전달합니다.
    /// </summary>
    private async Task<StorePackageUpdateResult> InstallStoreUpdatesAsync(Action<StorePackageUpdateStatus> progressCallback)
    {
        ArgumentNullException.ThrowIfNull(progressCallback);

        _storeContext ??= CreateStoreContext();
        var progress = new Progress<StorePackageUpdateStatus>(progressCallback);
        return await _storeContext
            .RequestDownloadAndInstallStorePackageUpdatesAsync(_availableStoreUpdates)
            .AsTask(progress);
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
    /// Store 업데이트 설치 대화 상자용 시각 요소를 생성합니다.
    /// </summary>
    private static StackPanel CreateStoreUpdateDialogContent(StoreUpdateDialogProgressView progressView)
    {
        var stackPanel = new StackPanel
        {
            Spacing = 12
        };

        stackPanel.Children.Add(progressView.MessageTextBlock);
        stackPanel.Children.Add(progressView.ProgressBar);
        stackPanel.Children.Add(progressView.StatusTextBlock);
        stackPanel.Children.Add(progressView.ProgressTextBlock);
        return stackPanel;
    }

    /// <summary>
    /// Store 업데이트 설치 대화 상자에서 재사용할 진행 표시 요소를 생성합니다.
    /// </summary>
    private static StoreUpdateDialogProgressView CreateStoreUpdateDialogProgressView()
    {
        return new StoreUpdateDialogProgressView
        {
            MessageTextBlock = new TextBlock
            {
                Text = "새 업데이트가 있습니다. 지금 설치하시겠습니까?",
                TextWrapping = TextWrapping.WrapWholeWords
            },
            ProgressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Height = 8,
                Visibility = Visibility.Collapsed
            },
            StatusTextBlock = new TextBlock
            {
                Visibility = Visibility.Collapsed
            },
            ProgressTextBlock = new TextBlock
            {
                Visibility = Visibility.Collapsed
            }
        };
    }

    /// <summary>
    /// Dev 구성에서 Store 업데이트 설치 과정을 진행률과 함께 가볍게 시뮬레이션합니다.
    /// </summary>
    private static async Task SimulateStoreUpdateInstallAsync(StoreUpdateDialogProgressView progressView)
    {
        progressView.ProgressBar.IsIndeterminate = false;
        progressView.StatusTextBlock.Visibility = Visibility.Visible;
        progressView.ProgressTextBlock.Visibility = Visibility.Visible;

        (double Percent, string Status)[] steps =
        [
            (15, "다운로드 준비 중"),
            (45, "다운로드 중"),
            (75, "설치 중"),
            (100, "설치 완료")
        ];

        foreach ((double percent, string status) in steps)
        {
            progressView.ProgressBar.Value = percent;
            progressView.StatusTextBlock.Text = $"상태: {status}";
            progressView.ProgressTextBlock.Text = $"진행률: {percent:0}%";
            await Task.Delay(350);
        }
    }

    /// <summary>
    /// 설치 시작 직후 대화 상자 상태를 진행 모드로 전환합니다.
    /// </summary>
    private static void ConfigureStoreUpdateDialogForInstall(ContentDialog dialog, StoreUpdateDialogProgressView progressView)
    {
        dialog.IsPrimaryButtonEnabled = false;
        dialog.CloseButtonText = string.Empty;
        progressView.MessageTextBlock.Text = "업데이트를 설치하고 있습니다. 스토어 확인 창이 나타나면 설치를 허용해 주세요.";
        progressView.ProgressBar.Visibility = Visibility.Visible;
        progressView.ProgressBar.IsIndeterminate = true;
        progressView.ProgressBar.Value = 0;
        progressView.StatusTextBlock.Visibility = Visibility.Visible;
        progressView.StatusTextBlock.Text = "상태: 다운로드 준비 중";
        progressView.ProgressTextBlock.Visibility = Visibility.Visible;
        progressView.ProgressTextBlock.Text = "진행률: 확인 중";
    }

    /// <summary>
    /// 설치 완료 시 대화 상자 상태를 종료 안내 모드로 전환합니다.
    /// </summary>
    private static void ConfigureStoreUpdateDialogForCompletion(ContentDialog dialog, StoreUpdateDialogProgressView progressView)
    {
        dialog.PrimaryButtonText = "확인";
        dialog.IsPrimaryButtonEnabled = true;
        dialog.CloseButtonText = string.Empty;
        progressView.MessageTextBlock.Text = "업데이트가 완료되었습니다. 앱을 다시 실행해주세요.";
        progressView.StatusTextBlock.Visibility = Visibility.Visible;
        progressView.StatusTextBlock.Text = "상태: 설치 완료";
        progressView.ProgressBar.IsIndeterminate = false;
        progressView.ProgressBar.Value = 100;
        progressView.ProgressTextBlock.Text = "진행률: 100%";
    }

    /// <summary>
    /// 설치 실패 또는 취소 시 대화 상자 상태를 재시도 모드로 전환합니다.
    /// </summary>
    private static void ConfigureStoreUpdateDialogForRetry(
        ContentDialog dialog,
        StoreUpdateDialogProgressView progressView,
        string message)
    {
        dialog.PrimaryButtonText = "다시 시도";
        dialog.IsPrimaryButtonEnabled = true;
        dialog.CloseButtonText = "닫기";
        progressView.MessageTextBlock.Text = message;
        progressView.StatusTextBlock.Visibility = Visibility.Visible;
        progressView.StatusTextBlock.Text = "상태: 설치 중단";
        progressView.ProgressBar.IsIndeterminate = false;
    }

    /// <summary>
    /// Store에서 전달한 진행률을 대화 상자 UI에 반영합니다.
    /// </summary>
    private static void UpdateStoreUpdateDialogProgress(StoreUpdateDialogProgressView progressView, StorePackageUpdateStatus progress)
    {
        double percent = Math.Clamp(progress.PackageDownloadProgress * 100d, 0d, 100d);
        progressView.ProgressBar.IsIndeterminate = false;
        progressView.ProgressBar.Value = percent;
        progressView.StatusTextBlock.Text = $"상태: {GetStoreUpdateStatusText(progress.PackageUpdateState.ToString(), percent)}";
        progressView.ProgressTextBlock.Text = $"진행률: {percent:0}%";
    }

    /// <summary>
    /// Store 진행 상태를 사용자가 이해하기 쉬운 한국어 문구로 변환합니다.
    /// </summary>
    private static string GetStoreUpdateStatusText(string state, double percent)
    {
        return state switch
        {
            "Pending" when percent <= 0 => "다운로드 준비 중",
            "Pending" => "다운로드 대기 중",
            "Downloading" => "다운로드 중",
            "Deploying" => "설치 중",
            "Completed" => "설치 완료",
            "Canceled" => "설치 취소됨",
            "OtherError" => "설치 오류",
            "ErrorLowBattery" => "배터리 부족으로 대기 중",
            "ErrorWiFiRecommended" => "Wi-Fi 권장 대기 중",
            "ErrorWiFiRequired" => "Wi-Fi 필요",
            "ErrorWiFiDownload" => "Wi-Fi 다운로드 대기 중",
            _ => state
        };
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
                  <text>창 최소화 시 기본 동작은 설정 - 모양에서 변경하실 수 있습니다.</text>
                </binding>
              </visual>
            </toast>
            """);

        ToastNotificationManager.CreateToastNotifier()
            .Show(new ToastNotification(toastXml));
    }
}
