using Core.Patch;
using System.Diagnostics;
using System.Net;

namespace Core.Test;

/*
테스트 결과 요약
1) “총합”은 거의 고정(≈ 58 MB/s)
WALL throughput이 57~59 MB/s로 고정이므로, 시스템 전체가 그 근처에서 포화됨. (원인은 다운로드 네트워크 경로 중 병목이 있는 듯)

2) 동시성 ↑ → 파일당 속도 ↓ 는 정상
Concurrency=1이면 한 파일이 거의 58 MB/s를 다 먹고,
Concurrency=8이면 8개가 나눠 가지면서 대략 58/8 ≈ 7.25 MB/s 근처가 찍혀야 정상인데,
실제로 6~12 MB/s로 분포하는 게 딱 그 패턴임(파일 크기/시작 시점 차이 때문에 분산).

3) 그런데 “2개가 더 빠르다”는 결론은 이번 15개 케이스만 보면 성립 안 함
1: 14.47s
2: 14.15s
8: 14.19s
차이가 0.3초 이내라서 측정 노이즈 수준(디스크 캐시, 스케줄링, 순간 네트워크 변동)로 보는 게 타당함.

즉 이 케이스의 올바른 결론은:
동시성 조절로 wall time을 크게 줄이긴 어렵다(대역폭 cap).
동시성을 높이면 개별 파일 완료시간만 늘어나고, 총합은 비슷하다.

DownloadManager 정책으로 바로 연결하면
기본값 MaxConcurrency=2 유지 추천
이유: wall은 비슷한데, 8은 파일별 완료가 느려져 “체감”이 나빠짐.
더 나은 UX 목표(구체)
“전체 시간 최소화”보다, 실제로는 초반에 파일 몇 개라도 빨리 끝나게 해서 진행 체감이 중요할 확률이 큼.
이 목표면 낮은 동시성(1~2) 이 유리함.

--------------------------------------------------
Patch multi-file full download probe
Files=15, MaxConcurrency=1
[File 0] _patchdata_33801.gsz | 62.15 MB | 1.14s | 54.75 MB/s
[File 1] _patchdata_33802.gsz | 60.99 MB | 1.06s | 57.73 MB/s
[File 2] _patchdata_33803.gsz | 62.36 MB | 1.07s | 58.32 MB/s
[File 3] _patchdata_33804.gsz | 84.22 MB | 1.44s | 58.34 MB/s
[File 4] _patchdata_33806.gsz | 62.47 MB | 1.08s | 58.06 MB/s
[File 5] _patchdata_33807.gsz | 86.57 MB | 1.48s | 58.54 MB/s
[File 6] _patchdata_33808.gsz | 77.52 MB | 1.35s | 57.52 MB/s
[File 7] _patchdata_33809.gsz | 60.77 MB | 1.06s | 57.21 MB/s
[File 8] _patchdata_33810.gsz | 1.43 MB | 0.04s | 38.52 MB/s
[File 9] _patchdata_33811.gsz | 60.99 MB | 1.05s | 58.09 MB/s
[File 10] _patchdata_33812.gsz | 62.33 MB | 1.07s | 58.24 MB/s
[File 11] _patchdata_33813.gsz | 60.80 MB | 1.04s | 58.21 MB/s
[File 12] _patchdata_33814.gsz | 14.39 MB | 0.27s | 53.90 MB/s
[File 13] _patchdata_33815.gsz | 71.16 MB | 1.27s | 56.20 MB/s
[File 14] _patchdata_33816.gsz | 2.39 MB | 0.06s | 40.88 MB/s
--------------------------------------------------
TOTAL files: 15
TOTAL downloaded: 830.56 MB
WALL time: 14.47s
TOTAL throughput (wall 기준): 57.41 MB/s

--------------------------------------------------
Patch multi-file full download probe
Files=15, MaxConcurrency=2
[File 0] _patchdata_33801.gsz | 62.15 MB | 3.45s | 17.99 MB/s
[File 1] _patchdata_33802.gsz | 60.99 MB | 1.41s | 43.17 MB/s
[File 2] _patchdata_33803.gsz | 62.36 MB | 1.60s | 39.03 MB/s
[File 3] _patchdata_33804.gsz | 84.22 MB | 2.38s | 35.43 MB/s
[File 4] _patchdata_33806.gsz | 62.47 MB | 2.55s | 24.45 MB/s
[File 5] _patchdata_33807.gsz | 86.57 MB | 2.92s | 29.61 MB/s
[File 6] _patchdata_33808.gsz | 77.52 MB | 2.65s | 29.26 MB/s
[File 7] _patchdata_33809.gsz | 60.77 MB | 1.65s | 36.92 MB/s
[File 8] _patchdata_33810.gsz | 1.43 MB | 0.08s | 17.64 MB/s
[File 9] _patchdata_33811.gsz | 60.99 MB | 2.63s | 23.16 MB/s
[File 10] _patchdata_33812.gsz | 62.33 MB | 1.89s | 33.02 MB/s
[File 11] _patchdata_33813.gsz | 60.80 MB | 1.84s | 33.03 MB/s
[File 12] _patchdata_33814.gsz | 14.39 MB | 0.57s | 25.27 MB/s
[File 13] _patchdata_33815.gsz | 71.16 MB | 1.73s | 41.05 MB/s
[File 14] _patchdata_33816.gsz | 2.39 MB | 0.10s | 23.45 MB/s
--------------------------------------------------
TOTAL files: 15
TOTAL downloaded: 830.56 MB
WALL time: 14.15s
TOTAL throughput (wall 기준): 58.69 MB/s

--------------------------------------------------
Patch multi-file full download probe
Files=15, MaxConcurrency=8
[File 0] _patchdata_33801.gsz | 62.15 MB | 8.35s | 7.45 MB/s
[File 1] _patchdata_33802.gsz | 60.99 MB | 7.70s | 7.92 MB/s
[File 2] _patchdata_33803.gsz | 62.36 MB | 7.80s | 8.00 MB/s
[File 3] _patchdata_33804.gsz | 84.22 MB | 12.73s | 6.62 MB/s
[File 4] _patchdata_33806.gsz | 62.47 MB | 7.65s | 8.17 MB/s
[File 5] _patchdata_33807.gsz | 86.57 MB | 13.99s | 6.19 MB/s
[File 6] _patchdata_33808.gsz | 77.52 MB | 9.99s | 7.76 MB/s
[File 7] _patchdata_33809.gsz | 60.77 MB | 7.85s | 7.74 MB/s
[File 8] _patchdata_33810.gsz | 1.43 MB | 0.17s | 8.34 MB/s
[File 9] _patchdata_33811.gsz | 60.99 MB | 5.96s | 10.23 MB/s
[File 10] _patchdata_33812.gsz | 62.33 MB | 5.86s | 10.63 MB/s
[File 11] _patchdata_33813.gsz | 60.80 MB | 5.95s | 10.22 MB/s
[File 12] _patchdata_33814.gsz | 14.39 MB | 1.93s | 7.45 MB/s
[File 13] _patchdata_33815.gsz | 71.16 MB | 5.84s | 12.18 MB/s
[File 14] _patchdata_33816.gsz | 2.39 MB | 0.25s | 9.44 MB/s
--------------------------------------------------
TOTAL files: 15
TOTAL downloaded: 830.56 MB
WALL time: 14.19s
TOTAL throughput (wall 기준): 58.54 MB/s
*/

[TestClass]
public partial class ParallelBandwidthProbeTests
{
    private static readonly Uri[] PatchUrls =
    [
        // new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/_patchdata_33800.gsz"),
        new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/_patchdata_33801.gsz"),
        new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/_patchdata_33802.gsz"),
        new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/_patchdata_33803.gsz"),
        new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/_patchdata_33804.gsz"),
        new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/_patchdata_33806.gsz"),
        new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/_patchdata_33807.gsz"),
        new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/_patchdata_33808.gsz"),
        new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/_patchdata_33809.gsz"),
        new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/_patchdata_33810.gsz"),
        new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/_patchdata_33811.gsz"),
        new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/_patchdata_33812.gsz"),
        new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/_patchdata_33813.gsz"),
        new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/_patchdata_33814.gsz"),
        new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/_patchdata_33815.gsz"),
        new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/_patchdata_33816.gsz"),
    ];

    private string _tempFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp");

    [Ignore("전체 대역폭이 제한되어 있고, 동시성을 올려도 변함이 거의 없다는 것이 증명되었음. 최대 2개까지만 유효")]
    [DoNotParallelize]
    [DataTestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(8)]
    public async Task MultiPatchFiles_FullDownload_WithDownloader_LimitedConcurrency(int maxConcurrency)
    {
        // 실제 파일 수보다 큰 동시성은 의미 없으니 clamp
        int fileCount = PatchUrls.Length;
        int concurrency = Math.Min(maxConcurrency, fileCount);

        Directory.CreateDirectory(_tempFolder);

        using var http = new HttpClient(new HttpClientHandler
        {
            MaxConnectionsPerServer = Math.Max(16, concurrency),
            AutomaticDecompression = DecompressionMethods.None
        })
        {
            Timeout = TimeSpan.FromMinutes(30)
        };

        var downloader = new Downloader(http);

        // 다운로드 작업 목록 준비
        var jobs = new List<PatchDownloadJob>(fileCount);
        for (int i = 0; i < fileCount; i++)
        {
            string fileName = Path.GetFileName(PatchUrls[i].LocalPath);
            string dest = Path.Combine(_tempFolder, $"PatchProbe_{concurrency}_{i}_{fileName}");
            string temp = dest + ".crdownload";

            if (File.Exists(dest)) File.Delete(dest);
            if (File.Exists(temp)) File.Delete(temp);

            jobs.Add(new PatchDownloadJob(i, PatchUrls[i], dest, temp));
        }

        Debug.WriteLine("--------------------------------------------------");
        Debug.WriteLine($"Patch multi-file full download probe");
        Debug.WriteLine($"Files={fileCount}, MaxConcurrency={concurrency}");

        using var cts = new CancellationTokenSource();
        using var gate = new SemaphoreSlim(concurrency, concurrency);

        var wall = Stopwatch.StartNew();

        var tasks = jobs.Select(async job =>
        {
            await gate.WaitAsync(cts.Token);
            try
            {
                return await RunPatchJobAsync(downloader, job, cts.Token);
            }
            finally
            {
                gate.Release();
            }
        }).ToArray();

        PatchDownloadJobResult[] results = await Task.WhenAll(tasks);

        wall.Stop();

        long totalBytes = results.Sum(r => r.DownloadedBytes);
        double totalMB = totalBytes / 1024.0 / 1024.0;
        double totalThroughput = totalMB / Math.Max(wall.Elapsed.TotalSeconds, 0.001);

        foreach (var r in results.OrderBy(r => r.Index))
        {
            Debug.WriteLine(
                $"[File {r.Index}] {r.FileName} | " +
                $"{r.DownloadedBytes / 1024.0 / 1024.0:F2} MB | " +
                $"{r.Elapsed.TotalSeconds:F2}s | " +
                $"{r.AvgMbps:F2} MB/s");
        }

        Debug.WriteLine("--------------------------------------------------");
        Debug.WriteLine($"TOTAL files: {results.Length}");
        Debug.WriteLine($"TOTAL downloaded: {totalMB:F2} MB");
        Debug.WriteLine($"WALL time: {wall.Elapsed.TotalSeconds:F2}s");
        Debug.WriteLine($"TOTAL throughput (wall 기준): {totalThroughput:F2} MB/s");
    } // 메서드 끝나는 지점

    private sealed record PatchDownloadJob(
        int Index,
        Uri Url,
        string DestinationPath,
        string TempPath);

    private sealed record PatchDownloadJobResult(
        int Index,
        string FileName,
        long DownloadedBytes,
        TimeSpan Elapsed,
        double AvgMbps);

    private async Task<PatchDownloadJobResult> RunPatchJobAsync(
        Downloader downloader,
        PatchDownloadJob job,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        await downloader.DownloadAsync(
            job.Url,
            job.DestinationPath,
            new DownloadOptions(
                TempPath: job.TempPath,
                Overwrite: true,
                MaxRetries: 8,
                BufferSize: 1024 * 1024),
            progress: null, // 속도 비교 목적이라 파일별 progress는 생략
            ct);

        sw.Stop();

        long bytes = new FileInfo(job.DestinationPath).Length;
        double avgMbps = (bytes / 1024.0 / 1024.0) / Math.Max(sw.Elapsed.TotalSeconds, 0.001);

        return new PatchDownloadJobResult(
            job.Index,
            Path.GetFileName(job.Url.LocalPath),
            bytes,
            sw.Elapsed,
            avgMbps);
    }
}