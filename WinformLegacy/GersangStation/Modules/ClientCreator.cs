using System.Diagnostics;
using static GersangStation.Form1;

namespace GersangStation.Modules;

internal static class ClientCreator {
    public const int ReinstallRequiredBoundaryVersion = 34100;
    public const string ReinstallGuideUrl = "https://github.com/byungmeo/GersangStation/wiki/v34100-%EB%AF%B8%EB%A7%8C-%EB%B2%84%EC%A0%84-%EA%B4%80%EB%A0%A8";
    private const string SymbolGuideUrl = "https://github.com/byungmeo/GersangStation/discussions/39";

    public static bool RequiresReinstallForPatch(string currentVersion, string latestVersion) {
        return TryParseClientVersion(currentVersion, out int current)
            && TryParseClientVersion(latestVersion, out int latest)
            && current < ReinstallRequiredBoundaryVersion
            && latest >= ReinstallRequiredBoundaryVersion;
    }

    public static bool RequiresReinstallForMultiClient(string currentVersion) {
        return TryParseClientVersion(currentVersion, out int current)
            && current < ReinstallRequiredBoundaryVersion;
    }

    public static bool TryParseClientVersion(string versionText, out int version) {
        return int.TryParse(versionText, out version) && version >= 0;
    }

    public static void ShowReinstallRequiredDialog(Form owner, string message, string title) {
        InvokeUi(owner, () => {
            DialogResult dr = MessageBox.Show(owner,
                message + "\n\n안내 페이지를 여시겠습니까?",
                title,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if(dr == DialogResult.Yes) {
                OpenUrl(ReinstallGuideUrl);
            }
        });
    }

    public static bool IsValidPath(Form owner, string path, bool isOrg, Server server) {
        if(IsValidPath(path, isOrg, server, out string reason)) {
            return true;
        }

        string clientKind = isOrg ? "본클라" : "다클라";
        InvokeUi(owner, () => {
            MessageBox.Show(owner,
                reason + $"\n{path}",
                $"잘못된 {clientKind} 경로",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        });
        return false;
    }

    public static bool IsValidPath(string path, bool isOrg, Server server, out string reason) {
        return IsValidInstallPathCore(server, path, allowSymbolDirectory: !isOrg, out reason);
    }

    public static bool CreateClient(Form owner, string orgPath, string secondPath, string thirdPath, Server server) {
        if(!IsValidPath(owner, orgPath, true, server)) {
            return false;
        }

        string currentVersion = VersionChecker.GetCurrentVersion(owner, orgPath);
        if(string.IsNullOrWhiteSpace(currentVersion)) {
            return false;
        }

        if(RequiresReinstallForMultiClient(currentVersion)) {
            ShowReinstallRequiredDialog(owner,
                $"현재 설치된 거상 클라이언트 버전이 v{ReinstallRequiredBoundaryVersion} 미만입니다."
                + "\n구 다클라 생성 방식은 더 이상 지원되지 않습니다."
                + "\n게임을 재설치한 뒤 다시 생성해주세요.",
                "다클라 생성 방식 변경 안내");
            return false;
        }

        const bool overwriteConfig = false;

        if(!string.IsNullOrWhiteSpace(secondPath)) {
            bool success = CreateSymbolClient(server, orgPath, secondPath, overwriteConfig, out string reason);
            if(!success) {
                ShowCreateClientFailure(owner, reason);
                return false;
            }
        }

        if(!string.IsNullOrWhiteSpace(thirdPath)) {
            bool success = CreateSymbolClient(server, orgPath, thirdPath, overwriteConfig, out string reason);
            if(!success) {
                ShowCreateClientFailure(owner, reason);
                return false;
            }
        }

        Trace.WriteLine("다클라 생성 끝");
        return true;
    }

    private static bool IsValidInstallPathCore(Server? server, string installPath, bool allowSymbolDirectory, out string reason) {
        reason = string.Empty;
        string path = installPath.Trim();
        if(string.IsNullOrEmpty(path)) {
            reason = "설치 경로가 비어있습니다.";
            return false;
        }

        if(!Directory.Exists(path)) {
            reason = "존재하지 않는 폴더입니다.";
            return false;
        }

        string runExe = Path.Combine(path, "Run.exe");
        if(!File.Exists(runExe)) {
            reason = "경로에 거상 실행 파일(Run.exe)이 존재하지 않습니다.";
            return false;
        }

        string onlineMapDir = Path.Combine(path, "Online", "Map");
        if(!Directory.Exists(onlineMapDir)) {
            reason = @"거상 기본 폴더(\Online\Map)를 찾지 못했습니다.";
            return false;
        }

        if(!allowSymbolDirectory && IsSymbolDirectory(onlineMapDir)) {
            reason = "다클라 경로는 본클라 경로가 될 수 없습니다.";
            return false;
        }

        if(server is Server targetServer) {
            string serverIni = Path.Combine(path, GetServerFileName(targetServer));
            if(!File.Exists(serverIni)) {
                reason = $"선택한 경로는 {GetServerDisplayName(targetServer)} 경로가 아닙니다.";
                return false;
            }
        }

        return true;
    }

    private static bool CanUseSymbol(string anyPathInDrive, out string resolvedFormat) {
        resolvedFormat = "알 수 없음";

        try {
            string root = Path.GetPathRoot(anyPathInDrive) ?? string.Empty;
            if(root.Length == 0) {
                return false;
            }

            DriveInfo di = new DriveInfo(root);
            if(!di.IsReady) {
                return false;
            }

            resolvedFormat = (di.DriveFormat ?? string.Empty).ToUpperInvariant();
            return resolvedFormat == "NTFS" || resolvedFormat == "UDF";
        } catch {
            return false;
        }
    }

    private static bool IsSymbolDirectory(string dirPath) {
        try {
            DirectoryInfo di = new DirectoryInfo(dirPath);
            return (di.Attributes & FileAttributes.ReparsePoint) != 0;
        } catch {
            return false;
        }
    }

    private static void HardCopyDirectory(string sourceDirPath, string destDirPath, bool copySubDirs)
        => HardCopyDirectory(sourceDirPath, destDirPath, copySubDirs, overwriteExistingFiles: true);

    private static void HardCopyDirectory(string sourceDirPath, string destDirPath, bool copySubDirs, bool overwriteExistingFiles) {
        DirectoryInfo dir = new DirectoryInfo(sourceDirPath);

        if(!dir.Exists) {
            throw new DirectoryNotFoundException(
                "Source directory does not exist or could not be found: "
                + sourceDirPath);
        }

        DirectoryInfo[] dirs = dir.GetDirectories();
        EnsureRealDirectory(destDirPath);

        FileInfo[] files = dir.GetFiles();
        foreach(FileInfo file in files) {
            string tempPath = Path.Combine(destDirPath, file.Name);
            if(!overwriteExistingFiles && File.Exists(tempPath)) {
                continue;
            }

            file.CopyTo(tempPath, overwriteExistingFiles);
        }

        if(copySubDirs) {
            foreach(DirectoryInfo subdir in dirs) {
                string tempPath = Path.Combine(destDirPath, subdir.Name);
                HardCopyDirectory(subdir.FullName, tempPath, copySubDirs, overwriteExistingFiles);
            }
        }
    }

    private static void EnsureRealDirectory(string dirPath) {
        if(Directory.Exists(dirPath) && IsSymbolDirectory(dirPath)) {
            Directory.Delete(dirPath);
        } else if(File.Exists(dirPath)) {
            File.Delete(dirPath);
        }

        Directory.CreateDirectory(dirPath);
    }

    private static void RecreateSymbolicDirectory(string sourceDirPath, string destDirPath) {
        if(Directory.Exists(destDirPath)) {
            Directory.Delete(destDirPath);
        } else if(File.Exists(destDirPath)) {
            File.Delete(destDirPath);
        }

        Directory.CreateSymbolicLink(destDirPath, sourceDirPath);
    }

    private static void CopyAssetsDirectoryWithConfigPolicy(string sourceAssetsPath, string destAssetsPath, bool overwriteConfig) {
        EnsureRealDirectory(destAssetsPath);

        foreach(string sourceFilePath in Directory.GetFiles(sourceAssetsPath)) {
            string fileName = Path.GetFileName(sourceFilePath);
            string destFilePath = Path.Combine(destAssetsPath, fileName);
            File.Copy(sourceFilePath, destFilePath, overwrite: true);
        }

        foreach(string sourceDirPath in Directory.GetDirectories(sourceAssetsPath)) {
            string dirName = new DirectoryInfo(sourceDirPath).Name;
            string destDirPath = Path.Combine(destAssetsPath, dirName);

            if(dirName.Equals("Config", StringComparison.OrdinalIgnoreCase)) {
                HardCopyDirectory(sourceDirPath, destDirPath, copySubDirs: true, overwriteExistingFiles: overwriteConfig);
                continue;
            }

            RecreateSymbolicDirectory(sourceDirPath, destDirPath);
        }
    }

    private static bool CreateSymbolClient(Server server, string orgInstallPath, string destPath, bool overwriteConfig, out string reason) {
        reason = string.Empty;

        orgInstallPath = orgInstallPath.Trim();
        if(!IsValidInstallPathCore(server, orgInstallPath, allowSymbolDirectory: false, out reason)) {
            reason = $"유효하지 않은 본클라 경로: {reason}";
            return false;
        }

        destPath = destPath.Trim();
        if(string.IsNullOrEmpty(destPath)) {
            reason = "유효하지 않은 목적지 경로입니다.";
            return false;
        }

        if(string.Equals(Path.GetFullPath(orgInstallPath), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase)) {
            reason = "본클라와 동일한 경로에는 다클라를 생성할 수 없습니다.";
            return false;
        }

        if(!CanUseSymbol(destPath, out string driveFormat)) {
            reason = $"다클라 생성이 불가능한 드라이브 포맷({driveFormat}) 입니다.";
            return false;
        }

        string orgOnlinePath = Path.Combine(orgInstallPath, "Online");
        if(!Directory.Exists(orgOnlinePath)) {
            reason = "본클라 경로에 Online 폴더가 없습니다.";
            return false;
        }

        string destOnlineMapPath = Path.Combine(destPath, "Online", "Map");
        if(Directory.Exists(destOnlineMapPath) && !IsSymbolDirectory(destOnlineMapPath)) {
            reason = "이미 경로에 복사-붙여넣기로 생성한 클라이언트가 존재합니다. 삭제 후 다시 시도해주세요.";
            return false;
        }

        Directory.CreateDirectory(destPath);

        string destOnlinePath = Path.Combine(destPath, "Online");
        EnsureRealDirectory(destOnlinePath);

        foreach(string orgFilePath in Directory.GetFiles(orgOnlinePath)) {
            string fileName = Path.GetFileName(orgFilePath);
            string destFilePath = Path.Combine(destOnlinePath, fileName);
            File.Copy(orgFilePath, destFilePath, overwrite: true);
        }

        foreach(string eachDirPath in Directory.GetDirectories(orgOnlinePath)) {
            string? dirName = new DirectoryInfo(eachDirPath).Name;
            string destDirPath = Path.Combine(destOnlinePath, dirName);
            RecreateSymbolicDirectory(eachDirPath, destDirPath);
        }

        HardCopyDirectory(Path.Combine(orgInstallPath, "XIGNCODE"), Path.Combine(destPath, "XIGNCODE"), copySubDirs: true);

        foreach(string eachDirPath in Directory.GetDirectories(orgInstallPath)) {
            string? dirName = new DirectoryInfo(eachDirPath).Name;
            if(dirName == "XIGNCODE" || dirName == "Online" || dirName == "TempFiles" || dirName == "GersangDown") {
                continue;
            }

            string destDirPath = Path.Combine(destPath, dirName);
            if(dirName == "Assets") {
                CopyAssetsDirectoryWithConfigPolicy(eachDirPath, destDirPath, overwriteConfig);
                continue;
            }

            RecreateSymbolicDirectory(eachDirPath, destDirPath);
        }

        foreach(string orgFilePath in Directory.GetFiles(orgInstallPath)) {
            string extension = Path.GetExtension(orgFilePath);
            if(extension == ".tmp" || extension == ".bmp" || extension == ".dmp") {
                continue;
            }

            string fileName = Path.GetFileName(orgFilePath);
            string destFilePath = Path.Combine(destPath, fileName);
            File.Copy(orgFilePath, destFilePath, overwrite: true);
        }

        return true;
    }

    private static string GetServerFileName(Server server) {
        return server == Server.Main ? "GerSangKR.ini" : "GerSangKRTest.ini";
    }

    private static string GetServerDisplayName(Server server) {
        return server == Server.Main ? "본섭" : server == Server.Test ? "테섭" : "천라";
    }

    private static void ShowCreateClientFailure(Form owner, string reason) {
        InvokeUi(owner, () => {
            MessageBox.Show(owner,
                reason,
                "다클라 생성 실패",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            if(reason.Contains("드라이브 포맷") || reason.Contains("복사-붙여넣기")) {
                OpenUrl(SymbolGuideUrl);
            }
        });
    }

    private static void InvokeUi(Form owner, Action action) {
        if(owner.IsDisposed) {
            return;
        }

        if(owner.IsHandleCreated && owner.InvokeRequired) {
            owner.BeginInvoke(action);
            return;
        }

        action();
    }

    private static void OpenUrl(string url) {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
