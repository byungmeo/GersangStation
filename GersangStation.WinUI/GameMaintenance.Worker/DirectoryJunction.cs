using Microsoft.Win32.SafeHandles;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace GameMaintenance.Worker;

/// <summary>Creates local NTFS directory junctions without a shell or elevation.</summary>
internal static class DirectoryJunction
{
    private const uint MountPointTag = 0xA0000003;
    private const uint SetReparsePoint = 0x000900A4;

    internal static void ValidatePaths(string linkPath, string targetPath)
    {
        ValidateLocalNtfsPath(linkPath);
        ValidateLocalNtfsPath(targetPath);
        string link = Path.TrimEndingDirectorySeparator(Path.GetFullPath(linkPath));
        string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetPath));
        if (link.Equals(target, StringComparison.OrdinalIgnoreCase)
            || target.StartsWith(link + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || link.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("정션 원본과 대상 경로가 같거나 서로 포함되어 있습니다.");
        if (!Directory.Exists(target)) throw new DirectoryNotFoundException(target);
    }

    internal static void Create(string linkPath, string targetPath)
    {
        ValidatePaths(linkPath, targetPath);
        string link = Path.GetFullPath(linkPath);
        string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetPath));
        byte[] substituteName = Encoding.Unicode.GetBytes(@"\??\" + target);
        byte[] printName = Encoding.Unicode.GetBytes(target);
        int printOffset = substituteName.Length + 2;
        byte[] buffer = new byte[16 + printOffset + printName.Length + 2];
        if (buffer.Length > 16 * 1024) throw new PathTooLongException(link);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0), MountPointTag);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(4), checked((ushort)(buffer.Length - 8)));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(10), checked((ushort)substituteName.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(12), checked((ushort)printOffset));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(14), checked((ushort)printName.Length));
        substituteName.CopyTo(buffer, 16);
        printName.CopyTo(buffer, 16 + printOffset);

        bool created = !Directory.Exists(link);
        Directory.CreateDirectory(link);
        try
        {
            if ((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0
                || Directory.EnumerateFileSystemEntries(link).Any())
                throw new IOException($"정션 대상은 비어 있는 일반 폴더여야 합니다. Path={link}");

            // Open the directory itself, never follow an existing final-component link.
            using SafeFileHandle handle = CreateFileW(link, 0x40000000, 7, IntPtr.Zero, 3,
                0x02000000 | 0x00200000, IntPtr.Zero);
            if (handle.IsInvalid) throw Failure(link);
            if (!DeviceIoControl(handle, SetReparsePoint, buffer, (uint)buffer.Length,
                    IntPtr.Zero, 0, out _, IntPtr.Zero))
                throw Failure(link);
        }
        catch
        {
            // Avoid leaving a failed, empty placeholder that blocks the next attempt.
            // Never recurse, remove existing data, or mask the original failure.
            if (created)
            {
                try
                {
                    if ((File.GetAttributes(link) & FileAttributes.ReparsePoint) == 0)
                        Directory.Delete(link);
                }
                catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
            }
            throw;
        }
    }

    private static void ValidateLocalNtfsPath(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("절대 경로가 필요합니다.", nameof(path));
        string fullPath = Path.GetFullPath(path);
        // Junction targets are local DOS paths, not UNC or device namespaces.
        if (fullPath.Length < 3 || !char.IsAsciiLetter(fullPath[0]) || fullPath[1] != ':' || fullPath[2] != '\\')
            throw new NotSupportedException("정션 다클라는 로컬 NTFS 드라이브에서만 지원됩니다.");
        var drive = new DriveInfo(Path.GetPathRoot(fullPath)!);
        if (drive.DriveType == DriveType.Network || !drive.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("정션 다클라는 로컬 NTFS 드라이브에서만 지원됩니다.");
    }

    private static Exception Failure(string path)
    {
        var error = new Win32Exception(Marshal.GetLastWin32Error());
        if (error.NativeErrorCode == 5)
            return new UnauthorizedAccessException($"정션을 생성할 파일 접근 권한이 부족합니다. Path={path}", error);
        return new IOException($"정션을 생성하지 못했습니다. Path={path}: {error.Message}", error);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string fileName, uint access, uint share,
        IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint controlCode, byte[] input,
        uint inputSize, IntPtr output, uint outputSize, out uint returned, IntPtr overlapped);
}
