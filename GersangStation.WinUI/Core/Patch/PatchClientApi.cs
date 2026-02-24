using SharpCompress.Archives;
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
        using var archive = ArchiveFactory.Open(archiveStream);

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

    /// <summary>
    /// FullClient를 내려받아 지정한 설치 경로로 압축 해제합니다.
    /// </summary>
    public static async Task InstallFullClientAsync(
        string installRoot,
        string tempRoot,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(installRoot)) throw new ArgumentException("installRoot is required.", nameof(installRoot));
        if (string.IsNullOrWhiteSpace(tempRoot)) throw new ArgumentException("tempRoot is required.", nameof(tempRoot));

        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(installRoot);

        string archivePath = Path.Combine(tempRoot, "Gersang_Install.7z");

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
            progress: null,
            ct: ct).ConfigureAwait(false);

        using var archive = ArchiveFactory.Open(archivePath);
        ExtractEntriesWithOverwrite(archive, installRoot);
    }

    private static void ExtractEntriesWithOverwrite(IArchive archive, string destinationRoot)
    {
        string normalizedRoot = Path.GetFullPath(destinationRoot);
        if (!normalizedRoot.EndsWith(Path.DirectorySeparatorChar))
            normalizedRoot += Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            string relativePath = (entry.Key ?? string.Empty)
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);

            string destinationPath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));

            if (!destinationPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Archive entry has invalid path. entry='{entry.Key}', root='{destinationRoot}'");

            string? destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir))
                Directory.CreateDirectory(destinationDir);

            using var sourceStream = entry.OpenEntryStream();
            using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            sourceStream.CopyTo(destinationStream);
        }
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
