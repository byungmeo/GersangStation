using Core;
using GersangStation.Diagnostics;
using GersangStation.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using Windows.System;

namespace GersangStation.Main.Setting;

public sealed partial class AdvancedSettingPage : Page
{
    private bool _isInitializing;

    /// <summary>
    /// 저장된 개발자 도구 활성화 상태를 토글 초기값에 반영합니다.
    /// </summary>
    public AdvancedSettingPage()
    {
        InitializeComponent();
        _isInitializing = true;
        ToggleSwitch_MouseConfinement.IsOn = App.IsRunningAsAdministrator && AppDataManager.IsMouseConfinementEnabled;
        ToggleSwitch_MouseConfinement.IsEnabled = App.IsRunningAsAdministrator;
        ToggleSwitch_WindowSwitching.IsOn = App.IsRunningAsAdministrator && AppDataManager.IsWindowSwitchingEnabled;
        ToggleSwitch_WindowSwitching.IsEnabled = App.IsRunningAsAdministrator;
        ToggleSwitch_DeveloperTool.IsOn = AppDataManager.IsDeveloperToolEnabled;
        UpdateClipMouseUiState();
        UpdateWindowSwitchUiState();
        _isInitializing = false;
    }

    /// <summary>
    /// 초기 바인딩이 끝난 뒤 사용자가 토글을 바꾸면 마우스 가두기 활성화 상태를 저장합니다.
    /// </summary>
    private void ToggleSwitch_MouseConfinement_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
            return;

        AppDataManager.IsMouseConfinementEnabled = ToggleSwitch_MouseConfinement.IsOn;
        UpdateClipMouseUiState();
    }

    /// <summary>
    /// 초기 바인딩이 끝난 뒤 사용자가 토글을 바꾸면 창 전환 활성화 상태를 저장합니다.
    /// </summary>
    private void ToggleSwitch_WindowSwitching_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
            return;

        AppDataManager.IsWindowSwitchingEnabled = ToggleSwitch_WindowSwitching.IsOn;
        UpdateWindowSwitchUiState();
    }

    /// <summary>
    /// 초기 바인딩이 끝난 뒤 사용자가 토글을 바꾸면 개발자 도구 활성화 상태를 저장합니다.
    /// </summary>
    private void ToggleSwitch_DeveloperTool_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
            return;

        AppDataManager.IsDeveloperToolEnabled = ToggleSwitch_DeveloperTool.IsOn;
    }

    /// <summary>
    /// 관리자 권한 경고를 갱신하고 토글 상태에 맞춰 종속 항목을 펼치거나 접습니다.
    /// </summary>
    private void UpdateClipMouseUiState()
    {
        bool isAdmin = App.IsRunningAsAdministrator;
        bool isClipMouseOn = ToggleSwitch_MouseConfinement.IsOn;

        Panel_ClipMouseAdminWarning.Visibility = isAdmin ? Visibility.Collapsed : Visibility.Visible;
        SettingsExpander_MouseConfinement.IsExpanded = isClipMouseOn;
    }

    /// <summary>
    /// 관리자 권한 경고를 갱신하고 토글 상태에 맞춰 창 전환 종속 항목을 펼치거나 접습니다.
    /// </summary>
    private void UpdateWindowSwitchUiState()
    {
        bool isAdmin = App.IsRunningAsAdministrator;
        bool isWindowSwitchOn = ToggleSwitch_WindowSwitching.IsOn;

        Panel_WindowSwitchAdminWarning.Visibility = isAdmin ? Visibility.Collapsed : Visibility.Visible;
        SettingsExpander_WindowSwitching.IsExpanded = isWindowSwitchOn;
    }

    /// <summary>
    /// 관리자 권한 도움말 링크를 기본 외부 브라우저에서 엽니다.
    /// </summary>
    private async void HyperlinkButton_ClipMouseAdminHelp_Click(object sender, RoutedEventArgs e)
    {
        await SafeExecution.RunHandledAsync(async () =>
        {
            if (!App.LinkManager.TryGetUrl(AppLinkKeys.HelpAdminClipMouse, out string url))
                throw new InvalidOperationException("관리자 권한 도움말 주소를 불러올 수 없습니다.");

            if (!await Launcher.LaunchUriAsync(new Uri(url)))
                throw new InvalidOperationException("기본 외부 브라우저에서 관리자 권한 도움말을 열지 못했습니다.");
        }, "AdvancedSettingPage.OpenAdminHelpInExternalBrowser");
    }
}
