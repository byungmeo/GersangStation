using GersangStation.Diagnostics;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WinRT.Interop;

namespace GersangStation;

public static class DispatcherQueueExtensions
{
    /// <summary>
    /// DispatcherQueue에서 비동기 작업을 실행하고, 호출자는 완료 Task를 await할 수 있습니다.
    /// </summary>
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

    /// <summary>
    /// 현재 스레드가 UI 스레드면 즉시 실행하고, 아니면 DispatcherQueue에 예약합니다.
    /// </summary>
    public static Task RunOrEnqueueAsync(this Microsoft.UI.Dispatching.DispatcherQueue queue, Func<Task> action)
    {
        if (queue.HasThreadAccess)
            return action();

        return queue.EnqueueAsync(action);
    }

    /// <summary>
    /// DispatcherQueue 콜백에서 발생한 예외를 중앙 예외 처리기로 전달합니다.
    /// </summary>
    public static bool TryEnqueueHandled(
        this Microsoft.UI.Dispatching.DispatcherQueue queue,
        Action action,
        string context,
        bool isFatal = false)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        return queue.TryEnqueue(async () =>
        {
            await SafeExecution.RunHandledAsync(action, context, isFatal);
        });
    }

    /// <summary>
    /// DispatcherQueue 비동기 콜백에서 발생한 예외를 중앙 예외 처리기로 전달합니다.
    /// </summary>
    public static bool TryEnqueueHandled(
        this Microsoft.UI.Dispatching.DispatcherQueue queue,
        Func<Task> action,
        string context,
        bool isFatal = false)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        return queue.TryEnqueue(async () =>
        {
            await SafeExecution.RunHandledAsync(action, context, isFatal);
        });
    }
}

public static class WindowExtensions
{
    private const int WM_NCLBUTTONDBLCLK = 0x00A3;
    private const uint PreventMaximizeSubclassId = 0x47534443;
    private static readonly Dictionary<nint, SubclassDelegate> SubclassDelegates = [];

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(
        nint hWnd,
        SubclassDelegate pfnSubclass,
        nuint uIdSubclass,
        nint dwRefData);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint hWnd, uint uMsg, nint wParam, nint lParam);

    public static void PreventMaximizeOnTitleBarDoubleClick(this Window window)
    {
        nint windowHandle = WindowNative.GetWindowHandle(window);
        if (SubclassDelegates.ContainsKey(windowHandle))
            return;

        SubclassDelegate subclassDelegate = PreventMaximizeSubclassProc;
        if (!SetWindowSubclass(windowHandle, subclassDelegate, PreventMaximizeSubclassId, 0))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        SubclassDelegates[windowHandle] = subclassDelegate;
    }

    private static nint PreventMaximizeSubclassProc(nint hWnd, uint msg, nint wParam, nint lParam, nuint uIdSubclass, nint dwRefData)
    {
        if (msg == WM_NCLBUTTONDBLCLK)
            return nint.Zero;

        return DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint SubclassDelegate(nint hWnd, uint msg, nint wParam, nint lParam, nuint uIdSubclass, nint dwRefData);
}
