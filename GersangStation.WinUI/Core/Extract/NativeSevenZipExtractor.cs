using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Core.Extract;

/// <summary>
/// 7za 커맨드라인(7za.exe)을 사용해 압축을 풉니다.
/// .gsz 파일 압축 해제 시 파일 정합성을 보장할 수 없으므로, 7z 파일 압축 해제에만 사용 권장
/// </summary>
public sealed class NativeSevenZipExtractor : IExtractor, IExtractorSupportProbe
{
    public enum ExtractionFailureStage
    {
        PrepareDestinationRoot,
        AnalyzeArchive,
        RunSevenZip,
        ReplicateDirectory
    }

    private enum ArchiveListingFailureStage
    {
        RunListCommand,
        ParseSummary
    }

    /// <summary>
    /// 7za 기반 추출 실패 시 단계, 경로, 종료 코드를 함께 전달합니다.
    /// </summary>
    public sealed class ExtractionOperationException : IOException
    {
        public string ArchivePath { get; }
        public string DestinationPath { get; }
        public ExtractionFailureStage Stage { get; }
        public int? ExitCode { get; }

        public ExtractionOperationException(
            string message,
            string archivePath,
            string destinationPath,
            ExtractionFailureStage stage,
            Exception innerException,
            int? exitCode = null)
            : base(message, innerException)
        {
            ArchivePath = archivePath;
            DestinationPath = destinationPath;
            Stage = stage;
            ExitCode = exitCode;
        }

        public ExtractionOperationException(
            string message,
            string archivePath,
            string destinationPath,
            ExtractionFailureStage stage,
            int exitCode)
            : base(message)
        {
            ArchivePath = archivePath;
            DestinationPath = destinationPath;
            Stage = stage;
            ExitCode = exitCode;
        }
    }

    private sealed record ArchiveListingProbeResult(
        bool Success,
        int? TotalEntries,
        ArchiveListingFailureStage? FailureStage,
        string? FailureReason,
        int? ExitCode);

    private static readonly Regex ExtractionProgressRegex = new(
        @"^\s*(?<percent>\d{1,3})%\s*(?<processed>\d+)?(?:\s*-\s*(?<entry>.+))?$",
        RegexOptions.Compiled);
    private static readonly Regex ListingSummaryRegex = new(
        @"\s(?<files>\d+)\s+files,\s+(?<folders>\d+)\s+folders\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly string _sevenZipExePath;

    public NativeSevenZipExtractor(string? sevenZipExePath = null)
    {
        _sevenZipExePath = ResolveSevenZipExecutablePath(sevenZipExePath);
    }

    public string Name => "Native 7-Zip CLI (7za)";

    public bool CanHandle(string archivePath)
    {
        return ProbeSupport(archivePath).CanHandle;
    }

    /// <summary>
    /// 7za 추출기가 현재 아카이브를 처리할 수 있는지 실패 이유와 함께 반환합니다.
    /// </summary>
    public ExtractorSupportProbeResult ProbeSupport(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
            return new(false, "archive path is empty.");

        if (!File.Exists(archivePath))
            return new(false, $"archive file does not exist: {archivePath}");

        if (!File.Exists(_sevenZipExePath))
            return new(false, $"7za executable does not exist: {_sevenZipExePath}");

        return new(true, string.Empty);
    }

    public async Task ExtractAsync(
        string archivePath,
        string destinationRoot,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath)) throw new ArgumentException("archivePath is required.", nameof(archivePath));
        if (string.IsNullOrWhiteSpace(destinationRoot)) throw new ArgumentException("destinationRoot is required.", nameof(destinationRoot));

        try
        {
            Directory.CreateDirectory(destinationRoot);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ExtractionOperationException(
                $"Failed to prepare extraction destination '{destinationRoot}'.",
                archivePath,
                destinationRoot,
                ExtractionFailureStage.PrepareDestinationRoot,
                ex);
        }

        int? totalEntries;
        try
        {
            ArchiveListingProbeResult listingResult = await TryGetTotalEntriesAsync(archivePath, ct).ConfigureAwait(false);
            totalEntries = listingResult.TotalEntries;
            if (!listingResult.Success)
            {
                WriteLog(
                    $"[7ZA][LIST][FALLBACK] stage={listingResult.FailureStage?.ToString() ?? "unknown"}, " +
                    $"exitCode={listingResult.ExitCode?.ToString() ?? "null"}, reason={listingResult.FailureReason ?? "unknown"}");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ExtractionOperationException(
                $"Failed to inspect archive '{archivePath}' before extraction.",
                archivePath,
                destinationRoot,
                ExtractionFailureStage.AnalyzeArchive,
                ex);
        }

        WriteLog($"[7ZA][LIST][PARSED] TotalEntries={totalEntries?.ToString() ?? "null"}");

        var psi = new ProcessStartInfo
        {
            FileName = _sevenZipExePath,
            Arguments = $"x \"{archivePath}\" -o\"{destinationRoot}\" -aoa -y -mmt=on -bsp1",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        string currentArchive = Path.GetFileName(archivePath);
        List<string> stderrLines = [];

        bool hasReportedCompletion = false;
        process.OutputDataReceived += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(args.Data)) return;

            WriteLog($"[7ZA][OUT] {args.Data}");

            var match = ExtractionProgressRegex.Match(args.Data);
            if (!match.Success)
            {
                WriteLog($"[7ZA][PROGRESS][SKIP] {args.Data}");
                return;
            }

            if (!int.TryParse(match.Groups["percent"].Value, out int percent))
            {
                WriteLog($"[7ZA][PROGRESS][PARSE-FAIL] percent line={args.Data}");
                return;
            }

            // 7za 출력에서 파싱한 보조 숫자이며, totalEntries와 동일 기준의 "처리 엔트리 수"를 보장하지 않습니다.
            int parsedProcessedValue = 0;
            if (match.Groups["processed"].Success && !int.TryParse(match.Groups["processed"].Value, out parsedProcessedValue))
                WriteLog($"[7ZA][PROGRESS][PARSE-FAIL] processed line={args.Data}");

            var currentEntry = match.Groups["entry"].Success ? match.Groups["entry"].Value : null;
            WriteLog($"[7ZA][PROGRESS][PARSED] percent={percent}, parsedProcessedValue={parsedProcessedValue}, entry={currentEntry ?? "(null)"}, total={totalEntries?.ToString() ?? "null"}");

            if (percent >= 100)
                hasReportedCompletion = true;

            progress?.Report(new ExtractionProgress(Name, percent, parsedProcessedValue, totalEntries, currentArchive));
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(args.Data)) return;
            stderrLines.Add(args.Data);
            WriteLog($"[7ZA][ERR] {args.Data}");
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cancellationRegistration = ct.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
        });

        await process.WaitForExitAsync().ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
        {
            string stderrSummary = stderrLines.Count == 0
                ? "stderr unavailable"
                : string.Join(" | ", stderrLines.TakeLast(5));

            throw new ExtractionOperationException(
                $"7za extraction failed with exitCode={process.ExitCode}. stderr={stderrSummary}",
                archivePath,
                destinationRoot,
                ExtractionFailureStage.RunSevenZip,
                process.ExitCode);
        }

        WriteLog("[7ZA][PROGRESS][COMPLETE] exitCode=0");

        if (!hasReportedCompletion)
            progress?.Report(new ExtractionProgress(Name, 100, totalEntries ?? 0, totalEntries, null));
    }

    /// <summary>
    /// 아카이브를 1회만 압축 해제한 뒤, 나머지 대상에는 파일 복제(기본: 복사)로 배포합니다.
    /// </summary>
    public async Task ExtractAndReplicateAsync(
        string archivePath,
        IReadOnlyList<string> destinationRoots,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath)) throw new ArgumentException("archivePath is required.", nameof(archivePath));
        if (destinationRoots is null) throw new ArgumentNullException(nameof(destinationRoots));
        if (destinationRoots.Count == 0) throw new ArgumentException("At least one destination root is required.", nameof(destinationRoots));

        string primaryRoot = destinationRoots[0];
        if (string.IsNullOrWhiteSpace(primaryRoot))
            throw new ArgumentException("Primary destination root is required.", nameof(destinationRoots));

        await ExtractAsync(archivePath, primaryRoot, progress, ct).ConfigureAwait(false);

        if (destinationRoots.Count == 1)
            return;

        string normalizedPrimaryRoot;
        try
        {
            normalizedPrimaryRoot = Path.GetFullPath(primaryRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ExtractionOperationException(
                $"Failed to normalize primary replication destination '{primaryRoot}'.",
                archivePath,
                primaryRoot,
                ExtractionFailureStage.ReplicateDirectory,
                ex);
        }

        var failedDestinations = new List<string>();
        Exception? firstReplicationException = null;

        for (int i = 1; i < destinationRoots.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            string destinationRoot = destinationRoots[i];
            if (string.IsNullOrWhiteSpace(destinationRoot))
            {
                failedDestinations.Add($"(index:{i}) <empty>");
                continue;
            }

            string normalizedDestinationRoot = destinationRoot;
            try
            {
                normalizedDestinationRoot = Path.GetFullPath(destinationRoot)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (string.Equals(normalizedPrimaryRoot, normalizedDestinationRoot, StringComparison.OrdinalIgnoreCase))
                    continue;

                Directory.CreateDirectory(normalizedDestinationRoot);
                await ReplicateDirectoryAsync(
                    normalizedPrimaryRoot,
                    normalizedDestinationRoot,
                    ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                firstReplicationException ??= ex;
                failedDestinations.Add($"{normalizedDestinationRoot} ({ex.GetType().Name}: {ex.Message})");
            }
        }

        if (failedDestinations.Count > 0)
        {
            throw new ExtractionOperationException(
                $"Replication failed for {failedDestinations.Count} destination(s): {string.Join(", ", failedDestinations)}",
                archivePath,
                string.Join(", ", destinationRoots),
                ExtractionFailureStage.ReplicateDirectory,
                firstReplicationException is null
                    ? new IOException("Failed to replicate extracted contents to one or more destinations.")
                    : new IOException("Failed to replicate extracted contents to one or more destinations.", firstReplicationException));
        }
    }

    private static async Task ReplicateDirectoryAsync(
        string sourceRoot,
        string destinationRoot,
        CancellationToken ct)
    {
        foreach (string sourceDirectory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            string relativeDirectory = Path.GetRelativePath(sourceRoot, sourceDirectory);
            string destinationDirectory = Path.Combine(destinationRoot, relativeDirectory);

            Directory.CreateDirectory(destinationDirectory);
        }

        foreach (string sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            string relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
            string destinationPath = Path.Combine(destinationRoot, relativePath);

            string? destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDir))
                Directory.CreateDirectory(destinationDir);

            await CopyFileAsync(sourcePath, destinationPath, ct).ConfigureAwait(false);
        }
    }

    private static async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken ct)
    {
        const int bufferSize = 1024 * 128;

        await using var sourceStream = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize,
            useAsync: true);

        await using var destinationStream = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize,
            useAsync: true);

        await sourceStream.CopyToAsync(destinationStream, bufferSize, ct).ConfigureAwait(false);
    }

    private async Task<ArchiveListingProbeResult> TryGetTotalEntriesAsync(string archivePath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _sevenZipExePath,
            Arguments = $"l \"{archivePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        string stdout = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        string stderr = await process.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);

        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            WriteLog($"[7ZA][LIST][ERR] exitCode={process.ExitCode}, stderr={stderr}");
            return new ArchiveListingProbeResult(
                false,
                null,
                ArchiveListingFailureStage.RunListCommand,
                $"7za list command failed. stderr={stderr}",
                process.ExitCode);
        }

        var lines = stdout
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        const int tailLineCount = 5;
        int startIndex = Math.Max(0, lines.Length - tailLineCount);

        WriteLog($"[7ZA][LIST][TAIL] totalLines={lines.Length}, checkingLast={lines.Length - startIndex}");

        // 전체 출력 전부를 파싱하지 않고, 마지막 몇 줄만 검사합니다.
        for (int i = lines.Length - 1; i >= startIndex; i--)
        {
            string line = lines[i];
            WriteLog($"[7ZA][LIST][TAIL][OUT] {line}");

            var match = ListingSummaryRegex.Match(line);
            if (!match.Success)
                continue;

            if (!int.TryParse(match.Groups["files"].Value, out int files))
            {
                WriteLog($"[7ZA][LIST][PARSE-FAIL] files line={line}");
                continue;
            }

            if (!int.TryParse(match.Groups["folders"].Value, out int folders))
            {
                WriteLog($"[7ZA][LIST][PARSE-FAIL] folders line={line}");
                continue;
            }

            WriteLog($"[7ZA][LIST][PARSED] files={files}, folders={folders}, totalFiles={files}");
            return new ArchiveListingProbeResult(true, files, null, null, process.ExitCode);
        }

        WriteLog("[7ZA][LIST][PARSED] summary not found in tail lines.");
        return new ArchiveListingProbeResult(
            false,
            null,
            ArchiveListingFailureStage.ParseSummary,
            "summary not found in tail lines",
            process.ExitCode);
    }

    private static string ResolveSevenZipExecutablePath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            return explicitPath;

        string[] localCandidates =
        [
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            Path.Combine(AppContext.BaseDirectory, "Include", "7za.exe"),
            Path.Combine(AppContext.BaseDirectory, "Core", "Include", "7za.exe"),
        ];

        foreach (var candidate in localCandidates)
        {
            if (File.Exists(candidate))
            {
                WriteLog($"Resolved 7za.exe Path: {candidate}");
                return candidate;
            }
        }

        throw new FileNotFoundException("7za.exe not found.");
    }

    private static void WriteLog(string message)
    {
#if DEBUGGING
        Debug.WriteLine(message);
#endif
    }
}
