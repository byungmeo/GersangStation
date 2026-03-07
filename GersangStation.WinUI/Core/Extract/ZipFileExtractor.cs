using System.IO.Compression;

namespace Core.Extract;

public sealed class ZipFileExtractor : IExtractor
{
    public string Name => "System.IO.Compression.ZipFile Wrapper";

    public bool CanHandle(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
            return false;

        if (!File.Exists(archivePath))
            return false;

        try
        {
            using ZipArchive _ = ZipFile.OpenRead(archivePath);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public async Task ExtractAsync(
        string archivePath,
        string destinationPath,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
            throw new ArgumentException("archivePath is required.", nameof(archivePath));

        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("destinationPath is required.", nameof(destinationPath));

        Directory.CreateDirectory(destinationPath);

        using ZipArchive archive = await ZipFile.OpenReadAsync(archivePath, ct).ConfigureAwait(false);

        int totalEntries = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (!string.IsNullOrEmpty(entry.Name))
                totalEntries++;
        }

        if (totalEntries == 0)
        {
            progress?.Report(new ExtractionProgress(Name, 100, 0, 0, null));
            return;
        }

        string normalizedDestinationRoot = Path.GetFullPath(destinationPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        int processedEntries = 0;

        string currentArchive = Path.GetFileName(archivePath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();

            string destinationFullPath = Path.GetFullPath(
                Path.Combine(destinationPath, entry.FullName));

            if (!destinationFullPath.StartsWith(normalizedDestinationRoot, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"Entry escapes destination directory: {entry.FullName}");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationFullPath);
                continue;
            }

            string? parentDirectory = Path.GetDirectoryName(destinationFullPath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
                Directory.CreateDirectory(parentDirectory);

            await entry.ExtractToFileAsync(
                destinationFullPath,
                overwrite: true,
                cancellationToken: ct).ConfigureAwait(false);

            processedEntries++;

            int percentage = (int)((long)processedEntries * 100 / totalEntries);

            progress?.Report(new ExtractionProgress(
                Name,
                percentage,
                processedEntries,
                totalEntries,
                currentArchive));
        }
    }
}