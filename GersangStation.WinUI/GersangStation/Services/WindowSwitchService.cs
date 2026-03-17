using GersangStation.Diagnostics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace GersangStation.Services;

/// <summary>
/// Polls the foreground window and the fixed Alt+` hotkey to cycle launched game windows with a short TopMost pulse.
/// </summary>
public sealed partial class WindowSwitchService : IDisposable
{
    private const int VkLButton = 0x01;
    private const int VkOem3 = 0xC0;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLMenu = 0xA4;
    private const int VkRMenu = 0xA5;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;
    private const uint GaRootOwner = 3;
    private const uint GwOwner = 4;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZOrder = 0x0200;
    private static readonly nint HwndTop = IntPtr.Zero;
    private static readonly nint HwndTopMost = new(-1);
    private static readonly nint HwndNoTopMost = new(-2);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    private readonly object _syncRoot = new();
    private readonly GameStarter _gameStarter;
    private Timer? _monitorTimer;
    private bool _isEnabled;
    private bool _isDisposed;
    private bool _isBrowsing;
    private bool _wasLeftButtonDown;
    private bool _wasCycleKeyDown;
    private int _isPolling;
    private nint _lastForegroundWindow;
    private int? _currentClientIndex;
    private int? _browsingSourceClientIndex;
    private nint _browsingSourceWindowHandle;
    private bool _isBrowsingArmed;

    public event EventHandler<bool>? BrowsingStateChanged;

    /// <summary>
    /// Creates the service and optionally starts polling immediately.
    /// </summary>
    public WindowSwitchService(GameStarter gameStarter, bool isEnabled)
    {
        _gameStarter = gameStarter ?? throw new ArgumentNullException(nameof(gameStarter));
        _gameStarter.PropertyChanged += OnGameStarterPropertyChanged;
        SetEnabled(isEnabled);
    }

    /// <summary>
    /// Gets whether the service is currently waiting for the user to pick a window after cycling.
    /// </summary>
    public bool IsBrowsing
    {
        get
        {
            lock (_syncRoot)
            {
                return _isBrowsing;
            }
        }
    }

    /// <summary>
    /// Enables or disables Alt+`-based window cycling.
    /// </summary>
    public void SetEnabled(bool isEnabled)
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            bool effectiveEnabled = isEnabled && App.IsRunningAsAdministrator;
            if (_isEnabled == effectiveEnabled)
                return;

            _isEnabled = effectiveEnabled;
            if (effectiveEnabled)
            {
                StartMonitor_NoLock();
                return;
            }

            StopMonitor_NoLock();
        }
    }

    /// <summary>
    /// Stops polling and resets the current cycle state.
    /// </summary>
    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _isEnabled = false;
            _gameStarter.PropertyChanged -= OnGameStarterPropertyChanged;
            StopMonitor_NoLock();
        }
    }

    /// <summary>
    /// GameStarter state changes may invalidate the current cycle target immediately.
    /// </summary>
    private void OnGameStarterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(GameStarter.ActiveClients), StringComparison.Ordinal) &&
            !string.Equals(e.PropertyName, nameof(GameStarter.ActiveClientCount), StringComparison.Ordinal))
        {
            return;
        }

        IReadOnlyDictionary<int, SwitchClientTarget> activeClients = GetSwitchableClients();
        RefreshTrackedState(activeClients);
    }

    /// <summary>
    /// Polls the foreground window, active launched clients, and the Alt+` hotkey state.
    /// </summary>
    private void PollWindowSwitchState()
    {
        if (Interlocked.Exchange(ref _isPolling, 1) != 0)
            return;

        try
        {
            if (!IsMonitoringEnabled())
            {
                ResetTransientState();
                return;
            }

            IReadOnlyDictionary<int, SwitchClientTarget> activeClients = GetSwitchableClients();
            RefreshTrackedState(activeClients);

            nint foregroundWindow = GetStableForegroundWindow();
            HandleForegroundWindowChange(foregroundWindow, activeClients);

            bool isCycleKeyDown = IsKeyDown(VkOem3);
            bool isLeftButtonDown = IsKeyDown(VkLButton);
            bool shouldHandleHotkey;
            bool shouldHandleLeftClick;
            lock (_syncRoot)
            {
                shouldHandleHotkey = isCycleKeyDown && !_wasCycleKeyDown && IsAnyAltDown();
                _wasCycleKeyDown = isCycleKeyDown;
                shouldHandleLeftClick = isLeftButtonDown && !_wasLeftButtonDown;
                _wasLeftButtonDown = isLeftButtonDown;
            }

            ArmBrowsingAfterCycleKeyRelease(isCycleKeyDown);

            if (TryResolveBrowsingFromLeftClick(
                    shouldHandleLeftClick,
                    foregroundWindow,
                    activeClients))
            {
                return;
            }

            if (!shouldHandleHotkey || HasDisallowedModifierKeyPressed() || activeClients.Count < 2)
                return;

            CycleToNextClient(activeClients, foregroundWindow);
        }
        finally
        {
            Volatile.Write(ref _isPolling, 0);
        }
    }

    private bool IsMonitoringEnabled()
    {
        lock (_syncRoot)
        {
            return _isEnabled && !_isDisposed;
        }
    }

    private void StartMonitor_NoLock()
    {
        _monitorTimer ??= SafeExecution.StartHandledTimer(
            PollWindowSwitchState,
            TimeSpan.Zero,
            PollInterval,
            "WindowSwitchService.PollWindowSwitchState",
            isFatal: false);
    }

    private void StopMonitor_NoLock()
    {
        _monitorTimer?.Dispose();
        _monitorTimer = null;
        _lastForegroundWindow = IntPtr.Zero;
        _currentClientIndex = null;
        SetBrowsingState_NoLock(isBrowsing: false, sourceWindowHandle: IntPtr.Zero, sourceClientIndex: null);
        _wasLeftButtonDown = false;
        _wasCycleKeyDown = false;
    }

    private void ResetTransientState()
    {
        lock (_syncRoot)
        {
            _lastForegroundWindow = IntPtr.Zero;
            SetBrowsingState_NoLock(isBrowsing: false, sourceWindowHandle: IntPtr.Zero, sourceClientIndex: null);
            _wasLeftButtonDown = false;
            _wasCycleKeyDown = false;
        }
    }

    private IReadOnlyDictionary<int, SwitchClientTarget> GetSwitchableClients()
        => _gameStarter.ActiveClients
            .Where(snapshot => snapshot.State == GameClientState.Running && snapshot.GameProcessId.HasValue)
            .OrderBy(snapshot => snapshot.ClientIndex)
            .GroupBy(snapshot => snapshot.ClientIndex)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    GameClientActivitySnapshot snapshot = group.First();
                    return new SwitchClientTarget(snapshot.ClientIndex, snapshot.GameProcessId!.Value);
                });

    private void HandleForegroundWindowChange(
        nint foregroundWindow,
        IReadOnlyDictionary<int, SwitchClientTarget> activeClients)
    {
        bool changed;
        lock (_syncRoot)
        {
            changed = foregroundWindow != _lastForegroundWindow;
            if (changed)
                _lastForegroundWindow = foregroundWindow;
        }

        if (!changed || foregroundWindow == IntPtr.Zero)
            return;

        int? clientIndex = ResolveClientIndexForWindow(foregroundWindow, activeClients);
        if (!clientIndex.HasValue)
            return;

        lock (_syncRoot)
        {
            _currentClientIndex = clientIndex;
        }
    }

    private void RefreshTrackedState(IReadOnlyDictionary<int, SwitchClientTarget> activeClients)
    {
        bool shouldExitBrowsing = activeClients.Count < 2;

        lock (_syncRoot)
        {
            if (_currentClientIndex.HasValue && !activeClients.ContainsKey(_currentClientIndex.Value))
                _currentClientIndex = null;

            if (_browsingSourceClientIndex.HasValue && !activeClients.ContainsKey(_browsingSourceClientIndex.Value))
                shouldExitBrowsing = true;
        }

        if (shouldExitBrowsing)
            SetBrowsingState(isBrowsing: false, sourceWindowHandle: IntPtr.Zero, sourceClientIndex: null);
    }

    private void CycleToNextClient(
        IReadOnlyDictionary<int, SwitchClientTarget> activeClients,
        nint foregroundWindow)
    {
        int[] orderedClientIndices = activeClients.Keys.OrderBy(index => index).ToArray();
        if (orderedClientIndices.Length < 2)
            return;

        int? currentClientIndex;
        lock (_syncRoot)
        {
            currentClientIndex = _currentClientIndex;
        }

        int currentPosition = currentClientIndex.HasValue
            ? Array.IndexOf(orderedClientIndices, currentClientIndex.Value)
            : -1;
        int nextPosition = currentPosition >= 0
            ? (currentPosition + 1) % orderedClientIndices.Length
            : 0;
        int nextClientIndex = orderedClientIndices[nextPosition];

        if (!activeClients.TryGetValue(nextClientIndex, out SwitchClientTarget target))
            return;

        if (!TryGetTopLevelWindow(target.ProcessId, out nint windowHandle))
            return;

        if (!TryRaiseWindowToFront(windowHandle))
            return;

        lock (_syncRoot)
        {
            _currentClientIndex = nextClientIndex;
            SetBrowsingState_NoLock(
                isBrowsing: true,
                sourceWindowHandle: foregroundWindow,
                sourceClientIndex: ResolveClientIndexForWindowUnsafe(foregroundWindow, activeClients));
        }

        BrowsingStateChanged?.Invoke(this, true);
    }

    private bool TryResolveBrowsingFromLeftClick(
        bool shouldHandleLeftClick,
        nint foregroundWindow,
        IReadOnlyDictionary<int, SwitchClientTarget> activeClients)
    {
        nint browsingSourceWindowHandle;
        int? browsingSourceClientIndex;
        int? resolvedForegroundClientIndex = null;

        lock (_syncRoot)
        {
            if (!_isBrowsing)
                return false;

            if (!_isBrowsingArmed || !shouldHandleLeftClick)
                return false;

            browsingSourceWindowHandle = _browsingSourceWindowHandle;
            browsingSourceClientIndex = _browsingSourceClientIndex;
        }

        if (TryGetStableWindowHandle(foregroundWindow, out nint stableForegroundWindow))
            foregroundWindow = stableForegroundWindow;

        if (foregroundWindow != IntPtr.Zero)
            resolvedForegroundClientIndex = ResolveClientIndexForWindow(foregroundWindow, activeClients);

        if (foregroundWindow != IntPtr.Zero && foregroundWindow == browsingSourceWindowHandle)
        {
            _ = TryRaiseWindowToFront(foregroundWindow);
        }

        if (resolvedForegroundClientIndex.HasValue)
        {
            lock (_syncRoot)
            {
                _currentClientIndex = resolvedForegroundClientIndex;
            }
        }
        else if (foregroundWindow != IntPtr.Zero &&
                 foregroundWindow == browsingSourceWindowHandle &&
                 browsingSourceClientIndex.HasValue)
        {
            lock (_syncRoot)
            {
                _currentClientIndex = browsingSourceClientIndex;
            }
        }

        SetBrowsingState(isBrowsing: false, sourceWindowHandle: IntPtr.Zero, sourceClientIndex: null);
        return true;
    }

    private static int? ResolveClientIndexForWindow(
        nint windowHandle,
        IReadOnlyDictionary<int, SwitchClientTarget> activeClients)
        => ResolveClientIndexForWindowUnsafe(windowHandle, activeClients);

    private static int? ResolveClientIndexForWindowUnsafe(
        nint windowHandle,
        IReadOnlyDictionary<int, SwitchClientTarget> activeClients)
    {
        if (windowHandle == IntPtr.Zero)
            return null;

        _ = GetWindowThreadProcessId(windowHandle, out int processId);
        if (processId <= 0)
            return null;

        foreach ((int clientIndex, SwitchClientTarget client) in activeClients)
        {
            if (client.ProcessId == processId)
                return clientIndex;
        }

        return null;
    }

    private void SetBrowsingState(bool isBrowsing, nint sourceWindowHandle, int? sourceClientIndex)
    {
        bool changed;
        lock (_syncRoot)
        {
            changed = SetBrowsingState_NoLock(isBrowsing, sourceWindowHandle, sourceClientIndex);
        }

        if (changed)
            BrowsingStateChanged?.Invoke(this, isBrowsing);
    }

    private bool SetBrowsingState_NoLock(bool isBrowsing, nint sourceWindowHandle, int? sourceClientIndex)
    {
        bool changed = _isBrowsing != isBrowsing;
        _isBrowsing = isBrowsing;
        _browsingSourceWindowHandle = sourceWindowHandle;
        _browsingSourceClientIndex = sourceClientIndex;
        _isBrowsingArmed = !isBrowsing;
        return changed;
    }

    /// <summary>
    /// Waits until the ` key is released so the Alt+` navigation chord itself does not resolve browsing.
    /// </summary>
    private void ArmBrowsingAfterCycleKeyRelease(bool isCycleKeyDown)
    {
        lock (_syncRoot)
        {
            if (!_isBrowsing || _isBrowsingArmed || isCycleKeyDown)
                return;

            // Treat the ` key release as the end of navigation input, not as the deciding input.
            _isBrowsingArmed = true;
        }
    }

    private static bool HasDisallowedModifierKeyPressed()
        => IsAnyKeyDown(VkShift, 0xA0, 0xA1)
           || IsAnyKeyDown(VkControl, 0xA2, 0xA3)
           || IsKeyDown(VkLWin)
           || IsKeyDown(VkRWin);

    private static bool IsAnyAltDown()
        => IsAnyKeyDown(VkMenu, VkLMenu, VkRMenu);

    private static bool IsKeyDown(int virtualKey)
        => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static bool IsAnyKeyDown(int key1, int key2, int key3)
        => IsKeyDown(key1) || IsKeyDown(key2) || IsKeyDown(key3);

    private static bool TryGetTopLevelWindow(int processId, out nint windowHandle)
    {
        nint resolvedWindowHandle = IntPtr.Zero;

        EnumWindows((hwnd, _) =>
        {
            if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd) || IsIconic(hwnd))
                return true;

            if (GetWindow(hwnd, GwOwner) != IntPtr.Zero)
                return true;

            GetWindowThreadProcessId(hwnd, out int windowProcessId);
            if (windowProcessId != processId)
                return true;

            resolvedWindowHandle = hwnd;
            return false;
        }, IntPtr.Zero);

        windowHandle = resolvedWindowHandle;
        return windowHandle != IntPtr.Zero;
    }

    private static bool TryRaiseWindowToFront(nint windowHandle)
    {
        if (!TryGetStableWindowHandle(windowHandle, out nint stableWindowHandle))
            return false;

        if (!SetWindowPos(
                stableWindowHandle,
                HwndTopMost,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoActivate | SwpNoOwnerZOrder))
        {
            return false;
        }

        if (!SetWindowPos(
                stableWindowHandle,
                HwndNoTopMost,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoActivate | SwpNoOwnerZOrder))
        {
            return false;
        }

        return SetWindowPos(
            stableWindowHandle,
            HwndTop,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoActivate | SwpNoOwnerZOrder);
    }

    private static nint GetStableForegroundWindow()
        => TryGetStableWindowHandle(GetForegroundWindow(), out nint stableWindowHandle)
            ? stableWindowHandle
            : IntPtr.Zero;

    private static bool TryGetStableWindowHandle(nint windowHandle, out nint stableWindowHandle)
    {
        stableWindowHandle = IntPtr.Zero;

        if (windowHandle == IntPtr.Zero || !IsWindow(windowHandle))
            return false;

        nint rootOwnerWindow = GetAncestor(windowHandle, GaRootOwner);
        if (rootOwnerWindow != IntPtr.Zero)
            windowHandle = rootOwnerWindow;

        if (!IsWindow(windowHandle) || !IsWindowVisible(windowHandle) || IsIconic(windowHandle))
            return false;

        stableWindowHandle = windowHandle;
        return true;
    }

    private readonly record struct SwitchClientTarget(int ClientIndex, int ProcessId);

    private delegate bool EnumWindowsProc(nint hwnd, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out int lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint hWnd, uint gaFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint hWnd,
        nint hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);
}
