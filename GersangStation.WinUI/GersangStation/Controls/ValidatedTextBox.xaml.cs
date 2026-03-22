using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GersangStation.Controls;

public sealed partial class ValidatedTextBox : UserControl
{
    public ValidatedTextBox()
    {
        InitializeComponent();
        UpdateErrorUI();
    }

    // 밖에서 그냥 IdBox.TextChanged += ... 쓰기
    public event TextChangedEventHandler? TextChanged;
    private void InnerTextBox_TextChanged(object sender, TextChangedEventArgs e)
        => TextChanged?.Invoke(this, e);

    // Header (object)
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(object), typeof(ValidatedTextBox),
            new PropertyMetadata(null, OnHeaderChanged));
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    // Header Visibility: Header가 null/empty면 숨김 처리용 (원치 않으면 삭제 가능)
    public Visibility HeaderVisibility { get; private set; } = Visibility.Collapsed;

    private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ValidatedTextBox v) return;

        bool hasHeader = e.NewValue is not null;
        v.HeaderVisibility = hasHeader ? Visibility.Visible : Visibility.Collapsed;
    }

    // InputRowLeftContent / InputRowRightContent
    public static readonly DependencyProperty InputRowLeftContentProperty =
        DependencyProperty.Register(nameof(InputRowLeftContent), typeof(object), typeof(ValidatedTextBox),
            new PropertyMetadata(null));
    public object? InputRowLeftContent
    {
        get => GetValue(InputRowLeftContentProperty);
        set => SetValue(InputRowLeftContentProperty, value);
    }

    public static readonly DependencyProperty InputRowRightContentProperty =
        DependencyProperty.Register(nameof(InputRowRightContent), typeof(object), typeof(ValidatedTextBox),
            new PropertyMetadata(null));
    public object? InputRowRightContent
    {
        get => GetValue(InputRowRightContentProperty);
        set => SetValue(InputRowRightContentProperty, value);
    }

    // Text/Placeholder/Validation
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(ValidatedTextBox),
            new PropertyMetadata(string.Empty));
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(ValidatedTextBox),
            new PropertyMetadata(string.Empty));
    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public static readonly DependencyProperty IsValidProperty =
        DependencyProperty.Register(nameof(IsValid), typeof(bool), typeof(ValidatedTextBox),
            new PropertyMetadata(true, OnValidationChanged));
    public bool IsValid
    {
        get => (bool)GetValue(IsValidProperty);
        set => SetValue(IsValidProperty, value);
    }

    public static readonly DependencyProperty ErrorTextProperty =
        DependencyProperty.Register(nameof(ErrorText), typeof(string), typeof(ValidatedTextBox),
            new PropertyMetadata(string.Empty, OnValidationChanged));
    public string ErrorText
    {
        get => (string)GetValue(ErrorTextProperty);
        set => SetValue(ErrorTextProperty, value);
    }

    private static void OnValidationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ValidatedTextBox v)
            v.UpdateErrorUI();
    }

    private Brush? _normalBorderBrush;
    private Thickness? _normalBorderThickness;
    private void UpdateErrorUI()
    {
        bool show = !IsValid && !string.IsNullOrWhiteSpace(ErrorText);
        ErrorTextBlock.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

        if (_normalBorderBrush is null)
            _normalBorderBrush = InnerTextBox.BorderBrush;

        if (_normalBorderThickness is null)
            _normalBorderThickness = InnerTextBox.BorderThickness;

        if (show)
        {
            InnerTextBox.BorderBrush = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
            InnerTextBox.BorderThickness = new Thickness(1);
        }
        else
        {
            InnerTextBox.BorderBrush = _normalBorderBrush;
            InnerTextBox.BorderThickness = _normalBorderThickness ?? new Thickness(1);
        }
    }

    public static readonly DependencyProperty InputIsEnabledProperty =
    DependencyProperty.Register(nameof(InputIsEnabled), typeof(bool), typeof(ValidatedTextBox),
        new PropertyMetadata(true));
    public bool InputIsEnabled
    {
        get => (bool)GetValue(InputIsEnabledProperty);
        set => SetValue(InputIsEnabledProperty, value);
    }
}
