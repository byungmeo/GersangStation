using System.Diagnostics;

namespace GersangStation.Modules;

internal class ClientCreator {
    /// <summary>
    /// 다클라 생성이 가능한 경로(드라이브)인지 검사합니다.
    /// </summary>
    private static bool IsValidDrive(Form owner, string path) {
        DirectoryInfo dirInfo;
        DriveInfo driveInfo;
        try {
            dirInfo = new DirectoryInfo(path);
            driveInfo = new DriveInfo(dirInfo.Root.FullName);
        } catch(Exception ex) {
            owner.BeginInvoke(() => {
                MessageBox.Show(owner,
                    $"{ex}",
                    "드라이브 유효성 확인 실패",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            });
            return false;
        }

        string name = driveInfo.Name;
        DriveType type = driveInfo.DriveType;

        Trace.WriteLine($"Drive Name: {name}");
        Trace.WriteLine($"Drive Type: {type}");
        if(driveInfo.IsReady) {
            string format = driveInfo.DriveFormat.ToUpper();
            Trace.WriteLine($"Drive Format: {format}");

            if(format == "NTFS" || format == "UDF") return true;
            else {
                owner.BeginInvoke(() => {
                    MessageBox.Show(owner, 
                        $"다클라 생성이 불가능한 드라이브 포맷({format}) 입니다.",
                        "다클라 생성(패치 적용) 불가", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);

                    Process.Start(new ProcessStartInfo("https://github.com/byungmeo/GersangStation/discussions/39") { UseShellExecute = true });
                });
                return false;
            }
        } else {
            owner.BeginInvoke(() => {
                MessageBox.Show(owner,
                    $"현재 드라이브 상태가 준비 중이 아닙니다.",
                    "다클라 생성(패치 적용) 불가",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                Process.Start(new ProcessStartInfo("https://github.com/byungmeo/GersangStation/discussions/39") { UseShellExecute = true });
            });
            Trace.WriteLine("The drive is not ready.");
            return false;
        }
    }

    /// <summary>
    /// <para>해당 거상 클라이언트 경로가 유효한지 판단합니다.</para>
    /// <para>원본 클라이언트 경로라면 <paramref name="isOrg"/>를 <see langword="true"/>로,</para>
    /// <para>다클라 경로라면 <see langword="false"/>로 설정 하세요.</para>
    /// </summary>
    public static bool IsValidPath(Form owner, string path, bool isOrg) {
        DirectoryInfo pathInfo = new DirectoryInfo(path + "\\char");
        if(false == Directory.Exists(path)) {
            owner.BeginInvoke(() => {
                MessageBox.Show(owner,
                    "존재하지 않는 경로입니다." +
                    $"\n{path}",
                    $"잘못된 {(isOrg ? "본클라" : "다클라")} 경로",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            });
            return false;
        }

        if(false == File.Exists(Path.Combine(path, "Gersang.exe"))) {
            owner.BeginInvoke(() => {
                MessageBox.Show(owner,
                    "경로에 Gersang.exe 파일이 존재하지 않습니다." +
                    "\n거상 클라이언트 경로가 맞는지 확인해주세요." +
                    $"\n{path}",
                    $"잘못된 {(isOrg ? "본클라" : "다클라")} 경로",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            });
            return false;
        }

        bool useSymbolic = bool.Parse(ConfigManager.GetConfig("use_symbolic"));
        bool isSymbolicClient = false;
        foreach(string dirPath in Directory.GetDirectories(path)) {
            var info = new DirectoryInfo(dirPath);

            if(info.ResolveLinkTarget(false) != null) {
                isSymbolicClient = true;
                break;
            }
        }

        // 본클라 경로가 symlink 방식으로 생성되었다면 유효하지 않음
        if(isOrg && isSymbolicClient) {
            owner.BeginInvoke(() => {
                MessageBox.Show(owner,
                    "본클라 경로가 올바르지 않습니다." +
                    "\n다클 생성 기능을 통해 생성된 클라이언트는 본클라가 될 수 없습니다." +
                    $"\n{path}",
                    "잘못된 본클라 경로",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            });
            return false;
        }

        if(!isOrg) { 
            if(useSymbolic && !isSymbolicClient) {
                // 심볼릭 다클 생성 방식을 사용하고 있는데 다클라 경로가 symlink 방식으로 생성된게 아니라면
                owner.BeginInvoke(() => {
                    MessageBox.Show(owner,
                        "지정된 경로의 클라이언트 생성 방식이 유효하지 않습니다." +
                        "\n심볼릭 다클 생성 옵션이 활성화된 경우 프로그램 내 다클 생성 기능을 통해 생성하서야 합니다." +
                        $"\n{path}",
                        "잘못된 다클라 경로",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Process.Start(new ProcessStartInfo("https://github.com/byungmeo/GersangStation/discussions/60") { UseShellExecute = true });
                });
                return false;
            }

            if(!useSymbolic && isSymbolicClient) {
                // 다클라 생성 기능을 사용하지 않고 있는데 다클라 경로가 symlink 방식으로 생성되었다면
                owner.BeginInvoke(() => {
                    MessageBox.Show(owner,
                        "지정된 경로의 클라이언트 생성 방식이 유효하지 않습니다." +
                        "\n확인 버튼을 누른 뒤 나타나는 페이지를 참고해주세요." +
                        $"\n{path}",
                        "잘못된 다클라 경로",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Process.Start(new ProcessStartInfo("https://github.com/byungmeo/GersangStation/discussions/60") { UseShellExecute = true });
                });
                return false;
            }
        }

        return true;
    }

    //성공 여부를 반환합니다.
    public static bool CreateClient(Form owner, string orgPath, string secondPath, string thirdPath) {
        string orgOnlinePath = orgPath + "\\Online";

        if(false == Directory.Exists(orgPath)) {
            owner.BeginInvoke(() => {
                MessageBox.Show(owner, 
                    "경로를 찾을 수 없습니다.", 
                    "경로 인식 실패", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            });
            return false;
        }

        if(false == IsValidDrive(owner, orgPath)) return false;
        Trace.WriteLine("드라이브 유효성 검사 완료");

        if(false == IsValidPath(owner, orgPath, true)) return false;
        Trace.WriteLine("원본 클라 확인 완료");

        // 거상 폴더 내 파일을 복사하는 로컬 함수
        void copy(string path) {
            foreach(string eachFilePath in Directory.GetFiles(orgPath)) {
                string fileName = eachFilePath.Substring(eachFilePath.LastIndexOf('\\')); // \파일이름
                string extension = Path.GetExtension(eachFilePath); // '.' 포함
                if(extension == ".tmp" || extension == ".bmp" || extension == ".dmp")
                    continue;
                Trace.WriteLine("COPY : " + eachFilePath + " -> " + path + fileName);
                File.Copy(eachFilePath, path + fileName, true);
            }
        }

        // 거상 폴더 내 폴더 심볼릭링크 생성하는 로컬 함수 (XIGNCODE, Online는 별도 복사 또는 생성)
        void symbolic(string path) {
            foreach(string eachDirPath in Directory.GetDirectories(orgPath)) {
                string? dirName = new DirectoryInfo(eachDirPath).Name;
                if(dirName == "XIGNCODE" || dirName == "Online")
                    continue;
                string linkPath = path + '\\' + dirName;
                Trace.WriteLine("SYMLINK_DIR" + linkPath + ", " + eachDirPath);
                if(Directory.Exists(linkPath)) { Directory.Delete(linkPath); }
                Directory.CreateSymbolicLink(linkPath, eachDirPath);
            }

            // XIGNCODE 폴더 복사
            Trace.WriteLine("COPY_DIR : " + orgPath + "\\XIGNCODE" + " -> " + path + "\\XIGNCODE");
            DirectoryCopy(orgPath + "\\XIGNCODE", path + "\\XIGNCODE", true);

            // Online 폴더 생성
            string onlinePath = path + "\\Online";
            if(true == Directory.Exists(onlinePath)) {
                if(true == File.GetAttributes(onlinePath).HasFlag(FileAttributes.ReparsePoint)) { Directory.Delete(onlinePath); }
                ;
            }
            Directory.CreateDirectory(onlinePath);

            // Online 폴더 내 파일 심볼릭링크 생성 (KeySetting.dat , PetSetting.dat은 없을 경우에만 복사)
            foreach(string eachFilePath in Directory.GetFiles(orgOnlinePath)) {
                string fileName = eachFilePath.Substring(eachFilePath.LastIndexOf('\\')); // \파일이름
                if(fileName == "\\KeySetting.dat" || fileName == "\\PetSetting.dat" || fileName == "\\AKinteractive.cfg" || fileName == "\\CombineInfo.txt") {
                    if(File.Exists(onlinePath + fileName)) {
                        if(File.GetAttributes(onlinePath + fileName).HasFlag(FileAttributes.ReparsePoint)) {
                            // 설정 파일들이 이미 심볼릭 링크일 경우 삭제하고 복사합니다.
                            File.Delete(onlinePath + fileName);
                            Trace.WriteLine("COPY : " + eachFilePath + " -> " + onlinePath + fileName);
                            File.Copy(eachFilePath, onlinePath + fileName, false);
                        }
                    } else {
                        Trace.WriteLine("COPY : " + eachFilePath + " -> " + onlinePath + fileName);
                        File.Copy(eachFilePath, onlinePath + fileName, false);
                    }
                } else {
                    Trace.WriteLine("SYMLINK_FILE : " + eachFilePath + " -> " + onlinePath + fileName);
                    if(File.Exists(onlinePath + fileName)) { File.Delete(onlinePath + fileName); }
                    File.CreateSymbolicLink(onlinePath + fileName, eachFilePath);
                }
            }

            //Online 폴더 내 폴더 심볼릭링크 생성
            foreach(string eachDirPath in Directory.GetDirectories(orgOnlinePath)) {
                string? dirName = new DirectoryInfo(eachDirPath).Name;
                string linkPath = onlinePath + '\\' + dirName;
                Trace.WriteLine("SYMLINK_DIR : " + eachDirPath + " -> " + linkPath);
                if(Directory.Exists(linkPath)) { Directory.Delete(linkPath, true); }
                Directory.CreateSymbolicLink(linkPath, eachDirPath);
            }
        }

        // 2클라 생성
        if(secondPath != "") {
            if(Directory.Exists(secondPath) && false == IsValidPath(owner, secondPath, false))
                return false;
            Directory.CreateDirectory(secondPath); // 거상 폴더 생성
            copy(secondPath); // 거상 폴더 내 파일 복사
            symbolic(secondPath);
        }

        // 3클라 생성
        if(thirdPath != "") {
            if(Directory.Exists(thirdPath) && false == IsValidPath(owner, thirdPath, false))
                return false;
            Directory.CreateDirectory(thirdPath); // 거상 폴더 생성
            copy(thirdPath); // 거상 폴더 내 파일 복사
            symbolic(thirdPath);
        }

        Trace.WriteLine("다클라 생성 끝");
        return true;
    }

    private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs) {
        // Get the subdirectories for the specified directory.
        DirectoryInfo dir = new DirectoryInfo(sourceDirName);

        if(!dir.Exists) {
            throw new DirectoryNotFoundException(
                "Source directory does not exist or could not be found: "
                + sourceDirName);
        }

        DirectoryInfo[] dirs = dir.GetDirectories();

        // If the destination directory doesn't exist, create it.       
        Directory.CreateDirectory(destDirName);

        // Get the files in the directory and copy them to the new location.
        FileInfo[] files = dir.GetFiles();
        foreach(FileInfo file in files) {
            string tempPath = Path.Combine(destDirName, file.Name);
            file.CopyTo(tempPath, true);
        }

        // If copying subdirectories, copy them and their contents to new location.
        if(copySubDirs) {
            foreach(DirectoryInfo subdir in dirs)
            {
                string tempPath = Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
            }
        }
    }
}