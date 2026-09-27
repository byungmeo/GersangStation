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

        await ApplyStartupSelectionAsync(ToggleSwitch_StartupRegistration.IsOn);
    }

    private async void Button_CreateAdminLaunchDesktopShortcut_Click(object sender, RoutedEventArgs e)
    {
        Button_CreateAdminLaunchDesktopShortcut.IsEnabled = false;

        try
        {
            await AdminStartupRegistrationService.EnsureLauncherSupportFilesAsync();
            StartupRegistrationOperationResult taskResult = _isStartupRegistrationEnabled
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
        await DisableRegularStartupTaskIfPossibleAsync();
        AdminStartupRegistrationState adminState = await AdminStartupRegistrationService.GetStateAsync();
        if (adminState.IsRegistered && adminState.HasLogonTrigger)
        {
            AppDataManager.IsStartupRunAsAdministratorEnabled = true;
            ApplyStartupStateToControls(enabled: true);
            ExecutionRegistrationMessage = string.Empty;
            return;
        }

        AppDataManager.IsStartupRunAsAdministratorEnabled = false;
        ApplyStartupStateToControls(enabled: false);
        ExecutionRegistrationMessage = adminState.Message;
    }

    private async Task ApplyStartupSelectionAsync(bool enabled)
    {
        bool previousEnabled = _isStartupRegistrationEnabled;

        SetStartupToggleInteractivity(isInteractive: false);
        try
        {
            StartupRegistrationOperationResult result = await ConfigureStartupRegistrationAsync(enabled);

            if (result.Success)
            {
                ApplyStartupStateToControls(enabled);
                ExecutionRegistrationMessage = result.Message;
                return;
            }

            ApplyStartupStateToControls(previousEnabled);
            ExecutionRegistrationMessage = result.Message;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to apply startup selection: {ex}");
            ApplyStartupRegistrationFailure(
                previousEnabled,
                "자동 실행 설정을 변경하지 못했습니다. 다시 시도해도 안 되면 Windows 시작 앱 또는 작업 스케줄러에서 직접 확인해주세요.");
        }
        finally
        {
            SetStartupToggleInteractivity(isInteractive: true);
        }
    }

    private async Task<StartupRegistrationOperationResult> ConfigureStartupRegistrationAsync(bool enabled)
    {
        if (!enabled)
        {
            StartupRegistrationOperationResult adminResult = AdminLaunchDesktopShortcutService.ShortcutExists()
                ? await AdminStartupRegistrationService.EnableManualLaunchAsync()
                : await AdminStartupRegistrationService.DisableAsync();
            if (!adminResult.Success)
                return adminResult;

            StartupRegistrationOperationResult disableRegularResult = await DisableRegularStartupAsync(allowPolicyEnabledState: true);
            if (!disableRegularResult.Success)
                return disableRegularResult;

            AppDataManager.IsStartupRunAsAdministratorEnabled = false;
            return new StartupRegistrationOperationResult(true, string.Empty);
        }

        await AdminStartupRegistrationService.EnsureLauncherSupportFilesAsync();
        StartupRegistrationOperationResult enableAdminResult = await AdminStartupRegistrationService.EnableAsync();
        if (!enableAdminResult.Success)
            return enableAdminResult;

        StartupRegistrationOperationResult disableOldRegularResult = await DisableRegularStartupAsync(allowPolicyEnabledState: true);
        if (!disableOldRegularResult.Success)
        {
            await RestoreManualAdminTaskAfterStartupFailureAsync();
            return disableOldRegularResult;
        }

        AdminLaunchDesktopShortcutService.RefreshShortcutIfExists();
        AppDataManager.IsStartupRunAsAdministratorEnabled = true;
        return new StartupRegistrationOperationResult(true, string.Empty);
    }

    private static async Task RestoreManualAdminTaskAfterStartupFailureAsync()
    {
        StartupRegistrationOperationResult rollbackResult = AdminLaunchDesktopShortcutService.ShortcutExists()
            ? await AdminStartupRegistrationService.EnableManualLaunchAsync()
            : await AdminStartupRegistrationService.DisableAsync();

        if (!rollbackResult.Success)
            Debug.WriteLine($"Failed to roll back admin startup task after regular startup disable failure: {rollbackResult.Message}");
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

    private void ApplyStartupRegistrationFailure(bool enabled, string message)
    {
        ApplyStartupStateToControls(enabled);
        ExecutionRegistrationMessage = message;
    }

    private void ApplyStartupStateToControls(bool enabled)
    {
        _isUpdatingStartupRegistration = true;
        try
        {
            _isStartupRegistrationEnabled = enabled;

            ToggleSwitch_StartupRegistration.IsOn = enabled;
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
        Button_CreateAdminLaunchDesktopShortcut.IsEnabled = isInteractive;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
