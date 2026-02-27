namespace Core.Test;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

[TestClass]
[Ignore("Extractor benchmark only run mode")]
public class SingleResumeDownloadTests {
    private const string DownloadUrl = "http://ak-gersangkr.xcache.kinxcdn.com/FullClient/Gersang_Install.7z";

    // Resume 재시도 횟수
    private const int MaxRetries = 8;

    // 파일 I/O 버퍼
    private const int BufferSize = 1024 * 1024; // 1MB

    private string _tempFolder = default!;
    private string _finalPath = default!;
    private string _partialPath = default!;

    private static readonly HttpClient _httpClient = new HttpClient(
        new HttpClientHandler {
            MaxConnectionsPerServer = 1, // 단일 연결 의도 명확화
            AutomaticDecompression = DecompressionMethods.None
        }) {
        Timeout = TimeSpan.FromMinutes(30)
    };

    [TestInitialize]
    public void Setup() {
        _tempFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp");
        _finalPath = Path.Combine(_tempFolder, "Gersang_SingleResume_Install.7z");
        _partialPath = _finalPath + ".crdownload";

        Directory.CreateDirectory(_tempFolder);

        // 테스트는 "resume"를 보기 위해 partial 파일을 지우지 않는다.
        // 필요하면 여기서 삭제하면 됨.
        if(File.Exists(_finalPath))
            File.Delete(_finalPath);

        Debug.WriteLine($"다운로드 대상: {DownloadUrl}");
        Debug.WriteLine($"임시 파일: {_partialPath}");
        Debug.WriteLine($"최종 파일: {_finalPath}");
    }

    [Ignore("duration : 3m 51s 883ms")]
    [TestMethod]
    public async Task SingleStreamResumeDownloadTest() {
        var startTime = DateTime.Now;

        // 1) HEAD로 메타 확보
        var meta = await GetServerMetadataAsync();

        Debug.WriteLine($"서버 Content-Length: {meta.TotalSize:N0} bytes");
        Debug.WriteLine($"서버 ETag: {meta.ETag ?? "(none)"}");
        Debug.WriteLine($"서버 Last-Modified: {(meta.LastModifiedUtc.HasValue ? meta.LastModifiedUtc.Value.ToString("R") : "(none)")}");

        // 2) 이미 받아둔 partial이 있으면 이어받기, 없으면 새로 시작
        long localSize = File.Exists(_partialPath) ? new FileInfo(_partialPath).Length : 0;

        if(localSize > meta.TotalSize) {
            // 로컬이 더 크면 의미 없으니 새로 시작
            File.Delete(_partialPath);
            localSize = 0;
        }

        if(localSize == meta.TotalSize && meta.TotalSize > 0) {
            // 이미 완료 상태로 간주 → rename
            FinalizeDownload();
            Debug.WriteLine("이미 동일 크기 다운로드 완료로 판단되어 종료");
            return;
        }

        Debug.WriteLine($"로컬 진행: {localSize:N0} / {meta.TotalSize:N0} bytes ({(meta.TotalSize > 0 ? (localSize * 100.0 / meta.TotalSize).ToString("F2", CultureInfo.InvariantCulture) : "0")}%)");

        // 3) 다운로드(Resume)
        await DownloadWithResumeAsync(meta, localSize);

        // 4) 완료 처리
        FinalizeDownload();

        var duration = DateTime.Now - startTime;
        double mb = meta.TotalSize / 1024.0 / 1024.0;
        double mbps = mb / Math.Max(duration.TotalSeconds, 0.001);

        Debug.WriteLine("--------------------------------------------------");
        Debug.WriteLine($"소요 시간: {duration.TotalSeconds:F2}초");
        Debug.WriteLine($"평균 속도: {mbps:F2} MB/s");
    }

    private sealed class ServerMetadata {
        public long TotalSize { get; init; }
        public string? ETag { get; init; }
        public DateTimeOffset? LastModifiedUtc { get; init; }
        public bool AcceptRangesBytes { get; init; }
    }

    private async Task<ServerMetadata> GetServerMetadataAsync() {
        using var headReq = new HttpRequestMessage(HttpMethod.Head, DownloadUrl);
        using var headRes = await _httpClient.SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead);

        headRes.EnsureSuccessStatusCode();

        long totalSize = headRes.Content.Headers.ContentLength
                         ?? throw new InvalidOperationException("Content-Length가 없어 용량 확인 불가");

        bool acceptRanges = false;
        if(headRes.Headers.TryGetValues("Accept-Ranges", out var values)) {
            acceptRanges = values.Any(v => v.IndexOf("bytes", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        return new ServerMetadata {
            TotalSize = totalSize,
            ETag = headRes.Headers.ETag?.Tag, // e.g. "6941f865:5314a079"
            LastModifiedUtc = headRes.Content.Headers.LastModified,
            AcceptRangesBytes = acceptRanges
        };
    }

    private async Task DownloadWithResumeAsync(ServerMetadata meta, long initialLocalSize) {
        long localSize = initialLocalSize;

        // append 모드로 열고, localSize 지점부터 이어서 기록
        using var fileStream = new FileStream(
            _partialPath,
            FileMode.OpenOrCreate,
            FileAccess.Write,
            FileShare.Read,
            BufferSize,
            useAsync: true);

        fileStream.Seek(localSize, SeekOrigin.Begin);

        for(int attempt = 1; attempt <= MaxRetries; attempt++) {
            try {
                if(localSize > 0)
                    Debug.WriteLine($"[시도 {attempt}] Resume from {localSize:N0}");

                using var req = new HttpRequestMessage(HttpMethod.Get, DownloadUrl);

                // Resume 지점부터 끝까지
                if(localSize > 0)
                    req.Headers.Range = new RangeHeaderValue(localSize, null);

                // 가능한 경우 If-Range로 “같은 파일일 때만 부분 전송” 유도
                // ETag 우선, 없으면 Last-Modified 사용
                if(localSize > 0) {
                    if(!string.IsNullOrWhiteSpace(meta.ETag)) {
                        req.Headers.TryAddWithoutValidation("If-Range", meta.ETag);
                    } else if(meta.LastModifiedUtc.HasValue) {
                        req.Headers.TryAddWithoutValidation("If-Range", meta.LastModifiedUtc.Value.ToString("R"));
                    }
                }

                using var res = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

                // 서버가 resume을 무시하고 200으로 전체를 주면, partial은 버리고 처음부터 다시 받는다.
                if(localSize > 0 && res.StatusCode == HttpStatusCode.OK) {
                    Debug.WriteLine("[경고] 서버가 Range/If-Range를 무시하고 200(전체)을 반환. partial 삭제 후 처음부터 재시작.");
                    fileStream.SetLength(0);
                    localSize = 0;
                    fileStream.Seek(0, SeekOrigin.Begin);
                    continue;
                }

                res.EnsureSuccessStatusCode();

                using var netStream = await res.Content.ReadAsStreamAsync();

                byte[] buffer = new byte[BufferSize];
                int read;

                while((read = await netStream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                    await fileStream.WriteAsync(buffer, 0, read);
                    localSize += read;

                    // 진행률 로그는 과도하면 느려지니 큰 단위로만
                    if(meta.TotalSize > 0 && (localSize % (256L * 1024L * 1024L) < read)) // 256MB마다
                    {
                        double pct = localSize * 100.0 / meta.TotalSize;
                        Debug.WriteLine($"진행: {localSize:N0} / {meta.TotalSize:N0} ({pct:F2}%)");
                    }
                }

                await fileStream.FlushAsync();

                // 여기서 사이즈가 다르면 다음 루프에서 resume로 이어서 받게 됨
                if(meta.TotalSize > 0 && localSize >= meta.TotalSize) {
                    Debug.WriteLine("다운로드 스트림 종료: 목표 크기 도달로 판단");
                    return;
                }

                // 스트림이 정상 종료됐는데도 덜 받았으면 재시도
                Debug.WriteLine($"[시도 {attempt}] 스트림 종료됐지만 아직 부족: {localSize:N0} / {meta.TotalSize:N0}");
            } catch(Exception ex) {
                Debug.WriteLine($"[시도 {attempt}] 실패: {ex.Message}");
            }

            // 다음 재시도 전에 현재 파일 길이를 다시 측정(예외 발생 시 부분 기록 가능)
            localSize = new FileInfo(_partialPath).Length;
            fileStream.Seek(localSize, SeekOrigin.Begin);

            await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, 1 << Math.Min(attempt, 5))));
        }

        throw new IOException($"다운로드 재시도 {MaxRetries}회 초과. partial={new FileInfo(_partialPath).Length:N0} bytes");
    }

    private void FinalizeDownload() {
        // 최종 파일이 이미 있으면 삭제 후 이동
        if(File.Exists(_finalPath))
            File.Delete(_finalPath);

        File.Move(_partialPath, _finalPath);
        Debug.WriteLine($"완료: {_finalPath}");
    }
}