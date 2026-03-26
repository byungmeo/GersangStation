using Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace GersangStation.Services;

/// <summary>
/// GitHub raw의 앱 버전 매니페스트를 읽고 현재 버전 기준 수동 업데이트 필요 여부를 계산합니다.
/// </summary>
public sealed class StoreUpdateFallbackManifestService
{
    private const string ManifestUrl =
        "https://raw.githubusercontent.com/byungmeo/GersangStation/master/GersangStation.WinUI/metadata/winui-store-update-version-manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly HttpClient Http = CreateHttpClient();

    /// <summary>
    /// 원격 버전 매니페스트를 매번 새로 읽어 현재 버전 이후 필수 업데이트 존재 여부를 계산합니다.
    /// </summary>
    public async Task<AppUpdateRequirementEvaluation> EvaluateRequiredManualUpdateAsync()
    {
        string json = await DownloadManifestJsonAsync();
        StoreUpdateFallbackManifestDocument? manifest = JsonSerializer.Deserialize<StoreUpdateFallbackManifestDocument>(json, JsonOptions);

        if (manifest?.Versions is null)
            throw new InvalidDataException("수동 업데이트 버전 매니페스트의 versions 섹션을 읽지 못했습니다.");

        AppUpdateRequirementEvaluation evaluation = AppUpdateVersionManifestEvaluator.EvaluateRequiredManualUpdate(
            manifest.Versions,
            Package.Current.Id.Version);

        if (evaluation.InvalidEntries.Count > 0)
        {
            Debug.WriteLine(
                $"[StoreUpdateFallbackManifestService] Invalid version entries detected: {string.Join(", ", evaluation.InvalidEntries)}");
        }

        return evaluation;
    }

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

        using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
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

    private sealed class StoreUpdateFallbackManifestDocument
    {
        [JsonPropertyName("versions")]
        public string[]? Versions { get; init; }
    }
}
