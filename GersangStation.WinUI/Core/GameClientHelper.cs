using Core.Models;
using System.Diagnostics;

namespace Core;

public static class GameClientHelper
{
    public const int MultiClientLayoutBoundaryVersion = 34100;

    public enum MultiClientLayoutPolicy
    {
        Legacy = 0,
        V34100OrLater = 1
    }

    /// <summary>
    /// 현재 경로가 속해있는 드라이브의 포맷이 SymbolicLink를 사용할 수 있는지 여부를 반환합니다.
    /// </summary>
    /// <param name="anyPathInDrive">SymbolicLink 사용 가능 여부를 알고싶은 경로 문자열</param>
    /// <param name="resolvedFormat">드라이브 포맷 문자열</param>
    /// <returns>SymbolicLink 사용이 가능하면 true를 반환합니다.</returns>
    public static bool CanUseSymbol(string anyPathInDrive, out string resolvedFormat)
    {
        resolvedFormat = "알 수 없음";

        try
        {
            string root = Path.GetPathRoot(anyPathInDrive) ?? "";
            if (root.Length == 0) return false;

            var di = new DriveInfo(root);
            resolvedFormat = (di.DriveFormat ?? "").ToUpperInvariant();
            return resolvedFormat == "NTFS" || resolvedFormat == "UDF";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 특정 폴더가 SymbolicLink 방식으로 생선된 것인지 여부를 반환합니다.
    /// </summary>
    /// <param name="dirPath">확인하고 싶은 폴더 경로</param>
    /// <returns>SymbolicLink라면 true를 반환합니다.</returns>
    public static bool IsSymbolDirectory(string dirPath)
    {
        try
        {
            var di = new DirectoryInfo(dirPath);
            return (di.Attributes & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 특정 원본 클라이언트 경로가 현재 문맥에서 유효한지 여부를 반환합니다.
    /// </summary>
    /// <param name="server">게임 서버 종류</param>
    /// <param name="installPath">클라이언트 경로(원본만 가능)</param>
    /// <param name="reason">유효성 검사 성공 또는 실패 사유</param>
    /// <returns></returns>
    public static bool IsValidInstallPath(GameServer server, string installPath, out string reason)
    {
        return IsValidInstallPathCore(server, installPath, allowSymbolDirectory: false, out reason);
    }

    public static bool IsValidInstallPath(string installPath, out string reason)
        => IsValidInstallPathCore(server: null, installPath, allowSymbolDirectory: false, out reason);

    /// <summary>
    /// 복제 클라이언트 경로가 현재 문맥에서 유효한지 여부를 반환합니다.
    /// </summary>
    public static bool IsValidCloneInstallPath(GameServer server, string installPath, out string reason)
        => IsValidInstallPathCore(server, installPath, allowSymbolDirectory: true, out reason);

    private static bool IsValidInstallPathCore(GameServer? server, string installPath, bool allowSymbolDirectory, out string reason)
    {
        reason = string.Empty;
        string path = installPath.Trim();
        if (string.IsNullOrEmpty(path))
        {
            reason = "❌ 설치 경로가 비어있습니다.";
            return false;
        }

        if (!Directory.Exists(path))
        {
            reason = "❌ 존재하지 않는 폴더입니다.";
            return false;
        }

        string runExe = Path.Combine(path, "Run.exe");
        if (!File.Exists(runExe))
        {
            reason = "❌ 거상 실행 파일(Run.exe)을 찾지 못했습니다.";
            return false;
        }

        string onlineMapDir = Path.Combine(path, "Online", "Map");
        if (!Directory.Exists(onlineMapDir))
        {
            reason = @"❌ 거상 기본 폴더(\Online\Map)를 찾지 못했습니다.";
            return false;
        }

        if (!allowSymbolDirectory && IsSymbolDirectory(onlineMapDir))
        {
            reason = "❌ 메인 거상 경로가 아닙니다. 다클라 경로는 메인 경로가 될 수 없습니다.";
            return false;
        }

        if (server is GameServer gameServer)
        {
            string serverIni = Path.Combine(path, GameServerHelper.GetServerFileName(gameServer));
            if (!File.Exists(serverIni))
            {
                reason = $"❌ 선택한 경로는 {GameServerHelper.GetServerDisplayName(gameServer)} 경로가 아닙니다.";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 특정 폴더를 목적 경로에 복사합니다.
    /// </summary>
    /// <param name="sourceDirPath">원본 경로</param>
    /// <param name="destDirPath">목적 경로</param>
    /// <param name="copySubDirs">하위 폴더까지 재귀적으로 복사할지 여부</param>
    /// <exception cref="DirectoryNotFoundException"></exception>
    private static void HardCopyDirectory(string sourceDirPath, string destDirPath, bool copySubDirs)
        => HardCopyDirectory(sourceDirPath, destDirPath, copySubDirs, overwriteExistingFiles: true);

    /// <summary>
    /// 특정 폴더를 목적 경로에 복사하되 파일 덮어쓰기 정책을 선택적으로 적용합니다.
    /// </summary>
    private static void HardCopyDirectory(string sourceDirPath, string destDirPath, bool copySubDirs, bool overwriteExistingFiles)
    {
        DirectoryInfo dir = new DirectoryInfo(sourceDirPath);

        if (!dir.Exists)
            throw new DirectoryNotFoundException(
                "Source directory does not exist or could not be found: "
                + sourceDirPath);

        DirectoryInfo[] dirs = dir.GetDirectories();
        EnsureRealDirectory(destDirPath);

        FileInfo[] files = dir.GetFiles();
        foreach (FileInfo file in files)
        {
            string tempPath = Path.Combine(destDirPath, file.Name);
            if (!overwriteExistingFiles && File.Exists(tempPath))
                continue;

            file.CopyTo(tempPath, overwriteExistingFiles);
        }

        if (copySubDirs)
        {
            foreach (DirectoryInfo subdir in dirs)
            {
                string tempPath = Path.Combine(destDirPath, subdir.Name);
                HardCopyDirectory(subdir.FullName, tempPath, copySubDirs, overwriteExistingFiles);
            }
        }
    }

    /// <summary>
    /// 실 디렉터리가 필요할 때 기존 심볼릭 링크를 제거하고 실제 디렉터리를 보장합니다.
    /// </summary>
    private static void EnsureRealDirectory(string dirPath)
    {
        if (Directory.Exists(dirPath) && IsSymbolDirectory(dirPath))
            Directory.Delete(dirPath);
        else if (File.Exists(dirPath))
            File.Delete(dirPath);

        Directory.CreateDirectory(dirPath);
    }

    /// <summary>
    /// 기존 대상이 있으면 정리한 뒤 디렉터리 심볼릭 링크를 다시 만듭니다.
    /// </summary>
    private static void RecreateSymbolicDirectory(string sourceDirPath, string destDirPath)
    {
        if (Directory.Exists(destDirPath))
            Directory.Delete(destDirPath);
        else if (File.Exists(destDirPath))
            File.Delete(destDirPath);

        Directory.CreateSymbolicLink(destDirPath, sourceDirPath);
    }

    /// <summary>
    /// 34100 이후 레이아웃에서 Assets\Config만 설정파일 덮어쓰기 정책을 적용해 재구성합니다.
    /// </summary>
    private static void CopyAssetsDirectoryWithConfigPolicy(string sourceAssetsPath, string destAssetsPath, bool overwriteConfig)
    {
        EnsureRealDirectory(destAssetsPath);

        foreach (string sourceFilePath in Directory.GetFiles(sourceAssetsPath))
        {
            string fileName = Path.GetFileName(sourceFilePath);
            string destFilePath = Path.Combine(destAssetsPath, fileName);
            File.Copy(sourceFilePath, destFilePath, overwrite: true);
        }

        foreach (string sourceDirPath in Directory.GetDirectories(sourceAssetsPath))
        {
            string dirName = new DirectoryInfo(sourceDirPath).Name;
            string destDirPath = Path.Combine(destAssetsPath, dirName);

            if (dirName.Equals("Config", StringComparison.OrdinalIgnoreCase))
            {
                HardCopyDirectory(sourceDirPath, destDirPath, copySubDirs: true, overwriteExistingFiles: overwriteConfig);
                continue;
            }

            RecreateSymbolicDirectory(sourceDirPath, destDirPath);
        }
    }

    /*
     * Symbol 방식 다클라 생성 정책
     * 1. 목적지 경로가 비어있지 않고 Online\Map이 Symbol이 아니라면 실패(삭제 안내)
     * 2. 최상위 폴더 내 파일들은 모두 HardCopy(.tmp, .bmp, .dmp 확장자 파일은 아예 무시)
     * 3. XIGONCODE 폴더는 안의 내용물 포함 모두 HardCopy
     * 4. Legacy 정책: Online 직접 파일은 설정파일만 overwriteConfig를 따르고, 하위 폴더는 Symbol
     * 5. 34100+ 정책: Online 직접 파일은 모두 HardCopy 덮어쓰기, Assets\Config 전체는 overwriteConfig 정책으로 HardCopy
    */

    private static bool CreateSymbolClient(string orgInstallPath, string destPath, bool overwriteConfig, MultiClientLayoutPolicy layoutPolicy, out string reason)
    {
        reason = string.Empty;

        #region Validation
        orgInstallPath = orgInstallPath.Trim();
        if (string.IsNullOrEmpty(orgInstallPath) || !IsValidInstallPath(orgInstallPath, out reason))
        {
            reason = $"유효하지 않은 메인 클라 경로: {reason}";
            return false;
        }

        destPath = destPath.Trim();
        if (string.IsNullOrEmpty(destPath)) {
            reason = $"유효하지 않은 목적지 경로";
            return false;
        }

        if (!CanUseSymbol(destPath, out _))
        {
            reason = "Symbol을 지원하지 않는 드라이브";
            return false;
        }

        string destOnlineMapPath = Path.Combine(destPath, "Online", "Map");
        if (Directory.Exists(destOnlineMapPath) && !IsSymbolDirectory(destOnlineMapPath))
        {
            reason = "이미 경로에 복사-붙여넣기로 생성한 클라이언트 존재";
            return false;
        }

        string orgOnlinePath = orgInstallPath + @"\Online";
        if (!Directory.Exists(orgOnlinePath))
        {
            reason = "메인 클라 경로에 Online 폴더가 없음";
            return false;
        }
        #endregion Validation

        Directory.CreateDirectory(destPath);

        // Online 정책
        {
            string destOnlinePath = destPath + @"\Online";
            Directory.CreateDirectory(destOnlinePath);

            // 파일들은 모두 HardCopy (설정 파일은 덮어쓰기 정책을 따름)
            foreach (var orgFilePath in Directory.GetFiles(orgOnlinePath))
            {
                string fileName = orgFilePath.Substring(orgFilePath.LastIndexOf('\\')); // {path}\{fileName} -> \{fileName}
                string destFilePath = destOnlinePath + fileName;
                bool overwrite = layoutPolicy == MultiClientLayoutPolicy.V34100OrLater;
                if (layoutPolicy == MultiClientLayoutPolicy.Legacy)
                {
                    bool isConfig = fileName is @"\CombineInfo.txt.txt" or @"\PetSetting.dat" or @"\AKinteractive.cfg";
                    overwrite = !isConfig || overwriteConfig;
                }

                if (overwrite is false && File.Exists(destFilePath))
                    continue;
                File.Copy(orgFilePath, destFilePath, overwrite);
            }

            // 폴더는 모두 Symbol로 생성
            foreach (var eachDirPath in Directory.GetDirectories(orgOnlinePath))
            {
                string? dirName = new DirectoryInfo(eachDirPath).Name;
                string destDirPath = $"{destOnlinePath}\\{dirName}";
                RecreateSymbolicDirectory(eachDirPath, destDirPath);
            }
        }

        // XIGNCODE 정책
        HardCopyDirectory(orgInstallPath + @"\XIGNCODE", destPath + @"\XIGNCODE", true);

        // 최상위 폴더 정책
        {
            // XIGNCODE와 Online폴더를 제외한 모든 폴더 복사 또는 심볼 생성
            foreach (string eachDirPath in Directory.GetDirectories(orgInstallPath))
            {
                string? dirName = new DirectoryInfo(eachDirPath).Name;
                if (dirName == "XIGNCODE" || dirName == "Online")
                    continue;

                string destDirPath = $"{destPath}\\{dirName}";
                if (layoutPolicy == MultiClientLayoutPolicy.V34100OrLater
                    && dirName == "Assets")
                {
                    CopyAssetsDirectoryWithConfigPolicy(eachDirPath, destDirPath, overwriteConfig);
                    continue;
                }

                RecreateSymbolicDirectory(eachDirPath, destDirPath);
            }

            // 최상위 폴더 내 파일들은 모두 HardCopy (config.ln 파일은 설정파일 덮어쓰기 정책을 따름, 임시 및 사진 파일 제외)
            foreach (string orgFilePath in Directory.GetFiles(orgInstallPath))
            {
                string fileName = orgFilePath.Substring(orgFilePath.LastIndexOf('\\')); // {path}\{fileName} -> \{fileName}
                string extension = Path.GetExtension(orgFilePath); // '.' 포함
                if (extension == ".tmp" || extension == ".bmp" || extension == ".dmp")
                    continue;
                string destFilePath = destPath + fileName;
                bool overwrite = layoutPolicy == MultiClientLayoutPolicy.V34100OrLater;
                if (layoutPolicy == MultiClientLayoutPolicy.Legacy)
                {
                    bool isConfig = fileName is @"\config.ln";
                    overwrite = !isConfig || overwriteConfig;
                }

                if (overwrite is false && File.Exists(destFilePath))
                    continue;
                File.Copy(orgFilePath, destFilePath, overwrite);
            }
        }

        return true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="installPath"></param>
    /// <param name="useSymbol"></param>
    /// <param name="destPath2">2번 클라 생성 경로</param>
    /// <param name="destPath3">3번 클라 생성 경로</param>
    public static bool CreateSymbolMultiClient(CreateSymbolMultiClientArgs args, out string reason)
    {
        reason = string.Empty;

        if (string.IsNullOrEmpty(args.InstallPath) || !IsValidInstallPath(args.InstallPath, out reason))
        {
            reason = $"올바르지 않은 메인 클라 경로: {reason}";
            return false;
        }

        if (!string.IsNullOrEmpty(args.DestPath2))
        {
            bool success = CreateSymbolClient(args.InstallPath, args.DestPath2, args.OverwriteConfig, args.LayoutPolicy, out reason);
            if (!success)
            {
                reason = $"2클라 생성 실패: {reason}";
                return false;
            }
        }
            
        if (!string.IsNullOrEmpty(args.DestPath3))
        {
            bool success = CreateSymbolClient(args.InstallPath, args.DestPath3, args.OverwriteConfig, args.LayoutPolicy, out reason);
            if (!success)
            {
                reason = $"3클라 생성 실패: {reason}";
                return false;
            }
        }

        return true;
    }
}

public sealed class CreateSymbolMultiClientArgs
{
    public string InstallPath { get; set; } = string.Empty;
    public string DestPath2 { get; set; } = string.Empty;
    public string DestPath3 { get; set; } = string.Empty;
    public bool OverwriteConfig { get; set; }
    public GameClientHelper.MultiClientLayoutPolicy LayoutPolicy { get; set; } = GameClientHelper.MultiClientLayoutPolicy.Legacy;
}
