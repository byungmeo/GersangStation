using SharpCompress.Archives;

namespace Core.Extractor;

/// <summary>
/// 기존 SharpCompress 기반 압축 해제 로직을 레거시로 유지하기 위한 구현체.
/// </summary>
public sealed class SharpCompressExtractor : IExtractor
{
    public string Name => "SharpCompress (Legacy)";

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
        string normalizedRoot = NormalizeRoot(destinationRoot);

        using var archive = ArchiveFactory.OpenArchive(archivePath);
        var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();

        int processed = 0;
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            string relativePath = NormalizeRelativePath(entry.Key);
            string destinationPath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));

            EnsurePathInRoot(normalizedRoot, destinationPath, entry.Key ?? string.Empty, destinationRoot);

            string? destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir))
                Directory.CreateDirectory(destinationDir);

            using var sourceStream = entry.OpenEntryStream();
            using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            sourceStream.CopyTo(destinationStream);

            processed++;
            progress?.Report(new ExtractionProgress(Name, entry.Key, processed, entries.Count));
        }

        return Task.CompletedTask;
    }

    internal static string NormalizeRoot(string destinationRoot)
    {
        string normalizedRoot = Path.GetFullPath(destinationRoot);
        if (!normalizedRoot.EndsWith(Path.DirectorySeparatorChar))
            normalizedRoot += Path.DirectorySeparatorChar;

        return normalizedRoot;
    }

    internal static string NormalizeRelativePath(string? key)
        => (key ?? string.Empty)
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

    internal static void EnsurePathInRoot(string normalizedRoot, string destinationPath, string entryName, string destinationRoot)
    {
        if (!destinationPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Archive entry has invalid path. entry='{entryName}', root='{destinationRoot}'");
    }
}
