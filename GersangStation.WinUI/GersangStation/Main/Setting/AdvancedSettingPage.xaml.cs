using Core;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Linq;

namespace GersangStation.Main.Setting;

public sealed partial class AdvancedSettingPage : Page
{
    private const string ClipMouseHelpUrl = "https://github.com/byungmeo/GersangStation/wiki/Q&A";
    private bool _isInitializing;
    private static readonly ClipMouseEscapeModifierOption[] ClipMouseEscapeModifierOptions =
    [
        new(AppDataManager.ClipMouseHotkeyModifier.Alt, "Alt", "기본값입니다. Alt를 누르는 동안 거상 창 마우스 가두기가 잠시 해제됩니다."),
        new(AppDataManager.ClipMouseHotkeyModifier.Control, "Ctrl", "Ctrl을 누르는 동안 거상 창 마우스 가두기가 잠시 해제됩니다."),
        new(AppDataManager.ClipMouseHotkeyModifier.Shift, "Shift", "Shift를 누르는 동안 거상 창 마우스 가두기가 잠시 해제됩니다.")
    ];

    /// <summary>
    /// 저장된 개발자 도구 활성화 상태를 토글 초기값에 반영합니다.
    /// </summary>
    public AdvancedSettingPage()
    {
        InitializeComponent();
        _isInitializing = true;
        ComboBox_ClipMouseEscapeModifier.ItemsSource = ClipMouseEscapeModifierOptions;
        ToggleSwitch_MouseConfinement.IsOn = App.IsRunningAsAdministrator && AppDataManager.IsMouseConfinementEnabled;
        ToggleSwitch_MouseConfinement.IsEnabled = App.IsRunningAsAdministrator;
        ComboBox_ClipMouseEscapeModifier.SelectedItem = ClipMouseEscapeModifierOptions
            .First(option => option.Value == AppDataManager.ClipMouseEscapeModifier);
        ToggleSwitch_DeveloperTool.IsOn = AppDataManager.IsDeveloperToolEnabled;
        UpdateClipMouseUiState();
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
    /// 탈출 단축키를 바꾸면 저장값과 설명을 즉시 갱신합니다.
    /// </summary>
    private void ComboBox_ClipMouseEscapeModifier_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboBox_ClipMouseEscapeModifier.SelectedItem is not ClipMouseEscapeModifierOption option)
            return;

        TextBlock_ClipMouseEscapeModifierDescription.Text = option.Description;

        if (_isInitializing)
            return;

        AppDataManager.ClipMouseEscapeModifier = option.Value;
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
        ComboBox_ClipMouseEscapeModifier.IsEnabled = isClipMouseOn && isAdmin;

        if (ComboBox_ClipMouseEscapeModifier.SelectedItem is ClipMouseEscapeModifierOption option)
            TextBlock_ClipMouseEscapeModifierDescription.Text = option.Description;
    }

    /// <summary>
    /// 관리자 권한 도움말 링크를 앱 내부 브라우저 페이지에서 엽니다.
    /// </summary>
    private void HyperlinkButton_ClipMouseAdminHelp_Click(object sender, RoutedEventArgs e)
    {
        if (App.CurrentWindow is MainWindow window)
            window.NavigateToWebViewPage(ClipMouseHelpUrl);
    }

    private sealed record ClipMouseEscapeModifierOption(
        AppDataManager.ClipMouseHotkeyModifier Value,
        string Label,
        string Description);
}
