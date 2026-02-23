using System.Globalization;
using System.Text;

namespace Core.Patch;

/// <summary>
/// ZIP/GSZ 파일의 Central Directory를 파싱하여 엔트리별 CRC를 추출합니다.
/// </summary>
public static class ZipCrcReader
{
    private const uint EOCD_SIG = 0x06054B50; // End of central directory
    private const uint CEN_SIG = 0x02014B50; // Central directory file header

    private static bool _encodingProviderRegistered;

    /// <summary>
    /// ZIP/GSZ 헤더(Central Directory)에 기록된 모든 엔트리의 CRC를 추출합니다.
    /// 반환 형식: { FileName, CRCHexString(X8) }
    /// </summary>
    /// <param name="zipPath">ZIP 또는 GSZ 파일 경로</param>
    /// <returns>파일명 -> CRC32(8자리 대문자 HEX) 딕셔너리</returns>
    /// <exception cref="ArgumentException">경로가 비어있음</exception>
    /// <exception cref="FileNotFoundException">파일이 없음</exception>
    /// <exception cref="InvalidDataException">ZIP 구조가 아님 / 손상됨</exception>
    /// <exception cref="NotSupportedException">멀티디스크 ZIP 또는 미지원 포맷</exception>
    public static Dictionary<string, string> ReadEntryCrcMap(string zipPath)
    {
        if (string.IsNullOrWhiteSpace(zipPath))
            throw new ArgumentException("zipPath is null or empty.", nameof(zipPath));

        if (!File.Exists(zipPath))
            throw new FileNotFoundException("File not found.", zipPath);

        EnsureEncodingProvider();

        using var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false);

        // 1) EOCD 탐색
        long eocdOffset = FindEocd(fs, br);
        fs.Position = eocdOffset;

        uint eocdSig = br.ReadUInt32();
        if (eocdSig != EOCD_SIG)
            throw new InvalidDataException("Invalid ZIP file: EOCD signature not found.");

        ushort diskNo = br.ReadUInt16();
        ushort centralDirDiskNo = br.ReadUInt16();
        ushort entriesThisDisk = br.ReadUInt16();
        ushort totalEntries = br.ReadUInt16();
        uint centralDirSize = br.ReadUInt32();
        uint centralDirOffset = br.ReadUInt32();
        ushort commentLen = br.ReadUInt16();

        // 멀티디스크 ZIP 미지원
        if (diskNo != 0 || centralDirDiskNo != 0)
            throw new NotSupportedException("Multi-disk ZIP is not supported.");

        // Zip64 신호값(일반 EOCD 필드가 max 값으로 채워지는 경우) 미지원
        if (totalEntries == ushort.MaxValue || centralDirOffset == uint.MaxValue || centralDirSize == uint.MaxValue)
            throw new NotSupportedException("Zip64 format is not supported.");

        if (centralDirOffset >= fs.Length)
            throw new InvalidDataException("Invalid ZIP file: central directory offset is out of range.");

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 2) Central Directory 순회
        fs.Position = centralDirOffset;

        for (int i = 0; i < totalEntries; i++)
        {
            if (fs.Position + 46 > fs.Length)
                throw new InvalidDataException("Invalid ZIP file: truncated central directory header.");

            uint sig = br.ReadUInt32();
            if (sig != CEN_SIG)
                throw new InvalidDataException($"Invalid ZIP file: central header signature mismatch at entry #{i + 1}.");

            ushort versionMadeBy = br.ReadUInt16();
            ushort versionNeeded = br.ReadUInt16();
            ushort generalPurposeBitFlag = br.ReadUInt16();
            ushort compressionMethod = br.ReadUInt16();
            ushort lastModTime = br.ReadUInt16();
            ushort lastModDate = br.ReadUInt16();
            uint crc32 = br.ReadUInt32();
            uint compressedSize = br.ReadUInt32();
            uint uncompressedSize = br.ReadUInt32();
            ushort fileNameLen = br.ReadUInt16();
            ushort extraLen = br.ReadUInt16();
            ushort commentLength = br.ReadUInt16();
            ushort diskNumberStart = br.ReadUInt16();
            ushort internalFileAttrs = br.ReadUInt16();
            uint externalFileAttrs = br.ReadUInt32();
            uint localHeaderOffset = br.ReadUInt32();

            if (fs.Position + fileNameLen + extraLen + commentLength > fs.Length)
                throw new InvalidDataException($"Invalid ZIP file: entry #{i + 1} exceeds file length.");

            byte[] fileNameBytes = br.ReadBytes(fileNameLen);
            if (fileNameBytes.Length != fileNameLen)
                throw new EndOfStreamException("Unexpected EOF while reading filename.");

            bool isUtf8 = (generalPurposeBitFlag & (1 << 11)) != 0;
            string fileName = DecodeZipFileName(fileNameBytes, isUtf8);

            // extra/comment skip
            if (extraLen > 0)
                fs.Seek(extraLen, SeekOrigin.Current);
            if (commentLength > 0)
                fs.Seek(commentLength, SeekOrigin.Current);

            // 요청사항: Dictionary<string, string> {FileName, CRC}
            // CRC 문자열 형식: 대문자 HEX 8자리
            result[fileName] = crc32.ToString("X8");
        }

        return result;
    }

    /// <summary>
    /// ZIP/GSZ 여부를 예외 없이 확인하고 싶을 때 사용하는 헬퍼.
    /// true면 ReadEntryCrcMap 호출 가능성이 높음(완전 보장 아님).
    /// </summary>
    public static bool IsSupportedZipLikeFile(string zipPath)
    {
        try
        {
            _ = ReadEntryCrcMap(zipPath);
            return true;
        }
        catch (ArgumentException) { return false; }
        catch (FileNotFoundException) { return false; }
        catch (InvalidDataException) { return false; }
        catch (NotSupportedException) { return false; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static void EnsureEncodingProvider()
    {
        if (_encodingProviderRegistered)
            return;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _encodingProviderRegistered = true;
    }

    private static string DecodeZipFileName(byte[] fileNameBytes, bool isUtf8)
    {
        if (isUtf8)
            return Encoding.UTF8.GetString(fileNameBytes);

        // ZIP 기본 레거시 인코딩(CP437)
        return Encoding.GetEncoding(437).GetString(fileNameBytes);
    }

    private static long FindEocd(FileStream fs, BinaryReader br)
    {
        const int minEocdSize = 22;
        const int maxCommentLen = ushort.MaxValue; // 65535

        long fileLength = fs.Length;
        if (fileLength < minEocdSize)
            throw new InvalidDataException("Invalid ZIP file: file too small.");

        long searchSize = Math.Min(fileLength, minEocdSize + maxCommentLen);
        long searchStart = fileLength - searchSize;

        fs.Position = searchStart;
        byte[] buffer = br.ReadBytes((int)searchSize);

        // 뒤에서부터 EOCD 시그니처(50 4B 05 06) 탐색
        for (int i = buffer.Length - minEocdSize; i >= 0; i--)
        {
            if (buffer[i] == 0x50 &&
                buffer[i + 1] == 0x4B &&
                buffer[i + 2] == 0x05 &&
                buffer[i + 3] == 0x06)
            {
                return searchStart + i;
            }
        }

        throw new InvalidDataException("Invalid ZIP file: EOCD not found.");
    }

    /// <summary>
    /// 10진수 문자열(예: "4236703619")을 CRC 비교용 16진수 문자열(X8, 대문자)로 변환합니다.
    /// </summary>
    public static string DecimalStringToCrcHex(string decimalValue)
    {
        if (string.IsNullOrWhiteSpace(decimalValue))
            throw new ArgumentException("decimalValue is null or empty.", nameof(decimalValue));

        if (!uint.TryParse(decimalValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint value))
            throw new FormatException($"Invalid UInt32 decimal value: '{decimalValue}'");

        return value.ToString("X8", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// UInt32 값을 CRC 비교용 16진수 문자열(X8, 대문자)로 변환합니다.
    /// </summary>
    public static string UInt32ToCrcHex(uint value)
    {
        return value.ToString("X8", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Int32(음수 포함 가능) 값을 비트패턴 그대로 CRC용 16진수 문자열(X8, 대문자)로 변환합니다.
    /// 로그/DB에서 signed int로 저장된 CRC를 다룰 때 사용.
    /// 예: -583? -> 0xXXXXXXXX
    /// </summary>
    public static string Int32ToCrcHex(int value)
    {
        return unchecked((uint)value).ToString("X8", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 16진수 CRC 문자열("FC86F783" 또는 "0xFC86F783")을 UInt32로 변환합니다.
    /// </summary>
    public static uint CrcHexToUInt32(string hexValue)
    {
        if (string.IsNullOrWhiteSpace(hexValue))
            throw new ArgumentException("hexValue is null or empty.", nameof(hexValue));

        string s = hexValue.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            s = s.Substring(2);

        if (!uint.TryParse(s, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint value))
            throw new FormatException($"Invalid CRC hex value: '{hexValue}'");

        return value;
    }

    /// <summary>
    /// 16진수 CRC 문자열("FC86F783" / "0xFC86F783")을 10진수 문자열로 변환합니다.
    /// 매니페스트 숫자와 비교할 때 사용.
    /// </summary>
    public static string CrcHexToDecimalString(string hexValue)
    {
        uint value = CrcHexToUInt32(hexValue);
        return value.ToString(CultureInfo.InvariantCulture);
    }
}