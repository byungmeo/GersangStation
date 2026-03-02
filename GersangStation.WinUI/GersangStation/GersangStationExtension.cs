using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WinRT.Interop;

namespace GersangStation;

public static class DispatcherQueueExtensions
{
    public static Task EnqueueAsync(this Microsoft.UI.Dispatching.DispatcherQueue queue, Func<Task> action)
    {
        var tcs = new TaskCompletionSource<object?>();
        queue.TryEnqueue(async () =>
        {
            try
            {
                await action();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public static Task RunOrEnqueueAsync(this Microsoft.UI.Dispatching.DispatcherQueue queue, Func<Task> action)
    {
        if (queue.HasThreadAccess)
            return action();

        return queue.EnqueueAsync(action);
    }
}

public static class WindowExtensions
{
    private const int WM_NCLBUTTONDBLCLK = 0x00A3;
    private const int GWLP_WNDPROC = -4;
    private static SubclassDelegate? _newWndProc;
    private static IntPtr _oldWndProc;

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, SubclassDelegate newProc);
    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    public static void PreventMaximizeOnTitleBarDoubleClick(this Window window)
    {
        IntPtr _hwnd = WindowNative.GetWindowHandle(window);
        _newWndProc = WndProc;
        _oldWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _newWndProc);
    }

    private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_NCLBUTTONDBLCLK)
            return IntPtr.Zero;
        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }
    private delegate IntPtr SubclassDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}