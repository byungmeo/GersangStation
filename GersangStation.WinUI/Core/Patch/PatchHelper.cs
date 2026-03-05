using Core.Extractor;
using Core.Models;
using Microsoft.Win32;
using System.Diagnostics;
using System.Net;

namespace Core.Patch;

/// <summary>
/// 상위 계층(WinUI 등)에서 사용하기 위한 단순 Patch API.
/// 본섭 CDN 기준으로 고정되어 있습니다.
/// </summary>
public static class PatchHelper
{
    private static readonly IExtractor Extractor = new NativeSevenZipExtractor();

    public const string ReadMeSuffix = "Client_Readme/readme.txt";
    public const string LatestVersionArchiveSuffix = "Client_Patch_File/Online/vsn.dat.gsz";

    /// <summary>
    /// 설치 경로의 Online/vsn.dat을 읽어 현재 클라이언트 버전을 반환합니다.
    /// </summary>
    public static int GetCurrentClientVersion(GameServer server)
    {
        ClientSettings clientSettings = AppDataManager.LoadServerClientSettings(server);

        string vsnPath = Path.Combine(clientSettings.InstallPath, "Online", "vsn.dat");
        if (!File.Exists(vsnPath))
            throw new FileNotFoundException("Current version file not found.", vsnPath);

        using var stream = File.OpenRead(vsnPath);
        return PatchPipeline.DecodeLatestVersionFromVsnDat(stream);
    }

    /// <summary>
    /// 서버의 최신 버전(vsn.dat.gsz)을 읽어 int 버전을 반환합니다.
    /// </summary>
    public static async Task<int> GetLatestServerVersionAsync(GameServer server, CancellationToken ct = default)
    {
        string vsnUrl = GameServerHelper.GetVsnUrl(server);
        using var http = CreateHttpClient(TimeSpan.FromMinutes(5));
        using var response = await http.GetAsync(new Uri(vsnUrl), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        string probeRoot = Path.Combine(Path.GetTempPath(), "GersangStation", "LatestVersion", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(probeRoot);

        try
        {
            string archivePath = Path.Combine(probeRoot, "vsn.dat.gsz");
            string extractRoot = Path.Combine(probeRoot, "extract");

            await using (var fs = File.Create(archivePath))
            {
                await response.Content.CopyToAsync(fs, ct).ConfigureAwait(false);
            }

            Directory.CreateDirectory(extractRoot);
            await Extractor.ExtractAsync(archivePath, extractRoot, ct: ct).ConfigureAwait(false);

            string vsnPath = Directory.EnumerateFiles(extractRoot, "*", SearchOption.AllDirectories).FirstOrDefault()
                ?? throw new InvalidDataException("No vsn.dat entry found in latest version archive.");

            await using var entryStream = File.OpenRead(vsnPath);
            return PatchPipeline.DecodeLatestVersionFromVsnDat(entryStream);
        }
        finally
        {
            try
            {
                if (Directory.Exists(probeRoot))
                    Directory.Delete(probeRoot, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
    }

    /// <summary>
    /// 패치를 수행합니다. 상위 계층에서는 현재 버전만 전달하면 됩니다.
    /// </summary>
    public static Task PatchAsync(GameServer server, int currentClientVersion, CancellationToken ct = default)
    {
        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "GersangStation",
            "Patch",
            Guid.NewGuid().ToString("N"));

        ClientSettings clientSettings = AppDataManager.LoadServerClientSettings(server);
        return PatchPipeline.RunPatchAsync(
            currentClientVersion: currentClientVersion,
            server: server,
            installRoot: clientSettings.InstallPath,
            tempRoot: tempRoot,
            maxConcurrency: 2,
            maxExtractRetryCount: 2,
            ct: ct,
            cleanupTemp: true);
    }

    /// <summary>
    /// FullClient를 내려받아 지정한 설치 경로로 압축 해제합니다.
    /// </summary>
    public static string NormalizeFullClientInstallRoot(string installRoot)
    {
        if (string.IsNullOrWhiteSpace(installRoot)) throw new ArgumentException("installRoot is required.", nameof(installRoot));

        string trimmedRoot = Path.GetFullPath(installRoot.Trim())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return string.Equals(Path.GetFileName(trimmedRoot), "Gersang", StringComparison.OrdinalIgnoreCase)
            ? trimmedRoot
            : Path.Combine(trimmedRoot, "Gersang");
    }

    /// <summary>
    /// FullClient를 내려받아 지정한 설치 경로(\Gersang 하위)로 압축 해제합니다.
    /// </summary>
    public static async Task InstallFullClientAsync(
        string installRoot,
        GameServer server,
        IProgress<DownloadProgress>? progress = null,
        IProgress<ExtractionProgress>? extractionProgress = null,
        bool skipDownloadIfArchiveExists = true,
        CancellationToken ct = default)
    {
        string actualInstallRoot = NormalizeFullClientInstallRoot(installRoot);

        Directory.CreateDirectory(actualInstallRoot);

        string archivePath = Path.Combine(actualInstallRoot, "Gersang_Install.7z");
        bool archiveExists = File.Exists(archivePath);

        using var http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.None
        })
        {
            Timeout = TimeSpan.FromHours(2)
        };

        var downloader = new Downloader(http);

        try
        {
            if (archiveExists && skipDownloadIfArchiveExists)
            {
                Debug.WriteLine($"[InstallFullClient] Archive already exists. Skip download and extract directly: {archivePath}");
            }
            else
            {
                await downloader.DownloadAsync(
                    new Uri(GameServerHelper.GetFullClientUrl(server)),
                    archivePath,
                    new DownloadOptions(Overwrite: !skipDownloadIfArchiveExists),
                    progress: progress,
                    ct: ct).ConfigureAwait(false);

                Debug.WriteLine($"[InstallFullClient] Download complete: {archivePath}");
            }

            await Extractor.ExtractAsync(archivePath, actualInstallRoot, progress: extractionProgress, ct: ct).ConfigureAwait(false);
            SaveInstallPathToRegistry(actualInstallRoot);
        }
        finally
        {
            try
            {
                if (File.Exists(archivePath))
                    File.Delete(archivePath);
            }
            catch
            {
                // best-effort
            }
        }
    }

    public static HttpClient CreateHttpClient(TimeSpan timeout)
    {
        return new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.None
        })
        {
            Timeout = timeout
        };
    }

    private static void SaveInstallPathToRegistry(string installRoot)
    {
        using RegistryKey? key = Registry.CurrentUser.CreateSubKey(@"Software\JOYON\Gersang\Korean", writable: true);
        key?.SetValue("InstallPath", installRoot, RegistryValueKind.String);
    }
}
