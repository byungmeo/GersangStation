using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

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
        => await HandleCoreAsync(exception, context, isFatal, preferNativeFallback: false).ConfigureAwait(false);

    /// <summary>
    /// WinUI 창을 건너뛰고 Win32 fallback 경로를 바로 테스트합니다.
    /// </summary>
    public async Task HandleWithNativeFallbackAsync(Exception exception, string context, bool isFatal)
        => await HandleCoreAsync(exception, context, isFatal, preferNativeFallback: true).ConfigureAwait(false);

    private async Task HandleCoreAsync(Exception exception, string context, bool isFatal, bool preferNativeFallback)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException)
            return;

        if (ShouldSuppressDuplicate(exception, isFatal))
            return;

        string details = ExceptionDetailsBuilder.Build(exception, context, isFatal);
        string? logPath = TryWriteCrashLog(details, isFatal);
        string detailsWithLogPath = logPath is null
            ? details
            : $"{details}{Environment.NewLine}Crash Log: {logPath}";

        Debug.WriteLine(detailsWithLogPath);

        await _presentationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            bool presented = !preferNativeFallback &&
                await TryShowWindowAsync(exception, context, detailsWithLogPath, logPath, isFatal).ConfigureAwait(false);
            if (!presented)
                ShowNativeFallback(exception, context, logPath, isFatal);
        }
        catch (Exception presentationException)
        {
            Debug.WriteLine(
                $"[AppExceptionHandler] 예외 창 표시 중 추가 예외가 발생했습니다.{Environment.NewLine}{presentationException}");

            ShowNativeFallback(exception, context, logPath, isFatal);
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

    /// <summary>
    /// WinUI 예외 창을 표시할 수 있으면 표시하고, 실패하면 false를 반환합니다.
    /// </summary>
    private static async Task<bool> TryShowWindowAsync(Exception exception, string context, string details, string? logPath, bool isFatal)
    {
        Microsoft.UI.Dispatching.DispatcherQueue? dispatcherQueue = App.UiDispatcherQueue ?? App.CurrentWindow?.DispatcherQueue;
        if (dispatcherQueue is null)
            return false;

        await dispatcherQueue.RunOrEnqueueAsync(async () =>
        {
            var window = new ExceptionDisplayWindow(exception, context, details, logPath, isFatal);
            await window.ShowAndWaitAsync();

            if (isFatal)
                Application.Current.Exit();
        });

        return true;
    }

    /// <summary>
    /// 예외 상세 정보를 로컬 로그 폴더에 저장합니다.
    /// </summary>
    private static string? TryWriteCrashLog(string details, bool isFatal)
    {
        try
        {
            string basePath = TryGetWritableLogRoot();
            string logDirectory = Path.Combine(basePath, "Logs");
            Directory.CreateDirectory(logDirectory);

            string timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff");
            string fileName = isFatal ? $"fatal-{timestamp}.log" : $"error-{timestamp}.log";
            string filePath = Path.Combine(logDirectory, fileName);
            File.WriteAllText(filePath, details);
            return filePath;
        }
        catch (Exception logException)
        {
            Debug.WriteLine(
                $"[AppExceptionHandler] 예외 로그 저장 중 추가 예외가 발생했습니다.{Environment.NewLine}{logException}");
            return null;
        }
    }

    /// <summary>
    /// WinUI 창 표시가 실패했을 때 Win32 메시지 박스로 최소 정보를 안내합니다.
    /// </summary>
    private static void ShowNativeFallback(Exception exception, string context, string? logPath, bool isFatal)
    {
        string title = isFatal ? "GersangStation 치명적 오류" : "GersangStation 오류";
        string message =
            $"오류 유형: {exception.GetType().FullName}{Environment.NewLine}" +
            $"Context: {context}{Environment.NewLine}" +
            $"Message: {exception.Message}{Environment.NewLine}" +
            (logPath is null
                ? "로그 파일 저장에 실패했습니다."
                : $"로그 파일: {logPath}{Environment.NewLine}자세한 정보는 로그 파일을 확인하세요.");

        bool shown = NativeDialogFallback.TryShow(title, message);
        if (!shown)
        {
            Debug.WriteLine(
                $"[AppExceptionHandler] 네이티브 오류 대화상자 표시도 실패했습니다.{Environment.NewLine}{message}");
        }

        if (isFatal)
            Application.Current.Exit();
    }

    /// <summary>
    /// 로그 저장에 사용할 쓰기 가능한 루트 폴더를 찾습니다.
    /// </summary>
    private static string TryGetWritableLogRoot()
    {
        try
        {
            return ApplicationData.Current.LocalFolder.Path;
        }
        catch
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GersangStation");
        }
    }
}
