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
/// 앱에 포함된 WinUI 링크 매니페스트를 기본값으로 사용하고, 가능하면 GitHub raw 매니페스트로 덮어씁니다.
/// </summary>
public sealed class LinkManager
{
    private const string ManifestUrl =
        "https://raw.githubusercontent.com/byungmeo/GersangStation/master/GersangStation.WinUI/metadata/winui-links-manifest.json";
    private const string LocalManifestRelativePath = "metadata/winui-links-manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly HttpClient Http = CreateHttpClient();

    private readonly object _syncRoot = new();
    private IReadOnlyDictionary<string, LinkManifestItem> _embeddedLinks =
        new Dictionary<string, LinkManifestItem>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, LinkManifestItem> _remoteLinks =
        new Dictionary<string, LinkManifestItem>(StringComparer.OrdinalIgnoreCase);
    private LinkFallbackOptions _embeddedFallback = new();
    private LinkFallbackOptions _remoteFallback = new();
    private bool _localManifestLoaded;
    private bool _remoteManifestLoaded;
    private Exception? _remoteLoadException;

    /// <summary>
    /// 마지막 매니페스트 로드 실패 예외를 반환합니다.
    /// </summary>
    public Exception? LoadException => _remoteLoadException;

    /// <summary>
    /// 앱 시작 시 앱 내장 매니페스트를 먼저 확보하고, 가능하면 GitHub 매니페스트로 덮어씁니다.
    /// </summary>
    public void Initialize()
    {
        lock (_syncRoot)
        {
            EnsureLocalManifestLoaded();
            EnsureRemoteManifestLoaded();
        }
    }

    /// <summary>
    /// 지정한 key에 매핑된 URL 문자열을 반환합니다. GitHub 값이 우선이고, 없으면 앱 내장 매니페스트를 확인합니다.
    /// </summary>
    public bool TryGetUrl(string key, out string url)
    {
        EnsureAvailableManifests();

        LinkManifestItem? item = GetLinkItem(key);
        if (item is not null
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
    public LinkNavigationTarget ResolveNavigation(string key)
    {
        EnsureAvailableManifests();

        if (TryGetUrl(key, out string url))
            return LinkNavigationTarget.ForUri(new Uri(url, UriKind.Absolute));

        return LinkNavigationTarget.ForHtml(BuildFallbackHtml(key));
    }

    /// <summary>
    /// 앱 번들에 포함된 기본 매니페스트를 읽어옵니다.
    /// </summary>
    private void EnsureLocalManifestLoaded()
    {
        if (_localManifestLoaded)
            return;

        string localManifestPath = Path.Combine(AppContext.BaseDirectory, LocalManifestRelativePath);

        try
        {
            if (!File.Exists(localManifestPath))
            {
                Debug.WriteLine($"[LinkManager] Embedded manifest not found at '{localManifestPath}'.");
                _embeddedLinks = new Dictionary<string, LinkManifestItem>(StringComparer.OrdinalIgnoreCase);
                _embeddedFallback = new LinkFallbackOptions();
                _localManifestLoaded = true;
                return;
            }

            WinUiLinksManifestDocument? manifest = DeserializeManifest(File.ReadAllText(localManifestPath));
            if (manifest?.Links is null)
            {
                Debug.WriteLine("[LinkManager] Embedded manifest links section is missing.");
                _embeddedLinks = new Dictionary<string, LinkManifestItem>(StringComparer.OrdinalIgnoreCase);
                _embeddedFallback = manifest?.Fallback ?? new LinkFallbackOptions();
                _localManifestLoaded = true;
                return;
            }

            _embeddedLinks = new Dictionary<string, LinkManifestItem>(manifest.Links, StringComparer.OrdinalIgnoreCase);
            _embeddedFallback = manifest.Fallback ?? new LinkFallbackOptions();
            _localManifestLoaded = true;
            Debug.WriteLine($"[LinkManager] Embedded manifest loaded. Count={_embeddedLinks.Count}");
        }
        catch (Exception ex)
        {
            _embeddedLinks = new Dictionary<string, LinkManifestItem>(StringComparer.OrdinalIgnoreCase);
            _embeddedFallback = new LinkFallbackOptions();
            _localManifestLoaded = true;
            Debug.WriteLine($"[LinkManager] Failed to load embedded manifest: {ex}");
        }
    }

    /// <summary>
    /// GitHub raw 매니페스트를 한 번만 읽고, 성공 시 로컬 기본값 위를 덮습니다.
    /// 실패는 로그만 남기고 앱은 로컬 기본값으로 계속 동작합니다.
    /// </summary>
    private void EnsureRemoteManifestLoaded()
    {
        if (_remoteManifestLoaded)
            return;

        try
        {
            string json = DownloadManifestJson();
            WinUiLinksManifestDocument? manifest = DeserializeManifest(json);

            if (manifest?.Links is null)
            {
                _remoteLinks = new Dictionary<string, LinkManifestItem>(StringComparer.OrdinalIgnoreCase);
                _remoteFallback = manifest?.Fallback ?? new LinkFallbackOptions();
                _remoteLoadException = new InvalidDataException("WinUI 링크 매니페스트의 links 섹션을 읽지 못했습니다.");
                Debug.WriteLine("[LinkManager] Remote manifest links section is missing. Embedded manifest will be used as fallback.");
                _remoteManifestLoaded = true;
                return;
            }

            _remoteLinks = new Dictionary<string, LinkManifestItem>(manifest.Links, StringComparer.OrdinalIgnoreCase);
            _remoteFallback = manifest.Fallback ?? new LinkFallbackOptions();
            _remoteLoadException = null;
            _remoteManifestLoaded = true;
            Debug.WriteLine($"[LinkManager] Remote manifest loaded. Count={_remoteLinks.Count}");
        }
        catch (Exception ex)
        {
            _remoteLinks = new Dictionary<string, LinkManifestItem>(StringComparer.OrdinalIgnoreCase);
            _remoteFallback = new LinkFallbackOptions();
            _remoteLoadException = ex;
            _remoteManifestLoaded = true;
            Debug.WriteLine($"[LinkManager] Failed to load remote manifest. Embedded manifest will be used. {ex}");
        }
    }

    /// <summary>
    /// 로컬/원격 매니페스트를 사용할 준비를 보장합니다.
    /// </summary>
    private void EnsureAvailableManifests()
    {
        if (_localManifestLoaded && _remoteManifestLoaded)
            return;

        lock (_syncRoot)
        {
            EnsureLocalManifestLoaded();
            EnsureRemoteManifestLoaded();
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

    private static WinUiLinksManifestDocument? DeserializeManifest(string json)
        => JsonSerializer.Deserialize<WinUiLinksManifestDocument>(json, JsonOptions);

    /// <summary>
    /// 원격 매니페스트를 우선하고, 없으면 앱 내장 매니페스트에서 링크를 찾습니다.
    /// </summary>
    private LinkManifestItem? GetLinkItem(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        if (_remoteLinks.TryGetValue(key, out LinkManifestItem? remoteItem))
            return remoteItem;

        if (_embeddedLinks.TryGetValue(key, out LinkManifestItem? embeddedItem))
            return embeddedItem;

        return null;
    }

    private string BuildFallbackHtml(string key)
    {
        LinkFallbackOptions fallback = GetFallbackOptions();
        string title = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fallback.Title)
            ? "링크를 불러오는데 실패하였습니다."
            : fallback.Title);
        string message = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fallback.Message)
            ? "요청한 링크 정보를 준비하지 못했습니다."
            : fallback.Message);
        string supportText = WebUtility.HtmlEncode(fallback.SupportText ?? string.Empty);
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

    private LinkFallbackOptions GetFallbackOptions()
    {
        if (_remoteManifestLoaded && _remoteLoadException is null)
            return _remoteFallback;

        if (_localManifestLoaded)
            return _embeddedFallback;

        return new LinkFallbackOptions();
    }

    private string GetFailureDetail(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "비어 있는 링크 키가 전달되었습니다.";

        if (_remoteLoadException is not null && _embeddedLinks.Count == 0)
            return $"원격/내장 매니페스트를 모두 사용할 수 없습니다. 원격 로드 실패: {_remoteLoadException.Message}";

        if (_remoteLoadException is not null)
            return $"원격 매니페스트 로드에 실패했고, 앱 내장 매니페스트에도 '{key}' 키가 없습니다. 원격 로드 실패: {_remoteLoadException.Message}";

        if (_embeddedLinks.ContainsKey(key) || _remoteLinks.ContainsKey(key))
            return "링크 URL 형식이 올바르지 않습니다.";

        return "요청한 링크 키를 원격/앱 내장 매니페스트 모두에서 찾지 못했습니다.";
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
public sealed record LinkNavigationTarget(Uri? Uri, string? HtmlContent)
{
    public static LinkNavigationTarget ForUri(Uri uri)
        => new(uri, null);

    public static LinkNavigationTarget ForHtml(string htmlContent)
        => new(null, htmlContent);
}
