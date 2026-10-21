using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace GersangStation.Diagnostics;

/// <summary>
/// 미처리 예외 이벤트조차 발생하지 않는 종료를 다음 시작 시 감지합니다.
/// 실행 중/정상 종료만 기록하며, 예외나 종료 원인을 추측하지 않습니다.
/// </summary>
internal sealed class AppSessionJournal
{
    private readonly string _path;
    private readonly Func<int, long, bool> _isSameProcessAlive;
    private AppSessionSnapshot? _current;
    private bool _completed;

    internal AppSessionJournal(string localFolder, Func<int, long, bool>? isSameProcessAlive = null)
    {
        _path = Path.Combine(localFolder, "Logs", "app-session.txt");
        _isSameProcessAlive = isSameProcessAlive ?? IsSameProcessAlive;
    }

    internal string? CurrentSessionId => _current?.Id;

    /// <summary>단일 인스턴스 판정 후 한 번만 호출합니다. 살아 있는 이전 프로세스의 표식은 덮지 않습니다.</summary>
    internal AppSessionSnapshot? Begin(string? appVersion)
    {
        try
        {
            AppSessionSnapshot? previous = Read();
            if (previous is { Completed: false } && _isSameProcessAlive(previous.ProcessId, previous.ProcessStartUtcTicks))
                return null;

            using Process process = Process.GetCurrentProcess();
            var current = new AppSessionSnapshot(Guid.NewGuid().ToString("N"), Environment.ProcessId,
                process.StartTime.ToUniversalTime().Ticks, DateTimeOffset.Now, appVersion, Completed: false);
            Write(current);
            _current = current;
            return previous is { Completed: false } ? previous : null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppSessionJournal] 실행 표식 기록 실패: {ex}");
            return null;
        }
    }

    /// <summary>실제로 닫힌 창/승인된 앱 종료/정상 메시지 루프 종료에서만 호출합니다.</summary>
    internal void Complete()
    {
        if (_current is null || _completed)
            return;

        try
        {
            if (Read()?.Id != _current.Id)
                return;
            Write(_current with { Completed = true });
            _completed = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppSessionJournal] 정상 종료 표식 기록 실패: {ex}");
        }
    }

    private AppSessionSnapshot? Read()
    {
        if (!File.Exists(_path) || new FileInfo(_path).Length > 2048)
            return null;

        string[] fields = File.ReadAllLines(_path);
        if (fields.Length != 7 || fields[0] != "1" || !Guid.TryParseExact(fields[1], "N", out _) ||
            !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int pid) || pid <= 0 ||
            !long.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out long startTicks) || startTicks <= 0 ||
            !DateTimeOffset.TryParseExact(fields[4], "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startedAt) ||
            fields[5] is not ("running" or "completed"))
            return null;

        return new(fields[1], pid, startTicks, startedAt, fields[6], fields[5] == "completed");
    }

    private void Write(AppSessionSnapshot snapshot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string temporaryPath = _path + "." + snapshot.Id + ".tmp";
        string data = string.Join("\n", "1", snapshot.Id,
            snapshot.ProcessId.ToString(CultureInfo.InvariantCulture),
            snapshot.ProcessStartUtcTicks.ToString(CultureInfo.InvariantCulture),
            snapshot.StartedAt.ToString("O", CultureInfo.InvariantCulture),
            snapshot.Completed ? "completed" : "running", snapshot.AppVersion ?? "unknown");
        // 완성된 작은 표식만 교체합니다. 시작 표식은 UI 생성 전에 디스크에 반영합니다.
        using (FileStream stream = new(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporaryPath, _path, overwrite: true);
    }

    private static bool IsSameProcessAlive(int pid, long startTicks)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == startTicks;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch
        {
            // 프로세스 조회 권한이 없을 때 죽었다고 단정하지 않습니다.
            return true;
        }
    }
}

internal sealed record AppSessionSnapshot(string Id, int ProcessId, long ProcessStartUtcTicks,
    DateTimeOffset StartedAt, string? AppVersion, bool Completed);
