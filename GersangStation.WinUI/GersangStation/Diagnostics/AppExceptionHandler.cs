using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage;

namespace GersangStation.Diagnostics;

/// <summary>
/// 앱 전역에서 수집한 예외를 사용자에게 일관된 방식으로 표시합니다.
/// </summary>
public sealed class AppExceptionHandler
{
    private static readonly TimeSpan DuplicateSuppressionWindow = TimeSpan.FromSeconds(2);

    internal RecoverableErrorStore RecoverableErrors { get; } = new();
    private readonly object _duplicateLock = new();
    private readonly ConditionalWeakTable<Exception, Lazy<(string Details, string? LogPath)>> _recordedExceptions = new();

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
    /// 복구 가능한 경계 예외를 기록하고 비모달 안내를 예약합니다. 사용자 응답을 기다리지 않습니다.
    /// </summary>
    public Task ShowRecoverableAsync(Exception exception, string context)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is OperationCanceledException ||
            exception is BrowserUnavailableException browserFailure && !browserFailure.TryClaimReport())
            return Task.CompletedTask;

        try
        {
            var (details, logPath) = RecordRecoverableOnce(exception, context);
            if (ShouldSuppressDuplicate(exception, isFatal: false))
                return Task.CompletedTask;
            string summary = exception is BrowserUnavailableException
                ? "브라우저 요청을 완료하지 못했습니다. 앱을 다시 실행한 후 시도해 주세요."
                : "요청을 완료하지 못했습니다. 문제가 반복되면 오류 기록을 확인해 주세요.";
            RecoverableErrors.Add(new DiagnosticRecord(Guid.NewGuid(), DateTimeOffset.Now,
                context, summary, details, logPath));
        }
        catch (Exception recordingException)
        {
            Debug.WriteLine($"[AppExceptionHandler] 오류 기록 및 안내 준비 실패: {recordingException}");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 작업별 UI에서 이미 안내하는 복구 가능한 오류를 기록만 합니다.
    /// Dialog/Dispatcher에 접근하지 않으며, 기록 실패를 호출자에게 다시 던지지 않습니다.
    /// </summary>
    public void RecordRecoverable(Exception exception, string context)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is OperationCanceledException)
            return;

        try { RecordRecoverableOnce(exception, context); }
        catch (Exception recordingException)
        {
            Debug.WriteLine($"[AppExceptionHandler] 예외 기록 실패: {recordingException}");
        }
    }

    /// <summary>
    /// 기존 비동기 호출부용 호환 진입점입니다. UI를 사용하지 않고 기록 후 즉시 종료합니다.
    /// </summary>
    public Task HandleFatalUiExceptionAsync(Exception exception, string context)
    {
        FatalErrorHandler.Current.Terminate(exception, context);
        return Task.CompletedTask;
    }

    /// <summary>
    /// CLR이 이미 프로세스를 종료하는 AppDomain 경계용입니다. UI 없이 기록만 시도합니다.
    /// </summary>
    public void HandleFatalProcessException(Exception exception, string context)
        => FatalErrorHandler.Current.RecordProcessFailure(exception, context);

    /// <summary>
    /// WinUI를 사용할 수 없는 경로에서 예외를 저수준 fallback으로만 기록하고 안내합니다.
    /// </summary>
    public void HandleProcessFallbackException(Exception exception, string context, bool isFatal, bool exitApplication)
    {
        if (isFatal)
            FatalErrorHandler.Current.Terminate(exception, context);

        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException)
            return;

        if (exception is BrowserUnavailableException browserFailure && !browserFailure.TryClaimReport())
            return;

        var (_, logPath) = RecordRecoverableOnce(exception, context);
        if (ShouldSuppressDuplicate(exception, isFatal)) return;
        ShowNativeFallback(exception, context, logPath, isFatal, exitApplication);
    }

    /// <summary>
    /// WinUI 창을 건너뛰고 Win32 fallback 경로를 바로 테스트합니다.
    /// </summary>
    public Task HandleWithNativeFallbackAsync(Exception exception, string context, bool isFatal)
    {
        HandleProcessFallbackException(exception, context, isFatal, exitApplication: false);
        return Task.CompletedTask;
    }

    private bool ShouldSuppressDuplicate(Exception exception, bool isFatal)
    {
        // 연쇄 중단은 같은 안내 객체를 공유합니다. 새 사용자 요청은 시간 간격과 무관하게 안내합니다.
        if (exception is BrowserUnavailableException)
            return false;

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
    private (string Details, string? LogPath) RecordRecoverableOnce(Exception exception, string context)
        => _recordedExceptions.GetValue(exception,
            error => new Lazy<(string Details, string? LogPath)>(() => RecordException(error, context, isFatal: false))).Value;

    /// <summary>
    /// 기록 전용/안내 경로가 공유하는 진단 기록입니다. 안내 중복 억제와는 독립적입니다.
    /// </summary>
    private static (string Details, string? LogPath) RecordException(Exception exception, string context, bool isFatal)
    {
        string details;
        try { details = ExceptionDetailsBuilder.Build(exception, context, isFatal); }
        catch (Exception formattingException)
        {
            Debug.WriteLine($"[AppExceptionHandler] 상세 정보 구성 실패: {formattingException}");
            details = $"Context: {context}{Environment.NewLine}{exception}";
        }
        string? logPath = TryWriteCrashLog(details, isFatal);
        string detailsWithLogPath = logPath is null
            ? details
            : $"{details}{Environment.NewLine}Crash Log: {logPath}";
        Debug.WriteLine(detailsWithLogPath);
        return (detailsWithLogPath, logPath);
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
            string fileName = $"{(isFatal ? "fatal" : "error")}-{timestamp}-{Guid.NewGuid():N}.log";
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
            App.ExitNormally();
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
