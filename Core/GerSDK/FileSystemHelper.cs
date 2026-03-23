using System;
using System.IO;

namespace GerSDK;

/// <summary>
/// 파일 시스템 경로와 디렉터리 작업을 도와주는 공용 헬퍼입니다.
/// </summary>
internal static class FileSystemHelper
{
    /// <summary>
    /// 경로를 절대 경로로 변환하고 마지막 디렉터리 구분자를 정리합니다.
    /// </summary>
    /// <param name="path">정규화할 경로입니다.</param>
    /// <returns>정규화된 절대 경로입니다.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="path"/>가 <see langword="null"/>인 경우 발생합니다.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="path"/>가 비어 있거나 공백만 포함하는 경우 발생합니다.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="path"/>의 형식이 현재 시스템에서 지원되지 않는 경우 발생할 수 있습니다.
    /// </exception>
    /// <exception cref="PathTooLongException">
    /// 전체 경로로 변환하는 과정에서 경로 길이가 시스템 제한을 초과하면 발생할 수 있습니다.
    /// </exception>
    public static string NormalizePath(string path)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is empty or consists only of white-space characters", nameof(path));

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    /// <summary>
    /// 두 경로가 같은 실제 경로인지 확인합니다.
    /// </summary>
    /// <param name="path1">비교할 첫 번째 경로입니다.</param>
    /// <param name="path2">비교할 두 번째 경로입니다.</param>
    /// <returns>같은 경로이면 <see langword="true"/>를 반환합니다.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="path1"/> 또는 <paramref name="path2"/>가 <see langword="null"/>인 경우 발생합니다.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="path1"/> 또는 <paramref name="path2"/>가 비어 있거나 공백만 포함하는 경우 발생합니다.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// 전달된 경로 형식이 현재 시스템에서 지원되지 않는 경우 발생할 수 있습니다.
    /// </exception>
    /// <exception cref="PathTooLongException">
    /// 경로 정규화 중 길이 제한을 초과하면 발생할 수 있습니다.
    /// </exception>
    public static bool IsSamePath(string path1, string path2)
    {
        string normalizedPath1 = NormalizePath(path1);
        string normalizedPath2 = NormalizePath(path2);

        return normalizedPath1.Equals(normalizedPath2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 한 경로가 다른 경로와 같거나 그 하위 경로인지 확인합니다.
    /// </summary>
    /// <param name="basePath">기준이 되는 경로입니다.</param>
    /// <param name="otherPath">비교할 경로입니다.</param>
    /// <returns>같거나 하위 경로이면 <see langword="true"/>를 반환합니다.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="basePath"/> 또는 <paramref name="otherPath"/>가 <see langword="null"/>인 경우 발생합니다.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="basePath"/> 또는 <paramref name="otherPath"/>가 비어 있거나 공백만 포함하는 경우 발생합니다.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// 전달된 경로 형식이 현재 시스템에서 지원되지 않는 경우 발생할 수 있습니다.
    /// </exception>
    /// <exception cref="PathTooLongException">
    /// 경로 정규화 중 길이 제한을 초과하면 발생할 수 있습니다.
    /// </exception>
    public static bool IsSameOrNestedPath(string basePath, string otherPath)
    {
        string normalizedBase = NormalizePath(basePath);
        string normalizedOther = NormalizePath(otherPath);

        if (normalizedBase.Equals(normalizedOther, StringComparison.OrdinalIgnoreCase))
            return true;

        return normalizedOther.StartsWith(
            normalizedBase + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 심볼릭 링크의 LinkTarget 값을 절대 경로로 해석합니다.
    /// </summary>
    /// <param name="linkPath">심볼릭 링크 경로입니다.</param>
    /// <param name="linkTarget">심볼릭 링크의 LinkTarget 값입니다.</param>
    /// <returns>절대 경로로 정규화된 대상 경로입니다.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="linkTarget"/>이 <see langword="null"/>인 경우 발생합니다.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="linkPath"/>의 부모 디렉터리를 계산할 수 없거나 경로 정규화가 실패한 경우 발생합니다.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// 전달된 경로 형식이 현재 시스템에서 지원되지 않는 경우 발생할 수 있습니다.
    /// </exception>
    /// <exception cref="PathTooLongException">
    /// 경로 정규화 중 길이 제한을 초과하면 발생할 수 있습니다.
    /// </exception>
    public static string ResolveLinkTargetFullPath(string linkPath, string linkTarget)
    {
        if (linkTarget is null)
            throw new ArgumentNullException(nameof(linkTarget));

        if (Path.IsPathFullyQualified(linkTarget))
            return NormalizePath(linkTarget);

        string normalizedLinkPath = NormalizePath(linkPath);
        string? linkParentPath = Path.GetDirectoryName(normalizedLinkPath);
        if (linkParentPath is null)
            throw new ArgumentException("Failed to resolve symbolic link parent directory.", nameof(linkPath));

        return NormalizePath(Path.Combine(linkParentPath, linkTarget));
    }

    /// <summary>
    /// 디렉터리 심볼릭 링크가 지정한 대상을 가리키는지 확인합니다.
    /// </summary>
    /// <param name="linkPath">확인할 심볼릭 링크 경로입니다.</param>
    /// <param name="expectedTargetPath">기대하는 대상 경로입니다.</param>
    /// <returns>같은 대상을 가리키면 <see langword="true"/>를 반환합니다.</returns>
    /// <exception cref="ArgumentNullException">
    /// 내부적으로 사용하는 경로 값이 <see langword="null"/>인 경우 발생할 수 있습니다.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// 링크 대상 해석 또는 경로 정규화에 실패한 경우 발생할 수 있습니다.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// 전달된 경로 형식이 현재 시스템에서 지원되지 않는 경우 발생할 수 있습니다.
    /// </exception>
    /// <exception cref="PathTooLongException">
    /// 경로 정규화 중 길이 제한을 초과하면 발생할 수 있습니다.
    /// </exception>
    public static bool IsDirectorySymbolicLinkPointingTo(string linkPath, string expectedTargetPath)
    {
        DirectoryInfo linkDirectoryInfo = new(linkPath);
        if (!linkDirectoryInfo.Exists || linkDirectoryInfo.LinkTarget is null)
            return false;

        string resolvedTargetPath = ResolveLinkTargetFullPath(linkPath, linkDirectoryInfo.LinkTarget);
        return IsSamePath(resolvedTargetPath, expectedTargetPath);
    }

    /// <summary>
    /// 디렉터리 심볼릭 링크를 만들거나, 이미 같은 대상을 가리키면 그대로 재사용합니다.
    /// </summary>
    /// <param name="linkPath">생성할 링크 경로입니다.</param>
    /// <param name="targetPath">링크가 가리킬 대상 경로입니다.</param>
    /// <returns>새로 생성했으면 <see langword="true"/>, 기존 링크를 재사용했으면 <see langword="false"/>를 반환합니다.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="linkPath"/>에 이미 다른 링크나 일반 디렉터리, 일반 파일이 존재하는 경우 발생합니다.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// 심볼릭 링크를 만들 권한이 없거나 대상 경로에 쓸 권한이 없는 경우 발생할 수 있습니다.
    /// </exception>
    /// <exception cref="IOException">
    /// 파일 시스템 충돌이나 이미 존재하는 항목 때문에 링크 생성이 실패한 경우 발생할 수 있습니다.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// 현재 파일 시스템이 심볼릭 링크 생성을 지원하지 않는 경우 발생할 수 있습니다.
    /// </exception>
    public static bool CreateOrReuseDirectorySymbolicLink(string linkPath, string targetPath)
    {
        if (Directory.Exists(linkPath) || File.Exists(linkPath))
        {
            if (Directory.Exists(linkPath) && IsDirectorySymbolicLinkPointingTo(linkPath, targetPath))
                return false;

            throw new ArgumentException("심볼릭링크 생성 경로에 이미 LinkTarget이 다른 링크가 존재하거나 일반 파일이 또는 폴더가 존재합니다. 삭제 후 다시 생성 시도하세요.", nameof(linkPath));
        }

        Directory.CreateSymbolicLink(linkPath, targetPath);
        return true;
    }

    /// <summary>
    /// 디렉터리를 하위 항목까지 모두 복사합니다.
    /// </summary>
    /// <param name="sourceDirInfo">복사할 원본 디렉터리 정보입니다.</param>
    /// <param name="deepCopyDestPath">복사 대상 경로입니다.</param>
    /// <exception cref="ArgumentException">
    /// 복사 대상 경로가 잘못되었거나 복사 도중 예외가 발생한 경우 발생합니다.
    /// 실제 원인은 내부 예외에 포함됩니다.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// 원본을 읽거나 대상에 쓸 권한이 없는 경우 내부 작업 중 발생할 수 있습니다.
    /// 현재 구현에서는 내부 예외로 감싸져 전달될 수 있습니다.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// 원본 또는 대상 경로 일부를 찾을 수 없는 경우 내부 작업 중 발생할 수 있습니다.
    /// 현재 구현에서는 내부 예외로 감싸져 전달될 수 있습니다.
    /// </exception>
    /// <exception cref="IOException">
    /// 파일이 사용 중이거나 디스크 I/O 충돌이 있는 경우 내부 작업 중 발생할 수 있습니다.
    /// 현재 구현에서는 내부 예외로 감싸져 전달될 수 있습니다.
    /// </exception>
    public static void DeepCopyDirectory(DirectoryInfo sourceDirInfo, string deepCopyDestPath)
    {
        try
        {
            Directory.CreateDirectory(deepCopyDestPath);

            foreach (FileInfo file in sourceDirInfo.GetFiles())
            {
                string copyDestPath = Path.Combine(deepCopyDestPath, file.Name);
                file.CopyTo(copyDestPath, true);
            }

            foreach (DirectoryInfo subDir in sourceDirInfo.GetDirectories())
            {
                DeepCopyDirectory(subDir, Path.Combine(deepCopyDestPath, subDir.Name));
            }
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"An exception occurred while deep copy {sourceDirInfo.FullName} -> {deepCopyDestPath}", ex);
        }
    }
}
