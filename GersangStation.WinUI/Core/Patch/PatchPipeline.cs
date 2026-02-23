using SevenZipExtractor;
using System.Net;
using System.Security.Cryptography;

namespace Core.Patch;

public static class PatchPipeline
{
    /// <summary>
    /// 서버 메타(vsn.dat.gsz + Client_info_File/{version})를 읽어
    /// 다운로드/병합/압축해제 파이프라인을 실행한다.
    /// </summary>
    public static async Task RunPatchFromServerAsync(
        int currentClientVersion,
        Uri patchBaseUri,
        string installRoot,
        string tempRoot,
        int maxConcurrency,
        CancellationToken ct)
    {
        if (patchBaseUri is null) throw new ArgumentNullException(nameof(patchBaseUri));

        // 메타 조회는 짧은 요청 위주라 기본 HttpClient로 분리
        using var http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.None
        })
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        int latestServerVersion = await ResolveLatestServerVersionAsync(http, patchBaseUri, tempRoot, ct).ConfigureAwait(false);
        var entriesByVersion = await DownloadEntriesByVersionAsync(http, currentClientVersion, latestServerVersion, patchBaseUri, ct).ConfigureAwait(false);

        await RunPatchAsync(
            currentClientVersion: currentClientVersion,
            latestServerVersion: latestServerVersion,
            entriesByVersion: entriesByVersion,
            patchBaseUri: patchBaseUri,
            installRoot: installRoot,
            tempRoot: tempRoot,
            maxConcurrency: maxConcurrency,
            ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 패치 파일 다운로드 + (버전 오름차순) 압축 해제 + 임시폴더 정리.
    ///
    /// ⚠️ 파싱(2번)은 여기서 구현하지 않는다.
    ///    - 파싱 결과(entriesByVersion)만 입력으로 받는다.
    /// </summary>
    public static async Task RunPatchAsync(
        int currentClientVersion,
        int latestServerVersion,
        IReadOnlyDictionary<int, List<string[]>> entriesByVersion,
        Uri patchBaseUri,
        string installRoot,
        string tempRoot,
        int maxConcurrency,
        CancellationToken ct)
    {
        if (entriesByVersion is null) throw new ArgumentNullException(nameof(entriesByVersion));
        if (patchBaseUri is null) throw new ArgumentNullException(nameof(patchBaseUri));
        if (string.IsNullOrWhiteSpace(installRoot)) throw new ArgumentException("installRoot is required.", nameof(installRoot));
        if (string.IsNullOrWhiteSpace(tempRoot)) throw new ArgumentException("tempRoot is required.", nameof(tempRoot));
        if (maxConcurrency < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrency));

        if (latestServerVersion < currentClientVersion)
            return;

        Directory.CreateDirectory(tempRoot);

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
                    string gszPath = Path.Combine(tempRoot, version.ToString(), f.CompressedFileName);
                    ExtractGsz(gszPath, installRoot, f.RelativeDir, f.ArchiveChecksum);
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

        int raw = BitConverter.ToInt32(bytes[..sizeof(int)]);
        return -(raw + 1); // 1의 보수
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

    private static async Task<int> ResolveLatestServerVersionAsync(HttpClient http, Uri patchBaseUri, string tempRoot, CancellationToken ct)
    {
        string probeRoot = Path.Combine(tempRoot, "LatestVersionProbe");
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
        using (var archive = new ArchiveFile(archivePath))
        {
            archive.Extract(extractRoot, overwrite: true);
        }

        string vsnPath = ResolveVsnDatPath(extractRoot);

        byte[] bytes = await File.ReadAllBytesAsync(vsnPath, ct).ConfigureAwait(false);
        int latestVersion = DecodeLatestVersionFromVsnDat(bytes);

        TryDeleteDirectory(probeRoot);
        return latestVersion;
    }

    private static string ResolveVsnDatPath(string extractRoot)
    {
        var files = Directory.EnumerateFiles(extractRoot, "*", SearchOption.AllDirectories).ToList();

        if (files.Count == 0)
            throw new FileNotFoundException("No file extracted from vsn.dat.gsz.", extractRoot);

        // 파일명 대소문자 차이를 허용해서 우선 탐색
        string? exact = files.FirstOrDefault(path =>
            string.Equals(Path.GetFileName(path), "vsn.dat", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(exact))
            return exact;

        // 예외 케이스: 엔트리명이 달라진 경우 4바이트 이상 파일 중 가장 큰 파일을 후보로 사용
        string? fallback = files
            .OrderByDescending(path => new FileInfo(path).Length)
            .FirstOrDefault(path => new FileInfo(path).Length >= sizeof(int));

        if (!string.IsNullOrWhiteSpace(fallback))
            return fallback;

        throw new InvalidDataException($"vsn.dat candidate not found. extracted file count={files.Count}");
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

    private static void ExtractGsz(string archivePath, string installRoot, string relativeDir, string? expectedArchiveChecksum)
    {
        VerifyArchiveChecksum(archivePath, expectedArchiveChecksum);

        // relativeDir가 "\Online\Sub\" 형태면 Path.Combine이 앞 "\" 때문에 무시될 수 있어서
        // installRoot + relativeDir를 "문자열 결합"으로 만든다.
        string rel = relativeDir.TrimStart('\\', '/');
        string targetDir = Path.Combine(installRoot, rel);

        Directory.CreateDirectory(targetDir);

        // ---- LOG: 시작 + 아카이브 내부 파일 목록 ----
        System.Diagnostics.Debug.WriteLine($"[EXTRACT][BEGIN] {archivePath}");
        System.Diagnostics.Debug.WriteLine($"[EXTRACT] targetDir: {targetDir}");

        using var archive = new ArchiveFile(archivePath);

        // SevenZipExtractor는 Entries 컬렉션을 제공함 (파일명/디렉토리 포함)
        int total = 0;
        int printed = 0;
        const int MaxPrint = 200;

        foreach (var e in archive.Entries)
        {
            total++;

            // 너무 길어지면 앞쪽만 출력
            if (printed < MaxPrint)
            {
                System.Diagnostics.Debug.WriteLine($"  - [{e.CRC}] {e.FileName}");
                printed++;
            }
        }

        if (total > MaxPrint)
            System.Diagnostics.Debug.WriteLine($"  ... ({total - MaxPrint} more)");

        System.Diagnostics.Debug.WriteLine($"[EXTRACT] entries: {total}");

        // ---- 실제 해제 ----
        archive.Extract(targetDir, overwrite: true);

        // ---- LOG: 종료 ----
        System.Diagnostics.Debug.WriteLine($"[EXTRACT][END] {archivePath}");
    }


    private static void VerifyArchiveChecksum(string archivePath, string? expectedArchiveChecksum)
    {
        if (string.IsNullOrWhiteSpace(expectedArchiveChecksum))
        {
            // 서버 메타에 아카이브 체크섬이 없는 경우는 기존 동작 유지
            return;
        }

        string normalizedExpected = NormalizeChecksum(expectedArchiveChecksum);

        // 거상 메타의 ZIP File CheckSum은 10진수 CRC32인 경우가 있어 별도 처리
        if (uint.TryParse(normalizedExpected, out uint expectedDecimalCrc32))
        {
            using var stream = File.OpenRead(archivePath);
            uint actualCrc32 = ComputeCrc32(stream);
            if (actualCrc32 != expectedDecimalCrc32)
            {
                throw new InvalidDataException(
                    $"Archive checksum mismatch. file='{archivePath}', expected='{expectedDecimalCrc32}', actual='{actualCrc32}'");
            }
            return;
        }

        string actual = ComputeArchiveChecksum(archivePath, normalizedExpected.Length);

        if (!string.Equals(actual, normalizedExpected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Archive checksum mismatch. file='{archivePath}', expected='{normalizedExpected}', actual='{actual}'");
        }
    }

    private static string ComputeArchiveChecksum(string filePath, int checksumLength)
    {
        using var stream = File.OpenRead(filePath);

        return checksumLength switch
        {
            8 => ComputeCrc32(stream).ToString("X8"),
            32 => Convert.ToHexString(MD5.HashData(stream)),
            40 => Convert.ToHexString(SHA1.HashData(stream)),
            64 => Convert.ToHexString(SHA256.HashData(stream)),
            _ => throw new InvalidDataException($"Unsupported checksum format(length={checksumLength}). file='{filePath}'")
        };
    }

    private static uint ComputeCrc32(Stream stream)
    {
        // System.IO.Hashing 의존성을 피하기 위해 로컬 CRC32 구현 사용
        uint crc = 0xFFFFFFFF;
        Span<byte> buffer = stackalloc byte[8192];

        int read;
        while ((read = stream.Read(buffer)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                crc = (crc >> 8) ^ Crc32Table[(crc ^ buffer[i]) & 0xFF];
            }
        }

        return crc ^ 0xFFFFFFFF;
    }

    private static readonly uint[] Crc32Table =
    [
        0x00000000u, 0x77073096u, 0xEE0E612Cu, 0x990951BAu, 0x076DC419u, 0x706AF48Fu, 0xE963A535u, 0x9E6495A3u,
        0x0EDB8832u, 0x79DCB8A4u, 0xE0D5E91Eu, 0x97D2D988u, 0x09B64C2Bu, 0x7EB17CBDu, 0xE7B82D07u, 0x90BF1D91u,
        0x1DB71064u, 0x6AB020F2u, 0xF3B97148u, 0x84BE41DEu, 0x1ADAD47Du, 0x6DDDE4EBu, 0xF4D4B551u, 0x83D385C7u,
        0x136C9856u, 0x646BA8C0u, 0xFD62F97Au, 0x8A65C9ECu, 0x14015C4Fu, 0x63066CD9u, 0xFA0F3D63u, 0x8D080DF5u,
        0x3B6E20C8u, 0x4C69105Eu, 0xD56041E4u, 0xA2677172u, 0x3C03E4D1u, 0x4B04D447u, 0xD20D85FDu, 0xA50AB56Bu,
        0x35B5A8FAu, 0x42B2986Cu, 0xDBBBC9D6u, 0xACBCF940u, 0x32D86CE3u, 0x45DF5C75u, 0xDCD60DCFu, 0xABD13D59u,
        0x26D930ACu, 0x51DE003Au, 0xC8D75180u, 0xBFD06116u, 0x21B4F4B5u, 0x56B3C423u, 0xCFBA9599u, 0xB8BDA50Fu,
        0x2802B89Eu, 0x5F058808u, 0xC60CD9B2u, 0xB10BE924u, 0x2F6F7C87u, 0x58684C11u, 0xC1611DABu, 0xB6662D3Du,
        0x76DC4190u, 0x01DB7106u, 0x98D220BCu, 0xEFD5102Au, 0x71B18589u, 0x06B6B51Fu, 0x9FBFE4A5u, 0xE8B8D433u,
        0x7807C9A2u, 0x0F00F934u, 0x9609A88Eu, 0xE10E9818u, 0x7F6A0DBBu, 0x086D3D2Du, 0x91646C97u, 0xE6635C01u,
        0x6B6B51F4u, 0x1C6C6162u, 0x856530D8u, 0xF262004Eu, 0x6C0695EDu, 0x1B01A57Bu, 0x8208F4C1u, 0xF50FC457u,
        0x65B0D9C6u, 0x12B7E950u, 0x8BBEB8EAu, 0xFCB9887Cu, 0x62DD1DDFu, 0x15DA2D49u, 0x8CD37CF3u, 0xFBD44C65u,
        0x4DB26158u, 0x3AB551CEu, 0xA3BC0074u, 0xD4BB30E2u, 0x4ADFA541u, 0x3DD895D7u, 0xA4D1C46Du, 0xD3D6F4FBu,
        0x4369E96Au, 0x346ED9FCu, 0xAD678846u, 0xDA60B8D0u, 0x44042D73u, 0x33031DE5u, 0xAA0A4C5Fu, 0xDD0D7CC9u,
        0x5005713Cu, 0x270241AAu, 0xBE0B1010u, 0xC90C2086u, 0x5768B525u, 0x206F85B3u, 0xB966D409u, 0xCE61E49Fu,
        0x5EDEF90Eu, 0x29D9C998u, 0xB0D09822u, 0xC7D7A8B4u, 0x59B33D17u, 0x2EB40D81u, 0xB7BD5C3Bu, 0xC0BA6CADu,
        0xEDB88320u, 0x9ABFB3B6u, 0x03B6E20Cu, 0x74B1D29Au, 0xEAD54739u, 0x9DD277AFu, 0x04DB2615u, 0x73DC1683u,
        0xE3630B12u, 0x94643B84u, 0x0D6D6A3Eu, 0x7A6A5AA8u, 0xE40ECF0Bu, 0x9309FF9Du, 0x0A00AE27u, 0x7D079EB1u,
        0xF00F9344u, 0x8708A3D2u, 0x1E01F268u, 0x6906C2FEu, 0xF762575Du, 0x806567CBu, 0x196C3671u, 0x6E6B06E7u,
        0xFED41B76u, 0x89D32BE0u, 0x10DA7A5Au, 0x67DD4ACCu, 0xF9B9DF6Fu, 0x8EBEEFF9u, 0x17B7BE43u, 0x60B08ED5u,
        0xD6D6A3E8u, 0xA1D1937Eu, 0x38D8C2C4u, 0x4FDFF252u, 0xD1BB67F1u, 0xA6BC5767u, 0x3FB506DDu, 0x48B2364Bu,
        0xD80D2BDAu, 0xAF0A1B4Cu, 0x36034AF6u, 0x41047A60u, 0xDF60EFC3u, 0xA867DF55u, 0x316E8EEFu, 0x4669BE79u,
        0xCB61B38Cu, 0xBC66831Au, 0x256FD2A0u, 0x5268E236u, 0xCC0C7795u, 0xBB0B4703u, 0x220216B9u, 0x5505262Fu,
        0xC5BA3BBEu, 0xB2BD0B28u, 0x2BB45A92u, 0x5CB36A04u, 0xC2D7FFA7u, 0xB5D0CF31u, 0x2CD99E8Bu, 0x5BDEAE1Du,
        0x9B64C2B0u, 0xEC63F226u, 0x756AA39Cu, 0x026D930Au, 0x9C0906A9u, 0xEB0E363Fu, 0x72076785u, 0x05005713u,
        0x95BF4A82u, 0xE2B87A14u, 0x7BB12BAEu, 0x0CB61B38u, 0x92D28E9Bu, 0xE5D5BE0Du, 0x7CDCEFB7u, 0x0BDBDF21u,
        0x86D3D2D4u, 0xF1D4E242u, 0x68DDB3F8u, 0x1FDA836Eu, 0x81BE16CDu, 0xF6B9265Bu, 0x6FB077E1u, 0x18B74777u,
        0x88085AE6u, 0xFF0F6A70u, 0x66063BCAu, 0x11010B5Cu, 0x8F659EFFu, 0xF862AE69u, 0x616BFFD3u, 0x166CCF45u,
        0xA00AE278u, 0xD70DD2EEu, 0x4E048354u, 0x3903B3C2u, 0xA7672661u, 0xD06016F7u, 0x4969474Du, 0x3E6E77DBu,
        0xAED16A4Au, 0xD9D65ADCu, 0x40DF0B66u, 0x37D83BF0u, 0xA9BCAE53u, 0xDEBB9EC5u, 0x47B2CF7Fu, 0x30B5FFE9u,
        0xBDBDF21Cu, 0xCABAC28Au, 0x53B39330u, 0x24B4A3A6u, 0xBAD03605u, 0xCDD70693u, 0x54DE5729u, 0x23D967BFu,
        0xB3667A2Eu, 0xC4614AB8u, 0x5D681B02u, 0x2A6F2B94u, 0xB40BBE37u, 0xC30C8EA1u, 0x5A05DF1Bu, 0x2D02EF8Du
    ];

    private static string NormalizeChecksum(string checksum)
    {
        var normalized = checksum.Trim().ToUpperInvariant();
        return normalized.Replace("-", string.Empty);
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
