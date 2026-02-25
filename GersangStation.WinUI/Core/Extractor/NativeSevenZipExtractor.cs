using System.Runtime.InteropServices;
using SevenZipExtractor;

namespace Core.Extractor;

/// <summary>
/// 저장소에 포함한 네이티브 7z.dll을 직접 로드해서 사용하는 압축 해제기.
/// </summary>
public sealed class NativeSevenZipExtractor : IExtractor
{
    private readonly string _libraryPath;

    public NativeSevenZipExtractor(string? libraryPath = null)
    {
        _libraryPath = ResolveLibraryPath(libraryPath);
        NativeLibrary.Load(_libraryPath);
    }

    public string Name => "Native 7-Zip (7z.dll)";

    public bool CanHandle(string archivePath) => File.Exists(archivePath);

    public Task ExtractAsync(
        string archivePath,
        string destinationRoot,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath)) throw new ArgumentException("archivePath is required.", nameof(archivePath));
        if (string.IsNullOrWhiteSpace(destinationRoot)) throw new ArgumentException("destinationRoot is required.", nameof(destinationRoot));

        Directory.CreateDirectory(destinationRoot);
        string normalizedRoot = SharpCompressExtractor.NormalizeRoot(destinationRoot);

        using var archive = new ArchiveFile(archivePath);
        var entries = archive.Entries.Where(entry => !entry.IsFolder).ToList();

        int processed = 0;
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            string relativePath = SharpCompressExtractor.NormalizeRelativePath(entry.FileName);
            string destinationPath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));

            SharpCompressExtractor.EnsurePathInRoot(normalizedRoot, destinationPath, entry.FileName, destinationRoot);

            string? destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir))
                Directory.CreateDirectory(destinationDir);

            entry.Extract(destinationPath);

            processed++;
            progress?.Report(new ExtractionProgress(Name, entry.FileName, processed, entries.Count));
        }

        return Task.CompletedTask;
    }

    private static string ResolveLibraryPath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            return explicitPath;

        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "7z.dll"),
            Path.Combine(AppContext.BaseDirectory, "7zip", "7z.dll"),
            Path.Combine(AppContext.BaseDirectory, "Extractor", "7zip", "7z.dll"),
            Path.Combine(AppContext.BaseDirectory, "Core", "Extractor", "7zip", "7z.dll")
        ];

        string? found = candidates.FirstOrDefault(File.Exists);
        if (found is not null)
            return found;

        throw new FileNotFoundException("7z.dll not found. Place it at Core/Extractor/7zip/7z.dll.");
    }
}
