using System.IO.Compression;

namespace Core.Extract;

public sealed class ZipFileExtractor : IExtractor
{
    /// <summary>
    /// Zip 추출 실패 지점을 구분합니다.
    /// </summary>
    public enum ZipExtractionFailureStage
    {
        PrepareDestinationRoot,
        OpenArchive,
        ValidateEntryPath,
        ExtractEntry
    }

    /// <summary>
    /// Zip 추출 실패 시 단계, 아카이브, 대상 경로, 엔트리명을 함께 보존합니다.
    /// </summary>
    public sealed class ZipExtractionOperationException : IOException
    {
        public string ArchivePath { get; }
        public string DestinationPath { get; }
        public string? EntryName { get; }
        public ZipExtractionFailureStage Stage { get; }

        public ZipExtractionOperationException(
            string message,
            string archivePath,
            string destinationPath,
            ZipExtractionFailureStage stage,
            Exception innerException,
            string? entryName = null)
            : base(message, innerException)
        {
            ArchivePath = archivePath;
            DestinationPath = destinationPath;
            EntryName = entryName;
            Stage = stage;
        }
    }

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

        try
        {
            Directory.CreateDirectory(destinationPath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ZipExtractionOperationException(
                $"Failed to prepare extraction destination '{destinationPath}'.",
                archivePath,
                destinationPath,
                ZipExtractionFailureStage.PrepareDestinationRoot,
                ex);
        }

        ZipArchive archive;
        try
        {
            archive = await ZipFile.OpenReadAsync(archivePath, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ZipExtractionOperationException(
                $"Failed to open zip archive '{archivePath}'.",
                archivePath,
                destinationPath,
                ZipExtractionFailureStage.OpenArchive,
                ex);
        }

        using (archive)
        {
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

            string normalizedDestinationRoot;
            try
            {
                normalizedDestinationRoot = Path.GetFullPath(destinationPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new ZipExtractionOperationException(
                    $"Failed to normalize extraction destination '{destinationPath}'.",
                    archivePath,
                    destinationPath,
                    ZipExtractionFailureStage.ValidateEntryPath,
                    ex);
            }

            int processedEntries = 0;

            string currentArchive = Path.GetFileName(archivePath);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();

                string destinationFullPath;
                try
                {
                    destinationFullPath = Path.GetFullPath(
                        Path.Combine(destinationPath, entry.FullName));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new ZipExtractionOperationException(
                        $"Failed to resolve destination path for zip entry '{entry.FullName}'.",
                        archivePath,
                        destinationPath,
                        ZipExtractionFailureStage.ValidateEntryPath,
                        ex,
                        entry.FullName);
                }

                if (!destinationFullPath.StartsWith(normalizedDestinationRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ZipExtractionOperationException(
                        $"Zip entry escapes destination directory: {entry.FullName}",
                        archivePath,
                        destinationPath,
                        ZipExtractionFailureStage.ValidateEntryPath,
                        new IOException($"Entry escapes destination directory: {entry.FullName}"),
                        entry.FullName);
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    try
                    {
                        Directory.CreateDirectory(destinationFullPath);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        throw new ZipExtractionOperationException(
                            $"Failed to create directory for zip entry '{entry.FullName}'.",
                            archivePath,
                            destinationPath,
                            ZipExtractionFailureStage.ExtractEntry,
                            ex,
                            entry.FullName);
                    }

                    continue;
                }

                string? parentDirectory = Path.GetDirectoryName(destinationFullPath);
                if (!string.IsNullOrWhiteSpace(parentDirectory))
                {
                    try
                    {
                        Directory.CreateDirectory(parentDirectory);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        throw new ZipExtractionOperationException(
                            $"Failed to create destination directory for zip entry '{entry.FullName}'.",
                            archivePath,
                            destinationPath,
                            ZipExtractionFailureStage.ExtractEntry,
                            ex,
                            entry.FullName);
                    }
                }

                try
                {
                    await entry.ExtractToFileAsync(
                        destinationFullPath,
                        overwrite: true,
                        cancellationToken: ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new ZipExtractionOperationException(
                        $"Failed to extract zip entry '{entry.FullName}'.",
                        archivePath,
                        destinationPath,
                        ZipExtractionFailureStage.ExtractEntry,
                        ex,
                        entry.FullName);
                }

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
}
