using System;
using System.IO;
using System.Runtime.InteropServices;

namespace GersangStation.Diagnostics;

/// <summary>WinUI/WinRT에 의존하지 않는 치명적 오류 기록과 다음 실행 안내 표식입니다.</summary>
internal sealed class FatalErrorStore(string localFolder)
{
    private const int MaxDetailsLength = 256 * 1024;
    private const string MissingDetailsMessage = "이전 실행 중 치명적 오류가 발생해 앱이 종료되었습니다. 상세 로그는 저장되지 않았거나 읽을 수 없습니다.";
    private readonly string _logDirectory = Path.Combine(localFolder, "Logs");
    private readonly string _pendingPath = Path.Combine(localFolder, "Logs", "pending-fatal.txt");
    private readonly string _acknowledgedPath = Path.Combine(localFolder, "Logs", "acknowledged-fatal.txt");

    internal void Record(Exception exception, string context)
    {
        Directory.CreateDirectory(_logDirectory);
        string fileName = $"fatal-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}-{SessionId ?? Guid.NewGuid().ToString("N")}.log";
        string temporaryMarker = Path.Combine(_logDirectory, fileName + ".pending");

        // 상세 포맷팅이나 로그 쓰기가 실패/정체되어도 다음 시작 시 안내할 수 있도록 먼저 기록합니다.
        File.WriteAllText(temporaryMarker, fileName);
        File.Move(temporaryMarker, _pendingPath, overwrite: true);

        string header = $"=== GersangStation Fatal Exception Report ==={Environment.NewLine}" +
            $"Occurred At: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}" +
            $"Context: {context}{Environment.NewLine}Fatal: True{Environment.NewLine}" +
            $"Process Id: {Environment.ProcessId}{Environment.NewLine}" +
            $"Runtime: {RuntimeInformation.FrameworkDescription}{Environment.NewLine}" +
            $"Architecture: {RuntimeInformation.ProcessArchitecture}{Environment.NewLine}" +
            $"App Version: {AppVersion}{Environment.NewLine}{Environment.NewLine}";
        string details = header + exception;
        if (details.Length > MaxDetailsLength)
            details = details[..MaxDetailsLength] + "\n[Report truncated]";
        File.WriteAllText(Path.Combine(_logDirectory, fileName), details);
    }

    // 정상 시작 시 전달받은 값만 사용합니다. 치명적 오류 경로에서 Package/WinRT를 호출하지 않습니다.
    internal string? AppVersion { get; set; }
    internal string? SessionId { get; set; }

    internal PendingFatalError RecordUnexpectedTermination(AppSessionSnapshot previous)
    {
        string fileName = $"interrupted-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}-{previous.Id}.log";
        string filePath = Path.Combine(_logDirectory, fileName);
        try
        {
            Directory.CreateDirectory(_logDirectory);
            string details = $"=== GersangStation Unexpected Termination Report ==={Environment.NewLine}" +
                $"Detected At: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}" +
                $"Previous Session: {previous.Id}{Environment.NewLine}" +
                $"Previous Process Id: {previous.ProcessId}{Environment.NewLine}" +
                $"Started At: {previous.StartedAt:yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}" +
                $"App Version: {previous.AppVersion}{Environment.NewLine}{Environment.NewLine}" +
                "이전 실행의 정상 종료 기록을 확인하지 못했습니다.\n" +
                "예외 정보는 확보되지 않았으므로 종료 원인을 특정할 수 없습니다.\n" +
                "강제 종료, Windows/프로세스 수준 오류, 시스템 종료 또는 전원 문제 등이 원인일 수 있습니다.";
            File.WriteAllText(filePath, details);
            string temporaryMarker = filePath + ".pending";
            File.WriteAllText(temporaryMarker, fileName);
            File.Move(temporaryMarker, _pendingPath, overwrite: true);
        }
        catch
        {
            // 기록 저장이 실패해도 이번 실행의 안내에는 종료 원인 불명임을 표시합니다.
        }
        return new PendingFatalError(filePath, IsUncleanExit: true);
    }

    internal PendingFatalError? ReadPending()
    {
        try
        {
            if (!File.Exists(_pendingPath))
                return null;

            // 잘못되거나 손상된 표식으로 임의 경로를 읽지 않습니다.
            if (new FileInfo(_pendingPath).Length > 256)
                return null;
            string fileName = File.ReadAllText(_pendingPath).Trim();
            if (!(fileName.StartsWith("fatal-", StringComparison.Ordinal) || fileName.StartsWith("interrupted-", StringComparison.Ordinal)) ||
                !fileName.EndsWith(".log", StringComparison.Ordinal) ||
                fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                Path.GetFileName(fileName) != fileName)
                return null;

            if (File.Exists(_acknowledgedPath) && new FileInfo(_acknowledgedPath).Length <= 256 &&
                File.ReadAllText(_acknowledgedPath).Trim() == fileName)
                return null;

            return new PendingFatalError(Path.Combine(_logDirectory, fileName),
                IsUncleanExit: fileName.StartsWith("interrupted-", StringComparison.Ordinal));
        }
        catch
        {
            return null;
        }
    }

    internal void Acknowledge(PendingFatalError previous)
    {
        try
        {
            if (ReadPending()?.LogPath == previous.LogPath)
            {
                // pending 파일을 삭제하면 확인/삭제 사이 새 오류가 덮어쓴 표식까지 잃을 수 있습니다.
                // 확인한 로그 이름만 별도로 남겨 새 오류의 안내 표식은 항상 유지합니다.
                File.WriteAllText(_acknowledgedPath, Path.GetFileName(previous.LogPath));
            }
        }
        catch { /* 안내 표식 정리 실패로 앱 시작을 중단하지 않습니다. */ }
    }

    internal static string ReadDetails(PendingFatalError previous)
    {
        try
        {
            using StreamReader reader = new(previous.LogPath);
            char[] buffer = new char[MaxDetailsLength];
            int length = reader.ReadBlock(buffer, 0, buffer.Length);
            return length == 0 ? GetMissingDetailsMessage(previous) : new string(buffer, 0, length);
        }
        catch
        {
            return GetMissingDetailsMessage(previous);
        }
    }

    private static string GetMissingDetailsMessage(PendingFatalError previous) => previous.IsUncleanExit
        ? "이전 실행의 정상 종료 기록을 확인하지 못했습니다. 예외 정보가 없어 종료 원인은 알 수 없으며, 종료 기록 파일도 읽을 수 없습니다."
        : MissingDetailsMessage;
}

internal sealed record PendingFatalError(string LogPath, bool IsUncleanExit = false);
