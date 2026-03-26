using Core;
using GersangStation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace GersangStation.Main.Setting;

/// <summary>
/// 창 최소화와 닫기 관련 사용자 기본 동작을 편집합니다.
/// </summary>
public sealed partial class BehaviorSettingPage : Page, INotifyPropertyChanged
{
    private const string StartupTaskId = "GersangStationStartup";
    private static readonly AdminStartupRegistrationService AdminStartupRegistrationService = new();
    private static readonly AdminLaunchDesktopShortcutService AdminLaunchDesktopShortcutService = new(AdminStartupRegistrationService.TaskName);

    private int _minimizeBehaviorIndex = AppDataManager.MinimizeBehavior == AppDataManager.WindowMinimizeBehavior.HideToSystemTray
        ? 0
        : 1;
    private int _closeBehaviorIndex = AppDataManager.CloseBehavior == AppDataManager.WindowCloseBehavior.ExitApplication
        ? 0
        : 1;
    private bool _isUpdatingStartupRegistration;
    private bool _isStartupRegistrationEnabled;
    private bool _isStartupRegistrationRunAsAdministrator;
    private string _startupRegistrationMessage = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Windows 시작 프로그램 등록과 관련된 안내 문구입니다.
    /// </summary>
    public string StartupRegistrationMessage
    {
        get => _startupRegistrationMessage;
        private set
        {
            if (string.Equals(_startupRegistrationMessage, value, System.StringComparison.Ordinal))
                return;

            _startupRegistrationMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StartupRegistrationMessageVisibility));
        }
    }

    /// <summary>
    /// 자동 실행 안내 문구의 표시 여부입니다.
    /// </summary>
    public Visibility StartupRegistrationMessageVisibility
        => string.IsNullOrWhiteSpace(StartupRegistrationMessage)
            ? Visibility.Collapsed
            : Visibility.Visible;

    /// <summary>
    /// 창 최소화 기본 동작을 RadioButtons 인덱스로 표현합니다.
    /// </summary>
    public int MinimizeBehaviorIndex
    {
        get => _minimizeBehaviorIndex;
        set
        {
            int normalized = value == 1 ? 1 : 0;
            if (_minimizeBehaviorIndex == normalized)
                return;

            _minimizeBehaviorIndex = normalized;
            AppDataManager.MinimizeBehavior = normalized == 0
                ? AppDataManager.WindowMinimizeBehavior.HideToSystemTray
                : AppDataManager.WindowMinimizeBehavior.MinimizeToTaskbar;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 창 닫기 기본 동작을 RadioButtons 인덱스로 표현합니다.
    /// </summary>
    public int CloseBehaviorIndex
    {
        get => _closeBehaviorIndex;
        set
        {
            int normalized = value == 1 ? 1 : 0;
            if (_closeBehaviorIndex == normalized)
                return;

            _closeBehaviorIndex = normalized;
            AppDataManager.CloseBehavior = normalized == 0
                ? AppDataManager.WindowCloseBehavior.ExitApplication
                : AppDataManager.WindowCloseBehavior.HideToSystemTray;
            OnPropertyChanged();
        }
    }

    public BehaviorSettingPage()
    {
        InitializeComponent();
        Button_CreateAdminLaunchDesktopShortcut.IsEnabled = false;
    }

    /// <summary>
    /// 페이지가 표시될 때 현재 Windows 시작 프로그램 등록 상태를 불러옵니다.
    /// </summary>
    private async void BehaviorSettingPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadStartupRegistrationStateAsync();
    }

    /// <summary>
    /// 사용자가 자동 실행 토글을 변경하면 현재 선택에 맞는 시작 프로그램 구성을 적용합니다.
    /// </summary>
    private async void ToggleSwitch_StartupRegistration_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingStartupRegistration)
            return;

        await ApplyStartupSelectionAsync(
            ToggleSwitch_StartupRegistration.IsOn,
            ToggleSwitch_StartupRegistration.IsOn && ToggleSwitch_StartupRegistrationRunAsAdministrator.IsOn,
            changedByAdministratorToggle: false);
    }

    /// <summary>
    /// 사용자가 관리자 권한 자동 실행 토글을 변경하면 작업 스케줄러 기반 시작 설정으로 전환합니다.
    /// </summary>
    private async void ToggleSwitch_StartupRegistrationRunAsAdministrator_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingStartupRegistration || !ToggleSwitch_StartupRegistration.IsOn)
            return;

        await ApplyStartupSelectionAsync(
            enabled: true,
            runAsAdministrator: ToggleSwitch_StartupRegistrationRunAsAdministrator.IsOn,
            changedByAdministratorToggle: true);
    }

    /// <summary>
    /// 관리자 자동 실행 작업을 호출하는 바탕화면 바로가기를 생성합니다.
    /// </summary>
    private async void Button_CreateAdminLaunchDesktopShortcut_Click(object sender, RoutedEventArgs e)
    {
        Button_CreateAdminLaunchDesktopShortcut.IsEnabled = false;

        try
        {
            AdminStartupRegistrationState adminState = await AdminStartupRegistrationService.GetStateAsync();
            if (!adminState.IsRegistered)
            {
                StartupRegistrationMessage = string.IsNullOrWhiteSpace(adminState.Message)
                    ? "관리자 실행 바로가기를 만들려면 먼저 자동 실행 시 관리자 권한으로 실행을 켜주세요."
                    : adminState.Message;
                return;
            }

            DesktopShortcutCreationResult result = AdminLaunchDesktopShortcutService.CreateShortcut();
            StartupRegistrationMessage = result.Success
                ? $"관리자 실행 바로가기를 바탕화면에 만들었습니다: {result.ShortcutPath}"
                : result.Message;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to create admin desktop shortcut: {ex}");
            StartupRegistrationMessage = "관리자 실행 바로가기를 만들지 못했습니다. 다시 시도해도 안 되면 바탕화면 쓰기 권한과 Windows 바로가기 구성을 확인해주세요.";
        }
        finally
        {
            Button_CreateAdminLaunchDesktopShortcut.IsEnabled = _isStartupRegistrationEnabled && _isStartupRegistrationRunAsAdministrator;
        }
    }

    /// <summary>
    /// 현재 시스템의 시작 프로그램 등록 상태를 읽어 두 토글과 안내 문구를 동기화합니다.
    /// </summary>
    private async Task LoadStartupRegistrationStateAsync()
    {
        AdminStartupRegistrationState adminState = await AdminStartupRegistrationService.GetStateAsync();
        if (adminState.IsRegistered)
        {
            AppDataManager.IsStartupRunAsAdministratorEnabled = true;
            await DisableRegularStartupTaskIfPossibleAsync();
            ApplyStartupStateToControls(enabled: true, runAsAdministrator: true);
            StartupRegistrationMessage = string.Empty;
            return;
        }

        if (AppDataManager.IsStartupRunAsAdministratorEnabled)
        {
            AppDataManager.IsStartupRunAsAdministratorEnabled = false;
            StartupRegistrationMessage = string.IsNullOrWhiteSpace(adminState.Message)
                ? "관리자 권한 자동 실행이 등록되어 있지 않아 일반 자동 실행 상태를 다시 확인했습니다."
                : adminState.Message;
        }

        try
        {
            StartupTask startupTask = await StartupTask.GetAsync(StartupTaskId);
            ApplyRegularStartupStateToControls(startupTask.State, preserveExistingMessage: !string.IsNullOrWhiteSpace(StartupRegistrationMessage));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load startup task state: {ex}");
            ApplyStartupRegistrationFailure(enabled: false, runAsAdministrator: false, "자동 실행 상태를 확인하지 못했습니다. 현재는 이 기능을 사용할 수 없습니다.");
        }
    }

    /// <summary>
    /// 사용자가 선택한 자동 실행 조합을 실제 시스템 시작 프로그램 설정에 반영합니다.
    /// </summary>
    private async Task ApplyStartupSelectionAsync(bool enabled, bool runAsAdministrator, bool changedByAdministratorToggle)
    {
        bool previousEnabled = _isStartupRegistrationEnabled;
        bool previousRunAsAdministrator = _isStartupRegistrationRunAsAdministrator;

        SetStartupToggleInteractivity(isInteractive: false);
        try
        {
            StartupRegistrationOperationResult result = await ConfigureStartupRegistrationAsync(
                enabled,
                runAsAdministrator,
                previousEnabled,
                previousRunAsAdministrator,
                changedByAdministratorToggle);

            if (result.Success)
            {
                ApplyStartupStateToControls(enabled, runAsAdministrator);
                StartupRegistrationMessage = result.Message;
                return;
            }

            ApplyStartupStateToControls(previousEnabled, previousRunAsAdministrator);
            StartupRegistrationMessage = result.Message;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to apply startup selection: {ex}");
            ApplyStartupRegistrationFailure(
                previousEnabled,
                previousRunAsAdministrator,
                "자동 실행 설정을 변경하지 못했습니다. 다시 시도해도 안 되면 Windows 시작 앱 또는 작업 스케줄러에서 직접 확인해주세요.");
        }
        finally
        {
            SetStartupToggleInteractivity(isInteractive: true);
        }
    }

    /// <summary>
    /// 사용자가 요청한 조합에 맞게 시작 프로그램 등록 방식을 전환합니다.
    /// </summary>
    private async Task<StartupRegistrationOperationResult> ConfigureStartupRegistrationAsync(
        bool enabled,
        bool runAsAdministrator,
        bool previousEnabled,
        bool previousRunAsAdministrator,
        bool changedByAdministratorToggle)
    {
        if (!enabled)
        {
            if (previousRunAsAdministrator)
            {
                StartupRegistrationOperationResult disableAdminResult = await AdminStartupRegistrationService.DisableAsync();
                if (!disableAdminResult.Success)
                    return disableAdminResult;
            }

            StartupRegistrationOperationResult disableRegularResult = await DisableRegularStartupAsync();
            if (!disableRegularResult.Success)
                return disableRegularResult;

            AppDataManager.IsStartupRunAsAdministratorEnabled = false;
            return new StartupRegistrationOperationResult(true, string.Empty);
        }

        if (runAsAdministrator)
        {
            StartupTaskState regularState = await GetRegularStartupTaskStateAsync();
            if (regularState == StartupTaskState.EnabledByPolicy)
            {
                return new StartupRegistrationOperationResult(
                    false,
                    "현재 Windows 정책으로 일반 자동 실행이 강제로 켜져 있어 관리자 권한 자동 실행으로 전환할 수 없습니다.");
            }

            StartupRegistrationOperationResult enableAdminResult = await AdminStartupRegistrationService.EnableAsync();
            if (!enableAdminResult.Success)
                return enableAdminResult;

            StartupRegistrationOperationResult disableRegularResult = await DisableRegularStartupAsync(allowPolicyEnabledState: true);
            if (!disableRegularResult.Success)
            {
                await AdminStartupRegistrationService.DisableAsync();
                return disableRegularResult;
            }

            AppDataManager.IsStartupRunAsAdministratorEnabled = true;
            return new StartupRegistrationOperationResult(true, string.Empty);
        }

        StartupRegistrationOperationResult enableRegularResult = await EnableRegularStartupAsync();
        if (!enableRegularResult.Success)
            return enableRegularResult;

        if (previousRunAsAdministrator || AppDataManager.IsStartupRunAsAdministratorEnabled)
        {
            StartupRegistrationOperationResult disableAdminResult = await AdminStartupRegistrationService.DisableAsync();
            if (!disableAdminResult.Success)
            {
                if (changedByAdministratorToggle)
                    await DisableRegularStartupAsync();

                return disableAdminResult;
            }
        }

        AppDataManager.IsStartupRunAsAdministratorEnabled = false;
        return new StartupRegistrationOperationResult(true, string.Empty);
    }

    /// <summary>
    /// 일반 시작 프로그램의 현재 상태를 가져옵니다.
    /// </summary>
    private static async Task<StartupTaskState> GetRegularStartupTaskStateAsync()
    {
        StartupTask startupTask = await StartupTask.GetAsync(StartupTaskId);
        return startupTask.State;
    }

    /// <summary>
    /// 일반 시작 프로그램을 가능한 경우 해제합니다.
    /// </summary>
    private static async Task DisableRegularStartupTaskIfPossibleAsync()
    {
        try
        {
            await DisableRegularStartupAsync(allowPolicyEnabledState: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to disable regular startup task while loading admin mode: {ex}");
        }
    }

    /// <summary>
    /// 일반 시작 프로그램 등록을 켭니다.
    /// </summary>
    private static async Task<StartupRegistrationOperationResult> EnableRegularStartupAsync()
    {
        try
        {
            StartupTask startupTask = await StartupTask.GetAsync(StartupTaskId);
            StartupTaskState state = await startupTask.RequestEnableAsync();

            return state switch
            {
                StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy => new StartupRegistrationOperationResult(true, string.Empty),
                StartupTaskState.DisabledByUser => new StartupRegistrationOperationResult(false, "자동 실행을 켜지 못했습니다. Windows 시작 앱 설정에서 이 앱을 다시 허용해주세요."),
                StartupTaskState.DisabledByPolicy => new StartupRegistrationOperationResult(false, "자동 실행은 현재 Windows 정책 또는 실행 환경에서 허용되지 않습니다."),
                _ => new StartupRegistrationOperationResult(false, "자동 실행을 켜지 못했습니다. Windows 시작 앱 설정에서 상태를 확인해주세요.")
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to enable regular startup task: {ex}");
            return new StartupRegistrationOperationResult(false, "자동 실행을 켜지 못했습니다. Windows 시작 앱 설정에서 상태를 확인해주세요.");
        }
    }

    /// <summary>
    /// 일반 시작 프로그램 등록을 끕니다.
    /// </summary>
    private static async Task<StartupRegistrationOperationResult> DisableRegularStartupAsync(bool allowPolicyEnabledState = false)
    {
        try
        {
            StartupTask startupTask = await StartupTask.GetAsync(StartupTaskId);
            startupTask.Disable();

            StartupTaskState state = startupTask.State;
            if (state == StartupTaskState.Enabled)
                state = StartupTaskState.Disabled;

            return state switch
            {
                StartupTaskState.Disabled or StartupTaskState.DisabledByUser or StartupTaskState.DisabledByPolicy => new StartupRegistrationOperationResult(true, string.Empty),
                StartupTaskState.EnabledByPolicy when allowPolicyEnabledState => new StartupRegistrationOperationResult(true, string.Empty),
                StartupTaskState.EnabledByPolicy => new StartupRegistrationOperationResult(false, "자동 실행이 Windows 정책으로 강제로 켜져 있어 해제할 수 없습니다."),
                _ => new StartupRegistrationOperationResult(false, "자동 실행을 해제하지 못했습니다. Windows 시작 앱 설정에서 상태를 확인해주세요.")
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to disable regular startup task: {ex}");
            return new StartupRegistrationOperationResult(false, "자동 실행을 해제하지 못했습니다. Windows 시작 앱 설정에서 상태를 확인해주세요.");
        }
    }

    /// <summary>
    /// 일반 자동 실행 상태를 현재 페이지 토글 표현으로 반영합니다.
    /// </summary>
    private void ApplyRegularStartupStateToControls(StartupTaskState state, bool preserveExistingMessage)
    {
        switch (state)
        {
            case StartupTaskState.Enabled:
            case StartupTaskState.EnabledByPolicy:
                ApplyStartupStateToControls(enabled: true, runAsAdministrator: false);
                if (!preserveExistingMessage)
                {
                    StartupRegistrationMessage = state == StartupTaskState.EnabledByPolicy
                        ? "자동 실행이 현재 Windows 정책으로 강제로 켜져 있습니다."
                        : string.Empty;
                }
                break;

            case StartupTaskState.Disabled:
                ApplyStartupStateToControls(enabled: false, runAsAdministrator: false);
                if (!preserveExistingMessage)
                    StartupRegistrationMessage = string.Empty;
                break;

            case StartupTaskState.DisabledByUser:
                ApplyStartupStateToControls(enabled: false, runAsAdministrator: false);
                if (!preserveExistingMessage)
                    StartupRegistrationMessage = "Windows에서 시작 프로그램이 꺼져 있습니다. 자동 실행이 필요하면 Windows 시작 앱 설정에서 다시 켜주세요.";
                break;

            case StartupTaskState.DisabledByPolicy:
                ApplyStartupStateToControls(enabled: false, runAsAdministrator: false);
                if (!preserveExistingMessage)
                    StartupRegistrationMessage = "일반 자동 실행은 현재 Windows 정책 또는 실행 환경에서 허용되지 않습니다.";
                break;

            default:
                ApplyStartupRegistrationFailure(enabled: false, runAsAdministrator: false, "자동 실행 상태를 확인하지 못했습니다. 현재는 이 기능을 사용할 수 없습니다.");
                break;
        }
    }

    /// <summary>
    /// 자동 실행 처리 실패 시 마지막 유효 상태로 되돌리고 설명 문구를 표시합니다.
    /// </summary>
    private void ApplyStartupRegistrationFailure(bool enabled, bool runAsAdministrator, string message)
    {
        ApplyStartupStateToControls(enabled, runAsAdministrator);
        StartupRegistrationMessage = message;
    }

    /// <summary>
    /// 두 토글의 상태와 활성화 여부를 이벤트 재진입 없이 갱신합니다.
    /// </summary>
    private void ApplyStartupStateToControls(bool enabled, bool runAsAdministrator)
    {
        _isUpdatingStartupRegistration = true;
        try
        {
            _isStartupRegistrationEnabled = enabled;
            _isStartupRegistrationRunAsAdministrator = enabled && runAsAdministrator;

            ToggleSwitch_StartupRegistration.IsOn = enabled;
            ToggleSwitch_StartupRegistrationRunAsAdministrator.IsOn = enabled && runAsAdministrator;
            ToggleSwitch_StartupRegistrationRunAsAdministrator.IsEnabled = enabled;
            Button_CreateAdminLaunchDesktopShortcut.IsEnabled = enabled && runAsAdministrator;
        }
        finally
        {
            _isUpdatingStartupRegistration = false;
        }
    }

    /// <summary>
    /// 비동기 적용 중 사용자가 토글을 다시 조작하지 못하도록 잠깁니다.
    /// </summary>
    private void SetStartupToggleInteractivity(bool isInteractive)
    {
        ToggleSwitch_StartupRegistration.IsEnabled = isInteractive;
        ToggleSwitch_StartupRegistrationRunAsAdministrator.IsEnabled = isInteractive && ToggleSwitch_StartupRegistration.IsOn;
        Button_CreateAdminLaunchDesktopShortcut.IsEnabled =
            isInteractive &&
            _isStartupRegistrationEnabled &&
            _isStartupRegistrationRunAsAdministrator;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
