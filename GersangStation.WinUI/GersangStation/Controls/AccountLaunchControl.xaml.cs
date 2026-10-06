using GersangStation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace GersangStation.Controls;

/// <summary>계정 선택과 실행 UI만 소유합니다. 로그인/실행 처리는 요청 이벤트로 전달합니다.</summary>
public sealed partial class AccountLaunchControl : UserControl
{
    public AccountLaunchControl() => InitializeComponent();

    public event EventHandler? BrowserRequested;
    public event EventHandler? ExecuteRequested;

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(AccountLaunchControl), new PropertyMetadata(string.Empty));
    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly DependencyProperty ClientIndexProperty =
        DependencyProperty.Register(nameof(ClientIndex), typeof(int), typeof(AccountLaunchControl), new PropertyMetadata(0));
    public int ClientIndex
    {
        get => (int)GetValue(ClientIndexProperty);
        set => SetValue(ClientIndexProperty, value);
    }

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(object), typeof(AccountLaunchControl), new PropertyMetadata(null));
    public object? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DependencyProperty SelectedAccountIdProperty =
        DependencyProperty.Register(nameof(SelectedAccountId), typeof(string), typeof(AccountLaunchControl), new PropertyMetadata(string.Empty));
    public string? SelectedAccountId
    {
        get => (string?)GetValue(SelectedAccountIdProperty);
        set => SetValue(SelectedAccountIdProperty, value);
    }

    public static readonly DependencyProperty CanBrowseProperty =
        DependencyProperty.Register(nameof(CanBrowse), typeof(bool), typeof(AccountLaunchControl), new PropertyMetadata(false));
    public bool CanBrowse
    {
        get => (bool)GetValue(CanBrowseProperty);
        set => SetValue(CanBrowseProperty, value);
    }

    public static readonly DependencyProperty LaunchStateProperty =
        DependencyProperty.Register(nameof(LaunchState), typeof(GameLaunchButtonState), typeof(AccountLaunchControl), new PropertyMetadata(null));
    public GameLaunchButtonState? LaunchState
    {
        get => (GameLaunchButtonState?)GetValue(LaunchStateProperty);
        set => SetValue(LaunchStateProperty, value);
    }

    public bool CanBrowseAccount(bool canBrowse, string? accountId)
        => canBrowse && !string.IsNullOrWhiteSpace(accountId);

    public string AutomationIdFor(int clientIndex, string controlName)
        => $"Client{clientIndex + 1}{controlName}";

    private void BrowserButton_Click(object sender, RoutedEventArgs e)
    {
        if (CanBrowseAccount(CanBrowse, SelectedAccountId))
            BrowserRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ExecuteButton_Click(object sender, RoutedEventArgs e)
    {
        // 처리 중에는 같은 요청 이벤트가 기존 실행 취소 경로로 연결됩니다.
        if (LaunchState?.IsEnabled == true)
            ExecuteRequested?.Invoke(this, EventArgs.Empty);
    }
}
