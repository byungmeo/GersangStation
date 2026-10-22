using Core;
using Core.Models;
using GersangStation.Diagnostics;
using GersangStation.Main.Setting;
using GersangStation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
/// 사이드바 실행 경로의 다클라 생성 결과를 페이지 표시 상태와 무관하게 전달합니다.
/// </summary>
public sealed class MultiClientCreationCompletedEventArgs(
    GameServer server, int clientIndex, bool success, string message,
    Exception? exception = null, bool canceled = false) : EventArgs
{
    public GameServer Server { get; } = server;
    public int ClientIndex { get; } = clientIndex;
    public bool Success { get; } = success;
    public string Message { get; } = message;
    public Exception? Exception { get; } = exception;
    public bool Canceled { get; } = canceled;
}

/// <summary>
/// 홈 컨텐츠와 사이드바의 서버/계정 선택 및 실행 검증을 제공합니다.
/// </summary>
public sealed partial class StationPage : Page, INotifyPropertyChanged
{
    private enum ClientLaunchStatus
    {
        Available,
        Starting,
        Running,
        RetryCooldown,
        InstallPathMissing,
        InstallPathInvalid,
        MultiClientSetupRequired
    }

    private readonly record struct ClientLaunchAvailability(ClientLaunchStatus Status, string? Detail = null);

    private bool _isInitializing = true;
    private bool _isShellActive;
    private bool _isViewUnloaded;
    private MainWindow? _shellWindow;
    private GameStarter? _gameStarter;
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
            NotifyAccountSelectionAvailabilityChanged();
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
            NotifyAccountSelectionAvailabilityChanged();
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
                NotifyAccountSelectionAvailabilityChanged();
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
    public bool HasSelectedAccount1 => IsAccountAvailable(SelectedAccount1Id);
    public bool HasSelectedAccount2 => IsAccountAvailable(SelectedAccount2Id);
    public bool HasSelectedAccount3 => IsAccountAvailable(SelectedAccount3Id);
    public IReadOnlyList<string> PresetNumbers { get; } = ["프리셋1", "프리셋2", "프리셋3", "프리셋4"];
    public string CurrentAppVersionText { get; private set; } = CreateCurrentVersionText();
    public Visibility StoreUpdateButtonVisibility { get; private set; } = Visibility.Collapsed;
    public bool StoreUpdateButtonEnabled { get; private set; } = true;
    public string VersionStatusText { get; private set; } = "게임 버전을 확인하는 중입니다.";
    public Visibility ClientSettingButtonVisibility { get; private set; } = Visibility.Collapsed;
    public Visibility RefreshVersionButtonVisibility { get; private set; } = Visibility.Visible;
    public Visibility PatchButtonVisibility { get; private set; } = Visibility.Collapsed;

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
                OnPropertyChanged(nameof(HasSelectedAccount1));
                break;
            case 1:
                OnPropertyChanged(nameof(SelectedAccount2Id));
                OnPropertyChanged(nameof(HasSelectedAccount2));
                break;
            case 2:
                OnPropertyChanged(nameof(SelectedAccount3Id));
                OnPropertyChanged(nameof(HasSelectedAccount3));
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
        NotifyAccountSelectionAvailabilityChanged();
    }

    private bool IsAccountAvailable(string? accountId)
        => !string.IsNullOrWhiteSpace(accountId)
            && Accounts.Any(account => string.Equals(account.Id, accountId, StringComparison.OrdinalIgnoreCase));

    private void NotifyAccountSelectionAvailabilityChanged()
    {
        OnPropertyChanged(nameof(HasSelectedAccount1));
        OnPropertyChanged(nameof(HasSelectedAccount2));
        OnPropertyChanged(nameof(HasSelectedAccount3));
    }

    public StationPage()
    {
        RefreshStateFromStorage();
        InitializeComponent();
        EventLoad.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(HomeContentLoadState.HasSucceeded)) return;
            OnPropertyChanged(nameof(EventHeaderText));
            UpdateEventFlipTimerState();
        };
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _eventFlipTimer.Interval = EventFlipInterval;
        _eventFlipTimer.Tick += EventFlipTimer_Tick;
        _isInitializing = false;
    }

    /// <summary>
    /// 페이지가 다시 시각 트리에 연결되면 컨트롤 접근 가능 상태로 되돌립니다.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isViewUnloaded = false;
        EventBanner.IsActive = _isShellActive;
    }

    /// <summary>
    /// 페이지가 시각 트리에서 제거되면 이후 지연 UI 갱신이 해제된 XAML 컨트롤을 건드리지 않게 차단합니다.
    /// </summary>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isViewUnloaded = true;
        _eventPeriodOverlayVersion++;
        StopEventFlipTimer();
        EventBanner.IsActive = false;
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
        EventBanner.IsActive = true;
        _shellWindow = window ?? _shellWindow ?? App.CurrentWindow as MainWindow;

        if (_shellWindow is not null)
        {
            _gameStarter = _shellWindow.GameStarter;
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
        if (!_isShellActive) return;
        await Task.WhenAll(
            UpdateServer(),
            LoadEventsAsync(),
            LoadHomepageNoticesAsync(),
            LoadGersangStationNoticesAsync());
        UpdateEventFlipTimerState();
    }

    /// <summary>
    /// 앱 창 복귀 시 진행 중 이벤트 목록을 기존 캐시 정책에 맞춰 다시 동기화합니다.
    /// </summary>
    internal Task RefreshEventsAfterWindowActivationAsync()
        => LoadEventsAsync();

    /// <summary>
    /// Station 섹션이 숨겨질 때 타이머와 구독을 정리합니다.
    /// </summary>
    internal void OnShellDeactivated()
    {
        if (!_isShellActive)
            return;

        _isShellActive = false;
        _eventPeriodOverlayVersion++;
        StopEventFlipTimer();
        EventBanner.IsActive = false;
        CancelEventLoad();
        CancelHomepageNoticeLoad();
        CancelGersangStationNoticeLoad();

        if (_shellWindow is not null)
            _shellWindow.StoreUpdateStateChanged -= MainWindow_StoreUpdateStateChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<MultiClientCreationCompletedEventArgs>? MultiClientCreationCompleted;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// DevUI 스타일 사이드바에서 기존 실행 검증 경로를 재사용합니다.
    /// </summary>
    public Task ExecuteClientAsync(int clientIndex)
    {
        if (_shellWindow is null) return Task.CompletedTask;
        string accountId = clientIndex switch
        {
            0 => SelectedAccount1Id,
            1 => SelectedAccount2Id,
            2 => SelectedAccount3Id,
            _ => throw new ArgumentOutOfRangeException(nameof(clientIndex))
        };
        GameServer server = (GameServer)SelectedServerIndex;
        return _shellWindow.GameLaunchCoordinator.ToggleAsync(server, clientIndex, accountId,
            GetClientInstallPath(server, clientIndex), HandleClientExecuteAsync);
    }

    /// <summary>
    /// 슬롯별 클라이언트 경로와 실행 정책을 검사한 뒤 실제 게임 시작을 요청합니다.
    /// </summary>
    private async Task<bool> HandleClientExecuteAsync(GameLaunchAttempt attempt)
    {
        if (_shellWindow is not MainWindow window) return false;
        window.ClearGameLaunchWarnings();
        GameServer server = attempt.Server;
        int clientIndex = attempt.ClientIndex;
        string accountId = attempt.AccountId;
        ClientLaunchAvailability availability = GetClientLaunchAvailability(clientIndex);
        switch (availability.Status)
        {
            case ClientLaunchStatus.InstallPathMissing:
                attempt.Fail("경로 미설정");
                window.ShowGameLaunchSettingsNotice(clientIndex, "설치 경로 미설정",
                    $"{GameServerHelper.GetServerDisplayName(server)} 게임 설치 경로가 없습니다.",
                    SettingSection.GameInstall, server, "설치 경로 설정");
                return false;
            case ClientLaunchStatus.InstallPathInvalid:
                attempt.Fail("잘못된 경로");
                window.ShowGameLaunchSettingsNotice(clientIndex, "잘못된 설치 경로",
                    $"{GameServerHelper.GetServerDisplayName(server)} 게임 설치 경로가 올바르지 않습니다.",
                    SettingSection.GameInstall, server, "설치 경로 설정");
                return false;
            case ClientLaunchStatus.MultiClientSetupRequired:
                attempt.Fail("다클라 생성 필요");
                await HandleMultiClientSetupRequiredAsync(server, clientIndex, availability.Detail);
                return false;
            case ClientLaunchStatus.RetryCooldown:
                attempt.Fail("재시도 제한");
                window.ShowGameLaunchWarning(clientIndex, "잠시 후 다시 시도해 주세요", "로그인 실패 후 5초 동안은 다시 실행할 수 없습니다.");
                return false;
            case ClientLaunchStatus.Starting:
            case ClientLaunchStatus.Running:
                return false;
        }

        if (string.IsNullOrWhiteSpace(accountId))
        {
            attempt.Fail("계정 미선택");
            window.ShowGameLaunchSettingsNotice(clientIndex, "계정 미선택",
                "선택된 계정이 없습니다.",
                SettingSection.Account, actionLabel: "계정 설정", actionLeadingText: "아직 계정 추가를 안하셨나요?");
            return false;
        }
        if (!EnsureAccountReadyForLaunch(window, accountId, attempt)) return false;
        attempt.Token.ThrowIfCancellationRequested();
        if (await CheckAndHandleRequiredPatchAsync(window, server, attempt.InstallPath, attempt)) return false;
        attempt.Token.ThrowIfCancellationRequested();

        // 취소된 외부 런처의 늦은 PID를 새 실행 시도로 오인하지 않도록 탐색은 직렬화합니다.
        if (window.GameStarter.ActiveClients.Any(client => client.State == GameClientState.Starting))
        {
            attempt.Fail("이전 런처 탐색 중");
            window.ShowGameLaunchWarning(clientIndex, "중복된 게임 실행 요청",
                "이미 게임 실행을 시도하고 있습니다. 잠시만 기다려주세요.");
            return false;
        }
        if (window.GameStarter.ActiveClientCount >= 3)
        {
            attempt.Fail("최대 실행 개수");
            window.ShowGameLaunchWarning(clientIndex, "게임 실행 불가", "게임이 이미 한 컴퓨터에서 실행할 수 있는 최대 개수만큼 켜져있습니다. 확인하신 뒤 다시 시도해주세요.");
            return false;
        }
        if (window.GameStarter.IsAccountActive(accountId))
        {
            attempt.Fail("동일 계정 중복");
            window.ShowGameLaunchWarning(clientIndex, "이미 사용 중인 계정", "동일한 계정으로 다른 클라이언트를 동시에 실행할 수 없습니다.");
            return false;
        }
        return await window.WebViewManager.TryGameStart(accountId, clientIndex, attempt);
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
                ? $"{clientLabel} 경로가 없거나 메인 클라이언트와 동기화되지 않았습니다.\n\n2·3클라이언트를 함께 생성합니다. 이미 생성된 클라이언트도 다시 동기화됩니다.\n계속할까요?"
                : $"{clientLabel} 생성이 필요합니다.\n{detail}\n\n2·3클라이언트를 함께 생성합니다. 이미 생성된 클라이언트도 다시 동기화됩니다.\n계속할까요?",
            PrimaryButtonText = "생성",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowManagedAsync() != ContentDialogResult.Primary)
            return;

        MultiClientCreationResult createResult = await MultiClientCreationService.CreateFromSavedSettingsAsync(server);
        MultiClientCreationCompleted?.Invoke(this, new(server, clientIndex, createResult.Success,
            createResult.Message, createResult.Exception, createResult.Canceled));

        RefreshClientAvailabilityState();
        await UpdateServer();
    }

    /// <summary>
    /// 실행 대상 클라이언트가 구버전이면 패치 안내 후 설정의 패치 화면으로 유도합니다.
    /// </summary>
    private async Task<bool> CheckAndHandleRequiredPatchAsync(MainWindow window, GameServer server, string clientInstallPath,
        GameLaunchAttempt attempt)
    {
        if (string.IsNullOrWhiteSpace(clientInstallPath) || !Directory.Exists(clientInstallPath))
            return false;

        ClientVersionReadResult currentVersionResult = PatchManager.TryGetCurrentClientVersion(clientInstallPath);
        if (!currentVersionResult.Success)
        {
            attempt.Fail("클라이언트 버전 확인 실패");
            ShowClientVersionCheckRequiredNotice(window, server, currentVersionResult, attempt.ClientIndex);
            return true;
        }

        int currentVersion = currentVersionResult.Version ?? 0;
        if (currentVersion <= 0)
        {
            attempt.Fail("클라이언트 버전 없음");
            window.ShowGameLaunchSettingsNotice(attempt.ClientIndex, "게임 버전 확인 실패",
                $"{GameServerHelper.GetServerDisplayName(server)} 게임 버전을 확인할 수 없습니다.",
                SettingSection.GameInstall, server, "설치 경로 설정");
            return true;
        }

        int latestVersion;
        try
        {
            latestVersion = await PatchManager.GetLatestServerVersionAsync(server).WaitAsync(attempt.Token);
            attempt.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (attempt.Token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            attempt.Token.ThrowIfCancellationRequested();
            if (ex is OperationCanceledException)
                ex = new IOException("최신 게임 버전 확인이 예기치 않게 중단되었습니다.", ex);
            App.ExceptionHandler.RecordRecoverable(ex, "StationPage.CheckLatestVersionBeforeLaunch");
            bool shouldContinue = await ShowLatestVersionCheckFailedDialogAsync(server, attempt.Token);
            if (!shouldContinue)
            {
                attempt.Fail("최신 버전 확인 후 실행 취소");
                return true;
            }

            return false;
        }

        if (currentVersion >= latestVersion)
            return false;

        attempt.Fail("패치 필요");
        window.ShowGameLaunchSettingsNotice(attempt.ClientIndex, "패치가 필요합니다",
            $"{GameServerHelper.GetServerDisplayName(server)} 현재 v{currentVersion} → 최신 v{latestVersion}",
            SettingSection.GamePatch, server, "패치 하기");

        return true;
    }

    /// <summary>
    /// 게임 실행 전에 선택한 계정의 비밀번호 상태를 확인합니다.
    /// </summary>
    private bool EnsureAccountReadyForLaunch(MainWindow window, string accountId,
        GameLaunchAttempt attempt)
    {
        PasswordVaultHelper.PasswordVaultReadResult passwordResult = PasswordVaultHelper.TryGetPassword(accountId);
        if (!passwordResult.Success)
        {
            attempt.Fail("비밀번호 저장소 접근 실패");
            window.ReportGameLaunchCredentialFailure(
                attempt.ClientIndex,
                passwordResult.Exception ?? new InvalidOperationException("비밀번호 저장소에서 실패 원인을 반환하지 않았습니다."),
                "StationPage.EnsureAccountReadyForLaunch");
            return false;
        }

        if (passwordResult.HasCredential && !string.IsNullOrWhiteSpace(passwordResult.Password))
            return true;

        attempt.Fail("비밀번호 없음");
        Account? account = Accounts.FirstOrDefault(account =>
            string.Equals(account.Id?.Trim(), accountId, StringComparison.OrdinalIgnoreCase));
        window.ShowMissingGameLaunchCredential(attempt.ClientIndex, account?.DisplayNickname ?? accountId);
        return false;
    }

    /// <summary>
    /// 설치된 클라이언트의 vsn.dat을 읽지 못했을 때 경로 확인을 안내합니다.
    /// </summary>
    private void ShowClientVersionCheckRequiredNotice(
        MainWindow window,
        GameServer server,
        ClientVersionReadResult result, int clientIndex)
    {
        string message = result.Exception is FileNotFoundException
            ? $"{GameServerHelper.GetServerDisplayName(server)} 게임 버전 파일이 없습니다."
            : $"{GameServerHelper.GetServerDisplayName(server)} 게임 버전을 읽을 수 없습니다.";

        if (result.Exception is { } exception)
            App.ExceptionHandler.RecordRecoverable(exception, "StationPage.CheckClientVersion");
        window.ShowGameLaunchSettingsNotice(clientIndex, "게임 버전 확인 실패", message,
            SettingSection.GameInstall, server, "설치 경로 설정");
    }

    /// <summary>
    /// 최신 패치 버전을 확인하지 못했을 때 사용자에게 계속 실행 여부를 묻습니다.
    /// </summary>
    private async Task<bool> ShowLatestVersionCheckFailedDialogAsync(GameServer server, CancellationToken cancellationToken)
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

        return await dialog.ShowManagedAsync(cancellationToken) == ContentDialogResult.Primary;
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

        if (result.ErrorKind == AppDataManager.AppDataErrorKind.Serialization)
        {
            App.ExceptionHandler.RecordRecoverable(BuildAppDataLoadException(dataName, result),
                "StationPage.HandleInitialStorageLoadIssuesAsync");
            if (await ShowResetCorruptedDataDialogAsync(dataName, result.Target))
                await ResetStationDataAsync(result.Target);
            return;
        }

        await App.ExceptionHandler.ShowRecoverableAsync(BuildAppDataLoadException(dataName, result),
            "StationPage.LoadStoredData");
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
            await App.ExceptionHandler.ShowRecoverableAsync(new IOException(
                $"저장된 데이터를 초기화하지 못했습니다. Operation={resetResult.Operation}, Target={resetResult.Target}, ErrorKind={resetResult.ErrorKind}", resetResult.Exception),
                "StationPage.ResetStoredData");
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
        if (!hasValidInstallPath)
        {
            SetVersionStatus(
                "게임 버전 확인 실패",
                clientSettingVisible: Visibility.Visible,
                refreshVisible: Visibility.Collapsed,
                patchVisible: Visibility.Collapsed);
            return;
        }

        ClientVersionReadResult currentVersionResult = PatchManager.TryGetCurrentClientVersion(clientInstallPath);
        int currentVersion = currentVersionResult.Success ? currentVersionResult.Version ?? 0 : 0;
        if (!currentVersionResult.Success || currentVersion <= 0)
        {
            SetVersionStatus(
                "게임 버전 확인 실패",
                clientSettingVisible: Visibility.Visible,
                refreshVisible: Visibility.Collapsed,
                patchVisible: Visibility.Collapsed);
            return;
        }

        int latestVersion;
        try
        {
            latestVersion = await PatchManager.GetLatestServerVersionAsync(server);
            if (latestVersion <= 0)
                throw new InvalidOperationException("최신 게임 버전이 올바르지 않습니다.");
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
                ex = new IOException("최신 게임 버전 확인이 예기치 않게 중단되었습니다.", ex);
            App.ExceptionHandler.RecordRecoverable(ex, "StationPage.RefreshLatestVersion");
            SetVersionStatus(
                "최신 버전 확인 실패",
                clientSettingVisible: Visibility.Collapsed,
                refreshVisible: Visibility.Visible,
                patchVisible: Visibility.Collapsed);
            return;
        }

        if (currentVersion < latestVersion)
        {
            SetVersionStatus(
                "게임 패치가 필요합니다.",
                clientSettingVisible: Visibility.Collapsed,
                refreshVisible: Visibility.Collapsed,
                patchVisible: Visibility.Visible);
            return;
        }

        SetVersionStatus(
            "현재 게임이 최신 버전입니다.",
            clientSettingVisible: Visibility.Collapsed,
            refreshVisible: Visibility.Visible,
            patchVisible: Visibility.Collapsed);
    }

    private void SetVersionStatus(
        string statusText,
        Visibility clientSettingVisible,
        Visibility refreshVisible,
        Visibility patchVisible)
    {
        VersionStatusText = statusText;
        ClientSettingButtonVisibility = clientSettingVisible;
        RefreshVersionButtonVisibility = refreshVisible;
        PatchButtonVisibility = patchVisible;
        OnPropertyChanged(nameof(VersionStatusText));
        OnPropertyChanged(nameof(ClientSettingButtonVisibility));
        OnPropertyChanged(nameof(RefreshVersionButtonVisibility));
        OnPropertyChanged(nameof(PatchButtonVisibility));
    }

    public Task RefreshServerAsync() => UpdateServer();

    public void NavigateToPatchSetting()
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
        return $"v{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
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
        StoreUpdateButtonVisibility = window.ShouldShowStoreUpdateButton ? Visibility.Visible : Visibility.Collapsed;
        StoreUpdateButtonEnabled = window.StoreUpdateButtonEnabled;
        OnPropertyChanged(nameof(CurrentAppVersionText));
        OnPropertyChanged(nameof(StoreUpdateButtonVisibility));
        OnPropertyChanged(nameof(StoreUpdateButtonEnabled));
    }

    public async Task InstallStoreUpdateAsync()
    {
        if (App.CurrentWindow is not MainWindow window)
            return;

        SyncStoreUpdateState(window);
        await window.InstallStoreUpdateAsync();
        SyncStoreUpdateState(window);
    }

    public void NavigateToInstallPathSetting()
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

    /// <summary>
    /// 모든 슬롯을 사용 가능하게 유지합니다. 실제 경로 검증과 실행 중 여부는 별도로 판단합니다.
    /// </summary>
    private void RefreshClientAvailabilityState()
    {
        for (int i = 0; i < 3; i++)
            _shellWindow?.GameLaunchCoordinator.SetSlotEnabled(i, true);
    }

    /// <summary>
    /// 현재 선택한 서버의 경로 유효성과, 버튼 번호 기준 전역 런타임 상태를 함께 반영한 실행 가능 상태를 계산합니다.
    /// </summary>
    private ClientLaunchAvailability GetClientLaunchAvailability(int clientIndex)
    {
        GameServer server = (GameServer)SelectedServerIndex;
        GameClientState runtimeState = (_shellWindow?.GameStarter ?? _gameStarter)?.GetClientState(server, clientIndex, null) ?? GameClientState.Available;
        if (runtimeState == GameClientState.RetryCooldown)
            return new ClientLaunchAvailability(ClientLaunchStatus.RetryCooldown);

        if (runtimeState == GameClientState.Starting)
            return new ClientLaunchAvailability(ClientLaunchStatus.Starting);

        if (runtimeState == GameClientState.Running)
            return new ClientLaunchAvailability(ClientLaunchStatus.Running);

        ClientSettings settings = AppDataManager.LoadServerClientSettings(server);

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
    public HomeContentLoadState EventLoad { get; } = new();
    public HomeContentLoadState HomepageNoticeLoad { get; } = new();
    public HomeContentLoadState GersangStationNoticeLoad { get; } = new();
    public static Visibility VisibleIf(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    private static readonly TimeSpan EventFlipInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan EventPeriodOverlayShowDelay = TimeSpan.FromMilliseconds(320);
    private int _eventPeriodOverlayVersion;
    private int _selectedEventIndex = -1;
    public IReadOnlyList<StationEventThumbnailItem> EventItems { get; private set; } = [];
    public IReadOnlyList<StationHomepageNoticeItem> HomepageNoticeItems { get; private set; } = [];
    public IReadOnlyList<StationHomepageNoticeItem> GersangStationNoticeItems { get; private set; } = [];
    public Visibility EventPeriodOverlayVisibility { get; private set; } = Visibility.Collapsed;

    public string EventHeaderText
    {
        get
        {
            if (EventLoad.IsLoading) return "이벤트를 불러오는 중...";
            if (EventLoad.HasFailed) return "이벤트를 불러올 수 없습니다.";
            if (EventItems.Count == 0)
                return EventLoad.IsEmpty ? "진행 중인 이벤트가 없습니다" : string.Empty;

            int selectedIndex = _selectedEventIndex;
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
            int selectedIndex = _selectedEventIndex;
            if (selectedIndex < 0 || selectedIndex >= EventItems.Count)
            {
                return string.Empty;
            }

            return EventItems[selectedIndex].PeriodOverlayText;
        }
    }

    public int CurrentEventRemainingDays => GetCurrentEventItem()?.RemainingDays ?? -1;

    /// <summary>이벤트 목록을 독립적으로 조회하며, 성공한 빈 결과만 '없음'으로 표시합니다.</summary>
    private Task LoadEventsAsync(bool forceRefresh = false)
        => EventLoad.LoadAsync<StationEventThumbnailItem>(
            async ct => (await GameHomepageCrawler.GetAllEventsAsync(HttpClientProvider.Http,
                new GameEventLoadOptions(IgnorePlaceholderEndDate: true), ct))
                .Where(item => !string.IsNullOrWhiteSpace(item.ThumbnailUrl))
                .Select(CreateEventThumbnailItem).ToArray(),
            items =>
            {
                EventItems = items;
                SetSelectedEventIndex(EventItems.Count > 0 ? 0 : -1);
                OnPropertyChanged(nameof(EventItems));
                OnPropertyChanged(nameof(EventHeaderText));
                OnPropertyChanged(nameof(CurrentEventPeriodOverlayText));
                OnPropertyChanged(nameof(CurrentEventRemainingDays));
                EventPeriodOverlayVisibility = EventItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                OnPropertyChanged(nameof(EventPeriodOverlayVisibility));
            },
            ex => App.ExceptionHandler.RecordRecoverable(ex, "StationPage.LoadEvents"), forceRefresh);

    private void CancelEventLoad() => EventLoad.Cancel();

    private Task LoadHomepageNoticesAsync(bool forceRefresh = false)
        => HomepageNoticeLoad.LoadAsync<StationHomepageNoticeItem>(
            async ct => (await GameHomepageCrawler.GetHomepageNoticesAsync(HttpClientProvider.Http, ct))
                .Take(5).Select(CreateHomepageNoticeItem).ToArray(),
            items =>
            {
                HomepageNoticeItems = items;
                OnPropertyChanged(nameof(HomepageNoticeItems));
            },
            ex => App.ExceptionHandler.RecordRecoverable(ex, "StationPage.LoadHomepageNotices"), forceRefresh);

    private void CancelHomepageNoticeLoad() => HomepageNoticeLoad.Cancel();

    private Task LoadGersangStationNoticesAsync(bool forceRefresh = false)
        => GersangStationNoticeLoad.LoadAsync<StationHomepageNoticeItem>(
            async ct => (await GersangStationNoticeCrawler.GetNoticesAsync(HttpClientProvider.Http, ct))
                .Select(CreateGersangStationNoticeItem).ToArray(),
            items =>
            {
                GersangStationNoticeItems = items;
                OnPropertyChanged(nameof(GersangStationNoticeItems));
            },
            ex => App.ExceptionHandler.RecordRecoverable(ex, "StationPage.LoadStationNotices"), forceRefresh);

    private void CancelGersangStationNoticeLoad() => GersangStationNoticeLoad.Cancel();

    private async void RetryEvents_Click(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(() => LoadEventsAsync(true), "StationPage.RetryEvents");

    private async void RetryHomepageNotices_Click(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(() => LoadHomepageNoticesAsync(true), "StationPage.RetryHomepageNotices");

    private async void RetryStationNotices_Click(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(() => LoadGersangStationNoticesAsync(true), "StationPage.RetryStationNotices");

    /// <summary>
    /// 이벤트 개수에 따라 자동 넘김 타이머를 시작하거나 중지합니다.
    /// </summary>
    private void UpdateEventFlipTimerState()
    {
        if (!_isShellActive || !EventLoad.HasSucceeded)
        {
            StopEventFlipTimer();
            return;
        }
        if (EventItems.Count > 1)
        {
            if (_selectedEventIndex < 0 || _selectedEventIndex >= EventItems.Count)
                SetSelectedEventIndex(0);

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
        OnPropertyChanged(nameof(CurrentEventRemainingDays));
    }

    /// <summary>
    /// 현재 선택된 이벤트 전환이 끝날 시점에 기간 오버레이를 다시 표시합니다.
    /// </summary>
    private async void ShowEventPeriodOverlayWhenReady()
    {
        int version = ++_eventPeriodOverlayVersion;
        await Task.Delay(EventPeriodOverlayShowDelay);

        if (version != _eventPeriodOverlayVersion || _isViewUnloaded || !_isShellActive)
            return;

        if (EventItems.Count > 0)
        {
            EventPeriodOverlayVisibility = Visibility.Visible;
            OnPropertyChanged(nameof(CurrentEventPeriodOverlayText));
            OnPropertyChanged(nameof(EventPeriodOverlayVisibility));
            OnPropertyChanged(nameof(CurrentEventRemainingDays));
        }
    }

    /// <summary>
    /// 5초마다 다음 이벤트로 이동하고 마지막이면 처음으로 되돌립니다.
    /// </summary>
    private void EventFlipTimer_Tick(object? sender, object e)
        => FatalErrorHandler.Current.RunCallback(AdvanceEventFlip, "StationPage.EventFlipTimer");

    private void AdvanceEventFlip()
    {
        if (EventItems.Count <= 1)
        {
            StopEventFlipTimer();
            return;
        }

        HideEventPeriodOverlay();
        int nextIndex = _selectedEventIndex + 1;
        if (nextIndex >= EventItems.Count || nextIndex < 0)
        {
            nextIndex = 0;
        }

        SetSelectedEventIndex(nextIndex);
    }

    /// <summary>
    /// 사용자가 이벤트를 직접 넘겼을 때 자동 넘김 타이머를 다시 시작합니다.
    /// </summary>
    private void FlipView_Event_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedEventIndex = FlipView_Event?.SelectedIndex ?? -1;
        HideEventPeriodOverlay();
        OnPropertyChanged(nameof(EventHeaderText));
        OnPropertyChanged(nameof(CurrentEventPeriodOverlayText));
        OnPropertyChanged(nameof(CurrentEventRemainingDays));
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
        int selectedIndex = _selectedEventIndex;
        if (selectedIndex < 0 || selectedIndex >= EventItems.Count)
            return null;

        return EventItems[selectedIndex];
    }

    /// <summary>
    /// 선택 이벤트 인덱스 캐시를 갱신하고, 아직 살아 있는 FlipView가 있으면 동일한 인덱스로 동기화합니다.
    /// </summary>
    private void SetSelectedEventIndex(int index)
    {
        _selectedEventIndex = index;

        if (_isViewUnloaded || FlipView_Event is null)
            return;

        if (FlipView_Event.SelectedIndex != index)
            FlipView_Event.SelectedIndex = index;
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

    #endregion EventFlipView
}
