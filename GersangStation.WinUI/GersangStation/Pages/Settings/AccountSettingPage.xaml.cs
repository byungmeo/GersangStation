using Core;
using Core.Models;
using GersangStation;
using GersangStation.Diagnostics;
using GersangStation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SideDrawer.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.System;

namespace GersangStation.Main.Setting;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class AccountSettingPage : Page, INotifyPropertyChanged, IConfirmLeave
{
    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);
        e.Cancel |= _isAccountOperationInProgress || _isEditorOpen
            || AccountEditorDrawer.State != DrawerState.Closed;
    }

    public async Task<bool> ConfirmLeaveAsync(LeaveReason reason = LeaveReason.Navigation)
    {
        if (_isAccountOperationInProgress)
        {
            const string message = "계정 작업이 끝난 뒤 다시 시도해 주세요.";
            if (_isEditorOpen)
            {
                ShowEditorFeedback("작업 진행 중", message);
                AccountEditorInfoBar.Severity = InfoBarSeverity.Warning;
            }
            else
            {
                ClearAccountListFeedback();
                AccountListInfoBar.Title = "작업 진행 중";
                AccountListInfoBar.Message = message;
                AccountListInfoBar.Severity = InfoBarSeverity.Warning;
                AccountListInfoBar.IsOpen = true;
            }
            return false;
        }

        // 종료를 취소했는데 편집 내용부터 버려지는 일이 없도록 종료 의사를 먼저 확인합니다.
        if (reason == LeaveReason.AppExit
            && (XamlRoot is null || !await ExitConfirmationDialog.ShowAsync(XamlRoot)))
            return false;

        if (AccountEditorDrawer.State != DrawerState.Closed
            && !await AccountEditorDrawer.CloseAsync())
            return false;

        return !_isAccountOperationInProgress;
    }

    private enum AccountListDisplayMode
    {
        Grouped = 0,
        Ordered = 1
    }

    public sealed class AccountEditor : INotifyPropertyChanged
    {
        private string _originalId = "";
        private string _id = "";
        private string _password = "";
        private string _nickname = "";
        private string _groupName = "";
        private bool _isEditingExisting;
        private bool _isChangingPassword = true;
        private string _initialId = "";
        private string _initialNickname = "";
        private string _initialGroupName = "";
        private bool _initialChangingPassword = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool HasChanges =>
            !string.Equals(Id, _initialId, StringComparison.Ordinal) ||
            !string.Equals(Nickname, _initialNickname, StringComparison.Ordinal) ||
            !string.Equals(GroupName, _initialGroupName, StringComparison.Ordinal) ||
            IsChangingPassword != _initialChangingPassword ||
            Password.Length > 0;

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
            CaptureBaseline();
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
            CaptureBaseline();
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasChanges)));
            return true;
        }

        private void CaptureBaseline()
        {
            _initialId = Id;
            _initialNickname = Nickname;
            _initialGroupName = GroupName;
            _initialChangingPassword = IsChangingPassword;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasChanges)));
        }
    }

    private readonly List<Account> _selectedAccounts = [];
    private readonly Dictionary<Button, long> _reorderButtons = [];
    private bool _suppressSelectionChanged;
    private bool _isLoadingAccounts;
    private bool _hasBlockingAccountLoadFailure;
    private bool _isAccountOperationInProgress;
    private bool _isEditorOpen;
    private bool _allowEditorClose;
    private int _editorFeedbackGeneration;
    private int _accountListFeedbackGeneration;
    private MenuFlyout? _accountContextMenu;
    private List<Account> _loadedAccounts = [];
    private AccountListDisplayMode _accountListDisplayMode = AppDataManager.AccountListGroupedView
        ? AccountListDisplayMode.Grouped : AccountListDisplayMode.Ordered;

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<Account> OrderedAccounts { get; } = [];

    private bool RequiresPassword => !Editor.IsEditingExisting || Editor.IsChangingPassword;

    private bool IsIdChanged => Editor.IsEditingExisting &&
        !string.Equals(Editor.OriginalId.Trim(), Editor.Id.Trim(), StringComparison.OrdinalIgnoreCase);

    public bool Editable => _selectedAccounts.Count == 1;
    public bool Deletable => _selectedAccounts.Count > 0;
    public bool CanEditSelectedAccount => AccountManagementEnabled && Editable;
    public bool CanDeleteSelectedAccount => AccountManagementEnabled && Deletable;
    public bool CanSave => !_isLoadingAccounts &&
        !_hasBlockingAccountLoadFailure &&
        !_isAccountOperationInProgress && _isEditorOpen &&
        AccountEditorDrawer.State == DrawerState.Open &&
        !string.IsNullOrWhiteSpace(Editor.Id) &&
        (!RequiresPassword || !string.IsNullOrWhiteSpace(Editor.Password));
    public string SaveButtonText => Editor.IsEditingExisting ? "수정 저장" : "추가";
    public string EditorTitle => Editor.IsEditingExisting ? "선택한 계정 수정" : "새 계정 추가";
    public string PasswordBoxHeader => Editor.IsEditingExisting ? "새 패스워드" : "패스워드";
    public string PasswordPlaceholderText => Editor.IsEditingExisting ? "변경할 때만 입력" : "필수 입력";
    public bool AccountManagementEnabled => !_isLoadingAccounts && !_hasBlockingAccountLoadFailure && !_isAccountOperationInProgress && !_isEditorOpen;
    public bool CanUseAccountListActions => !_isLoadingAccounts && !_isAccountOperationInProgress && !_isEditorOpen;
    public bool CanReloadAccounts => _hasBlockingAccountLoadFailure && CanUseAccountListActions;
    public bool EditorInputsEnabled => !_isAccountOperationInProgress && AccountEditorDrawer.State == DrawerState.Open;
    public Visibility ChangePasswordToggleVisibility => Editor.IsEditingExisting ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PasswordBoxVisibility => !Editor.IsEditingExisting || Editor.IsChangingPassword ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ResetButtonVisibility => Editor.IsEditingExisting ? Visibility.Collapsed : Visibility.Visible;
    public Visibility GroupedListVisibility => _accountListDisplayMode == AccountListDisplayMode.Grouped ? Visibility.Visible : Visibility.Collapsed;
    public Visibility OrderedListVisibility => _accountListDisplayMode == AccountListDisplayMode.Ordered ? Visibility.Visible : Visibility.Collapsed;

    public AccountEditor Editor { get; } = new();

    /// <summary>
    /// 계정 목록 표시 방식을 그룹 보기 또는 순서 보기로 전환합니다.
    /// </summary>
    public int AccountListDisplayModeIndex
    {
        get => _accountListDisplayMode == AccountListDisplayMode.Ordered ? 0 : 1;
        set
        {
            if (value is < 0 or > 1) return;
            AccountListDisplayMode nextMode = value == 0
                ? AccountListDisplayMode.Ordered
                : AccountListDisplayMode.Grouped;

            if (_accountListDisplayMode == nextMode)
                return;

            SafeExecution.RunHandledAsync(() =>
            {
                try
                {
                    AppDataManager.AccountListGroupedView = nextMode == AccountListDisplayMode.Grouped;
                    _accountListDisplayMode = nextMode;
                    RefreshReorderButtons();
                }
                finally
                {
                    OnPropertyChanged(nameof(AccountListDisplayModeIndex));
                    OnPropertyChanged(nameof(GroupedListVisibility));
                    OnPropertyChanged(nameof(OrderedListVisibility));
                }
            }, "AccountSettingPage.DisplayMode").FireAndForgetHandled("AccountSettingPage.DisplayMode");
        }
    }

    public AccountSettingPage()
    {
        InitializeComponent();
        Editor.PropertyChanged += Editor_PropertyChanged;
        AccountEditorDrawer.RegisterPropertyChangedCallback(Drawer.StateProperty, (_, _) => NotifyEditorStateChanged());
    }

    /// <summary>
    /// 최초 표시 시 이미 측정된 카드 영역의 높이를 반영합니다.
    /// </summary>
    private void AccountCardViewport_Loaded(object sender, RoutedEventArgs e)
        => UpdateAccountCardContentHeight(AccountCardViewport.ActualHeight);

    /// <summary>
    /// ActualHeight는 변경 알림을 제공하지 않으므로 독립된 부모 영역의 SizeChanged에서 높이를 반영합니다.
    /// </summary>
    private void AccountCardViewport_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateAccountCardContentHeight(e.NewSize.Height);

    private void UpdateAccountCardContentHeight(double viewportHeight)
    {
        if (AccountCardContent is null || AccountSettingsCard is null)
            return;

        Thickness padding = AccountSettingsCard.Padding;
        Thickness borderThickness = AccountSettingsCard.BorderThickness;
        double height = Math.Max(0, viewportHeight - padding.Top - padding.Bottom - borderThickness.Top - borderThickness.Bottom);
        if (AccountCardContent.Height != height)
            AccountCardContent.Height = height;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        await SafeExecution.RunHandledAsync(async () =>
        {
            await LoadAccountsAsync();
            BeginCreateMode(clearSelection: false);
        }, "AccountSettingPage.OnNavigatedTo");
    }

    private void AccountListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChanged)
            return;

        ApplySelectionFrom(AccountListView.SelectedItems.OfType<Account>());
    }

    private void OrderedAccountListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChanged)
            return;

        ApplySelectionFrom(OrderedAccountListView.SelectedItems.OfType<Account>());
    }

    private async void OrderedAccountListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        => await SafeExecution.RunHandledAsync(SaveReorderedAccountsAsync, "AccountSettingPage.ReorderAccounts");

    public static string GetAccountDisplayText(string? id, string? nickname)
    {
        string accountId = id?.Trim() ?? string.Empty;
        string displayName = nickname?.Trim() ?? string.Empty;
        return displayName.Length == 0 || string.Equals(displayName, accountId, StringComparison.OrdinalIgnoreCase)
            ? accountId : $"{displayName} ({accountId})";
    }

    private async void AccountItem_Edit_Click(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(async () =>
        {
            if (sender is Button { Tag: Account account }) await EditContextAccountAsync(account.Id);
        }, "AccountSettingPage.ItemEdit");

    private async void AccountItem_Delete_Click(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(async () =>
        {
            if (sender is Button { Tag: Account account }) await DeleteContextAccountAsync(account.Id);
        }, "AccountSettingPage.ItemDelete");

    private async void AccountItem_MoveUp_Click(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(() => MoveAccountAsync(sender, -1), "AccountSettingPage.ItemMoveUp");

    private async void AccountItem_MoveDown_Click(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(() => MoveAccountAsync(sender, 1), "AccountSettingPage.ItemMoveDown");

    private void AccountItemButton_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        => e.Handled = true;

    private void ReorderButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (!_reorderButtons.ContainsKey(button))
            _reorderButtons.Add(button, button.RegisterPropertyChangedCallback(FrameworkElement.TagProperty,
                (element, _) => UpdateReorderButton((Button)element)));
        UpdateReorderButton(button);
    }

    private void ReorderButton_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && _reorderButtons.Remove(button, out long token))
            button.UnregisterPropertyChangedCallback(FrameworkElement.TagProperty, token);
    }

    private void RefreshReorderButtons()
    {
        foreach (Button button in _reorderButtons.Keys.ToArray()) UpdateReorderButton(button);
    }

    private void UpdateReorderButton(Button button)
    {
        int destination = -1;
        if (int.TryParse(button.CommandParameter?.ToString(), out int offset) && offset is -1 or 1)
        {
            if (button.Tag is Account account)
                destination = GetAccountMoveDestination(account, offset);
            else if (button.Tag is AccountGroupInfoList group && AccountsCVS.Source is IList<AccountGroupInfoList> groups)
            {
                int index = groups.ToList().FindIndex(item => item.Key == group.Key);
                destination = GetMoveDestination(index, groups.Count, offset);
            }
        }
        button.Visibility = destination >= 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static int GetMoveDestination(int index, int count, int offset)
    {
        int destination = index + offset;
        return offset is -1 or 1 && index >= 0 && index < count && destination >= 0 && destination < count
            ? destination : -1;
    }

    private int GetAccountMoveDestination(Account account, int offset)
    {
        int index = OrderedAccounts.IndexOf(account);
        if (index < 0 || offset is not (-1 or 1)) return -1;
        if (_accountListDisplayMode != AccountListDisplayMode.Grouped)
            return GetMoveDestination(index, OrderedAccounts.Count, offset);
        for (int peer = index + offset; peer >= 0 && peer < OrderedAccounts.Count; peer += offset)
            if (OrderedAccounts[peer].GroupKey == account.GroupKey) return peer;
        return -1;
    }

    private async Task MoveAccountAsync(object sender, int offset)
    {
        if (!AccountManagementEnabled || sender is not Button { Tag: Account account }) return;
        int index = OrderedAccounts.IndexOf(account);
        int destination = GetAccountMoveDestination(account, offset);
        if (destination < 0) return;
        _suppressSelectionChanged = true;
        try
        {
            if (_accountListDisplayMode == AccountListDisplayMode.Grouped)
                (OrderedAccounts[index], OrderedAccounts[destination]) = (OrderedAccounts[destination], OrderedAccounts[index]);
            else OrderedAccounts.Move(index, destination);
        }
        finally { _suppressSelectionChanged = false; }
        await SaveReorderedAccountsAsync();
    }

    private async void AccountGroup_MoveUp_Click(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(() => MoveAccountGroup(sender, -1), "AccountSettingPage.GroupMoveUp");

    private async void AccountGroup_MoveDown_Click(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(() => MoveAccountGroup(sender, 1), "AccountSettingPage.GroupMoveDown");

    private void MoveAccountGroup(object sender, int offset)
    {
        if (!AccountManagementEnabled || _accountListDisplayMode != AccountListDisplayMode.Grouped
            || sender is not Button { Tag: AccountGroupInfoList group }) return;
        List<string> order = Account.GetAccountsGrouped(_loadedAccounts, AppDataManager.AccountGroupOrder)
            .Select(item => item.Key).ToList();
        int index = order.IndexOf(group.Key);
        int destination = GetMoveDestination(index, order.Count, offset);
        if (destination < 0) return;
        (order[index], order[destination]) = (order[destination], order[index]);
        string[] selectedIds = _selectedAccounts.Select(item => item.Id.Trim()).ToArray();
        ClearAccountListFeedback();
        SetAccountOperationInProgress(true);
        try
        {
            try { AppDataManager.AccountGroupOrder = order; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ReportAccountListFailure(ex, "ReorderAccountGroups", "그룹 순서 저장 실패",
                    "계정 그룹 순서를 저장하지 못했습니다. 기존 순서와 선택한 계정은 유지됩니다. 잠시 후 다시 시도해 주세요.");
                return;
            }
            ApplyAccounts(_loadedAccounts);
            RestoreSelectionByIds(selectedIds);
        }
        finally { SetAccountOperationInProgress(false); }
    }

    private void ApplySelectionFrom(IEnumerable<Account> accounts)
    {
        _selectedAccounts.Clear();
        _selectedAccounts.AddRange(accounts);
        SyncListSelections();
        NotifySelectionStateChanged();
    }

    private async void Button_Save_Click(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(SaveEditorAsync, "AccountSettingPage.SaveEditor");

    /// <summary>
    /// 아이디, 패스워드, 닉네임, 그룹 이름 입력에서 Enter를 누르면 저장 동작을 실행합니다.
    /// </summary>
    private async void EditorInput_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || !CanSave)
            return;

        e.Handled = true;
        await SafeExecution.RunHandledAsync(SaveEditorAsync, "AccountSettingPage.SaveEditorByEnter");
    }

    /// <summary>
    /// 현재 편집 중인 계정 입력값을 검증하고 저장합니다.
    /// </summary>
    private async Task SaveEditorAsync()
    {
        if (!CanSave)
            return;

        SetAccountOperationInProgress(true);
        try { await SaveEditorCoreAsync(); }
        finally
        {
            SetAccountOperationInProgress(false);
            if (!_isEditorOpen) ScheduleAddButtonFocus();
        }
    }

    private async Task SaveEditorCoreAsync()
    {
        ClearEditorFeedback();
        string? validationError = GetValidationErrorMessage();
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            ShowEditorFeedback("계정을 저장할 수 없어요", validationError);
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
            ReportEditorSaveFailure(ex, "비밀번호를 준비하지 못했어요",
                "비밀번호 저장을 준비하지 못했습니다. 입력한 내용은 유지됩니다. 잠시 후 다시 저장해 주세요.");
            return;
        }

        IList<Account> savedAccounts;
        AppDataManager.AppDataOperationResult saveResult;
        try
        {
            (savedAccounts, saveResult) =
                await AppDataManager.SaveAccountsWithCredentialsAsync(nextAccounts, credentialUpdates, credentialRenames);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ReportEditorSaveFailure(ex, "계정 저장 실패",
                "계정 정보를 저장하지 못했습니다. 입력한 내용은 유지됩니다. 잠시 후 다시 저장해 주세요.");
            return;
        }

        if (!saveResult.Success)
        {
            string message = saveResult.ErrorKind switch
            {
                AppDataManager.AppDataErrorKind.CredentialVault =>
                    "Windows 자격 증명 저장소에 비밀번호를 저장하지 못했습니다. 입력한 내용은 유지됩니다. 잠시 후 다시 저장해 주세요.",
                AppDataManager.AppDataErrorKind.Storage =>
                    "계정 파일에 접근하거나 저장하지 못했습니다. 입력한 내용은 유지됩니다. 파일 사용 여부와 저장 권한을 확인한 후 다시 저장해 주세요.",
                AppDataManager.AppDataErrorKind.Serialization =>
                    "계정 데이터 형식을 처리하지 못했습니다. 입력한 내용은 유지됩니다. 문제가 반복되면 문의해 주세요.",
                AppDataManager.AppDataErrorKind.Validation =>
                    "계정 정보가 저장 조건에 맞지 않습니다. 입력한 내용을 확인한 후 다시 저장해 주세요.",
                _ => "계정 정보를 저장하지 못했습니다. 입력한 내용은 유지됩니다. 잠시 후 다시 저장해 주세요."
            };
            ReportEditorSaveFailure(new InvalidOperationException(
                $"계정 저장 실패: {saveResult.Operation}, {saveResult.Target}, {saveResult.ErrorKind}", saveResult.Exception),
                "계정 저장 실패", message);
            return;
        }

        ApplyAccounts(savedAccounts);
        // 저장 성공 후에만 입력 폐기 확인을 건너뛰고 닫습니다. 실패 시 폼은 그대로 유지됩니다.
        _allowEditorClose = true;
        try { await AccountEditorDrawer.CloseAsync(); }
        finally { _allowEditorClose = false; }
    }

    private async void Button_Add_Click(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(() => OpenEditorAsync(), "AccountSettingPage.AddAccount");

    private async Task OpenEditorAsync(Account? account = null)
    {
        if (!AccountManagementEnabled || !IsLoaded)
            return;

        ClearEditorFeedback();
        _accountContextMenu?.Hide();
        if (account is null)
            BeginCreateMode(clearSelection: true);
        else
            Editor.BeginEdit(account);

        _isEditorOpen = true;
        NotifyEditorStateChanged();
        try
        {
            // 페이지뿐 아니라 사이드바도 모달 범위에 포함합니다.
            AccountEditorDrawer.Host = XamlRoot.Content as FrameworkElement ?? AccountPageRoot;
            await AccountEditorDrawer.OpenAsync();
        }
        catch
        {
            if (AccountEditorDrawer.State == DrawerState.Closed)
            {
                _isEditorOpen = false;
                BeginCreateMode(clearSelection: false);
                NotifyEditorStateChanged();
            }
            throw;
        }
    }

    private void AccountEditorDrawer_Opened(object? sender, EventArgs e)
        => ScheduleEditorStartFocus(selectAll: Editor.IsEditingExisting);

    private async void AccountEditorDrawer_Closing(object? sender, DrawerClosingEventArgs e)
    {
        if (_allowEditorClose)
            return;

        e.Handled = true;
        if (_isAccountOperationInProgress)
            return;

        if (!Editor.HasChanges)
        {
            e.Handled = false;
            return;
        }

        using var deferral = e.GetDeferral();
        SetAccountOperationInProgress(true);
        try
        {
            await SafeExecution.RunHandledAsync(async () =>
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = this.XamlRoot,
                    Title = "입력한 내용을 버릴까요?",
                    Content = "저장하지 않은 계정 정보가 있습니다. 닫으면 입력한 내용이 사라집니다.",
                    PrimaryButtonText = "버리고 닫기",
                    CloseButtonText = "계속 편집",
                    DefaultButton = ContentDialogButton.Close
                };

                e.Handled = await dialog.ShowManagedAsync() != ContentDialogResult.Primary;
            }, "AccountSettingPage.ConfirmDiscardEditor");
        }
        finally { SetAccountOperationInProgress(false); }
    }

    private void AccountEditorDrawer_Closed(object? sender, DrawerClosedEventArgs e)
    {
        _isEditorOpen = false;
        BeginCreateMode(clearSelection: true);
        NotifyEditorStateChanged();
        if (e.Reason != DrawerCloseReason.HostUnloaded && !_isAccountOperationInProgress)
            ScheduleAddButtonFocus();
    }

    private void AccountEditorDrawer_InteractionFailed(object? sender, DrawerInteractionFailedEventArgs e)
        => Task.FromException(e.Exception).FireAndForgetHandled("AccountSettingPage.DrawerInteraction");

    private async void AccountList_ContextRequested(UIElement sender, ContextRequestedEventArgs e)
        => await SafeExecution.RunHandledAsync(() => ShowAccountContextMenu(sender, e), "AccountSettingPage.AccountContextMenu");

    private async void AccountList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(async () =>
        {
            if (sender is not ListView list || !AccountManagementEnabled)
                return;

            ListViewItem? container = FindAccountItemContainer(list, e.OriginalSource);
            if (container is null || list.ItemFromContainer(container) is not Account account)
                return;

            e.Handled = true;
            await EditContextAccountAsync(account.Id);
        }, "AccountSettingPage.DoubleTapEditAccount");

    private static ListViewItem? FindAccountItemContainer(ListView list, object? originalSource)
    {
        DependencyObject? source = originalSource as DependencyObject;
        while (source is not null && !ReferenceEquals(source, list))
        {
            if (source is ListViewItem item)
                return item;

            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }

    private void ShowAccountContextMenu(UIElement sender, ContextRequestedEventArgs e)
    {
        if (sender is not ListView list || !AccountManagementEnabled)
            return;

        bool hasPointerPosition = e.TryGetPosition(list, out var position);
        ListViewItem? container = FindAccountItemContainer(list, e.OriginalSource);

        Account? target = container is null ? null : list.ItemFromContainer(container) as Account;
        // 빈 영역 우클릭에는 기존 선택을 사용하지 않습니다. 키보드 메뉴 요청만 선택 항목을 사용합니다.
        if (!hasPointerPosition && target is null && list.SelectedItem is Account selected)
        {
            target = selected;
            container = list.ContainerFromItem(selected) as ListViewItem;
        }

        _accountContextMenu?.Hide();
        var menu = new MenuFlyout();
        var add = new MenuFlyoutItem { Text = "추가", Icon = new SymbolIcon(Symbol.Add) };
        AutomationProperties.SetAutomationId(add, "AccountContextAdd");
        add.Click += Button_Add_Click;
        menu.Items.Add(add);

        if (target is not null)
        {
            string targetId = target.Id;
            menu.Items.Add(new MenuFlyoutSeparator());
            var edit = new MenuFlyoutItem { Text = "수정", Icon = new SymbolIcon(Symbol.Edit) };
            AutomationProperties.SetAutomationId(edit, "AccountContextEdit");
            edit.Click += async (_, _) => await SafeExecution.RunHandledAsync(
                () => EditContextAccountAsync(targetId), "AccountSettingPage.ContextEditAccount");
            menu.Items.Add(edit);
            var delete = new MenuFlyoutItem { Text = "삭제", Icon = new SymbolIcon(Symbol.Delete) };
            AutomationProperties.SetAutomationId(delete, "AccountContextDelete");
            delete.Click += async (_, _) => await SafeExecution.RunHandledAsync(
                () => DeleteContextAccountAsync(targetId), "AccountSettingPage.ContextDeleteAccount");
            menu.Items.Add(delete);
        }

        _accountContextMenu = menu;
        e.Handled = true;
        if (hasPointerPosition)
            menu.ShowAt(list, position);
        else
            menu.ShowAt((FrameworkElement?)container ?? list);
    }

    private async Task EditContextAccountAsync(string targetId)
    {
        Account? account = FindContextAccount(targetId);
        if (account is null)
            return;
        ApplySelectionFrom([account]);
        await OpenEditorAsync(account);
    }

    private async Task DeleteContextAccountAsync(string targetId)
    {
        Account? account = FindContextAccount(targetId);
        if (account is null)
            return;
        ApplySelectionFrom([account]);
        await DeleteSelectedAccountsAsync();
    }

    private Account? FindContextAccount(string targetId)
        => AccountManagementEnabled
            ? _loadedAccounts.FirstOrDefault(account => string.Equals(account.Id, targetId, StringComparison.OrdinalIgnoreCase))
            : null;

    private void SetAccountOperationInProgress(bool value)
    {
        _isAccountOperationInProgress = value;
        NotifyEditorStateChanged();
        NotifySelectionStateChanged();
    }

    private async void Button_Edit_Click(object sender, RoutedEventArgs e)
    {
        if (!CanEditSelectedAccount)
            return;

        Account selected = _selectedAccounts[0];
        await SafeExecution.RunHandledAsync(() => OpenEditorAsync(selected), "AccountSettingPage.EditAccount");
    }

    private async void Button_Delete_Click(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(DeleteSelectedAccountsAsync, "AccountSettingPage.DeleteAccounts");

    private async Task DeleteSelectedAccountsAsync()
    {
        if (!CanDeleteSelectedAccount)
            return;

        // 확인 대기 중 선택이 바뀌어도 삭제 대상은 고정합니다.
        Account[] targets = _selectedAccounts.ToArray();
        SetAccountOperationInProgress(true);
        try { await DeleteAccountsCoreAsync(targets); }
        finally { SetAccountOperationInProgress(false); }
    }

    private async Task DeleteAccountsCoreAsync(IReadOnlyList<Account> targets)
    {
        string targetDescription = targets.Count == 1
            ? $"'{targets[0].DisplayNickname}'"
            : $"{targets.Count}개 계정";

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

        ClearAccountListFeedback();
        HashSet<string> selectedIds = targets
            .Select(account => account.Id.Trim())
            .Where(id => id.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<Account> nextAccounts = _loadedAccounts
            .Where(account => !string.IsNullOrWhiteSpace(account.Id))
            .Where(account => !selectedIds.Contains(account.Id.Trim()))
            .Select(account => account.Clone())
            .ToList();

        var (savedAccounts, saved) = await TrySaveAccountListAsync(nextAccounts, "DeleteAccounts",
            "계정 삭제 실패", "계정 정보를 삭제하지 못했습니다. 목록과 선택한 계정은 유지됩니다. 파일 사용 여부와 저장 권한을 확인한 후 다시 시도해 주세요.");
        if (!saved)
            return;

        ApplyAccounts(savedAccounts);
        BeginCreateMode(clearSelection: true);
    }

    private void Button_Reset_Click(object sender, RoutedEventArgs e)
    {
        if (!EditorInputsEnabled)
            return;
        BeginCreateMode(clearSelection: true);
        ScheduleEditorStartFocus();
    }

    private async void Button_CancelEdit_Click(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(async () => { await AccountEditorDrawer.CloseAsync(); }, "AccountSettingPage.CancelEditor");

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
        if (!CanUseAccountListActions)
            return;
        string[] selectedIds = _selectedAccounts.Select(account => account.Id.Trim()).ToArray();
        _accountListFeedbackGeneration++;
        _isLoadingAccounts = true;
        NotifyEditorStateChanged();
        NotifySelectionStateChanged();
        try
        {
            var (savedAccounts, result) = await AppDataManager.LoadAccountsAsync();
            if (!result.Success)
            {
                ReportAccountLoadFailure(BuildAccountLoadException(result));
                return;
            }
            ApplyAccounts(savedAccounts);
            RestoreSelectionByIds(selectedIds);
            _hasBlockingAccountLoadFailure = false;
            ClearAccountListFeedback();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ReportAccountLoadFailure(ex);
        }
        finally
        {
            _isLoadingAccounts = false;
            NotifyEditorStateChanged();
            NotifySelectionStateChanged();
        }
    }

    private void ReportAccountLoadFailure(Exception exception)
    {
        _hasBlockingAccountLoadFailure = true;
        ReportAccountListFailure(exception, "LoadAccountsAsync", "계정 목록 불러오기 실패",
            "계정 목록을 불러오지 못했습니다. 기존 데이터를 보호하기 위해 계정 변경을 중단했습니다. 파일 접근 문제나 데이터 상태를 확인한 후 다시 불러와 주세요.");
        AccountListInfoBar.IsClosable = false;
        ReloadAccountsButton.Visibility = Visibility.Visible;
    }

    private async void ReloadAccountsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CanReloadAccounts)
            return;
        await SafeExecution.RunHandledAsync(LoadAccountsAsync, "AccountSettingPage.ReloadAccounts");
    }

    private void ApplyAccounts(IEnumerable<Account> accounts)
    {
        _loadedAccounts = accounts
            .Where(account => !string.IsNullOrWhiteSpace(account.Id))
            .Select(account => account.Clone())
            .ToList();

        OrderedAccounts.Clear();
        foreach (Account account in _loadedAccounts)
            OrderedAccounts.Add(account);

        AccountsCVS.Source = Account.GetAccountsGrouped(_loadedAccounts
            .Where(account => !string.IsNullOrWhiteSpace(account.Id))
            .ToList(), AppDataManager.AccountGroupOrder);

        RefreshReorderButtons();

        ClearSelection();
    }

    private void BeginCreateMode(bool clearSelection)
    {
        ClearEditorFeedback();
        Editor.BeginCreate();

        if (clearSelection)
            ClearSelection();
    }

    private void ClearSelection()
    {
        _selectedAccounts.Clear();

        SyncListSelections();
        NotifySelectionStateChanged();
    }

    private void SyncListSelections()
    {
        List<Account> selectedAccounts = _selectedAccounts.ToList();

        if (AccountListView is not null)
        {
            _suppressSelectionChanged = true;
            AccountListView.SelectedItems.Clear();
            foreach (Account account in selectedAccounts)
                AccountListView.SelectedItems.Add(account);
            _suppressSelectionChanged = false;
        }

        if (OrderedAccountListView is not null)
        {
            _suppressSelectionChanged = true;
            OrderedAccountListView.SelectedItems.Clear();
            foreach (Account account in selectedAccounts)
                OrderedAccountListView.SelectedItems.Add(account);
            _suppressSelectionChanged = false;
        }
    }

    private void NotifySelectionStateChanged()
    {
        OnPropertyChanged(nameof(Editable));
        OnPropertyChanged(nameof(Deletable));
        OnPropertyChanged(nameof(CanEditSelectedAccount));
        OnPropertyChanged(nameof(CanDeleteSelectedAccount));
    }

    /// <summary>
    /// 계정 목록 로드 실패의 원본 예외와 작업 메타데이터를 진단 기록에 보존합니다.
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

    private void ClearEditorFeedback()
    {
        _editorFeedbackGeneration++;
        AccountEditorInfoBar.IsOpen = false;
        EditorCredentialHelpButton.Visibility = Visibility.Collapsed;
    }

    private void ShowEditorFeedback(string title, string message, bool showCredentialHelp = false)
    {
        AccountEditorInfoBar.Severity = InfoBarSeverity.Error;
        AccountEditorInfoBar.Title = title;
        AccountEditorInfoBar.Message = message;
        EditorCredentialHelpButton.Visibility = showCredentialHelp ? Visibility.Visible : Visibility.Collapsed;
        AccountEditorInfoBar.IsOpen = true;
    }

    private void ReportEditorSaveFailure(Exception exception, string title, string message)
    {
        App.ExceptionHandler.RecordRecoverable(exception, "AccountSettingPage.SaveEditor");
        bool serviceUnavailable = CredentialVaultGuidanceDialog.IsServiceUnavailable(exception);
        if (serviceUnavailable)
            message = "Windows 자격 증명 관리자 서비스에 접근하지 못했습니다. 입력한 내용은 유지됩니다. 해결 방법을 확인한 후 다시 저장해 주세요.";
        ShowEditorFeedback(title, message, serviceUnavailable);
    }

    private async void EditorCredentialHelpButton_Click(object sender, RoutedEventArgs e)
    {
        int generation = _editorFeedbackGeneration;
        try
        {
            if (!App.LinkManager.TryGetUrl(AppLinkKeys.HelpCredentialVault, out string url) ||
                !await Launcher.LaunchUriAsync(new Uri(url)))
                throw new InvalidOperationException("자격 증명 관리자 도움말을 외부 브라우저에서 열지 못했습니다.");
        }
        catch (Exception ex)
        {
            App.ExceptionHandler.RecordRecoverable(ex, "AccountSettingPage.OpenCredentialHelp");
            if (_isEditorOpen && generation == _editorFeedbackGeneration)
                ShowEditorFeedback("도움말을 열지 못했습니다.",
                    "계정은 아직 저장되지 않았으며 입력한 내용은 유지됩니다. 잠시 후 다시 시도해 주세요.", showCredentialHelp: true);
        }
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
        OnPropertyChanged(nameof(CanUseAccountListActions));
        OnPropertyChanged(nameof(CanReloadAccounts));
        OnPropertyChanged(nameof(EditorInputsEnabled));
        OnPropertyChanged(nameof(ChangePasswordToggleVisibility));
        OnPropertyChanged(nameof(PasswordBoxVisibility));
        OnPropertyChanged(nameof(ResetButtonVisibility));
    }

    /// <summary>
    /// 드래그 또는 이동 버튼으로 바뀐 계정 배열 순서를 그대로 저장합니다.
    /// </summary>
    private async Task SaveReorderedAccountsAsync()
    {
        if (!AccountManagementEnabled)
            return;

        SetAccountOperationInProgress(true);
        try { await SaveReorderedAccountsCoreAsync(); }
        finally { SetAccountOperationInProgress(false); }
    }

    private async Task SaveReorderedAccountsCoreAsync()
    {
        string[] currentOrder = _loadedAccounts.Select(account => account.Id).ToArray();
        string[] reorderedOrder = OrderedAccounts.Select(account => account.Id).ToArray();
        if (currentOrder.SequenceEqual(reorderedOrder, StringComparer.Ordinal))
            return;

        ClearAccountListFeedback();
        HashSet<string> selectedIds = _selectedAccounts
            .Select(account => account.Id.Trim())
            .Where(id => id.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<Account> nextAccounts = OrderedAccounts
            .Where(account => !string.IsNullOrWhiteSpace(account.Id))
            .Select(account => account.Clone())
            .ToList();

        bool committed = false;
        try
        {
            var (savedAccounts, saved) = await TrySaveAccountListAsync(nextAccounts, "ReorderAccounts",
                "계정 순서 저장 실패", "계정 표시 순서를 저장하지 못해 이전 순서로 되돌렸습니다. 선택한 계정은 유지됩니다. 잠시 후 다시 시도해 주세요.");
            if (!saved)
                return;
            committed = true;
            ApplyAccounts(savedAccounts);
            RestoreSelectionByIds(selectedIds);
        }
        finally
        {
            if (!committed)
            {
                ApplyAccounts(_loadedAccounts);
                RestoreSelectionByIds(selectedIds);
            }
        }
    }

    private void ClearAccountListFeedback()
    {
        _accountListFeedbackGeneration++;
        AccountListInfoBar.IsOpen = false;
        AccountListInfoBar.IsClosable = true;
        ReloadAccountsButton.Visibility = Visibility.Collapsed;
        AccountListCredentialHelpButton.Visibility = Visibility.Collapsed;
    }

    private async Task<(IList<Account> Accounts, bool Success)> TrySaveAccountListAsync(
        List<Account> accounts, string operation, string title, string message)
    {
        AppDataManager.AppDataOperationResult result;
        IList<Account> savedAccounts;
        try { (savedAccounts, result) = await AppDataManager.SaveAccountsWithCredentialsAsync(accounts); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ReportAccountListFailure(ex, operation, title, message);
            return (Array.Empty<Account>(), false);
        }
        if (!result.Success)
        {
            ReportAccountListFailure(new InvalidOperationException(
                $"계정 목록 저장 실패: {result.Operation}, {result.Target}, {result.ErrorKind}", result.Exception),
                operation, title, message);
            return (Array.Empty<Account>(), false);
        }
        return (savedAccounts, true);
    }

    private void ReportAccountListFailure(Exception exception, string operation, string title, string message)
    {
        App.ExceptionHandler.RecordRecoverable(exception, $"AccountSettingPage.{operation}");
        bool serviceUnavailable = CredentialVaultGuidanceDialog.IsServiceUnavailable(exception);
        if (serviceUnavailable)
            message += " Windows 자격 증명 관리자 서비스에 접근하지 못했습니다. 해결 방법을 확인해 주세요.";
        AccountListInfoBar.Severity = InfoBarSeverity.Error;
        AccountListInfoBar.Title = title;
        AccountListInfoBar.Message = message;
        AccountListCredentialHelpButton.Visibility = serviceUnavailable ? Visibility.Visible : Visibility.Collapsed;
        AccountListInfoBar.IsOpen = true;
    }

    private async void AccountListCredentialHelpButton_Click(object sender, RoutedEventArgs e)
    {
        int generation = _accountListFeedbackGeneration;
        try
        {
            if (!App.LinkManager.TryGetUrl(AppLinkKeys.HelpCredentialVault, out string url) ||
                !await Launcher.LaunchUriAsync(new Uri(url)))
                throw new InvalidOperationException("자격 증명 관리자 도움말을 외부 브라우저에서 열지 못했습니다.");
        }
        catch (Exception ex)
        {
            App.ExceptionHandler.RecordRecoverable(ex, "AccountSettingPage.OpenListCredentialHelp");
            if (IsLoaded && generation == _accountListFeedbackGeneration)
            {
                AccountListInfoBar.Message = "도움말을 열지 못했습니다. 잠시 후 다시 시도해 주세요.";
                AccountListInfoBar.IsOpen = true;
            }
        }
    }

    /// <summary>
    /// 계정 컬렉션을 다시 그린 뒤에도 기존 선택 계정을 ID 기준으로 복원합니다.
    /// </summary>
    private void RestoreSelectionByIds(IReadOnlyCollection<string> selectedIds)
    {
        if (selectedIds.Count == 0)
            return;

        _selectedAccounts.Clear();
        _selectedAccounts.AddRange(_loadedAccounts.Where(account => selectedIds.Contains(account.Id.Trim(), StringComparer.OrdinalIgnoreCase)));
        SyncListSelections();
        NotifySelectionStateChanged();
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
        if (TextBox_Id is null || !_isEditorOpen || AccountEditorDrawer.State != DrawerState.Open)
            return;

        TextBox_Id.Focus(FocusState.Programmatic);

        if (selectAll)
            TextBox_Id.SelectAll();
    }

    private void ScheduleAddButtonFocus()
    {
        _ = DispatcherQueue.TryEnqueueHandled(() =>
        {
            if (IsLoaded && AccountManagementEnabled)
                Button_Add.Focus(FocusState.Programmatic);
        }, "AccountSettingPage.ScheduleAddButtonFocus");
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 개인정보처리방침 링크를 앱 내부 브라우저 페이지로 엽니다.
    /// </summary>
    private async void PrivacyPolicyHyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        => await SafeExecution.RunHandledAsync(async () =>
        {
            if (await AccountEditorDrawer.CloseAsync() && App.CurrentWindow is MainWindow window)
                window.NavigateToWebViewPageByLinkKey(AppLinkKeys.PolicyPrivacyStore);
        }, "AccountSettingPage.OpenPrivacyPolicy");
}
