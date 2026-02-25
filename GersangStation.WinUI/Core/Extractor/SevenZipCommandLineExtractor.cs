using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Core.Extractor;

public sealed class SevenZipCommandLineExtractor : IExtractor
{
    private static readonly Regex PercentageRegex = new(@"\b(?<percent>\d{1,3})%\b", RegexOptions.Compiled);
    private readonly string _sevenZipExePath;

    public SevenZipCommandLineExtractor(string? sevenZipExePath = null)
    {
        _sevenZipExePath = ResolveSevenZipExecutablePath(sevenZipExePath);
    }

    public string Name => "7za.exe (7-Zip.CommandLine)";

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
            Arguments = $"x \"{archivePath}\" -o\"{destinationRoot}\" -y -bsp1",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Start();

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

        Task readStdOut = Task.Run(async () =>
        {
            while (!process.StandardOutput.EndOfStream)
            {
                string? line = await process.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false);
                if (line is null) continue;

                var match = PercentageRegex.Match(line);
                if (match.Success && int.TryParse(match.Groups["percent"].Value, out int percent))
                {
                    progress?.Report(new ExtractionProgress(Name, null, 0, null, percent));
                }
            }
        }, ct);

        string stdErr = await process.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);

        await Task.WhenAll(readStdOut, process.WaitForExitAsync(ct)).ConfigureAwait(false);

        if (ct.IsCancellationRequested)
            ct.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
            throw new InvalidDataException($"7za extraction failed with exitCode={process.ExitCode}. stderr={stdErr}");

        progress?.Report(new ExtractionProgress(Name, null, 0, null, 100));
    }

    private static string ResolveSevenZipExecutablePath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            return explicitPath;

        string[] localCandidates =
        [
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            Path.Combine(AppContext.BaseDirectory, "tools", "7za.exe")
        ];

        foreach (var candidate in localCandidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string packageRoot = Path.Combine(userProfile, ".nuget", "packages", "7-zip.commandline");

        if (Directory.Exists(packageRoot))
        {
            var versionDir = Directory.EnumerateDirectories(packageRoot)
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (versionDir is not null)
            {
                string path = Path.Combine(versionDir, "tools", "7za.exe");
                if (File.Exists(path))
                    return path;
            }
        }

        throw new FileNotFoundException("7za.exe not found. Ensure 7-Zip.CommandLine package is restored correctly.");
    }
}
