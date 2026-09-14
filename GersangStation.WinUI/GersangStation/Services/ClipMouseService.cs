using GersangStation.Diagnostics;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace GersangStation.Services;

/// <summary>
/// Monitors the foreground window and corrects cursor movement at the active Gersang client edges.
/// </summary>
public sealed partial class ClipMouseService : IDisposable
{
    private const string TargetProcessName = "Gersang";
    private const int ClipInsetPixels = 2;
    private const int WhMouseLl = 14;
    private const int WmMouseMove = 0x0200;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int VkLButton = 0x01;
    private const int VkMenu = 0x12;
    private const int VkLMenu = 0xA4;
    private const int VkRMenu = 0xA5;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(5);

    private readonly object _syncRoot = new();
    private readonly LowLevelMouseProc _mouseHookProcedure;
    private Timer? _monitorTimer;
    private nint _mouseHookHandle;
    private bool _isEnabled;
    private bool _isDisposed;
    private bool _isExternallySuspended;
    private bool _hasCachedTarget;
    private WindowClipTarget _cachedTarget;
    private bool _isConfinementActive;
    private bool _isOutsideDrag;
    private int _stateVersion;
    private int _isPolling;
    private int _isApplyingCursorCorrection;

    /// <summary>
    /// Creates the service and optionally starts foreground monitoring immediately.
    /// </summary>
    public ClipMouseService(bool isEnabled)
    {
        _mouseHookProcedure = MouseHookCallback;
        SetEnabled(isEnabled);
    }

    /// <summary>
    /// Enables or disables cursor confinement monitoring.
    /// </summary>
    public void SetEnabled(bool isEnabled)
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            bool effectiveEnabled = isEnabled && GersangStation.App.IsRunningAsAdministrator;
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
    /// Temporarily suspends cursor confinement while another feature is waiting for user window selection.
    /// </summary>
    public void SetExternalSuspended(bool isSuspended)
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _isExternallySuspended = isSuspended;

            if (isSuspended)
                ClearCachedTarget_NoLock();
        }
    }

    /// <summary>
    /// Stops monitoring without changing the game-owned OS cursor clip.
    /// </summary>
    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _isEnabled = false;
            StopMonitor_NoLock();
        }
    }

    /// <summary>
    /// Polls the foreground window and corrects cursor positions that leave its client area.
    /// </summary>
    private void PollCursorClip()
    {
        if (Interlocked.Exchange(ref _isPolling, 1) != 0)
            return;

        try
        {
            int stateVersion;
            lock (_syncRoot)
                stateVersion = _stateVersion;

            if (!IsMonitoringEnabled())
            {
                ClearCachedTarget();
                return;
            }

            if (IsSuspendKeyPressed())
            {
                ReleaseConfinement();
                return;
            }

            nint foregroundWindow = GetForegroundWindow();
            if (!TryGetClipTarget(foregroundWindow, out WindowClipTarget target))
            {
                ClearCachedTarget();
                return;
            }

            if (!TrySetCachedTarget(target, stateVersion))
                return;
            if (IsOutsideDrag())
            {
                // A mouse-up can be skipped when the input chain must not wait for our lock.
                if (!IsKeyDown(VkLButton))
                    EndOutsideDrag();
                return;
            }

            if (!GetCursorPos(out NativePoint cursorPosition))
                return;

            if (ShouldBypassCorrection(target, cursorPosition, stateVersion))
                return;

            _ = TryCorrectCursorPosition(target, cursorPosition, isHookMove: false, stateVersion);
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
            return _isEnabled && !_isDisposed && !_isExternallySuspended;
        }
    }

    private void StartMonitor_NoLock()
    {
        InstallMouseHook_NoLock();
        _monitorTimer ??= SafeExecution.StartHandledTimer(
            PollCursorClip,
            TimeSpan.Zero,
            PollInterval,
            "ClipMouseService.PollCursorClip",
            isFatal: false);
    }

    private void StopMonitor_NoLock()
    {
        _monitorTimer?.Dispose();
        _monitorTimer = null;

        ClearCachedTarget_NoLock();
        if (_mouseHookHandle == IntPtr.Zero)
            return;

        if (!UnhookWindowsHookEx(_mouseHookHandle))
        {
            Debug.WriteLine(
                $"[ClipMouse] Failed to remove the low-level mouse hook. " +
                $"Win32Error={Marshal.GetLastWin32Error()}");
            return;
        }

        _mouseHookHandle = IntPtr.Zero;
    }

    /// <summary>
    /// Installs the low-level hook on the current UI thread. Polling remains active if installation fails.
    /// </summary>
    private void InstallMouseHook_NoLock()
    {
        if (_mouseHookHandle != IntPtr.Zero)
            return;

        _mouseHookHandle = SetWindowsHookEx(
            WhMouseLl,
            _mouseHookProcedure,
            GetModuleHandle(null),
            0);

        if (_mouseHookHandle == IntPtr.Zero)
        {
            Debug.WriteLine(
                $"[ClipMouse] Failed to install the low-level mouse hook. " +
                $"Polling fallback remains active. Win32Error={Marshal.GetLastWin32Error()}");
        }
    }

    /// <summary>
    /// Never waits for service state from the global input chain. Only corrected moves are consumed.
    /// </summary>
    private nint MouseHookCallback(int code, nint wParam, nint lParam)
    {
        if (code < 0 || lParam == IntPtr.Zero ||
            Volatile.Read(ref _isApplyingCursorCorrection) != 0 ||
            !Monitor.TryEnter(_syncRoot))
        {
            return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }

        bool needsCorrection;
        WindowClipTarget target;
        NativePoint cursorPosition;
        try
        {
            needsCorrection = ProcessMouseHookMessage(
                unchecked((int)wParam.ToInt64()), lParam, out target, out cursorPosition);
        }
        finally
        {
            Monitor.Exit(_syncRoot);
        }

        // Both native cursor writes and the next hook run outside our state lock.
        bool consumed = needsCorrection &&
            TryCorrectCursorPosition(target, cursorPosition) == CursorCorrectionResult.Corrected;
        return consumed ? (nint)1 : CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    /// <summary>
    /// Tracks client entry and outside drags under the callback's nonblocking state lock.
    /// </summary>
    private bool ProcessMouseHookMessage(
        int mouseMessage, nint lParam, out WindowClipTarget target, out NativePoint cursorPosition)
    {
        target = default;
        cursorPosition = default;
        if (!IsMonitoringEnabled())
            return false;

        if (mouseMessage != WmMouseMove &&
            mouseMessage != WmLButtonDown &&
            mouseMessage != WmLButtonUp)
        {
            return false;
        }

        if (!TryGetCachedTarget(out target) ||
            GetForegroundWindow() != target.WindowHandle)
        {
            ClearCachedTarget_NoLock();
            return false;
        }

        NativeMouseHookData mouseData = Marshal.PtrToStructure<NativeMouseHookData>(lParam);
        cursorPosition = mouseData.Point;
        bool isSuspendKeyPressed = IsSuspendKeyPressed();

        if (mouseMessage == WmLButtonDown)
        {
            if (isSuspendKeyPressed)
                ReleaseConfinement();

            bool isEscapeActive = isSuspendKeyPressed || ShouldBypassCorrection(target, mouseData.Point);
            BeginOutsideDrag(target, mouseData.Point, isEscapeActive);
            return false;
        }

        if (mouseMessage == WmLButtonUp)
        {
            EndOutsideDrag();
            return false;
        }

        if (isSuspendKeyPressed)
        {
            ReleaseConfinement();
            return false;
        }

        if (IsOutsideDrag())
            return false;

        if (ShouldBypassCorrection(target, mouseData.Point))
            return false;

        return true;
    }

    /// <summary>
    /// Publishes geometry only if no release, stop, or foreground invalidation occurred during lookup.
    /// </summary>
    private bool TrySetCachedTarget(WindowClipTarget target, int stateVersion)
    {
        lock (_syncRoot)
        {
            if (!_isEnabled || _isDisposed || _isExternallySuspended ||
                stateVersion != _stateVersion || GetForegroundWindow() != target.WindowHandle)
                return false;

            if (!_hasCachedTarget || _cachedTarget.WindowHandle != target.WindowHandle ||
                _cachedTarget.ProcessId != target.ProcessId)
            {
                _isConfinementActive = false;
                _isOutsideDrag = false;
            }

            _cachedTarget = target;
            _hasCachedTarget = true;
            return true;
        }
    }

    private bool TryGetCachedTarget(out WindowClipTarget target)
    {
        lock (_syncRoot)
        {
            target = _cachedTarget;
            return _hasCachedTarget;
        }
    }

    private void ClearCachedTarget()
    {
        lock (_syncRoot)
        {
            ClearCachedTarget_NoLock();
        }
    }

    private void ClearCachedTarget_NoLock()
    {
        _hasCachedTarget = false;
        _cachedTarget = default;
        _isConfinementActive = false;
        _isOutsideDrag = false;
        _stateVersion++;
    }

    /// <summary>
    /// Releases confinement until client re-entry without discarding a drag in progress.
    /// </summary>
    private void ReleaseConfinement()
    {
        lock (_syncRoot)
        {
            if (_isEnabled && !_isDisposed && !_isExternallySuspended)
            {
                _isConfinementActive = false;
                _stateVersion++;
            }
        }
    }

    /// <summary>
    /// Activates only on client entry; outside movement is corrected only after activation.
    /// </summary>
    private bool ShouldBypassCorrection(WindowClipTarget target, NativePoint cursorPosition, int? stateVersion = null)
    {
        lock (_syncRoot)
        {
            if ((stateVersion.HasValue && stateVersion.Value != _stateVersion) ||
                !IsMonitoringEnabled() || !_hasCachedTarget || !_cachedTarget.Equals(target) ||
                _isOutsideDrag || IsSuspendKeyPressed())
                return true;

            // Do not rearm mid-drag even if its initial button event was skipped or another app owned it.
            if (!_isConfinementActive && IsKeyDown(VkLButton))
                return true;

            if (target.Bounds.Contains(cursorPosition))
                _isConfinementActive = true;

            return !_isConfinementActive;
        }
    }

    /// <summary>
    /// Tracks a drag that starts outside the client while correction is released.
    /// This is a conservative outside-drag bypass, not a native title-bar hit test.
    /// </summary>
    private void BeginOutsideDrag(
        WindowClipTarget target,
        NativePoint cursorPosition,
        bool isEscapeActive)
    {
        lock (_syncRoot)
        {
            _isOutsideDrag = isEscapeActive && !target.Bounds.Contains(cursorPosition);
            if (_isOutsideDrag)
            {
                _isConfinementActive = false;
                _stateVersion++;
            }
        }
    }

    /// <summary>
    /// Ends the temporary native non-client drag bypass when the left mouse button is released.
    /// </summary>
    private void EndOutsideDrag()
    {
        lock (_syncRoot)
        {
            if (_isOutsideDrag)
            {
                _isConfinementActive = false;
                _stateVersion++;
            }
            _isOutsideDrag = false;
        }
    }

    /// <summary>
    /// Reports whether a drag that started outside the client is currently bypassing correction.
    /// </summary>
    private bool IsOutsideDrag()
    {
        lock (_syncRoot)
        {
            return _isOutsideDrag;
        }
    }

    /// <summary>
    /// Revalidates the target at the write boundary and rejects invalidated observations.
    /// Failed SetCursorPos calls remain eligible for the next 5ms poll.
    /// </summary>
    private CursorCorrectionResult TryCorrectCursorPosition(
        WindowClipTarget target,
        NativePoint cursorPosition,
        bool isHookMove = true,
        int? stateVersion = null)
    {
        if (Interlocked.Exchange(ref _isApplyingCursorCorrection, 1) != 0)
            return CursorCorrectionResult.Retry;

        try
        {
            if (!Monitor.TryEnter(_syncRoot))
                return CursorCorrectionResult.Retry;

            NativePoint correctedPosition;
            int correctionVersion;
            try
            {
                if ((stateVersion.HasValue && stateVersion.Value != _stateVersion) ||
                    !IsMonitoringEnabled() || !_hasCachedTarget || !_cachedTarget.Equals(target) ||
                    !_isConfinementActive || _isOutsideDrag || IsSuspendKeyPressed())
                    return CursorCorrectionResult.NotNeeded;

                // HWND values can be reused after destruction. Validate the cached owner as well.
                _ = GetWindowThreadProcessId(target.WindowHandle, out int processId);
                if (processId != target.ProcessId || !IsWindowVisible(target.WindowHandle) ||
                    IsIconic(target.WindowHandle) || GetForegroundWindow() != target.WindowHandle)
                {
                    ClearCachedTarget_NoLock();
                    return CursorCorrectionResult.NotNeeded;
                }

                // Poll observations can age while waiting for a hook or a state transition.
                if (!isHookMove && !GetCursorPos(out cursorPosition))
                    return CursorCorrectionResult.NotNeeded;

                if (!TryGetCorrectedCursorPosition(target, cursorPosition, out correctedPosition))
                    return CursorCorrectionResult.NotNeeded;

                correctionVersion = _stateVersion;
            }
            finally
            {
                Monitor.Exit(_syncRoot);
            }

            // SetCursorPos may reenter the hook thread; do not make that thread wait for our lock.
            // Windows does not provide an atomic foreground-check-and-cursor-write operation.
            if (Volatile.Read(ref _stateVersion) != correctionVersion ||
                GetForegroundWindow() != target.WindowHandle || IsSuspendKeyPressed())
                return CursorCorrectionResult.NotNeeded;

            return SetCursorPos(correctedPosition.X, correctedPosition.Y)
                ? CursorCorrectionResult.Corrected
                : CursorCorrectionResult.Retry;
        }
        finally
        {
            Volatile.Write(ref _isApplyingCursorCorrection, 0);
        }
    }

    /// <summary>
    /// Calculates a corrected screen position within the active client area.
    /// </summary>
    private static bool TryGetCorrectedCursorPosition(
        WindowClipTarget target,
        NativePoint cursorPosition,
        out NativePoint correctedPosition)
    {
        correctedPosition = cursorPosition;

        if (cursorPosition.X < target.Bounds.Left)
            correctedPosition.X = target.Bounds.Left;
        else if (cursorPosition.X >= target.Bounds.Right)
            correctedPosition.X = target.Bounds.Right - 1;

        if (cursorPosition.Y < target.Bounds.Top)
            correctedPosition.Y = target.Bounds.Top;
        else if (cursorPosition.Y >= target.Bounds.Bottom)
            correctedPosition.Y = target.Bounds.Bottom - 1;

        return correctedPosition.X != cursorPosition.X || correctedPosition.Y != cursorPosition.Y;
    }

    private static bool TryGetClipTarget(nint windowHandle, out WindowClipTarget target)
    {
        target = default;

        if (windowHandle == IntPtr.Zero || !IsWindowVisible(windowHandle) || IsIconic(windowHandle))
            return false;

        _ = GetWindowThreadProcessId(windowHandle, out int processId);
        if (processId <= 0 || !IsTargetProcess(processId))
            return false;

        if (!TryGetClientBounds(windowHandle, out NativeRect bounds))
            return false;

        target = new WindowClipTarget(windowHandle, processId, bounds);
        return true;
    }

    private static bool IsTargetProcess(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return string.Equals(process.ProcessName, TargetProcessName, StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves the active client-area bounds used for cursor correction.
    /// </summary>
    private static bool TryGetClientBounds(nint windowHandle, out NativeRect bounds)
    {
        bounds = default;
        if (!GetClientRect(windowHandle, out NativeRect clientRect) || clientRect.IsEmpty)
            return false;

        // 좌상단과 우하단을 각각 화면 좌표로 변환해 배율 반올림 오차 누적을 피합니다.
        NativePoint topLeft = new() { X = clientRect.Left, Y = clientRect.Top };
        NativePoint bottomRight = new() { X = clientRect.Right, Y = clientRect.Bottom };
        if (!ClientToScreen(windowHandle, ref topLeft) ||
            !ClientToScreen(windowHandle, ref bottomRight))
            return false;

        int screenLeft = Math.Min(topLeft.X, bottomRight.X);
        int screenTop = Math.Min(topLeft.Y, bottomRight.Y);
        int screenRight = Math.Max(topLeft.X, bottomRight.X);
        int screenBottom = Math.Max(topLeft.Y, bottomRight.Y);

        bounds = new NativeRect
        {
            Left = screenLeft,
            Top = screenTop,
            Right = screenRight,
            Bottom = screenBottom
        };

        bounds.Inset(ClipInsetPixels);
        return !bounds.IsEmpty;
    }

    /// <summary>
    /// Uses Alt as the fixed temporary escape hotkey for cursor confinement.
    /// </summary>
    private static bool IsSuspendKeyPressed()
        => IsAnyKeyDown(VkMenu, VkLMenu, VkRMenu);

    private static bool IsKeyDown(int virtualKey)
        => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static bool IsAnyKeyDown(int key1, int key2, int key3)
        => IsKeyDown(key1) || IsKeyDown(key2) || IsKeyDown(key3);

    private readonly record struct WindowClipTarget(
        nint WindowHandle,
        int ProcessId,
        NativeRect Bounds);

    private enum CursorCorrectionResult
    {
        NotNeeded,
        Corrected,
        Retry
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint LowLevelMouseProc(int code, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public bool IsEmpty => Right <= Left || Bottom <= Top;

        public bool Contains(NativePoint point)
            => point.X >= Left && point.X < Right &&
               point.Y >= Top && point.Y < Bottom;

        public void Inset(int pixels)
        {
            if (pixels <= 0)
                return;

            int insetX = Math.Min(pixels, Math.Max(0, (Right - Left - 1) / 2));
            int insetY = Math.Min(pixels, Math.Max(0, (Bottom - Top - 1) / 2));

            Left += insetX;
            Top += insetY;
            Right -= insetX;
            Bottom -= insetY;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMouseHookData
    {
        public NativePoint Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out int lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint hWnd, out NativeRect lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint hWnd, ref NativePoint lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(
        int hookType,
        LowLevelMouseProc hookProcedure,
        nint moduleHandle,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hookHandle);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(
        nint hookHandle,
        int code,
        nint wParam,
        nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}
