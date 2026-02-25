using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using System.Diagnostics;
using System.Net;

namespace Core.Patch;

/// <summary>
/// 상위 계층(WinUI 등)에서 사용하기 위한 단순 Patch API.
/// 본섭 CDN 기준으로 고정되어 있습니다.
/// </summary>
public static class PatchClientApi
{
    public static readonly Uri PatchBaseUri = new("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/");
    public static readonly Uri FullClientUri = new("http://ak-gersangkr.xcache.kinxcdn.com/FullClient/Gersang_Install.7z");

    public const string ReadMeSuffix = "Client_Readme/readme.txt";
    public const string LatestVersionArchiveSuffix = "Client_Patch_File/Online/vsn.dat.gsz";

    private const string FullClientInstallFolderName = "Gersang";
    private static readonly TimeSpan ExtractProgressReportInterval = TimeSpan.FromMilliseconds(200);
    private const long ExtractProgressReportByteStep = 8L * 1024 * 1024;

    private static string? _clientInstallRoot;

    /// <summary>
    /// 전역 클라이언트 설치 경로를 지정합니다.
    /// </summary>
    public static void SetClientInstallRoot(string clientInstallRoot)
    {
        if (string.IsNullOrWhiteSpace(clientInstallRoot))
            throw new ArgumentException("clientInstallRoot is required.", nameof(clientInstallRoot));

        _clientInstallRoot = clientInstallRoot;
    }

    /// <summary>
    /// 현재 저장된 전역 클라이언트 설치 경로를 반환합니다.
    /// </summary>
    public static string GetClientInstallRoot()
    {
        EnsureClientInstallRootConfigured();
        return _clientInstallRoot!;
    }

    /// <summary>
    /// 설치 경로의 Online/vsn.dat을 읽어 현재 클라이언트 버전을 반환합니다.
    /// </summary>
    public static int GetCurrentClientVersion()
    {
        EnsureClientInstallRootConfigured();

        string vsnPath = Path.Combine(_clientInstallRoot!, "Online", "vsn.dat");
        if (!File.Exists(vsnPath))
            throw new FileNotFoundException("Current version file not found.", vsnPath);

        using var stream = File.OpenRead(vsnPath);
        return PatchPipeline.DecodeLatestVersionFromVsnDat(stream);
    }

    /// <summary>
    /// 서버의 최신 버전(vsn.dat.gsz)을 읽어 int 버전을 반환합니다.
    /// </summary>
    public static async Task<int> GetLatestServerVersionAsync(CancellationToken ct = default)
    {
        using var http = CreateHttpClient(TimeSpan.FromMinutes(5));
        using var response = await http.GetAsync(new Uri(PatchBaseUri, LatestVersionArchiveSuffix), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var archiveStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var archive = ArchiveFactory.OpenArchive(archiveStream);

        var entry = archive.Entries.FirstOrDefault(e => !e.IsDirectory)
            ?? throw new InvalidDataException("No vsn.dat entry found in latest version archive.");

        using var entryStream = entry.OpenEntryStream();
        return PatchPipeline.DecodeLatestVersionFromVsnDat(entryStream);
    }

    /// <summary>
    /// 패치를 수행합니다. 상위 계층에서는 현재 버전만 전달하면 됩니다.
    /// </summary>
    public static Task PatchAsync(int currentClientVersion, CancellationToken ct = default)
    {
        EnsureClientInstallRootConfigured();

        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "GersangStation",
            "Patch",
            Guid.NewGuid().ToString("N"));

        return PatchPipeline.RunPatchAsync(
            currentClientVersion: currentClientVersion,
            patchBaseUri: PatchBaseUri,
            installRoot: _clientInstallRoot!,
            tempRoot: tempRoot,
            maxConcurrency: 2,
            maxExtractRetryCount: 2,
            ct: ct,
            cleanupTemp: true);
    }

    /// <summary>
    /// 패치 내역(ReadMe) 텍스트를 다운로드합니다.
    /// </summary>
    public static async Task<string> DownloadReadMeAsync(CancellationToken ct = default)
    {
        using var http = CreateHttpClient(TimeSpan.FromMinutes(1));
        return await http.GetStringAsync(new Uri(PatchBaseUri, ReadMeSuffix), ct).ConfigureAwait(false);
    }

    public static string ResolveFullClientInstallRoot(string selectedRoot)
    {
        if (string.IsNullOrWhiteSpace(selectedRoot))
            throw new ArgumentException("selectedRoot is required.", nameof(selectedRoot));

        string normalizedSelectedRoot = Path.GetFullPath(selectedRoot.Trim());

        string leafName = Path.GetFileName(normalizedSelectedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.Equals(leafName, FullClientInstallFolderName, StringComparison.OrdinalIgnoreCase))
            return normalizedSelectedRoot;

        return Path.Combine(normalizedSelectedRoot, FullClientInstallFolderName);
    }

    /// <summary>
    /// FullClient를 내려받아 지정한 설치 경로로 압축 해제합니다.
    /// </summary>
    public static async Task InstallFullClientAsync(
        string installRoot,
        IProgress<InstallClientProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(installRoot)) throw new ArgumentException("installRoot is required.", nameof(installRoot));

        installRoot = ResolveFullClientInstallRoot(installRoot);
        Directory.CreateDirectory(installRoot);

        string archivePath = Path.GetFullPath(Path.Combine(installRoot, "Gersang_Install.7z"));
        string normalizedRoot = installRoot.EndsWith(Path.DirectorySeparatorChar)
            ? installRoot
            : installRoot + Path.DirectorySeparatorChar;

        if (!archivePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Archive path resolved outside installRoot. installRoot='{installRoot}', archivePath='{archivePath}'");

        using var http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.None
        })
        {
            Timeout = TimeSpan.FromHours(2)
        };

        var downloader = new Downloader(http);
        await downloader.DownloadAsync(
            FullClientUri,
            archivePath,
            new DownloadOptions(Overwrite: true),
            progress: progress is null ? null : new Progress<DownloadProgress>(p =>
            {
                progress.Report(new InstallClientProgress(
                    InstallClientPhase.Downloading,
                    p.BytesReceived,
                    p.TotalBytes,
                    0,
                    p.BytesPerSecond));
            }),
            ct: ct).ConfigureAwait(false);

        var perEntryProgress = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var extractProgressSync = new object();
        long extractedBytes = 0;
        long totalExtractBytes = 0;
        long bytesAtLastReport = 0;
        var extractProgressWatch = Stopwatch.StartNew();

        var readerOptions = ReaderOptions.ForOwnedFile with
        {
            Overwrite = true,
            ExtractFullPath = true,
            PreserveFileTime = true,
            Progress = progress is null ? null : new Progress<ProgressReport>(report =>
            {
                // 라이브러리 진행률(report)은 엔트리별로 보고될 수 있어 누적치로 변환합니다.
                lock (extractProgressSync)
                {
                    string key = report.EntryPath ?? string.Empty;
                    long current = Math.Max(0, report.BytesTransferred);
                    long previous = perEntryProgress.TryGetValue(key, out long value) ? value : 0;
                    if (current <= previous)
                        return;

                    extractedBytes += current - previous;
                    perEntryProgress[key] = current;

                    long sinceLastReportBytes = extractedBytes - bytesAtLastReport;
                    bool intervalElapsed = extractProgressWatch.Elapsed >= ExtractProgressReportInterval;
                    if (!intervalElapsed && sinceLastReportBytes < ExtractProgressReportByteStep)
                        return;

                    bytesAtLastReport = extractedBytes;
                    extractProgressWatch.Restart();

                    progress.Report(new InstallClientProgress(
                        InstallClientPhase.Extracting,
                        0,
                        totalExtractBytes,
                        Math.Min(extractedBytes, totalExtractBytes),
                        null));
                }
            })
        };

        await using var archive = await ArchiveFactory.OpenAsyncArchive(archivePath, readerOptions, ct).ConfigureAwait(false);
        totalExtractBytes = await archive.TotalUncompressedSizeAsync().ConfigureAwait(false);

        // 7z/solid 아카이브는 라이브러리 내부 순차 해제가 가장 빠른 경로입니다.
        await archive.WriteToDirectoryAsync(installRoot, cancellationToken: ct).ConfigureAwait(false);

        progress?.Report(new InstallClientProgress(
            InstallClientPhase.Extracting,
            0,
            totalExtractBytes,
            totalExtractBytes,
            null));
    }

    private static void EnsureClientInstallRootConfigured()
    {
        if (string.IsNullOrWhiteSpace(_clientInstallRoot))
            throw new InvalidOperationException("Client install root is not configured. Call SetClientInstallRoot first.");
    }

    private static HttpClient CreateHttpClient(TimeSpan timeout)
    {
        return new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.None
        })
        {
            Timeout = timeout
        };
    }
}

public enum InstallClientPhase
{
    Downloading,
    Extracting
}

public sealed record InstallClientProgress(
    InstallClientPhase Phase,
    long BytesReceived,
    long? TotalBytes,
    long BytesExtracted,
    double? BytesPerSecond);
