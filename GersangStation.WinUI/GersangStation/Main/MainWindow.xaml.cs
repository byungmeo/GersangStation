using Core;
using GersangStation.Main.Setting;
using GersangStation.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading.Tasks;

namespace GersangStation.Main;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    public WebViewManager? WebViewManager { get; private set; }
    public GameStarter GameStarter { get; } = new();

    private bool _allowForceClose;
    private bool _hasShownFirstRunPrompt;
    private bool _isFirstRunPromptPending;
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
        GameStarter.Dispose();
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
        AppDataManager.IsSetupCompleted = true;

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
    /// 창 닫기 시 현재 페이지의 이탈 확인 로직을 먼저 실행하고, 거부되면 종료를 취소합니다.
    /// </summary>
    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowForceClose)
            return;

        if (ContentFrame.Content is not IConfirmLeave confirm)
            return;

        // AppWindowClosingEventArgs는 deferral을 제공하지 않으므로
        // 일단 종료를 막고 확인 결과에 따라 명시적으로 다시 닫습니다.
        args.Cancel = true;

        bool canLeave = await confirm.ConfirmLeaveAsync();
        if (!canLeave)
            return;

        _allowForceClose = true;
        Close();
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
