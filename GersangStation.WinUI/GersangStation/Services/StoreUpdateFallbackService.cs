using Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace GersangStation.Services;

/// <summary>
/// 스토어 API가 업데이트 없음을 반환했을 때 GitHub update manifest를 보조 판정에 사용합니다.
/// </summary>
public sealed class StoreUpdateFallbackService
{
    private const string ManifestUrl =
        "https://raw.githubusercontent.com/byungmeo/GersangStation/master/GersangStation.WinUI/metadata/store-update-manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly HttpClient Http = CreateHttpClient();

    /// <summary>
    /// 현재 버전보다 높은 항목들 중 필수 업데이트가 하나라도 있으면 true를 반환합니다.
    /// 원격 manifest 로드 실패는 예외를 다시 던지지 않고 false로 무시합니다.
    /// </summary>
    public async Task<StoreUpdateFallbackCheckResult> CheckRequiredUpdateAsync(PackageVersion currentVersion)
    {
        try
        {
            string json = await DownloadManifestJsonAsync().ConfigureAwait(false);
            StoreUpdateManifestDocument? manifest = DeserializeManifest(json);
            if (manifest?.Versions is null || manifest.Versions.Count == 0)
                return StoreUpdateFallbackCheckResult.NotRequired();

            foreach (StoreUpdateManifestEntry entry in manifest.Versions)
            {
                if (entry.Version is null)
                    continue;

                PackageVersion manifestVersion = entry.Version.ToPackageVersion();
                if (!PackageVersionComparer.IsNewerMajorMinorBuild(manifestVersion, currentVersion))
                    continue;

                if (entry.Required)
                    return StoreUpdateFallbackCheckResult.Required(manifestVersion);
            }

            return StoreUpdateFallbackCheckResult.NotRequired();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StoreUpdateFallbackService] Failed to load remote manifest. Ignored. {ex}");
            return StoreUpdateFallbackCheckResult.LoadFailed(ex);
        }
    }

    /// <summary>
    /// GitHub raw URL에서 update manifest를 내려받습니다.
    /// </summary>
    private static async Task<string> DownloadManifestJsonAsync()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, ManifestUrl);

        request.Headers.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true,
            MaxAge = TimeSpan.Zero
        };
        request.Headers.Pragma.Add(new NameValueHeaderValue("no-cache"));
        request.Headers.UserAgent.ParseAdd("GersangStation/1.0");

        using HttpResponseMessage response = await Http
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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

    private static StoreUpdateManifestDocument? DeserializeManifest(string json)
        => JsonSerializer.Deserialize<StoreUpdateManifestDocument>(json, JsonOptions);

    /// <summary>
    /// GitHub manifest 판정 결과를 호출자에게 전달합니다.
    /// </summary>
    public sealed record StoreUpdateFallbackCheckResult(bool HasRequiredUpdate, PackageVersion? RequiredVersion, Exception? LoadException)
    {
        public static StoreUpdateFallbackCheckResult NotRequired()
            => new(false, null, null);

        public static StoreUpdateFallbackCheckResult Required(PackageVersion requiredVersion)
            => new(true, requiredVersion, null);

        public static StoreUpdateFallbackCheckResult LoadFailed(Exception exception)
            => new(false, null, exception);
    }

    private sealed class StoreUpdateManifestDocument
    {
        [JsonPropertyName("versions")]
        public List<StoreUpdateManifestEntry>? Versions { get; init; }
    }

    private sealed class StoreUpdateManifestEntry
    {
        [JsonPropertyName("version")]
        public StoreUpdateManifestVersion? Version { get; init; }

        [JsonPropertyName("required")]
        public bool Required { get; init; }
    }

    private sealed class StoreUpdateManifestVersion
    {
        [JsonPropertyName("major")]
        public ushort Major { get; init; }

        [JsonPropertyName("minor")]
        public ushort Minor { get; init; }

        [JsonPropertyName("build")]
        public ushort Build { get; init; }

        public PackageVersion ToPackageVersion()
            => new(Major, Minor, Build, 0);
    }
}
