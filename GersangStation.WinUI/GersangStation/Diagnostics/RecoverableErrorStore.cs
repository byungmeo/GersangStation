using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace GersangStation.Diagnostics;

/// <summary>현재 실행의 사용자 안내용 스냅샷입니다. 원본 예외 객체는 보관하지 않습니다.</summary>
public sealed record DiagnosticRecord(
    Guid Id, DateTimeOffset OccurredAt, string Context, string Summary, string Details, string? LogPath)
{
    public string DisplayName => $"{OccurredAt:HH:mm:ss.fff} — {Summary}";
}

internal sealed class RecoverableErrorStore
{
    internal const int Capacity = 20;
    private readonly object _gate = new();
    private readonly List<DiagnosticRecord> _records = new();

    // 구독자는 UI 처리를 예약만 합니다. 잠금 밖에서 호출하여 재진입을 허용합니다.
    internal event EventHandler? Changed;

    internal DiagnosticRecord[] Snapshot()
    {
        lock (_gate)
            return _records.ToArray();
    }

    internal void Add(DiagnosticRecord record)
    {
        lock (_gate)
        {
            _records.Insert(0, record);
            if (_records.Count > Capacity)
                _records.RemoveAt(_records.Count - 1);
        }

        if (Changed is not { } changed)
            return;

        foreach (EventHandler subscriber in changed.GetInvocationList())
        {
            try { subscriber(this, EventArgs.Empty); }
            catch (Exception ex) { Debug.WriteLine($"[RecoverableErrorStore] 안내 예약 실패: {ex}"); }
        }
    }
}
