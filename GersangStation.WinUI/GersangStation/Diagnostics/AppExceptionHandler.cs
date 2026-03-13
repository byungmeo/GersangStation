using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace GersangStation.Diagnostics;

/// <summary>
/// 앱 전역에서 수집한 예외를 사용자에게 일관된 방식으로 표시합니다.
/// </summary>
public sealed class AppExceptionHandler
{
    private static readonly TimeSpan DuplicateSuppressionWindow = TimeSpan.FromSeconds(2);

    private readonly SemaphoreSlim _presentationGate = new(1, 1);
    private readonly object _duplicateLock = new();

    private string? _lastFingerprint;
    private DateTimeOffset _lastPresentedAt;

    /// <summary>
    /// 예외를 상세 정보 창으로 표시하고, 치명적 예외면 창을 닫은 뒤 앱을 종료합니다.
    /// </summary>
    public async Task HandleAsync(Exception exception, string context, bool isFatal)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException)
            return;

        if (ShouldSuppressDuplicate(exception, isFatal))
            return;

        string details = ExceptionDetailsBuilder.Build(exception, context, isFatal);
        Debug.WriteLine(details);

        await _presentationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await ShowWindowAsync(exception, context, details, isFatal).ConfigureAwait(false);
        }
        catch (Exception presentationException)
        {
            Debug.WriteLine(
                $"[AppExceptionHandler] 예외 창 표시 중 추가 예외가 발생했습니다.{Environment.NewLine}{presentationException}");
        }
        finally
        {
            _presentationGate.Release();
        }
    }

    private bool ShouldSuppressDuplicate(Exception exception, bool isFatal)
    {
        string fingerprint = string.Join(
            "|",
            exception.GetType().FullName,
            exception.Message,
            exception.StackTrace,
            isFatal);

        lock (_duplicateLock)
        {
            DateTimeOffset now = DateTimeOffset.Now;
            bool isDuplicate =
                string.Equals(_lastFingerprint, fingerprint, StringComparison.Ordinal) &&
                now - _lastPresentedAt <= DuplicateSuppressionWindow;

            _lastFingerprint = fingerprint;
            _lastPresentedAt = now;
            return isDuplicate;
        }
    }

    private static async Task ShowWindowAsync(Exception exception, string context, string details, bool isFatal)
    {
        Microsoft.UI.Dispatching.DispatcherQueue? dispatcherQueue = App.UiDispatcherQueue ?? App.CurrentWindow?.DispatcherQueue;
        if (dispatcherQueue is null)
            return;

        await dispatcherQueue.RunOrEnqueueAsync(async () =>
        {
            var window = new ExceptionDisplayWindow(exception, context, details, isFatal);
            await window.ShowAndWaitAsync();

            if (isFatal)
                Application.Current.Exit();
        });
    }
}
