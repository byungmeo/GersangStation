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
    private enum PresentationMode
    {
        Recoverable,
        FatalUiCrash
    }

    private static readonly TimeSpan DuplicateSuppressionWindow = TimeSpan.FromSeconds(2);

    private readonly SemaphoreSlim _presentationGate = new(1, 1);
    private readonly object _duplicateLock = new();

    private string? _lastFingerprint;
    private DateTimeOffset _lastPresentedAt;

    /// <summary>
    /// 기존 호출부 호환성을 위해 남겨둔 진입점입니다. 새 코드는 역할별 메서드를 직접 사용합니다.
    /// </summary>
    public async Task HandleAsync(Exception exception, string context, bool isFatal)
    {
        if (isFatal)
        {
            await HandleFatalUiExceptionAsync(exception, context).ConfigureAwait(false);
            return;
        }

        await ShowRecoverableAsync(exception, context).ConfigureAwait(false);
    }

    /// <summary>
    /// 복구 가능한 경계 예외를 상세 정보 창으로 표시합니다.
    /// </summary>
    public async Task ShowRecoverableAsync(Exception exception, string context)
        => await HandleUiPresentationAsync(exception, context, PresentationMode.Recoverable, preferNativeFallback: false).ConfigureAwait(false);

    /// <summary>
    /// 전역 UI 미처리 예외를 마지막 crash UI로 표시한 뒤 앱을 종료합니다.
    /// </summary>
    public async Task HandleFatalUiExceptionAsync(Exception exception, string context)
        => await HandleUiPresentationAsync(exception, context, PresentationMode.FatalUiCrash, preferNativeFallback: false).ConfigureAwait(false);

    /// <summary>
    /// WinUI를 신뢰할 수 없는 전역 프로세스 수준 예외를 저수준 fallback으로만 기록하고 안내합니다.
    /// </summary>
    public void HandleFatalProcessException(Exception exception, string context)
        => HandleProcessFallbackException(exception, context, isFatal: true, exitApplication: false);

    /// <summary>
    /// WinUI를 사용할 수 없는 경로에서 예외를 저수준 fallback으로만 기록하고 안내합니다.
    /// </summary>
    public void HandleProcessFallbackException(Exception exception, string context, bool isFatal, bool exitApplication)
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
        ShowNativeFallback(exception, context, logPath, isFatal, exitApplication);
    }

    /// <summary>
    /// WinUI 창을 건너뛰고 Win32 fallback 경로를 바로 테스트합니다.
    /// </summary>
    public async Task HandleWithNativeFallbackAsync(Exception exception, string context, bool isFatal)
    {
        PresentationMode mode = isFatal
            ? PresentationMode.FatalUiCrash
            : PresentationMode.Recoverable;

        await HandleUiPresentationAsync(exception, context, mode, preferNativeFallback: true).ConfigureAwait(false);
    }

    private async Task HandleUiPresentationAsync(
        Exception exception,
        string context,
        PresentationMode presentationMode,
        bool preferNativeFallback)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException)
            return;

        bool isFatal = presentationMode is PresentationMode.FatalUiCrash;
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
                ShowNativeFallback(exception, context, logPath, isFatal, exitApplication: isFatal);
        }
        catch (Exception presentationException)
        {
            Debug.WriteLine(
                $"[AppExceptionHandler] 예외 창 표시 중 추가 예외가 발생했습니다.{Environment.NewLine}{presentationException}");

            ShowNativeFallback(exception, context, logPath, isFatal, exitApplication: isFatal);
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
    private static void ShowNativeFallback(
        Exception exception,
        string context,
        string? logPath,
        bool isFatal,
        bool exitApplication)
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

        if (exitApplication)
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
