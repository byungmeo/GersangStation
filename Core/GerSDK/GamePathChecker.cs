using System;
using System.IO;
using System.Runtime.InteropServices;

namespace GerSDK;

/// <summary>
/// 
/// </summary>
public static class GamePathChecker
{
    /// <summary>
    /// 지정한 경로가 속한 볼륨이 reparse point를 지원하는지 확인합니다.
    /// 실제로 존재하는 드라이브 문자만 'C:\'와 같이 잘 포함하고 있다면, 존재하지 않는 경로나 파일이라도 확인 가능합니다.
    /// </summary>
    /// <param name="qualifiedPath">확인할 완전 경로입니다.</param>
    /// <returns>
    /// 지정한 경로의 볼륨이 reparse point를 지원하면 <see langword="true"/>를 반환합니다.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="qualifiedPath"/>가 가 null이거나 비어있는 경우 발생합니다.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="qualifiedPath"/>가 완전 경로가 아닌 경우 발생합니다.
    /// </exception>
    /// <exception cref="System.ComponentModel.Win32Exception">
    /// 볼륨 정보를 조회하지 못한 경우 발생합니다.
    /// </exception>
    public static bool IsSupportedSymlink(string qualifiedPath)
    {
        if (string.IsNullOrWhiteSpace(qualifiedPath))
        {
            throw new ArgumentNullException(nameof(qualifiedPath));
        }

        if (false == Path.IsPathFullyQualified(qualifiedPath))
        {
            throw new ArgumentException("Path must be fully qualified.", nameof(qualifiedPath));
        }

        string root = Path.GetPathRoot(qualifiedPath)!;
        bool ok = GetVolumeInformation(
            root,
            IntPtr.Zero, 0,
            IntPtr.Zero,
            IntPtr.Zero,
            out uint flags,
            IntPtr.Zero, 0);

        if (!ok)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        const uint FILE_SUPPORTS_REPARSE_POINTS = 0x00000080;
        return (flags & FILE_SUPPORTS_REPARSE_POINTS) != 0;
    }

    /// <summary>
    /// https://learn.microsoft.com/ko-kr/windows/win32/api/fileapi/nf-fileapi-getvolumeinformationa
    /// </summary>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetVolumeInformation(
        string lpRootPathName,
        IntPtr lpVolumeNameBuffer,
        int nVolumeNameSize,
        IntPtr lpVolumeSerialNumber,
        IntPtr lpMaximumComponentLength,
        out uint lpFileSystemFlags,
        IntPtr lpFileSystemNameBuffer,
        int nFileSystemNameSize);
}
