namespace GersangStation.Test;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

[TestClass]
public class ParallelBandwidthProbeTests {
    private const string DownloadUrl = "http://ak-gersangkr.xcache.kinxcdn.com/FullClient/Gersang_Install.7z";

    // 각 Range 조각 크기
    private const int SegmentSizeBytes = 32 * 1024 * 1024; // 32MB

    // ✅ 전체가 아니라 이만큼만 다운로드하고 종료 (예: 1GiB)
    private const long ProbeBytes = 1L * 1024 * 1024 * 1024; // 1GiB

    private const int MaxRetriesPerSegment = 4;

    private string _tempFolder = default!;
    private string _destinationPath = default!;

    [TestInitialize]
    public void Setup() {
        _tempFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp");
        Directory.CreateDirectory(_tempFolder);
    }

    /*
    --------------------------------------------------
    WorkerCount=1, SegmentSize=32MB, Target=1024.00 MB
    Done. Segments completed=32
    Elapsed: 17.95s, Avg: 57.06 MB/s
    --------------------------------------------------
    WorkerCount=2, SegmentSize=32MB, Target=1024.00 MB
    Done. Segments completed=32
    Elapsed: 17.68s, Avg: 57.92 MB/s
    --------------------------------------------------
    WorkerCount=4, SegmentSize=32MB, Target=1024.00 MB
    Done. Segments completed=32
    Elapsed: 17.35s, Avg: 59.01 MB/s
    --------------------------------------------------
    WorkerCount=8, SegmentSize=32MB, Target=1024.00 MB
    Done. Segments completed=32
    Elapsed: 17.84s, Avg: 57.39 MB/s
    */
    [Ignore("Done")]
    [DoNotParallelize] // ✅ DataRow 케이스들이 동시에 돌지 않게
    [DataTestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(4)]
    [DataRow(8)]
    public async Task BandwidthProbe_ByWorkerCount(int workerCount) {
        _destinationPath = Path.Combine(_tempFolder, $"Gersang_Probe_{workerCount}w.part");

        if(File.Exists(_destinationPath))
            File.Delete(_destinationPath);

        using var http = CreateHttpClient(workerCount);

        // 1) 전체 크기 (참고용)
        long totalSize = await GetTotalSizeAsync(http);

        // 2) 이번 테스트에서 실제로 받을 바이트 수 확정
        long targetBytes = Math.Min(ProbeBytes, totalSize);

        Debug.WriteLine("--------------------------------------------------");
        Debug.WriteLine($"WorkerCount={workerCount}, SegmentSize={SegmentSizeBytes / 1024 / 1024}MB, Target={(targetBytes / 1024.0 / 1024.0):F2} MB");

        // 3) 파일 공간 확보 (targetBytes만큼만)
        using(var fs = new FileStream(_destinationPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
            fs.SetLength(targetBytes);
        }

        var startTime = DateTime.Now;

        // 4) Range 큐 구성 (0 ~ targetBytes-1)
        var queue = new ConcurrentQueue<(long Start, long End)>();
        for(long start = 0; start < targetBytes; start += SegmentSizeBytes) {
            long end = Math.Min(start + SegmentSizeBytes - 1, targetBytes - 1);
            queue.Enqueue((start, end));
        }

        int completedSegments = 0;

        var tasks = new List<Task>(workerCount);
        for(int i = 0; i < workerCount; i++) {
            int workerIndex = i;
            tasks.Add(Task.Run(async () => {
                while(queue.TryDequeue(out var seg)) {
                    bool ok = await DownloadSegmentWithRetryAsync(http, seg.Start, seg.End, workerIndex);
                    if(!ok)
                        Assert.Fail($"Segment failed after retries: {seg.Start}-{seg.End}");

                    Interlocked.Increment(ref completedSegments);
                }
            }));
        }

        await Task.WhenAll(tasks);

        var duration = DateTime.Now - startTime;
        double mb = targetBytes / 1024.0 / 1024.0;
        double mbps = mb / Math.Max(duration.TotalSeconds, 0.001);

        Debug.WriteLine($"Done. Segments completed={completedSegments}");
        Debug.WriteLine($"Elapsed: {duration.TotalSeconds:F2}s, Avg: {mbps:F2} MB/s");
    }

    private static HttpClient CreateHttpClient(int maxConnectionsPerServer) {
        var handler = new HttpClientHandler {
            MaxConnectionsPerServer = maxConnectionsPerServer,
            AutomaticDecompression = DecompressionMethods.None
        };

        return new HttpClient(handler) {
            Timeout = TimeSpan.FromMinutes(10)
        };
    }

    private static async Task<long> GetTotalSizeAsync(HttpClient http) {
        using var headReq = new HttpRequestMessage(HttpMethod.Head, DownloadUrl);
        using var headRes = await http.SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead);
        headRes.EnsureSuccessStatusCode();

        return headRes.Content.Headers.ContentLength
               ?? throw new InvalidOperationException("Content-Length가 없어 용량 확인 불가");
    }

    private async Task<bool> DownloadSegmentWithRetryAsync(HttpClient http, long start, long end, int workerIndex) {
        for(int attempt = 1; attempt <= MaxRetriesPerSegment; attempt++) {
            try {
                using var req = new HttpRequestMessage(HttpMethod.Get, DownloadUrl);
                req.Headers.Range = new RangeHeaderValue(start, end);

                using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

                if(res.StatusCode != HttpStatusCode.PartialContent)
                    throw new IOException($"Expected 206, got {(int)res.StatusCode} {res.ReasonPhrase}");

                using var netStream = await res.Content.ReadAsStreamAsync();

                using var fileStream = new FileStream(_destinationPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, 1024 * 1024, useAsync: true);
                fileStream.Seek(start, SeekOrigin.Begin);

                byte[] buffer = new byte[1024 * 1024];
                long remaining = end - start + 1;

                while(remaining > 0) {
                    int toRead = (int)Math.Min(buffer.Length, remaining);
                    int read = await netStream.ReadAsync(buffer, 0, toRead);
                    if(read <= 0)
                        throw new IOException($"Stream ended early. remaining={remaining}");

                    await fileStream.WriteAsync(buffer, 0, read);
                    remaining -= read;
                }

                return true;
            } catch(Exception ex) {
                Debug.WriteLine($"[W{workerIndex}] seg {start}-{end} attempt {attempt} fail: {ex.Message}");
                if(attempt == MaxRetriesPerSegment)
                    return false;

                await Task.Delay(300 * attempt);
            }
        }

        return false;
    }
}