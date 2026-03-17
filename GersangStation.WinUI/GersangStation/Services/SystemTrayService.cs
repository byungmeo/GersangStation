using Microsoft.UI.Xaml;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace GersangStation.Services;

/// <summary>
/// 메인 창 최소화 시 시스템 트레이 아이콘과 복원/종료 상호작용을 관리합니다.
/// </summary>
public sealed class SystemTrayService : IDisposable
{
    private const uint SubclassId = 0x47535452;
    private const uint TrayIconId = 1;
    private const uint TrayCallbackMessage = 0x8001;
    private const uint NotifyIconVersion4 = 4;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NimAdd = 0x00000000;
    private const uint NimModify = 0x00000001;
    private const uint NimDelete = 0x00000002;
    private const uint NimSetVersion = 0x00000004;
    private const int WmNull = 0x0000;
    private const int WmSize = 0x0005;
    private const int WmContextMenu = 0x007B;
    private const int WmLButtonDblClk = 0x0203;
    private const int WmRButtonUp = 0x0205;
    private const int SizeMinimized = 1;
    private const uint MfString = 0x00000000;
    private const uint TpmLeftAlign = 0x0000;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;
    private const int ExitMenuCommandId = 1001;
    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x00000010;
    private const uint LrDefaultSize = 0x00000040;
    private const int SwHide = 0;
    private const int SwRestore = 9;
    private static readonly nint DefaultApplicationIconResource = new(32512);

    private readonly nint _windowHandle;
    private readonly Func<bool> _shouldHideOnMinimize;
    private readonly Action _hiddenToTrayCallback;
    private readonly Action _restoreRequestedCallback;
    private readonly Action _exitRequestedCallback;
    private readonly SubclassProc _subclassProc;
    private readonly nint _iconHandle;
    private bool _disposed;
    private bool _isHiddenToTray;
    private bool _isNotifyIconVisible;

    /// <summary>
    /// 시스템 트레이 서비스가 현재 창을 숨긴 상태인지 반환합니다.
    /// </summary>
    public bool IsHiddenToTray => _isHiddenToTray;

    /// <summary>
    /// 메인 창 핸들에 시스템 트레이용 윈도우 서브클래스를 연결합니다.
    /// </summary>
    public SystemTrayService(
        Window window,
        Func<bool> shouldHideOnMinimize,
        Action hiddenToTrayCallback,
        Action restoreRequestedCallback,
        Action exitRequestedCallback)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(shouldHideOnMinimize);
        ArgumentNullException.ThrowIfNull(hiddenToTrayCallback);
        ArgumentNullException.ThrowIfNull(restoreRequestedCallback);
        ArgumentNullException.ThrowIfNull(exitRequestedCallback);

        _windowHandle = WindowNative.GetWindowHandle(window);
        _shouldHideOnMinimize = shouldHideOnMinimize;
        _hiddenToTrayCallback = hiddenToTrayCallback;
        _restoreRequestedCallback = restoreRequestedCallback;
        _exitRequestedCallback = exitRequestedCallback;
        _subclassProc = WindowSubclassProc;
        _iconHandle = LoadTrayIconHandle();

        if (!SetWindowSubclass(_windowHandle, _subclassProc, SubclassId, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    /// <summary>
    /// 시스템 트레이에 숨겨진 창을 다시 복원합니다.
    /// </summary>
    public void RestoreWindow()
    {
        ThrowIfDisposed();

        _isHiddenToTray = false;
        ShowWindow(_windowHandle, SwRestore);
        SetForegroundWindow(_windowHandle);
        RemoveNotifyIcon();
    }

    /// <summary>
    /// 서브클래스와 트레이 아이콘 리소스를 정리합니다.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        RemoveNotifyIcon();
        RemoveWindowSubclass(_windowHandle, _subclassProc, SubclassId);

        if (_iconHandle != nint.Zero)
            DestroyIcon(_iconHandle);

        _disposed = true;
    }

    private nint WindowSubclassProc(nint hWnd, uint message, nint wParam, nint lParam, nuint uIdSubclass, nint dwRefData)
    {
        try
        {
            if (message == WmSize &&
                wParam.ToInt32() == SizeMinimized &&
                _shouldHideOnMinimize())
            {
                HideToTray();
                return nint.Zero;
            }

            if (message == TrayCallbackMessage)
            {
                HandleTrayCallback(GetTrayCallbackCode(lParam));
                return nint.Zero;
            }
        }
        catch (Exception ex)
        {
            _ = App.ExceptionHandler.ShowRecoverableAsync(ex, "SystemTrayService.WindowSubclassProc");
        }

        return DefSubclassProc(hWnd, message, wParam, lParam);
    }

    /// <summary>
    /// 최소화된 창을 트레이로 숨기고 알림 콜백을 실행합니다.
    /// </summary>
    private void HideToTray()
    {
        if (_isHiddenToTray)
            return;

        EnsureNotifyIconVisible();
        ShowWindow(_windowHandle, SwHide);
        _isHiddenToTray = true;
        _hiddenToTrayCallback();
    }

    /// <summary>
    /// 트레이 아이콘에서 발생한 더블 클릭과 우클릭 메뉴 요청을 분기합니다.
    /// </summary>
    private void HandleTrayCallback(int callbackMessage)
    {
        switch (callbackMessage)
        {
            case WmLButtonDblClk:
                _restoreRequestedCallback();
                break;

            case WmRButtonUp:
            case WmContextMenu:
                ShowContextMenu();
                break;
        }
    }

    /// <summary>
    /// NOTIFYICON_VERSION_4 콜백에서 하위 워드에 담겨 오는 실제 마우스 메시지를 추출합니다.
    /// </summary>
    private static int GetTrayCallbackCode(nint lParam)
        => unchecked((ushort)lParam.ToInt64());

    /// <summary>
    /// 트레이 우클릭 메뉴를 표시하고 종료 명령을 처리합니다.
    /// </summary>
    private void ShowContextMenu()
    {
        nint menuHandle = CreatePopupMenu();
        if (menuHandle == nint.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            if (!AppendMenu(menuHandle, MfString, ExitMenuCommandId, "종료"))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            if (!GetCursorPos(out POINT cursorPosition))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            SetForegroundWindow(_windowHandle);
            uint selectedCommand = TrackPopupMenuEx(
                menuHandle,
                TpmLeftAlign | TpmRightButton | TpmReturnCmd,
                cursorPosition.X,
                cursorPosition.Y,
                _windowHandle,
                nint.Zero);

            if (selectedCommand == ExitMenuCommandId)
            {
                _isHiddenToTray = false;
                RemoveNotifyIcon();
                _exitRequestedCallback();
            }
        }
        finally
        {
            DestroyMenu(menuHandle);
            PostMessage(_windowHandle, WmNull, nint.Zero, nint.Zero);
        }
    }

    /// <summary>
    /// 현재 프로세스의 아이콘을 시스템 트레이용 아이콘 핸들로 로드합니다.
    /// </summary>
    private static nint LoadTrayIconHandle()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "GersangStationShortcut.ico");
        if (File.Exists(iconPath))
        {
            nint iconHandle = LoadImage(nint.Zero, iconPath, ImageIcon, 0, 0, LrLoadFromFile | LrDefaultSize);
            if (iconHandle != nint.Zero)
                return iconHandle;
        }

        return LoadIcon(nint.Zero, DefaultApplicationIconResource);
    }

    /// <summary>
    /// 시스템 트레이에 아이콘을 등록하고 최신 셸 버전을 사용하도록 초기화합니다.
    /// </summary>
    private void EnsureNotifyIconVisible()
    {
        if (_isNotifyIconVisible)
            return;

        NOTIFYICONDATA data = CreateNotifyIconData();
        if (!ShellNotifyIcon(NimAdd, ref data))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        data.uVersion = NotifyIconVersion4;
        if (!ShellNotifyIcon(NimSetVersion, ref data))
        {
            ShellNotifyIcon(NimDelete, ref data);
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        _isNotifyIconVisible = true;
    }

    /// <summary>
    /// 시스템 트레이에 등록된 아이콘을 제거합니다.
    /// </summary>
    private void RemoveNotifyIcon()
    {
        if (!_isNotifyIconVisible)
            return;

        NOTIFYICONDATA data = CreateNotifyIconData();
        ShellNotifyIcon(NimDelete, ref data);
        _isNotifyIconVisible = false;
    }

    private NOTIFYICONDATA CreateNotifyIconData()
    {
        return new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _windowHandle,
            uID = TrayIconId,
            uFlags = NifMessage | NifIcon | NifTip,
            uCallbackMessage = TrayCallbackMessage,
            hIcon = _iconHandle,
            szTip = "거상스테이션",
            szInfo = string.Empty,
            szInfoTitle = string.Empty
        };
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(
        nint hWnd,
        SubclassProc pfnSubclass,
        nuint uIdSubclass,
        nint dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(
        nint hWnd,
        SubclassProc pfnSubclass,
        nuint uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellNotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", EntryPoint = "CreatePopupMenu", SetLastError = true)]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(nint hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint TrackPopupMenuEx(
        nint hMenu,
        uint uFlags,
        int x,
        int y,
        nint hWnd,
        nint lptpm);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(nint hMenu);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint hWnd, int msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadImage(
        nint hInst,
        string lpszName,
        uint uType,
        int cx,
        int cy,
        uint fuLoad);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint LoadIcon(nint hInstance, nint lpIconName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint hIcon);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint SubclassProc(nint hWnd, uint msg, nint wParam, nint lParam, nuint uIdSubclass, nint dwRefData);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }
}
