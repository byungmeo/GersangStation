using Core;
using Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GersangStation.Main.Setting;

public enum SettingSection
{
    Account,
    InstallPath,
    GamePatch,
    GameInstall,
    Notification,
    Advanced,
    DeveloperTool,
    ProgramInfo
}

public sealed class GamePatchSettingNavigationParameter
{
    public GameServer Server { get; init; } = GameServer.Korea_Live;
}

public sealed class GameServerSettingNavigationParameter
{
    public GameServer Server { get; init; } = GameServer.Korea_Live;
}

public sealed class SettingPageNavigationParameter
{
    public SettingSection Section { get; init; } = SettingSection.Account;
    public object? PageParameter { get; init; }
}

public sealed partial class SettingPage : Page, IConfirmLeave
{
    private const string HelpPageUrl = "https://github.com/byungmeo/GersangStation/wiki/Q&A";
    private object _previousSelectedItem;
    private bool _suppressNavSelectionChanged = false;

    public static Dictionary<string, Type> PageDictionary { get; } = new Dictionary<string, Type>
    {
        {"GersangStation.Main.Setting.AccountSettingPage", typeof(GersangStation.Main.Setting.AccountSettingPage)},
        {"GersangStation.Main.Setting.InstallPathSettingPage", typeof(GersangStation.Main.Setting.InstallPathSettingPage)},
        {"GersangStation.Main.Setting.GamePatchSettingPage", typeof(GersangStation.Main.Setting.GamePatchSettingPage)},
        {"GersangStation.Main.Setting.GameInstallSettingPage", typeof(GersangStation.Main.Setting.GameInstallSettingPage)},
        {"GersangStation.Main.Setting.NotificationSettingPage", typeof(GersangStation.Main.Setting.NotificationSettingPage)},
        {"GersangStation.Main.Setting.AdvancedSettingPage", typeof(GersangStation.Main.Setting.AdvancedSettingPage)},
        {"GersangStation.Main.Setting.DeveloperToolPage", typeof(GersangStation.Main.Setting.DeveloperToolPage)},
        {"GersangStation.Main.Setting.ProgramInfoPage", typeof(GersangStation.Main.Setting.ProgramInfoPage)},
        // {"GersangStation.Main.Setting.BrowserSettingPage", typeof(GersangStation.Main.Setting.BrowserSettingPage)},
        // {"GersangStation.Main.Setting.AppearanceSettingPage", typeof(GersangStation.Main.Setting.AppearanceSettingPage)},
        // {"GersangStation.Main.Setting.HelpPage", typeof(GersangStation.Main.Setting.HelpPage)},
        // {"GersangStation.Main.Setting.SponsorPage", typeof(GersangStation.Main.Setting.SponsorPage)},
        // {"GersangStation.Main.Setting.ProgramInfoPage", typeof(GersangStation.Main.Setting.ProgramInfoPage)},
    };

    public SettingPage()
    {
        InitializeComponent();

        _previousSelectedItem = SettingNavigationView.SelectedItem;
        ContentFrame.Navigated += ContentFrame_Navigated;
        AppDataManager.DeveloperToolEnabledChanged += OnDeveloperToolEnabledChanged;
        Unloaded += OnUnloaded;
        RefreshDeveloperToolVisibility();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        RefreshDeveloperToolVisibility();

        if (e.Parameter is SettingPageNavigationParameter parameter)
        {
            NavigateToSection(parameter.Section, parameter.PageParameter);
            return;
        }

        if (ContentFrame.Content is null)
            NavigateToSection(SettingSection.Account, pageParameter: null);
        else
            SyncNavigationSelection(ContentFrame.Content.GetType());
    }

    public async Task<bool> ConfirmLeaveAsync()
    {
        if (ContentFrame.Content is IConfirmLeave confirm)
            return await confirm.ConfirmLeaveAsync();

        return true;
    }

    private async void SettingNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if(_suppressNavSelectionChanged)
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

        var selectedItem = (NavigationViewItem)args.SelectedItem;

        if (selectedItem != null)
        {
            if (string.Equals((string?)selectedItem.Tag, "HelpPage", StringComparison.Ordinal))
            {
                NavigateToHelpPage();
                return;
            }

            Type? pageType = ResolvePageType(selectedItem);
            if (pageType is not null)
                ContentFrame.Navigate(pageType);
        }
    }

    /// <summary>
    /// 지정한 설정 섹션에 해당하는 NavigationView 항목과 페이지를 동기화합니다.
    /// </summary>
    private void NavigateToSection(SettingSection section, object? pageParameter)
    {
        NavigationViewItem selectedItem = section switch
        {
            SettingSection.GamePatch => NavigationViewItem_GamePatch,
            SettingSection.GameInstall => NavigationViewItem_GameInstall,
            SettingSection.Notification => NavigationViewItem_Notification,
            SettingSection.Account => NavigationViewItem_Account,
            SettingSection.InstallPath => NavigationViewItem_InstallPath,
            SettingSection.Advanced => NavigationViewItem_Advanced,
            SettingSection.DeveloperTool => NavigationViewItem_Developer,
            SettingSection.ProgramInfo => NavigationViewItem_ProgramInfo,
            _ => throw new ArgumentOutOfRangeException(nameof(section), section, null),
        };

        Type? pageType = ResolvePageType(selectedItem);
        if (pageType is null)
            return;

        _suppressNavSelectionChanged = true;
        SettingNavigationView.SelectedItem = selectedItem;
        _suppressNavSelectionChanged = false;

        ContentFrame.Navigate(pageType, pageParameter);
    }

    /// <summary>
    /// NavigationViewItem의 Tag 값을 실제 설정 페이지 타입으로 변환합니다.
    /// </summary>
    private static Type? ResolvePageType(NavigationViewItem selectedItem)
    {
        string selectedItemTag = (string)selectedItem.Tag;
        string pageName = "GersangStation.Main.Setting." + selectedItemTag;
        PageDictionary.TryGetValue(pageName, out Type? pageType);
        return pageType;
    }

    /// <summary>
    /// 설정의 도움말 항목을 선택하면 앱 내부 브라우저 페이지에서 위키 Q&A를 엽니다.
    /// </summary>
    private void NavigateToHelpPage()
    {
        SyncNavigationSelection(ContentFrame.Content?.GetType());

        if (App.CurrentWindow is MainWindow window)
            window.NavigateToWebViewPage(HelpPageUrl);
    }

    /// <summary>
    /// 설정 컨텐츠 프레임이 실제로 연 페이지 타입에 맞춰 NavigationView 선택 상태를 다시 맞춥니다.
    /// </summary>
    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        SyncNavigationSelection(e.SourcePageType);
    }

    /// <summary>
    /// 현재 표시 중인 설정 페이지 타입에 대응하는 NavigationViewItem을 선택 상태로 반영합니다.
    /// </summary>
    private void SyncNavigationSelection(Type? pageType)
    {
        NavigationViewItem? selectedItem = pageType switch
        {
            Type t when t == typeof(AccountSettingPage) => NavigationViewItem_Account,
            Type t when t == typeof(InstallPathSettingPage) => NavigationViewItem_InstallPath,
            Type t when t == typeof(GamePatchSettingPage) => NavigationViewItem_GamePatch,
            Type t when t == typeof(GameInstallSettingPage) => NavigationViewItem_GameInstall,
            Type t when t == typeof(NotificationSettingPage) => NavigationViewItem_Notification,
            Type t when t == typeof(AdvancedSettingPage) => NavigationViewItem_Advanced,
            Type t when t == typeof(DeveloperToolPage) => NavigationViewItem_Developer,
            Type t when t == typeof(ProgramInfoPage) => NavigationViewItem_ProgramInfo,
            _ => null
        };

        if (selectedItem is null)
            return;

        bool isClientSection = selectedItem == NavigationViewItem_InstallPath
            || selectedItem == NavigationViewItem_GamePatch
            || selectedItem == NavigationViewItem_GameInstall;

        if (isClientSection)
            NavigationViewItem_Client.IsExpanded = true;

        _suppressNavSelectionChanged = true;
        SettingNavigationView.SelectedItem = selectedItem;
        _suppressNavSelectionChanged = false;
        _previousSelectedItem = selectedItem;

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            selectedItem.StartBringIntoView();
            selectedItem.Focus(FocusState.Programmatic);
        });
    }

    /// <summary>
    /// 개발자 도구 설정값에 따라 개발자 도구 메뉴의 표시 여부를 갱신합니다.
    /// </summary>
    private void RefreshDeveloperToolVisibility()
    {
        NavigationViewItem_Developer.Visibility =
            AppDataManager.IsDeveloperToolEnabled
                ? Microsoft.UI.Xaml.Visibility.Visible
                : Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    /// <summary>
    /// 개발자 도구 활성화 설정이 바뀌면 메뉴 표시 상태를 UI 스레드에서 다시 계산합니다.
    /// </summary>
    private void OnDeveloperToolEnabledChanged(object? sender, bool enabled)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            RefreshDeveloperToolVisibility();
            return;
        }

        _ = DispatcherQueue.TryEnqueue(RefreshDeveloperToolVisibility);
    }

    /// <summary>
    /// 페이지 해제 시 정적 설정 이벤트 구독을 정리합니다.
    /// </summary>
    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ContentFrame.Navigated -= ContentFrame_Navigated;
        AppDataManager.DeveloperToolEnabledChanged -= OnDeveloperToolEnabledChanged;
        Unloaded -= OnUnloaded;
    }
}
