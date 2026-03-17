using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GersangStation.Services;

/// <summary>
/// GitHub raw의 WinUI 링크 매니페스트를 한 번만 읽어와 key-value 형태로 제공합니다.
/// </summary>
public sealed class LinkManager
{
    private const string ManifestUrl =
        "https://raw.githubusercontent.com/byungmeo/GersangStation/master/GersangStation.WinUI/metadata/winui-links-manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly HttpClient Http = CreateHttpClient();

    private readonly object _syncRoot = new();
    private IReadOnlyDictionary<string, LinkManifestItem> _links =
        new Dictionary<string, LinkManifestItem>(StringComparer.OrdinalIgnoreCase);
    private LinkFallbackOptions _fallback = new();
    private bool _initialized;
    private Exception? _loadException;

    /// <summary>
    /// 마지막 매니페스트 로드 실패 예외를 반환합니다.
    /// </summary>
    public Exception? LoadException => _loadException;

    /// <summary>
    /// 앱 시작 시 한 번만 GitHub에서 매니페스트를 읽어 캐시합니다.
    /// </summary>
    public void Initialize()
    {
        if (_initialized)
            return;

        lock (_syncRoot)
        {
            if (_initialized)
                return;

            LoadManifestCore();
            _initialized = true;
        }
    }

    /// <summary>
    /// 지정한 key에 매핑된 URL 문자열을 반환합니다.
    /// </summary>
    public bool TryGetUrl(string key, out string url)
    {
        Initialize();

        if (!string.IsNullOrWhiteSpace(key)
            && _links.TryGetValue(key, out LinkManifestItem? item)
            && !string.IsNullOrWhiteSpace(item.Url)
            && Uri.TryCreate(item.Url, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            url = uri.AbsoluteUri;
            return true;
        }

        url = string.Empty;
        return false;
    }

    /// <summary>
    /// 지정한 링크 key를 WebView 네비게이션 대상으로 해석합니다.
    /// </summary>
    public WinUiLinkNavigationTarget ResolveNavigation(string key)
    {
        if (TryGetUrl(key, out string url))
            return WinUiLinkNavigationTarget.ForUri(new Uri(url, UriKind.Absolute));

        return WinUiLinkNavigationTarget.ForHtml(BuildFallbackHtml(key));
    }

    /// <summary>
    /// GitHub raw에서 매니페스트 JSON을 받아와 메모리에 적재합니다.
    /// </summary>
    private void LoadManifestCore()
    {
        try
        {
            string json = DownloadManifestJson();
            WinUiLinksManifestDocument? manifest = JsonSerializer.Deserialize<WinUiLinksManifestDocument>(json, JsonOptions);

            if (manifest?.Links is null)
            {
                _links = new Dictionary<string, LinkManifestItem>(StringComparer.OrdinalIgnoreCase);
                _fallback = manifest?.Fallback ?? new LinkFallbackOptions();
                _loadException = new InvalidDataException("WinUI 링크 매니페스트의 links 섹션을 읽지 못했습니다.");
                Debug.WriteLine("[LinkManager] Manifest links section is missing.");
                return;
            }

            _fallback = manifest.Fallback ?? new LinkFallbackOptions();
            _links = new Dictionary<string, LinkManifestItem>(manifest.Links, StringComparer.OrdinalIgnoreCase);
            _loadException = null;

            Debug.WriteLine($"[LinkManager] Manifest loaded from GitHub. Count={_links.Count}");
        }
        catch (Exception ex)
        {
            _links = new Dictionary<string, LinkManifestItem>(StringComparer.OrdinalIgnoreCase);
            _fallback = new LinkFallbackOptions();
            _loadException = ex;
            Debug.WriteLine($"[LinkManager] Failed to load manifest from GitHub: {ex}");
        }
    }

    /// <summary>
    /// GitHub raw URL에서 매니페스트 JSON 문자열을 내려받습니다.
    /// </summary>
    private static string DownloadManifestJson()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, ManifestUrl);

        // 중간 캐시/로컬 캐시 영향을 줄이기 위해 캐시 무효화 헤더를 명시합니다.
        request.Headers.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true,
            MaxAge = TimeSpan.Zero
        };
        request.Headers.Pragma.Add(new NameValueHeaderValue("no-cache"));
        request.Headers.UserAgent.ParseAdd("GersangStation/1.0");

        using HttpResponseMessage response = Http
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        response.EnsureSuccessStatusCode();

        return response.Content
            .ReadAsStringAsync()
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    private string BuildFallbackHtml(string key)
    {
        string title = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(_fallback.Title)
            ? "링크를 불러오는데 실패하였습니다."
            : _fallback.Title);
        string message = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(_fallback.Message)
            ? "요청한 링크 정보를 준비하지 못했습니다."
            : _fallback.Message);
        string supportText = WebUtility.HtmlEncode(_fallback.SupportText ?? string.Empty);
        string encodedKey = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(key) ? "(empty)" : key);
        string encodedDetail = WebUtility.HtmlEncode(GetFailureDetail(key));

        return $$"""
<!DOCTYPE html>
<html lang="ko">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{title}}</title>
  <style>
    :root {
      color-scheme: light dark;
      font-family: "Segoe UI", "Malgun Gothic", sans-serif;
    }
    body {
      margin: 0;
      background: linear-gradient(180deg, #f7efe3 0%, #efe5d2 100%);
      color: #1e1a14;
    }
    main {
      max-width: 760px;
      margin: 0 auto;
      padding: 48px 24px 72px;
    }
    article {
      padding: 28px;
      border-radius: 20px;
      background: rgba(255, 255, 255, 0.86);
      box-shadow: 0 16px 40px rgba(92, 66, 35, 0.12);
    }
    h1 {
      margin: 0 0 12px;
      font-size: 30px;
      line-height: 1.25;
    }
    p {
      margin: 0 0 12px;
      line-height: 1.7;
    }
    dl {
      margin: 20px 0 0;
      display: grid;
      grid-template-columns: 120px 1fr;
      gap: 10px 16px;
    }
    dt {
      font-weight: 600;
    }
    dd {
      margin: 0;
      word-break: break-all;
    }
    @media (prefers-color-scheme: dark) {
      body {
        background: linear-gradient(180deg, #1e1813 0%, #12100d 100%);
        color: #f5eee5;
      }
      article {
        background: rgba(36, 31, 26, 0.94);
        box-shadow: none;
      }
    }
  </style>
</head>
<body>
  <main>
    <article>
      <h1>{{title}}</h1>
      <p>{{message}}</p>
      <p>{{supportText}}</p>
      <dl>
        <dt>요청 키</dt>
        <dd>{{encodedKey}}</dd>
        <dt>상세</dt>
        <dd>{{encodedDetail}}</dd>
      </dl>
    </article>
  </main>
</body>
</html>
""";
    }

    private string GetFailureDetail(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "비어 있는 링크 키가 전달되었습니다.";

        if (_loadException is not null)
            return $"매니페스트 로드 실패: {_loadException.Message}";

        return _links.ContainsKey(key)
            ? "링크 URL 형식이 올바르지 않습니다."
            : "요청한 링크 키를 매니페스트에서 찾지 못했습니다.";
    }

    private sealed class WinUiLinksManifestDocument
    {
        [JsonPropertyName("fallback")]
        public LinkFallbackOptions? Fallback { get; init; }

        [JsonPropertyName("links")]
        public Dictionary<string, LinkManifestItem>? Links { get; init; }
    }

    private sealed class LinkManifestItem
    {
        [JsonPropertyName("url")]
        public string Url { get; init; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;
    }

    private sealed class LinkFallbackOptions
    {
        [JsonPropertyName("title")]
        public string Title { get; init; } = "링크를 불러오는데 실패하였습니다.";

        [JsonPropertyName("message")]
        public string Message { get; init; } = "요청한 링크 정보를 준비하지 못했습니다.";

        [JsonPropertyName("supportText")]
        public string SupportText { get; init; } = "잠시 후 다시 시도해 주세요.";
    }
}

/// <summary>
/// 링크 key 해석 결과를 WebView URI 또는 HTML 문서로 전달합니다.
/// </summary>
public sealed record WinUiLinkNavigationTarget(Uri? Uri, string? HtmlContent)
{
    public static WinUiLinkNavigationTarget ForUri(Uri uri)
        => new(uri, null);

    public static WinUiLinkNavigationTarget ForHtml(string htmlContent)
        => new(null, htmlContent);
}