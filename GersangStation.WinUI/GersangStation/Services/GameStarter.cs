using Core.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GersangStation.Services;

public enum GameClientState
{
    Available,
    Starting,
    Running,
    RetryCooldown
}

public readonly record struct GameClientSlot(GameServer Server, int ClientIndex);

public sealed record GameStartPayload(string Id, string Password, string? AccountId)
{
    public string ToLauncherArguments()
        => AccountId is not null
            ? $"{Id}\t{Password}\t{AccountId}"
            : $"{Id}\t{Password}";
}

public sealed record GameClientActivitySnapshot(
    GameServer Server,
    int ClientIndex,
    string AccountId,
    string InstallPath,
    GameClientState State,
    DateTimeOffset StartedAt,
    int? LauncherProcessId,
    int? GameProcessId);

/// <summary>
/// 외부 런처 실행과 Gersang.exe 추적을 전담합니다.
/// </summary>
public sealed partial class GameStarter : IDisposable, INotifyPropertyChanged
{
    private const int MaxConcurrentGames = 3;
    private const string LauncherExecutableName = "Run.exe";
    private const string GameExecutableName = "Gersang.exe";
    private const string GameProcessName = "Gersang";
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan DiscoveryGracePeriod = TimeSpan.FromSeconds(5);

    private readonly object _syncRoot = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Task _backgroundMonitorTask;
    // 세션은 시작한 서버 정보를 유지해야 하므로 서버+클라번호로 저장하되,
    // 실행 버튼 잠금 정책은 clientIndex 기준으로 서버와 무관하게 적용합니다.
    private readonly Dictionary<GameClientSlot, LaunchSession> _sessionsBySlot = [];
    private readonly Dictionary<GameClientSlot, DateTimeOffset> _retryCooldownUntilBySlot = [];
    private readonly HashSet<nint> _knownDialogWindows = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public GameStarter()
    {
        _backgroundMonitorTask = Task.Run(() => BackgroundMonitorLoopAsync(_disposeCts.Token));
    }

    public IReadOnlyList<GameClientActivitySnapshot> ActiveClients
    {
        get
        {
            lock (_syncRoot)
            {
                return _sessionsBySlot.Values
                    .OrderBy(session => session.Slot.Server)
                    .ThenBy(session => session.Slot.ClientIndex)
                    .Select(ToSnapshot)
                    .ToArray();
            }
        }
    }

    public int ActiveClientCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _sessionsBySlot.Count;
            }
        }
    }

    public int RunningClientCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _sessionsBySlot.Values.Count(session => session.State == GameClientState.Running);
            }
        }
    }

    /// <summary>
    /// 서버와 무관하게 해당 계정이 이미 어떤 클라이언트에서 사용 중인지 확인합니다.
    /// </summary>
    public bool IsAccountActive(string? accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            return false;

        lock (_syncRoot)
        {
            return _sessionsBySlot.Values.Any(session => session.AccountId.Equals(accountId, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// 특정 실행 버튼 번호의 상태를 계산합니다.
    /// 같은 번호의 버튼은 서버가 달라도 하나의 전역 슬롯처럼 잠금 상태를 공유합니다.
    /// </summary>
    public GameClientState GetClientState(GameServer server, int clientIndex, string? accountId)
    {
        GameClientSlot slot = new(server, clientIndex);

        lock (_syncRoot)
        {
            if (_sessionsBySlot.TryGetValue(slot, out LaunchSession? slotSession))
                return slotSession.State;

            LaunchSession? sharedButtonSession = GetSessionByClientIndexUnsafe(clientIndex, slot);
            if (sharedButtonSession is not null)
                return sharedButtonSession.State;

            if (_retryCooldownUntilBySlot.TryGetValue(slot, out DateTimeOffset retryCooldownUntil))
            {
                if (retryCooldownUntil > DateTimeOffset.Now)
                    return GameClientState.RetryCooldown;

                _retryCooldownUntilBySlot.Remove(slot);
            }

            if (_sessionsBySlot.Count >= MaxConcurrentGames)
                return _sessionsBySlot.Values.Any(session => session.State == GameClientState.Starting)
                    ? GameClientState.Starting
                    : GameClientState.Running;
        }

        return GameClientState.Available;
    }

    /// <summary>
    /// 지정 슬롯의 재시도 제한 남은 시간을 반환합니다.
    /// </summary>
    public TimeSpan? GetRetryCooldownRemaining(GameServer server, int clientIndex)
    {
        GameClientSlot slot = new(server, clientIndex);

        lock (_syncRoot)
        {
            if (!_retryCooldownUntilBySlot.TryGetValue(slot, out DateTimeOffset retryCooldownUntil))
                return null;

            TimeSpan remaining = retryCooldownUntil - DateTimeOffset.Now;
            if (remaining > TimeSpan.Zero)
                return remaining;

            _retryCooldownUntilBySlot.Remove(slot);
        }

        NotifySessionsChanged();
        return null;
    }

    /// <summary>
    /// 인증 실패 등으로 잠시 재시도를 막아야 할 때 슬롯별 쿨다운을 시작합니다.
    /// </summary>
    public void StartRetryCooldown(GameServer server, int clientIndex, TimeSpan duration, string reason)
    {
        if (clientIndex < 0 || clientIndex >= MaxConcurrentGames || duration <= TimeSpan.Zero)
            return;

        GameClientSlot slot = new(server, clientIndex);
        DateTimeOffset cooldownUntil = DateTimeOffset.Now.Add(duration);

        lock (_syncRoot)
        {
            _retryCooldownUntilBySlot[slot] = cooldownUntil;
        }

        Debug.WriteLine(
            $"[GameStarter] 재시도 쿨다운 시작. server:{server}, clientIndex:{clientIndex}, until:{cooldownUntil:O}, reason:{reason}");
        NotifySessionsChanged();
    }

    /// <summary>
    /// 실제 실행 전 단계에서 버튼 번호를 선점해 즉시 "켜는 중" 상태로 전환합니다.
    /// 같은 번호의 버튼은 서버가 달라도 동시에 사용할 수 없습니다.
    /// </summary>
    public bool TryBeginStart(GameServer server, int clientIndex, string clientInstallPath, string? accountId)
    {
        if (clientIndex < 0 || clientIndex >= MaxConcurrentGames)
            return false;

        string normalizedAccountId = accountId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedAccountId))
            return false;

        GameClientSlot slot = new(server, clientIndex);
        LaunchSession? replacedSession = null;

        lock (_syncRoot)
        {
            if (_sessionsBySlot.TryGetValue(slot, out LaunchSession? existingSlotSession))
            {
                if (existingSlotSession.State == GameClientState.Running)
                    return false;

                if (existingSlotSession.State == GameClientState.Starting &&
                    existingSlotSession.AccountId.Equals(normalizedAccountId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existingSlotSession.InstallPath, clientInstallPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                replacedSession = existingSlotSession;
                _sessionsBySlot.Remove(slot);
            }

            LaunchSession? sharedButtonSession = GetSessionByClientIndexUnsafe(clientIndex, slot);
            if (sharedButtonSession is not null)
            {
                if (replacedSession is not null)
                    _sessionsBySlot[slot] = replacedSession;
                return false;
            }

            // 계정 중복은 서버가 달라도 허용하지 않습니다.
            LaunchSession? accountSession = _sessionsBySlot.Values.FirstOrDefault(
                session => session.AccountId.Equals(normalizedAccountId, StringComparison.OrdinalIgnoreCase));
            if (accountSession is not null)
            {
                if (replacedSession is not null)
                    _sessionsBySlot[slot] = replacedSession;
                return false;
            }

            if (_sessionsBySlot.Count >= MaxConcurrentGames)
            {
                if (replacedSession is not null)
                    _sessionsBySlot[slot] = replacedSession;
                return false;
            }

            _sessionsBySlot[slot] = new LaunchSession(
                slot,
                normalizedAccountId,
                clientInstallPath,
                CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token));
        }

        CancelSession(replacedSession);
        NotifySessionsChanged();
        return true;
    }

    /// <summary>
    /// 아직 Gersang.exe가 붙지 않은 "켜는 중" 세션을 취소합니다.
    /// </summary>
    public void CancelStart(GameServer server, int clientIndex, string reason)
    {
        GameClientSlot slot = new(server, clientIndex);
        LaunchSession? removedSession = null;

        lock (_syncRoot)
        {
            if (_sessionsBySlot.TryGetValue(slot, out LaunchSession? session) &&
                session.State == GameClientState.Starting)
            {
                removedSession = session;
                _sessionsBySlot.Remove(slot);
            }
        }

        if (removedSession is null)
            return;

        Debug.WriteLine(
            $"[GameStarter] 시작 준비 취소. server:{server}, clientIndex:{clientIndex}, account:{removedSession.AccountId}, reason:{reason}");
        removedSession.Cancellation.Cancel();
        removedSession.Cancellation.Dispose();
        NotifySessionsChanged();
    }

    /// <summary>
    /// WebView에서 받은 payload로 외부 런처를 실행하고, 이후 시작된 Gersang.exe를 현재 세션에 연결합니다.
    /// </summary>
    public async Task<bool> StartAsync(
        GameServer server,
        int clientIndex,
        string clientInstallPath,
        string selectedAccountId,
        GameStartPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (clientIndex < 0 || clientIndex >= MaxConcurrentGames)
            return false;

        string accountId = selectedAccountId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(accountId))
            return false;

        if (!TryBeginStart(server, clientIndex, clientInstallPath, accountId))
            return false;

        string launcherPath = Path.Combine(clientInstallPath, LauncherExecutableName);
        if (!File.Exists(launcherPath))
        {
            Debug.WriteLine($"[GameStarter] Run.exe를 찾을 수 없습니다. path:{launcherPath}");
            CancelStart(server, clientIndex, "Run.exe 파일 없음");
            return false;
        }

        GameClientSlot slot = new(server, clientIndex);
        LaunchSession? launchSession = GetSession(slot, accountId);
        if (launchSession is null)
            return false;
        using CancellationTokenRegistration cancellationRegistration = cancellationToken.CanBeCanceled
            ? cancellationToken.Register(() => CancelStart(server, clientIndex, "상위 작업 취소"))
            : default;

        HashSet<int> existingGameProcessIds = SnapshotCurrentGameProcessIds();
        using Process launcherProcess = new();
        launcherProcess.StartInfo.FileName = launcherPath;
        launcherProcess.StartInfo.Arguments = payload.ToLauncherArguments();
        launcherProcess.StartInfo.WorkingDirectory = clientInstallPath;
        launcherProcess.StartInfo.UseShellExecute = true;
        launcherProcess.StartInfo.Verb = "runas";

        try
        {
            if (!launcherProcess.Start())
            {
                Debug.WriteLine("[GameStarter] Run.exe 시작에 실패했습니다.");
                RemoveSession(launchSession, "Run.exe 시작 실패");
                return false;
            }

            UpdateSessionAfterLauncherStart(
                launchSession,
                TryGetProcessStartTime(launcherProcess) ?? DateTimeOffset.Now,
                launcherProcess.Id);

            Debug.WriteLine(
                $"[GameStarter] Run.exe 시작. server:{server}, clientIndex:{clientIndex}, pid:{launcherProcess.Id}, account:{accountId}");

            await DiscoverGameProcessForSessionAsync(launchSession, existingGameProcessIds);
            return true;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine(
                $"[GameStarter] 게임 시작 추적 취소. server:{server}, clientIndex:{clientIndex}, account:{accountId}");
            RemoveSession(launchSession, "게임 시작 추적 취소");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameStarter] Run.exe 실행 중 예외가 발생했습니다. {ex}");
            RemoveSession(launchSession, "Run.exe 실행 예외");
            return false;
        }
    }

    /// <summary>
    /// 추적 중인 작업과 백그라운드 모니터링을 정리합니다.
    /// </summary>
    public void Dispose()
    {
        _disposeCts.Cancel();

        LaunchSession[] sessions;
        lock (_syncRoot)
        {
            sessions = _sessionsBySlot.Values.ToArray();
            _sessionsBySlot.Clear();
            _retryCooldownUntilBySlot.Clear();
            _knownDialogWindows.Clear();
        }

        foreach (LaunchSession session in sessions)
        {
            session.Cancellation.Cancel();
            session.Cancellation.Dispose();
        }

        try
        {
            _backgroundMonitorTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _disposeCts.Dispose();
        }
    }

    /// <summary>
    /// 런처/게임 프로세스 생존 여부와 대화상자를 주기적으로 확인합니다.
    /// </summary>
    private async Task BackgroundMonitorLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                RefreshSessions();
                RefreshRetryCooldowns();
                LogDialogWindows();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GameStarter] 백그라운드 모니터링 중 예외가 발생했습니다. {ex}");
            }

            await Task.Delay(MonitorInterval, cancellationToken);
        }
    }

    /// <summary>
    /// 외부 런처가 새로 띄운 Gersang.exe를 찾아 현재 세션의 실행 상태로 승격합니다.
    /// </summary>
    private async Task DiscoverGameProcessForSessionAsync(LaunchSession session, HashSet<int> existingGameProcessIds)
    {
        bool discoveredGame = false;
        DateTimeOffset? launcherExitedAt = null;

        while (true)
        {
            session.Cancellation.Token.ThrowIfCancellationRequested();

            discoveredGame |= TryAttachGameProcess(session, existingGameProcessIds);

            if (!session.LauncherProcessId.HasValue || !IsProcessAlive(session.LauncherProcessId.Value))
            {
                if (launcherExitedAt is null)
                {
                    launcherExitedAt = DateTimeOffset.Now;
                    Debug.WriteLine(
                        $"[GameStarter] Run.exe 종료. server:{session.Slot.Server}, clientIndex:{session.Slot.ClientIndex}, launcherPid:{session.LauncherProcessId?.ToString() ?? "unknown"}");
                    ClearLauncherProcess(session);
                }
            }

            if (session.State == GameClientState.Running)
                return;

            if (launcherExitedAt is not null && DateTimeOffset.Now - launcherExitedAt.Value >= DiscoveryGracePeriod)
                break;

            await Task.Delay(MonitorInterval, session.Cancellation.Token);
        }

        if (!discoveredGame)
        {
            RemoveSession(session, "Gersang.exe 시작 감지 실패");
        }
    }

    /// <summary>
    /// 설치 경로와 시작 시각을 기준으로 현재 세션에 해당하는 Gersang.exe를 연결합니다.
    /// </summary>
    private bool TryAttachGameProcess(LaunchSession session, HashSet<int> existingGameProcessIds)
    {
        if (session.State != GameClientState.Starting)
            return false;

        string expectedExecutablePath = Path.Combine(session.InstallPath, GameExecutableName);
        Process[] processes = Process.GetProcessesByName(GameProcessName);
        foreach (Process process in processes)
        {
            using (process)
            {
                if (existingGameProcessIds.Contains(process.Id) || IsProcessAlreadyTracked(process.Id))
                    continue;

                DateTimeOffset? processStartedAt = TryGetProcessStartTime(process);
                if (processStartedAt is null || processStartedAt.Value < session.StartedAt.AddSeconds(-1))
                    continue;

                string? executablePath = TryGetExecutablePath(process);
                if (!string.IsNullOrWhiteSpace(executablePath) &&
                    !string.Equals(executablePath, expectedExecutablePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!PromoteSessionToRunning(session, process.Id, processStartedAt.Value))
                    continue;

                existingGameProcessIds.Add(process.Id);
                Debug.WriteLine(
                    $"[GameStarter] Gersang.exe 추적 시작. server:{session.Slot.Server}, clientIndex:{session.Slot.ClientIndex}, pid:{process.Id}, account:{session.AccountId}, path:{session.InstallPath}");
                return true;
            }
        }

        return false;
    }

    private bool PromoteSessionToRunning(LaunchSession session, int processId, DateTimeOffset processStartedAt)
    {
        lock (_syncRoot)
        {
            if (!_sessionsBySlot.TryGetValue(session.Slot, out LaunchSession? currentSession) ||
                !ReferenceEquals(currentSession, session) ||
                currentSession.State != GameClientState.Starting)
            {
                return false;
            }

            currentSession.State = GameClientState.Running;
            currentSession.GameProcessId = processId;
            currentSession.StartedAt = processStartedAt;
        }

        NotifySessionsChanged();
        return true;
    }

    /// <summary>
    /// 실행 중 세션의 종료 여부를 반영해 상태를 최신화합니다.
    /// </summary>
    private void RefreshSessions()
    {
        LaunchSession[] sessions;
        lock (_syncRoot)
        {
            sessions = _sessionsBySlot.Values.ToArray();
        }

        foreach (LaunchSession session in sessions)
        {
            if (session.LauncherProcessId.HasValue && !IsProcessAlive(session.LauncherProcessId.Value))
                ClearLauncherProcess(session);

            if (session.State != GameClientState.Running || !session.GameProcessId.HasValue)
                continue;

            if (IsProcessAlive(session.GameProcessId.Value))
                continue;

            RemoveSession(session, "Gersang.exe 종료 감지");
        }
    }

    private void RefreshRetryCooldowns()
    {
        bool changed = false;
        DateTimeOffset now = DateTimeOffset.Now;

        lock (_syncRoot)
        {
            if (_retryCooldownUntilBySlot.Count == 0)
                return;

            foreach (GameClientSlot slot in _retryCooldownUntilBySlot
                         .Where(pair => pair.Value <= now)
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                _retryCooldownUntilBySlot.Remove(slot);
                changed = true;
            }
        }

        if (changed)
            NotifySessionsChanged();
    }

    private void UpdateSessionAfterLauncherStart(LaunchSession session, DateTimeOffset startedAt, int launcherProcessId)
    {
        lock (_syncRoot)
        {
            if (!_sessionsBySlot.TryGetValue(session.Slot, out LaunchSession? currentSession) ||
                !ReferenceEquals(currentSession, session))
            {
                return;
            }

            currentSession.StartedAt = startedAt;
            currentSession.LauncherProcessId = launcherProcessId;
        }

        NotifySessionsChanged();
    }

    private void ClearLauncherProcess(LaunchSession session)
    {
        bool changed = false;
        lock (_syncRoot)
        {
            if (!_sessionsBySlot.TryGetValue(session.Slot, out LaunchSession? currentSession) ||
                !ReferenceEquals(currentSession, session) ||
                currentSession.LauncherProcessId is null)
            {
                return;
            }

            currentSession.LauncherProcessId = null;
            changed = true;
        }

        if (changed)
            NotifySessionsChanged();
    }

    private void RemoveSession(LaunchSession session, string reason)
    {
        bool removed = false;
        lock (_syncRoot)
        {
            if (_sessionsBySlot.TryGetValue(session.Slot, out LaunchSession? currentSession) &&
                ReferenceEquals(currentSession, session))
            {
                _sessionsBySlot.Remove(session.Slot);
                removed = true;
            }
        }

        if (!removed)
            return;

        Debug.WriteLine(
            $"[GameStarter] 세션 제거. server:{session.Slot.Server}, clientIndex:{session.Slot.ClientIndex}, account:{session.AccountId}, reason:{reason}");
        session.Cancellation.Cancel();
        session.Cancellation.Dispose();
        NotifySessionsChanged();
    }

    private void CancelSession(LaunchSession? session)
    {
        if (session is null)
            return;

        session.Cancellation.Cancel();
        session.Cancellation.Dispose();
    }

    private bool IsProcessAlreadyTracked(int processId)
    {
        lock (_syncRoot)
        {
            return _sessionsBySlot.Values.Any(session => session.GameProcessId == processId);
        }
    }

    private LaunchSession? GetSessionByClientIndexUnsafe(int clientIndex, GameClientSlot? excludedSlot = null)
    {
        foreach (LaunchSession session in _sessionsBySlot.Values)
        {
            if (session.Slot.ClientIndex != clientIndex)
                continue;

            if (excludedSlot.HasValue && session.Slot == excludedSlot.Value)
                continue;

            return session;
        }

        return null;
    }

    private LaunchSession? GetSession(GameClientSlot slot, string accountId)
    {
        lock (_syncRoot)
        {
            if (_sessionsBySlot.TryGetValue(slot, out LaunchSession? session) &&
                session.AccountId.Equals(accountId, StringComparison.OrdinalIgnoreCase))
            {
                return session;
            }
        }

        return null;
    }

    /// <summary>
    /// 버튼 상태 계산에 필요한 공개 속성 변경을 한 번에 알립니다.
    /// </summary>
    private void NotifySessionsChanged()
    {
        OnPropertyChanged(nameof(ActiveClients));
        OnPropertyChanged(nameof(ActiveClientCount));
        OnPropertyChanged(nameof(RunningClientCount));
    }

    private void LogDialogWindows()
    {
        HashSet<int> targetProcessIds;
        lock (_syncRoot)
        {
            targetProcessIds = _sessionsBySlot.Values
                .SelectMany(session => new[] { session.LauncherProcessId, session.GameProcessId })
                .Where(processId => processId.HasValue)
                .Select(processId => processId!.Value)
                .ToHashSet();

            if (targetProcessIds.Count == 0)
            {
                _knownDialogWindows.Clear();
                return;
            }
        }

        List<DialogWindowSnapshot> visibleDialogs = [];
        EnumWindows((hwnd, _) =>
        {
            if (!TryCreateDialogWindowSnapshot(hwnd, targetProcessIds, out DialogWindowSnapshot? snapshot))
                return true;

            visibleDialogs.Add(snapshot!);
            return true;
        }, IntPtr.Zero);

        HashSet<nint> currentDialogWindows = [.. visibleDialogs.Select(dialog => dialog.Handle)];
        List<DialogWindowSnapshot> newDialogs;

        lock (_syncRoot)
        {
            newDialogs = [.. visibleDialogs.Where(dialog => !_knownDialogWindows.Contains(dialog.Handle))];
            _knownDialogWindows.Clear();
            foreach (nint hwnd in currentDialogWindows)
            {
                _knownDialogWindows.Add(hwnd);
            }
        }

        foreach (DialogWindowSnapshot dialog in newDialogs)
        {
            Debug.WriteLine(dialog.ToDebugString());
        }
    }

    private static bool TryCreateDialogWindowSnapshot(
        nint hwnd,
        HashSet<int> targetProcessIds,
        out DialogWindowSnapshot? snapshot)
    {
        snapshot = null;

        if (!IsWindowVisible(hwnd))
            return false;

        _ = GetWindowThreadProcessId(hwnd, out int processId);
        if (!targetProcessIds.Contains(processId))
            return false;

        string className = GetClassNameSafe(hwnd);
        if (!string.Equals(className, "#32770", StringComparison.Ordinal) && GetWindow(hwnd, GW_OWNER) == IntPtr.Zero)
            return false;

        string title = GetWindowTextSafe(hwnd);
        List<string> lines = GetChildWindowTexts(hwnd);
        if (string.IsNullOrWhiteSpace(title) && lines.Count == 0)
            return false;

        snapshot = new DialogWindowSnapshot(hwnd, processId, className, title, lines);
        return true;
    }

    private static List<string> GetChildWindowTexts(nint parentHwnd)
    {
        List<string> lines = [];
        EnumChildWindows(parentHwnd, (childHwnd, _) =>
        {
            string text = GetWindowTextSafe(childHwnd);
            if (string.IsNullOrWhiteSpace(text))
                return true;

            string className = GetClassNameSafe(childHwnd);
            lines.Add($"{className}:{text}");
            return true;
        }, IntPtr.Zero);
        return lines;
    }

    private static HashSet<int> SnapshotCurrentGameProcessIds()
    {
        HashSet<int> processIds = [];
        Process[] processes = Process.GetProcessesByName(GameProcessName);
        foreach (Process process in processes)
        {
            using (process)
            {
                processIds.Add(process.Id);
            }
        }

        return processIds;
    }

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static DateTimeOffset? TryGetProcessStartTime(Process process)
    {
        try
        {
            return process.StartTime;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private static GameClientActivitySnapshot ToSnapshot(LaunchSession session)
        => new(
            session.Slot.Server,
            session.Slot.ClientIndex,
            session.AccountId,
            session.InstallPath,
            session.State,
            session.StartedAt,
            session.LauncherProcessId,
            session.GameProcessId);

    private void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class LaunchSession
    {
        public LaunchSession(
            GameClientSlot slot,
            string accountId,
            string installPath,
            CancellationTokenSource cancellation)
        {
            Slot = slot;
            AccountId = accountId;
            InstallPath = installPath;
            Cancellation = cancellation;
            State = GameClientState.Starting;
            StartedAt = DateTimeOffset.Now;
        }

        public GameClientSlot Slot { get; }
        public string AccountId { get; }
        public string InstallPath { get; }
        public CancellationTokenSource Cancellation { get; }
        public GameClientState State { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public int? LauncherProcessId { get; set; }
        public int? GameProcessId { get; set; }
    }

    private sealed record DialogWindowSnapshot(
        nint Handle,
        int ProcessId,
        string ClassName,
        string Title,
        IReadOnlyList<string> Lines)
    {
        public string ToDebugString()
        {
            string title = string.IsNullOrWhiteSpace(Title) ? "<empty>" : Title;
            string content = Lines.Count == 0 ? "<empty>" : string.Join(" | ", Lines);
            return $"[GameStarter] 대화상자 감지. pid:{ProcessId}, hwnd:0x{Handle.ToInt64():X}, class:{ClassName}, title:{title}, content:{content}";
        }
    }

    private delegate bool EnumWindowsProc(nint hwnd, nint lParam);

    private const uint GW_OWNER = 4;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(nint hWndParent, EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out int lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint hWnd, uint uCmd);

    private static string GetWindowTextSafe(nint hwnd)
    {
        int length = GetWindowTextLength(hwnd);
        if (length <= 0)
            return string.Empty;

        StringBuilder builder = new(length + 1);
        _ = GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString().Trim();
    }

    private static string GetClassNameSafe(nint hwnd)
    {
        StringBuilder builder = new(256);
        _ = GetClassName(hwnd, builder, builder.Capacity);
        return builder.ToString().Trim();
    }
}
