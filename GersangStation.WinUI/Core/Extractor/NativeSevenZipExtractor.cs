using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Core.Extractor;

/// <summary>
/// 7za 커맨드라인(7za.exe)을 사용해 압축을 풉니다.
/// 7za.dll은 배포 시 동일 폴더에 두어 네이티브 의존성을 맞춥니다.
/// </summary>
public sealed class NativeSevenZipExtractor : IExtractor
{
    private static readonly Regex PercentageRegex = new(@"\b(?<percent>\d{1,3})%\b", RegexOptions.Compiled);
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

            var match = PercentageRegex.Match(args.Data);
            if (match.Success && int.TryParse(match.Groups["percent"].Value, out int percent))
                progress?.Report(new ExtractionProgress(Name, null, 0, null, percent));
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

        progress?.Report(new ExtractionProgress(Name, null, 0, null, 100));
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
