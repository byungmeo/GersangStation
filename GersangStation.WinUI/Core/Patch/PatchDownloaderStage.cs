using Core.Models;
using System.Diagnostics;
using System.Net;

namespace Core.Patch;

public static class PatchDownloaderStage
{
    /// <summary>
    /// ExtractPlan에 포함된 파일들을 TempRoot/{version}/ 아래로 다운로드.
    /// </summary>
    public static async Task DownloadAllAsync(
        PatchExtractPlan plan,
        string tempRoot,
        GameServer server,                 // 예: https://.../Gersang/Patch/Gersang_Server
        int maxConcurrency,
        Action<int, int>? onFileDownloaded,
        CancellationToken ct)
    {
        if (plan is null) throw new ArgumentNullException(nameof(plan));
        if (string.IsNullOrWhiteSpace(tempRoot)) throw new ArgumentException("tempRoot is required.", nameof(tempRoot));
        if (maxConcurrency < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrency));

        Debug.WriteLine($"[PATCH][DOWNLOAD][BEGIN] tempRoot='{tempRoot}', maxConcurrency={maxConcurrency}");

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
        int totalFileCount = plan.ByVersion.Values.Sum(files => files.Count);
        int completedFileCount = 0;
        Debug.WriteLine($"[PATCH][DOWNLOAD][PLAN] versionCount={plan.ByVersion.Count}, totalFileCount={totalFileCount}");

        foreach (var kv in plan.ByVersion) // SortedDictionary => 오름차순
        {
            ct.ThrowIfCancellationRequested();

            int version = kv.Key;
            var files = kv.Value;
            Debug.WriteLine($"[PATCH][DOWNLOAD][VERSION][BEGIN] version={version}, fileCount={files.Count}");

            string versionDir = Path.Combine(tempRoot, version.ToString());
            Directory.CreateDirectory(versionDir);

            foreach (var f in files)
            {
                ct.ThrowIfCancellationRequested();

                // URL: .../Client_Patch_File/{경로}/{파일명}
                // patchBaseUri = .../Gersang/Patch/Gersang_Server 라고 두면:
                // => {patchBaseUri}/Client_Patch_File/...
                var url = new Uri(GameServerHelper.GetPatchFileUrl(server, f.RelativeDir + f.CompressedFileName));

                string dest = Path.Combine(versionDir, f.CompressedFileName);
                string temp = dest + ".crdownload";

                if (File.Exists(dest)) File.Delete(dest);
                if (File.Exists(temp)) File.Delete(temp);

                var options = new DownloadOptions(
                    TempPath: temp,
                    Overwrite: true);
                Debug.WriteLine($"[PATCH][DOWNLOAD][ENQUEUE] version={version}, url='{url}', dest='{dest}', temp='{temp}'");

                int completionNotified = 0;
                IProgress<DownloadProgress>? progress = onFileDownloaded is null
                    ? null
                    : new Progress<DownloadProgress>(p =>
                    {
                        long totalBytes = p.TotalBytes ?? 0;
                        if (totalBytes <= 0 || p.BytesReceived < totalBytes)
                            return;

                        if (Interlocked.CompareExchange(ref completionNotified, 1, 0) != 0)
                            return;

                        int completed = Interlocked.Increment(ref completedFileCount);
                        Debug.WriteLine($"[PATCH][DOWNLOAD][FILE][DONE] version={version}, file='{f.CompressedFileName}', completed={completed}/{totalFileCount}");
                        onFileDownloaded(completed, totalFileCount);
                    });

                tasks.Add(manager.EnqueueAsync(url, dest, options, progress: progress, ct));
            }
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        Debug.WriteLine("[PATCH][DOWNLOAD][END] all downloads completed");
    }
}
