using HtmlAgilityPack;
using System.Globalization;

namespace Core;

/// <summary>
/// GersangStation GitHub Discussions 공지사항 카테고리에서 추출한 개별 공지 정보를 나타냅니다.
/// </summary>
public sealed record GersangStationNoticeInfo(
    string Title,
    string Url,
    string DateText,
    DateOnly? Date,
    bool IsPinned);

/// <summary>
/// GersangStation GitHub Discussions 공지사항 카테고리에서 표시용 공지 목록을 수집합니다.
/// </summary>
public static class GersangStationNoticeCrawler
{
    /// <summary>
    /// GersangStation 공지 크롤링 실패 지점을 구분합니다.
    /// </summary>
    public enum CrawlerFailureStage
    {
        FetchCategoryHtml,
        ParseNoticeList
    }

    /// <summary>
    /// GersangStation 공지 크롤링 실패 시 단계와 URL 문맥을 함께 보존합니다.
    /// </summary>
    public sealed class GersangStationNoticeCrawlerException : InvalidOperationException
    {
        public CrawlerFailureStage Stage { get; }
        public string Url { get; }

        public GersangStationNoticeCrawlerException(
            string message,
            CrawlerFailureStage stage,
            string url,
            Exception innerException)
            : base(message, innerException)
        {
            Stage = stage;
            Url = url;
        }
    }

    private const int MaxNoticeCount = 5;
    private static readonly Uri BaseUri = new("https://github.com");
    private static readonly Uri CategoryUri = new("https://github.com/byungmeo/GersangStation/discussions/categories/%EA%B3%B5%EC%A7%80%EC%82%AC%ED%95%AD");

    /// <summary>
    /// 고정 공지와 최신 공지를 합쳐 최대 5개의 표시용 공지 목록을 반환합니다.
    /// </summary>
    public static async Task<IReadOnlyList<GersangStationNoticeInfo>> GetNoticesAsync(
        HttpClient httpClient,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        string html;
        try
        {
            html = await GetCategoryHtmlAsync(httpClient, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new GersangStationNoticeCrawlerException(
                $"Failed to fetch GersangStation notices from '{CategoryUri}'.",
                CrawlerFailureStage.FetchCategoryHtml,
                CategoryUri.ToString(),
                ex);
        }

        try
        {
            return ParseNotices(html);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new GersangStationNoticeCrawlerException(
                $"Failed to parse GersangStation notices from '{CategoryUri}'.",
                CrawlerFailureStage.ParseNoticeList,
                CategoryUri.ToString(),
                ex);
        }
    }

    private static async Task<string> GetCategoryHtmlAsync(HttpClient httpClient, CancellationToken ct)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, CategoryUri);
        request.Headers.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/144.0.0.0 Safari/537.36");
        request.Headers.TryAddWithoutValidation("Accept-Language", "ko-KR,ko;q=0.9,en-US;q=0.8,en;q=0.7");
        request.Headers.Referrer = BaseUri;

        using HttpResponseMessage response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static IReadOnlyList<GersangStationNoticeInfo> ParseNotices(string html)
    {
        HtmlDocument doc = new();
        doc.LoadHtml(html);

        List<GersangStationNoticeInfo> pinnedItems = ParsePinnedNotices(doc);
        List<GersangStationNoticeInfo> allItems = ParseAllNotices(doc);

        List<GersangStationNoticeInfo> result = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (GersangStationNoticeInfo item in pinnedItems)
        {
            if (seen.Add(item.Url))
                result.Add(item);
        }

        foreach (GersangStationNoticeInfo item in allItems)
        {
            if (result.Count >= MaxNoticeCount)
                break;

            if (seen.Add(item.Url))
                result.Add(item with { IsPinned = false });
        }

        if (result.Count == 0)
            throw new InvalidDataException("No notice entries were found in the GersangStation discussions category HTML.");

        return result;
    }

    private static List<GersangStationNoticeInfo> ParsePinnedNotices(HtmlDocument doc)
    {
        HtmlNode? pinnedList = doc.DocumentNode.SelectSingleNode("//ul[@aria-labelledby='pinned-discussions-list']");
        if (pinnedList is null)
            return [];

        return ParseDiscussionList(pinnedList, isPinned: true);
    }

    private static List<GersangStationNoticeInfo> ParseAllNotices(HtmlDocument doc)
    {
        List<GersangStationNoticeInfo> result = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        HtmlNodeCollection? titleAnchors = doc.DocumentNode.SelectNodes("//a[contains(@class,'markdown-title') and contains(@href,'/byungmeo/GersangStation/discussions/')]");
        if (titleAnchors is null)
            return result;

        foreach (HtmlNode titleAnchor in titleAnchors)
        {
            GersangStationNoticeInfo? item = TryParseDiscussionFromAnchor(titleAnchor, isPinned: false);
            if (item is null)
                continue;

            if (seen.Add(item.Url))
                result.Add(item);
        }

        return result;
    }

    private static List<GersangStationNoticeInfo> ParseDiscussionList(HtmlNode listNode, bool isPinned)
    {
        List<GersangStationNoticeInfo> result = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        HtmlNodeCollection? titleAnchors = listNode.SelectNodes(".//a[contains(@class,'markdown-title') and contains(@href,'/byungmeo/GersangStation/discussions/')]");
        if (titleAnchors is null)
            return result;

        foreach (HtmlNode titleAnchor in titleAnchors)
        {
            GersangStationNoticeInfo? item = TryParseDiscussionFromAnchor(titleAnchor, isPinned);
            if (item is null)
                continue;

            if (seen.Add(item.Url))
                result.Add(item);
        }

        return result;
    }

    private static GersangStationNoticeInfo? TryParseDiscussionFromAnchor(HtmlNode titleAnchor, bool isPinned)
    {
        string url = ToAbsoluteUrl(titleAnchor.GetAttributeValue("href", string.Empty));
        string title = NormalizeText(HtmlEntity.DeEntitize(titleAnchor.InnerText));
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(title))
            return null;

        HtmlNode? rowNode = titleAnchor.Ancestors("li").FirstOrDefault();
        HtmlNode? relativeTimeNode = rowNode?.SelectSingleNode(".//relative-time[@datetime]");
        string dateTimeText = relativeTimeNode?.GetAttributeValue("datetime", string.Empty) ?? string.Empty;

        DateOnly? date = null;
        string dateText = string.Empty;

        if (DateTimeOffset.TryParse(dateTimeText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset timestamp))
        {
            date = DateOnly.FromDateTime(timestamp.LocalDateTime);
            dateText = date.Value.ToString("yy.MM.dd", CultureInfo.InvariantCulture);
        }

        return new GersangStationNoticeInfo(
            Title: title,
            Url: url,
            DateText: dateText,
            Date: date,
            IsPinned: isPinned);
    }

    private static string NormalizeText(string text)
        => string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : HtmlEntity.DeEntitize(text).Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static string ToAbsoluteUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? absolute))
            return absolute.ToString();

        return new Uri(BaseUri, url).ToString();
    }
}
