using Core;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GersangStation.Main.Setting;

/// <summary>
/// 창 최소화 등 외형 관련 사용자 기본 동작을 편집합니다.
/// </summary>
public sealed partial class AppearanceSettingPage : Page, INotifyPropertyChanged
{
    private int _minimizeBehaviorIndex = AppDataManager.MinimizeBehavior == AppDataManager.WindowMinimizeBehavior.HideToSystemTray
        ? 0
        : 1;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 창 최소화 기본 동작을 RadioButtons 인덱스로 표현합니다.
    /// </summary>
    public int MinimizeBehaviorIndex
    {
        get => _minimizeBehaviorIndex;
        set
        {
            int normalized = value == 1 ? 1 : 0;
            if (_minimizeBehaviorIndex == normalized)
                return;

            _minimizeBehaviorIndex = normalized;
            AppDataManager.MinimizeBehavior = normalized == 0
                ? AppDataManager.WindowMinimizeBehavior.HideToSystemTray
                : AppDataManager.WindowMinimizeBehavior.MinimizeToTaskbar;
            OnPropertyChanged();
        }
    }

    public AppearanceSettingPage()
    {
        InitializeComponent();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
