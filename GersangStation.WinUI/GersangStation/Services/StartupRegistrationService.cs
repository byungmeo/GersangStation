using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace GersangStation.Services;

/// <summary>
/// Windows 정식 시작 앱 등록을 관리합니다.
/// </summary>
public sealed class StartupRegistrationService
{
    private const string StartupTaskId = "GersangStationStartup";

    /// <summary>
    /// 바로가기에서 사용할 아이콘 경로입니다.
    /// </summary>
    public string DesktopShortcutIconPath => GetSourceIconPath();

    /// <summary>
    /// 현재 사용자에 대한 Windows 시작 앱 등록 상태를 확인합니다.
    /// </summary>
    public async Task<StartupRegistrationState> GetStateAsync()
    {
        try
        {
            StartupTask startupTask = await StartupTask.GetAsync(StartupTaskId);
            return startupTask.State switch
            {
                StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy => new StartupRegistrationState(true, string.Empty),
                StartupTaskState.DisabledByUser => new StartupRegistrationState(false, "자동 실행이 Windows 설정에서 사용자에 의해 해제되어 있습니다."),
                StartupTaskState.DisabledByPolicy => new StartupRegistrationState(false, "자동 실행이 Windows 정책으로 차단되어 있습니다."),
                _ => new StartupRegistrationState(false, string.Empty)
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to query startup task state: {ex}");
            return new StartupRegistrationState(false, "Windows 시작 앱 상태를 확인하지 못했습니다.");
        }
    }

    /// <summary>
    /// 현재 사용자 로그인 시 앱이 시작되도록 Windows 시작 앱에 등록합니다.
    /// </summary>
    public async Task<StartupRegistrationOperationResult> EnableAsync()
    {
        try
        {
            StartupTask startupTask = await StartupTask.GetAsync(StartupTaskId);
            if (startupTask.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy)
                return new StartupRegistrationOperationResult(true, string.Empty);

            if (startupTask.State == StartupTaskState.DisabledByPolicy)
                return new StartupRegistrationOperationResult(false, "자동 실행이 Windows 정책으로 차단되어 있습니다.");

            StartupTaskState requestedState = await startupTask.RequestEnableAsync();
            return requestedState switch
            {
                StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy => new StartupRegistrationOperationResult(true, string.Empty),
                StartupTaskState.DisabledByUser => new StartupRegistrationOperationResult(false, "Windows 설정에서 시작 앱 사용을 허용해야 자동 실행을 등록할 수 있습니다."),
                StartupTaskState.DisabledByPolicy => new StartupRegistrationOperationResult(false, "자동 실행이 Windows 정책으로 차단되어 있습니다."),
                _ => new StartupRegistrationOperationResult(false, "Windows 시작 앱 등록을 완료하지 못했습니다.")
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to enable startup task: {ex}");
            return new StartupRegistrationOperationResult(false, "Windows 시작 앱 등록을 변경하지 못했습니다.");
        }
    }

    /// <summary>
    /// Windows 시작 앱 등록을 해제합니다.
    /// </summary>
    public async Task<StartupRegistrationOperationResult> DisableAsync()
    {
        try
        {
            StartupTask startupTask = await StartupTask.GetAsync(StartupTaskId);
            if (startupTask.State == StartupTaskState.EnabledByPolicy)
                return new StartupRegistrationOperationResult(false, "자동 실행이 Windows 정책으로 강제로 켜져 있어 해제할 수 없습니다.");

            startupTask.Disable();
            StartupTaskState state = startupTask.State;
            return state switch
            {
                StartupTaskState.Disabled or StartupTaskState.DisabledByUser or StartupTaskState.DisabledByPolicy => new StartupRegistrationOperationResult(true, string.Empty),
                _ => new StartupRegistrationOperationResult(false, "Windows 시작 앱 등록을 해제하지 못했습니다.")
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to disable startup task: {ex}");
            return new StartupRegistrationOperationResult(false, "Windows 시작 앱 등록을 변경하지 못했습니다.");
        }
    }

    private static string GetSourceIconPath()
        => Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "GersangStationShortcut.ico");
}

/// <summary>
/// Windows 시작 앱의 현재 등록 상태입니다.
/// </summary>
public readonly record struct StartupRegistrationState(bool IsEnabled, string Message);

/// <summary>
/// 시작 프로그램 구성 변경 결과입니다.
/// </summary>
public readonly record struct StartupRegistrationOperationResult(bool Success, string Message);
