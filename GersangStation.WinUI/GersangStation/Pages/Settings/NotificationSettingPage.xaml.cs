using Core;
using Core.Models;
using GersangStation.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI;

namespace GersangStation.Main.Setting;

/// <summary>
/// 알림 관련 사용자 설정을 편집합니다.
/// </summary>
public sealed partial class NotificationSettingPage : Page, INotifyPropertyChanged
{
    private bool _synchronizingColorPicker;
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool EventUrgencyEnabled
    {
        get => AppDataManager.EventUrgencyEnabled;
        set
        {
            if (value == EventUrgencyEnabled) return;
            SaveSetting(() => AppDataManager.EventUrgencyEnabled = value);
        }
    }

    /// <summary>
    /// Station 페이지에서 마감 임박으로 간주할 남은 일수입니다.
    /// </summary>
    public double EventUrgencyThresholdDays
    {
        get => AppDataManager.EventUrgencyThresholdDays;
        set
        {
            if (!double.IsFinite(value)) { OnPropertyChanged(); return; }
            int normalized = (int)Math.Clamp(Math.Round(value), 0, int.MaxValue);
            if (normalized == AppDataManager.EventUrgencyThresholdDays) return;
            SaveSetting(() => AppDataManager.EventUrgencyThresholdDays = normalized);
        }
    }

    public Color HighlightColor
    {
        get
        {
            uint color = AppDataManager.EventUrgencyColorArgb;
            return Color.FromArgb(255, (byte)(color >> 16), (byte)(color >> 8), (byte)color);
        }
        set
        {
            uint color = 0xFF000000u | ((uint)value.R << 16) | ((uint)value.G << 8) | value.B;
            if (color == AppDataManager.EventUrgencyColorArgb) return;
            SaveSetting(() => AppDataManager.EventUrgencyColorArgb = color);
        }
    }
    public SolidColorBrush HighlightColorBrush => new(HighlightColor);
    public string HighlightColorHex => $"#{HighlightColor.R:X2}{HighlightColor.G:X2}{HighlightColor.B:X2}";

    public double EventUrgencyBorderThickness
    {
        get => AppDataManager.EventUrgencyBorderThickness;
        set
        {
            if (!double.IsFinite(value)) { OnPropertyChanged(); return; }
            double normalized = EventHighlightOptions.NormalizeThickness(value);
            if (normalized == EventUrgencyBorderThickness) return;
            SaveSetting(() => AppDataManager.EventUrgencyBorderThickness = normalized);
        }
    }

    public int RepeatSpeedIndex
    {
        get => (int)AppDataManager.EventUrgencyRepeatSpeed;
        set
        {
            if (value is < 0 or > 2) return;
            if (value == RepeatSpeedIndex) return;
            SaveSetting(() => AppDataManager.EventUrgencyRepeatSpeed = (EventHighlightRepeatSpeed)value);
        }
    }

    public bool EventUrgencyMessageEnabled
    {
        get => AppDataManager.EventUrgencyMessageEnabled;
        set
        {
            if (value == EventUrgencyMessageEnabled) return;
            SaveSetting(() => AppDataManager.EventUrgencyMessageEnabled = value);
        }
    }
    public int PreviewRemainingDays => AppDataManager.EventUrgencyThresholdDays;
    public string PreviewPeriodText => $"이벤트 종료까지 D-{PreviewRemainingDays}";

    private void SaveSetting(Action save, [CallerMemberName] string? propertyName = null)
        => SafeExecution.RunHandledAsync(() =>
        {
            try { save(); }
            finally
            {
                OnPropertyChanged(propertyName);
                OnPropertyChanged(nameof(HighlightColorBrush));
                OnPropertyChanged(nameof(HighlightColorHex));
                OnPropertyChanged(nameof(PreviewRemainingDays));
                OnPropertyChanged(nameof(PreviewPeriodText));
            }
        }, $"NotificationSettingPage.{propertyName}").FireAndForgetHandled($"NotificationSettingPage.{propertyName}");

    public NotificationSettingPage()
    {
        InitializeComponent();
    }

    private void HighlightColorPicker_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ColorPicker picker)
            SafeExecution.RunHandledAsync(() => SynchronizeColorPicker(picker), "NotificationSettingPage.ColorPickerLoaded")
                .FireAndForgetHandled("NotificationSettingPage.ColorPickerLoaded");
    }

    private void HighlightColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        => SafeExecution.RunHandledAsync(() =>
        {
            // Initial template updates are not user choices. In particular, do not write a
            // PropertyChanged value back into ColorPicker.Color while it is changing Color.
            if (_synchronizingColorPicker || !sender.IsLoaded) return;
            HighlightColor = args.NewColor;
            if (!HighlightColor.Equals(Color.FromArgb(255, args.NewColor.R, args.NewColor.G, args.NewColor.B)))
            {
                // A failed save keeps the old value; restore the picker after its event unwinds.
                DispatcherQueue.TryEnqueueHandled(() =>
                {
                    if (sender.IsLoaded) SynchronizeColorPicker(sender);
                }, "NotificationSettingPage.RestoreColorPicker");
            }
        }, "NotificationSettingPage.ColorPickerChanged").FireAndForgetHandled("NotificationSettingPage.ColorPickerChanged");

    private void SynchronizeColorPicker(ColorPicker picker)
    {
        _synchronizingColorPicker = true;
        try
        {
            Color saved = HighlightColor;
            if (!picker.Color.Equals(saved)) picker.Color = saved;
        }
        finally { _synchronizingColorPicker = false; }
    }

    /// <summary>
    /// NumberBox 내부의 기본 삭제 버튼을 사용하지 않도록 숨깁니다.
    /// </summary>
    private void NumberBox_NoClear_Loaded(object sender, RoutedEventArgs e)
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
