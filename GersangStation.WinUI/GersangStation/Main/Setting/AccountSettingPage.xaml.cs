using Core;
using Core.Models;
using GersangStation;
using GersangStation.Diagnostics;
using GersangStation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.System;

namespace GersangStation.Main.Setting;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class AccountSettingPage : Page, INotifyPropertyChanged
{
    public sealed class AccountEditor : INotifyPropertyChanged
    {
        private string _originalId = "";
        private string _id = "";
        private string _password = "";
        private string _nickname = "";
        private string _groupName = "";
        private bool _isEditingExisting;
        private bool _isChangingPassword = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string OriginalId
        {
            get => _originalId;
            private set => SetProperty(ref _originalId, value);
        }

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string Nickname
        {
            get => _nickname;
            set => SetProperty(ref _nickname, value);
        }

        public string GroupName
        {
            get => _groupName;
            set => SetProperty(ref _groupName, value);
        }

        public bool IsEditingExisting
        {
            get => _isEditingExisting;
            private set => SetProperty(ref _isEditingExisting, value);
        }

        public bool IsChangingPassword
        {
            get => _isChangingPassword;
            private set => SetProperty(ref _isChangingPassword, value);
        }

        public void BeginCreate()
        {
            OriginalId = "";
            IsEditingExisting = false;
            IsChangingPassword = true;
            Id = "";
            Password = "";
            Nickname = "";
            GroupName = "";
        }

        public void BeginEdit(Account account)
        {
            string accountId = account.Id.Trim();

            OriginalId = accountId;
            IsEditingExisting = true;
            IsChangingPassword = false;
            Id = accountId;
            Password = "";
            Nickname = account.Nickname ?? "";
            GroupName = account.GroupName ?? "";
        }

        public void SetChangingPassword(bool value)
        {
            if (!value && !string.IsNullOrEmpty(Password))
                Password = "";

            IsChangingPassword = value;
        }

        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    private readonly List<Account> _selectedAccounts = [];
    private bool _suppressSelectionChanged;
    private bool _isLoadingAccounts;
    private bool _hasBlockingAccountLoadFailure;
    private List<Account> _loadedAccounts = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool RequiresPassword => !Editor.IsEditingExisting || Editor.IsChangingPassword;

    private bool IsIdChanged => Editor.IsEditingExisting &&
        !string.Equals(Editor.OriginalId.Trim(), Editor.Id.Trim(), StringComparison.OrdinalIgnoreCase);

    public bool Editable => _selectedAccounts.Count == 1;
    public bool Deletable => _selectedAccounts.Count > 0;
    public bool CanEditSelectedAccount => AccountManagementEnabled && Editable;
    public bool CanDeleteSelectedAccount => AccountManagementEnabled && Deletable;
    public bool CanSave => !_isLoadingAccounts &&
        !_hasBlockingAccountLoadFailure &&
        !string.IsNullOrWhiteSpace(Editor.Id) &&
        (!RequiresPassword || !string.IsNullOrWhiteSpace(Editor.Password));
    public string SaveButtonText => Editor.IsEditingExisting ? "수정 저장" : "추가";
    public string EditorTitle => Editor.IsEditingExisting ? "선택한 계정 수정" : "새 계정 추가";
    public string PasswordBoxHeader => Editor.IsEditingExisting ? "새 패스워드" : "패스워드";
    public string PasswordPlaceholderText => Editor.IsEditingExisting ? "변경할 때만 입력" : "필수 입력";
    public bool AccountManagementEnabled => !_isLoadingAccounts && !_hasBlockingAccountLoadFailure && !Editor.IsEditingExisting;
    public Visibility ChangePasswordToggleVisibility => Editor.IsEditingExisting ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PasswordBoxVisibility => !Editor.IsEditingExisting || Editor.IsChangingPassword ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ResetButtonVisibility => Editor.IsEditingExisting ? Visibility.Collapsed : Visibility.Visible;
    public Visibility CancelEditButtonVisibility => Editor.IsEditingExisting ? Visibility.Visible : Visibility.Collapsed;

    public AccountEditor Editor { get; } = new();

    public AccountSettingPage()
    {
        InitializeComponent();
        Editor.PropertyChanged += Editor_PropertyChanged;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        await LoadAccountsAsync();
        BeginCreateMode(clearSelection: false);
    }

    private void AccountListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChanged)
            return;

        _selectedAccounts.Clear();
        _selectedAccounts.AddRange(AccountListView.SelectedItems.OfType<Account>());
        NotifySelectionStateChanged();
    }

    private async void Button_Save_Click(object sender, RoutedEventArgs e)
        => await SaveEditorAsync();

    /// <summary>
    /// 아이디, 패스워드, 닉네임, 그룹 이름 입력에서 Enter를 누르면 저장 동작을 실행합니다.
    /// </summary>
    private async void EditorInput_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || !CanSave)
            return;

        e.Handled = true;
        await SaveEditorAsync();
    }

    /// <summary>
    /// 현재 편집 중인 계정 입력값을 검증하고 저장합니다.
    /// </summary>
    private async Task SaveEditorAsync()
    {
        if (_isLoadingAccounts || _hasBlockingAccountLoadFailure)
            return;

        string? validationError = GetValidationErrorMessage();
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            await ShowMessageDialogAsync("계정을 저장할 수 없어요", validationError);
            return;
        }

        string originalId = Editor.OriginalId.Trim();
        string id = Editor.Id.Trim();
        string password = Editor.Password;
        string nickname = string.IsNullOrWhiteSpace(Editor.Nickname) ? id : Editor.Nickname.Trim();
        string groupName = Editor.GroupName.Trim();

        List<Account> nextAccounts = _loadedAccounts
            .Where(account => !string.IsNullOrWhiteSpace(account.Id))
            .Select(account => account.Clone())
            .ToList();

        Account nextAccount = new(id, nickname, groupName);
        int existingIndex = Editor.IsEditingExisting
            ? nextAccounts.FindIndex(account => string.Equals(account.Id, originalId, StringComparison.OrdinalIgnoreCase))
            : -1;

        if (existingIndex >= 0)
            nextAccounts[existingIndex] = nextAccount;
        else
            nextAccounts.Add(nextAccount);

        List<AppDataManager.AccountCredential> credentialUpdates;
        List<AppDataManager.AccountCredentialRename> credentialRenames;
        try
        {
            credentialUpdates = CreateCredentialUpdates(originalId, id, password);
            credentialRenames = CreateCredentialRenames(originalId, id);
        }
        catch (Exception ex)
        {
            await ShowMessageDialogAsync("비밀번호를 준비하지 못했어요", ex.Message);
            return;
        }

        (IList<Account> savedAccounts, AppDataManager.AppDataOperationResult saveResult) =
            await AppDataManager.SaveAccountsWithCredentialsAsync(nextAccounts, credentialUpdates, credentialRenames);

        if (!saveResult.Success)
        {
            await AppDataOperationDialog.ShowFailureAsync(
                XamlRoot,
                "계정 저장 실패",
                "계정 정보를 저장하지 못했습니다.",
                saveResult);
            return;
        }

        ApplyAccounts(savedAccounts);
        BeginCreateMode(clearSelection: true);
    }

    private void Button_Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedAccounts.Count != 1)
            return;

        Editor.BeginEdit(_selectedAccounts[0]);
        ScheduleEditorStartFocus(selectAll: true);
    }

    private async void Button_Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoadingAccounts || _hasBlockingAccountLoadFailure)
            return;

        if (_selectedAccounts.Count == 0)
            return;

        string targetDescription = _selectedAccounts.Count == 1
            ? $"'{_selectedAccounts[0].DisplayNickname}'"
            : $"{_selectedAccounts.Count}개 계정";

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "계정을 삭제할까요?",
            Content = $"{targetDescription}을(를) 삭제합니다. 저장된 비밀번호도 함께 제거됩니다.",
            PrimaryButtonText = "삭제",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Close
        };

        ContentDialogResult result = await dialog.ShowManagedAsync();
        if (result != ContentDialogResult.Primary)
            return;

        HashSet<string> selectedIds = _selectedAccounts
            .Select(account => account.Id.Trim())
            .Where(id => id.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<Account> nextAccounts = _loadedAccounts
            .Where(account => !string.IsNullOrWhiteSpace(account.Id))
            .Where(account => !selectedIds.Contains(account.Id.Trim()))
            .Select(account => account.Clone())
            .ToList();

        (IList<Account> savedAccounts, AppDataManager.AppDataOperationResult saveResult) =
            await AppDataManager.SaveAccountsWithCredentialsAsync(nextAccounts);

        if (!saveResult.Success)
        {
            await AppDataOperationDialog.ShowFailureAsync(
                XamlRoot,
                "계정 삭제 실패",
                "계정 정보를 삭제하지 못했습니다.",
                saveResult);
            return;
        }

        ApplyAccounts(savedAccounts);
        BeginCreateMode(clearSelection: true);
    }

    private void Button_Reset_Click(object sender, RoutedEventArgs e)
    {
        BeginCreateMode(clearSelection: true);
    }

    private void Button_CancelEdit_Click(object sender, RoutedEventArgs e)
    {
        BeginCreateMode(clearSelection: true);
    }

    private void CheckBox_ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        Editor.SetChangingPassword(CheckBox_ChangePassword.IsChecked == true);
    }

    private void PasswordBox_Pwd_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (PasswordBox_Pwd.Password != Editor.Password)
            Editor.Password = PasswordBox_Pwd.Password;
    }

    private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AccountEditor.Password) &&
            PasswordBox_Pwd is not null &&
            PasswordBox_Pwd.Password != Editor.Password)
        {
            PasswordBox_Pwd.Password = Editor.Password;
        }

        NotifyEditorStateChanged();
    }

    private async Task LoadAccountsAsync()
    {
        _isLoadingAccounts = true;
        NotifyEditorStateChanged();
        NotifySelectionStateChanged();

        (IList<Account> savedAccounts, AppDataManager.AppDataOperationResult result) = await AppDataManager.LoadAccountsAsync();
        ApplyAccounts(savedAccounts);
        _hasBlockingAccountLoadFailure = !result.Success;
        _isLoadingAccounts = false;
        NotifyEditorStateChanged();
        NotifySelectionStateChanged();

        if (!result.Success)
        {
            await App.ExceptionHandler.ShowRecoverableAsync(
                BuildAccountLoadException(result),
                "AccountSettingPage.LoadAccountsAsync");
        }
    }

    private void ApplyAccounts(IEnumerable<Account> accounts)
    {
        _loadedAccounts = accounts
            .Where(account => !string.IsNullOrWhiteSpace(account.Id))
            .Select(account => account.Clone())
            .ToList();

        AccountsCVS.Source = Account.GetAccountsGrouped(_loadedAccounts
            .Where(account => !string.IsNullOrWhiteSpace(account.Id))
            .ToList());

        ClearSelection();
    }

    private void BeginCreateMode(bool clearSelection)
    {
        Editor.BeginCreate();

        if (clearSelection)
            ClearSelection();

        ScheduleEditorStartFocus();
    }

    private void ClearSelection()
    {
        _selectedAccounts.Clear();

        if (AccountListView is not null)
        {
            _suppressSelectionChanged = true;
            AccountListView.SelectedItems.Clear();
            _suppressSelectionChanged = false;
        }

        NotifySelectionStateChanged();
    }

    private void NotifySelectionStateChanged()
    {
        OnPropertyChanged(nameof(Editable));
        OnPropertyChanged(nameof(Deletable));
        OnPropertyChanged(nameof(CanEditSelectedAccount));
        OnPropertyChanged(nameof(CanDeleteSelectedAccount));
    }

    /// <summary>
    /// 계정 목록 로드 실패를 상세 예외 창으로 전달할 예외 객체로 정리합니다.
    /// </summary>
    private static Exception BuildAccountLoadException(AppDataManager.AppDataOperationResult result)
    {
        return new InvalidOperationException(
            $"계정 목록을 불러오지 못했습니다. Operation={result.Operation}, Target={result.Target}, ErrorKind={result.ErrorKind}",
            result.Exception);
    }

    private string? GetValidationErrorMessage()
    {
        string id = Editor.Id.Trim();
        if (string.IsNullOrWhiteSpace(id))
            return "아이디를 입력해주세요.";

        if (RequiresPassword && string.IsNullOrWhiteSpace(Editor.Password))
            return Editor.IsEditingExisting ? "새 패스워드를 입력해주세요." : "패스워드를 입력해주세요.";

        string nickname = string.IsNullOrWhiteSpace(Editor.Nickname) ? id : Editor.Nickname.Trim();
        string originalId = Editor.OriginalId.Trim();

        IEnumerable<Account> otherAccounts = _loadedAccounts.Where(account =>
            !string.Equals(account.Id.Trim(), originalId, StringComparison.OrdinalIgnoreCase));

        if (otherAccounts.Any(account => string.Equals(account.Id.Trim(), id, StringComparison.OrdinalIgnoreCase)))
            return "같은 아이디의 계정이 이미 있습니다.";

        if (otherAccounts.Any(account => string.Equals(account.DisplayNickname.Trim(), nickname, StringComparison.OrdinalIgnoreCase)))
            return "같은 별명은 사용할 수 없습니다.";

        return null;
    }

    /// <summary>
    /// 계정 저장에 맞춰 함께 반영해야 할 비밀번호 변경 내용을 계산합니다.
    /// </summary>
    private List<AppDataManager.AccountCredential> CreateCredentialUpdates(string originalId, string id, string password)
    {
        // 정책:
        // - 비밀번호는 항상 계정과 1:1 관계를 유지해야 합니다.
        // - 아이디 변경만으로는 여기서 vault를 읽지 않고, Core 저장 흐름에서 키 이동을 처리합니다.
        List<AppDataManager.AccountCredential> updates = [];

        if (!Editor.IsEditingExisting)
        {
            updates.Add(new AppDataManager.AccountCredential(id, password));
            return updates;
        }

        if (Editor.IsChangingPassword)
        {
            updates.Add(new AppDataManager.AccountCredential(id, password));
            return updates;
        }

        return updates;
    }

    /// <summary>
    /// 계정 ID가 바뀐 경우 Core 저장 흐름에서 자격 증명 키를 함께 이동하도록 요청합니다.
    /// </summary>
    private List<AppDataManager.AccountCredentialRename> CreateCredentialRenames(string originalId, string id)
    {
        List<AppDataManager.AccountCredentialRename> renames = [];
        if (Editor.IsEditingExisting && IsIdChanged && !Editor.IsChangingPassword)
            renames.Add(new AppDataManager.AccountCredentialRename(originalId, id));

        return renames;
    }

    private async Task ShowMessageDialogAsync(string title, string content)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = title,
            Content = content,
            CloseButtonText = "확인",
            DefaultButton = ContentDialogButton.Close
        };

        await dialog.ShowManagedAsync();
    }

    private void NotifyEditorStateChanged()
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanEditSelectedAccount));
        OnPropertyChanged(nameof(CanDeleteSelectedAccount));
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(PasswordBoxHeader));
        OnPropertyChanged(nameof(PasswordPlaceholderText));
        OnPropertyChanged(nameof(AccountManagementEnabled));
        OnPropertyChanged(nameof(ChangePasswordToggleVisibility));
        OnPropertyChanged(nameof(PasswordBoxVisibility));
        OnPropertyChanged(nameof(ResetButtonVisibility));
        OnPropertyChanged(nameof(CancelEditButtonVisibility));
    }

    /// <summary>
    /// 편집기 모드 전환 직후 아이디 입력 칸에 포커스를 다시 맞춥니다.
    /// </summary>
    private void ScheduleEditorStartFocus(bool selectAll = false)
    {
        if (DispatcherQueue is null)
            return;

        _ = DispatcherQueue.TryEnqueueHandled(
            () => FocusEditorStartControl(selectAll),
            "AccountSettingPage.ScheduleEditorStartFocus");
    }

    /// <summary>
    /// 계정 편집기의 첫 입력 칸인 아이디 TextBox에 포커스를 적용합니다.
    /// </summary>
    private void FocusEditorStartControl(bool selectAll)
    {
        if (TextBox_Id is null)
            return;

        TextBox_Id.Focus(FocusState.Programmatic);

        if (selectAll)
            TextBox_Id.SelectAll();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 개인정보처리방침 링크를 앱 내부 브라우저 페이지로 엽니다.
    /// </summary>
    private void PrivacyPolicyHyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        if (App.CurrentWindow is MainWindow window)
            window.NavigateToWebViewPageByLinkKey(AppLinkKeys.PolicyPrivacyStore);
    }
}
