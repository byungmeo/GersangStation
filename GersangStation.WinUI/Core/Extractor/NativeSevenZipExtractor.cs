using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Core.Extractor;

/// <summary>
/// 7za 커맨드라인(7za.exe)을 사용해 압축을 풉니다.
/// </summary>
public sealed class NativeSevenZipExtractor : IExtractor
{
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

    public bool CanHandle(string archivePath) => File.Exists(archivePath);

    public async Task ExtractAsync(
        string archivePath,
        string destinationRoot,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath)) throw new ArgumentException("archivePath is required.", nameof(archivePath));
        if (string.IsNullOrWhiteSpace(destinationRoot)) throw new ArgumentException("destinationRoot is required.", nameof(destinationRoot));

        Directory.CreateDirectory(destinationRoot);

        int? totalEntries = await TryGetTotalEntriesAsync(archivePath, ct).ConfigureAwait(false);
        Debug.WriteLine($"[7ZA][LIST][PARSED] TotalEntries={totalEntries?.ToString() ?? "null"}");

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

        process.OutputDataReceived += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(args.Data)) return;

            Debug.WriteLine($"[7ZA][OUT] {args.Data}");

            var match = ExtractionProgressRegex.Match(args.Data);
            if (!match.Success)
            {
                Debug.WriteLine($"[7ZA][PROGRESS][SKIP] {args.Data}");
                return;
            }

            if (!int.TryParse(match.Groups["percent"].Value, out int percent))
            {
                Debug.WriteLine($"[7ZA][PROGRESS][PARSE-FAIL] percent line={args.Data}");
                return;
            }

            int processedEntries = 0;
            if (match.Groups["processed"].Success && !int.TryParse(match.Groups["processed"].Value, out processedEntries))
                Debug.WriteLine($"[7ZA][PROGRESS][PARSE-FAIL] processed line={args.Data}");

            var currentEntry = match.Groups["entry"].Success ? match.Groups["entry"].Value : null;
            Debug.WriteLine($"[7ZA][PROGRESS][PARSED] percent={percent}, processed={processedEntries}, entry={currentEntry ?? "(null)"}, total={totalEntries?.ToString() ?? "null"}");
            progress?.Report(new ExtractionProgress(Name, percent, processedEntries, totalEntries, currentEntry));
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(args.Data)) return;
            Debug.WriteLine($"[7ZA][ERR] {args.Data}");
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
                // ignore kill race
            }
        });

        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        if (ct.IsCancellationRequested)
            ct.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
            throw new InvalidDataException($"7za extraction failed with exitCode={process.ExitCode}.");

        Debug.WriteLine("[7ZA][PROGRESS][COMPLETE] exitCode=0");
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

        string normalizedPrimaryRoot = Path.GetFullPath(primaryRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var failedDestinations = new List<string>();

        for (int i = 1; i < destinationRoots.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            string destinationRoot = destinationRoots[i];
            if (string.IsNullOrWhiteSpace(destinationRoot))
            {
                failedDestinations.Add($"(index:{i}) <empty>");
                continue;
            }

            string normalizedDestinationRoot = Path.GetFullPath(destinationRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(normalizedPrimaryRoot, normalizedDestinationRoot, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                Directory.CreateDirectory(normalizedDestinationRoot);
                await ReplicateDirectoryAsync(
                    normalizedPrimaryRoot,
                    normalizedDestinationRoot,
                    ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failedDestinations.Add(normalizedDestinationRoot);
            }
        }

        if (failedDestinations.Count > 0)
            throw new IOException($"Replication failed for {failedDestinations.Count} destination(s): {string.Join(", ", failedDestinations)}");
    }

    private static async Task ReplicateDirectoryAsync(
        string sourceRoot,
        string destinationRoot,
        CancellationToken ct)
    {
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

    private async Task<int?> TryGetTotalEntriesAsync(string archivePath, CancellationToken ct)
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
            Debug.WriteLine($"[7ZA][LIST][ERR] exitCode={process.ExitCode}, stderr={stderr}");
            return null;
        }

        var lines = stdout
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        const int tailLineCount = 5;
        int startIndex = Math.Max(0, lines.Length - tailLineCount);

        Debug.WriteLine($"[7ZA][LIST][TAIL] totalLines={lines.Length}, checkingLast={lines.Length - startIndex}");

        // 전체 출력 전부를 파싱하지 않고, 마지막 몇 줄만 검사합니다.
        for (int i = lines.Length - 1; i >= startIndex; i--)
        {
            string line = lines[i];
            Debug.WriteLine($"[7ZA][LIST][TAIL][OUT] {line}");

            var match = ListingSummaryRegex.Match(line);
            if (!match.Success)
                continue;

            if (!int.TryParse(match.Groups["files"].Value, out int files))
            {
                Debug.WriteLine($"[7ZA][LIST][PARSE-FAIL] files line={line}");
                continue;
            }

            if (!int.TryParse(match.Groups["folders"].Value, out int folders))
            {
                Debug.WriteLine($"[7ZA][LIST][PARSE-FAIL] folders line={line}");
                continue;
            }

            Debug.WriteLine($"[7ZA][LIST][PARSED] files={files}, folders={folders}, total={files + folders}");
            return files + folders;
        }

        Debug.WriteLine("[7ZA][LIST][PARSED] summary not found in tail lines.");
        return null;
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
                Debug.WriteLine($"Resolved 7za.exe Path: {candidate}");
                return candidate;
            }
        }

        throw new FileNotFoundException("7za.exe not found.");
    }
}
