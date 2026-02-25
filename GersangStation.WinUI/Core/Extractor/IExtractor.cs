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
    int Percentage,
    int ProcessedEntries,
    int? TotalEntries,
    string? CurrentEntry);
