namespace Core.Extractor;

public interface IExtractor
{
    string Name { get; }

    bool CanHandle(string archivePath);

    Task ExtractAsync(
        string archivePath,
        string destinationRoot,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken ct = default);
}

public sealed record ExtractionProgress(
    string ExtractorName,
    string? CurrentEntry,
    int ProcessedEntries,
    int? TotalEntries,
    double? Percentage = null);
