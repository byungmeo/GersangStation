using System.IO.Compression;

namespace Core.Extractor;

public sealed class ZipFileExtractor : IExtractor
{
    public string Name => "System.IO.Compression.ZipFile";

    public bool CanHandle(string archivePath)
        => string.Equals(Path.GetExtension(archivePath), ".zip", StringComparison.OrdinalIgnoreCase);

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

        using var archive = ZipFile.OpenRead(archivePath);
        var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();

        int processed = 0;
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            string relativePath = SharpCompressExtractor.NormalizeRelativePath(entry.FullName);
            string destinationPath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));

            SharpCompressExtractor.EnsurePathInRoot(normalizedRoot, destinationPath, entry.FullName, destinationRoot);

            string? destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir))
                Directory.CreateDirectory(destinationDir);

            entry.ExtractToFile(destinationPath, overwrite: true);

            processed++;
            progress?.Report(new ExtractionProgress(Name, entry.FullName, processed, entries.Count));
        }

        return Task.CompletedTask;
    }
}
