using Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace GersangStation.Setup;

public sealed partial class AccountSettingPage : Page, ISetupStepPage, IAsyncSetupStepPage, INotifyPropertyChanged
{
    private enum UiState { Edit, Saving }

    public sealed class AccountEntry
    {
        public string Id { get; set; } = "";
        public string Password { get; set; } = "";
        public string Nickname { get; set; } = "";
        public bool IsImported { get; set; }
        public bool CanEdit => !IsImported;
    }

    private UiState _uiState = UiState.Edit;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public ObservableCollection<AccountEntry> Accounts { get; } = [];

    public bool IsSaving => _uiState == UiState.Saving;
    public bool IsEdit => _uiState == UiState.Edit;

    public bool CanGoNext => !IsSaving && GetValidationErrorMessage() is null;

    public bool CanSkip => !IsSaving;

    public string NextHintText
    {
        get
        {
            if (IsSaving)
                return "계정 저장 중이에요. 잠시만 기다려주세요.";

            string? errorMessage = GetValidationErrorMessage();
            if (!string.IsNullOrWhiteSpace(errorMessage))
                return errorMessage;

            return "✅ 계정 입력이 끝났어요. 다음으로 진행할 수 있어요.";
        }
    }

    public event EventHandler? StateChanged;

    public AccountSettingPage()
    {
        InitializeComponent();
        LoadSavedAccounts();
        RecomputeCommon();
    }

    public bool OnNext()
    {
        // SetupWindow에서 비동기 경로를 사용하지 않는 경우를 위한 fallback 입니다.
        return OnNextAsync().GetAwaiter().GetResult();
    }

    public async Task<bool> OnNextAsync()
    {
        // 방어 코드: 버튼 상태와 무관하게 최종 저장 시점에도 다시 검증합니다.
        if (GetValidationErrorMessage() is not null)
            return false;

        List<AccountEntry> newCompletedAccounts = Accounts
            .Where(a => !a.IsImported && IsCompletedAccount(a))
            .ToList();

        var sw = Stopwatch.StartNew();

        SetUiState(UiState.Saving);

        try
        {
            await Task.Run(() => SaveAccounts(newCompletedAccounts));

            int remain = 1000 - (int)sw.ElapsedMilliseconds;
            if (remain > 0)
                await Task.Delay(remain);

            return true;
        }
        catch
        {
            SetUiState(UiState.Edit);
            return false;
        }
    }

    public void OnSkip() { }

    private void OnAddAccount(object sender, RoutedEventArgs e)
    {
        AddAccount();
        RecomputeCommon();
    }

    private void OnRemoveAccount(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        if (element.DataContext is not AccountEntry target) return;

        // 불러온 계정을 삭제하면 PasswordVault에서도 즉시 삭제합니다.
        if (target.IsImported)
        {
            string id = target.Id.Trim();
            if (!string.IsNullOrWhiteSpace(id))
                PasswordVaultHelper.Delete(id);
        }

        Accounts.Remove(target);

        if (Accounts.Count == 0)
            AddAccount();

        RecomputeCommon();
    }

    private void OnAccountIdChanged(object sender, TextChangedEventArgs e)
    {
        RecomputeCommon();
    }

    private void OnAccountNicknameChanged(object sender, TextChangedEventArgs e)
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

    private void LoadSavedAccounts()
    {
        IReadOnlyList<AppDataManager.AccountProfile> accounts = AppDataManager.LoadAccounts();

        foreach (var account in accounts)
        {
            if (string.IsNullOrWhiteSpace(account.Id))
                continue;

            Accounts.Add(new AccountEntry
            {
                Id = account.Id,
                Nickname = account.Nickname,
                IsImported = true
            });
        }

        if (Accounts.Count == 0)
            AddAccount();
    }

    private void SaveAccounts(IReadOnlyList<AccountEntry> completedAccounts)
    {
        var accountProfiles = new List<AppDataManager.AccountProfile>(completedAccounts.Count);

        foreach (AccountEntry account in completedAccounts)
        {
            string id = account.Id.Trim();
            string nickname = account.Nickname.Trim();

            // 별명이 비어있으면 아이디를 별명으로 저장합니다.
            string finalNickname = string.IsNullOrWhiteSpace(nickname) ? id : nickname;

            accountProfiles.Add(new AppDataManager.AccountProfile
            {
                Id = id,
                Nickname = finalNickname
            });

            // 자격 증명은 PasswordVault에 저장(동일 아이디 존재 시 덮어쓰기)
            PasswordVaultHelper.Save(id, account.Password);
        }

        // 불러온 계정은 제외하고, 이번에 입력한 계정만 LocalFolder에 저장합니다.
        AppDataManager.SaveAccounts(accountProfiles);
    }

    private void SetUiState(UiState state)
    {
        if (_uiState == state)
            return;

        _uiState = state;
        OnPropertyChanged(nameof(IsSaving));
        OnPropertyChanged(nameof(IsEdit));
        RecomputeCommon();
    }

    private void AddAccount()
    {
        Accounts.Add(new AccountEntry { IsImported = false });
    }

    private void RecomputeCommon()
    {
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanSkip));
        OnPropertyChanged(nameof(NextHintText));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private string? GetValidationErrorMessage()
    {
        List<AccountEntry> importedAccounts = Accounts.Where(a => a.IsImported).ToList();
        List<AccountEntry> completedNewAccounts = Accounts.Where(a => !a.IsImported && IsCompletedAccount(a)).ToList();

        if (importedAccounts.Count + completedNewAccounts.Count == 0)
            return "아이디+비밀번호를 모두 입력한 계정을 1개 이상 추가하면 다음 버튼이 활성화돼요.";

        if (Accounts.Any(IsPartialEditableAccount))
            return "❌ 입력 중인 계정은 아이디/비밀번호를 모두 채워주세요.";

        var duplicateCheckTargets = importedAccounts.Concat(completedNewAccounts).ToList();

        if (HasDuplicate(duplicateCheckTargets.Select(a => a.Id)))
            return "❌ 아이디가 중복되면 안돼요.";

        // 별명이 비어있으면 아이디로 취급해서 중복을 검사합니다.
        if (HasDuplicate(duplicateCheckTargets.Select(GetNicknameOrId)))
            return "❌ 별명이 중복되면 안돼요.";

        return null;
    }

    private static string GetNicknameOrId(AccountEntry account)
    {
        string nickname = (account.Nickname ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nickname))
            return (account.Id ?? "").Trim();

        return nickname;
    }

    private static bool HasDuplicate(IEnumerable<string> values)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string value in values)
        {
            string normalized = value.Trim();
            if (!set.Add(normalized))
                return true;
        }

        return false;
    }

    private static bool IsCompletedAccount(AccountEntry account)
    {
        if (account is null) return false;

        return !string.IsNullOrWhiteSpace(account.Id) &&
               !string.IsNullOrWhiteSpace(account.Password);
    }

    private static bool IsPartialEditableAccount(AccountEntry account)
    {
        if (account is null || account.IsImported)
            return false;

        // 별명은 선택 입력이므로 아이디/비밀번호 기준으로만 부분 입력 여부를 판단합니다.
        string id = (account.Id ?? "").Trim();
        string password = (account.Password ?? "").Trim();

        bool hasId = id.Length > 0;
        bool hasPassword = password.Length > 0;

        return hasId != hasPassword;
    }
}
