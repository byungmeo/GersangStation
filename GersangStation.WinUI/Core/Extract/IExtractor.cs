namespace Core.Extract;

public interface IExtractor
{
    string Name { get; }

    bool CanHandle(string archivePath);

    Task ExtractAsync(
        string archivePath,
        string destinationPath,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken ct = default);
}

/// <summary>
/// 추출기별 아카이브 지원 여부를 이유와 예외 문맥까지 포함해 진단할 수 있게 합니다.
/// </summary>
public interface IExtractorSupportProbe
{
    ExtractorSupportProbeResult ProbeSupport(string archivePath);
}

/// <summary>
/// 추출기 지원 여부 확인 결과를 상위 오케스트레이션으로 전달합니다.
/// </summary>
public sealed record ExtractorSupportProbeResult(
    bool CanHandle,
    string Reason,
    Exception? Exception = null);

public sealed record ExtractionProgress(
    string ExtractorName,
    int Percentage,
    int ProcessedEntries,
    int? TotalEntries,
    string? CurrentArchive);
