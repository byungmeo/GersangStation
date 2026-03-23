using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using static GerSDK.GamePathChecker;

namespace GerSDK;

// 참고사항: TempFiles처럼 Symbol 이라서 문제가 생기는건 있어도, 그냥 복사해서 문제가 생기지는 않는다.
// 따라서, 기본 정책은 복사-붙여넣기고, 특별하게 지정된 폴더 또는 파일만 복사를 건너뛰거나 Symbol로 만든다.

/// <summary>
/// 
/// </summary>
public static class ClientCreator
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="sourceGamePath"></param>
    /// <param name="destGamePath"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    public static void CreateSymbolClient(string sourceGamePath, string destGamePath)
    {
        // 게임 설치 시 기본적으로 포함되는 폴더 및 파일 (v34200 기준)
        // 폴더: Online, DLL, Assets, XIGNCODE
        // 파일: AutopatchUpdater.exe, Gersang.exe, gersang.gcs, Korean.gts, Run.exe, run.ico, system.gcs, system.gts, zlib.dll, GersangKR.ini(서버별로 다름)
        // 하지만, 이는 매번 바뀔 수 있기 때문에 강한 제약은 걸지 않고 DLL 폴더만 확인한다.

        // CreateSymbolClient에서 다루는 GamePath 정책
        // 1. GamePath는 DLL 폴더가 심볼릭 링크인지 여부에 따라 SourceGamePath와 SymbolGamePath로 구분한다.
        // 2. SourceGamePath는 언제나 존재하며, 유효해야 한다. (DLL 폴더가 일반적인 형태로 존재해야 하며, Run.exe 파일이 존재해야 한다.)
        // 3. SymbolGamePath는 존재하지 않을 수 있으며, 이미 존재할 경우 SymbolGamePath로 구분될 수 있는 조건을 충족해야 한다.
        // 4. 단, SymbolGamePath에 DLL 폴더가 꼭 존재해야 할 이유는 없다. 어차피 생성 할 것이기 때문이다.

        //
        // SourceGamePath 검증
        //
        if (sourceGamePath is null)
            throw new ArgumentNullException(nameof(sourceGamePath));

        if (string.IsNullOrWhiteSpace(sourceGamePath))
            throw new ArgumentException("SourceGamePath is empty or consists only of white-space characters", nameof(sourceGamePath));

        try
        {
            sourceGamePath = Path.GetFullPath(sourceGamePath);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Failed get SourceGame full path", nameof(sourceGamePath), ex);
        }

        if (!Directory.Exists(sourceGamePath))
            throw new ArgumentException("SourceGamePath must be exists.", nameof(sourceGamePath));

        string sourceRequiredDirectoryName = "DLL";
        string sourceRequiredDirectoryPath = Path.Combine(sourceGamePath, sourceRequiredDirectoryName);

        DirectoryInfo sourceRequiredDirectoryInfo;
        try
        {
            sourceRequiredDirectoryInfo = new DirectoryInfo(sourceRequiredDirectoryPath);
        }
        catch (Exception ex)
        {
            // 예방 못 한 예외:
            //   T:System.Security.SecurityException:
            //     The caller does not have the required permission.
            throw new ArgumentException($"An exception occurred while creating directoryInfo with sourceGamePath\\{sourceRequiredDirectoryName}", nameof(sourceGamePath), ex);
        }

        if (!sourceRequiredDirectoryInfo.Exists)
            throw new ArgumentException($"SourceGamePath must contained {sourceRequiredDirectoryName} directory.", nameof(sourceGamePath));
        if (sourceRequiredDirectoryInfo.LinkTarget is not null)
            throw new ArgumentException($"SourceGamePath contained {sourceRequiredDirectoryName} must not be symlink.", nameof(sourceGamePath));

        string sourceRequiredFileName = "Run.exe";
        string sourceRequiredFilePath = Path.Combine(sourceGamePath, sourceRequiredFileName);

        FileInfo requiredFileInfo;
        try
        {
            requiredFileInfo = new FileInfo(sourceRequiredFilePath);
        }
        catch (Exception ex)
        {
            // 예방 못 한 예외:
            //   T:System.Security.SecurityException:
            //     The caller does not have the required permission.
            throw new ArgumentException($"An exception occurred while creating directoryInfo with sourceGamePath\\{sourceRequiredFileName}", nameof(sourceGamePath), ex);
        }

        if (!requiredFileInfo.Exists)
            throw new ArgumentException($"SourceGamePath must contained {sourceRequiredFileName} file.", nameof(sourceGamePath));

        //
        // DestGamePath 검증
        //
        if (destGamePath is null)
            throw new ArgumentNullException(nameof(destGamePath));

        if (string.IsNullOrWhiteSpace(destGamePath))
            throw new ArgumentException("DestGamePath is empty or consists only of white-space characters", nameof(destGamePath));

        try
        {
            destGamePath = Path.GetFullPath(destGamePath);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Failed get DestGame full path", nameof(destGamePath), ex);
        }

        // 두 경로가 동일할 수 없음
        if (sourceGamePath.Equals(destGamePath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("DestGamePath == SourceGamePath", nameof(destGamePath));

        // 두 경로는 부모/자식 관계가 될 수 없음 (무한 재귀 복사 방지)
        if (IsSameOrNestedPath(sourceGamePath, destGamePath) || IsSameOrNestedPath(destGamePath, sourceGamePath))
        {
            throw new ArgumentException(
                "SourceGamePath and DestGamePath must not be the same path or nested paths.",
                nameof(destGamePath));
        }

        if (File.Exists(destGamePath))
        {
            throw new ArgumentException("DestGamePath must not be file path", nameof(destGamePath));
        }

        try
        {
            if (!IsSupportedSymlink(destGamePath))
            {
                throw new NotSupportedException("DestGamePath not supported symlink");
            }
        }
        catch (Win32Exception ex)
        {
            throw new ArgumentException("An exception occurred while checking if DestGamePath supports Symlink.", nameof(destGamePath), ex);
        }

        if (Directory.Exists(destGamePath))
        {
            string destRequiredDirectoryName = "DLL";
            string destRequiredDirectoryPath = Path.Combine(destGamePath, destRequiredDirectoryName);
            DirectoryInfo destRequiredDirectoryInfo;
            try
            {
                destRequiredDirectoryInfo = new DirectoryInfo(destRequiredDirectoryPath);
            }
            catch (Exception ex)
            {
                // 예방 못 한 예외:
                //   T:System.Security.SecurityException:
                //     The caller does not have the required permission.
                throw new ArgumentException($"An exception occurred while creating directoryInfo with destGamePath\\{destRequiredDirectoryName}", nameof(destGamePath), ex);
            }
            if (destRequiredDirectoryInfo.Exists && destRequiredDirectoryInfo.LinkTarget is null)
                throw new ArgumentException($"DestGamePath contained {destRequiredDirectoryName} must be symlink or not exists.", nameof(destGamePath));
        }

        //
        // 다클라 생성
        //
        DirectoryInfo destDirInfo;
        try
        {
            destDirInfo = Directory.CreateDirectory(destGamePath);
        }
        catch (Exception ex)
        {
            // 예방 못 한 예외:
            //   T:UnauthorizedAccessException:
            //     사용자에게 디렉터리를 만들 권한이 없는 경우
            throw new ArgumentException($"An exception occurred while creating directory with destGamePath", nameof(destGamePath), ex);
        }

        HashSet<string> excludeExtensions = new(StringComparer.OrdinalIgnoreCase) { ".bmp", ".dmp", ".tmp" };
        DirectoryInfo sourceDirInfo;
        try
        {
            // 게임 폴더 최상위에 있는 파일들은 일부 확장자를 제외하고 모두 복사한다
            sourceDirInfo = new(sourceGamePath);
            foreach (FileInfo file in sourceDirInfo.GetFiles())
            {
                if (!excludeExtensions.Contains(file.Extension))
                {
                    string copyDestPath = Path.Combine(destGamePath, file.Name);
                    file.CopyTo(copyDestPath, true);
                }
            }
        }
        catch (Exception ex)
        {
            // 예방 못 한 예외:
            //   T:UnauthorizedAccessException:
            //     대상 경로가 디렉터리이거나 파일 쓰기 권한 부족 또는 기존 파일이 읽기 전용일 때 발생
            //   T:System.Security.SecurityException:
            //     호출자에게 필요한 권한이 없는 경우
            throw new ArgumentException($"An exception occurred while copy files in top level game path", nameof(destGamePath), ex);
        }

        // 특수한 복사 규칙을 가진 폴더들을 정의
        Dictionary<string, Action<DirectoryInfo, string>> customTargets = new(StringComparer.OrdinalIgnoreCase)
        {
            // Assets 폴더는 안의 파일들은 모두 덮어쓰기 복사하고, Config 폴더는 깊은 복사, 나머지는 심볼릭 링크 생성
            {"Assets", (sourceDirInfo, destPath) =>
                {
                    try
                    {
                        Directory.CreateDirectory(destPath);

                        foreach (var file in sourceDirInfo.GetFiles())
                        {
                            string copyDestPath = Path.Combine(destPath, file.Name);
                            file.CopyTo(copyDestPath, true);
                        }

                        foreach (var subDir in sourceDirInfo.GetDirectories())
                        {
                            string destSubDirPath = Path.Combine(destPath, subDir.Name);
                            if (subDir.Name.Equals("Config", StringComparison.OrdinalIgnoreCase))
                            {
                                DirectoryInfo destConfigDirInfo = Directory.CreateDirectory(destSubDirPath);
                                DeepCopy(subDir, destConfigDirInfo.FullName);
                            }
                            else
                            {
                                if (Directory.Exists(destSubDirPath))
                                {
                                    DirectoryInfo symbolDestDirInfo = new(destSubDirPath);
                                    if (symbolDestDirInfo.LinkTarget is not null &&
                                        IsSamePath(ResolveLinkTargetFullPath(destSubDirPath, symbolDestDirInfo.LinkTarget), subDir.FullName))
                                    {
                                        // 만약, 이미 동일한 대상을 가리키고 있는 심볼릭 링크라면 스킵한다
                                        continue;
                                    }
                                    else
                                    {
                                        // 그렇지 않다면 예외를 던진다. (유일한 방법은 삭제이지만 너무 위험함)
                                        throw new ArgumentException("심볼릭링크 생성 경로에 이미 LinkTarget이 다른 링크가 존재하거나 일반 파일이 또는 폴더가 존재합니다. 삭제 후 다시 생성 시도하세요.");
                                    }
                                }
                                Directory.CreateSymbolicLink(destSubDirPath, subDir.FullName);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException($"An exception occurred while custom copy {sourceDirInfo.FullName} -> {destPath}", ex);
                    }
                }
            }
        };

        // 심볼릭 링크로 생성하면 되는 폴더들을 정의
        HashSet<string> symbolTargets = new(StringComparer.OrdinalIgnoreCase) { "DLL", "Online" };
        foreach (DirectoryInfo dir in sourceDirInfo.GetDirectories())
        {
            string destPath = Path.Combine(destGamePath, dir.Name);
            if (customTargets.TryGetValue(dir.Name, out Action<DirectoryInfo, string>? value))
            {
                value(dir, destPath);
            }
            else if (symbolTargets.Contains(dir.Name))
            {
                if (Directory.Exists(destPath))
                {
                    DirectoryInfo symbolDestDirInfo = new(destPath);
                    if (symbolDestDirInfo.LinkTarget is not null &&
                        IsSamePath(ResolveLinkTargetFullPath(destPath, symbolDestDirInfo.LinkTarget), dir.FullName))
                    {
                        // 만약, 이미 동일한 대상을 가리키고 있는 심볼릭 링크라면 스킵한다
                        continue;
                    }
                    else
                    {
                        // 그렇지 않다면 예외를 던진다. (유일한 방법은 삭제이지만 너무 위험함)
                        throw new ArgumentException("심볼릭링크 생성 경로에 이미 LinkTarget이 다른 링크가 존재하거나 일반 파일이 또는 폴더가 존재합니다. 삭제 후 다시 생성 시도하세요.");
                    }
                }

                try
                {
                    Directory.CreateSymbolicLink(destPath, dir.FullName);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"An exception occurred while create symbol {dir.FullName} -> {destPath}", ex);
                }
            }
            else
            {
                // 별도로 표시하지 않았으면 무조건 깊은 복사
                DeepCopy(dir, destPath);
            }
        }
    }

    /// <summary>
    /// 원본 경로와 대상 경로가 부모/자식 관계인지 여부를 확인
    /// </summary>
    /// <param name="basePath"></param>
    /// <param name="otherPath"></param>
    /// <returns></returns>
    static bool IsSameOrNestedPath(string basePath, string otherPath)
    {
        string normalizedBase = Path.TrimEndingDirectorySeparator(Path.GetFullPath(basePath));
        string normalizedOther = Path.TrimEndingDirectorySeparator(Path.GetFullPath(otherPath));

        if (normalizedBase.Equals(normalizedOther, StringComparison.OrdinalIgnoreCase))
            return true;

        return normalizedOther.StartsWith(
            normalizedBase + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 두 경로가 동일한 실제 경로인지 확인
    /// </summary>
    /// <param name="path1"></param>
    /// <param name="path2"></param>
    /// <returns></returns>
    static bool IsSamePath(string path1, string path2)
    {
        string normalizedPath1 = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path1));
        string normalizedPath2 = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path2));

        return normalizedPath1.Equals(normalizedPath2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 심볼릭 링크의 LinkTarget을 절대 경로로 정규화
    /// </summary>
    /// <param name="linkPath"></param>
    /// <param name="linkTarget"></param>
    /// <returns></returns>
    static string ResolveLinkTargetFullPath(string linkPath, string linkTarget)
    {
        if (Path.IsPathFullyQualified(linkTarget))
            return Path.GetFullPath(linkTarget);

        string? linkParentPath = Path.GetDirectoryName(linkPath);
        if (linkParentPath is null)
            throw new ArgumentException("Failed to resolve symbolic link parent directory.", nameof(linkPath));

        return Path.GetFullPath(Path.Combine(linkParentPath, linkTarget));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sourceDirInfo"></param>
    /// <param name="deepCopyDestPath"></param>
    /// <exception cref="ArgumentException"></exception>
    static void DeepCopy(DirectoryInfo sourceDirInfo, string deepCopyDestPath)
    {
        try
        {
            Directory.CreateDirectory(deepCopyDestPath);

            foreach (var file in sourceDirInfo.GetFiles())
            {
                string copyDestPath = Path.Combine(deepCopyDestPath, file.Name);
                file.CopyTo(copyDestPath, true);
            }

            foreach (var subDir in sourceDirInfo.GetDirectories())
            {
                DeepCopy(subDir, Path.Combine(deepCopyDestPath, subDir.Name));
            }
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"An exception occurred while deep copy {sourceDirInfo.FullName} -> {deepCopyDestPath}", ex);
        }
    }
}
