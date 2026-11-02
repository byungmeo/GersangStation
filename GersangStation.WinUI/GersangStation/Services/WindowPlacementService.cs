using Core;
using Core.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

namespace GersangStation.Services;

/// <summary>일반 창 크기와 최대화 상태를 저장하고, 다음 실행 시 현재 화면에 맞게 복원합니다.</summary>
internal sealed class WindowPlacementService : IDisposable
{
    private const int MinimumWidth = 960;
    private const int MinimumHeight = 640;
    private const uint ShowMaximized = 3;
    private const uint ShowMinimized = 2;
    private const uint RestoreToMaximized = 2;

    private readonly AppWindow _appWindow;
    private readonly nint _windowHandle;
    private readonly DispatcherQueueTimer _saveTimer;
    private WindowPlacementSettings? _pendingPlacement;
    private WindowPlacementSettings? _lastSavedPlacement;
    private bool _canSave = true;
    private bool _hasReportedFailure;
    private bool _disposed;

    public WindowPlacementService(Window window)
    {
        _appWindow = window.AppWindow;
        _windowHandle = WindowNative.GetWindowHandle(window);
        _saveTimer = window.DispatcherQueue.CreateTimer();
        _saveTimer.Interval = TimeSpan.FromMilliseconds(500);
        _saveTimer.IsRepeating = false;
        _saveTimer.Tick += OnSaveTimerTick;

        WindowPlacementSettings? saved = null;
        try { saved = AppDataManager.MainWindowPlacement; }
        catch (Exception ex)
        {
            // 읽지 못한 설정을 기본값으로 덮어쓰지 않습니다.
            _canSave = false;
            RecordFailure(ex, "Load");
        }

        ApplyInitialPlacement(saved);
        _lastSavedPlacement = saved;
        _appWindow.Changed += OnWindowChanged;
        _appWindow.Closing += OnWindowClosing;
        CapturePlacement();
    }

    private void ApplyInitialPlacement(WindowPlacementSettings? saved)
    {
        _appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        RectInt32 workArea = displayArea?.WorkArea ?? new RectInt32(
            _appWindow.Position.X, _appWindow.Position.Y, _appWindow.Size.Width, _appWindow.Size.Height);
        int minimumWidth = Math.Min(MinimumWidth, workArea.Width);
        int minimumHeight = Math.Min(MinimumHeight, workArea.Height);

        if (_appWindow.Presenter is not OverlappedPresenter presenter)
            return;

        presenter.PreferredMinimumWidth = minimumWidth;
        presenter.PreferredMinimumHeight = minimumHeight;
        presenter.IsResizable = true;
        presenter.IsMaximizable = true;
        presenter.IsMinimizable = true;

        double scale = GetScale();
        int width = (int)Math.Round(Math.Clamp(saved?.WidthInDips * scale ?? _appWindow.Size.Width, minimumWidth, workArea.Width));
        int height = (int)Math.Round(Math.Clamp(saved?.HeightInDips * scale ?? _appWindow.Size.Height, minimumHeight, workArea.Height));
        _appWindow.MoveAndResize(new RectInt32(
            workArea.X + (workArea.Width - width) / 2,
            workArea.Y + (workArea.Height - height) / 2, width, height));

        if (saved?.IsMaximized == true)
            presenter.Maximize();
    }

    private double GetScale() => Math.Max(96, GetDpiForWindow(_windowHandle)) / 96.0;

    private void OnWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange || args.DidPresenterChange)
            CapturePlacement();
    }

    private void CapturePlacement()
    {
        if (_disposed || !_canSave)
            return;

        try
        {
            var placement = new NativeWindowPlacement { Length = (uint)Marshal.SizeOf<NativeWindowPlacement>() };
            if (!GetWindowPlacement(_windowHandle, ref placement))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            int width = placement.NormalPosition.Right - placement.NormalPosition.Left;
            int height = placement.NormalPosition.Bottom - placement.NormalPosition.Top;
            if (width <= 0 || height <= 0)
                return;

            // 최소화 중에도 일반 크기를 얻고, 최대화에서 최소화한 상태도 유지합니다.
            bool isMaximized = placement.ShowCommand == ShowMaximized ||
                (placement.ShowCommand == ShowMinimized && (placement.Flags & RestoreToMaximized) != 0);
            double scale = GetScale();
            _pendingPlacement = new WindowPlacementSettings(width / scale, height / scale, isMaximized);
            _saveTimer.Stop();
            if (_pendingPlacement != _lastSavedPlacement)
                _saveTimer.Start();
        }
        catch (Exception ex) { RecordFailure(ex, "Capture"); }
    }

    public void SaveCurrentPlacement()
    {
        CapturePlacement();
        SavePendingPlacement();
    }

    private void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args) => SaveCurrentPlacement();
    private void OnSaveTimerTick(DispatcherQueueTimer sender, object args) => SavePendingPlacement();

    private void SavePendingPlacement()
    {
        _saveTimer.Stop();
        if (!_canSave || _pendingPlacement is not { } placement || placement == _lastSavedPlacement)
            return;

        try
        {
            AppDataManager.MainWindowPlacement = placement;
            _lastSavedPlacement = placement;
        }
        catch (Exception ex) { RecordFailure(ex, "Save"); }
    }

    private void RecordFailure(Exception exception, string operation)
    {
        if (_hasReportedFailure)
            return;
        _hasReportedFailure = true;
        App.ExceptionHandler.RecordRecoverable(exception, $"WindowPlacement.{operation}");
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _appWindow.Changed -= OnWindowChanged;
        _appWindow.Closing -= OnWindowClosing;
        _saveTimer.Tick -= OnSaveTimerTick;
        SavePendingPlacement();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeWindowPlacement
    {
        public uint Length, Flags, ShowCommand;
        public PointInt32 MinPosition, MaxPosition;
        public NativeRect NormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(nint windowHandle, ref NativeWindowPlacement placement);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint windowHandle);
}
