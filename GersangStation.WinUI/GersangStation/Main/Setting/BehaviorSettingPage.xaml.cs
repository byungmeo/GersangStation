using Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel;

namespace GersangStation.Main.Setting;

/// <summary>
/// 창 최소화와 닫기 관련 사용자 기본 동작을 편집합니다.
/// </summary>
public sealed partial class BehaviorSettingPage : Page, INotifyPropertyChanged
{
    private const string StartupTaskId = "GersangStationStartup";

    private int _minimizeBehaviorIndex = AppDataManager.MinimizeBehavior == AppDataManager.WindowMinimizeBehavior.HideToSystemTray
        ? 0
        : 1;
    private int _closeBehaviorIndex = AppDataManager.CloseBehavior == AppDataManager.WindowCloseBehavior.ExitApplication
        ? 0
        : 1;
    private bool _isUpdatingStartupRegistration;
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
    }

    /// <summary>
    /// 페이지가 표시될 때 현재 Windows 시작 프로그램 등록 상태를 불러옵니다.
    /// </summary>
    private async void BehaviorSettingPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadStartupRegistrationStateAsync();
    }

    /// <summary>
    /// 사용자가 자동 실행 토글을 변경하면 Windows 시작 프로그램 등록 상태를 반영합니다.
    /// </summary>
    private async void ToggleSwitch_StartupRegistration_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingStartupRegistration)
            return;

        await UpdateStartupRegistrationAsync(ToggleSwitch_StartupRegistration.IsOn);
    }

    /// <summary>
    /// 현재 시스템의 시작 프로그램 등록 상태를 읽어 토글과 안내 문구를 동기화합니다.
    /// </summary>
    private async System.Threading.Tasks.Task LoadStartupRegistrationStateAsync()
    {
        try
        {
            StartupTask startupTask = await StartupTask.GetAsync(StartupTaskId);
            ApplyStartupTaskState(startupTask.State, StartupTaskMessageMode.Load);
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"Failed to load startup task state: {ex}");
            ApplyStartupRegistrationFailure("자동 실행 상태를 확인하지 못했습니다. 현재는 이 기능을 사용할 수 없습니다.");
        }
    }

    /// <summary>
    /// 사용자가 원하는 자동 실행 상태를 Windows 시작 프로그램 등록에 반영합니다.
    /// </summary>
    private async System.Threading.Tasks.Task UpdateStartupRegistrationAsync(bool shouldEnable)
    {
        try
        {
            StartupTask startupTask = await StartupTask.GetAsync(StartupTaskId);
            StartupTaskState resultingState;

            if (shouldEnable)
            {
                resultingState = await startupTask.RequestEnableAsync();
                ApplyStartupTaskState(resultingState, StartupTaskMessageMode.EnableRequest);
                return;
            }

            startupTask.Disable();
            resultingState = startupTask.State;
            if (resultingState == StartupTaskState.Enabled)
                resultingState = StartupTaskState.Disabled;

            ApplyStartupTaskState(resultingState, StartupTaskMessageMode.DisableRequest);
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"Failed to update startup task state: {ex}");
            ApplyStartupRegistrationFailure("자동 실행을 변경하지 못했습니다. 다시 시도해도 안 되면 Windows 시작 앱 설정에서 직접 확인해주세요.");
        }
    }

    /// <summary>
    /// Windows 시작 프로그램 상태를 토글과 안내 문구에 반영합니다.
    /// </summary>
    private void ApplyStartupTaskState(StartupTaskState state, StartupTaskMessageMode messageMode)
    {
        switch (state)
        {
            case StartupTaskState.Enabled:
                SetStartupRegistrationToggleState(isOn: true, isEnabled: true);
                StartupRegistrationMessage = string.Empty;
                break;

            case StartupTaskState.Disabled:
                SetStartupRegistrationToggleState(isOn: false, isEnabled: true);
                StartupRegistrationMessage = string.Empty;
                break;

            case StartupTaskState.DisabledByUser:
                SetStartupRegistrationToggleState(isOn: false, isEnabled: true);
                StartupRegistrationMessage = messageMode == StartupTaskMessageMode.Load
                    ? "Windows에서 시작 프로그램이 꺼져 있습니다. 자동 실행이 필요하면 Windows 시작 앱 설정에서 다시 켜주세요."
                    : "자동 실행을 켜지 못했습니다. Windows에서 시작 프로그램이 꺼져 있으면 앱에서 다시 켤 수 없습니다.";
                break;

            case StartupTaskState.DisabledByPolicy:
                SetStartupRegistrationToggleState(isOn: false, isEnabled: false);
                StartupRegistrationMessage = "자동 실행은 현재 Windows 정책 또는 실행 환경에서 허용되지 않습니다.";
                break;

            case StartupTaskState.EnabledByPolicy:
                SetStartupRegistrationToggleState(isOn: true, isEnabled: false);
                StartupRegistrationMessage = "자동 실행은 현재 Windows 정책으로 항상 켜져 있어 앱에서 변경할 수 없습니다.";
                break;

            default:
                ApplyStartupRegistrationFailure("자동 실행 상태를 확인하지 못했습니다. 현재는 이 기능을 사용할 수 없습니다.");
                break;
        }
    }

    /// <summary>
    /// 자동 실행 처리 실패 시 토글을 해제 상태로 되돌리고 설명 문구를 표시합니다.
    /// </summary>
    private void ApplyStartupRegistrationFailure(string message)
    {
        SetStartupRegistrationToggleState(isOn: false, isEnabled: true);
        StartupRegistrationMessage = message;
    }

    /// <summary>
    /// 자동 실행 토글 상태를 이벤트 재진입 없이 갱신합니다.
    /// </summary>
    private void SetStartupRegistrationToggleState(bool isOn, bool isEnabled)
    {
        _isUpdatingStartupRegistration = true;
        try
        {
            ToggleSwitch_StartupRegistration.IsOn = isOn;
            ToggleSwitch_StartupRegistration.IsEnabled = isEnabled;
        }
        finally
        {
            _isUpdatingStartupRegistration = false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// 시작 프로그램 상태를 안내 문구 관점에서 해석할 때의 문맥입니다.
    /// </summary>
    private enum StartupTaskMessageMode
    {
        Load,
        EnableRequest,
        DisableRequest,
    }
}
