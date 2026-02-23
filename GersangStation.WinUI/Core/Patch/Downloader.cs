using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;

namespace Core.Patch;

public sealed class Downloader
{
    private readonly HttpClient _http;

    public Downloader(HttpClient httpClient)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task DownloadAsync(
        Uri url,
        string destinationPath,
        DownloadOptions options,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        if (url is null) throw new ArgumentNullException(nameof(url));
        if (string.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentException("destinationPath is required.", nameof(destinationPath));
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (options.MaxRetries < 1) throw new ArgumentOutOfRangeException(nameof(options.MaxRetries));
        if (options.BufferSize < 4096) throw new ArgumentOutOfRangeException(nameof(options.BufferSize));

        string? directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string tempPath = options.TempPath ?? (destinationPath + ".crdownload");

        // 최종 파일 존재 시 정책 처리
        if (File.Exists(destinationPath))
        {
            if (!options.Overwrite)
                throw new IOException($"Destination file already exists: {destinationPath}");

            File.Delete(destinationPath);
        }

        var meta = await GetServerMetadataAsync(url, ct).ConfigureAwait(false);

        if (meta.TotalBytes is null || meta.TotalBytes <= 0)
            throw new InvalidOperationException("Server did not provide Content-Length.");

        long totalBytes = meta.TotalBytes.Value;

        long localSize = File.Exists(tempPath) ? new FileInfo(tempPath).Length : 0;

        // 서버가 Range 미지원이면 resume 의미 없음 -> temp 폐기 후 처음부터
        if (!meta.AcceptRangesBytes)
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            localSize = 0;
        }

        // 비정상 partial 파일 정리
        if (localSize > totalBytes)
        {
            File.Delete(tempPath);
            localSize = 0;
        }

        // 이미 완료 상태면 rename만
        if (localSize == totalBytes)
        {
            FinalizeTempFile(tempPath, destinationPath);
            progress?.Report(new DownloadProgress(totalBytes, totalBytes, null));
            return;
        }

        var sw = Stopwatch.StartNew();
        long bytesAtLastReport = localSize;
        long msAtLastReport = 0;
        bool completed = false;

        using (var fileStream = new FileStream(
            tempPath,
            FileMode.OpenOrCreate,
            FileAccess.Write,
            FileShare.Read,
            options.BufferSize,
            useAsync: true))
        {
            fileStream.Seek(localSize, SeekOrigin.Begin);

            for (int attempt = 1; attempt <= options.MaxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, url);

                    if (localSize > 0)
                    {
                        req.Headers.Range = new RangeHeaderValue(localSize, null);

                        // If-Range: ETag 우선, 없으면 Last-Modified
                        if (!string.IsNullOrWhiteSpace(meta.ETag))
                        {
                            req.Headers.TryAddWithoutValidation("If-Range", meta.ETag);
                        }
                        else if (meta.LastModifiedUtc.HasValue)
                        {
                            req.Headers.TryAddWithoutValidation(
                                "If-Range",
                                meta.LastModifiedUtc.Value.ToString("R", CultureInfo.InvariantCulture));
                        }
                    }

                    using var res = await _http.SendAsync(
                        req,
                        HttpCompletionOption.ResponseHeadersRead,
                        ct).ConfigureAwait(false);

                    // Resume 요청했는데 서버가 200 전체 응답을 준 경우: partial 폐기 후 처음부터 재시작
                    if (localSize > 0 && res.StatusCode == HttpStatusCode.OK)
                    {
                        fileStream.SetLength(0);
                        fileStream.Seek(0, SeekOrigin.Begin);
                        localSize = 0;

                        // 진행률 기준 리셋(음수/이상 bps 방지)
                        bytesAtLastReport = 0;
                        msAtLastReport = sw.ElapsedMilliseconds;

                        continue;
                    }

                    res.EnsureSuccessStatusCode();

                    using var net = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

                    byte[] buffer = new byte[options.BufferSize];
                    int read;

                    while ((read = await net.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                        localSize += read;

                        ReportProgressIfNeeded(
                            progress,
                            localSize,
                            totalBytes,
                            sw,
                            ref bytesAtLastReport,
                            ref msAtLastReport);
                    }

                    await fileStream.FlushAsync(ct).ConfigureAwait(false);

                    if (localSize >= totalBytes)
                    {
                        progress?.Report(new DownloadProgress(totalBytes, totalBytes, null));
                        completed = true;
                        break;
                    }

                    // 스트림 종료됐는데 덜 받았으면 재시도 루프 계속
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Downloader] Attempt {attempt} failed: {ex.Message}");
                }

                // 부분 기록이 있었을 수 있으므로 실제 파일 길이 기준으로 재개 위치 보정
                localSize = new FileInfo(tempPath).Length;
                fileStream.Seek(localSize, SeekOrigin.Begin);

                if (attempt == options.MaxRetries)
                    break;

                await Task.Delay(GetBackoff(attempt), ct).ConfigureAwait(false);
            }
        }

        if (completed)
        {
            FinalizeTempFile(tempPath, destinationPath);
            return;
        }

        throw new IOException($"Download failed after {options.MaxRetries} attempts. Received {localSize:N0}/{totalBytes:N0} bytes.");
    }

    private async Task<ServerMetadata> GetServerMetadataAsync(Uri url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Head, url);
        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();

        bool acceptRangesBytes = false;
        if (res.Headers.TryGetValues("Accept-Ranges", out var values))
            acceptRangesBytes = values.Any(v => v.IndexOf("bytes", StringComparison.OrdinalIgnoreCase) >= 0);

        return new ServerMetadata(
            res.Content.Headers.ContentLength,
            res.Headers.ETag?.Tag,
            res.Content.Headers.LastModified,
            acceptRangesBytes);
    }

    private static void FinalizeTempFile(string tempPath, string destinationPath)
    {
        if (File.Exists(destinationPath))
            File.Delete(destinationPath);

        File.Move(tempPath, destinationPath);
    }

    private static TimeSpan GetBackoff(int attempt)
    {
        // 1s, 2s, 4s, 8s, 16s, 30s...
        int seconds = Math.Min(30, 1 << Math.Min(attempt - 1, 5));
        return TimeSpan.FromSeconds(seconds);
    }

    private static void ReportProgressIfNeeded(
        IProgress<DownloadProgress>? progress,
        long bytesReceived,
        long totalBytes,
        Stopwatch sw,
        ref long bytesAtLastReport,
        ref long msAtLastReport)
    {
        if (progress is null)
            return;

        long elapsedMs = sw.ElapsedMilliseconds;
        if (elapsedMs - msAtLastReport < 200) // 너무 잦은 UI 업데이트 방지
            return;

        long deltaBytes = bytesReceived - bytesAtLastReport;
        long deltaMs = elapsedMs - msAtLastReport;

        double? bps = null;
        if (deltaMs > 0)
            bps = deltaBytes * 1000.0 / deltaMs;

        bytesAtLastReport = bytesReceived;
        msAtLastReport = elapsedMs;

        progress.Report(new DownloadProgress(bytesReceived, totalBytes, bps));
    }

    private sealed record ServerMetadata(
        long? TotalBytes,
        string? ETag,
        DateTimeOffset? LastModifiedUtc,
        bool AcceptRangesBytes);
}

public sealed record DownloadOptions(
    string? TempPath = null,
    bool Overwrite = false,
    int MaxRetries = 8,
    int BufferSize = 1024 * 1024);

public sealed record DownloadProgress(
    long BytesReceived,
    long? TotalBytes,
    double? BytesPerSecond);