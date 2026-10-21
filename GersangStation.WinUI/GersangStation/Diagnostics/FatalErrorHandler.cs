using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;

namespace GersangStation.Diagnostics;

/// <summary>
/// 손상되었을 수 있는 UI, Dispatcher, Dialog 대기열에 접근하지 않는 치명적 오류 경계입니다.
/// </summary>
internal sealed class FatalErrorHandler
{
    internal static FatalErrorHandler Current { get; } = new();

    private FatalErrorStore _store = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GersangStation"));
    private readonly Action<Exception, string> _record;
    private readonly Action<string, Exception> _terminate;
    private readonly TimeSpan _recordingBudget;
    private readonly ManualResetEventSlim _recordingFinished = new(false);
    private int _hasStartedRecording;
    private AppSessionJournal? _sessionJournal;

    internal PendingFatalError? PreviousError { get; private set; }

    private FatalErrorHandler()
    {
        _record = (exception, context) => _store.Record(exception, context);
        _terminate = Environment.FailFast;
        _recordingBudget = TimeSpan.FromSeconds(1.5);
    }

    // 실제 프로세스를 종료하지 않고 경계 동작을 검증할 수 있는 내부 의존성 주입 지점입니다.
    internal FatalErrorHandler(Action<Exception, string> record, Action<string, Exception> terminate, TimeSpan recordingBudget)
    {
        _record = record;
        _terminate = terminate;
        _recordingBudget = recordingBudget;
    }

    /// <summary>주 인스턴스의 정상 시작 단계에서만 저장소를 준비합니다.</summary>
    internal void Initialize(string localFolder, string? appVersion)
    {
        _store = new FatalErrorStore(localFolder) { AppVersion = appVersion };
        _sessionJournal = new AppSessionJournal(localFolder);
        AppSessionSnapshot? interrupted = _sessionJournal.Begin(appVersion);
        _store.SessionId = _sessionJournal.CurrentSessionId;
        PreviousError = _store.ReadPending();
        if (interrupted is not null &&
            (PreviousError is null || !PreviousError.LogPath.EndsWith($"-{interrupted.Id}.log", StringComparison.OrdinalIgnoreCase)))
            PreviousError = _store.RecordUnexpectedTermination(interrupted);
    }

    internal void CompleteSession()
    {
        if (Volatile.Read(ref _hasStartedRecording) == 0)
            _sessionJournal?.Complete();
    }

    /// <summary>WinRT/native 콜백에서 예외가 ABI 밖으로 빠져나가 기록 없이 종료되는 것을 방지합니다.</summary>
    internal void RunCallback(Action callback, string context)
    {
        try { callback(); }
        catch (Exception ex) { Terminate(ex, context); }
    }

    internal void AcknowledgePreviousError()
    {
        if (PreviousError is { } previous)
            _store.Acknowledge(previous);
    }

    [DoesNotReturn]
    internal void Terminate(Exception exception, string context)
    {
        try
        {
            RecordProcessFailure(exception, context);
        }
        finally
        {
            // finally/종료 이벤트에서 추가 상태 저장이나 UI 정리를 시도하지 않습니다.
            _terminate("GersangStation: unhandled fatal error.", exception);
        }

        throw new InvalidOperationException("치명적 오류 종료 함수가 반환되었습니다.");
    }

    /// <summary>
    /// CLR이 이미 종료 중인 AppDomain 경계에서는 기록만 시도하고 런타임 종료를 방해하지 않습니다.
    /// 중복 진입과 디스크/예외 포맷팅 정체에도 대기는 제한됩니다.
    /// </summary>
    internal void RecordProcessFailure(Exception exception, string context)
    {
        if (Interlocked.CompareExchange(ref _hasStartedRecording, 1, 0) != 0)
        {
            _recordingFinished.Wait(_recordingBudget);
            return;
        }

        try
        {
            Thread recorder = new(() =>
            {
                try { _record(exception, context); }
                catch { /* 기록 실패가 원래 오류를 덮거나 종료를 막아서는 안 됩니다. */ }
                finally { _recordingFinished.Set(); }
            })
            {
                IsBackground = true,
                Name = "Fatal error recorder"
            };
            recorder.Start();
            _recordingFinished.Wait(_recordingBudget);
        }
        catch
        {
            _recordingFinished.Set();
        }
    }
}
