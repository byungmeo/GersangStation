using Core.Download;
using Core.Extract;
using Core.Models;

namespace Core;

/// <summary>
/// 게임 전체 클라이언트 설치 파일 다운로드와 압축 해제를 관리합니다.
/// </summary>
public sealed class GameInstallManager
{
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

        string normalizedInstallPath = Path.GetFullPath(installPath.Trim());
        Directory.CreateDirectory(normalizedInstallPath);

        Uri archiveUrl = new(GameServerHelper.GetFullClientUrl(targetServer));
        string archivePath = GetArchivePath(targetServer, normalizedInstallPath);
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

        await _downloader.DownloadFileAsync(
            archiveUrl,
            archivePath,
            new DownloadOptions(
                Overwrite: true,
                ExistingArtifactMode: archiveReuseMode),
            downloadProgress,
            ct).ConfigureAwait(false);

        state.Update(s =>
        {
            s.DownloadFileName = Path.GetFileName(archivePath);
            s.DownloadPercent = 100;
        });
        ReportProgressSnapshot();

        await _extractor.ExtractAsync(
            archivePath,
            normalizedInstallPath,
            extractionProgress,
            ct).ConfigureAwait(false);

        state.Update(s =>
        {
            s.ExtractPercent = 100;
        });
        ReportProgressSnapshot();

        DeleteArchiveArtifacts(targetServer, normalizedInstallPath);
    }

    /// <summary>
    /// 설치 경로 안에 저장되는 전체 클라이언트 압축 파일 경로를 반환합니다.
    /// </summary>
    public static string GetArchivePath(GameServer targetServer, string installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath))
            throw new ArgumentException("installPath is required.", nameof(installPath));

        return Path.Combine(
            Path.GetFullPath(installPath.Trim()),
            GetArchiveFileName(targetServer));
    }

    /// <summary>
    /// 이어받기용 설치 압축 파일과 다운로드 부속 파일을 삭제합니다.
    /// </summary>
    public static void DeleteArchiveArtifacts(GameServer targetServer, string installPath)
    {
        string archivePath = GetArchivePath(targetServer, installPath);
        DeleteFileIfExists(archivePath);
        DeleteFileIfExists($"{archivePath}.gsdownload");
        DeleteFileIfExists($"{archivePath}.meta");
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

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
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
