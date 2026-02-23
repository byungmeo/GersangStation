using SevenZipExtractor;
using System.IO.Hashing;
using System.Security.Cryptography;

namespace Core.Patch;

public static class PatchPipeline
{
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
            8 => ComputeCrc32Hex(stream),
            32 => Convert.ToHexString(MD5.HashData(stream)),
            40 => Convert.ToHexString(SHA1.HashData(stream)),
            64 => Convert.ToHexString(SHA256.HashData(stream)),
            _ => throw new InvalidDataException($"Unsupported checksum format(length={checksumLength}). file='{filePath}'")
        };
    }

    private static string ComputeCrc32Hex(Stream stream)
    {
        Span<byte> hash = stackalloc byte[4];
        Crc32.Hash(stream, hash);
        return Convert.ToHexString(hash);
    }

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