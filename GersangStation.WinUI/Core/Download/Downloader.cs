using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;

namespace Core.Download;

/// <summary>
/// 파일 1개를 다운로드하는 저수준 클래스.
/// 같은 destinationPath에 대한 중복 다운로드는 내부적으로 직렬화한다.
///
/// 정책 요약:
/// 1. 다운로드 중에는 최종 파일을 직접 쓰지 않고 ".gsdownload" temp 파일에만 기록한다.
/// 2. metadata 파일은 항상 "destinationPath.meta"에 저장한다.
///    이 metadata는 final 파일 또는 temp 파일 중 하나와 반드시 함께 존재해야 유효하다.
/// 3. final 파일 + metadata가 유효하면 다운로드를 생략할 수 있다.
/// 4. temp 파일 + metadata가 유효하면 HTTP Range 기반 resume을 시도할 수 있다.
/// 5. 서버의 If-Range 동작은 신뢰하지 않으므로 사용하지 않는다.
/// 6. Accept-Ranges 헤더는 참고 정보일 뿐이며, resume 허용/거부의 근거로 사용하지 않는다.
/// 7. 실제 resume 성공 여부는
///    - 요청 시 Range 헤더를 보냈는지
///    - 응답이 206 Partial Content인지
///    - Content-Range 시작 위치/전체 길이가 기대값과 정확히 일치하는지
///    를 모두 확인한 뒤에만 인정한다.
/// 8. 서버가 Range 요청에 200 OK 전체 응답을 주면 "이어붙이기"를 절대 하지 않고
///    temp를 비운 뒤 0부터 다시 받는다.
/// 9. 사용자가 "새로 받기"를 선택하면 Downloader가 관리하는 기존 산출물(final/temp/meta)을
///    먼저 폐기한 뒤 처음부터 다시 받는다.
/// </summary>
public sealed class Downloader
{
    public enum DownloadFailureStage
    {
        MetadataLookup,
        Transfer
    }

    private enum MetadataLookupFailureStage
    {
        HeadRequest,
        RangeProbe,
        HeadAndRangeProbe
    }

    private enum MetadataReadFailureStage
    {
        MissingFile,
        ReadFileContents,
        ValidateLineCount,
        ParseTotalBytes,
        ParseLastModified
    }

    /// <summary>
    /// 다운로드 실패 시 URL, 대상 경로, 단계 정보를 함께 보존합니다.
    /// </summary>
    public sealed class DownloadOperationException : IOException
    {
        public Uri Url { get; }
        public string DestinationPath { get; }
        public DownloadFailureStage Stage { get; }

        public DownloadOperationException(
            string message,
            Uri url,
            string destinationPath,
            DownloadFailureStage stage,
            Exception innerException)
            : base(message, innerException)
        {
            Url = url;
            DestinationPath = destinationPath;
            Stage = stage;
        }
    }

    private sealed class MetadataLookupException : InvalidOperationException
    {
        public Uri Url { get; }
        public MetadataLookupFailureStage Stage { get; }

        public MetadataLookupException(
            string message,
            Uri url,
            MetadataLookupFailureStage stage,
            Exception innerException)
            : base(message, innerException)
        {
            Url = url;
            Stage = stage;
        }
    }

    private sealed record MetadataReadResult(
        bool Success,
        LocalMetadata? Metadata,
        MetadataReadFailureStage? FailureStage,
        string? FailureReason,
        Exception? Exception);

    private readonly HttpClient _http;

    // 동일한 최종 경로에 대해 동시에 다운로드가 수행되면
    // temp 파일/최종 파일/meta 파일이 서로 꼬일 수 있으므로 경로별 잠금을 둔다.
    private static readonly Dictionary<string, SemaphoreSlim> _pathLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _pathLocksGate = new();

    public Downloader(HttpClient httpClient)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// 지정한 URL의 파일을 destinationPath로 다운로드한다.
    ///
    /// 다운로드 정책:
    /// - 최종 파일은 완료 시점에만 교체한다.
    /// - final 파일 + metadata가 현재 서버와 동일하면 다운로드를 생략한다.
    /// - temp 파일 + metadata가 현재 서버와 동일하면 resume 후보로 본다.
    /// - resume 후보라고 해도 실제 GET 응답이 올바른 206 partial 응답인지
    ///   다시 검증해야만 이어쓴다.
    /// - If-Range는 서버 구현 편차가 있어 신뢰하지 않으며, 보내지 않는다.
    ///   안전성은 응답 검증으로 확보한다.
    /// - 사용자가 "새로 받기"를 선택한 경우에는 기존 final/temp/meta를 폐기하고 다시 받는다.
    /// </summary>
    /// <param name="url">다운로드할 원본 파일의 URL.</param>
    /// <param name="destinationPath">최종적으로 저장할 파일 경로.</param>
    /// <param name="options">덮어쓰기, 재시도 횟수, 버퍼 크기, 기존 산출물 처리 정책.</param>
    /// <param name="progress">
    /// 진행률 보고 대상.
    /// 총 길이를 알 수 있는 경우 현재 수신 바이트 / 총 바이트 / 추정 전송 속도를 전달한다.
    /// null이면 진행률을 보고하지 않는다.
    /// </param>
    /// <param name="ct">다운로드 취소 토큰.</param>
    /// <exception cref="ArgumentNullException">url 또는 options가 null인 경우.</exception>
    /// <exception cref="ArgumentException">destinationPath가 비어 있거나 공백뿐인 경우.</exception>
    /// <exception cref="ArgumentOutOfRangeException">재시도 횟수 또는 버퍼 크기가 유효 범위를 벗어난 경우.</exception>
    /// <exception cref="IOException">
    /// 최종 파일이 이미 존재하고 덮어쓰기가 허용되지 않았거나,
    /// 재시도 이후에도 전체 파일을 끝까지 받지 못한 경우.
    /// </exception>
    /// <exception cref="InvalidOperationException">서버가 Content-Length를 제공하지 않는 경우.</exception>
    /// <exception cref="OperationCanceledException">다운로드 도중 취소된 경우.</exception>
    public async Task DownloadFileAsync(
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

        SemaphoreSlim pathLock = GetPathLock(destinationPath);
        await pathLock.WaitAsync(ct).ConfigureAwait(false);

        LogDownload(destinationPath, $"START url={url}");

        try
        {
            string? directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string tempPath = $"{destinationPath}.gsdownload";
            string metaPath = GetMetadataPath(destinationPath);

            // 사용자가 "새로 받기"를 선택한 경우,
            // Downloader가 관리하는 기존 산출물(final/temp/meta)을 먼저 폐기한다.
            if (options.ExistingArtifactMode == DownloadExistingArtifactMode.RestartFromScratch)
            {
                LogDownload(destinationPath, "ARTIFACT_RESET reason=user requested restart from scratch");
                DeleteFileIfExists(destinationPath);
                DeleteFileIfExists(tempPath);
                DeleteFileIfExists(metaPath);
            }

            // 먼저 서버 파일의 "전체 길이 + validator(ETag / Last-Modified)"를 조회한다.
            // Accept-Ranges는 resume 판정의 근거로 신뢰하지 않으므로 사용하지 않는다.
            ServerMetadata serverMeta;
            try
            {
                serverMeta = await GetServerMetadataAsync(url, destinationPath, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new DownloadOperationException(
                    $"Failed to read server metadata before downloading '{url}'.",
                    url,
                    destinationPath,
                    DownloadFailureStage.MetadataLookup,
                    ex);
            }

            LogDownload(
                destinationPath,
                $"SERVER total={serverMeta.TotalBytes?.ToString() ?? "null"}, etag={serverMeta.ETag ?? "null"}, lastModified={serverMeta.LastModifiedUtc?.ToString("O") ?? "null"}");

            if (serverMeta.TotalBytes is null || serverMeta.TotalBytes <= 0)
                throw new InvalidOperationException("Server did not provide Content-Length.");

            long totalBytes = serverMeta.TotalBytes.Value;
            long localSize = 0;

            var existingState = ClassifyExistingArtifacts(
                destinationPath,
                tempPath,
                metaPath,
                serverMeta,
                out long existingLength);

            switch (existingState)
            {
                case ExistingArtifactState.CompletedAndReusable:
                    // final 파일과 metadata가 현재 서버와 동일하다면,
                    // 최종 archive를 다시 받을 필요가 없다.
                    if (existingLength == totalBytes)
                    {
                        LogDownload(destinationPath, $"SKIP_COMPLETED bytes={totalBytes}");
                        progress?.Report(new DownloadProgress(totalBytes, totalBytes, null));
                        return;
                    }

                    LogDownload(
                        destinationPath,
                        $"ARTIFACT_RESET reason=completed file size mismatch existing={existingLength} total={totalBytes}");

                    if (!options.Overwrite)
                        throw new IOException($"Destination file already exists: {destinationPath}");

                    DeleteFileIfExists(destinationPath);
                    DeleteFileIfExists(metaPath);
                    break;

                case ExistingArtifactState.PartialAndReusable:
                    if (existingLength > totalBytes)
                    {
                        LogDownload(
                            destinationPath,
                            $"ARTIFACT_RESET reason=temp larger than server total existing={existingLength} total={totalBytes}");

                        DeleteFileIfExists(tempPath);
                        DeleteFileIfExists(metaPath);
                    }
                    else
                    {
                        localSize = existingLength;
                        LogDownload(destinationPath, $"TEMP_REUSE existingTemp={localSize}");
                    }
                    break;

                case ExistingArtifactState.InvalidArtifacts:
                    LogDownload(destinationPath, "ARTIFACT_RESET reason=invalid local artifact state");

                    // final 파일이 남아 있는데 이를 안전하게 재사용할 수 없고
                    // caller도 덮어쓰기를 허용하지 않았다면 즉시 실패한다.
                    if (File.Exists(destinationPath) && !options.Overwrite)
                        throw new IOException($"Destination file already exists: {destinationPath}");

                    DeleteFileIfExists(destinationPath);
                    DeleteFileIfExists(tempPath);
                    DeleteFileIfExists(metaPath);
                    break;

                case ExistingArtifactState.None:
                default:
                    break;
            }

            // 새로 시작하는 경우 현재 서버 metadata를 기록해 둔다.
            // 이후 재시작 시 이 metadata와 현재 서버 metadata를 비교해
            // skip/resume/full download 여부를 판단한다.
            if (localSize == 0)
            {
                WriteMetadata(metaPath, serverMeta);
                LogDownload(destinationPath, "META_WRITE fresh start");
            }

            // temp 길이가 전체 길이와 정확히 같으면 다운로드는 이미 끝난 상태로 보고
            // 최종 파일 교체만 수행한다.
            if (localSize == totalBytes)
            {
                LogDownload(destinationPath, $"FINALIZE_FROM_TEMP bytes={totalBytes}");
                FinalizeTempFile(tempPath, destinationPath);
                progress?.Report(new DownloadProgress(totalBytes, totalBytes, null));
                return;
            }

            var sw = Stopwatch.StartNew();
            long bytesAtLastReport = localSize;
            long msAtLastReport = 0;
            bool completed = false;
            Exception? lastAttemptException = null;

            using (var fileStream = new FileStream(
                tempPath,
                FileMode.OpenOrCreate,
                FileAccess.Write,
                FileShare.Read,
                options.BufferSize,
                useAsync: true))
            {
                fileStream.Seek(localSize, SeekOrigin.Begin);

                // 네트워크 오류나 스트림 조기 종료에 대비해 재시도한다.
                // 실패 시에는 실제 temp 파일 길이를 다시 읽어 resume 위치를 보정한다.
                for (int attempt = 1; attempt <= options.MaxRetries; attempt++)
                {
                    LogDownload(destinationPath, $"ATTEMPT {attempt}/{options.MaxRetries} offset={localSize}");

                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        using var req = new HttpRequestMessage(HttpMethod.Get, url);

                        if (localSize > 0)
                        {
                            // resume 시에는 반드시 Range 요청을 명시적으로 보낸다.
                            // ex) Range: bytes={localSize}-
                            //
                            // 중요:
                            // - If-Range는 보내지 않는다.
                            // - 일부 서버는 If-Range를 기대와 다르게 처리하거나 무시할 수 있다.
                            // - 따라서 안전성은 "응답이 정확한 206 partial인지"를 직접 검증해서 확보한다.
                            req.Headers.Range = new RangeHeaderValue(localSize, null);
                            LogDownload(destinationPath, $"REQUEST_RANGE bytes={localSize}-");
                        }
                        else
                        {
                            LogDownload(destinationPath, "REQUEST_FULL");
                        }

                        using var response = await _http.SendAsync(
                            req,
                            HttpCompletionOption.ResponseHeadersRead,
                            ct).ConfigureAwait(false);

                        var responseMeta = CreateServerMetadataFromDownloadResponse(response);
                        LogDownload(
                            destinationPath,
                            $"RESPONSE status={(int)response.StatusCode}, total={responseMeta.TotalBytes?.ToString() ?? "null"}, etag={responseMeta.ETag ?? "null"}, lastModified={responseMeta.LastModifiedUtc?.ToString("O") ?? "null"}");

                        // HEAD/probe 이후 실제 GET이 나가기 전 사이에 서버 파일이 교체될 수 있다.
                        // 이 경우 기존 temp는 더 이상 같은 파일의 일부라고 볼 수 없으므로
                        // 현재 응답 metadata를 새 기준으로 삼고 0부터 다시 받는다.
                        if (!IsResponseForExpectedFile(serverMeta, responseMeta))
                        {
                            if (responseMeta.TotalBytes is null || responseMeta.TotalBytes <= 0)
                                throw new IOException("Download response metadata did not provide Content-Length.");

                            LogDownload(destinationPath, "TEMP_RESET reason=response metadata changed during download");

                            serverMeta = responseMeta;
                            totalBytes = responseMeta.TotalBytes.Value;

                            ResetTempFileForFreshDownload(
                                fileStream,
                                metaPath,
                                serverMeta,
                                sw,
                                ref localSize,
                                ref bytesAtLastReport,
                                ref msAtLastReport);

                            continue;
                        }

                        serverMeta = MergeServerMetadata(serverMeta, responseMeta);
                        totalBytes = serverMeta.TotalBytes!.Value;

                        // 실제 GET 응답이 HEAD보다 더 풍부한 validator를 줄 수 있다.
                        // 이후 재시작에서도 같은 기준을 쓰도록 metadata를 최신값으로 갱신한다.
                        WriteMetadata(metaPath, serverMeta);
                        LogDownload(destinationPath, "META_WRITE refreshed from download response");

                        if (localSize > 0)
                        {
                            // 여기부터는 "분명히 Range 요청을 보낸 상태"라는 전제가 중요하다.
                            //
                            // resume를 인정하는 유일한 경우:
                            // - 상태 코드가 206 Partial Content
                            // - Content-Range가 존재
                            // - Content-Range.From == localSize
                            // - Content-Range.Length == totalBytes
                            //
                            // 위 조건 중 하나라도 틀리면 partial 응답을 신뢰하지 않고
                            // temp를 비운 뒤 0부터 다시 받는다.

                            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                            {
                                if (localSize == totalBytes)
                                {
                                    LogDownload(destinationPath, $"RANGE_416_BUT_COMPLETE bytes={totalBytes}");
                                    progress?.Report(new DownloadProgress(totalBytes, totalBytes, null));
                                    completed = true;
                                    break;
                                }

                                LogDownload(destinationPath, $"TEMP_RESET reason=416 offset={localSize} total={totalBytes}");

                                ResetTempFileForFreshDownload(
                                    fileStream,
                                    metaPath,
                                    serverMeta,
                                    sw,
                                    ref localSize,
                                    ref bytesAtLastReport,
                                    ref msAtLastReport);

                                continue;
                            }

                            if (response.StatusCode == HttpStatusCode.OK)
                            {
                                LogDownload(destinationPath, "TEMP_RESET reason=server ignored range and returned 200");

                                ResetTempFileForFreshDownload(
                                    fileStream,
                                    metaPath,
                                    serverMeta,
                                    sw,
                                    ref localSize,
                                    ref bytesAtLastReport,
                                    ref msAtLastReport);

                                continue;
                            }

                            var contentRange = response.Content.Headers.ContentRange;

                            if (response.StatusCode != HttpStatusCode.PartialContent ||
                                contentRange is null ||
                                contentRange.From != localSize ||
                                contentRange.Length != totalBytes)
                            {
                                LogDownload(
                                    destinationPath,
                                    $"TEMP_RESET reason=invalid partial response from={contentRange?.From?.ToString() ?? "null"}, length={contentRange?.Length?.ToString() ?? "null"}, expectedOffset={localSize}, expectedTotal={totalBytes}");

                                ResetTempFileForFreshDownload(
                                    fileStream,
                                    metaPath,
                                    serverMeta,
                                    sw,
                                    ref localSize,
                                    ref bytesAtLastReport,
                                    ref msAtLastReport);

                                continue;
                            }
                        }

                        response.EnsureSuccessStatusCode();

                        using var net = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

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

                        // 다운로드 완료는 "전체 길이와 정확히 일치"할 때만 성공으로 본다.
                        if (localSize == totalBytes)
                        {
                            LogDownload(destinationPath, $"DOWNLOAD_COMPLETE bytes={totalBytes}");
                            progress?.Report(new DownloadProgress(totalBytes, totalBytes, null));
                            completed = true;
                            break;
                        }

                        // 기대 길이를 초과하는 경우는 명백한 비정상 상태다.
                        if (localSize > totalBytes)
                        {
                            LogDownload(destinationPath, $"FAIL received={localSize} expected={totalBytes} reason=downloaded size exceeded expected size");
                            throw new IOException($"Downloaded size exceeded expected size. Received {localSize:N0}/{totalBytes:N0} bytes.");
                        }

                        // 스트림이 조기에 끝났으면 재시도 루프로 돌아간다.
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        lastAttemptException = ex;
                        LogDownload(destinationPath, $"ATTEMPT_FAIL {attempt}/{options.MaxRetries} {ex.GetType().Name}: {ex.Message}");
                    }

                    // 일부 쓰기가 성공했을 수 있으므로 실제 temp 파일 길이를 기준으로 resume 위치를 보정한다.
                    // 드물지만 temp 파일이 사라졌다면 0부터 다시 시작한다.
                    localSize = File.Exists(tempPath)
                        ? new FileInfo(tempPath).Length
                        : 0;

                    fileStream.Seek(localSize, SeekOrigin.Begin);

                    if (attempt == options.MaxRetries)
                        break;

                    await Task.Delay(GetBackoff(attempt), ct).ConfigureAwait(false);
                }
            }

            if (completed)
            {
                LogDownload(destinationPath, $"FINALIZE_SUCCESS bytes={totalBytes}");
                FinalizeTempFile(tempPath, destinationPath);
                return;
            }

            LogDownload(destinationPath, $"FAIL exhausted retries received={localSize} expected={totalBytes}");
            if (lastAttemptException is not null)
            {
                throw new DownloadOperationException(
                    $"Download failed after {options.MaxRetries} attempts. Received {localSize:N0}/{totalBytes:N0} bytes.",
                    url,
                    destinationPath,
                    DownloadFailureStage.Transfer,
                    lastAttemptException);
            }

            throw new IOException($"Download failed after {options.MaxRetries} attempts. Received {localSize:N0}/{totalBytes:N0} bytes.");
        }
        finally
        {
            pathLock.Release();
        }
    }

    /// <summary>
    /// 서버 파일의 metadata를 조회한다.
    /// 먼저 HEAD를 시도하고, 실패하거나 Content-Length가 불충분하면
    /// GET bytes=0-0 probe로 fallback한다.
    ///
    /// 주의:
    /// - 여기서 필요한 것은 "전체 길이 + validator"다.
    /// - Accept-Ranges는 서버가 보내더라도 신뢰하지 않는다.
    ///   실제 resume 성공 여부는 나중의 Range GET 응답을 직접 검증해서 판단한다.
    /// </summary>
    private async Task<ServerMetadata> GetServerMetadataAsync(Uri url, string destinationPath, CancellationToken ct)
    {
        Exception? headFailure = null;
        bool headWasInsufficient = false;

        try
        {
            using var headReq = new HttpRequestMessage(HttpMethod.Head, url);
            using var headRes = await _http.SendAsync(
                headReq,
                HttpCompletionOption.ResponseHeadersRead,
                ct).ConfigureAwait(false);

            headRes.EnsureSuccessStatusCode();

            var meta = new ServerMetadata(
                headRes.Content.Headers.ContentLength,
                headRes.Headers.ETag?.Tag,
                headRes.Content.Headers.LastModified);

            if (meta.TotalBytes is not null && meta.TotalBytes > 0)
                return meta;

            headWasInsufficient = true;
            LogDownload(destinationPath, "HEAD_METADATA_INSUFFICIENT fallback=range-probe");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            headFailure = new MetadataLookupException(
                $"HEAD metadata lookup failed for '{url}'.",
                url,
                MetadataLookupFailureStage.HeadRequest,
                ex);
            LogDownload(destinationPath, $"HEAD_METADATA_FAIL {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            return await GetServerMetadataFromRangeProbeAsync(url, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            MetadataLookupException rangeProbeFailure = new(
                $"Range probe metadata lookup failed for '{url}'.",
                url,
                MetadataLookupFailureStage.RangeProbe,
                ex);

            if (headFailure is not null)
            {
                throw new MetadataLookupException(
                    $"Both HEAD metadata lookup and range probe failed for '{url}'. HEAD: {headFailure.GetType().Name}: {headFailure.Message}",
                    url,
                    MetadataLookupFailureStage.HeadAndRangeProbe,
                    rangeProbeFailure);
            }

            if (headWasInsufficient)
            {
                throw new MetadataLookupException(
                    $"HEAD metadata was insufficient and range probe failed for '{url}'.",
                    url,
                    MetadataLookupFailureStage.RangeProbe,
                    rangeProbeFailure);
            }

            throw rangeProbeFailure;
        }
    }

    /// <summary>
    /// HEAD가 실패하거나 metadata가 불충분할 때 사용하는 fallback probe.
    /// Range: bytes=0-0 요청으로 전체 길이(Content-Range.Length)를 얻는 것을 기대한다.
    ///
    /// 이 probe는 resume 허용 여부를 판정하기 위한 것이 아니라,
    /// HEAD 대신 전체 길이와 validator를 확보하기 위한 fallback이다.
    /// </summary>
    private async Task<ServerMetadata> GetServerMetadataFromRangeProbeAsync(Uri url, CancellationToken ct)
    {
        using var getReq = new HttpRequestMessage(HttpMethod.Get, url);
        getReq.Headers.Range = new RangeHeaderValue(0, 0);

        using var getRes = await _http.SendAsync(
            getReq,
            HttpCompletionOption.ResponseHeadersRead,
            ct).ConfigureAwait(false);

        Debug.WriteLine($"Probe URL: {url}");
        Debug.WriteLine($"Request Range: {getReq.Headers.Range}");
        Debug.WriteLine($"StatusCode: {(int)getRes.StatusCode} {getRes.StatusCode}");
        Debug.WriteLine($"Final RequestUri: {getRes.RequestMessage?.RequestUri}");

        if (getRes.StatusCode == HttpStatusCode.NotFound)
        {
            throw new HttpRequestException($"Metadata probe returned 404. url={url}");
        }

        if (getRes.StatusCode != HttpStatusCode.OK &&
            getRes.StatusCode != HttpStatusCode.PartialContent)
        {
            getRes.EnsureSuccessStatusCode();
        }

        long? totalBytes = getRes.Content.Headers.ContentLength;

        if (getRes.Content.Headers.ContentRange?.Length is long rangeLength)
            totalBytes = rangeLength;

        return new ServerMetadata(
            totalBytes,
            getRes.Headers.ETag?.Tag,
            getRes.Content.Headers.LastModified);
    }

    /// <summary>
    /// 기존 서버 metadata와 실제 다운로드 응답 metadata를 합쳐
    /// 더 풍부한 정보를 가진 기준 metadata를 만든다.
    /// 전체 길이는 response 쪽 값을 우선 사용하고,
    /// ETag / Last-Modified는 비어 있지 않은 값을 우선 채택한다.
    /// </summary>
    private static ServerMetadata MergeServerMetadata(
        ServerMetadata baseMeta,
        ServerMetadata responseMeta)
    {
        return new ServerMetadata(
            responseMeta.TotalBytes ?? baseMeta.TotalBytes,
            !string.IsNullOrWhiteSpace(responseMeta.ETag) ? responseMeta.ETag : baseMeta.ETag,
            responseMeta.LastModifiedUtc ?? baseMeta.LastModifiedUtc);
    }

    /// <summary>
    /// 실제 다운로드 응답(GET / Range GET)에서 비교용 metadata를 추출한다.
    /// 206 응답인 경우 전체 길이는 Content-Range.Length를 우선 사용하고,
    /// 200 응답인 경우 Content-Length를 전체 길이로 사용한다.
    /// </summary>
    private static ServerMetadata CreateServerMetadataFromDownloadResponse(HttpResponseMessage response)
    {
        long? totalBytes = null;

        if (response.Content.Headers.ContentRange?.Length is long rangeLength)
        {
            totalBytes = rangeLength;
        }
        else if (response.StatusCode == HttpStatusCode.OK)
        {
            totalBytes = response.Content.Headers.ContentLength;
        }

        return new ServerMetadata(
            totalBytes,
            response.Headers.ETag?.Tag,
            response.Content.Headers.LastModified);
    }

    /// <summary>
    /// 다운로드 시작 전에 조회한 metadata와 실제 다운로드 응답 metadata가
    /// 같은 파일을 가리키는지 판정한다.
    /// 전체 길이는 반드시 같아야 하고,
    /// ETag / Last-Modified는 응답이 해당 값을 제공한 경우에만 비교한다.
    /// </summary>
    private static bool IsResponseForExpectedFile(
        ServerMetadata expectedMeta,
        ServerMetadata responseMeta)
    {
        if (expectedMeta.TotalBytes != responseMeta.TotalBytes)
            return false;

        if (!string.IsNullOrWhiteSpace(expectedMeta.ETag) &&
            !string.IsNullOrWhiteSpace(responseMeta.ETag) &&
            !string.Equals(expectedMeta.ETag, responseMeta.ETag, StringComparison.Ordinal))
        {
            return false;
        }

        if (expectedMeta.LastModifiedUtc.HasValue &&
            responseMeta.LastModifiedUtc.HasValue &&
            expectedMeta.LastModifiedUtc != responseMeta.LastModifiedUtc)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// final 파일에 대응하는 metadata 파일 경로를 반환한다.
    /// metadata는 final 파일 또는 temp 파일 중 하나와 짝을 이루어야만 유효하다.
    /// </summary>
    private static string GetMetadataPath(string destinationPath)
    {
        return $"{destinationPath}.meta";
    }

    /// <summary>
    /// 현재 남아 있는 local metadata가 "지금 내려받으려는 서버 파일"과 같은 원본을 가리키는지 판정한다.
    ///
    /// 정책:
    /// - metadata 재사용 여부는 local metadata와 현재 서버 metadata의 일치 여부로만 판단한다.
    /// - 서버 If-Range 동작은 신뢰하지 않는다.
    /// - validator가 하나도 없으면 같은 파일이라고 증명할 수 없으므로 재사용을 허용하지 않는다.
    /// - validator는 "예전에 있었던 값"만으로는 부족하고, 현재 서버도 같은 값을 다시 제공해야 한다.
    ///   즉, 지금 시점에도 같은 파일이라고 확인 가능할 때만 재사용한다.
    /// </summary>
    private static bool IsMetadataReusable(
        LocalMetadata localMeta,
        ServerMetadata serverMeta)
    {
        if (localMeta.TotalBytes != serverMeta.TotalBytes)
            return false;

        bool hasAnyValidator =
            !string.IsNullOrWhiteSpace(localMeta.ETag) ||
            localMeta.LastModifiedUtc.HasValue;

        if (!hasAnyValidator)
            return false;

        if (!string.Equals(localMeta.ETag, serverMeta.ETag, StringComparison.Ordinal))
            return false;

        if (localMeta.LastModifiedUtc != serverMeta.LastModifiedUtc)
            return false;

        return true;
    }

    /// <summary>
    /// 기존 final/temp/meta 조합을 판정한다.
    ///
    /// 상태 정의:
    /// - CompletedAndReusable: final + metadata가 현재 서버와 동일하다고 판정됨
    /// - PartialAndReusable: temp + metadata가 현재 서버와 동일하다고 판정됨
    /// - InvalidArtifacts: metadata가 고아 상태이거나, 조합이 비정상이거나, 현재 서버와 맞지 않음
    /// - None: 아무 산출물도 없음
    /// </summary>
    private static ExistingArtifactState ClassifyExistingArtifacts(
        string destinationPath,
        string tempPath,
        string metaPath,
        ServerMetadata serverMeta,
        out long existingLength)
    {
        existingLength = 0;

        bool hasFinal = File.Exists(destinationPath);
        bool hasTemp = File.Exists(tempPath);
        bool hasMeta = File.Exists(metaPath);

        if (!hasMeta)
        {
            if (hasFinal || hasTemp)
                return ExistingArtifactState.InvalidArtifacts;

            return ExistingArtifactState.None;
        }

        MetadataReadResult metadataReadResult = TryReadMetadata(metaPath);
        if (!metadataReadResult.Success)
        {
            LogDownload(
                destinationPath,
                $"META_INVALID path={metaPath} stage={metadataReadResult.FailureStage?.ToString() ?? "unknown"} reason={metadataReadResult.FailureReason ?? "unknown"}");
            return ExistingArtifactState.InvalidArtifacts;
        }

        LocalMetadata localMeta = metadataReadResult.Metadata!;

        // final과 temp가 동시에 존재하면 어떤 파일을 신뢰해야 하는지 불명확하므로 비정상 상태로 본다.
        if (hasFinal && hasTemp)
            return ExistingArtifactState.InvalidArtifacts;

        if (hasFinal)
        {
            if (!IsMetadataReusable(localMeta, serverMeta))
                return ExistingArtifactState.InvalidArtifacts;

            existingLength = new FileInfo(destinationPath).Length;
            return ExistingArtifactState.CompletedAndReusable;
        }

        if (hasTemp)
        {
            if (!IsMetadataReusable(localMeta, serverMeta))
                return ExistingArtifactState.InvalidArtifacts;

            existingLength = new FileInfo(tempPath).Length;
            return ExistingArtifactState.PartialAndReusable;
        }

        // metadata만 남아 있는 경우는 고아 metadata이므로 무효다.
        return ExistingArtifactState.InvalidArtifacts;
    }

    /// <summary>
    /// local metadata 파일을 기록한다.
    /// 이후 재시작 시 이 정보를 현재 서버 metadata와 비교해
    /// skip/resume/full download 여부를 판정한다.
    /// </summary>
    private static void WriteMetadata(string metaPath, ServerMetadata serverMeta)
    {
        string[] lines =
        [
            serverMeta.TotalBytes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            serverMeta.ETag ?? string.Empty,
            serverMeta.LastModifiedUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty
        ];

        File.WriteAllLines(metaPath, lines);
    }

    /// <summary>
    /// local metadata 파일을 읽어 파싱한다.
    /// 파일이 없거나 형식이 맞지 않으면 실패 단계와 이유를 함께 반환합니다.
    /// </summary>
    private static MetadataReadResult TryReadMetadata(string metaPath)
    {
        if (!File.Exists(metaPath))
        {
            return new MetadataReadResult(
                false,
                null,
                MetadataReadFailureStage.MissingFile,
                "metadata file not found",
                null);
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(metaPath);
        }
        catch (Exception ex)
        {
            return new MetadataReadResult(
                false,
                null,
                MetadataReadFailureStage.ReadFileContents,
                $"{ex.GetType().Name}: {ex.Message}",
                ex);
        }

        if (lines.Length < 3)
        {
            return new MetadataReadResult(
                false,
                null,
                MetadataReadFailureStage.ValidateLineCount,
                $"expected at least 3 lines but found {lines.Length}",
                null);
        }

        if (!long.TryParse(lines[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long totalBytes))
        {
            return new MetadataReadResult(
                false,
                null,
                MetadataReadFailureStage.ParseTotalBytes,
                $"invalid totalBytes value '{lines[0]}'",
                null);
        }

        string? etag = string.IsNullOrWhiteSpace(lines[1]) ? null : lines[1];

        DateTimeOffset? lastModifiedUtc = null;
        if (!string.IsNullOrWhiteSpace(lines[2]))
        {
            if (!DateTimeOffset.TryParseExact(
                    lines[2],
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTimeOffset parsed))
            {
                return new MetadataReadResult(
                    false,
                    null,
                    MetadataReadFailureStage.ParseLastModified,
                    $"invalid lastModified value '{lines[2]}'",
                    null);
            }

            lastModifiedUtc = parsed;
        }

        return new MetadataReadResult(
            true,
            new LocalMetadata(totalBytes, etag, lastModifiedUtc),
            null,
            null,
            null);
    }

    /// <summary>
    /// partial 이어받기를 신뢰할 수 없다고 판정된 경우 temp 파일을 비우고
    /// "현재 serverMeta를 기준으로 0부터 다시 받는 상태"로 되돌린다.
    ///
    /// 이 메서드는 다음과 같은 경우에 사용된다.
    /// - 서버 파일이 다운로드 도중 교체된 경우
    /// - Range 요청에 200 OK 전체 응답이 온 경우
    /// - 206 응답이지만 Content-Range가 기대값과 맞지 않는 경우
    /// - 416으로 인해 현재 resume offset이 무효가 된 경우
    /// </summary>
    private static void ResetTempFileForFreshDownload(
        FileStream fileStream,
        string metaPath,
        ServerMetadata serverMeta,
        Stopwatch sw,
        ref long localSize,
        ref long bytesAtLastReport,
        ref long msAtLastReport)
    {
        fileStream.SetLength(0);
        fileStream.Seek(0, SeekOrigin.Begin);
        localSize = 0;

        WriteMetadata(metaPath, serverMeta);

        bytesAtLastReport = 0;
        msAtLastReport = sw.ElapsedMilliseconds;
    }

    /// <summary>
    /// 다운로드가 완료된 temp 파일을 최종 파일로 교체한다.
    /// 기존 파일이 있으면 File.Replace를 우선 사용해 교체 시점의 위험을 줄인다.
    ///
    /// 주의:
    /// - metadata는 성공 후에도 남긴다.
    /// - 성공 후 전체 temp 루트 삭제 여부는 Downloader가 아니라 상위 orchestration에서 결정한다.
    /// </summary>
    private static void FinalizeTempFile(string tempPath, string destinationPath)
    {
        if (!File.Exists(tempPath))
            throw new FileNotFoundException("Temporary download file was not found.", tempPath);

        if (!File.Exists(destinationPath))
        {
            File.Move(tempPath, destinationPath);
            return;
        }

        string backupPath = $"{destinationPath}.gsbackup";

        if (File.Exists(backupPath))
            File.Delete(backupPath);

        File.Replace(tempPath, destinationPath, backupPath, ignoreMetadataErrors: true);

        if (File.Exists(backupPath))
            File.Delete(backupPath);
    }

    /// <summary>
    /// 지정한 파일이 존재할 때만 조용히 삭제한다.
    /// local artifact 정리 시 사용한다.
    /// </summary>
    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>
    /// 재시도 대기 시간 계산.
    /// 짧은 지수 백오프를 사용해 순간적인 네트워크 오류에 덜 민감하게 한다.
    /// </summary>
    private static TimeSpan GetBackoff(int attempt)
    {
        int seconds = Math.Min(30, 1 << Math.Min(attempt - 1, 5));
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// 진행률 보고 빈도를 제한한다.
    /// 너무 잦은 UI 갱신으로 인한 오버헤드를 줄이기 위해 200ms 간격으로만 보고한다.
    /// </summary>
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
        if (elapsedMs - msAtLastReport < 200)
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
        DateTimeOffset? LastModifiedUtc);

    /// <summary>
    /// destinationPath 슬롯의 local metadata.
    /// final 파일 또는 temp 파일과 짝을 이루어 현재 서버와 동일한 원본인지 판정하는 데 사용한다.
    /// </summary>
    private sealed record LocalMetadata(
        long TotalBytes,
        string? ETag,
        DateTimeOffset? LastModifiedUtc);

    private enum ExistingArtifactState
    {
        None = 0,
        CompletedAndReusable = 1,
        PartialAndReusable = 2,
        InvalidArtifacts = 3
    }

    /// <summary>
    /// 같은 destinationPath에 대한 동시 다운로드를 막기 위한 경로별 semaphore를 반환한다.
    /// 서로 다른 파일은 병렬 다운로드가 가능하다.
    /// </summary>
    private static SemaphoreSlim GetPathLock(string destinationPath)
    {
        string fullPath = Path.GetFullPath(destinationPath);

        lock (_pathLocksGate)
        {
            if (!_pathLocks.TryGetValue(fullPath, out var semaphore))
            {
                semaphore = new SemaphoreSlim(1, 1);
                _pathLocks.Add(fullPath, semaphore);
            }

            return semaphore;
        }
    }

    private static void LogDownload(string destinationPath, string message)
    {
        Debug.WriteLine($"[Downloader] [{Path.GetFileName(destinationPath)}] {message}");
    }
}

/// <summary>
/// 기존 local artifact를 어떻게 처리할지 결정한다.
/// - ResumeIfPossible: 기존 final/temp/meta를 검사해서 재사용 가능하면 skip/resume
/// - RestartFromScratch: 기존 final/temp/meta를 폐기하고 처음부터 다시 받기
/// </summary>
public enum DownloadExistingArtifactMode
{
    ResumeIfPossible = 0,
    RestartFromScratch = 1
}

public sealed record DownloadOptions(
    bool Overwrite = false,
    int MaxRetries = 8,
    int BufferSize = 1024 * 1024,
    DownloadExistingArtifactMode ExistingArtifactMode = DownloadExistingArtifactMode.ResumeIfPossible);

public sealed record DownloadProgress(
    long BytesReceived,
    long? TotalBytes,
    double? BytesPerSecond);
