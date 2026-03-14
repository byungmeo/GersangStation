using System;
using System.Runtime.InteropServices;

namespace GersangStation.Diagnostics;

/// <summary>
/// WinUI 창 표시가 불가능할 때 Win32 메시지 박스로 최소 안내를 제공합니다.
/// </summary>
internal static class NativeDialogFallback
{
    private const uint MB_OK = 0x00000000;
    private const uint MB_ICONERROR = 0x00000010;
    private const uint MB_TASKMODAL = 0x00002000;
    private const int MaxMessageLength = 1800;

    /// <summary>
    /// Win32 메시지 박스로 오류 요약을 표시합니다.
    /// </summary>
    public static bool TryShow(string title, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        try
        {
            _ = MessageBoxW(IntPtr.Zero, TrimMessage(message), title, MB_OK | MB_ICONERROR | MB_TASKMODAL);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string TrimMessage(string message)
    {
        if (message.Length <= MaxMessageLength)
            return message;

        return message[..MaxMessageLength] + Environment.NewLine + "..." + Environment.NewLine + "(자세한 내용은 로그 파일을 확인하세요.)";
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
