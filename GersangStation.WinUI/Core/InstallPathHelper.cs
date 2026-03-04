using Core.Models;

namespace Core
{
    public static class InstallPathHelper
    {
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

        public static bool IsValidInstallPath(GameServer server, string installPath, bool isOrgPath, bool useSymbol, out string reason)
        {
            reason = "유효한 경로";

            string path = installPath.Trim();
            if (path.Length == 0)
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

            string serverIni = Path.Combine(path, GameServerHelper.GetServerFileName(server));
            if (!File.Exists(serverIni))
            {
                reason = $"❌ 선택한 경로는 {GameServerHelper.GetServerDisplayName(server)} 경로가 아닙니다.";
                return false;
            }

            string charDir = Path.Combine(path, "char");
            if (!Directory.Exists(charDir))
            {
                reason = @"❌ 거상 기본 폴더(\char)를 찾지 못했습니다.";
                return false;
            }

            if (isOrgPath)
            {
                if (IsSymbolDirectory(charDir))
                {
                    reason = "❌ 메인 거상 경로가 아닙니다. 다클라 경로는 메인 경로가 될 수 없습니다.";
                    return false;
                }
            }
            else
            {
                if (useSymbol && !IsSymbolDirectory(charDir))
                {
                    reason = "❌ 올바르지 않은 다클라 폴더입니다. 다클라 폴더가 맞는지 다시 확인해주세요.";
                    return false;
                }
            }

            return true;
        }
    }
}
