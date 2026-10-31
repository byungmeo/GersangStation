namespace GameMaintenance;

public enum MaintenanceCancellationReason { BeforeStart, UserCanceled }

/// <summary>원인이 확인된 정상 중단만 표현합니다. 통신/시간 초과 오류에는 사용하지 않습니다.</summary>
public sealed class MaintenanceCanceledException : OperationCanceledException
{
    public MaintenanceCancellationReason Reason { get; }
    public bool CleanupFailed { get; }

    public MaintenanceCanceledException(MaintenanceCancellationReason reason, CancellationToken token, Exception? inner = null,
        bool cleanupFailed = false)
        : base(reason switch
        {
            MaintenanceCancellationReason.BeforeStart => "작업을 시작하기 전에 취소했습니다.",
            _ => "작업을 취소했습니다."
        }, inner, token)
    {
        Reason = reason;
        CleanupFailed = cleanupFailed;
    }
}
