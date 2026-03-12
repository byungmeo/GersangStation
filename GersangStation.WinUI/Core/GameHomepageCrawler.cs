using HtmlAgilityPack;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Core;

/// <summary>
/// 거상 홈페이지 이벤트 목록에서 추출한 개별 이벤트 정보를 나타냅니다.
/// </summary>
public sealed record GameEventInfo(
    int Page,
    string Status,
    string Title,
    string Summary,
    string Period,
    string ThumbnailUrl,
    string? DetailUrl,
    DateOnly? StartDate,
    DateOnly? EndDate);

/// <summary>
/// 거상 메인 홈페이지 공지사항 영역에서 추출한 개별 공지 정보를 나타냅니다.
/// </summary>
public sealed record GameHomepageNoticeInfo(
    string Title,
    string DateText,
    string Url,
    DateOnly? Date);

/// <summary>
/// 이벤트 목록을 읽어올 때 적용할 선택적 필터를 나타냅니다.
/// </summary>
public readonly record struct GameEventLoadOptions(
    bool IgnorePlaceholderEndDate = false);

/// <summary>
/// 거상 홈페이지의 이벤트와 메인 공지사항 메타데이터를 수집합니다.
/// </summary>
public static class GameHomepageCrawler
{
    private const int MaxPagesToScan = 3;
    private static readonly Uri BaseUri = new("https://www.gersang.co.kr");
    private static readonly DateOnly PlaceholderEventEndDate = new(2999, 12, 31);
    private static readonly Regex PeriodRegex = new(
        @"\b(?<start>\d{4}-\d{2}-\d{2})\s*~\s*(?<end>\d{4}-\d{2}-\d{2})\b",
        RegexOptions.Compiled);
    private static readonly Regex StatusRegex = new(
        @"^(진행중|종료)$",
        RegexOptions.Compiled);
    private static readonly Regex PageRegex = new(
        @"(?:\?|&)page=(\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly string[] NoticeDateFormats = ["yy.MM.dd"];

    /// <summary>
    /// 이벤트 목록 페이지들을 순회하며 중복 없는 전체 이벤트 목록을 반환합니다.
    /// </summary>
    public static async Task<IReadOnlyList<GameEventInfo>> GetAllEventsAsync(
        HttpClient httpClient,
        CancellationToken ct = default)
        => await GetAllEventsAsync(httpClient, new GameEventLoadOptions(), ct);

    /// <summary>
    /// 이벤트 목록 페이지들을 순회하며 선택한 필터를 적용한 전체 이벤트 목록을 반환합니다.
    /// </summary>
    public static async Task<IReadOnlyList<GameEventInfo>> GetAllEventsAsync(
        HttpClient httpClient,
        GameEventLoadOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        string firstPageHtml = await GetEventListPageHtmlAsync(httpClient, 1, ct);
        int lastPage = ParseLastPage(firstPageHtml);

        List<GameEventInfo> result = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        for (int page = 1; page <= lastPage; page++)
        {
            string html = page == 1
                ? firstPageHtml
                : await GetEventListPageHtmlAsync(httpClient, page, ct);

            List<GameEventInfo> items = ParseEventsFromListPage(html, page);

            foreach (GameEventInfo item in items)
            {
                if (!ShouldIncludeEvent(item, options))
                    continue;

                string dedupKey = $"{item.Title}|{item.Period}|{item.ThumbnailUrl}";
                if (seen.Add(dedupKey))
                    result.Add(item);
            }
        }

        return result;
    }

    /// <summary>
    /// 거상 메인 홈페이지 공지사항 영역에서 최신 공지 목록을 반환합니다.
    /// </summary>
    public static async Task<IReadOnlyList<GameHomepageNoticeInfo>> GetHomepageNoticesAsync(
        HttpClient httpClient,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        string html = await GetHomepageHtmlAsync(httpClient, ct);
        return ParseHomepageNotices(html);
    }

    /// <summary>
    /// 크롤링 결과를 디버그 출력으로 남겨 파서 동작을 점검합니다.
    /// </summary>
    public static async Task DebugWriteAllEventsAsync(
        HttpClient httpClient,
        CancellationToken ct = default)
    {
        IReadOnlyList<GameEventInfo> items = await GetAllEventsAsync(httpClient, ct);

        Debug.WriteLine($"[GameHomepageCrawler] TotalEventCount = {items.Count}");

        foreach (GameEventInfo item in items)
        {
            Debug.WriteLine("--------------------------------------------------");
            Debug.WriteLine($"Page       : {item.Page}");
            Debug.WriteLine($"Status     : {item.Status}");
            Debug.WriteLine($"Title      : {item.Title}");
            Debug.WriteLine($"Summary    : {item.Summary}");
            Debug.WriteLine($"Period     : {item.Period}");
            Debug.WriteLine($"Thumbnail  : {item.ThumbnailUrl}");
            Debug.WriteLine($"DetailUrl  : {item.DetailUrl ?? "(none)"}");
        }
    }

    /// <summary>
    /// 메인 공지사항 크롤링 결과를 디버그 출력으로 남겨 파서 동작을 점검합니다.
    /// </summary>
    public static async Task DebugWriteHomepageNoticesAsync(
        HttpClient httpClient,
        CancellationToken ct = default)
    {
        IReadOnlyList<GameHomepageNoticeInfo> items = await GetHomepageNoticesAsync(httpClient, ct);

        Debug.WriteLine($"[GameHomepageCrawler] TotalNoticeCount = {items.Count}");

        foreach (GameHomepageNoticeInfo item in items)
        {
            Debug.WriteLine("--------------------------------------------------");
            Debug.WriteLine($"Title      : {item.Title}");
            Debug.WriteLine($"Date       : {item.DateText}");
            Debug.WriteLine($"Url        : {item.Url}");
        }
    }

    /// <summary>
    /// 로딩 옵션에 따라 현재 이벤트를 최종 결과에 포함할지 결정합니다.
    /// </summary>
    private static bool ShouldIncludeEvent(GameEventInfo item, GameEventLoadOptions options)
        => !(options.IgnorePlaceholderEndDate && item.EndDate == PlaceholderEventEndDate);

    /// <summary>
    /// 지정한 페이지 번호의 이벤트 목록 HTML을 내려받습니다.
    /// </summary>
    private static Task<string> GetEventListPageHtmlAsync(
        HttpClient httpClient,
        int page,
        CancellationToken ct)
        => GetHtmlAsync(httpClient, $"https://www.gersang.co.kr/news/event.gs?GSserKey=&page={page}", ct);

    /// <summary>
    /// 메인 홈페이지 HTML을 내려받습니다.
    /// </summary>
    private static Task<string> GetHomepageHtmlAsync(HttpClient httpClient, CancellationToken ct)
        => GetHtmlAsync(httpClient, "https://www.gersang.co.kr", ct);

    /// <summary>
    /// 공통 헤더를 포함해 HTML 문서를 내려받습니다.
    /// </summary>
    private static async Task<string> GetHtmlAsync(HttpClient httpClient, string url, CancellationToken ct)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, url);
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

    /// <summary>
    /// 페이징 링크를 읽어 실제로 순회할 마지막 페이지 번호를 결정합니다.
    /// </summary>
    private static int ParseLastPage(string html)
    {
        HtmlDocument doc = new();
        doc.LoadHtml(html);

        IEnumerable<string> hrefs = doc.DocumentNode
            .Descendants("a")
            .Select(x => x.GetAttributeValue("href", string.Empty))
            .Where(x => !string.IsNullOrWhiteSpace(x));

        int maxPage = 1;

        foreach (string href in hrefs)
        {
            Match match = PageRegex.Match(href);
            if (!match.Success)
                continue;

            if (int.TryParse(match.Groups[1].Value, out int page))
                maxPage = Math.Max(maxPage, page);
        }

        return Math.Min(maxPage, MaxPagesToScan);
    }

    /// <summary>
    /// 메인 홈페이지 HTML에서 공지사항 목록을 추출합니다.
    /// </summary>
    private static IReadOnlyList<GameHomepageNoticeInfo> ParseHomepageNotices(string html)
    {
        HtmlDocument doc = new();
        doc.LoadHtml(html);

        HtmlNode? noticeContainer = doc.DocumentNode.SelectSingleNode(
            "//div[contains(concat(' ', normalize-space(@class), ' '), ' inner_wrap ') and contains(concat(' ', normalize-space(@class), ' '), ' notice ')]");
        if (noticeContainer is null)
            return [];

        List<GameHomepageNoticeInfo> result = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        HtmlNodeCollection? itemNodes = noticeContainer.SelectNodes(".//li");
        if (itemNodes is null)
            return result;

        foreach (HtmlNode itemNode in itemNodes)
        {
            HtmlNode? anchorNode = itemNode.SelectSingleNode("./a") ?? itemNode.SelectSingleNode(".//a[@href]");
            if (anchorNode is null)
                continue;

            string url = ToAbsoluteUrl(anchorNode.GetAttributeValue("href", string.Empty));
            string title = ExtractNoticeTitle(anchorNode);
            string dateText = ExtractNoticeDate(anchorNode);

            if (string.IsNullOrWhiteSpace(url) ||
                string.IsNullOrWhiteSpace(title) ||
                string.IsNullOrWhiteSpace(dateText))
            {
                continue;
            }

            string dedupKey = $"{url}|{title}|{dateText}";
            if (!seen.Add(dedupKey))
                continue;

            result.Add(new GameHomepageNoticeInfo(
                Title: title,
                DateText: dateText,
                Url: url,
                Date: ParseNoticeDate(dateText)));
        }

        return result;
    }

    /// <summary>
    /// 목록 페이지 HTML에서 이벤트 카드들을 찾아 모델 목록으로 변환합니다.
    /// </summary>
    private static List<GameEventInfo> ParseEventsFromListPage(string html, int page)
    {
        HtmlDocument doc = new();
        doc.LoadHtml(html);

        List<HtmlNode> imageNodes = doc.DocumentNode
            .Descendants("img")
            .Where(IsEventThumbnailImage)
            .ToList();

        List<GameEventInfo> result = [];

        foreach (HtmlNode imageNode in imageNodes)
        {
            HtmlNode? cardNode = FindEventCardNode(imageNode);
            if (cardNode is null)
                continue;

            List<string> lines = ExtractMeaningfulLines(cardNode);
            if (lines.Count == 0)
                continue;

            string? status = lines.FirstOrDefault(x => StatusRegex.IsMatch(x));
            string? period = lines.FirstOrDefault(x => PeriodRegex.IsMatch(x));

            if (string.IsNullOrWhiteSpace(status) || string.IsNullOrWhiteSpace(period))
                continue;

            string thumbnailUrl = ToAbsoluteUrl(imageNode.GetAttributeValue("src", string.Empty));

            HtmlNode? titleAnchor = cardNode
                .Descendants("a")
                .FirstOrDefault(x =>
                {
                    string href = x.GetAttributeValue("href", string.Empty);
                    string text = NormalizeText(HtmlEntity.DeEntitize(x.InnerText));

                    return !string.IsNullOrWhiteSpace(href)
                        && href.Contains("/event/", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(text);
                });

            string? detailUrl = null;
            string? title = null;

            if (titleAnchor is not null)
            {
                title = NormalizeText(HtmlEntity.DeEntitize(titleAnchor.InnerText));
                detailUrl = ToAbsoluteUrl(titleAnchor.GetAttributeValue("href", string.Empty));
            }

            title ??= ExtractTitleFromLines(lines, status, period);
            if (string.IsNullOrWhiteSpace(title))
                continue;

            string summary = ExtractSummaryFromLines(lines, status, title, period);
            (DateOnly? startDate, DateOnly? endDate) = ParsePeriodRange(period);

            result.Add(new GameEventInfo(
                Page: page,
                Status: status,
                Title: title,
                Summary: summary,
                Period: period,
                ThumbnailUrl: thumbnailUrl,
                DetailUrl: detailUrl,
                StartDate: startDate,
                EndDate: endDate));
        }

        return result;
    }

    /// <summary>
    /// 이벤트 기간 문자열에서 시작일과 종료일을 추출합니다.
    /// </summary>
    private static (DateOnly? StartDate, DateOnly? EndDate) ParsePeriodRange(string period)
    {
        if (string.IsNullOrWhiteSpace(period))
            return (null, null);

        Match match = PeriodRegex.Match(period);
        if (!match.Success)
            return (null, null);

        bool startParsed = DateOnly.TryParseExact(
            match.Groups["start"].Value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateOnly startDate);
        bool endParsed = DateOnly.TryParseExact(
            match.Groups["end"].Value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateOnly endDate);

        return startParsed && endParsed
            ? (startDate, endDate)
            : (null, null);
    }

    /// <summary>
    /// 대상 이미지가 이벤트 대표 썸네일인지 판별합니다.
    /// </summary>
    private static bool IsEventThumbnailImage(HtmlNode imageNode)
    {
        string src = imageNode.GetAttributeValue("src", string.Empty);
        if (string.IsNullOrWhiteSpace(src))
            return false;

        src = src.Replace('\\', '/');

        return src.Contains("/event/", StringComparison.OrdinalIgnoreCase)
            && src.Contains("/img/", StringComparison.OrdinalIgnoreCase)
            && (src.EndsWith("1920x610.jpg", StringComparison.OrdinalIgnoreCase)
                || src.EndsWith("1920x610.png", StringComparison.OrdinalIgnoreCase)
                || src.EndsWith("1920x610.jpeg", StringComparison.OrdinalIgnoreCase)
                || src.EndsWith("1920x610.webp", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 썸네일 노드에서 상태와 기간을 함께 가진 가장 가까운 이벤트 카드 조상을 찾습니다.
    /// </summary>
    private static HtmlNode? FindEventCardNode(HtmlNode imageNode)
    {
        foreach (HtmlNode ancestor in imageNode.Ancestors())
        {
            List<string> lines = ExtractMeaningfulLines(ancestor);

            bool hasStatus = lines.Any(x => StatusRegex.IsMatch(x));
            bool hasPeriod = lines.Any(x => PeriodRegex.IsMatch(x));

            if (!hasStatus || !hasPeriod)
                continue;

            int descendantAnchorCount = ancestor.Descendants("a").Count();
            if (descendantAnchorCount > 8)
                continue;

            return ancestor;
        }

        return null;
    }

    /// <summary>
    /// 노드 내부 텍스트를 줄 단위로 정리해 의미 있는 내용만 반환합니다.
    /// </summary>
    private static List<string> ExtractMeaningfulLines(HtmlNode node)
    {
        return HtmlEntity.DeEntitize(node.InnerText)
            .Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeText)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    /// <summary>
    /// 공지 앵커에서 제목 텍스트를 추출합니다.
    /// </summary>
    private static string ExtractNoticeTitle(HtmlNode anchorNode)
    {
        HtmlNode? titleNode = anchorNode.SelectSingleNode(".//*[contains(concat(' ', normalize-space(@class), ' '), ' tit ')]")
            ?? anchorNode.SelectSingleNode(".//p")
            ?? anchorNode;

        return NormalizeText(HtmlEntity.DeEntitize(titleNode.InnerText));
    }

    /// <summary>
    /// 공지 앵커에서 날짜 텍스트를 추출합니다.
    /// </summary>
    private static string ExtractNoticeDate(HtmlNode anchorNode)
    {
        HtmlNode? dateNode = anchorNode.SelectSingleNode(".//*[contains(concat(' ', normalize-space(@class), ' '), ' date ')]");
        return dateNode is null
            ? string.Empty
            : NormalizeText(HtmlEntity.DeEntitize(dateNode.InnerText));
    }

    /// <summary>
    /// 카드 텍스트에서 상태와 기간을 제외한 첫 제목 후보를 추출합니다.
    /// </summary>
    private static string ExtractTitleFromLines(
        List<string> lines,
        string status,
        string period)
    {
        foreach (string line in lines)
        {
            if (line == status || line == period)
                continue;

            if (StatusRegex.IsMatch(line) || PeriodRegex.IsMatch(line))
                continue;

            return line;
        }

        return string.Empty;
    }

    /// <summary>
    /// 제목 다음에 오는 첫 본문 줄을 이벤트 요약으로 사용합니다.
    /// </summary>
    private static string ExtractSummaryFromLines(
        List<string> lines,
        string status,
        string title,
        string period)
    {
        bool titlePassed = false;

        foreach (string line in lines)
        {
            if (line == status || line == period)
                continue;

            if (!titlePassed)
            {
                if (line == title)
                    titlePassed = true;

                continue;
            }

            if (line == title)
                continue;

            if (StatusRegex.IsMatch(line) || PeriodRegex.IsMatch(line))
                continue;

            return line;
        }

        return string.Empty;
    }

    /// <summary>
    /// 메인 공지 날짜 문자열을 DateOnly로 변환합니다.
    /// </summary>
    private static DateOnly? ParseNoticeDate(string dateText)
    {
        if (DateOnly.TryParseExact(
            dateText,
            NoticeDateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateOnly date))
        {
            return date;
        }

        return null;
    }

    /// <summary>
    /// HTML에서 읽은 텍스트를 한 줄 표시용으로 정규화합니다.
    /// </summary>
    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string decoded = WebUtility.HtmlDecode(text);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }

    /// <summary>
    /// 상대 또는 절대 URL을 거상 도메인 기준 절대 URL로 변환합니다.
    /// </summary>
    private static string ToAbsoluteUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? absolute))
            return absolute.ToString();

        return new Uri(BaseUri, url).ToString();
    }
}
