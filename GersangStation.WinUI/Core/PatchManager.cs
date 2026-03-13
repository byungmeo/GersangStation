using Core.Download;
using Core.Extract;
using Core.Models;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Channels;

namespace Core;

public sealed record PatchItem(
    string FileName,
    Uri DownloadUrl,
    string DownloadDestPath,
    string ExtractDestPath);

public sealed record PatchProgress(
    int TotalCount,
    int DownloadedCount,
    int ExtractedCount,
    string? DownloadingFileName,
    double? DownloadingPercent,
    string? ExtractingFileName,
    double? ExtractingPercent);

public sealed class PatchProgressState
{
    private readonly object _sync = new();

    public int TotalCount { get; init; }

    public int DownloadedCount { get; set; }
    public int ExtractedCount { get; set; }

    public string? DownloadingFileName { get; set; }
    public double? DownloadingPercent { get; set; }

    public string? ExtractingFileName { get; set; }
    public double? ExtractingPercent { get; set; }

    /// <summary>
    /// 진행 상태를 원자적으로 갱신합니다.
    /// 다운로드/압축 해제 진행 보고가 서로 다른 스레드에서 들어올 수 있으므로 잠금이 필요합니다.
    /// </summary>
    public void Update(Action<PatchProgressState> updater)
    {
        ArgumentNullException.ThrowIfNull(updater);

        lock (_sync)
        {
            updater(this);
        }
    }

    /// <summary>
    /// 현재 진행 상태의 스냅샷을 반환합니다.
    /// 프론트단에는 이 스냅샷만 전달합니다.
    /// </summary>
    public PatchProgress Snapshot()
    {
        lock (_sync)
        {
            return new PatchProgress(
                TotalCount,
                DownloadedCount,
                ExtractedCount,
                DownloadingFileName,
                DownloadingPercent,
                ExtractingFileName,
                ExtractingPercent);
        }
    }
}

public enum PatchArchiveReuseMode
{
    ResumeIfPossible = 0,
    RestartFromScratch = 1
}

public sealed record PatchRunOptions(
    bool DeleteTempFilesAfterPatch = false,
    PatchArchiveReuseMode ArchiveReuseMode = PatchArchiveReuseMode.ResumeIfPossible,
    bool ApplyMultiClientPatch = false);

public enum LatestVersionResolutionFailureStage
{
    ReadmeLookup,
    ProbeVersionInfo,
    NoPublishedVersionInfo
}

/// <summary>
/// 최신 패치 버전 확인 실패 시 단계와 서버/버전 문맥을 함께 보존합니다.
/// </summary>
public sealed class LatestVersionResolutionException : InvalidOperationException
{
    public GameServer Server { get; }
    public int? Version { get; }
    public string? VersionInfoUrl { get; }
    public LatestVersionResolutionFailureStage Stage { get; }

    public LatestVersionResolutionException(
        string message,
        GameServer server,
        LatestVersionResolutionFailureStage stage,
        Exception innerException,
        int? version = null,
        string? versionInfoUrl = null)
        : base(message, innerException)
    {
        Server = server;
        Version = version;
        VersionInfoUrl = versionInfoUrl;
        Stage = stage;
    }
}

public enum ClientVersionReadFailureStage
{
    ResolveVsnPath,
    OpenVsnFile,
    DecodeVsnContents
}

public enum ClientVersionWriteFailureStage
{
    ResolveVsnPath,
    ResolveVsnDirectory,
    CreateVsnDirectory,
    WriteVsnFile
}

public enum DirectoryDeleteFailureStage
{
    DeleteDirectory
}

/// <summary>
/// 설치된 클라이언트 버전 읽기 결과를 파일 경로와 실패 문맥과 함께 반환합니다.
/// </summary>
public sealed record ClientVersionReadResult(
    bool Success,
    int? Version,
    string VsnPath,
    ClientVersionReadFailureStage? FailureStage,
    Exception? Exception)
{
    public bool FileExists => File.Exists(VsnPath);
}

/// <summary>
/// 클라이언트 버전 쓰기 결과를 파일 경로와 실패 문맥과 함께 반환합니다.
/// </summary>
public sealed record ClientVersionWriteResult(
    bool Success,
    string VsnPath,
    ClientVersionWriteFailureStage? FailureStage,
    Exception? Exception);

/// <summary>
/// 디렉터리 삭제 재시도 결과를 실패 문맥과 함께 반환합니다.
/// </summary>
public sealed record DirectoryDeleteResult(
    bool Success,
    string Path,
    DirectoryDeleteFailureStage? FailureStage,
    Exception? Exception);

/*
public sealed record PatchProgress(
    int TotalCount,
    int DownloadedCount,
    int ExtractedCount,
    long TotalBytesReceived,
    long? TotalBytes,
    string? CurrentFileName);
*/

public sealed record ExtractJob(
    string ArchivePath,
    string DestinationPath,
    IProgress<ExtractionProgress>? Progress = null,
    Action? OnBeforeExtract = null,
    Action? OnAfterExtract = null);

public enum ExtractorWorkerFailureStage
{
    ValidateArchiveSupport,
    ExtractArchive
}

/// <summary>
/// 추출 워커가 큐 처리 중 실패했을 때 extractor, 경로, 시도 횟수 문맥을 보존합니다.
/// </summary>
public sealed class ExtractorWorkerException : InvalidOperationException
{
    public string ExtractorName { get; }
    public string ArchivePath { get; }
    public string DestinationPath { get; }
    public ExtractorWorkerFailureStage Stage { get; }
    public int Attempt { get; }
    public int MaxAttempts { get; }

    public ExtractorWorkerException(
        string message,
        string extractorName,
        string archivePath,
        string destinationPath,
        ExtractorWorkerFailureStage stage,
        Exception? innerException = null,
        int attempt = 0,
        int maxAttempts = 0)
        : base(message, innerException)
    {
        ExtractorName = extractorName;
        ArchivePath = archivePath;
        DestinationPath = destinationPath;
        Stage = stage;
        Attempt = attempt;
        MaxAttempts = maxAttempts;
    }
}

public sealed class ExtractorWorker
{
    private readonly IExtractor _extractor;
    private readonly Channel<ExtractJob> _channel;
    private readonly Task _workerTask;
    int _maxExtractAttempts;
    private readonly CancellationToken _ct;

    public Task Completion => _workerTask;

    public ExtractorWorker(IExtractor extractor, int maxExtractAttempts = 3, CancellationToken ct = default)
    {
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        ArgumentOutOfRangeException.ThrowIfLessThan(maxExtractAttempts, 1);

        _channel = Channel.CreateBounded<ExtractJob>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });
        _maxExtractAttempts = maxExtractAttempts;
        _ct = ct;

        _workerTask = RunAsync();
    }

    public ValueTask EnqueueAsync(ExtractJob job)
    {
        return _channel.Writer.WriteAsync(job, _ct);
    }

    public void Complete()
    {
        _channel.Writer.Complete();
    }

    private async Task RunAsync()
    {
        await foreach (ExtractJob job in _channel.Reader.ReadAllAsync(_ct))
        {
            ExtractorSupportProbeResult? supportProbeResult = (_extractor as IExtractorSupportProbe)?.ProbeSupport(job.ArchivePath);
            if (!(supportProbeResult?.CanHandle ?? _extractor.CanHandle(job.ArchivePath)))
            {
                string reason = supportProbeResult?.Reason is { Length: > 0 }
                    ? supportProbeResult.Reason
                    : $"Extractor '{_extractor.Name}' cannot handle archive '{job.ArchivePath}'.";
                Exception innerException = supportProbeResult?.Exception
                    ?? new NotSupportedException(reason);

                throw new ExtractorWorkerException(
                    reason,
                    _extractor.Name,
                    job.ArchivePath,
                    job.DestinationPath,
                    ExtractorWorkerFailureStage.ValidateArchiveSupport,
                    innerException);
            }

            Exception? lastException = null;

            job.OnBeforeExtract?.Invoke();

            for (int attempt = 1; attempt <= _maxExtractAttempts; ++attempt)
            {
                _ct.ThrowIfCancellationRequested();

                try
                {
                    await _extractor.ExtractAsync(
                        job.ArchivePath,
                        job.DestinationPath,
                        job.Progress,
                        _ct);

                    lastException = null;
                    job.OnAfterExtract?.Invoke();
                    break;
                }
                catch (OperationCanceledException) when (_ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    Debug.WriteLine(
                        $"[ExtractorWorker] Extract failed. " +
                        $"attempt={attempt}/{_maxExtractAttempts}, " +
                        $"archive={job.ArchivePath}, " +
                        $"dest={job.DestinationPath}, " +
                        $"error={ex}");

                    if (attempt == _maxExtractAttempts)
                        break;
                }
            }

            if (lastException is not null)
            {
                throw new ExtractorWorkerException(
                    $"Extraction failed after {_maxExtractAttempts} attempts. ArchivePath='{job.ArchivePath}'",
                    _extractor.Name,
                    job.ArchivePath,
                    job.DestinationPath,
                    ExtractorWorkerFailureStage.ExtractArchive,
                    lastException,
                    attempt: _maxExtractAttempts,
                    maxAttempts: _maxExtractAttempts);
            }
        }
    }
}


public sealed class PatchManager
{
    public enum VersionInfoFailureStage
    {
        DownloadVersionInfo,
        ParseVersionInfo
    }

    public enum PatchFailureStage
    {
        ResolveLatestVersion,
        ResetTempDirectory,
        PrepareTempDirectory,
        BuildPatchItems,
        DownloadPatchItem,
        ExtractPatchItem,
        ApplyMultiClientPatch,
        CleanupTempDirectory
    }

    /// <summary>
    /// 패치 실행 중 실패한 단계와 대상을 함께 보존합니다.
    /// </summary>
    public sealed class PatchOperationException : InvalidOperationException
    {
        public GameServer Server { get; }
        public PatchFailureStage Stage { get; }
        public int? Version { get; }
        public string? TargetPath { get; }
        public string? FileName { get; }

        public PatchOperationException(
            string message,
            GameServer server,
            PatchFailureStage stage,
            Exception innerException,
            int? version = null,
            string? targetPath = null,
            string? fileName = null)
            : base(message, innerException)
        {
            Server = server;
            Stage = stage;
            Version = version;
            TargetPath = targetPath;
            FileName = fileName;
        }
    }

    /// <summary>
    /// 개별 VersionInfo 조회/파싱 실패 시 버전과 URL 문맥을 함께 보존합니다.
    /// </summary>
    public sealed class VersionInfoException : InvalidOperationException
    {
        public GameServer Server { get; }
        public int Version { get; }
        public string VersionInfoUrl { get; }
        public VersionInfoFailureStage Stage { get; }

        public VersionInfoException(
            string message,
            GameServer server,
            int version,
            string versionInfoUrl,
            VersionInfoFailureStage stage,
            Exception innerException)
            : base(message, innerException)
        {
            Server = server;
            Version = version;
            VersionInfoUrl = versionInfoUrl;
            Stage = stage;
        }
    }

    private sealed class VersionInfo(int Version)
    {
        public int Version { get; } = Version;
        public IList<VersionInfoRow> Rows { get; } = [];
    }

    private sealed record VersionInfoRow(
        int UNUSED_Index,
        string ZipFileName,
        string FileNameAfterUnZip,
        string RelativeDir,
        string UNUSED_ZipFileCheckSum,
        string UNUSED_OriginFileCheckSum,
        string ZipCRC,
        int UNUSED_FileOption);

    private readonly Downloader _downloader = new(HttpClientProvider.Http);
    private readonly IExtractor _extractor = new ZipFileExtractor();

    public async Task RunAsync(
    GameServer targetServer,
    int currentClientVersion,
    string originClientPath,
    PatchRunOptions options,
    IProgress<PatchProgress>? progress,
    CancellationToken ct)
    {
        Debug.WriteLine(
            $"RunAsync\n\t" +
            $"targetServer: {targetServer}\n\t" +
            $"currentVersion: {currentClientVersion}\n\t" +
            $"clientPath: {originClientPath}");

        int latestClientVersion;
        try
        {
            latestClientVersion = await GetLatestServerVersionAsync(targetServer, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PatchOperationException(
                $"Failed to resolve the latest patch version for server '{targetServer}'.",
                targetServer,
                PatchFailureStage.ResolveLatestVersion,
                ex);
        }

        string tempRoot = GetPatchTempRoot(originClientPath, currentClientVersion, latestClientVersion);

        if (options.ArchiveReuseMode == PatchArchiveReuseMode.RestartFromScratch)
        {
            Debug.WriteLine($"[PatchManager] RestartFromScratch. Delete temp root: {tempRoot}");

            DirectoryDeleteResult deleteResult = await TryDeleteDirectoryIfExistsAsync(tempRoot, ct).ConfigureAwait(false);
            if (!deleteResult.Success)
            {
                throw new PatchOperationException(
                    $"Failed to delete existing patch temp directory '{tempRoot}'.",
                    targetServer,
                    PatchFailureStage.ResetTempDirectory,
                    deleteResult.Exception ?? new IOException($"Failed to delete existing patch temp directory: {tempRoot}"),
                    targetPath: tempRoot);
            }
        }

        try
        {
            Directory.CreateDirectory(tempRoot);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PatchOperationException(
                $"Failed to prepare patch temp directory '{tempRoot}'.",
                targetServer,
                PatchFailureStage.PrepareTempDirectory,
                ex,
                targetPath: tempRoot);
        }

        List<PatchItem> items;
        try
        {
            items = await BuildPatchItemsAsync(
                targetServer,
                currentClientVersion,
                latestClientVersion,
                originClientPath,
                tempRoot,
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PatchOperationException(
                $"Failed to build patch item list for server '{targetServer}'.",
                targetServer,
                PatchFailureStage.BuildPatchItems,
                ex,
                targetPath: tempRoot);
        }

        Debug.WriteLine("패치 파일 목록(압축 푸는 순서대로 정렬):");
        foreach (PatchItem item in items)
        {
            Debug.WriteLine(
                $"\t[{item.FileName}]\n\t" +
                $"{item.DownloadUrl}\n\t" +
                $"{item.DownloadDestPath}\n\t" +
                $"{item.ExtractDestPath}\n");
        }

        PatchProgressState state = new()
        {
            // TotalCount = 다운로드해야할파일 갯수 + 압축해제해야할파일 갯수로 정의
            // 다운로드 하는 파일이 모두 압축파일이라고 가정하기 때문에 * 2
            TotalCount = items.Count * 2
        };

        void ReportProgressSnapshot()
        {
            if (progress is null)
                return;

            progress.Report(state.Snapshot());
        }

        Progress<DownloadProgress>? downloadProgress = null;
        if (progress is not null)
        {
            downloadProgress = new Progress<DownloadProgress>(p =>
            {
                double? percent = null;
                if (p.TotalBytes is long totalBytes && totalBytes > 0)
                    percent = (double)p.BytesReceived / totalBytes * 100.0;

                state.Update(s =>
                {
                    s.DownloadingPercent = percent;
                });

                ReportProgressSnapshot();
            });
        }

        Progress<ExtractionProgress>? extractionProgress = null;
        if (progress is not null)
        {
            extractionProgress = new Progress<ExtractionProgress>(p =>
            {
                state.Update(s =>
                {
                    s.ExtractingPercent = p.Percentage;
                });

                ReportProgressSnapshot();
            });
        }

        DownloadOptions downloadOptions = CreateDownloadOptions(options);

        bool patchSucceeded = false;

        try
        {
            try
            {
                await ApplyPatchAsync(
                    targetServer,
                    items,
                    downloadOptions: downloadOptions,
                    onBeforeDownload: item =>
                    {
                        state.Update(s =>
                        {
                            s.DownloadingFileName = item.FileName;
                            s.DownloadingPercent = 0;
                        });

                        ReportProgressSnapshot();
                    },
                    onAfterDownload: item =>
                    {
                        state.Update(s =>
                        {
                            s.DownloadedCount++;
                            s.DownloadingFileName = item.FileName;
                            s.DownloadingPercent = 100;
                        });

                        ReportProgressSnapshot();
                    },
                    onBeforeExtract: item =>
                    {
                        state.Update(s =>
                        {
                            s.ExtractingFileName = item.FileName;
                            s.ExtractingPercent = 0;
                        });

                        ReportProgressSnapshot();
                    },
                    onAfterExtract: item =>
                    {
                        state.Update(s =>
                        {
                            s.ExtractedCount++;
                            s.ExtractingFileName = item.FileName;
                            s.ExtractingPercent = 100;
                        });

                        ReportProgressSnapshot();
                    },
                    downloadProgress: downloadProgress,
                    extractionProgress: extractionProgress,
                    ct: ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and PatchOperationException)
            {
                throw new PatchOperationException(
                    $"Failed to download or extract patch items for server '{targetServer}'.",
                    targetServer,
                    PatchFailureStage.DownloadPatchItem,
                    ex,
                    targetPath: tempRoot);
            }

            try
            {
                ApplyMultiClientPatchIfNeeded(targetServer, originClientPath, latestClientVersion, options);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new PatchOperationException(
                    $"Failed to apply multi-client patch updates for server '{targetServer}'.",
                    targetServer,
                    PatchFailureStage.ApplyMultiClientPatch,
                    ex,
                    version: latestClientVersion,
                    targetPath: originClientPath);
            }

            patchSucceeded = true;
        }
        finally
        {
            // 성공적으로 전체 패치가 끝났을 때만 temp 루트를 정리한다.
            // 실패/중단 시에는 archive/temp/meta를 그대로 남겨 다음 실행에서
            // 이어받기 / 새로 받기 선택이 가능해야 한다.
            if (patchSucceeded && options.DeleteTempFilesAfterPatch)
            {
                DirectoryDeleteResult deleteResult = await TryDeleteDirectoryIfExistsAsync(tempRoot, ct).ConfigureAwait(false);
                if (!deleteResult.Success)
                {
                    Debug.WriteLine($"[PatchManager] Temp root cleanup failed after successful patch: {tempRoot}, Error: {deleteResult.Exception}");
                }
            }
        }
    }

    public async Task ApplyPatchAsync(
        GameServer targetServer,
        IReadOnlyList<PatchItem> items,
        DownloadOptions downloadOptions,
        Action<PatchItem>? onBeforeDownload = null,
        Action<PatchItem>? onAfterDownload = null,
        Action<PatchItem>? onBeforeExtract = null,
        Action<PatchItem>? onAfterExtract = null,
        IProgress<DownloadProgress>? downloadProgress = null,
        IProgress<ExtractionProgress>? extractionProgress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(downloadOptions);

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        ExtractorWorker worker = new(_extractor, 3, cts.Token);

        try
        {
            foreach (PatchItem item in items)
            {
                onBeforeDownload?.Invoke(item);

                try
                {
                    await _downloader.DownloadFileAsync(
                        item.DownloadUrl,
                        item.DownloadDestPath,
                        downloadOptions,
                        downloadProgress,
                        cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new PatchOperationException(
                        $"Failed to download patch item '{item.FileName}'.",
                        targetServer,
                        PatchFailureStage.DownloadPatchItem,
                        ex,
                        targetPath: item.DownloadDestPath,
                        fileName: item.FileName);
                }

                onAfterDownload?.Invoke(item);

                try
                {
                    await worker.EnqueueAsync(
                        new ExtractJob(
                            item.DownloadDestPath,
                            item.ExtractDestPath,
                            extractionProgress,
                            OnBeforeExtract: () => onBeforeExtract?.Invoke(item),
                            OnAfterExtract: () => onAfterExtract?.Invoke(item))).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new PatchOperationException(
                        $"Failed to queue extraction for patch item '{item.FileName}'.",
                        targetServer,
                        PatchFailureStage.ExtractPatchItem,
                        ex,
                        targetPath: item.ExtractDestPath,
                        fileName: item.FileName);
                }
            }

            worker.Complete();
            try
            {
                await worker.Completion.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new PatchOperationException(
                    "Failed while extracting downloaded patch items.",
                    targetServer,
                    PatchFailureStage.ExtractPatchItem,
                    ex);
            }
        }
        catch
        {
            cts.Cancel();
            worker.Complete();
            throw;
        }
    }

    private async Task<List<PatchItem>> BuildPatchItemsAsync(
    GameServer targetServer,
    int currentClientVersion,
    int latestClientVersion,
    string originClientPath,
    string tempRoot,
    CancellationToken ct)
    {
        HashSet<string> added = [];
        List<PatchItem> items = [];

        // 구버전부터 해도 되는 이유:
        // 동일한 파일이 여러 버전 구간에 등장하더라도 downloadUrl은 항상 최신 archive 경로를 가리킨다.
        // 압축 해제는 구버전부터 순서대로 수행되어야 하므로 이 순서가 자연스럽다.
        for (int version = currentClientVersion + 1; version <= latestClientVersion; ++version)
        {
            ct.ThrowIfCancellationRequested();

            VersionInfo? versionInfo = await DownloadVersionInfo(targetServer, version, ct);
            if (versionInfo is null)
                continue;

            foreach (VersionInfoRow row in versionInfo.Rows)
            {
                string relativeDirForUrl = NormalizeRelativeDirForUrl(row.RelativeDir);
                string relativeDirForPath = NormalizeRelativeDirForPath(row.RelativeDir);

                string relativeArchiveUrlPath = string.IsNullOrEmpty(relativeDirForUrl)
                    ? row.ZipFileName
                    : $"{relativeDirForUrl}/{row.ZipFileName}";

                string downloadUrl = GameServerHelper.GetPatchFileUrl(targetServer, relativeArchiveUrlPath);

                string downloadDestPath = string.IsNullOrEmpty(relativeDirForPath)
                    ? Path.Combine(tempRoot, row.ZipFileName)
                    : Path.Combine(tempRoot, relativeDirForPath, row.ZipFileName);

                string extractDestPath = string.IsNullOrEmpty(relativeDirForPath)
                    ? originClientPath
                    : Path.Combine(originClientPath, relativeDirForPath);

                PatchItem item = new(
                    row.FileNameAfterUnZip,
                    new Uri(downloadUrl),
                    downloadDestPath,
                    extractDestPath);

                bool shouldAdd = added.Add(downloadUrl);
                if (shouldAdd)
                    items.Add(item);
            }
        }

        return items;
    }

    private static async Task<VersionInfo?> DownloadVersionInfo(GameServer server, int version, CancellationToken ct)
    {
        string versionInfoUrlText = GameServerHelper.GetVersionInfoUrl(server, version);
        Uri versionInfoUrl = new(versionInfoUrlText);

        HttpResponseMessage response;
        try
        {
            response = await HttpClientProvider.Http.GetAsync(versionInfoUrl, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new VersionInfoException(
                $"Failed to download version info. server='{server}', version={version}, url='{versionInfoUrlText}'",
                server,
                version,
                versionInfoUrlText,
                VersionInfoFailureStage.DownloadVersionInfo,
                ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            try
            {
                response.EnsureSuccessStatusCode();
                string text = await response.Content.ReadAsStringAsync(ct);
                VersionInfo ret = MakeVersionInfo(version, text);
                if (ret.Rows.Count == 0)
                {
                    throw new InvalidDataException(
                        $"Version info does not contain any patch rows. server='{server}', version={version}, url='{versionInfoUrlText}'");
                }

                return ret;
            }
            catch (Exception ex) when (ex is not OperationCanceledException and VersionInfoException)
            {
                throw new VersionInfoException(
                    $"Failed to parse version info. server='{server}', version={version}, url='{versionInfoUrlText}'",
                    server,
                    version,
                    versionInfoUrlText,
                    VersionInfoFailureStage.ParseVersionInfo,
                    ex);
            }
        }
    }

    private static VersionInfo MakeVersionInfo(int version, string sourceText)
    {
        VersionInfo info = new(version);

        foreach (var line in sourceText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith(';')) continue;
            if (line.StartsWith('#')) continue;
            int Index = 0;
            string ZipFileName = string.Empty;
            string FileNameAfterUnZip = string.Empty;
            string RelativeDir = string.Empty;
            string ZipFileCheckSum = string.Empty;
            string OriginFileCheckSum = string.Empty;
            string ZipCRC = string.Empty;
            int FileOption = 0;
            var cols = line.Split('\t');
            try
            {
                for (int i = 0; i < cols.Length; ++i)
                {
                    switch (i)
                    {
                        case 0:
                            Index = int.Parse(cols[i]);
                            break;
                        case 1:
                            ZipFileName = cols[i];
                            break;
                        case 2:
                            FileNameAfterUnZip = cols[i];
                            break;
                        case 3:
                            RelativeDir = cols[i];
                            break;
                        case 4:
                            ZipFileCheckSum = cols[i];
                            break;
                        case 5:
                            OriginFileCheckSum = cols[i];
                            break;
                        case 6:
                            ZipCRC = cols[i];
                            break;
                        case 7:
                            FileOption = int.Parse(cols[i]);
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Failed to parse version info. version={version}, line='{line}'", ex);
            }

            VersionInfoRow row = new(Index, ZipFileName, FileNameAfterUnZip, RelativeDir, ZipFileCheckSum, OriginFileCheckSum, ZipCRC, FileOption);
            info.Rows.Add(row);
        }

        return info;
    }



    /// <summary>
    /// 설치 경로의 Online/vsn.dat을 읽어 현재 클라이언트 버전을 상세 결과와 함께 반환합니다.
    /// </summary>
    public static ClientVersionReadResult TryGetCurrentClientVersion(string clientPath)
    {
        string vsnPath;
        try
        {
            vsnPath = GetVsnPath(clientPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new ClientVersionReadResult(
                false,
                null,
                clientPath?.Trim() ?? string.Empty,
                ClientVersionReadFailureStage.ResolveVsnPath,
                ex);
        }

        if (!File.Exists(vsnPath))
            return new ClientVersionReadResult(
                false,
                null,
                vsnPath,
                ClientVersionReadFailureStage.OpenVsnFile,
                new FileNotFoundException($"vsn.dat file was not found. path={vsnPath}", vsnPath));

        try
        {
            using var stream = File.OpenRead(vsnPath);

            try
            {
                int version = DecodeVersionFromVsn(stream);
                return new ClientVersionReadResult(true, version, vsnPath, null, null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new ClientVersionReadResult(
                    false,
                    null,
                    vsnPath,
                    ClientVersionReadFailureStage.DecodeVsnContents,
                    ex);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ClientVersionReadResult(
                false,
                null,
                vsnPath,
                ClientVersionReadFailureStage.OpenVsnFile,
                ex);
        }
    }

    /// <summary>
    /// 설치 경로의 Online/vsn.dat을 읽어 현재 클라이언트 버전을 반환합니다.
    /// 파일이 없거나 읽기에 실패하면 null을 반환합니다.
    /// </summary>
    public static int? GetCurrentClientVersion(string clientPath)
        => TryGetCurrentClientVersion(clientPath).Version;

    // 거상 패치 흐름
    // 1. Readme가 가장 먼저 올라온다
    // 2. VersionInfo 파일과 vsn.dat.gsz 파일이 CDN에 업로드된다

    /// <summary>
    /// <para>서버에 패치 파일이 올라온 최신 버전을 확인합니다</para>
    /// <para>만약, 패치 예정 최신 버전이 존재하더라도 서버에 파일이 없다면 무시합니다</para>
    /// </summary>
    public static async Task<int> GetLatestServerVersionAsync(GameServer server, CancellationToken ct = default)
    {
        List<int> latestVersionList;
        try
        {
            // Readme 에서 가장 최근 버전 5개를 추출한 뒤 내림차순 정렬
            latestVersionList = await PatchReadmeHelper.GetLatestVersionList(server, 5, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new LatestVersionResolutionException(
                $"Failed to resolve latest patch versions from readme for server '{server}'.",
                server,
                LatestVersionResolutionFailureStage.ReadmeLookup,
                ex);
        }

        latestVersionList.Sort((x, y) => y.CompareTo(x));

        if (latestVersionList.Count == 0)
        {
            throw new LatestVersionResolutionException(
                $"Patch readme did not provide any version candidates for server '{server}'.",
                server,
                LatestVersionResolutionFailureStage.NoPublishedVersionInfo,
                new InvalidDataException("Patch readme did not contain any version candidates."));
        }

        // 최신 버전부터 차례대로 순회하여 VersionInfo이 존재하는 버전이 실질적인 최신 버전
        foreach (var version in latestVersionList)
        {
            string versionInfoUrl = GameServerHelper.GetVersionInfoUrl(server, version);
            try
            {
                Uri versionInfoUri = new(versionInfoUrl);
                using var response = await HttpClientProvider.Http.GetAsync(versionInfoUri, HttpCompletionOption.ResponseHeadersRead, ct);
                if (response.StatusCode == HttpStatusCode.OK)
                    return version;

                if (response.StatusCode == HttpStatusCode.NotFound)
                    continue;

                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new LatestVersionResolutionException(
                    $"Failed to probe patch version info. server='{server}', version={version}, url='{versionInfoUrl}'",
                    server,
                    LatestVersionResolutionFailureStage.ProbeVersionInfo,
                    ex,
                    version,
                    versionInfoUrl);
            }
        }

        throw new LatestVersionResolutionException(
            $"No published version info was found for the latest readme candidates on server '{server}'.",
            server,
            LatestVersionResolutionFailureStage.NoPublishedVersionInfo,
            new FileNotFoundException(
                $"VersionInfo was not found for any candidate version from the patch readme. Candidates={string.Join(", ", latestVersionList)}"));
    }

    public static int DecodeVersionFromVsn(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < sizeof(int))
            throw new InvalidDataException($"vsn.dat size is too small. size={bytes.Length}");

        using var ms = new MemoryStream(bytes.ToArray(), writable: false);
        return DecodeVersionFromVsn(ms);
    }

    public static int DecodeVersionFromVsn(Stream stream)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));

        using var br = new BinaryReader(stream, Encoding.Default, leaveOpen: true);
        try
        {
            int raw = br.ReadInt32(); // BinaryReader 기본 동작: little-endian
            return -(raw + 1); // 1의 보수
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException("vsn.dat size is too small. need at least 4 bytes.", ex);
        }
    }

    /// <summary>
    /// 메인 클라이언트 패치가 끝난 뒤 필요 시 복제 클라이언트를 다시 HardCopy/심볼 재구성합니다.
    /// </summary>
    private static void ApplyMultiClientPatchIfNeeded(GameServer targetServer, string originClientPath, int latestClientVersion, PatchRunOptions options)
    {
        if (!options.ApplyMultiClientPatch)
            return;

        (ClientSettings clientSettings, AppDataManager.AppDataOperationResult loadResult) =
            AppDataManager.TryLoadServerClientSettings(targetServer);
        if (!loadResult.Success)
        {
            throw loadResult.Exception is null
                ? new IOException(
                    $"다클라 패치 설정을 불러오지 못했습니다. Target={loadResult.Target}, Operation={loadResult.Operation}, ErrorKind={loadResult.ErrorKind}")
                : new IOException(
                    $"다클라 패치 설정을 불러오지 못했습니다. Target={loadResult.Target}, Operation={loadResult.Operation}, ErrorKind={loadResult.ErrorKind}",
                    loadResult.Exception);
        }

        if (!clientSettings.UseMultiClient)
            return;

        CreateSymbolMultiClientArgs args = new()
        {
            InstallPath = originClientPath,
            DestPath2 = clientSettings.UseClient2 ? clientSettings.Client2Path : string.Empty,
            DestPath3 = clientSettings.UseClient3 ? clientSettings.Client3Path : string.Empty,
            OverwriteConfig = clientSettings.OverwriteMultiClientConfig,
            LayoutPolicy = latestClientVersion >= GameClientHelper.MultiClientLayoutBoundaryVersion
                ? GameClientHelper.MultiClientLayoutPolicy.V34100OrLater
                : GameClientHelper.MultiClientLayoutPolicy.Legacy
        };

        if (string.IsNullOrWhiteSpace(args.DestPath2) && string.IsNullOrWhiteSpace(args.DestPath3))
            return;

        CreateSymbolMultiClientResult result = GameClientHelper.TryCreateSymbolMultiClient(args);
        if (!result.Success)
        {
            throw result.Exception is null
                ? new IOException($"다클라 패치 적용 실패: {result.Reason}")
                : new IOException($"다클라 패치 적용 실패: {result.Reason}", result.Exception);
        }
    }

    /// <summary>
    /// 정수 버전을 거상 클라이언트의 vsn.dat 바이너리 형식으로 인코딩합니다.
    /// </summary>
    public static byte[] EncodeVersionToVsn(int version)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(version);

        int raw = checked(-(version + 1));
        return BitConverter.GetBytes(raw);
    }

    /// <summary>
    /// 설치 경로의 Online/vsn.dat를 지정한 버전 값으로 다시 기록합니다.
    /// </summary>
    public static ClientVersionWriteResult TryWriteClientVersion(string clientPath, int version)
    {
        string vsnPath;
        try
        {
            vsnPath = GetVsnPath(clientPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new ClientVersionWriteResult(
                false,
                clientPath?.Trim() ?? string.Empty,
                ClientVersionWriteFailureStage.ResolveVsnPath,
                ex);
        }

        string? vsnDirectory = Path.GetDirectoryName(vsnPath);
        if (string.IsNullOrWhiteSpace(vsnDirectory))
        {
            return new ClientVersionWriteResult(
                false,
                vsnPath,
                ClientVersionWriteFailureStage.ResolveVsnDirectory,
                new InvalidOperationException($"Failed to resolve vsn.dat directory. path={vsnPath}"));
        }

        try
        {
            Directory.CreateDirectory(vsnDirectory);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ClientVersionWriteResult(
                false,
                vsnPath,
                ClientVersionWriteFailureStage.CreateVsnDirectory,
                ex);
        }

        try
        {
            File.WriteAllBytes(vsnPath, EncodeVersionToVsn(version));
            return new ClientVersionWriteResult(true, vsnPath, null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ClientVersionWriteResult(
                false,
                vsnPath,
                ClientVersionWriteFailureStage.WriteVsnFile,
                ex);
        }
    }

    /// <summary>
    /// 설치 경로의 Online/vsn.dat를 지정한 버전 값으로 다시 기록합니다.
    /// </summary>
    public static void WriteClientVersion(string clientPath, int version)
    {
        ClientVersionWriteResult result = TryWriteClientVersion(clientPath, version);
        if (!result.Success)
            throw result.Exception ?? new IOException($"Failed to write vsn.dat. path={result.VsnPath}");
    }

    public static string GetPatchTempRoot(
    string clientPath,
    int currentVersion,
    int targetVersion)
    {
        return Path.Combine(
            clientPath,
            "PatchTemp",
            $"{currentVersion}-{targetVersion}");
    }

    /// <summary>
    /// 클라이언트 루트 기준 Online/vsn.dat 경로를 정규화해 반환합니다.
    /// </summary>
    private static string GetVsnPath(string clientPath)
    {
        if (string.IsNullOrWhiteSpace(clientPath))
            throw new ArgumentException("Client path is required.", nameof(clientPath));

        string normalizedClientPath = Path.GetFullPath(clientPath.Trim());
        return Path.Combine(normalizedClientPath, "Online", "vsn.dat");
    }

    // URL 경로로 사용하기 위해 주어진 상대 경로를 정규화 (\ -> /)
    private static string NormalizeRelativeDirForUrl(string relativeDir)
    {
        if (string.IsNullOrWhiteSpace(relativeDir))
            return string.Empty;

        return relativeDir.Replace('\\', '/').TrimStart('/');
    }

    // 윈도우 경로로 사용하기 위해 주어진 상대 경로를 정규화
    private static string NormalizeRelativeDirForPath(string relativeDir)
    {
        if (string.IsNullOrWhiteSpace(relativeDir))
            return string.Empty;

        string trimmed = relativeDir.TrimStart('\\', '/');

        return trimmed.Replace('/', Path.DirectorySeparatorChar);
    }

    private static DownloadOptions CreateDownloadOptions(PatchRunOptions options)
    {
        DownloadExistingArtifactMode existingArtifactMode =
            options.ArchiveReuseMode == PatchArchiveReuseMode.RestartFromScratch
                ? DownloadExistingArtifactMode.RestartFromScratch
                : DownloadExistingArtifactMode.ResumeIfPossible;

        return new DownloadOptions(
            Overwrite: true,
            MaxRetries: 8,
            BufferSize: 1024 * 1024,
            ExistingArtifactMode: existingArtifactMode);
    }

    private static async Task<DirectoryDeleteResult> TryDeleteDirectoryIfExistsAsync(
    string path,
    CancellationToken ct,
    int maxAttempts = 5)
    {
        if (!Directory.Exists(path))
            return new DirectoryDeleteResult(true, path, null, null);

        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; ++attempt)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);

                if (!Directory.Exists(path))
                    return new DirectoryDeleteResult(true, path, null, null);

                lastException = new IOException($"Directory still exists after delete attempt: {path}");
            }
            catch (IOException ex)
            {
                lastException = ex;
            }
            catch (UnauthorizedAccessException ex)
            {
                lastException = ex;
            }

            Debug.WriteLine(
                $"[PatchManager] Delete temp root failed. " +
                $"attempt={attempt}/{maxAttempts}, " +
                $"path={path}, " +
                $"error={lastException?.Message}");

            if (attempt < maxAttempts)
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct).ConfigureAwait(false);
        }

        Debug.WriteLine(
            $"[PatchManager] Delete temp root gave up. " +
            $"path={path}, " +
            $"error={lastException}");

        return new DirectoryDeleteResult(
            false,
            path,
            DirectoryDeleteFailureStage.DeleteDirectory,
            lastException ?? new IOException($"Failed to delete directory: {path}"));
    }

    ///// <summary>
    ///// FullClient를 내려받아 지정한 설치 경로로 압축 해제합니다.
    ///// </summary>
    //public static string NormalizeFullClientInstallRoot(string installRoot)
    //{
    //    if (string.IsNullOrWhiteSpace(installRoot)) throw new ArgumentException("installRoot is required.", nameof(installRoot));

    //    string trimmedRoot = Path.GetFullPath(installRoot.Trim())
    //        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    //    return string.Equals(Path.GetFileName(trimmedRoot), "Gersang", StringComparison.OrdinalIgnoreCase)
    //        ? trimmedRoot
    //        : Path.Combine(trimmedRoot, "Gersang");
    //}

    ///// <summary>
    ///// FullClient를 내려받아 지정한 설치 경로(\Gersang 하위)로 압축 해제합니다.
    ///// </summary>
    //public static async Task InstallFullClientAsync(
    //    string installRoot,
    //    GameServer server,
    //    IProgress<DownloadProgress>? downloadProgress = null,
    //    IProgress<ExtractionProgress>? extractionProgress = null,
    //    bool skipDownloadIfArchiveExists = true,
    //    CancellationToken ct = default)
    //{
    //    string actualInstallRoot = NormalizeFullClientInstallRoot(installRoot);

    //    Directory.CreateDirectory(actualInstallRoot);

    //    string archivePath = Path.Combine(actualInstallRoot, "Gersang_Install.7z");
    //    bool archiveExists = File.Exists(archivePath);

    //    using var http = new HttpClient(new HttpClientHandler
    //    {
    //        AutomaticDecompression = DecompressionMethods.None
    //    })
    //    {
    //        Timeout = TimeSpan.FromHours(2)
    //    };

    //    var downloader = new Downloader(http);

    //    try
    //    {
    //        if (archiveExists && skipDownloadIfArchiveExists)
    //        {
    //            Debug.WriteLine($"[InstallFullClient] Archive already exists. Skip download and extract directly: {archivePath}");
    //        }
    //        else
    //        {
    //            await downloader.DownloadFileAsync(
    //                new Uri(GameServerHelper.GetFullClientUrl(server)),
    //                archivePath,
    //                new DownloadOptions(Overwrite: !skipDownloadIfArchiveExists),
    //                downloadProgress: downloadProgress,
    //                ct: ct).ConfigureAwait(false);

    //            Debug.WriteLine($"[InstallFullClient] Download complete: {archivePath}");
    //        }

    //        await Extractor.ExtractAsync(archivePath, actualInstallRoot, downloadProgress: extractionProgress, ct: ct).ConfigureAwait(false);
    //    }
    //    finally
    //    {
    //        try
    //        {
    //            if (File.Exists(archivePath))
    //                File.Delete(archivePath);
    //        }
    //        catch
    //        {
    //            // best-effort
    //        }
    //    }
    //}
}
