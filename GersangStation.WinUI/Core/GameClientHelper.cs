using Core.Models;

namespace Core
{
    public static class GameClientHelper
    {
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
            reason = string.Empty;
            string path = installPath.Trim();
            bool success = IsValidInstallPath(installPath, out reason);
            if (!success)
                return false;

            string serverIni = Path.Combine(path, GameServerHelper.GetServerFileName(server));
            if (!File.Exists(serverIni))
            {
                reason = $"❌ 선택한 경로는 {GameServerHelper.GetServerDisplayName(server)} 경로가 아닙니다.";
                return false;
            }

            return true;
        }

        public static bool IsValidInstallPath(string installPath, out string reason)
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

            string charDir = Path.Combine(path, "char");
            if (!Directory.Exists(charDir))
            {
                reason = @"❌ 거상 기본 폴더(\char)를 찾지 못했습니다.";
                return false;
            }

            if (IsSymbolDirectory(charDir))
            {
                reason = "❌ 메인 거상 경로가 아닙니다. 다클라 경로는 메인 경로가 될 수 없습니다.";
                return false;
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
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirPath);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirPath);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the destination directory doesn't exist, create it.       
            Directory.CreateDirectory(destDirPath);

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destDirPath, file.Name);
                file.CopyTo(tempPath, true);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirPath, subdir.Name);
                    HardCopyDirectory(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }

        // 설정 관련 파일 참고
        // config.ln : 음향 설정, 화면 설정, 마우스 설정, 전투 설정 (ESC 메뉴를 닫을 때마다 변경)
        // Online\PetSetting.dat : 펫 설정 (설정 시 변경)
        // Online\AKinteractive.cfg : (클라이언트를 종료할 때 변경)
        // Online\ContentFavorites.dat : (클라이언트를 종료할 때 변경)

        /*
         * Symbol 방식 다클라 생성 정책
         * 1. 목적지 경로가 비어있지 않고 특정 폴더(char)가 Symbol이 아니라면 실패(삭제 안내)
         * 2. 최상위 폴더 내 파일들은 모두 HardCopy(.tmp, .bmp, .dmp 확장자 파일은 아예 무시)
         * 3. XIGONCODE 폴더는 안의 내용물 포함 모두 HardCopy
         * 4. Online 폴더는 안의 직접적으로 포함하고 있는 파일들은 HardCopy(덮어쓰기 금지), 이외 폴더들은 모두 Symbol로
         * 5. 설정 파일 매번 복사 기능 체크 해제 시 \config.ln, Online\AKinteractive.cfg, Online\CombineInfo.txt.txt, Online\PetSetting.dat 파일 덮어쓰기 금지 옵션 주고 HardCopy
        */

        private static bool CreateClient(string orgInstallPath, bool useSymbol, string destPath, bool overwriteConfig, out string reason)
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

            if (CanUseSymbol(destPath, out _))
            {
                reason = "Symbol을 지원하지 않는 드라이브";
                return false;
            }

            string charPath = destPath + @"\char";
            if (useSymbol && Directory.Exists(charPath) && !IsSymbolDirectory(charPath))
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
            string destOnlinePath = destPath + @"\Online";
            Directory.CreateDirectory(destOnlinePath);
            foreach (var orgFilePath in Directory.GetFiles(orgOnlinePath))
            {
                string fileName = orgFilePath.Substring(orgFilePath.LastIndexOf('\\')); // {path}\{fileName} -> \{fileName}
                string destFilePath = destOnlinePath + fileName;
                bool isConfig = fileName is @"\CombineInfo.txt.txt" or @"\PetSetting.dat" or @"\AKinteractive.cfg";
                File.Copy(orgFilePath, destFilePath, !isConfig || overwriteConfig);
            }

            // XIGNCODE 정책
            HardCopyDirectory(orgInstallPath + @"\XIGNCODE", destPath + @"\XIGNCODE", true);

            // 최상위 폴더 정책
            {
                // XIGNCODE와 Online폴더를 제외한 모든 폴더 Symbol로 생성
                foreach (string eachDirPath in Directory.GetDirectories(orgInstallPath))
                {
                    string? dirName = new DirectoryInfo(eachDirPath).Name;
                    if (dirName == "XIGNCODE" || dirName == "Online")
                        continue;
                    string destDirPath = $"{destPath}\\{dirName}";
                    if (Directory.Exists(destDirPath))
                        Directory.Delete(destDirPath);
                    Directory.CreateSymbolicLink(destDirPath, eachDirPath);
                }

                // 최상위 폴더 내 파일들은 모두 HardCopy (config.ln 파일은 설정파일 덮어쓰기 정책을 따름, 임시 및 사진 파일 제외)
                foreach (string orgFilePath in Directory.GetFiles(orgInstallPath))
                {
                    string fileName = orgFilePath.Substring(orgFilePath.LastIndexOf('\\')); // {path}\{fileName} -> \{fileName}
                    string extension = Path.GetExtension(orgFilePath); // '.' 포함
                    if (extension == ".tmp" || extension == ".bmp" || extension == ".dmp")
                        continue;
                    string destFilePath = destPath + fileName;
                    bool isConfig = fileName is @"\config.ln";
                    File.Copy(orgFilePath, destFilePath, !isConfig || overwriteConfig);
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
        public static bool CreateMultiClient(CreateMultiClientArgs args, out string reason)
        {
            reason = string.Empty;

            if (string.IsNullOrEmpty(args.InstallPath) || !IsValidInstallPath(args.InstallPath, out reason))
            {
                reason = $"올바르지 않은 메인 클라 경로: {reason}";
                return false;
            }

            if (!string.IsNullOrEmpty(args.DestPath2))
            {
                bool success = CreateClient(args.InstallPath, args.UseSymbol, args.DestPath2, args.OverwriteConfig, out reason);
                if (!success)
                {
                    reason = $"2클라 생성(복사) 실패: {reason}";
                    return false;
                }
            }
                
            if (!string.IsNullOrEmpty(args.DestPath3))
            {
                bool success = CreateClient(args.InstallPath, args.UseSymbol, args.DestPath3, args.OverwriteConfig, out reason);
                if (!success)
                {
                    reason = $"3클라 생성(복사) 실패: {reason}";
                    return false;
                }
            }

            return true;
        }

        public sealed class CreateMultiClientArgs
        {
            public string InstallPath { get; set; } = string.Empty;
            public string DestPath2 { get; set; } = string.Empty;
            public string DestPath3 { get; set; } = string.Empty;
            public bool UseSymbol { get; set; }
            public bool OverwriteConfig { get; set; }
        }
    }
}
