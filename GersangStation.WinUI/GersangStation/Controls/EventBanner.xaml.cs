using Core;
using Core.Models;
using GersangStation.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace GersangStation.Controls;

/// <summary>Shared event banner chrome and urgency rendering for the home page and settings preview.</summary>
public sealed partial class EventBanner : UserControl
{
    private readonly DispatcherTimer _timer = new();
    private EventHighlightOptions _options = EventHighlightOptions.Default;
    private SolidColorBrush _highlightBrush = new(Colors.IndianRed);
    private readonly SolidColorBrush _transparentBrush = new(Colors.Transparent);
    private bool _initialized;
    private int _phase;

    public UIElement? BannerContent
    {
        get => (UIElement?)GetValue(BannerContentProperty);
        set => SetValue(BannerContentProperty, value);
    }
    public static readonly DependencyProperty BannerContentProperty = DependencyProperty.Register(
        nameof(BannerContent), typeof(UIElement), typeof(EventBanner), new PropertyMetadata(null));

    public string HeaderText { get => (string)GetValue(HeaderTextProperty); set => SetValue(HeaderTextProperty, value); }
    public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register(
        nameof(HeaderText), typeof(string), typeof(EventBanner), new PropertyMetadata(string.Empty));

    public string PeriodText { get => (string)GetValue(PeriodTextProperty); set => SetValue(PeriodTextProperty, value); }
    public static readonly DependencyProperty PeriodTextProperty = DependencyProperty.Register(
        nameof(PeriodText), typeof(string), typeof(EventBanner), new PropertyMetadata(string.Empty));

    public Visibility PeriodVisibility { get => (Visibility)GetValue(PeriodVisibilityProperty); set => SetValue(PeriodVisibilityProperty, value); }
    public static readonly DependencyProperty PeriodVisibilityProperty = DependencyProperty.Register(
        nameof(PeriodVisibility), typeof(Visibility), typeof(EventBanner), new PropertyMetadata(Visibility.Visible));

    public int RemainingDays { get => (int)GetValue(RemainingDaysProperty); set => SetValue(RemainingDaysProperty, value); }
    public static readonly DependencyProperty RemainingDaysProperty = DependencyProperty.Register(
        nameof(RemainingDays), typeof(int), typeof(EventBanner), new PropertyMetadata(-1, OnHighlightInputChanged));

    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(EventBanner), new PropertyMetadata(true, OnHighlightInputChanged));

    public EventBanner()
    {
        InitializeComponent();
        _initialized = true;
        _timer.Tick += OnTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private static void OnHighlightInputChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is EventBanner { _initialized: true } banner) banner.ApplyHighlight();
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
        => await SafeExecution.RunHandledAsync(() =>
        {
            AppDataManager.EventHighlightSettingsChanged -= OnSettingsChanged;
            AppDataManager.EventHighlightSettingsChanged += OnSettingsChanged;
            RefreshSettings();
        }, "EventBanner.Loaded");

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        AppDataManager.EventHighlightSettingsChanged -= OnSettingsChanged;
        _timer.Stop();
        HighlightBorder.BorderBrush = _transparentBrush;
    }

    private void OnSettingsChanged(object? sender, EventArgs args)
        => DispatcherQueue.TryEnqueueHandled(RefreshSettings, "EventBanner.SettingsChanged");

    private void RefreshSettings()
    {
        if (!IsLoaded) return;
        _options = AppDataManager.EventHighlightSettings;
        uint color = _options.ColorArgb;
        _highlightBrush = new SolidColorBrush(Color.FromArgb((byte)(color >> 24), (byte)(color >> 16), (byte)(color >> 8), (byte)color));
        HighlightBorder.BorderThickness = new Thickness(_options.BorderThickness);
        MessageTextBlock.Text = EventHighlightOptions.DefaultMessage;
        ApplyHighlight();
    }

    private void ApplyHighlight()
    {
        _timer.Stop();
        bool urgent = _options.ShouldHighlight(RemainingDays);
        MessageTextBlock.Visibility = urgent && _options.ShowMessage
            ? Visibility.Visible : Visibility.Collapsed;
        if (!IsLoaded || !IsActive || !urgent)
        {
            HighlightBorder.BorderBrush = _transparentBrush;
            return;
        }

        // A static border preserves the information when Windows animations are disabled.
        HighlightBorder.BorderBrush = _highlightBrush;
        if (!new UISettings().AnimationsEnabled) return;
        _phase = 0;
        _timer.Interval = _options.GetPhaseDuration(_phase);
        _timer.Start();
    }

    private void OnTick(object? sender, object args)
        => FatalErrorHandler.Current.RunCallback(AdvanceHighlightPhase, "EventBanner.HighlightTimer");

    private void AdvanceHighlightPhase()
    {
        if (!IsLoaded || !IsActive || !_options.ShouldHighlight(RemainingDays)) { ApplyHighlight(); return; }
        _phase = (_phase + 1) % 4;
        HighlightBorder.BorderBrush = _phase is 0 or 2 ? _highlightBrush : _transparentBrush;
        _timer.Interval = _options.GetPhaseDuration(_phase);
    }
}
