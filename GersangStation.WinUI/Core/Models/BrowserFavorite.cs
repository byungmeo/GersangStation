using System.Text.Json.Serialization;

namespace Core.Models;

/// <summary>
/// 브라우저 페이지 즐겨찾기 항목을 나타냅니다.
/// </summary>
public sealed class BrowserFavorite(string name = "", string url = "", string faviconUrl = "")
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = name;

    [JsonPropertyName("Url")]
    public string Url { get; set; } = url;

    [JsonPropertyName("FaviconUrl")]
    public string FaviconUrl { get; set; } = faviconUrl;

    [JsonIgnore]
    public string DisplayFaviconUrl => string.IsNullOrWhiteSpace(FaviconUrl) ? BuildDefaultFaviconUrl(Url) : FaviconUrl;

    public static string BuildDefaultFaviconUrl(string? siteUrl)
    {
        if (!System.Uri.TryCreate(siteUrl, System.UriKind.Absolute, out System.Uri? targetUri))
            return string.Empty;

        return $"{targetUri.Scheme}://{targetUri.Authority}/favicon.ico";
    }
}
