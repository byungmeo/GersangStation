using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace GersangStation.Setup;

public sealed partial class AccountSettingPage : Page, ISetupStepPage, INotifyPropertyChanged
{
    public sealed class AccountEntry
    {
        public string Id { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public ObservableCollection<AccountEntry> Accounts { get; } = [];

    public bool CanGoNext => Accounts.Any(IsCompletedAccount);

    public bool CanSkip => true;

    public string NextHintText =>
        CanGoNext
            ? "✅ 1개 이상의 계정이 입력됐어요. 다음으로 진행할 수 있어요."
            : "아이디+비밀번호 계정을 1개 이상 추가하면 다음 버튼이 활성화돼요.";

    public event EventHandler? StateChanged;

    public AccountSettingPage()
    {
        InitializeComponent();
        AddAccount();
        RecomputeCommon();
    }

    public bool OnNext() => true;

    public void OnSkip() { }

    private void OnAddAccount(object sender, RoutedEventArgs e)
    {
        AddAccount();
    }

    private void OnRemoveAccount(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        if (element.DataContext is not AccountEntry target) return;

        Accounts.Remove(target);
        RecomputeCommon();
    }

    private void OnAccountIdChanged(object sender, TextChangedEventArgs e)
    {
        RecomputeCommon();
    }

    private void OnAccountPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox) return;
        if (passwordBox.DataContext is not AccountEntry account) return;

        account.Password = passwordBox.Password;
        RecomputeCommon();
    }

    private void AddAccount()
    {
        Accounts.Add(new AccountEntry());
        RecomputeCommon();
    }

    private void RecomputeCommon()
    {
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanSkip));
        OnPropertyChanged(nameof(NextHintText));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsCompletedAccount(AccountEntry account)
    {
        if (account is null) return false;

        return !string.IsNullOrWhiteSpace(account.Id) &&
               !string.IsNullOrWhiteSpace(account.Password);
    }
}
