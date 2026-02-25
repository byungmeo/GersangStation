using SevenZipExtractor;

namespace Core.Extractor;

public sealed class SevenZipExtractorAdapter : IExtractor
{
    public string Name => "SevenZipExtractor";

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
}
