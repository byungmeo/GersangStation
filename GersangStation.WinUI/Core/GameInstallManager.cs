using Core.Download;
using Core.Extract;
using Core.Models;

namespace Core;

/// <summary>
/// 게임 전체 클라이언트 설치 파일 다운로드와 압축 해제를 관리합니다.
/// </summary>
public sealed class GameInstallManager
{
    public enum GameInstallFailureStage
    {
        PrepareInstallDirectory,
        ResolveArchivePath,
        DownloadArchive,
        ValidateArchiveSupport,
        ExtractArchive,
        DeleteArtifacts
    }

    public enum GameInstallHelperFailureStage
    {
        ResolveArchivePath,
        DeleteArchive,
        DeleteTempDownload,
        DeleteMetadata
    }

    /// <summary>
    /// 전체 클라이언트 설치 중 실패한 단계와 경로를 함께 보존합니다.
    /// </summary>
    public sealed class GameInstallOperationException : InvalidOperationException
    {
        public GameServer Server { get; }
        public GameInstallFailureStage Stage { get; }
        public string InstallPath { get; }
        public string ArchivePath { get; }

        public GameInstallOperationException(
            string message,
            GameServer server,
            GameInstallFailureStage stage,
            string installPath,
            string archivePath,
            Exception innerException)
            : base(message, innerException)
        {
            Server = server;
            Stage = stage;
            InstallPath = installPath;
            ArchivePath = archivePath;
        }
    }

    /// <summary>
    /// 설치 아카이브 경로 계산 결과를 실패 문맥과 함께 반환합니다.
    /// </summary>
    public sealed record GameInstallArchivePathResult(
        bool Success,
        string ArchivePath,
        GameInstallHelperFailureStage? FailureStage,
        Exception? Exception);

    /// <summary>
    /// 설치 아티팩트 정리 결과를 실패 문맥과 함께 반환합니다.
    /// </summary>
    public sealed record GameInstallArtifactCleanupResult(
        bool Success,
        string ArchivePath,
        GameInstallHelperFailureStage? FailureStage,
        Exception? Exception);

    private readonly Downloader _downloader = new(HttpClientProvider.Http);
    private readonly NativeSevenZipExtractor _extractor = new();

    /// <summary>
    /// 선택한 서버의 전체 클라이언트를 지정 경로에 설치합니다.
    /// </summary>
    public async Task RunAsync(
        GameServer targetServer,
        string installPath,
        DownloadExistingArtifactMode archiveReuseMode,
        IProgress<GameInstallProgress>? progress,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(installPath))
            throw new ArgumentException("installPath is required.", nameof(installPath));

        string normalizedInstallPath;
        try
        {
            normalizedInstallPath = Path.GetFullPath(installPath.Trim());
            Directory.CreateDirectory(normalizedInstallPath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new GameInstallOperationException(
                $"Failed to prepare install directory '{installPath}'.",
                targetServer,
                GameInstallFailureStage.PrepareInstallDirectory,
                installPath,
                archivePath: string.Empty,
                ex);
        }

        GameInstallArchivePathResult archivePathResult = TryGetArchivePath(targetServer, normalizedInstallPath);
        if (!archivePathResult.Success)
        {
            throw new GameInstallOperationException(
                $"Failed to resolve the full client archive path for server '{targetServer}'.",
                targetServer,
                GameInstallFailureStage.ResolveArchivePath,
                normalizedInstallPath,
                archivePathResult.ArchivePath,
                archivePathResult.Exception ?? new IOException("Archive path resolution failed."));
        }

        Uri archiveUrl = new(GameServerHelper.GetFullClientUrl(targetServer));
        string archivePath = archivePathResult.ArchivePath;
        GameInstallProgressState state = new();

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
                    s.DownloadFileName = Path.GetFileName(archivePath);
                    s.DownloadedBytes = p.BytesReceived;
                    s.DownloadTotalBytes = p.TotalBytes;
                    s.DownloadPercent = percent;
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
                    s.ExtractPercent = p.Percentage;
                    s.ExtractedEntries = p.ProcessedEntries;
                    s.ExtractTotalEntries = p.TotalEntries;
                    s.CurrentExtractEntry = p.CurrentArchive;
                });

                ReportProgressSnapshot();
            });
        }

        try
        {
            await _downloader.DownloadFileAsync(
                archiveUrl,
                archivePath,
                new DownloadOptions(
                    Overwrite: true,
                    ExistingArtifactMode: archiveReuseMode),
                downloadProgress,
                ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new GameInstallOperationException(
                $"Failed to download the full client archive for server '{targetServer}'.",
                targetServer,
                GameInstallFailureStage.DownloadArchive,
                normalizedInstallPath,
                archivePath,
                ex);
        }

        state.Update(s =>
        {
            s.DownloadFileName = Path.GetFileName(archivePath);
            s.DownloadPercent = 100;
        });
        ReportProgressSnapshot();

        ExtractorSupportProbeResult? supportProbeResult = (_extractor as IExtractorSupportProbe)?.ProbeSupport(archivePath);
        if (!(supportProbeResult?.CanHandle ?? _extractor.CanHandle(archivePath)))
        {
            Exception innerException = supportProbeResult?.Exception
                ?? new NotSupportedException(supportProbeResult?.Reason ?? $"Extractor '{_extractor.Name}' cannot handle archive '{archivePath}'.");

            throw new GameInstallOperationException(
                $"Failed to validate the downloaded archive for extraction on server '{targetServer}'.",
                targetServer,
                GameInstallFailureStage.ValidateArchiveSupport,
                normalizedInstallPath,
                archivePath,
                innerException);
        }

        try
        {
            await _extractor.ExtractAsync(
                archivePath,
                normalizedInstallPath,
                extractionProgress,
                ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new GameInstallOperationException(
                $"Failed to extract the full client archive for server '{targetServer}'.",
                targetServer,
                GameInstallFailureStage.ExtractArchive,
                normalizedInstallPath,
                archivePath,
                ex);
        }

        state.Update(s =>
        {
            s.ExtractPercent = 100;
        });
        ReportProgressSnapshot();

        try
        {
            GameInstallArtifactCleanupResult cleanupResult = TryDeleteArchiveArtifacts(targetServer, normalizedInstallPath);
            if (!cleanupResult.Success)
            {
                throw cleanupResult.Exception
                    ?? new IOException($"Failed to delete install artifacts. archivePath={cleanupResult.ArchivePath}");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new GameInstallOperationException(
                $"Failed to delete temporary full client artifacts for server '{targetServer}'.",
                targetServer,
                GameInstallFailureStage.DeleteArtifacts,
                normalizedInstallPath,
                archivePath,
                ex);
        }
    }

    /// <summary>
    /// 설치 경로 안에 저장되는 전체 클라이언트 압축 파일 경로를 반환합니다.
    /// </summary>
    public static GameInstallArchivePathResult TryGetArchivePath(GameServer targetServer, string installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath))
            throw new ArgumentException("installPath is required.", nameof(installPath));

        try
        {
            string archivePath = Path.Combine(
                Path.GetFullPath(installPath.Trim()),
                GetArchiveFileName(targetServer));

            return new GameInstallArchivePathResult(true, archivePath, null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new GameInstallArchivePathResult(
                false,
                string.Empty,
                GameInstallHelperFailureStage.ResolveArchivePath,
                ex);
        }
    }

    /// <summary>
    /// 설치 경로 안에 저장되는 전체 클라이언트 압축 파일 경로를 반환합니다.
    /// </summary>
    public static string GetArchivePath(GameServer targetServer, string installPath)
    {
        GameInstallArchivePathResult result = TryGetArchivePath(targetServer, installPath);
        if (!result.Success)
            throw result.Exception ?? new IOException("Failed to resolve archive path.");

        return result.ArchivePath;
    }

    /// <summary>
    /// 이어받기용 설치 압축 파일과 다운로드 부속 파일을 삭제합니다.
    /// </summary>
    public static GameInstallArtifactCleanupResult TryDeleteArchiveArtifacts(GameServer targetServer, string installPath)
    {
        GameInstallArchivePathResult archivePathResult = TryGetArchivePath(targetServer, installPath);
        if (!archivePathResult.Success)
        {
            return new GameInstallArtifactCleanupResult(
                false,
                archivePathResult.ArchivePath,
                archivePathResult.FailureStage,
                archivePathResult.Exception);
        }

        string archivePath = archivePathResult.ArchivePath;

        GameInstallArtifactCleanupResult? deleteFailure = TryDeleteFileIfExists(archivePath, GameInstallHelperFailureStage.DeleteArchive, "archive");
        if (deleteFailure is not null)
            return deleteFailure;

        deleteFailure = TryDeleteFileIfExists($"{archivePath}.gsdownload", GameInstallHelperFailureStage.DeleteTempDownload, "temporary download file");
        if (deleteFailure is not null)
            return deleteFailure;

        deleteFailure = TryDeleteFileIfExists($"{archivePath}.meta", GameInstallHelperFailureStage.DeleteMetadata, "metadata file");
        if (deleteFailure is not null)
            return deleteFailure;

        return new GameInstallArtifactCleanupResult(true, archivePath, null, null);
    }

    /// <summary>
    /// 이어받기용 설치 압축 파일과 다운로드 부속 파일을 삭제합니다.
    /// </summary>
    public static void DeleteArchiveArtifacts(GameServer targetServer, string installPath)
    {
        GameInstallArtifactCleanupResult result = TryDeleteArchiveArtifacts(targetServer, installPath);
        if (!result.Success)
            throw result.Exception ?? new IOException($"Failed to delete install artifacts. archivePath={result.ArchivePath}");
    }

    /// <summary>
    /// 서버별 전체 클라이언트 압축 파일 이름을 반환합니다.
    /// </summary>
    public static string GetArchiveFileName(GameServer targetServer)
    {
        Uri archiveUrl = new(GameServerHelper.GetFullClientUrl(targetServer));
        string fileName = Path.GetFileName(archiveUrl.LocalPath);
        return string.IsNullOrWhiteSpace(fileName)
            ? $"{GameServerHelper.GetClientFolderName(targetServer)}_Install.7z"
            : fileName;
    }

    private static GameInstallArtifactCleanupResult? TryDeleteFileIfExists(
        string path,
        GameInstallHelperFailureStage failureStage,
        string artifactName)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            File.Delete(path);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new GameInstallArtifactCleanupResult(
                false,
                path,
                failureStage,
                new IOException($"Failed to delete {artifactName}. path={path}", ex));
        }
    }
}

/// <summary>
/// 전체 클라이언트 설치의 현재 다운로드/압축 해제 상태를 나타냅니다.
/// </summary>
public sealed record GameInstallProgress(
    string? DownloadFileName,
    long DownloadedBytes,
    long? DownloadTotalBytes,
    double? DownloadPercent,
    int ExtractedEntries,
    int? ExtractTotalEntries,
    double ExtractPercent,
    string? CurrentExtractEntry);

/// <summary>
/// 설치 진행 상태를 스레드 안전하게 누적한 뒤 UI에 전달할 스냅샷으로 변환합니다.
/// </summary>
public sealed class GameInstallProgressState
{
    private readonly object _sync = new();

    public string? DownloadFileName { get; set; }
    public long DownloadedBytes { get; set; }
    public long? DownloadTotalBytes { get; set; }
    public double? DownloadPercent { get; set; }
    public int ExtractedEntries { get; set; }
    public int? ExtractTotalEntries { get; set; }
    public double ExtractPercent { get; set; }
    public string? CurrentExtractEntry { get; set; }

    /// <summary>
    /// 진행 상태를 원자적으로 갱신합니다.
    /// </summary>
    public void Update(Action<GameInstallProgressState> updater)
    {
        ArgumentNullException.ThrowIfNull(updater);

        lock (_sync)
        {
            updater(this);
        }
    }

    /// <summary>
    /// 현재 설치 진행 상태의 스냅샷을 반환합니다.
    /// </summary>
    public GameInstallProgress Snapshot()
    {
        lock (_sync)
        {
            return new GameInstallProgress(
                DownloadFileName,
                DownloadedBytes,
                DownloadTotalBytes,
                DownloadPercent,
                ExtractedEntries,
                ExtractTotalEntries,
                ExtractPercent,
                CurrentExtractEntry);
        }
    }
}
