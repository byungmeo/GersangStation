using Core;
using GersangStation.Main.Setting;
using GersangStation.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.IO;
using System.Threading.Tasks;

namespace GersangStation.Main;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    public WebViewManager? WebViewManager { get; private set; }
    public GameStarter GameStarter { get; } = new();
    public ClipMouseService ClipMouseService { get; } = new(AppDataManager.IsMouseConfinementEnabled);

    private readonly DesktopShortcutService _desktopShortcutService = new();
    private bool _allowForceClose;
    private bool _hasShownFirstRunPrompt;
    private bool _isFirstRunPromptPending;
    private bool _isCloseConfirmationPending;
    private bool _isWindowActive = true;
    private bool _suppressNavSelectionChanged = false;
    private int _previousSelectedIndex = 0;
    private SelectorBarItem _previousSelectedItem;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        Activated += OnActivated;
        Root.Loaded += OnRootLoaded;
        Closed += OnClosed;
        AppWindow.Closing += OnAppWindowClosing;
        AppDataManager.MouseConfinementEnabledChanged += OnMouseConfinementEnabledChanged;

        // WebViewPage 초기화를 위해 강제로 Navigate 호출
        ContentFrame.Navigate(typeof(WebViewPage), this);
        ContentFrame.Navigate(typeof(StationPage));

        _previousSelectedItem = MainSelectorBar.SelectedItem;
    }

    internal void RegisterWebViewManager(WebViewManager webviewManager)
    {
        WebViewManager = webviewManager;
        UpdateWebViewMemoryMode();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Activated -= OnActivated;
        Root.Loaded -= OnRootLoaded;
        AppDataManager.MouseConfinementEnabledChanged -= OnMouseConfinementEnabledChanged;
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
    /// 최초 활성화 시 한 번만 최초 실행 안내를 표시합니다.
    /// </summary>
    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        _isWindowActive = args.WindowActivationState != WindowActivationState.Deactivated;
        UpdateWebViewMemoryMode();

        if (_hasShownFirstRunPrompt || _isFirstRunPromptPending || args.WindowActivationState == WindowActivationState.Deactivated)
            return;

        if (AppDataManager.IsSetupCompleted)
            return;

        _isFirstRunPromptPending = true;

        if (Root.XamlRoot is null)
            return;

        await ShowFirstRunPromptAsync();
    }

    /// <summary>
    /// 루트가 시각 트리에 연결되면 대기 중인 최초 실행 안내를 표시합니다.
    /// </summary>
    private async void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        if (!_isFirstRunPromptPending || _hasShownFirstRunPrompt || Root.XamlRoot is null)
            return;

        await ShowFirstRunPromptAsync();
    }

    /// <summary>
    /// 최초 실행 안내를 표시하고 설정 페이지 이동 여부를 처리합니다.
    /// </summary>
    private async Task ShowFirstRunPromptAsync()
    {
        if (_hasShownFirstRunPrompt || Root.XamlRoot is null)
            return;

        _hasShownFirstRunPrompt = true;
        _isFirstRunPromptPending = false;

        await ShowDesktopShortcutPromptAsync();
        AppDataManager.IsSetupCompleted = true;
        await ShowInitialSettingPromptAsync();
    }

    /// <summary>
    /// 최초 실행 시 바탕화면 바로가기 생성 여부를 묻고 필요하면 실제로 생성합니다.
    /// </summary>
    private async Task ShowDesktopShortcutPromptAsync()
    {
        if (Root.XamlRoot is null || _desktopShortcutService.DesktopShortcutExists())
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = "바탕화면 바로가기 생성",
            Content = "거상스테이션을 처음 실행하셨습니다. 바탕화면에 바로가기를 생성하시겠습니까?",
            PrimaryButtonText = "생성",
            CloseButtonText = "건너뛰기",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        DesktopShortcutOperationResult createResult = _desktopShortcutService.CreateDesktopShortcut();
        if (!createResult.Success)
            await ShowDesktopShortcutCreationFailedDialogAsync(createResult);
    }

    /// <summary>
    /// 최초 실행 시 필요한 바로가기 생성 및 초기 안내를 순서대로 표시합니다.
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

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
            NavigateToSettingPage();
    }

    /// <summary>
    /// 바로가기 생성 실패를 사용자에게 설명하는 대화 상자를 표시합니다.
    /// </summary>
    private async Task ShowDesktopShortcutCreationFailedDialogAsync(DesktopShortcutOperationResult result)
    {
        if (Root.XamlRoot is null)
            return;

        string message = result.Exception switch
        {
            UnauthorizedAccessException =>
                $"바탕화면 폴더에 쓸 권한이 없어 바로가기를 생성하지 못했습니다.{Environment.NewLine}경로: {result.ShortcutPath}",
            DirectoryNotFoundException =>
                $"바탕화면 폴더를 찾지 못해 바로가기를 생성하지 못했습니다.{Environment.NewLine}경로: {result.ShortcutPath}",
            _ =>
                $"바탕화면 바로가기를 생성하는 중 문제가 발생했습니다.{Environment.NewLine}경로: {result.ShortcutPath}"
        };

        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = "바탕화면 바로가기 생성 실패",
            Content = message,
            CloseButtonText = "확인",
            DefaultButton = ContentDialogButton.Close
        };

        await dialog.ShowAsync();
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
            bool canClose = ContentFrame.Content switch
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

        if (ContentFrame.Content is IConfirmLeave confirm)
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

        SelectorBarItem selectedItem = sender.SelectedItem;
        int currentSelectedIndex = sender.Items.IndexOf(selectedItem);
        System.Type pageType;

        switch (currentSelectedIndex)
        {
            case 0:
                pageType = typeof(StationPage);
                break;
            case 1:
                pageType = typeof(WebViewPage);
                break;
            //case 2:
            //    pageType = typeof(SamplePage3);
            //    break;
            case 3:
                pageType = typeof(Setting.SettingPage);
                break;
            default:
                throw new System.Exception("invalid selectorbar selected index");
        }

        var slideNavigationTransitionEffect = currentSelectedIndex - _previousSelectedIndex > 0 ? SlideNavigationTransitionEffect.FromRight : SlideNavigationTransitionEffect.FromLeft;

        ContentFrame.Navigate(pageType, null, new SlideNavigationTransitionInfo() { Effect = slideNavigationTransitionEffect });

        _previousSelectedIndex = currentSelectedIndex;
        _previousSelectedItem = sender.SelectedItem;
        UpdateWebViewMemoryMode();
    }

    /// <summary>
    /// 메인 셸에서 기본 설정 페이지를 엽니다.
    /// </summary>
    public void NavigateToSettingPage()
    {
        ContentFrame.Navigate(
            typeof(SettingPage),
            null,
            new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromLeft });

        _suppressNavSelectionChanged = true;
        _previousSelectedIndex = MainSelectorBar.Items.IndexOf(MainSelectorBar.SelectedItem);
        _previousSelectedItem = MainSelectorBar.SelectedItem;
        MainSelectorBar.SelectedItem = SelectorBarItem_Setting;
        _suppressNavSelectionChanged = false;
        UpdateWebViewMemoryMode();
    }

    /// <summary>
    /// 메인 셸에서 설정 페이지를 열고 대상 섹션 및 초기 페이지 파라미터를 전달합니다.
    /// </summary>
    public void NavigateToSettingPage(SettingSection section, object? pageParameter = null)
    {
        ContentFrame.Navigate(typeof(SettingPage), 
            new SettingPageNavigationParameter
            {
                Section = section,
                PageParameter = pageParameter
            }, 
            new SlideNavigationTransitionInfo{ Effect = SlideNavigationTransitionEffect.FromLeft });

        _suppressNavSelectionChanged = true;
        _previousSelectedIndex = MainSelectorBar.Items.IndexOf(MainSelectorBar.SelectedItem);
        _previousSelectedItem = MainSelectorBar.SelectedItem;
        MainSelectorBar.SelectedItem = SelectorBarItem_Setting;
        _suppressNavSelectionChanged = false;
        UpdateWebViewMemoryMode();
    }

    /// <summary>
    /// 메인 셸에서 현재 브라우저 상태를 유지한 채 브라우저 페이지로 전환합니다.
    /// </summary>
    public void NavigateToWebViewPage()
    {
        ContentFrame.Navigate(
            typeof(WebViewPage),
            null,
            new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });

        _suppressNavSelectionChanged = true;
        _previousSelectedIndex = MainSelectorBar.Items.IndexOf(MainSelectorBar.SelectedItem);
        _previousSelectedItem = MainSelectorBar.SelectedItem;
        MainSelectorBar.SelectedItem = SelectorBarItem_Browser;
        _suppressNavSelectionChanged = false;
        UpdateWebViewMemoryMode();
    }

    /// <summary>
    /// 메인 셸에서 브라우저 페이지를 열고 지정한 URL로 바로 이동합니다.
    /// </summary>
    public void NavigateToWebViewPage(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        ContentFrame.Navigate(
            typeof(WebViewPage),
            new WebViewPageNavigationParameter(url),
            new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });

        _suppressNavSelectionChanged = true;
        _previousSelectedIndex = MainSelectorBar.Items.IndexOf(MainSelectorBar.SelectedItem);
        _previousSelectedItem = MainSelectorBar.SelectedItem;
        MainSelectorBar.SelectedItem = SelectorBarItem_Browser;
        _suppressNavSelectionChanged = false;
        UpdateWebViewMemoryMode();
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

        bool isWebViewVisible = ContentFrame.Content is WebViewPage;
        if (_isWindowActive && isWebViewVisible)
            WebViewManager.SetActiveMemoryMode();
        else
            WebViewManager.SetInactiveMemoryMode();
    }
}
