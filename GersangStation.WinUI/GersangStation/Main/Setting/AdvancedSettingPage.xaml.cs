using Core;
using GersangStation.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

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
    /// 관리자 권한 여부와 토글 상태에 따라 단축키 패널 및 경고 표시를 갱신합니다.
    /// </summary>
    private void UpdateClipMouseUiState()
    {
        bool isAdmin = App.IsRunningAsAdministrator;
        bool isClipMouseOn = ToggleSwitch_MouseConfinement.IsOn;

        Panel_ClipMouseAdminWarning.Visibility = isAdmin ? Visibility.Collapsed : Visibility.Visible;
        Border_ClipMouseHotkeyPanel.Visibility = isClipMouseOn ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 관리자 권한 여부와 토글 상태에 따라 창 전환 안내 패널 및 경고 표시를 갱신합니다.
    /// </summary>
    private void UpdateWindowSwitchUiState()
    {
        bool isAdmin = App.IsRunningAsAdministrator;
        bool isWindowSwitchOn = ToggleSwitch_WindowSwitching.IsOn;

        Panel_WindowSwitchAdminWarning.Visibility = isAdmin ? Visibility.Collapsed : Visibility.Visible;
        Border_WindowSwitchHotkeyPanel.Visibility = isWindowSwitchOn ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 관리자 권한 도움말 링크를 앱 내부 브라우저 페이지에서 엽니다.
    /// </summary>
    private void HyperlinkButton_ClipMouseAdminHelp_Click(object sender, RoutedEventArgs e)
    {
        if (App.CurrentWindow is MainWindow window)
            window.NavigateToWebViewPageByLinkKey(AppLinkKeys.HelpAdminClipMouse);
    }
}
