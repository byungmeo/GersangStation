using System;
using System.Threading;
using System.Threading.Tasks;

namespace GersangStation.Diagnostics;

/// <summary>
/// UI 외부에서 실행되는 작업을 공통 예외 처리기로 연결하는 도우미입니다.
/// </summary>
public static class SafeExecution
{
    /// <summary>
    /// 동기 작업을 실행하고, 발생한 예외를 중앙 예외 처리기로 전달합니다.
    /// </summary>
    public static async Task RunHandledAsync(Action action, string context, bool isFatal = false)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        try
        {
            action();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await ReportAsync(ex, context, isFatal).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 비동기 작업을 실행하고, 발생한 예외를 중앙 예외 처리기로 전달합니다.
    /// </summary>
    public static async Task RunHandledAsync(Func<Task> action, string context, bool isFatal = false)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        try
        {
            await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await ReportAsync(ex, context, isFatal).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// fire-and-forget Task의 예외를 중앙 예외 처리기로 연결합니다.
    /// </summary>
    public static void FireAndForgetHandled(this Task task, string context, bool isFatal = false)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        _ = ObserveAsync(task, context, isFatal);
    }

    /// <summary>
    /// Timer 콜백을 예외 처리기와 함께 한 번 실행합니다.
    /// </summary>
    public static Timer StartHandledTimer(Action action, TimeSpan dueTime, string context, bool isFatal = false)
        => StartHandledTimer(action, dueTime, Timeout.InfiniteTimeSpan, context, isFatal);

    /// <summary>
    /// Timer 콜백을 예외 처리기와 함께 실행합니다.
    /// </summary>
    public static Timer StartHandledTimer(Action action, TimeSpan dueTime, TimeSpan period, string context, bool isFatal = false)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        return new Timer(
            static state =>
            {
                TimerState timerState = (TimerState)state!;
                RunHandledAsync(timerState.Action, timerState.Context, timerState.IsFatal)
                    .GetAwaiter()
                    .GetResult();
            },
            new TimerState(action, context, isFatal),
            dueTime,
            period);
    }

    /// <summary>
    /// 비동기 Timer 콜백을 예외 처리기와 함께 실행합니다.
    /// </summary>
    public static Timer StartHandledTimer(Func<Task> action, TimeSpan dueTime, TimeSpan period, string context, bool isFatal = false)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        return new Timer(
            static state =>
            {
                AsyncTimerState timerState = (AsyncTimerState)state!;
                RunHandledAsync(timerState.Action, timerState.Context, timerState.IsFatal)
                    .GetAwaiter()
                    .GetResult();
            },
            new AsyncTimerState(action, context, isFatal),
            dueTime,
            period);
    }

    private static async Task ObserveAsync(Task task, string context, bool isFatal)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await ReportAsync(ex, context, isFatal).ConfigureAwait(false);
        }
    }

    private static Task ReportAsync(Exception exception, string context, bool isFatal)
    {
        return isFatal
            ? App.ExceptionHandler.HandleFatalUiExceptionAsync(exception, context)
            : App.ExceptionHandler.ShowRecoverableAsync(exception, context);
    }

    private sealed record TimerState(Action Action, string Context, bool IsFatal);

    private sealed record AsyncTimerState(Func<Task> Action, string Context, bool IsFatal);
}
