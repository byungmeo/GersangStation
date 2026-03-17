using Core;
using GersangStation.Diagnostics;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace GersangStation.Services;

/// <summary>
/// Monitors the foreground window and confines the cursor to the active Gersang window.
/// </summary>
public sealed partial class ClipMouseService : IDisposable
{
    private const string TargetProcessName = "Gersang";
    private const int ClipInsetPixels = 2;
    private const int VkMenu = 0x12;
    private const int VkLMenu = 0xA4;
    private const int VkRMenu = 0xA5;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    private readonly object _syncRoot = new();
    private Timer? _monitorTimer;
    private bool _isEnabled;
    private bool _isDisposed;
    private bool _isExternallySuspended;
    private bool _hasActiveClip;
    private nint _clippedWindowHandle;
    private NativeRect _clippedBounds;
    private AppDataManager.ClipMouseHotkeyModifier _escapeModifier;
    private int _isPolling;

    /// <summary>
    /// Creates the service and optionally starts foreground monitoring immediately.
    /// </summary>
    public ClipMouseService(bool isEnabled, AppDataManager.ClipMouseHotkeyModifier escapeModifier)
    {
        _escapeModifier = escapeModifier;
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
    /// Updates the modifier key used to temporarily suspend cursor confinement.
    /// </summary>
    public void SetEscapeModifier(AppDataManager.ClipMouseHotkeyModifier escapeModifier)
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _escapeModifier = escapeModifier;
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
                ReleaseCursorClipCore_NoLock();
        }
    }

    /// <summary>
    /// Stops monitoring and releases any active cursor clip.
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
    /// Polls the foreground window and updates the cursor clip state.
    /// </summary>
    private void PollCursorClip()
    {
        if (Interlocked.Exchange(ref _isPolling, 1) != 0)
            return;

        try
        {
            if (!IsMonitoringEnabled())
            {
                ReleaseCursorClipIfNeeded();
                return;
            }

            if (IsSuspendKeyPressed())
            {
                ReleaseCursorClipIfNeeded();
                return;
            }

            nint foregroundWindow = GetForegroundWindow();
            if (!TryGetClipTarget(foregroundWindow, out WindowClipTarget target))
            {
                ReleaseCursorClipIfNeeded();
                return;
            }

            if (!GetCursorPos(out NativePoint cursorPosition) || !target.Bounds.Contains(cursorPosition))
            {
                ReleaseCursorClipIfNeeded();
                return;
            }

            ApplyCursorClip(target);
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
        ReleaseCursorClipCore_NoLock();
    }

    private void ApplyCursorClip(WindowClipTarget target)
    {
        lock (_syncRoot)
        {
            if (!_isEnabled || _isDisposed)
                return;

            if (_hasActiveClip &&
                _clippedWindowHandle == target.WindowHandle &&
                _clippedBounds.Equals(target.Bounds))
            {
                return;
            }

            NativeRect bounds = target.Bounds;
            if (!ClipCursor(ref bounds))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Failed to confine the cursor to the active Gersang window.");
            }

            _hasActiveClip = true;
            _clippedWindowHandle = target.WindowHandle;
            _clippedBounds = bounds;
        }
    }

    private void ReleaseCursorClipIfNeeded()
    {
        lock (_syncRoot)
        {
            ReleaseCursorClipCore_NoLock();
        }
    }

    private void ReleaseCursorClipCore_NoLock()
    {
        if (!_hasActiveClip)
            return;

        if (!ClipCursor(IntPtr.Zero))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Failed to release the cursor clip.");
        }

        _hasActiveClip = false;
        _clippedWindowHandle = IntPtr.Zero;
        _clippedBounds = default;
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

        bounds = new NativeRect
        {
            Left = Math.Min(topLeft.X, bottomRight.X),
            Top = Math.Min(topLeft.Y, bottomRight.Y),
            Right = Math.Max(topLeft.X, bottomRight.X),
            Bottom = Math.Max(topLeft.Y, bottomRight.Y)
        };

        bounds.Inset(ClipInsetPixels);
        return !bounds.IsEmpty;
    }

    private bool IsSuspendKeyPressed()
    {
        AppDataManager.ClipMouseHotkeyModifier escapeModifier;
        lock (_syncRoot)
        {
            escapeModifier = _escapeModifier;
        }

        return escapeModifier switch
        {
            AppDataManager.ClipMouseHotkeyModifier.Control => IsAnyKeyDown(0x11, 0xA2, 0xA3),
            AppDataManager.ClipMouseHotkeyModifier.Shift => IsAnyKeyDown(0x10, 0xA0, 0xA1),
            _ => IsAnyKeyDown(VkMenu, VkLMenu, VkRMenu)
        };
    }

    private static bool IsKeyDown(int virtualKey)
        => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static bool IsAnyKeyDown(int key1, int key2, int key3)
        => IsKeyDown(key1) || IsKeyDown(key2) || IsKeyDown(key3);

    private readonly record struct WindowClipTarget(nint WindowHandle, int ProcessId, NativeRect Bounds);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect : IEquatable<NativeRect>
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public bool IsEmpty => Right <= Left || Bottom <= Top;

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

        public bool Contains(NativePoint point)
            => point.X >= Left
               && point.X < Right
               && point.Y >= Top
               && point.Y < Bottom;

        public bool Equals(NativeRect other)
            => Left == other.Left
               && Top == other.Top
               && Right == other.Right
               && Bottom == other.Bottom;

        public override bool Equals(object? obj)
            => obj is NativeRect other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(Left, Top, Right, Bottom);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
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

    [DllImport("user32.dll", EntryPoint = "ClipCursor", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClipCursor(ref NativeRect lpRect);

    [DllImport("user32.dll", EntryPoint = "ClipCursor", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClipCursor(nint lpRect);
}
