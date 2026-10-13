using Core;
using GersangStation.Diagnostics;
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
/// 창 최소화/닫기 동작과 Windows 시작 프로그램 등록을 한 페이지에서 편집합니다.
/// </summary>
public sealed partial class GeneralSettingPage : Page, INotifyPropertyChanged
{
    private int _minimizeBehaviorIndex = AppDataManager.MinimizeBehavior == AppDataManager.WindowMinimizeBehavior.HideToSystemTray
        ? 0
        : 1;
    private int _closeBehaviorIndex = AppDataManager.CloseBehavior == AppDataManager.WindowCloseBehavior.ExitApplication
        ? 0
        : 1;

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

    private const string StartupTaskId = "GersangStationStartup";

    private StartupTaskState? _startupTaskState;
    private bool _isStartupRegistrationBusy;
    private bool _isUpdatingStartupRegistration;
    private bool _isStartupRegistrationEnabled;
    private string _startupRegistrationMessage = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string StartupRegistrationMessage
    {
        get => _startupRegistrationMessage;
        private set
        {
            if (string.Equals(_startupRegistrationMessage, value, StringComparison.Ordinal))
                return;

            _startupRegistrationMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StartupRegistrationMessageVisibility));
        }
    }

    public Visibility StartupRegistrationMessageVisibility
        => string.IsNullOrWhiteSpace(StartupRegistrationMessage)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public GeneralSettingPage()
    {
        InitializeComponent();
    }

    private async void GeneralSettingPage_Loaded(object sender, RoutedEventArgs e)
    {
        await SafeExecution.RunHandledAsync(LoadStartupRegistrationStateAsync,
            $"{nameof(GeneralSettingPage)}.{nameof(GeneralSettingPage_Loaded)}");
    }

    private async void ToggleSwitch_StartupRegistration_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingStartupRegistration || _isStartupRegistrationBusy)
            return;

        await SafeExecution.RunHandledAsync(() => ApplyStartupSelectionAsync(ToggleSwitch_StartupRegistration.IsOn),
            $"{nameof(GeneralSettingPage)}.{nameof(ToggleSwitch_StartupRegistration_Toggled)}");
    }

    private async Task LoadStartupRegistrationStateAsync()
    {
        if (_isStartupRegistrationBusy)
            return;
        SetStartupToggleInteractivity(isInteractive: false);
        try
        {
            StartupTask startupTask = await StartupTask.GetAsync(StartupTaskId);
            ApplyStartupState(startupTask.State);
            StartupRegistrationMessage = GetStartupStateMessage(startupTask.State);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to read Windows startup registration: {ex}");
            _startupTaskState = null;
            StartupRegistrationMessage = "자동 실행 상태를 확인하지 못했습니다. 일반 설정 페이지를 다시 열어 확인해주세요.";
        }
        finally
        {
            SetStartupToggleInteractivity(isInteractive: true);
        }
    }

    private async Task ApplyStartupSelectionAsync(bool enabled)
    {
        bool previousEnabled = _isStartupRegistrationEnabled;
        SetStartupToggleInteractivity(isInteractive: false);
        try
        {
            StartupTask startupTask = await StartupTask.GetAsync(StartupTaskId);
            StartupTaskState state = startupTask.State;
            if (enabled && state == StartupTaskState.Disabled)
            {
                state = await startupTask.RequestEnableAsync();
            }
            else if (!enabled && state == StartupTaskState.Enabled)
            {
                startupTask.Disable();
                // Read the actual Windows state instead of assuming Disable succeeded.
                state = (await StartupTask.GetAsync(StartupTaskId)).State;
            }

            ApplyStartupState(state);
            StartupRegistrationMessage = GetStartupStateMessage(state);
            if (IsStartupEnabled(state) != enabled && string.IsNullOrEmpty(StartupRegistrationMessage))
            {
                StartupRegistrationMessage = enabled
                    ? "자동 실행을 등록하지 못했습니다. Windows 시작 앱 설정에서 상태를 확인해주세요."
                    : "자동 실행을 해제하지 못했습니다. Windows 시작 앱 설정에서 상태를 확인해주세요.";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to change Windows startup registration: {ex}");
            ApplyStartupStateToControls(previousEnabled);
            StartupRegistrationMessage = "자동 실행 설정을 변경하지 못했습니다. Windows 설정 > 앱 > 시작 프로그램에서 상태를 확인해주세요.";
        }
        finally
        {
            SetStartupToggleInteractivity(isInteractive: true);
        }
    }

    private static bool IsStartupEnabled(StartupTaskState state)
        => state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;

    private static string GetStartupStateMessage(StartupTaskState state)
        => state switch
        {
            StartupTaskState.Disabled or StartupTaskState.Enabled => string.Empty,
            StartupTaskState.DisabledByUser => "Windows에서 사용자가 자동 실행을 껐습니다. Windows 설정 > 앱 > 시작 프로그램에서 다시 켜주세요.",
            StartupTaskState.DisabledByPolicy => "Windows 정책에 의해 자동 실행이 제한되어 있습니다.",
            StartupTaskState.EnabledByPolicy => "Windows 정책에 의해 자동 실행이 켜져 있어 앱에서 변경할 수 없습니다.",
            _ => "알 수 없는 자동 실행 상태입니다. Windows 시작 앱 설정에서 확인해주세요."
        };

    private void ApplyStartupState(StartupTaskState state)
    {
        _startupTaskState = state;
        ApplyStartupStateToControls(IsStartupEnabled(state));
    }

    private void ApplyStartupStateToControls(bool enabled)
    {
        _isUpdatingStartupRegistration = true;
        try
        {
            _isStartupRegistrationEnabled = enabled;
            ToggleSwitch_StartupRegistration.IsOn = enabled;
        }
        finally
        {
            _isUpdatingStartupRegistration = false;
        }
    }

    private void SetStartupToggleInteractivity(bool isInteractive)
    {
        _isStartupRegistrationBusy = !isInteractive;
        ToggleSwitch_StartupRegistration.IsEnabled = isInteractive
            && _startupTaskState is StartupTaskState.Disabled or StartupTaskState.DisabledByUser or StartupTaskState.Enabled;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
