using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Core.Extractor;

/// <summary>
/// 7za 커맨드라인(7za.exe)을 사용해 압축을 풉니다.
/// 7za.dll은 배포 시 동일 폴더에 두어 네이티브 의존성을 맞춥니다.
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
            Path.Combine(AppContext.BaseDirectory, "7zip", "7za.exe"),
            Path.Combine(AppContext.BaseDirectory, "Extractor", "7zip", "7za.exe"),
            Path.Combine(AppContext.BaseDirectory, "Core", "Extractor", "7zip", "7za.exe"),
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
