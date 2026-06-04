using Core;
using GersangStation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace GersangStation.Main.Setting;

/// <summary>
/// 앱 실행 방식과 Windows 시작 프로그램 등록을 편집합니다.
/// </summary>
public sealed partial class ExecutionSettingPage : Page, INotifyPropertyChanged
{
    private const string StartupTaskId = "GersangStationStartup";
    private static readonly AdminStartupRegistrationService AdminStartupRegistrationService = new();
    private static readonly AdminLaunchDesktopShortcutService AdminLaunchDesktopShortcutService = new(
        AdminStartupRegistrationService.TaskName,
        AdminStartupRegistrationService.DesktopShortcutIconPath,
        "GersangStation Admin.lnk");

    private bool _isUpdatingStartupRegistration;
    private bool _isStartupRegistrationEnabled;
    private bool _isStartupRegistrationRunAsAdministrator;
    private string _executionRegistrationMessage = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ExecutionRegistrationMessage
    {
        get => _executionRegistrationMessage;
        private set
        {
            if (string.Equals(_executionRegistrationMessage, value, StringComparison.Ordinal))
                return;

            _executionRegistrationMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ExecutionRegistrationMessageVisibility));
        }
    }

    public Visibility ExecutionRegistrationMessageVisibility
        => string.IsNullOrWhiteSpace(ExecutionRegistrationMessage)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public ExecutionSettingPage()
    {
        InitializeComponent();
        Button_CreateAdminLaunchDesktopShortcut.IsEnabled = true;
    }

    private async void ExecutionSettingPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadStartupRegistrationStateAsync();
    }

    private async void ToggleSwitch_StartupRegistration_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingStartupRegistration)
            return;

        await ApplyStartupSelectionAsync(
            ToggleSwitch_StartupRegistration.IsOn,
            ToggleSwitch_StartupRegistration.IsOn && ToggleSwitch_StartupRegistrationRunAsAdministrator.IsOn,
            changedByAdministratorToggle: false);
    }

    private async void ToggleSwitch_StartupRegistrationRunAsAdministrator_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingStartupRegistration || !ToggleSwitch_StartupRegistration.IsOn)
            return;

        await ApplyStartupSelectionAsync(
            enabled: true,
            runAsAdministrator: ToggleSwitch_StartupRegistrationRunAsAdministrator.IsOn,
            changedByAdministratorToggle: true);
    }

    private async void Button_CreateAdminLaunchDesktopShortcut_Click(object sender, RoutedEventArgs e)
    {
        Button_CreateAdminLaunchDesktopShortcut.IsEnabled = false;

        try
        {
            await AdminStartupRegistrationService.EnsureLauncherSupportFilesAsync();
            StartupRegistrationOperationResult taskResult = _isStartupRegistrationEnabled && _isStartupRegistrationRunAsAdministrator
                ? await AdminStartupRegistrationService.EnableAsync()
                : await AdminStartupRegistrationService.EnableManualLaunchAsync();
            if (!taskResult.Success)
            {
                ExecutionRegistrationMessage = taskResult.Message;
                return;
            }

            DesktopShortcutCreationResult result = AdminLaunchDesktopShortcutService.CreateShortcut();
            ExecutionRegistrationMessage = result.Success
                ? $"관리자 실행 바로가기를 바탕화면에 만들었습니다: {result.ShortcutPath}"
                : result.Message;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to create admin desktop shortcut: {ex}");
            ExecutionRegistrationMessage = "관리자 실행 바로가기를 만들지 못했습니다. 다시 시도해도 안 되면 바탕화면 쓰기 권한과 Windows 바로가기 구성을 확인해주세요.";
        }
        finally
        {
            Button_CreateAdminLaunchDesktopShortcut.IsEnabled = true;
        }
    }

    private async Task LoadStartupRegistrationStateAsync()
    {
        AdminStartupRegistrationState adminState = await AdminStartupRegistrationService.GetStateAsync();
        if (adminState.IsRegistered && adminState.HasLogonTrigger)
        {
            AppDataManager.IsStartupRunAsAdministratorEnabled = true;
            await DisableRegularStartupTaskIfPossibleAsync();
            ApplyStartupStateToControls(enabled: true, runAsAdministrator: true);
            ExecutionRegistrationMessage = string.Empty;
            return;
        }

        if (AppDataManager.IsStartupRunAsAdministratorEnabled)
        {
            AppDataManager.IsStartupRunAsAdministratorEnabled = false;
            ExecutionRegistrationMessage = string.IsNullOrWhiteSpace(adminState.Message)
                ? "관리자 권한 자동 실행이 등록되어 있지 않아 일반 자동 실행 상태를 다시 확인했습니다."
                : adminState.Message;
        }

        try
        {
            StartupTask startupTask = await StartupTask.GetAsync(StartupTaskId);
            ApplyRegularStartupStateToControls(startupTask.State, preserveExistingMessage: !string.IsNullOrWhiteSpace(ExecutionRegistrationMessage));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load startup task state: {ex}");
            ApplyStartupRegistrationFailure(enabled: false, runAsAdministrator: false, "자동 실행 상태를 확인하지 못했습니다. 현재는 이 기능을 사용할 수 없습니다.");
        }
    }

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
                previousRunAsAdministrator,
                changedByAdministratorToggle);

            if (result.Success)
            {
                ApplyStartupStateToControls(enabled, runAsAdministrator);
                ExecutionRegistrationMessage = result.Message;
                return;
            }

            ApplyStartupStateToControls(previousEnabled, previousRunAsAdministrator);
            ExecutionRegistrationMessage = result.Message;
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

    private async Task<StartupRegistrationOperationResult> ConfigureStartupRegistrationAsync(
        bool enabled,
        bool runAsAdministrator,
        bool previousRunAsAdministrator,
        bool changedByAdministratorToggle)
    {
        if (!enabled)
        {
            if (previousRunAsAdministrator)
            {
                StartupRegistrationOperationResult adminResult = AdminLaunchDesktopShortcutService.ShortcutExists()
                    ? await AdminStartupRegistrationService.EnableManualLaunchAsync()
                    : await AdminStartupRegistrationService.DisableAsync();
                if (!adminResult.Success)
                    return adminResult;
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

            await AdminStartupRegistrationService.EnsureLauncherSupportFilesAsync();
            StartupRegistrationOperationResult enableAdminResult = await AdminStartupRegistrationService.EnableAsync();
            if (!enableAdminResult.Success)
                return enableAdminResult;

            StartupRegistrationOperationResult disableRegularResult = await DisableRegularStartupAsync(allowPolicyEnabledState: true);
            if (!disableRegularResult.Success)
            {
                if (AdminLaunchDesktopShortcutService.ShortcutExists())
                    await AdminStartupRegistrationService.EnableManualLaunchAsync();
                else
                    await AdminStartupRegistrationService.DisableAsync();

                return disableRegularResult;
            }

            AdminLaunchDesktopShortcutService.RefreshShortcutIfExists();
            AppDataManager.IsStartupRunAsAdministratorEnabled = true;
            return new StartupRegistrationOperationResult(true, string.Empty);
        }

        StartupRegistrationOperationResult enableRegularResult = await EnableRegularStartupAsync();
        if (!enableRegularResult.Success)
            return enableRegularResult;

        if (previousRunAsAdministrator || AppDataManager.IsStartupRunAsAdministratorEnabled)
        {
            StartupRegistrationOperationResult adminResult = AdminLaunchDesktopShortcutService.ShortcutExists()
                ? await AdminStartupRegistrationService.EnableManualLaunchAsync()
                : await AdminStartupRegistrationService.DisableAsync();
            if (!adminResult.Success)
            {
                if (changedByAdministratorToggle)
                    await DisableRegularStartupAsync();

                return adminResult;
            }
        }

        AppDataManager.IsStartupRunAsAdministratorEnabled = false;
        return new StartupRegistrationOperationResult(true, string.Empty);
    }

    private static async Task<StartupTaskState> GetRegularStartupTaskStateAsync()
    {
        StartupTask startupTask = await StartupTask.GetAsync(StartupTaskId);
        return startupTask.State;
    }

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

    private void ApplyRegularStartupStateToControls(StartupTaskState state, bool preserveExistingMessage)
    {
        switch (state)
        {
            case StartupTaskState.Enabled:
            case StartupTaskState.EnabledByPolicy:
                ApplyStartupStateToControls(enabled: true, runAsAdministrator: false);
                if (!preserveExistingMessage)
                {
                    ExecutionRegistrationMessage = state == StartupTaskState.EnabledByPolicy
                        ? "자동 실행이 현재 Windows 정책으로 강제로 켜져 있습니다."
                        : string.Empty;
                }
                break;

            case StartupTaskState.Disabled:
                ApplyStartupStateToControls(enabled: false, runAsAdministrator: false);
                if (!preserveExistingMessage)
                    ExecutionRegistrationMessage = string.Empty;
                break;

            case StartupTaskState.DisabledByUser:
                ApplyStartupStateToControls(enabled: false, runAsAdministrator: false);
                if (!preserveExistingMessage)
                    ExecutionRegistrationMessage = "Windows에서 시작 프로그램이 꺼져 있습니다. 자동 실행이 필요하면 Windows 시작 앱 설정에서 다시 켜주세요.";
                break;

            case StartupTaskState.DisabledByPolicy:
                ApplyStartupStateToControls(enabled: false, runAsAdministrator: false);
                if (!preserveExistingMessage)
                    ExecutionRegistrationMessage = "일반 자동 실행은 현재 Windows 정책 또는 실행 환경에서 허용되지 않습니다.";
                break;

            default:
                ApplyStartupRegistrationFailure(enabled: false, runAsAdministrator: false, "자동 실행 상태를 확인하지 못했습니다. 현재는 이 기능을 사용할 수 없습니다.");
                break;
        }
    }

    private void ApplyStartupRegistrationFailure(bool enabled, bool runAsAdministrator, string message)
    {
        ApplyStartupStateToControls(enabled, runAsAdministrator);
        ExecutionRegistrationMessage = message;
    }

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
            Button_CreateAdminLaunchDesktopShortcut.IsEnabled = true;
        }
        finally
        {
            _isUpdatingStartupRegistration = false;
        }
    }

    private void SetStartupToggleInteractivity(bool isInteractive)
    {
        ToggleSwitch_StartupRegistration.IsEnabled = isInteractive;
        ToggleSwitch_StartupRegistrationRunAsAdministrator.IsEnabled = isInteractive && ToggleSwitch_StartupRegistration.IsOn;
        Button_CreateAdminLaunchDesktopShortcut.IsEnabled = isInteractive;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
