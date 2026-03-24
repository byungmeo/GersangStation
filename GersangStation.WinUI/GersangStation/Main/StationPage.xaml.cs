using Core;
using Core.Models;
using GersangStation.Diagnostics;
using GersangStation.Main.Setting;
using GersangStation.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace GersangStation.Main;

/// <summary>
/// StationPage에서 이벤트 썸네일과 오버레이 표시를 위해 가공한 모델입니다.
/// </summary>
public sealed record StationEventThumbnailItem(
    string Title,
    string ThumbnailUrl,
    string? DetailUrl,
    string PeriodOverlayText,
    int? RemainingDays);

/// <summary>
/// StationPage에서 메인 공지사항 목록 표시를 위해 가공한 모델입니다.
/// </summary>
public sealed record StationHomepageNoticeItem(
    string Title,
    string DateText,
    string Url);

/// <summary>
/// StationPage의 계정 콤보박스에서 사용하는 선택 항목입니다.
/// </summary>
public sealed record StationAccountSelectionOption(string Id, string DisplayName)
{
    /// <summary>
    /// 계정을 선택하지 않은 상태를 나타내는 기본 항목입니다.
    /// </summary>
    public static StationAccountSelectionOption Unselected { get; } = new(string.Empty, "계정 미선택");

    /// <summary>
    /// 저장된 계정 목록 앞에 미선택 항목을 붙인 콤보박스용 목록을 만듭니다.
    /// </summary>
    public static IReadOnlyList<StationAccountSelectionOption> Create(IEnumerable<Account>? accounts)
    {
        List<StationAccountSelectionOption> options = [Unselected];

        if (accounts is not null)
        {
            options.AddRange(accounts.Select(account => new StationAccountSelectionOption(
                account.Id,
                account.DisplayNickname)));
        }

        return options;
    }
}

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class StationPage : Page, INotifyPropertyChanged
{
    private enum ClientLaunchStatus
    {
        Available,
        Starting,
        Running,
        RetryCooldown,
        MultiClientDisabled,
        InstallPathMissing,
        InstallPathInvalid,
        MultiClientSetupRequired
    }

    private readonly record struct ClientLaunchAvailability(ClientLaunchStatus Status, string? Detail = null);
    private readonly record struct MultiClientCreationAttemptResult(bool Success, string Reason, Exception? Exception = null);

    private bool _isInitializing = true;
    private bool _isShellActive;
    private MainWindow? _shellWindow;
    private GameStarter? _gameStarter;
    private readonly ClientLaunchAvailability[] _clientLaunchAvailability = new ClientLaunchAvailability[3];
    private IReadOnlyList<StationAccountSelectionOption> _accountSelectionOptions = StationAccountSelectionOption.Create(accounts: null);
    private AppDataManager.AppDataOperationResult _accountsLoadResult = AppDataManager.AppDataOperationResult.Ok(
        nameof(AppDataManager.LoadAccountsAsync),
        "accounts.json");
    private AppDataManager.AppDataOperationResult _presetLoadResult = AppDataManager.AppDataOperationResult.Ok(
        nameof(AppDataManager.LoadPresetListAsync),
        "preset-list.json");

    private IList<Account> _accounts = [];
    public IList<Account> Accounts
    {
        get => _accounts;
        private set
        {
            if (ReferenceEquals(_accounts, value))
                return;

            _accounts = value;
            OnPropertyChanged(nameof(Accounts));
            AccountSelectionOptions = StationAccountSelectionOption.Create(value);
        }
    }

    public IReadOnlyList<StationAccountSelectionOption> AccountSelectionOptions
    {
        get => _accountSelectionOptions;
        private set
        {
            if (ReferenceEquals(_accountSelectionOptions, value))
                return;

            _accountSelectionOptions = value;
            OnPropertyChanged(nameof(AccountSelectionOptions));
        }
    }

    private int _selectedServerIndex;
    public int SelectedServerIndex
    {
        get => _selectedServerIndex;
        set
        {
            if (_selectedServerIndex != value)
            {
                _selectedServerIndex = value;
                OnPropertyChanged(nameof(SelectedServerIndex));

                if (!_isInitializing)
                    AppDataManager.SelectedServer = (GameServer)value;

                RefreshClientAvailabilityState();
            }
        }
    }

    private int _selectedPreset;
    public int SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            int normalizedValue = NormalizePresetIndex(value);
            if (_selectedPreset != normalizedValue)
            {
                _selectedPreset = normalizedValue;

                if (!_isInitializing)
                    AppDataManager.SelectedPreset = normalizedValue;

                OnPropertyChanged(nameof(SelectedPreset));
                OnPropertyChanged(nameof(SelectedAccount1Id));
                OnPropertyChanged(nameof(SelectedAccount2Id));
                OnPropertyChanged(nameof(SelectedAccount3Id));
                RefreshClientAvailabilityState();
            }
        }
    }

    private PresetList _presetList = new();
    public PresetList PresetList
    {
        get => _presetList;
        set
        {
            _presetList = value;

            if (!_isInitializing)
                AppDataManager.SavePresetList(PresetList);
        }
    }

    public string SelectedAccount1Id { get => GetId(0); set => SetId(0, value); }
    public string SelectedAccount2Id { get => GetId(1); set => SetId(1, value); }
    public string SelectedAccount3Id { get => GetId(2); set => SetId(2, value); }
    public string CurrentAppVersionText { get; private set; } = "현재 버전: 확인 중";
    public Visibility StoreUpdateButtonVisibility { get; private set; } = Visibility.Collapsed;
    public bool StoreUpdateButtonEnabled { get; private set; } = true;

    /// <summary>
    /// 현재 프리셋에서 지정된 계정 ID를 반환합니다.
    /// </summary>
    private string GetId(int comboBoxIndex)
    {
        int presetIndex = NormalizePresetIndex(_selectedPreset);
        return _presetList.Presets[presetIndex].Items[comboBoxIndex].Id;
    }

    /// <summary>
    /// 현재 프리셋의 슬롯 계정을 갱신하고 필요 시 저장합니다.
    /// </summary>
    private void SetId(int comboBoxIndex, string selectedValue)
    {
        selectedValue ??= string.Empty;

        int presetIndex = NormalizePresetIndex(_selectedPreset);
        if (_presetList.Presets[presetIndex].Items[comboBoxIndex].Id == selectedValue)
            return;

        _presetList.Presets[presetIndex].Items[comboBoxIndex].Id = selectedValue;
        if (!_isInitializing)
            AppDataManager.SavePresetList(_presetList);

        switch (comboBoxIndex)
        {
            case 0:
                OnPropertyChanged(nameof(SelectedAccount1Id));
                break;
            case 1:
                OnPropertyChanged(nameof(SelectedAccount2Id));
                break;
            case 2:
                OnPropertyChanged(nameof(SelectedAccount3Id));
                break;
        }

        RefreshClientAvailabilityState();
    }

    private int NormalizePresetIndex(int value)
    {
        if (_presetList?.Presets is null || _presetList.Presets.Length == 0)
            return 0;

        if (value < 0)
            return 0;

        if (value >= _presetList.Presets.Length)
            return _presetList.Presets.Length - 1;

        return value;
    }

    /// <summary>
    /// 저장소에서 서버, 프리셋, 계정 선택 상태를 다시 읽어옵니다.
    /// </summary>
    private void RefreshStateFromStorage()
    {
        bool previousInitializing = _isInitializing;
        _isInitializing = true;

        try
        {
            (IList<Account> loadedAccounts, AppDataManager.AppDataOperationResult accountsLoadResult) = AppDataManager.TryLoadAccounts();
            (PresetList loadedPresetList, AppDataManager.AppDataOperationResult presetLoadResult) = AppDataManager.TryLoadPresetList();

            _accountsLoadResult = accountsLoadResult;
            _presetLoadResult = presetLoadResult;

            Accounts = loadedAccounts;
            PresetList = loadedPresetList;

            SelectedServerIndex = (int)AppDataManager.SelectedServer;
            SelectedPreset = NormalizePresetIndex(AppDataManager.SelectedPreset);

            if (SelectedPreset != AppDataManager.SelectedPreset)
                AppDataManager.SelectedPreset = SelectedPreset;
        }
        finally
        {
            _isInitializing = previousInitializing;
        }

        OnPropertyChanged(nameof(SelectedAccount1Id));
        OnPropertyChanged(nameof(SelectedAccount2Id));
        OnPropertyChanged(nameof(SelectedAccount3Id));
    }

    public StationPage()
    {
        RefreshStateFromStorage();
        InitializeComponent();
        _eventFlipTimer.Interval = EventFlipInterval;
        _eventFlipTimer.Tick += EventFlipTimer_Tick;
        _eventUrgencyTimer.Tick += EventUrgencyTimer_Tick;
        _isInitializing = false;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _shellWindow = e.Parameter as MainWindow ?? App.CurrentWindow as MainWindow;
    }

    /// <summary>
    /// Station 섹션이 다시 보일 때 저장 상태와 GameStarter 상태를 동기화합니다.
    /// </summary>
    internal async Task OnShellActivatedAsync(MainWindow? window)
    {
        _isShellActive = true;
        _shellWindow = window ?? _shellWindow ?? App.CurrentWindow as MainWindow;

        if (_shellWindow is not null)
        {
            AttachGameStarter(_shellWindow.GameStarter);
            _shellWindow.StoreUpdateStateChanged -= MainWindow_StoreUpdateStateChanged;
            _shellWindow.StoreUpdateStateChanged += MainWindow_StoreUpdateStateChanged;
            SyncStoreUpdateState(_shellWindow);
        }
        else
        {
            CurrentAppVersionText = CreateCurrentVersionText();
            StoreUpdateButtonVisibility = Visibility.Collapsed;
            StoreUpdateButtonEnabled = false;
        }

        RefreshStateFromStorage();
        RefreshClientAvailabilityState();
        Bindings.Update();
        await HandleInitialStorageLoadIssuesAsync();
        RefreshClientAvailabilityState();
        Bindings.Update();
        await UpdateServer();
        await Task.WhenAll(
            LoadEventsAsync(),
            LoadHomepageNoticesAsync(),
            LoadGersangStationNoticesAsync());
        UpdateEventFlipTimerState();
    }

    /// <summary>
    /// Station 섹션이 숨겨질 때 타이머와 구독을 정리합니다.
    /// </summary>
    internal void OnShellDeactivated()
    {
        if (!_isShellActive)
            return;

        _isShellActive = false;
        StopEventFlipTimer();
        StopEventUrgencyTimer(resetHighlight: true);
        CancelEventLoad();
        CancelHomepageNoticeLoad();
        CancelGersangStationNoticeLoad();
        DetachGameStarter();

        if (_shellWindow is not null)
            _shellWindow.StoreUpdateStateChanged -= MainWindow_StoreUpdateStateChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// 1클라 실행 버튼을 처리합니다.
    /// </summary>
    private async void Button_Client1_Execute_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => await HandleClientExecuteAsync(0, SelectedAccount1Id);

    /// <summary>
    /// 2클라 실행 버튼을 처리합니다.
    /// </summary>
    private async void Button_Client2_Execute_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => await HandleClientExecuteAsync(1, SelectedAccount2Id);

    /// <summary>
    /// 3클라 실행 버튼을 처리합니다.
    /// </summary>
    private async void Button_Client3_Execute_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => await HandleClientExecuteAsync(2, SelectedAccount3Id);

    /// <summary>
    /// 슬롯별 클라이언트 경로와 실행 정책을 검사한 뒤 실제 게임 시작을 요청합니다.
    /// </summary>
    private async Task HandleClientExecuteAsync(int clientIndex, string accountId)
    {
        if (App.CurrentWindow is not MainWindow window || window.WebViewManager is null)
            return;

        GameServer server = (GameServer)SelectedServerIndex;
        ClientLaunchAvailability availability = GetClientLaunchAvailability(clientIndex);
        switch (availability.Status)
        {
            case ClientLaunchStatus.InstallPathMissing:
                await HandleMissingInstallPathAsync(window, server);
                return;
            case ClientLaunchStatus.InstallPathInvalid:
                await HandleInvalidInstallPathAsync(window, server, availability.Detail);
                return;
            case ClientLaunchStatus.MultiClientSetupRequired:
                await HandleMultiClientSetupRequiredAsync(server, clientIndex, availability.Detail);
                return;
            case ClientLaunchStatus.Starting:
            case ClientLaunchStatus.Running:
                return;
            case ClientLaunchStatus.Available:
            default:
                break;
        }

        accountId = accountId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(accountId))
        {
            await HandleMissingAccountSelectionAsync(window);
            return;
        }

        if (!await EnsureAccountReadyForLaunchAsync(window, server, accountId))
            return;

        string clientInstallPath = GetClientInstallPath(server, clientIndex);

        bool requiresPatch = await CheckAndHandleRequiredPatchAsync(window, server, clientInstallPath);
        if (requiresPatch)
            return;

        if (_gameStarter?.ActiveClientCount >= 3)
        {
            await ShowWarningDialogAsync("최대 실행 개수에 도달했어요", "거상은 동시에 최대 3개 클라이언트까지만 실행할 수 있습니다.");
            return;
        }

        // 동일 계정 중복은 서버가 달라도 금지합니다.
        if (_gameStarter?.IsAccountActive(accountId) == true)
        {
            await ShowWarningDialogAsync("이미 사용 중인 계정이에요", "동일한 계정으로 다른 클라이언트를 동시에 실행할 수 없습니다.");
            return;
        }

        Debug.WriteLine("App.CurrentWindow is MainWindow window");
        Debug.WriteLine("window.WebViewManager is not null");
        _ = await window.WebViewManager.TryGameStart(accountId, clientIndex);
    }

    /// <summary>
    /// 실행할 계정이 선택되지 않았을 때 계정 설정 페이지 이동을 제안합니다.
    /// </summary>
    private async Task HandleMissingAccountSelectionAsync(MainWindow window)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "계정을 먼저 선택해 주세요",
            Content =
                "선택한 클라이언트에 사용할 계정이 없습니다.\n\n" +
                "계정 설정 페이지로 이동할까요?",
            PrimaryButtonText = "계정 설정",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowManagedAsync() == ContentDialogResult.Primary)
            window.NavigateToSettingPage(SettingSection.Account);
    }

    /// <summary>
    /// 메인 클라이언트 경로가 비어 있을 때 경로 설정 또는 게임 설치 화면으로 안내합니다.
    /// </summary>
    private async Task HandleMissingInstallPathAsync(MainWindow window, GameServer server)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "경로 미설정",
            Content = $"{GameServerHelper.GetServerDisplayName(server)} 메인 클라이언트 경로가 설정되지 않았습니다.",
            PrimaryButtonText = "경로 설정",
            SecondaryButtonText = "게임 설치",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowManagedAsync();
        if (result == ContentDialogResult.Primary)
        {
            window.NavigateToSettingPage(
                SettingSection.InstallPath,
                new GameServerSettingNavigationParameter { Server = server });
        }
        else if (result == ContentDialogResult.Secondary)
        {
            window.NavigateToSettingPage(
                SettingSection.GameInstall,
                new GameServerSettingNavigationParameter { Server = server });
        }
    }

    /// <summary>
    /// 메인 클라이언트 경로가 잘못되었을 때 서버별 경로 설정 화면으로 유도합니다.
    /// </summary>
    private async Task HandleInvalidInstallPathAsync(MainWindow window, GameServer server, string? reason)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "잘못된 경로",
            Content = string.IsNullOrWhiteSpace(reason)
                ? "현재 설정된 메인 클라이언트 경로가 올바르지 않습니다.\n경로를 다시 설정할까요?"
                : $"{reason}\n\n경로를 다시 설정할까요?",
            PrimaryButtonText = "다시 설정",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowManagedAsync() == ContentDialogResult.Primary)
        {
            window.NavigateToSettingPage(
                SettingSection.InstallPath,
                new GameServerSettingNavigationParameter { Server = server });
        }
    }

    /// <summary>
    /// 복제 클라이언트가 없거나 메인 클라이언트와 불일치할 때 다클라 생성을 제안합니다.
    /// </summary>
    private async Task HandleMultiClientSetupRequiredAsync(GameServer server, int clientIndex, string? detail)
    {
        string clientLabel = clientIndex == 1 ? "2클라" : "3클라";
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "다클 생성 필요",
            Content = string.IsNullOrWhiteSpace(detail)
                ? $"{clientLabel} 경로가 없거나 메인 클라이언트와 동기화되지 않았습니다.\n다클라를 생성할까요?"
                : $"{clientLabel} 생성이 필요합니다.\n{detail}\n\n다클라를 생성할까요?",
            PrimaryButtonText = "생성",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowManagedAsync() != ContentDialogResult.Primary)
            return;

        MultiClientCreationAttemptResult createResult = await TryCreateMissingMultiClientAsync(server, clientIndex);
        if (!createResult.Success &&
            await PathPermissionDialog.ShowFailureGuidanceWhenPermissionMissingAsync(
                XamlRoot,
                createResult.Exception,
                "다클라 생성"))
        {
            RefreshClientAvailabilityState();
            await UpdateServer();
            return;
        }

        await ShowWarningDialogAsync(
            createResult.Success ? "다클라 생성 완료" : "다클라 생성 실패",
            createResult.Success ? $"{clientLabel} 생성을 완료했습니다." : createResult.Reason);

        RefreshClientAvailabilityState();
        await UpdateServer();
    }

    /// <summary>
    /// 실행 대상 클라이언트가 구버전이면 패치 안내 후 설정의 패치 화면으로 유도합니다.
    /// </summary>
    private async Task<bool> CheckAndHandleRequiredPatchAsync(MainWindow window, GameServer server, string clientInstallPath)
    {
        if (string.IsNullOrWhiteSpace(clientInstallPath) || !Directory.Exists(clientInstallPath))
            return false;

        ClientVersionReadResult currentVersionResult = PatchManager.TryGetCurrentClientVersion(clientInstallPath);
        if (!currentVersionResult.Success)
        {
            await ShowClientVersionCheckRequiredDialogAsync(window, server, currentVersionResult);
            return true;
        }

        int currentVersion = currentVersionResult.Version ?? 0;
        if (currentVersion <= 0)
        {
            await ShowWarningDialogAsync(
                "클라이언트 버전 확인 필요",
                $"{GameServerHelper.GetServerDisplayName(server)} 클라이언트 버전을 확인하지 못했습니다.\n설치 경로가 올바른지 확인해 주세요.");
            return true;
        }

        int latestVersion;
        try
        {
            latestVersion = await PatchManager.GetLatestServerVersionAsync(server);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            bool shouldContinue = await ShowLatestVersionCheckFailedDialogAsync(server);
            if (!shouldContinue)
                return true;

            Debug.WriteLine($"[StationPage] Latest version check failed but user chose to continue. Server: {server}, Error: {ex}");
            return false;
        }

        if (currentVersion >= latestVersion)
            return false;

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "패치가 필요합니다",
            Content =
                $"{GameServerHelper.GetServerDisplayName(server)} 클라이언트가 구버전입니다.\n\n" +
                $"현재 버전: v{currentVersion}\n" +
                $"최신 버전: v{latestVersion}\n\n" +
                $"패치 설정 화면으로 이동할까요?",
            PrimaryButtonText = "이동",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowManagedAsync();
        if (result == ContentDialogResult.Primary)
        {
            window.NavigateToSettingPage(
                SettingSection.GamePatch,
                new GamePatchSettingNavigationParameter { Server = server });
        }

        return true;
    }

    /// <summary>
    /// 게임 실행 전에 선택한 계정의 비밀번호 상태를 확인합니다.
    /// </summary>
    private async Task<bool> EnsureAccountReadyForLaunchAsync(MainWindow window, GameServer server, string accountId)
    {
        PasswordVaultHelper.PasswordVaultReadResult passwordResult = PasswordVaultHelper.TryGetPassword(accountId);
        if (!passwordResult.Success)
        {
            if (await CredentialVaultGuidanceDialog.TryShowAsync(XamlRoot, passwordResult.Exception))
                return false;

            await App.ExceptionHandler.ShowRecoverableAsync(
                new InvalidOperationException(
                    $"윈도우 자격 증명 관리자에서 계정 비밀번호를 읽지 못했습니다. AccountId='{accountId}'",
                    passwordResult.Exception),
                "StationPage.EnsureAccountReadyForLaunch");
            return false;
        }

        if (passwordResult.HasCredential)
            return true;

        Account? brokenAccount = Accounts.FirstOrDefault(account =>
            string.Equals(account.Id?.Trim(), accountId, StringComparison.OrdinalIgnoreCase));

        if (brokenAccount is null)
        {
            await ShowWarningDialogAsync("계정을 다시 선택해 주세요", "선택한 계정 정보를 찾지 못했습니다. 계정을 다시 선택해 주세요.");
            return false;
        }

        return await HandleBrokenAccountCredentialAsync(window, server, brokenAccount);
    }

    /// <summary>
    /// 계정은 남아 있지만 비밀번호가 없는 깨진 계정을 정리하고 설정 페이지로 유도합니다.
    /// </summary>
    private async Task<bool> HandleBrokenAccountCredentialAsync(MainWindow window, GameServer server, Account brokenAccount)
    {
        List<Account> nextAccounts = Accounts
            .Where(account => !string.Equals(account.Id?.Trim(), brokenAccount.Id?.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        (IList<Account> savedAccounts, AppDataManager.AppDataOperationResult saveResult) =
            await AppDataManager.SaveAccountsWithCredentialsAsync(nextAccounts);
        if (!saveResult.Success)
        {
            await AppDataOperationDialog.ShowFailureAsync(
                XamlRoot,
                "계정 정리 실패",
                "비밀번호가 없는 깨진 계정을 정리하지 못했습니다.",
                saveResult);
            return false;
        }

        Accounts = savedAccounts;
        RefreshStateFromStorage();
        RefreshClientAvailabilityState();
        Bindings.Update();

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "계정 다시 등록 필요",
            Content =
                $"계정 '{brokenAccount.DisplayNickname}'의 저장된 비밀번호를 찾지 못했습니다.\n" +
                "이 계정은 깨진 상태로 판단되어 목록에서 제거했습니다.\n\n" +
                "계정 설정 페이지에서 다시 등록해 주세요.",
            PrimaryButtonText = "계정 설정",
            CloseButtonText = "확인",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowManagedAsync() == ContentDialogResult.Primary)
            window.NavigateToSettingPage(SettingSection.Account);

        return false;
    }

    /// <summary>
    /// 설치된 클라이언트의 vsn.dat을 읽지 못했을 때 경로 확인을 안내합니다.
    /// </summary>
    private async Task ShowClientVersionCheckRequiredDialogAsync(
        MainWindow window,
        GameServer server,
        ClientVersionReadResult result)
    {
        string message = result.Exception is FileNotFoundException
            ? $"{GameServerHelper.GetServerDisplayName(server)} 클라이언트의 버전 파일(vsn.dat)을 찾지 못했습니다.\n설치 경로가 올바른지 확인해 주세요."
            : $"{GameServerHelper.GetServerDisplayName(server)} 클라이언트 버전을 읽지 못했습니다.\n설치 경로와 클라이언트 파일 상태를 확인해 주세요.";

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "설치 경로 확인 필요",
            Content = message,
            PrimaryButtonText = "경로 설정",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowManagedAsync() == ContentDialogResult.Primary)
        {
            window.NavigateToSettingPage(
                SettingSection.InstallPath,
                new GameServerSettingNavigationParameter { Server = server });
        }
    }

    /// <summary>
    /// 최신 패치 버전을 확인하지 못했을 때 사용자에게 계속 실행 여부를 묻습니다.
    /// </summary>
    private async Task<bool> ShowLatestVersionCheckFailedDialogAsync(GameServer server)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "최신 버전 확인 실패",
            Content =
                $"{GameServerHelper.GetServerDisplayName(server)} 최신 패치 정보를 확인하지 못했습니다.\n" +
                "패치가 필요한 상태인지 판단할 수 없습니다.\n\n" +
                "그래도 게임을 실행할까요?",
            PrimaryButtonText = "그래도 실행",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Close
        };

        return await dialog.ShowManagedAsync() == ContentDialogResult.Primary;
    }

    /// <summary>
    /// 실행 제한 사유를 사용자에게 경고 다이얼로그로 알립니다.
    /// </summary>
    private async Task ShowWarningDialogAsync(string title, string content)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = "확인",
            DefaultButton = ContentDialogButton.Primary
        };

        await dialog.ShowManagedAsync();
    }

    /// <summary>
    /// 초기 상태 로드 중 실패한 계정/프리셋 데이터를 사용자 정책에 맞춰 처리합니다.
    /// </summary>
    private async Task HandleInitialStorageLoadIssuesAsync()
    {
        if (!_accountsLoadResult.Success)
            await HandleStationDataLoadFailureAsync("계정 목록", _accountsLoadResult);

        if (!_presetLoadResult.Success && !IsSameAppDataFailure(_accountsLoadResult, _presetLoadResult))
            await HandleStationDataLoadFailureAsync("프리셋 목록", _presetLoadResult);
    }

    /// <summary>
    /// StationPage 초기 데이터 로드 실패를 경고 또는 초기화 선택지로 처리합니다.
    /// </summary>
    private async Task HandleStationDataLoadFailureAsync(string dataName, AppDataManager.AppDataOperationResult result)
    {
        if (!IsFileBackedInitialLoadFailure(result))
        {
            await App.ExceptionHandler.ShowRecoverableAsync(
                BuildAppDataLoadException(dataName, result),
                "StationPage.HandleInitialStorageLoadIssuesAsync");
            return;
        }

        if (result.ErrorKind == AppDataManager.AppDataErrorKind.Serialization &&
            await ShowResetCorruptedDataDialogAsync(dataName, result.Target))
        {
            await ResetStationDataAsync(result.Target);
            return;
        }

        await AppDataOperationDialog.ShowFailureAsync(
            XamlRoot,
            $"{dataName} 불러오기 실패",
            $"저장된 {dataName}을(를) 불러오지 못했습니다.",
            result);
    }

    /// <summary>
    /// 손상된 로컬 데이터 파일을 초기화할지 사용자에게 묻습니다.
    /// </summary>
    private async Task<bool> ShowResetCorruptedDataDialogAsync(string dataName, string target)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = $"{dataName} 초기화",
            Content =
                $"{dataName} 파일({target}) 형식이 손상되어 읽을 수 없습니다.\n\n" +
                "이 데이터를 초기화하고 다시 불러올까요?",
            PrimaryButtonText = "초기화",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Close
        };

        return await dialog.ShowManagedAsync() == ContentDialogResult.Primary;
    }

    /// <summary>
    /// 손상된 StationPage 관련 로컬 데이터를 기본값으로 다시 저장하고 상태를 새로 읽습니다.
    /// </summary>
    private async Task ResetStationDataAsync(string target)
    {
        AppDataManager.AppDataOperationResult resetResult = target switch
        {
            "accounts.json" => await AppDataManager.SaveAccountsAsync([]),
            "preset-list.json" => await AppDataManager.SavePresetListAsync(new PresetList()),
            _ => AppDataManager.AppDataOperationResult.Ok("UnsupportedResetTarget", target)
        };

        if (!resetResult.Success)
        {
            await AppDataOperationDialog.ShowFailureAsync(
                XamlRoot,
                "초기화 실패",
                $"손상된 데이터 파일({target})을 초기화하지 못했습니다.",
                resetResult);
            return;
        }

        RefreshStateFromStorage();
    }

    /// <summary>
    /// 같은 근본 원인으로 보고된 AppData 실패를 중복 안내하지 않도록 비교합니다.
    /// </summary>
    private static bool IsSameAppDataFailure(
        AppDataManager.AppDataOperationResult left,
        AppDataManager.AppDataOperationResult right)
    {
        if (left.Success || right.Success)
            return false;

        return string.Equals(left.Target, right.Target, StringComparison.OrdinalIgnoreCase) &&
            left.ErrorKind == right.ErrorKind &&
            string.Equals(left.Exception?.Message, right.Exception?.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// StationPage 초기 로드에서 파일 손상 또는 접근 실패처럼 사용자가 조치할 수 있는 항목만
    /// 간단 경고 대상으로 간주합니다.
    /// </summary>
    private static bool IsFileBackedInitialLoadFailure(AppDataManager.AppDataOperationResult result)
        => result.ErrorKind is AppDataManager.AppDataErrorKind.Storage or AppDataManager.AppDataErrorKind.Serialization;

    /// <summary>
    /// AppData 로드 실패를 기본 상세 예외 창으로 전달할 예외 객체로 정리합니다.
    /// </summary>
    private static Exception BuildAppDataLoadException(string dataName, AppDataManager.AppDataOperationResult result)
    {
        return new InvalidOperationException(
            $"{dataName}을(를) 불러오지 못했습니다. Operation={result.Operation}, Target={result.Target}, ErrorKind={result.ErrorKind}",
            result.Exception);
    }

    /// <summary>
    /// 현재 선택된 서버의 메인 클라이언트 버전과 최신 버전 표시를 갱신합니다.
    /// </summary>
    private async Task UpdateServer()
    {
        GameServer server = AppDataManager.SelectedServer = (GameServer)SelectedServerIndex;
        string clientInstallPath = GetClientInstallPath(server, clientIndex: 0);
        bool hasValidInstallPath = GameClientHelper.IsValidInstallPath(server, clientInstallPath, out _);
        ClientVersionReadResult currentVersionResult = hasValidInstallPath
            ? PatchManager.TryGetCurrentClientVersion(clientInstallPath)
            : new ClientVersionReadResult(
                false,
                null,
                clientInstallPath?.Trim() ?? string.Empty,
                ClientVersionReadFailureStage.ResolveVsnPath,
                null);
        int currentVersion = currentVersionResult.Success ? currentVersionResult.Version ?? 0 : 0;

        int latestVersion = 0;
        bool latestVersionAvailable = false;
        try
        {
            latestVersion = await PatchManager.GetLatestServerVersionAsync(server);
            latestVersionAvailable = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StationPage] UpdateServer latest version check failed. Server: {server}, Error: {ex}");
        }

        string currentStr = !hasValidInstallPath
            ? "확인 필요"
            : currentVersionResult.Success && currentVersion > 0
            ? $"v{currentVersion}"
            : currentVersionResult.Exception is FileNotFoundException
                ? "확인 필요"
                : "확인 실패";
        string latestStr = latestVersionAvailable && latestVersion > 0 ? $"v{latestVersion}" : "확인 실패";
        TextBlock_Version.Text = $"설치 버전: {currentStr} | 최신 버전: {latestStr}";
        bool canCompareVersions = currentVersionResult.Success && currentVersion > 0 && latestVersionAvailable && latestVersion > 0;
        bool needsPatch = canCompareVersions && currentVersion < latestVersion;
        Button_RefreshVersion.Visibility = needsPatch ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        Button_Patch.Visibility = needsPatch ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    private async void ComboBox_Server_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await UpdateServer();
    }

    private async void Button_RefreshVersion_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await UpdateServer();
    }

    private void Button_Patch_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (App.CurrentWindow is MainWindow window)
        {
            window.NavigateToSettingPage(
                SettingSection.GamePatch,
                new GamePatchSettingNavigationParameter { Server = (GameServer)SelectedServerIndex });
        }
    }

    /// <summary>
    /// 현재 패키지 버전 문자열을 생성합니다.
    /// </summary>
    private static string CreateCurrentVersionText()
    {
        PackageVersion version = Package.Current.Id.Version;
        return $"현재 버전: v{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }

    /// <summary>
    /// MainWindow의 Store 업데이트 상태가 바뀌면 버튼 표시를 갱신합니다.
    /// </summary>
    private void MainWindow_StoreUpdateStateChanged(object? sender, EventArgs e)
    {
        if (sender is MainWindow window)
            SyncStoreUpdateState(window);
    }

    /// <summary>
    /// MainWindow가 관리하는 Store 업데이트 상태를 StationPage 표시 속성에 반영합니다.
    /// </summary>
    private void SyncStoreUpdateState(MainWindow window)
    {
        CurrentAppVersionText = window.CurrentAppVersionText;
        StoreUpdateButtonVisibility = window.HasAvailableStoreUpdate ? Visibility.Visible : Visibility.Collapsed;
        StoreUpdateButtonEnabled = window.StoreUpdateButtonEnabled;
        OnPropertyChanged(nameof(CurrentAppVersionText));
        OnPropertyChanged(nameof(StoreUpdateButtonVisibility));
        OnPropertyChanged(nameof(StoreUpdateButtonEnabled));
    }

    /// <summary>
    /// 사용자가 직접 MainWindow의 Store 업데이트 설치 대화 상자를 엽니다.
    /// </summary>
    private async void Button_UpdateFromStore_Click(object sender, RoutedEventArgs e)
    {
        if (App.CurrentWindow is not MainWindow window)
            return;

        SyncStoreUpdateState(window);
        await window.ShowStoreUpdateDialogAsync();
        SyncStoreUpdateState(window);
    }

    /// <summary>
    /// 현재 선택된 서버에 대응하는 클라이언트 경로 설정 화면으로 이동합니다.
    /// </summary>
    private void Button_ClientSetting_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (App.CurrentWindow is not MainWindow window)
            return;

        window.NavigateToSettingPage(
            SettingSection.InstallPath,
            new GameServerSettingNavigationParameter { Server = (GameServer)SelectedServerIndex });
    }

    /// <summary>
    /// 서버와 슬롯 번호에 대응하는 실제 클라이언트 설치 경로를 반환합니다.
    /// </summary>
    private static string GetClientInstallPath(GameServer server, int clientIndex)
    {
        ClientSettings settings = AppDataManager.LoadServerClientSettings(server);
        return clientIndex switch
        {
            0 => settings.InstallPath,
            1 => settings.Client2Path,
            2 => settings.Client3Path,
            _ => throw new ArgumentOutOfRangeException(nameof(clientIndex), clientIndex, null),
        };
    }

    public bool Client1ButtonEnabled => IsClientButtonEnabled(0);
    public bool Client2ButtonEnabled => IsClientButtonEnabled(1);
    public bool Client3ButtonEnabled => IsClientButtonEnabled(2);

    public string Client1StatusText => GetClientStatusText(0);
    public string Client2StatusText => GetClientStatusText(1);
    public string Client3StatusText => GetClientStatusText(2);
    public Visibility Client1CancelVisibility => GetClientCancelVisibility(0);
    public Visibility Client2CancelVisibility => GetClientCancelVisibility(1);
    public Visibility Client3CancelVisibility => GetClientCancelVisibility(2);

    /// <summary>
    /// 버튼 상태 갱신을 위해 GameStarter 변경 이벤트를 구독합니다.
    /// </summary>
    private void AttachGameStarter(GameStarter gameStarter)
    {
        if (ReferenceEquals(_gameStarter, gameStarter))
            return;

        DetachGameStarter();
        _gameStarter = gameStarter;
        _gameStarter.PropertyChanged += OnGameStarterPropertyChanged;
        RefreshClientAvailabilityState();
    }

    /// <summary>
    /// 페이지 이탈 시 GameStarter 이벤트 구독을 해제합니다.
    /// </summary>
    private void DetachGameStarter()
    {
        if (_gameStarter is null)
            return;

        _gameStarter.PropertyChanged -= OnGameStarterPropertyChanged;
        _gameStarter = null;
    }

    private void OnGameStarterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            RefreshClientAvailabilityState();
            return;
        }

        _ = DispatcherQueue.TryEnqueue(RefreshClientAvailabilityState);
    }

    /// <summary>
    /// 실행 버튼의 활성화 여부와 상태 문구를 다시 계산하도록 알립니다.
    /// </summary>
    private void RefreshClientAvailabilityState()
    {
        RecomputeClientLaunchAvailability();
        OnPropertyChanged(nameof(Client1ButtonEnabled));
        OnPropertyChanged(nameof(Client2ButtonEnabled));
        OnPropertyChanged(nameof(Client3ButtonEnabled));
        OnPropertyChanged(nameof(Client1StatusText));
        OnPropertyChanged(nameof(Client2StatusText));
        OnPropertyChanged(nameof(Client3StatusText));
        OnPropertyChanged(nameof(Client1CancelVisibility));
        OnPropertyChanged(nameof(Client2CancelVisibility));
        OnPropertyChanged(nameof(Client3CancelVisibility));
    }

    /// <summary>
    /// 현재 선택한 서버의 경로 유효성과, 버튼 번호 기준 전역 런타임 상태를 함께 반영한 실행 가능 상태를 계산합니다.
    /// </summary>
    private ClientLaunchAvailability GetClientLaunchAvailability(int clientIndex)
    {
        GameServer server = (GameServer)SelectedServerIndex;
        GameClientState runtimeState = _gameStarter?.GetClientState(server, clientIndex, null) ?? GameClientState.Available;
        if (runtimeState == GameClientState.RetryCooldown)
            return new ClientLaunchAvailability(ClientLaunchStatus.RetryCooldown);

        if (runtimeState == GameClientState.Starting)
            return new ClientLaunchAvailability(ClientLaunchStatus.Starting);

        if (runtimeState == GameClientState.Running)
            return new ClientLaunchAvailability(ClientLaunchStatus.Running);

        ClientSettings settings = AppDataManager.LoadServerClientSettings(server);

        if (clientIndex > 0 && !IsMultiClientSlotEnabled(settings, clientIndex))
            return new ClientLaunchAvailability(ClientLaunchStatus.MultiClientDisabled);

        string mainInstallPath = settings.InstallPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(mainInstallPath))
            return new ClientLaunchAvailability(ClientLaunchStatus.InstallPathMissing);

        if (!GameClientHelper.IsValidInstallPath(server, mainInstallPath, out string mainReason))
            return new ClientLaunchAvailability(ClientLaunchStatus.InstallPathInvalid, mainReason);

        if (clientIndex == 0)
            return new ClientLaunchAvailability(ClientLaunchStatus.Available);

        string clonePath = clientIndex switch
        {
            1 => settings.Client2Path,
            2 => settings.Client3Path,
            _ => throw new ArgumentOutOfRangeException(nameof(clientIndex), clientIndex, null),
        };

        if (!GameClientHelper.IsValidCloneInstallPath(server, clonePath, out string cloneReason))
            return new ClientLaunchAvailability(ClientLaunchStatus.MultiClientSetupRequired, cloneReason);

        ClientVersionReadResult mainVersionResult = PatchManager.TryGetCurrentClientVersion(mainInstallPath);
        if (!mainVersionResult.Success || (mainVersionResult.Version ?? 0) <= 0)
        {
            return new ClientLaunchAvailability(
                ClientLaunchStatus.MultiClientSetupRequired,
                BuildVersionCheckFailureDetail("메인 클라이언트", mainVersionResult));
        }

        ClientVersionReadResult cloneVersionResult = PatchManager.TryGetCurrentClientVersion(clonePath);
        if (!cloneVersionResult.Success || (cloneVersionResult.Version ?? 0) <= 0)
        {
            return new ClientLaunchAvailability(
                ClientLaunchStatus.MultiClientSetupRequired,
                BuildVersionCheckFailureDetail("복제 클라이언트", cloneVersionResult));
        }

        int mainVersion = mainVersionResult.Version!.Value;
        int cloneVersion = cloneVersionResult.Version!.Value;
        if (mainVersion != cloneVersion)
        {
            string detail = $"메인 버전(v{mainVersion})과 복제 버전(v{cloneVersion})이 다릅니다.";
            return new ClientLaunchAvailability(ClientLaunchStatus.MultiClientSetupRequired, detail);
        }

        return new ClientLaunchAvailability(ClientLaunchStatus.Available);
    }

    /// <summary>
    /// 버튼 하단에 표시할 상태 문구를 반환합니다.
    /// </summary>
    private string GetClientStatusText(int clientIndex)
        => _clientLaunchAvailability[clientIndex].Status switch
        {
            ClientLaunchStatus.Starting => "켜는 중",
            ClientLaunchStatus.Running => "실행 중",
            ClientLaunchStatus.RetryCooldown => "잠시 후 재시도",
            ClientLaunchStatus.MultiClientDisabled => "다클라 미사용",
            ClientLaunchStatus.InstallPathMissing => "경로 미설정",
            ClientLaunchStatus.InstallPathInvalid => "잘못된 경로",
            ClientLaunchStatus.MultiClientSetupRequired => "다클 생성 필요",
            _ => "실행 가능"
        };

    /// <summary>
    /// 현재 슬롯이 켜지는 중일 때만 취소 버튼을 노출합니다.
    /// </summary>
    private Visibility GetClientCancelVisibility(int clientIndex)
        => _clientLaunchAvailability[clientIndex].Status == ClientLaunchStatus.Starting
            ? Visibility.Visible
            : Visibility.Collapsed;

    /// <summary>
    /// 현재 상태를 기준으로 해당 실행 버튼을 눌러볼 수 있는지 여부를 반환합니다.
    /// </summary>
    private bool IsClientButtonEnabled(int clientIndex)
        => !HasAnyClientStarting()
            && _clientLaunchAvailability[clientIndex].Status is not ClientLaunchStatus.Starting
            and not ClientLaunchStatus.RetryCooldown
            and not ClientLaunchStatus.Running
            and not ClientLaunchStatus.MultiClientDisabled;

    /// <summary>
    /// 현재 화면에서 하나라도 클라이언트가 켜지는 중이면 추가 실행을 막아야 하는지 판단합니다.
    /// </summary>
    private bool HasAnyClientStarting()
    {
        for (int clientIndex = 0; clientIndex < _clientLaunchAvailability.Length; clientIndex++)
        {
            if (_clientLaunchAvailability[clientIndex].Status == ClientLaunchStatus.Starting)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 사용자가 강제로 실행 준비를 취소하면 WebView와 GameStarter 상태를 함께 초기화합니다.
    /// </summary>
    private void CancelClientStart(int clientIndex)
    {
        if (App.CurrentWindow is not MainWindow window || window.WebViewManager is null)
            return;

        window.WebViewManager.CancelLaunchAttempt((GameServer)SelectedServerIndex, clientIndex);
        RefreshClientAvailabilityState();
    }

    private void Button_Client1_Cancel_Click(object sender, RoutedEventArgs e)
        => CancelClientStart(0);

    private void Button_Client2_Cancel_Click(object sender, RoutedEventArgs e)
        => CancelClientStart(1);

    private void Button_Client3_Cancel_Click(object sender, RoutedEventArgs e)
        => CancelClientStart(2);

    /// <summary>
    /// 2클라 또는 3클라 슬롯이 설정상 사용 대상인지 판별합니다.
    /// </summary>
    private static bool IsMultiClientSlotEnabled(ClientSettings settings, int clientIndex)
        => settings.UseMultiClient && clientIndex switch
        {
            1 => settings.UseClient2,
            2 => settings.UseClient3,
            _ => true
        };

    /// <summary>
    /// 현재 서버의 세 슬롯 상태를 한 번에 다시 계산합니다.
    /// </summary>
    private void RecomputeClientLaunchAvailability()
    {
        for (int clientIndex = 0; clientIndex < _clientLaunchAvailability.Length; clientIndex++)
            _clientLaunchAvailability[clientIndex] = GetClientLaunchAvailability(clientIndex);
    }

    /// <summary>
    /// 선택한 슬롯에서 다클라 생성이 필요할 때 해당 복제 클라이언트 하나만 생성합니다.
    /// </summary>
    private async Task<MultiClientCreationAttemptResult> TryCreateMissingMultiClientAsync(GameServer server, int clientIndex)
    {
        if (clientIndex == 0)
            return new(false, "1클라는 메인 클라이언트이므로 별도 생성이 필요하지 않습니다.");

        ClientSettings settings = AppDataManager.LoadServerClientSettings(server);
        string installPath = settings.InstallPath?.Trim() ?? string.Empty;
        if (!GameClientHelper.IsValidInstallPath(server, installPath, out string reason))
            return new(false, $"메인 클라이언트 경로가 유효하지 않아 다클라를 만들 수 없습니다. {reason}");

        ClientVersionReadResult currentVersionResult = PatchManager.TryGetCurrentClientVersion(installPath);
        if (!currentVersionResult.Success || (currentVersionResult.Version ?? 0) <= 0)
            return new(false, BuildVersionCheckFailureDetail("메인 클라이언트", currentVersionResult), currentVersionResult.Exception);

        int currentClientVersion = currentVersionResult.Version!.Value;

        GameClientHelper.MultiClientLayoutPolicy layoutPolicy =
            currentClientVersion >= GameClientHelper.MultiClientLayoutBoundaryVersion
                ? GameClientHelper.MultiClientLayoutPolicy.V34100OrLater
                : GameClientHelper.MultiClientLayoutPolicy.Legacy;

        string clientLabel = clientIndex == 1 ? "2클라" : "3클라";
        if (!IsMultiClientSlotEnabled(settings, clientIndex))
            return new(false, $"{clientLabel} 사용이 현재 서버 설정에서 비활성화되어 있습니다.");

        string targetPath = clientIndex switch
        {
            1 => settings.Client2Path,
            2 => settings.Client3Path,
            _ => string.Empty
        };

        DirectoryWriteProbeResult targetProbeResult =
            PathWriteProbe.TryProbeDirectoryWriteAccess(targetPath);
        if (!await PathPermissionDialog.ConfirmContinueWhenPermissionMissingAsync(XamlRoot, targetProbeResult))
            return new(false, $"{clientLabel} 경로 권한 확인 단계에서 작업을 중단했습니다.", targetProbeResult.Exception);

        CreateSymbolMultiClientResult createResult = GameClientHelper.TryCreateSymbolMultiClient(
            new CreateSymbolMultiClientArgs
            {
                InstallPath = installPath,
                DestPath2 = clientIndex == 1 ? settings.Client2Path : string.Empty,
                DestPath3 = clientIndex == 2 ? settings.Client3Path : string.Empty,
                OverwriteConfig = settings.OverwriteMultiClientConfig,
                LayoutPolicy = layoutPolicy
            });

        if (!createResult.Success)
            return new(false, createResult.Reason, createResult.Exception);

        AppDataManager.SaveServerClientSettings(server, settings);
        return new(true, string.Empty);
    }

    /// <summary>
    /// 클라이언트 버전 파일을 읽지 못했을 때 실행 가능 상태 문구에 사용할 안내를 만듭니다.
    /// </summary>
    private static string BuildVersionCheckFailureDetail(string clientLabel, ClientVersionReadResult result)
    {
        if (result.Exception is FileNotFoundException)
            return $"{clientLabel}의 버전 파일(vsn.dat)을 찾지 못했습니다. 설치 경로를 확인해 주세요.";

        return result.Exception is null
            ? $"{clientLabel}의 버전을 확인하지 못했습니다."
            : $"{clientLabel}의 버전을 확인하지 못했습니다. {result.Exception.Message}";
    }

    #region EventFlipView
    private readonly DispatcherTimer _eventFlipTimer = new();
    private CancellationTokenSource? _eventLoadCts;
    private CancellationTokenSource? _homepageNoticeLoadCts;
    private CancellationTokenSource? _gersangStationNoticeLoadCts;
    private DateTimeOffset? _lastEventLoadedAt;
    private DateTimeOffset? _lastHomepageNoticeLoadedAt;
    private DateTimeOffset? _lastGersangStationNoticeLoadedAt;
    private static readonly TimeSpan EventRefreshInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan EventFlipInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan EventPeriodOverlayShowDelay = TimeSpan.FromMilliseconds(320);
    // Two quick flashes followed by a longer rest keeps the alert noticeable
    // without crossing the 3 flashes/sec accessibility threshold.
    private static readonly (bool Highlighted, TimeSpan Duration)[] EventUrgencyBlinkPhases =
    [
        (true, TimeSpan.FromMilliseconds(240)),
        (false, TimeSpan.FromMilliseconds(160)),
        (true, TimeSpan.FromMilliseconds(240)),
        (false, TimeSpan.FromMilliseconds(2360))
    ];
    private int _eventPeriodOverlayVersion;
    private readonly DispatcherTimer _eventUrgencyTimer = new();
    private bool _isEventUrgencyHighlighted;
    private int _eventUrgencyPhaseIndex;
    public IReadOnlyList<StationEventThumbnailItem> EventItems { get; private set; } = [];
    public IReadOnlyList<StationHomepageNoticeItem> HomepageNoticeItems { get; private set; } = [];
    public IReadOnlyList<StationHomepageNoticeItem> GersangStationNoticeItems { get; private set; } = [];
    private const double ThumbnailAspectWidth = 1920.0;
    private const double ThumbnailAspectHeight = 610.0;
    public Visibility EventPeriodOverlayVisibility { get; private set; } = Visibility.Collapsed;
    public Visibility HomepageNoticeEmptyVisibility => HomepageNoticeItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility GersangStationNoticeEmptyVisibility => GersangStationNoticeItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public string HomepageNoticeStatusText { get; private set; } = "공지사항이 없습니다.";
    public string GersangStationNoticeStatusText { get; private set; } = "공지사항이 없습니다.";
    private string _eventHeaderWhenEmpty = "진행 중인 이벤트가 없습니다";

    public string EventHeaderText
    {
        get
        {
            if (EventItems.Count == 0)
                return _eventHeaderWhenEmpty;

            int selectedIndex = FlipView_Event?.SelectedIndex ?? -1;
            int currentPage = selectedIndex >= 0 && selectedIndex < EventItems.Count
                ? selectedIndex + 1
                : 1;

            return $"진행 중 이벤트 {currentPage}/{EventItems.Count}";
        }
    }

    public string CurrentEventPeriodOverlayText
    {
        get
        {
            int selectedIndex = FlipView_Event?.SelectedIndex ?? -1;
            if (selectedIndex < 0 || selectedIndex >= EventItems.Count)
            {
                return string.Empty;
            }

            return EventItems[selectedIndex].PeriodOverlayText;
        }
    }

    public Visibility EventUrgencyTextVisibility
        => IsCurrentEventDeadlineSoon() ? Visibility.Visible : Visibility.Collapsed;
    
    /// <summary>
    /// 이벤트 목록을 주기적으로 다시 읽어오되, 최근 결과가 있으면 재사용합니다.
    /// </summary>
    private async Task LoadEventsAsync(bool forceRefresh = false)
    {
        if (!forceRefresh
            && EventItems.Count > 0
            && _lastEventLoadedAt is DateTimeOffset loadedAt
            && DateTimeOffset.UtcNow - loadedAt < EventRefreshInterval)
        {
            return;
        }

        CancelEventLoad();
        CancellationTokenSource cts = new();
        _eventLoadCts = cts;

        try
        {
            IReadOnlyList<GameEventInfo> events = await GameHomepageCrawler.GetAllEventsAsync(
                HttpClientProvider.Http,
                new GameEventLoadOptions(IgnorePlaceholderEndDate: true),
                cts.Token);

            EventItems = events
                .Where(item => !string.IsNullOrWhiteSpace(item.ThumbnailUrl))
                .Select(CreateEventThumbnailItem)
                .ToList();

            _eventHeaderWhenEmpty = "진행 중인 이벤트가 없습니다";
            FlipView_Event.SelectedIndex = EventItems.Count > 0 ? 0 : -1;
            _lastEventLoadedAt = DateTimeOffset.UtcNow;
            OnPropertyChanged(nameof(EventItems));
            OnPropertyChanged(nameof(EventHeaderText));
            OnPropertyChanged(nameof(CurrentEventPeriodOverlayText));
            OnPropertyChanged(nameof(EventUrgencyTextVisibility));
            EventPeriodOverlayVisibility = EventItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            OnPropertyChanged(nameof(EventPeriodOverlayVisibility));
            UpdateEventUrgencyTimerState();
            UpdateEventFlipTimerState();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            EventItems = [];
            _eventHeaderWhenEmpty = "이벤트를 불러올 수 없습니다.";
            FlipView_Event.SelectedIndex = -1;
            EventPeriodOverlayVisibility = Visibility.Collapsed;
            OnPropertyChanged(nameof(EventItems));
            OnPropertyChanged(nameof(EventHeaderText));
            OnPropertyChanged(nameof(CurrentEventPeriodOverlayText));
            OnPropertyChanged(nameof(EventPeriodOverlayVisibility));
            OnPropertyChanged(nameof(EventUrgencyTextVisibility));
            UpdateEventUrgencyTimerState();
            UpdateEventFlipTimerState();
            Debug.WriteLine($"[StationPage] 이벤트 목록 로드 실패: {ex}");
        }
        finally
        {
            cts.Dispose();
            if (ReferenceEquals(_eventLoadCts, cts))
                _eventLoadCts = null;
        }
    }

    /// <summary>
    /// 진행 중인 이벤트 목록 요청이 있으면 취소합니다.
    /// </summary>
    private void CancelEventLoad()
    {
        _eventLoadCts?.Cancel();
    }

    /// <summary>
    /// 메인 홈페이지 공지사항 목록을 주기적으로 다시 읽어오되, 최근 결과가 있으면 재사용합니다.
    /// </summary>
    private async Task LoadHomepageNoticesAsync(bool forceRefresh = false)
    {
        if (!forceRefresh
            && HomepageNoticeItems.Count > 0
            && _lastHomepageNoticeLoadedAt is DateTimeOffset loadedAt
            && DateTimeOffset.UtcNow - loadedAt < EventRefreshInterval)
        {
            return;
        }

        CancelHomepageNoticeLoad();
        CancellationTokenSource cts = new();
        _homepageNoticeLoadCts = cts;

        try
        {
            IReadOnlyList<GameHomepageNoticeInfo> notices = await GameHomepageCrawler.GetHomepageNoticesAsync(
                HttpClientProvider.Http,
                cts.Token);

            HomepageNoticeItems = notices
                .Take(5)
                .Select(CreateHomepageNoticeItem)
                .ToList();

            HomepageNoticeStatusText = "공지사항이 없습니다.";
            _lastHomepageNoticeLoadedAt = DateTimeOffset.UtcNow;
            OnPropertyChanged(nameof(HomepageNoticeItems));
            OnPropertyChanged(nameof(HomepageNoticeStatusText));
            OnPropertyChanged(nameof(HomepageNoticeEmptyVisibility));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            HomepageNoticeItems = [];
            HomepageNoticeStatusText = "공지사항을 불러올 수 없습니다.";
            OnPropertyChanged(nameof(HomepageNoticeItems));
            OnPropertyChanged(nameof(HomepageNoticeStatusText));
            OnPropertyChanged(nameof(HomepageNoticeEmptyVisibility));
            Debug.WriteLine($"[StationPage] 공지사항 목록 로드 실패: {ex}");
        }
        finally
        {
            cts.Dispose();
            if (ReferenceEquals(_homepageNoticeLoadCts, cts))
                _homepageNoticeLoadCts = null;
        }
    }

    /// <summary>
    /// 진행 중인 공지사항 목록 요청이 있으면 취소합니다.
    /// </summary>
    private void CancelHomepageNoticeLoad()
    {
        _homepageNoticeLoadCts?.Cancel();
    }

    /// <summary>
    /// GersangStation GitHub Discussions 공지사항을 읽어와 고정 공지와 최신 공지를 합쳐 표시합니다.
    /// </summary>
    private async Task LoadGersangStationNoticesAsync(bool forceRefresh = false)
    {
        if (!forceRefresh
            && GersangStationNoticeItems.Count > 0
            && _lastGersangStationNoticeLoadedAt is DateTimeOffset loadedAt
            && DateTimeOffset.UtcNow - loadedAt < EventRefreshInterval)
        {
            return;
        }

        CancelGersangStationNoticeLoad();
        CancellationTokenSource cts = new();
        _gersangStationNoticeLoadCts = cts;

        try
        {
            IReadOnlyList<GersangStationNoticeInfo> notices = await GersangStationNoticeCrawler.GetNoticesAsync(
                HttpClientProvider.Http,
                cts.Token);

            GersangStationNoticeItems = notices
                .Select(CreateGersangStationNoticeItem)
                .ToList();
            GersangStationNoticeStatusText = "공지사항이 없습니다.";
            _lastGersangStationNoticeLoadedAt = DateTimeOffset.UtcNow;
            OnPropertyChanged(nameof(GersangStationNoticeItems));
            OnPropertyChanged(nameof(GersangStationNoticeStatusText));
            OnPropertyChanged(nameof(GersangStationNoticeEmptyVisibility));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            GersangStationNoticeItems = [];
            GersangStationNoticeStatusText = "공지사항을 불러올 수 없습니다.";
            OnPropertyChanged(nameof(GersangStationNoticeItems));
            OnPropertyChanged(nameof(GersangStationNoticeStatusText));
            OnPropertyChanged(nameof(GersangStationNoticeEmptyVisibility));
            Debug.WriteLine($"[StationPage] 거상스테이션 공지사항 로드 실패: {ex}");
        }
        finally
        {
            cts.Dispose();
            if (ReferenceEquals(_gersangStationNoticeLoadCts, cts))
                _gersangStationNoticeLoadCts = null;
        }
    }

    /// <summary>
    /// 진행 중인 GersangStation 공지사항 요청이 있으면 취소합니다.
    /// </summary>
    private void CancelGersangStationNoticeLoad()
    {
        _gersangStationNoticeLoadCts?.Cancel();
    }

    /// <summary>
    /// 이벤트 개수에 따라 자동 넘김 타이머를 시작하거나 중지합니다.
    /// </summary>
    private void UpdateEventFlipTimerState()
    {
        if (EventItems.Count > 1)
        {
            if (FlipView_Event.SelectedIndex < 0 || FlipView_Event.SelectedIndex >= EventItems.Count)
            {
                FlipView_Event.SelectedIndex = 0;
            }

            ResetEventFlipTimer();
            return;
        }

        StopEventFlipTimer();
    }

    /// <summary>
    /// 이벤트 자동 넘김 타이머를 중지합니다.
    /// </summary>
    private void StopEventFlipTimer()
    {
        if (_eventFlipTimer.IsEnabled)
        {
            _eventFlipTimer.Stop();
        }
    }

    /// <summary>
    /// 이벤트 자동 넘김 타이머를 다시 5초부터 시작합니다.
    /// </summary>
    private void ResetEventFlipTimer()
    {
        if (EventItems.Count <= 1)
        {
            StopEventFlipTimer();
            return;
        }

        _eventFlipTimer.Stop();
        _eventFlipTimer.Start();
    }

    /// <summary>
    /// 이벤트 기간 오버레이 표시를 즉시 숨깁니다.
    /// </summary>
    private void HideEventPeriodOverlay()
    {
        EventPeriodOverlayVisibility = Visibility.Collapsed;
        OnPropertyChanged(nameof(EventPeriodOverlayVisibility));
        OnPropertyChanged(nameof(EventUrgencyTextVisibility));
    }

    /// <summary>
    /// 현재 선택된 이벤트 전환이 끝날 시점에 기간 오버레이를 다시 표시합니다.
    /// </summary>
    private async void ShowEventPeriodOverlayWhenReady()
    {
        int version = ++_eventPeriodOverlayVersion;
        await Task.Delay(EventPeriodOverlayShowDelay);

        if (version != _eventPeriodOverlayVersion)
            return;

        if (EventItems.Count > 0)
        {
            EventPeriodOverlayVisibility = Visibility.Visible;
            OnPropertyChanged(nameof(CurrentEventPeriodOverlayText));
            OnPropertyChanged(nameof(EventPeriodOverlayVisibility));
            OnPropertyChanged(nameof(EventUrgencyTextVisibility));
            UpdateEventUrgencyTimerState();
        }
    }

    /// <summary>
    /// 5초마다 다음 이벤트로 이동하고 마지막이면 처음으로 되돌립니다.
    /// </summary>
    private void EventFlipTimer_Tick(object? sender, object e)
    {
        if (EventItems.Count <= 1)
        {
            StopEventFlipTimer();
            return;
        }

        HideEventPeriodOverlay();
        int nextIndex = FlipView_Event.SelectedIndex + 1;
        if (nextIndex >= EventItems.Count || nextIndex < 0)
        {
            nextIndex = 0;
        }

        FlipView_Event.SelectedIndex = nextIndex;
    }

    /// <summary>
    /// 사용자가 이벤트를 직접 넘겼을 때 자동 넘김 타이머를 다시 시작합니다.
    /// </summary>
    private void FlipView_Event_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        HideEventPeriodOverlay();
        OnPropertyChanged(nameof(EventHeaderText));
        OnPropertyChanged(nameof(CurrentEventPeriodOverlayText));
        OnPropertyChanged(nameof(EventUrgencyTextVisibility));
        ResetEventFlipTimer();
        ShowEventPeriodOverlayWhenReady();
    }

    /// <summary>
    /// 크롤링 모델을 StationPage 전용 썸네일 표시 모델로 변환합니다.
    /// </summary>
    private static StationEventThumbnailItem CreateEventThumbnailItem(GameEventInfo item)
    {
        int? remainingDays = item.EndDate is DateOnly endDate
            ? endDate.DayNumber - DateOnly.FromDateTime(DateTime.Now).DayNumber
            : null;

        return new(
            Title: item.Title,
            ThumbnailUrl: item.ThumbnailUrl,
            DetailUrl: item.DetailUrl,
            PeriodOverlayText: BuildEventPeriodOverlayText(item),
            RemainingDays: remainingDays);
    }

    /// <summary>
    /// 크롤링 모델을 StationPage 전용 공지 표시 모델로 변환합니다.
    /// </summary>
    private static StationHomepageNoticeItem CreateHomepageNoticeItem(GameHomepageNoticeInfo item)
        => new(
            Title: item.Title,
            DateText: item.DateText,
            Url: item.Url);

    /// <summary>
    /// GersangStation 공지 크롤링 모델을 StationPage 전용 표시 모델로 변환합니다.
    /// </summary>
    private static StationHomepageNoticeItem CreateGersangStationNoticeItem(GersangStationNoticeInfo item)
        => new(
            Title: item.Title,
            DateText: item.DateText,
            Url: item.Url);

    /// <summary>
    /// 썸네일 우측 상단 오버레이에 표시할 기간 및 남은 일수 문구를 생성합니다.
    /// </summary>
    private static string BuildEventPeriodOverlayText(GameEventInfo item)
    {
        if (item.StartDate is not DateOnly startDate || item.EndDate is not DateOnly endDate)
            return item.Period;

        int remainingDays = endDate.DayNumber - DateOnly.FromDateTime(DateTime.Now).DayNumber;
        string remainingText = remainingDays >= 0
            ? $"D-{remainingDays}"
            : "종료";

        return $"{startDate:yyyy.MM.dd} ~ {endDate:yyyy.MM.dd} / {remainingText}";
    }

    /// <summary>
    /// 현재 선택된 이벤트 항목을 반환합니다.
    /// </summary>
    private StationEventThumbnailItem? GetCurrentEventItem()
    {
        int selectedIndex = FlipView_Event?.SelectedIndex ?? -1;
        if (selectedIndex < 0 || selectedIndex >= EventItems.Count)
            return null;

        return EventItems[selectedIndex];
    }

    /// <summary>
    /// 현재 선택된 이벤트가 사용자 설정 기준으로 마감 임박 상태인지 판별합니다.
    /// </summary>
    private bool IsCurrentEventDeadlineSoon()
    {
        StationEventThumbnailItem? currentItem = GetCurrentEventItem();
        int? remainingDays = currentItem?.RemainingDays;
        return remainingDays is >= 0 && remainingDays <= AppDataManager.EventUrgencyThresholdDays;
    }

    private void EventUrgencyTimer_Tick(object? sender, object e)
    {
        if (!IsCurrentEventDeadlineSoon())
        {
            StopEventUrgencyTimer(resetHighlight: true);
            return;
        }

        _eventUrgencyPhaseIndex = (_eventUrgencyPhaseIndex + 1) % EventUrgencyBlinkPhases.Length;
        ApplyEventUrgencyPhase(_eventUrgencyPhaseIndex);
    }

    /// <summary>
    /// 현재 선택된 이벤트의 마감 임박 여부에 따라 썸네일 테두리 강조 타이머를 조정합니다.
    /// </summary>
    private void UpdateEventUrgencyTimerState()
    {
        if (IsCurrentEventDeadlineSoon())
        {
            StartEventUrgencyTimer();
            return;
        }

        StopEventUrgencyTimer(resetHighlight: true);
    }

    /// <summary>
    /// 이벤트 마감 임박 강조 패턴을 처음부터 다시 시작합니다.
    /// </summary>
    private void StartEventUrgencyTimer()
    {
        _eventUrgencyTimer.Stop();
        _eventUrgencyPhaseIndex = 0;
        ApplyEventUrgencyPhase(_eventUrgencyPhaseIndex);
        _eventUrgencyTimer.Start();
    }

    /// <summary>
    /// 현재 강조 단계의 색상과 다음 틱 간격을 반영합니다.
    /// </summary>
    private void ApplyEventUrgencyPhase(int phaseIndex)
    {
        (bool highlighted, TimeSpan duration) = EventUrgencyBlinkPhases[phaseIndex];
        _eventUrgencyTimer.Interval = duration;
        SetEventUrgencyHighlight(highlighted);
    }

    /// <summary>
    /// 이벤트 썸네일 테두리 강조 상태를 적용합니다.
    /// </summary>
    private void SetEventUrgencyHighlight(bool highlighted)
    {
        if (_isEventUrgencyHighlighted == highlighted)
            return;

        _isEventUrgencyHighlighted = highlighted;
        Border_EventBorder.BorderBrush = highlighted ? new SolidColorBrush(Colors.IndianRed) : (Brush)Application.Current.Resources["ControlStrongFillColorDefaultBrush"];
    }

    /// <summary>
    /// 이벤트 마감 임박 강조 타이머를 중지하고 기본 테두리 상태로 되돌립니다.
    /// </summary>
    private void StopEventUrgencyTimer(bool resetHighlight)
    {
        if (_eventUrgencyTimer.IsEnabled)
            _eventUrgencyTimer.Stop();

        if (!resetHighlight)
            return;

        SetEventUrgencyHighlight(false);
    }

    /// <summary>
    /// 이벤트 썸네일을 누르면 외부 브라우저 대신 앱 내부 WebView 페이지로 이동합니다.
    /// </summary>
    private void EventThumbnail_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;

        if (element.Tag is not string url || string.IsNullOrWhiteSpace(url))
            return;

        if (App.CurrentWindow is not MainWindow window)
            return;

        window.NavigateToWebViewPage(url);
    }

    /// <summary>
    /// 공지사항을 누르면 앱 내부 WebView 페이지로 이동합니다.
    /// </summary>
    private void HomepageNotice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;

        if (element.Tag is not string url || string.IsNullOrWhiteSpace(url))
            return;

        if (App.CurrentWindow is not MainWindow window)
            return;

        window.NavigateToWebViewPage(url);
    }

    /// <summary>
    /// 거상스테이션 공지사항을 누르면 앱 내부 WebView 페이지로 이동합니다.
    /// </summary>
    private void GersangStationNotice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;

        if (element.Tag is not string url || string.IsNullOrWhiteSpace(url))
            return;

        if (App.CurrentWindow is not MainWindow window)
            return;

        window.NavigateToWebViewPage(url);
    }

    private void Grid_ThumbnailHost_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        double width = e.NewSize.Width;
        if (width <= 0)
        {
            return;
        }

        Grid_ThumbnailHost.Height = width * ThumbnailAspectHeight / ThumbnailAspectWidth;
    }
    #endregion EventFlipView
}
