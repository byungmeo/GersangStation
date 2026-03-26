using Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GersangStation.Main.Setting;

/// <summary>
/// 알림 관련 사용자 설정을 편집합니다.
/// </summary>
public sealed partial class NotificationSettingPage : Page, INotifyPropertyChanged
{
    private double _eventUrgencyThresholdDays = AppDataManager.EventUrgencyThresholdDays;
    private bool _isInitializing;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Station 페이지에서 마감 임박으로 간주할 남은 일수입니다.
    /// </summary>
    public double EventUrgencyThresholdDays
    {
        get => _eventUrgencyThresholdDays;
        set
        {
            double normalized = Math.Max(0, Math.Round(value));
            if (Math.Abs(_eventUrgencyThresholdDays - normalized) < double.Epsilon)
                return;

            _eventUrgencyThresholdDays = normalized;
            AppDataManager.EventUrgencyThresholdDays = (int)normalized;
            OnPropertyChanged();
        }
    }

    public NotificationSettingPage()
    {
        InitializeComponent();
        _isInitializing = true;
        ToggleSwitch_StartupAdminPrompt.IsOn = AppDataManager.IsStartupAdminPromptEnabled;
        _isInitializing = false;
    }

    /// <summary>
    /// 초기 바인딩이 끝난 뒤 사용자가 토글을 바꾸면 시작 관리자 권한 안내 표시 여부를 저장합니다.
    /// </summary>
    private void ToggleSwitch_StartupAdminPrompt_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
            return;

        AppDataManager.IsStartupAdminPromptEnabled = ToggleSwitch_StartupAdminPrompt.IsOn;
    }

    /// <summary>
    /// NumberBox 내부의 기본 삭제 버튼을 사용하지 않도록 숨깁니다.
    /// </summary>
    private void NumberBox_EventUrgencyThresholdDays_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not NumberBox numberBox)
            return;

        if (FindDescendantByName<Button>(numberBox, "DeleteButton") is not Button deleteButton)
            return;

        deleteButton.Width = 0;
        deleteButton.MinWidth = 0;
        deleteButton.Opacity = 0;
        deleteButton.IsHitTestVisible = false;
        deleteButton.Visibility = Visibility.Collapsed;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static T? FindDescendantByName<T>(DependencyObject root, string name)
        where T : FrameworkElement
    {
        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < childCount; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T element && string.Equals(element.Name, name, StringComparison.Ordinal))
                return element;

            T? nested = FindDescendantByName<T>(child, name);
            if (nested is not null)
                return nested;
        }

        return null;
    }
}
