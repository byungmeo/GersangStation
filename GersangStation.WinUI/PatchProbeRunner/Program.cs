using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

internal static class Program
{
    // 큰 파일 하나로 고정 (원하는 파일로 바꿔도 됨)
    private static readonly Uri TestUrl =
        new("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/_patchdata_33807.gsz");

    private static async Task Main(string[] args)
    {
        // args 없으면 1/2/4/8 순회
        int[] ks = args.Length == 0 ? new[] { 1, 2, 4, 8 } : args.Select(int.Parse).ToArray();

        string outDir = Path.Combine(Path.GetTempPath(), "GersangRangeProbe");
        Directory.CreateDirectory(outDir);

        using var http = new HttpClient(new HttpClientHandler
        {
            MaxConnectionsPerServer = 32,               // 실험 중 병렬 커넥션이 막히지 않게
            AutomaticDecompression = DecompressionMethods.None
        })
        {
            Timeout = TimeSpan.FromMinutes(30)
        };

        // 메타
        var meta = await HeadAsync(http, TestUrl);
        if (meta.TotalBytes <= 0)
        {
            Console.WriteLine("Content-Length missing. Abort.");
            return;
        }

        Console.WriteLine("==================================================");
        Console.WriteLine($"URL: {TestUrl}");
        Console.WriteLine($"TotalBytes: {meta.TotalBytes:N0}");
        Console.WriteLine($"Accept-Ranges: {meta.AcceptRanges}");
        Console.WriteLine($"ETag: {meta.ETag ?? "(none)"}");
        Console.WriteLine($"Last-Modified: {meta.LastModified ?? "(none)"}");
        Console.WriteLine("==================================================");

        foreach (int k in ks)
        {
            await RunRangeProbeAsync(http, TestUrl, outDir, meta, k);
        }
    }

    private static async Task RunRangeProbeAsync(HttpClient http, Uri url, string outDir, HeadMeta meta, int k)
    {
        k = Math.Max(1, k);

        string fileName = Path.GetFileName(url.LocalPath);
        string baseName = $"RangeProbe_K{k}_{fileName}";
        string finalPath = Path.Combine(outDir, baseName);
        string partDir = Path.Combine(outDir, baseName + ".parts");

        if (File.Exists(finalPath)) File.Delete(finalPath);
        if (Directory.Exists(partDir)) Directory.Delete(partDir, recursive: true);
        Directory.CreateDirectory(partDir);

        long total = meta.TotalBytes;

        // 균등 분할 (마지막 chunk는 remainder 포함)
        (long start, long end, string path)[] parts = BuildParts(partDir, total, k);

        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"Range probe: K={k}");
        Console.WriteLine($"Parts: {parts.Length}");
        Console.WriteLine($"Output: {finalPath}");

        var wall = Stopwatch.StartNew();

        // 병렬 다운로드
        Task[] tasks = new Task[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            int idx = i;
            tasks[idx] = DownloadRangeToFileAsync(http, url, parts[idx].start, parts[idx].end, parts[idx].path, meta);
        }

        await Task.WhenAll(tasks);

        // concat (순서 중요)
        await ConcatAsync(parts.Select(p => p.path).ToArray(), finalPath);

        wall.Stop();

        long finalSize = new FileInfo(finalPath).Length;
        double mb = finalSize / 1024.0 / 1024.0;
        double secs = Math.Max(wall.Elapsed.TotalSeconds, 0.001);
        double mbps = mb / secs;

        Console.WriteLine($"FinalSize: {finalSize:N0} bytes");
        Console.WriteLine($"WALL: {secs:F2}s");
        Console.WriteLine($"Throughput: {mbps:F2} MB/s");

        // cleanup parts (원하면 주석처리)
        Directory.Delete(partDir, recursive: true);
    }

    private static (long start, long end, string path)[] BuildParts(string partDir, long totalBytes, int k)
    {
        long chunk = totalBytes / k;
        if (chunk <= 0) chunk = totalBytes;

        var parts = new (long start, long end, string path)[k];

        long cursor = 0;
        for (int i = 0; i < k; i++)
        {
            long start = cursor;
            long end = (i == k - 1) ? (totalBytes - 1) : (start + chunk - 1);

            if (end >= totalBytes) end = totalBytes - 1;

            cursor = end + 1;

            string path = Path.Combine(partDir, $"part_{i:D2}.bin");
            parts[i] = (start, end, path);
        }

        // totalBytes < k인 극단 케이스에서 중복/역전 방지(실험 범위에선 사실상 안 뜸)
        return parts.Where(p => p.start <= p.end).ToArray();
    }

    private static async Task DownloadRangeToFileAsync(
        HttpClient http,
        Uri url,
        long start,
        long end,
        string destPath,
        HeadMeta meta)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Range = new RangeHeaderValue(start, end);

        // If-Range: ETag 우선, 없으면 Last-Modified
        if (!string.IsNullOrWhiteSpace(meta.ETag))
        {
            req.Headers.TryAddWithoutValidation("If-Range", meta.ETag);
        }
        else if (!string.IsNullOrWhiteSpace(meta.LastModified))
        {
            req.Headers.TryAddWithoutValidation("If-Range", meta.LastModified);
        }

        using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        if (res.StatusCode != HttpStatusCode.PartialContent)
        {
            // 실험 목적상: 서버가 206을 안 주면 결과 해석이 깨짐
            throw new InvalidOperationException($"Expected 206 Partial Content. Got {(int)res.StatusCode} {res.ReasonPhrase}");
        }

        using var net = await res.Content.ReadAsStreamAsync();
        using var file = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 1024, useAsync: true);

        byte[] buffer = new byte[1024 * 1024];
        int read;
        while ((read = await net.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await file.WriteAsync(buffer, 0, read);
        }
    }

    private static async Task ConcatAsync(string[] partPaths, string finalPath)
    {
        using var outStream = new FileStream(finalPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 1024, useAsync: true);

        byte[] buffer = new byte[1024 * 1024];

        foreach (string part in partPaths)
        {
            using var inStream = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, useAsync: true);

            int read;
            while ((read = await inStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await outStream.WriteAsync(buffer, 0, read);
            }
        }

        await outStream.FlushAsync();
    }

    private static async Task<HeadMeta> HeadAsync(HttpClient http, Uri url)
    {
        using var req = new HttpRequestMessage(HttpMethod.Head, url);
        using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        res.EnsureSuccessStatusCode();

        long total = res.Content.Headers.ContentLength ?? -1;

        string? acceptRanges = res.Headers.TryGetValues("Accept-Ranges", out var v)
            ? string.Join(", ", v)
            : null;

        string? etag = res.Headers.ETag?.Tag;

        string? lastModified = res.Content.Headers.LastModified.HasValue
            ? res.Content.Headers.LastModified.Value.ToString("R", CultureInfo.InvariantCulture)
            : null;

        return new HeadMeta(total, acceptRanges, etag, lastModified);
    }

    private sealed record HeadMeta(long TotalBytes, string? AcceptRanges, string? ETag, string? LastModified);
}