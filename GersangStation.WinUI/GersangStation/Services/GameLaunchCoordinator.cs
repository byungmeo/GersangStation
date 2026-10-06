using Core.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace GersangStation.Services;

public enum GameLaunchPhase { Preparing, Starting, Running, Failed, Canceled, Exited }

/// <summary>한 번의 실행 요청. 선택값과 취소 수명은 페이지/프리셋 변경과 무관합니다.</summary>
public sealed class GameLaunchAttempt
{
    private static long _nextId;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private GameLaunchPhase _phase = GameLaunchPhase.Preparing;

    public GameLaunchAttempt(GameServer server, int clientIndex, string accountId, string installPath)
    {
        Id = Interlocked.Increment(ref _nextId);
        Server = server;
        ClientIndex = clientIndex;
        AccountId = accountId.Trim();
        InstallPath = installPath;
        Token = _cancellation.Token;
    }

    public long Id { get; }
    public GameServer Server { get; }
    public int ClientIndex { get; }
    public string AccountId { get; }
    public string InstallPath { get; }
    public CancellationToken Token { get; }
    public GameLaunchPhase Phase { get { lock (_gate) return _phase; } }
    public bool IsPending => Phase is GameLaunchPhase.Preparing or GameLaunchPhase.Starting;
    public string? Reason { get; private set; }
    internal bool PreserveDocumentOnCancel { get; set; }
    internal event EventHandler? Changed;

    internal bool MarkStarting() => Transition(GameLaunchPhase.Starting, null);
    internal bool MarkRunning() => Transition(GameLaunchPhase.Running, null);
    internal bool Fail(string reason) => Transition(GameLaunchPhase.Failed, reason);
    internal bool Cancel(string reason = "사용자 취소") => Transition(GameLaunchPhase.Canceled, reason);
    internal bool MarkExited() => Transition(GameLaunchPhase.Exited, "게임 종료");

    private bool Transition(GameLaunchPhase phase, string? reason)
    {
        lock (_gate)
        {
            bool permitted = phase switch
            {
                GameLaunchPhase.Starting => _phase == GameLaunchPhase.Preparing,
                GameLaunchPhase.Running => _phase == GameLaunchPhase.Starting,
                GameLaunchPhase.Exited => _phase == GameLaunchPhase.Running,
                _ => _phase is GameLaunchPhase.Preparing or GameLaunchPhase.Starting
            };
            if (!permitted) return false;
            _phase = phase;
            Reason = reason;
        }

        // 상태를 먼저 공개합니다. 취소 콜백의 정리가 끝나기 전에도 버튼은 기본 상태입니다.
        try { Changed?.Invoke(this, EventArgs.Empty); }
        finally
        {
            if (phase is GameLaunchPhase.Failed or GameLaunchPhase.Canceled or GameLaunchPhase.Exited)
            {
                try { _cancellation.Cancel(); }
                finally { _cancellation.Dispose(); }
            }
        }
        return true;
    }
}

public sealed class GameLaunchButtonState : INotifyPropertyChanged
{
    private bool _busy, _running, _enabled = true;
    public bool IsBusy => _busy;
    public bool IsEnabled => _enabled;
    public Visibility ProgressVisibility => _busy ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IconVisibility => _busy ? Visibility.Collapsed : Visibility.Visible;
    public string Glyph => _running ? "\uE8FB" : "\uE768";
    public string ToolTip => _busy ? "게임 실행 취소" : _running ? "게임 실행 중" : "게임 실행";
    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Update(bool busy, bool running, bool enabled)
    {
        if ((_busy, _running, _enabled) == (busy, running, enabled)) return;
        (_busy, _running, _enabled) = (busy, running, enabled);
        foreach (string name in new[] { nameof(IsBusy), nameof(IsEnabled), nameof(ProgressVisibility),
            nameof(IconVisibility), nameof(Glyph), nameof(ToolTip) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>MainWindow 수명의 실행 버튼 상태. 페이지 활성 상태에 의존하지 않습니다.</summary>
public sealed class GameLaunchCoordinator : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherQueue _dispatcher;
    private readonly WebViewManager _browser;
    private readonly GameLaunchAttempt?[] _attempts = new GameLaunchAttempt?[3];
    private readonly bool[] _slotEnabled = [true, true, true];
    private bool _disposed;

    public GameLaunchCoordinator(DispatcherQueue dispatcher, WebViewManager browser)
    {
        (_dispatcher, _browser) = (dispatcher, browser);
        browser.PropertyChanged += OnBrowserChanged;
    }

    public GameLaunchButtonState Client1 { get; } = new();
    public GameLaunchButtonState Client2 { get; } = new();
    public GameLaunchButtonState Client3 { get; } = new();
    public bool CanLogin => !HasPending && !_browser.TryingLogin && !_browser.TryingLogout;
    private bool HasPending => Array.Exists(_attempts, attempt => attempt?.IsPending == true);
    public event PropertyChangedEventHandler? PropertyChanged;

    internal void SetSlotEnabled(int index, bool enabled)
    {
        _slotEnabled[index] = enabled;
        Refresh();
    }

    internal async Task ToggleAsync(GameServer server, int index, string accountId, string installPath,
        Func<GameLaunchAttempt, Task<bool>> execute)
    {
        if (_disposed) return;
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _attempts.Length);
        if (_attempts[index] is { IsPending: true } pending)
        {
            pending.Cancel();
            return;
        }
        if (_attempts[index]?.Phase == GameLaunchPhase.Running || HasPending || !_slotEnabled[index]) return;
        if (_browser.TryingLogin || _browser.TryingLogout) return;

        if (_attempts[index] is { } previous) previous.Changed -= OnAttemptChanged;
        var attempt = new GameLaunchAttempt(server, index, accountId, installPath);
        _attempts[index] = attempt;
        attempt.Changed += OnAttemptChanged;
        Refresh();
        try
        {
            if (!await execute(attempt)) attempt.Fail("실행 전 검사 또는 실행 준비 실패");
        }
        catch (OperationCanceledException) when (attempt.Token.IsCancellationRequested) { }
        catch
        {
            attempt.Fail("실행 중 예외");
            throw;
        }
    }

    private void OnAttemptChanged(object? sender, EventArgs e)
    {
        void Apply()
        {
            if (_disposed || sender is not GameLaunchAttempt attempt
                || !ReferenceEquals(_attempts[attempt.ClientIndex], attempt)) return;
            Refresh();
            if (attempt.Phase is GameLaunchPhase.Canceled or GameLaunchPhase.Failed)
                _browser.CancelLaunchAttempt(attempt.Server, attempt.ClientIndex, attempt.Reason!, attempt);
        }
        if (_dispatcher.HasThreadAccess) Apply();
        else _dispatcher.TryEnqueue(Apply);
    }

    private void Refresh()
    {
        bool pending = HasPending;
        GameLaunchButtonState[] buttons = [Client1, Client2, Client3];
        for (int i = 0; i < buttons.Length; i++)
        {
            bool busy = _attempts[i]?.IsPending == true;
            bool running = _attempts[i]?.Phase == GameLaunchPhase.Running;
            buttons[i].Update(busy, running, busy || (!running && !pending
                && !_browser.TryingLogin && !_browser.TryingLogout && _slotEnabled[i]));
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanLogin)));
    }

    public void Dispose()
    {
        _browser.PropertyChanged -= OnBrowserChanged;
        foreach (GameLaunchAttempt? attempt in _attempts)
        {
            if (attempt is null) continue;
            attempt.Cancel("창 종료");
            attempt.Changed -= OnAttemptChanged;
        }
        _disposed = true;
    }

    private void OnBrowserChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(WebViewManager.TryingLogin) or nameof(WebViewManager.TryingLogout))) return;
        if (_dispatcher.HasThreadAccess) { if (!_disposed) Refresh(); }
        else _dispatcher.TryEnqueue(() => { if (!_disposed) Refresh(); });
    }
}
