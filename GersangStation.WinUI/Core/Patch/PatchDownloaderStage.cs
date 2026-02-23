using SevenZipExtractor;
using System.Net;

namespace Core.Patch;

public static class PatchDownloaderStage
{
    /// <summary>
    /// ExtractPlan에 포함된 파일들을 TempRoot/{version}/ 아래로 다운로드.
    /// 작업이 취소되면 TempRoot 전체 삭제(best-effort).
    /// </summary>
    public static async Task DownloadAllAsync(
        PatchExtractPlan plan,
        string tempRoot,
        Uri patchBaseUri,                 // 예: https://.../Gersang/Patch/Gersang_Server
        int maxConcurrency,
        CancellationToken ct)
    {
        if (plan is null) throw new ArgumentNullException(nameof(plan));
        if (string.IsNullOrWhiteSpace(tempRoot)) throw new ArgumentException("tempRoot is required.", nameof(tempRoot));
        if (patchBaseUri is null) throw new ArgumentNullException(nameof(patchBaseUri));
        if (maxConcurrency < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrency));

        Directory.CreateDirectory(tempRoot);

        // 너가 쓰던 HttpClient 설정 그대로
        using var http = new HttpClient(new HttpClientHandler
        {
            MaxConnectionsPerServer = Math.Max(16, maxConcurrency),
            AutomaticDecompression = DecompressionMethods.None
        })
        {
            Timeout = TimeSpan.FromMinutes(30)
        };

        var downloader = new Downloader(http);
        await using var manager = new DownloadManager(downloader, maxConcurrency);

        // enqueue tasks 모아서 await
        var tasks = new List<Task>();

        try
        {
            foreach (var kv in plan.ByVersion) // SortedDictionary => 오름차순
            {
                ct.ThrowIfCancellationRequested();

                int version = kv.Key;
                var files = kv.Value;

                string versionDir = Path.Combine(tempRoot, version.ToString());
                Directory.CreateDirectory(versionDir);

                foreach (var f in files)
                {
                    ct.ThrowIfCancellationRequested();

                    // URL: .../Client_Patch_File/{경로}/{파일명}
                    // patchBaseUri = .../Gersang/Patch/Gersang_Server 라고 두면:
                    // => {patchBaseUri}/Client_Patch_File/...
                    var url = new Uri(
                        patchBaseUri,
                        $"Client_Patch_File/{TrimSlashes(f.RelativeDir)}/{f.CompressedFileName}");

                    string dest = Path.Combine(versionDir, f.CompressedFileName);
                    string temp = dest + ".crdownload";

                    if (File.Exists(dest)) File.Delete(dest);
                    if (File.Exists(temp)) File.Delete(temp);

                    var options = new DownloadOptions(
                        TempPath: temp,
                        Overwrite: true);

                    tasks.Add(manager.EnqueueAsync(url, dest, options, progress: null, ct));
                }
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // “작업 취소 시 임시 폴더 삭제” 요구 반영
            TryDeleteDirectory(tempRoot);
            throw;
        }
    }

    private static string TrimSlashes(string s)
        => s.Trim().TrimStart('/').TrimEnd('/');

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }
    private static Uri BuildPatchUrl(Uri patchBaseUri, string relativeDir, string compressedFileName)
    {
        // relativeDir: "\Online\Sub\" -> "Online/Sub/"
        string rel = relativeDir.Replace('\\', '/').Trim('/');
        string baseStr = patchBaseUri.ToString();
        if (!baseStr.EndsWith("/")) baseStr += "/";

        // rel이 빈 문자열이면(루트 "\") "Client_Patch_File/" 바로 아래에 붙음
        if (rel.Length == 0)
            return new Uri(baseStr + "Client_Patch_File/" + compressedFileName);

        return new Uri(baseStr + "Client_Patch_File/" + rel + "/" + compressedFileName);
    }
}