namespace Core;

/// <summary>
/// 기존 이벤트 크롤러 호출부 호환을 위해 GameHomepageCrawler를 위임 호출합니다.
/// </summary>
public static class GameEventCrawler
{
    public static Task<IReadOnlyList<GameEventInfo>> GetAllEventsAsync(
        HttpClient httpClient,
        CancellationToken ct = default)
        => GameHomepageCrawler.GetAllEventsAsync(httpClient, ct);

    public static Task<IReadOnlyList<GameEventInfo>> GetAllEventsAsync(
        HttpClient httpClient,
        GameEventLoadOptions options,
        CancellationToken ct = default)
        => GameHomepageCrawler.GetAllEventsAsync(httpClient, options, ct);

    public static Task DebugWriteAllEventsAsync(
        HttpClient httpClient,
        CancellationToken ct = default)
        => GameHomepageCrawler.DebugWriteAllEventsAsync(httpClient, ct);
}
