using Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
        ToggleSwitch_MouseConfinement.IsOn = AppDataManager.IsMouseConfinementEnabled;
        ToggleSwitch_DeveloperTool.IsOn = AppDataManager.IsDeveloperToolEnabled;
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
}
