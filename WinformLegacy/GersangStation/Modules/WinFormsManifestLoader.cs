using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GersangStation.Modules;

internal static class WinFormsManifestLoader {
    private static readonly HttpClient httpClient = new HttpClient();
    private static readonly JsonSerializerOptions serializerOptions = new JsonSerializerOptions() {
        PropertyNameCaseInsensitive = true
    };

    public static Task<WinFormsReleaseManifest?> LoadReleaseAsync(string url, CancellationToken cancellationToken = default) {
        return LoadAsync<WinFormsReleaseManifest>(url, cancellationToken);
    }

    public static Task<WinFormsAnnouncementManifest?> LoadAnnouncementsAsync(string url, CancellationToken cancellationToken = default) {
        return LoadAsync<WinFormsAnnouncementManifest>(url, cancellationToken);
    }

    public static Task<WinFormsSponsorsManifest?> LoadSponsorsAsync(string url, CancellationToken cancellationToken = default) {
        return LoadAsync<WinFormsSponsorsManifest>(url, cancellationToken);
    }

    private static async Task<TManifest?> LoadAsync<TManifest>(string url, CancellationToken cancellationToken) where TManifest : WinFormsManifestDocument {
        using HttpResponseMessage response = await httpClient.GetAsync(url, cancellationToken);
        Trace.WriteLine($"Manifest HTTP 응답: {(int)response.StatusCode} {response.ReasonPhrase} ({typeof(TManifest).Name})");
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        TManifest? manifest = await JsonSerializer.DeserializeAsync<TManifest>(stream, serializerOptions, cancellationToken);
        Trace.WriteLine($"Manifest JSON 역직렬화 결과: {(manifest == null ? "null" : "success")} ({typeof(TManifest).Name})");
        return manifest;
    }
}

internal abstract class WinFormsManifestDocument {
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("product")]
    public string Product { get; set; } = string.Empty;

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    [JsonPropertyName("generated_at")]
    public string GeneratedAt { get; set; } = string.Empty;
}

internal sealed class WinFormsReleaseManifest : WinFormsManifestDocument {
    [JsonPropertyName("release")]
    public WinFormsManifestRelease? Release { get; set; }
}

internal sealed class WinFormsAnnouncementManifest : WinFormsManifestDocument {
    [JsonPropertyName("announcement")]
    public WinFormsManifestAnnouncement? Announcement { get; set; }
}

internal sealed class WinFormsSponsorsManifest : WinFormsManifestDocument {
    [JsonPropertyName("sponsors")]
    public WinFormsManifestSponsors? Sponsors { get; set; }
}

internal sealed class WinFormsManifestRelease {
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("compatibility_tag")]
    public string CompatibilityTag { get; set; } = string.Empty;

    [JsonPropertyName("published_at")]
    public string PublishedAt { get; set; } = string.Empty;

    [JsonPropertyName("is_mandatory")]
    public bool IsMandatory { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("notes_url")]
    public string NotesUrl { get; set; } = string.Empty;

    [JsonPropertyName("download")]
    public WinFormsManifestDownload? Download { get; set; }
}

internal sealed class WinFormsManifestDownload {
    [JsonPropertyName("asset_name")]
    public string AssetName { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

internal sealed class WinFormsManifestAnnouncement {
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("published_at")]
    public string PublishedAt { get; set; } = string.Empty;

    [JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; set; }

    [JsonPropertyName("show_popup")]
    public bool ShowPopup { get; set; }

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;
}

internal sealed class WinFormsManifestSponsors {
    [JsonPropertyName("last_updated_at")]
    public string LastUpdatedAt { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<JsonElement>? Items { get; set; }
}
