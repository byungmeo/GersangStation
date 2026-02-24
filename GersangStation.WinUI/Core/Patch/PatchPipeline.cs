using SharpCompress.Archives;
using System.Net;
using System.Text;

namespace Core.Patch;

public static class PatchPipeline
{
    /// <summary>
    /// 서버 메타(vsn.dat.gsz + Client_info_File/{version})를 읽어
    /// 다운로드/병합/압축해제 파이프라인을 실행한다.
    /// </summary>
    public static Task RunPatchFromServerAsync(
        int currentClientVersion,
        Uri patchBaseUri,
        string installRoot,
        string tempRoot,
        int maxConcurrency,
        int maxExtractRetryCount,
        CancellationToken ct)
    {
        return RunPatchAsync(
            currentClientVersion: currentClientVersion,
            patchBaseUri: patchBaseUri,
            installRoot: installRoot,
            tempRoot: tempRoot,
            maxConcurrency: maxConcurrency,
            maxExtractRetryCount: maxExtractRetryCount,
            ct: ct);
    }

    /// <summary>
    /// 패치 파일 다운로드 + (버전 오름차순) 압축 해제 + 임시폴더 정리.
    ///
    /// 1) vsn.dat.gsz로 최신 버전 조회
    /// 2) 현재+1..최신 버전의 Client_info_File 수집
    /// 3) 이후 다운로드/압축해제 파이프라인 실행
    /// </summary>
    public static async Task RunPatchAsync(
        int currentClientVersion,
        Uri patchBaseUri,
        string installRoot,
        string tempRoot,
        int maxConcurrency,
        int maxExtractRetryCount,
        CancellationToken ct)
    {
        if (patchBaseUri is null) throw new ArgumentNullException(nameof(patchBaseUri));
        if (string.IsNullOrWhiteSpace(installRoot)) throw new ArgumentException("installRoot is required.", nameof(installRoot));
        if (string.IsNullOrWhiteSpace(tempRoot)) throw new ArgumentException("tempRoot is required.", nameof(tempRoot));
        if (maxConcurrency < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
        if (maxExtractRetryCount < 0) throw new ArgumentOutOfRangeException(nameof(maxExtractRetryCount));

        Directory.CreateDirectory(tempRoot);

        // 메타 조회는 짧은 요청 위주라 기본 HttpClient로 분리
        using var http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.None
        })
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        int latestServerVersion = await ResolveLatestServerVersionAsync(http, patchBaseUri, tempRoot, maxExtractRetryCount, ct).ConfigureAwait(false);
        if (latestServerVersion < currentClientVersion)
            return;

        var entriesByVersion = await DownloadEntriesByVersionAsync(http, currentClientVersion, latestServerVersion, patchBaseUri, ct).ConfigureAwait(false);

        try
        {
            ct.ThrowIfCancellationRequested();

            // -----------------------------------------------------------------
            // 3) 최신 우선 병합 + 5) 버전 오름차순 해제용 ExtractPlan 생성
            // -----------------------------------------------------------------
            var plan = PatchPlanBuilder_StringRows.BuildExtractPlan(entriesByVersion);

            ct.ThrowIfCancellationRequested();

            // -----------------------------------------------------------------
            // 4) 계획에 포함된 .gsz 다운로드 (병렬 OK)
            // -----------------------------------------------------------------
            await PatchDownloaderStage.DownloadAllAsync(
                plan,
                tempRoot: tempRoot,
                patchBaseUri: patchBaseUri,
                maxConcurrency: maxConcurrency,
                ct: ct);

            ct.ThrowIfCancellationRequested();

            // -----------------------------------------------------------------
            // 5) 압축 해제: 반드시 "버전 오름차순"으로, 설치 경로에 덮어쓰기
            // -----------------------------------------------------------------
            foreach (var kv in plan.ByVersion) // 오름차순
            {
                int version = kv.Key;
                foreach (var f in kv.Value)
                {
                    ct.ThrowIfCancellationRequested();

                    string gszPath = Path.Combine(tempRoot, version.ToString(), f.CompressedFileName);
                    Uri patchUrl = PatchDownloaderStage.BuildPatchUrl(patchBaseUri, f.RelativeDir, f.CompressedFileName);

                    await ExtractWithRetryAsync(
                        archivePath: gszPath,
                        downloadUrl: patchUrl,
                        installRoot: installRoot,
                        relativeDir: f.RelativeDir,
                        expectedFirstEntryChecksum: f.FirstEntryChecksum,
                        maxExtractRetryCount: maxExtractRetryCount,
                        http: http,
                        ct: ct).ConfigureAwait(false);
                }
            }

            // -----------------------------------------------------------------
            // 6) 임시폴더 삭제
            // -----------------------------------------------------------------
            TryDeleteDirectory(tempRoot);
        }
        catch (OperationCanceledException)
        {
            // 요구사항: 작업 취소 시 임시폴더 제거
            TryDeleteDirectory(tempRoot);
            throw;
        }
        catch
        {
            // 실패 시에도 임시폴더 제거 (디스크 누수 방지)
            TryDeleteDirectory(tempRoot);
            throw;
        }
    }

    public static int DecodeLatestVersionFromVsnDat(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < sizeof(int))
            throw new InvalidDataException($"vsn.dat size is too small. size={bytes.Length}");

        using var ms = new MemoryStream(bytes.ToArray(), writable: false);
        return DecodeLatestVersionFromVsnDat(ms);
    }

    public static int DecodeLatestVersionFromVsnDat(Stream stream)
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

    public static List<string[]> ParseClientInfoRows(string content)
    {
        if (content is null) throw new ArgumentNullException(nameof(content));

        var rows = new List<string[]>();

        foreach (var line in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith(';')) continue;
            if (line.StartsWith('#')) continue;

            var cols = line.Split('\t');
            if (cols.Length < 4) continue;

            rows.Add(cols);
        }

        return rows;
    }

    private static async Task<int> ResolveLatestServerVersionAsync(HttpClient http, Uri patchBaseUri, string tempRoot, int maxExtractRetryCount, CancellationToken ct)
    {
        string probeRoot = Path.Combine(tempRoot, "LatestVersionProbe");
        TryDeleteDirectory(probeRoot);
        Directory.CreateDirectory(probeRoot);

        string archivePath = Path.Combine(probeRoot, "vsn.dat.gsz");
        string extractRoot = Path.Combine(probeRoot, "extract");

        var url = new Uri(patchBaseUri, "Client_Patch_File/Online/vsn.dat.gsz");

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using (var fs = File.Create(archivePath))
        {
            await response.Content.CopyToAsync(fs, ct).ConfigureAwait(false);
        }

        Directory.CreateDirectory(extractRoot);
        await ExtractArchiveToDirectoryWithRetryAsync(
            archivePath: archivePath,
            extractRoot: extractRoot,
            downloadUrl: url,
            maxExtractRetryCount: maxExtractRetryCount,
            http: http,
            ct: ct).ConfigureAwait(false);

        string vsnPath = ResolveVsnDatPath(extractRoot);

        int latestVersion;
        await using (var stream = File.OpenRead(vsnPath))
        {
            try
            {
                latestVersion = DecodeLatestVersionFromVsnDat(stream);
            }
            catch (InvalidDataException ex)
            {
                byte[] dump = await File.ReadAllBytesAsync(vsnPath, ct).ConfigureAwait(false);
                string hex = Convert.ToHexString(dump);
                throw new InvalidDataException(
                    $"Failed to decode vsn.dat. path='{vsnPath}', size={dump.Length}, hex='{hex}', archive='{archivePath}'", ex);
            }
        }

        TryDeleteDirectory(probeRoot);
        return latestVersion;
    }

    private static string ResolveVsnDatPath(string extractRoot)
    {
        var files = Directory.EnumerateFiles(extractRoot, "*", SearchOption.AllDirectories).ToList();

        if (files.Count == 0)
            throw new FileNotFoundException("No file extracted from vsn.dat.gsz.", extractRoot);

        if (files.Count != 1)
        {
            // 규격상 vsn.dat.gsz에는 파일 1개만 있어야 하므로, 다르면 즉시 원인 노출
            string details = string.Join(", ", files.Select(path => $"'{Path.GetFileName(path)}'({new FileInfo(path).Length} bytes)"));
            throw new InvalidDataException($"Unexpected file count extracted from vsn.dat.gsz: count={files.Count}, files=[{details}]");
        }

        return files[0];
    }

    private static async Task<IReadOnlyDictionary<int, List<string[]>>> DownloadEntriesByVersionAsync(
        HttpClient http,
        int currentClientVersion,
        int latestServerVersion,
        Uri patchBaseUri,
        CancellationToken ct)
    {
        var result = new Dictionary<int, List<string[]>>();

        for (int version = currentClientVersion + 1; version <= latestServerVersion; version++)
        {
            ct.ThrowIfCancellationRequested();

            var url = new Uri(patchBaseUri, $"Client_info_File/{version}");
            using var response = await http.GetAsync(url, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
                continue;

            response.EnsureSuccessStatusCode();
            string text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            var rows = ParseClientInfoRows(text);
            if (rows.Count > 0)
            {
                result[version] = rows;
            }
        }

        return result;
    }

    private static async Task ExtractWithRetryAsync(
        string archivePath,
        Uri downloadUrl,
        string installRoot,
        string relativeDir,
        string? expectedFirstEntryChecksum,
        int maxExtractRetryCount,
        HttpClient http,
        CancellationToken ct)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt <= maxExtractRetryCount; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (attempt > 0)
                {
                    await RedownloadArchiveAsync(http, downloadUrl, archivePath, ct).ConfigureAwait(false);
                }

                ExtractGsz(archivePath, installRoot, relativeDir, expectedFirstEntryChecksum);
                return;
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is IOException)
            {
                lastException = ex;
                if (attempt == maxExtractRetryCount)
                    break;
            }
        }

        throw new InvalidDataException($"Failed to extract archive after retry. file='{archivePath}', retryCount={maxExtractRetryCount}", lastException);
    }

    private static void ExtractGsz(string archivePath, string installRoot, string relativeDir, string? expectedFirstEntryChecksum)
    {
        // relativeDir가 "\Online\Sub\" 형태면 Path.Combine이 앞 "\" 때문에 무시될 수 있어서
        // installRoot + relativeDir를 "문자열 결합"으로 만든다.
        string rel = relativeDir.TrimStart('\\', '/');
        string targetDir = Path.Combine(installRoot, rel);

        Directory.CreateDirectory(targetDir);

        // ---- LOG: 시작 + 아카이브 내부 파일 목록 ----
        System.Diagnostics.Debug.WriteLine($"[EXTRACT][BEGIN] {archivePath}");
        System.Diagnostics.Debug.WriteLine($"[EXTRACT] targetDir: {targetDir}");

        using var archive = ArchiveFactory.OpenArchive(archivePath);
        VerifyFirstEntryChecksum(archive, archivePath, expectedFirstEntryChecksum);

        int total = 0;
        int printed = 0;
        const int MaxPrint = 200;

        foreach (var e in archive.Entries)
        {
            total++;

            // 너무 길어지면 앞쪽만 출력
            if (printed < MaxPrint)
            {
                System.Diagnostics.Debug.WriteLine($"  - [{e.Crc}] {e.Key}");
                printed++;
            }
        }

        if (total > MaxPrint)
            System.Diagnostics.Debug.WriteLine($"  ... ({total - MaxPrint} more)");

        System.Diagnostics.Debug.WriteLine($"[EXTRACT] entries: {total}");

        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            entry.WriteToDirectory(targetDir, new SharpCompress.Common.ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true
            });
        }

        // ---- LOG: 종료 ----
        System.Diagnostics.Debug.WriteLine($"[EXTRACT][END] {archivePath}");
    }


    private static void VerifyFirstEntryChecksum(IArchive archive, string archivePath, string? expectedFirstEntryChecksum)
    {
        var firstFileEntry = archive.Entries.FirstOrDefault(e => !e.IsDirectory)
            ?? throw new InvalidDataException($"Archive has no file entry. file='{archivePath}'");

        var actualCrc = firstFileEntry.Crc ?? 0;
        string actualDecimal = actualCrc.ToString();
        string actualHex = actualCrc.ToString("X8");

        if (string.IsNullOrWhiteSpace(expectedFirstEntryChecksum))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[EXTRACT][CRC] entry='{firstFileEntry.Key}', expected=(none), actualDec='{actualDecimal}', actualHex='{actualHex}', match=SKIP");
            return;
        }

        string normalizedExpected = NormalizeChecksum(expectedFirstEntryChecksum);
        bool isMatch = string.Equals(normalizedExpected, actualDecimal, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedExpected, actualHex, StringComparison.OrdinalIgnoreCase);

        System.Diagnostics.Debug.WriteLine(
            $"[EXTRACT][CRC] entry='{firstFileEntry.Key}', expected='{normalizedExpected}', actualDec='{actualDecimal}', actualHex='{actualHex}', match={isMatch}");

        if (!isMatch)
        {
            throw new InvalidDataException(
                $"Archive first-entry CRC mismatch. file='{archivePath}', entry='{firstFileEntry.Key}', expected='{normalizedExpected}', actualDec='{actualDecimal}', actualHex='{actualHex}'");
        }
    }

    private static string NormalizeChecksum(string checksum)
    {
        var normalized = checksum.Trim().ToUpperInvariant();
        return normalized.Replace("-", string.Empty);
    }

    private static async Task ExtractArchiveToDirectoryWithRetryAsync(
        string archivePath,
        string extractRoot,
        Uri downloadUrl,
        int maxExtractRetryCount,
        HttpClient http,
        CancellationToken ct)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt <= maxExtractRetryCount; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            TryDeleteDirectory(extractRoot);
            Directory.CreateDirectory(extractRoot);

            try
            {
                if (attempt > 0)
                {
                    await RedownloadArchiveAsync(http, downloadUrl, archivePath, ct).ConfigureAwait(false);
                }

                using var archive = ArchiveFactory.OpenArchive(archivePath);
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    entry.WriteToDirectory(extractRoot, new SharpCompress.Common.ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }

                return;
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is IOException)
            {
                lastException = ex;
                if (attempt == maxExtractRetryCount)
                    break;
            }
        }

        throw new InvalidDataException($"Failed to extract metadata archive after retry. file='{archivePath}', retryCount={maxExtractRetryCount}", lastException);
    }

    private static async Task RedownloadArchiveAsync(HttpClient http, Uri downloadUrl, string archivePath, CancellationToken ct)
    {
        using var response = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        if (File.Exists(archivePath))
            File.Delete(archivePath);

        await using var fs = File.Create(archivePath);
        await response.Content.CopyToAsync(fs, ct).ConfigureAwait(false);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }
}
