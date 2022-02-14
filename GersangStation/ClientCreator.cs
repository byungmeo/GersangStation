using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace GersangStation {
    internal class ClientCreator {

        public static void CreateClient_BAT(string original_path, string name2, string name3) {
            // 파일 복사 시 제외할 확장자명이 담긴 txt파일을 생성합니다.
            string excludeList = ".tmp\n.bmp";
            System.IO.File.WriteAllText("exclude.txt", excludeList);

            //다클생성기 bat파일의 초안을 불러옵니다.
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "GersangStation.ClientCreatorCommand.txt";
            string command = "";

#pragma warning disable CS8600 // null 리터럴 또는 가능한 null 값을 null을 허용하지 않는 형식으로 변환하는 중입니다.
#pragma warning disable CS8604 // 가능한 null 참조 인수입니다.
            using (Stream stream = assembly.GetManifestResourceStream(resourceName)) {
                using (StreamReader reader = new StreamReader(stream)) {
                    command = reader.ReadToEnd();
                }
            }
#pragma warning restore CS8604 // 가능한 null 참조 인수입니다.
#pragma warning restore CS8600 // null 리터럴 또는 가능한 null 값을 null을 허용하지 않는 형식으로 변환하는 중입니다.
                
            //bat파일 초안의 경로 부분을 사용자가 설정한 경로로 바꿉니다.
            command = command.Replace("#PATH1#", original_path);
            string[] splitString = original_path.Split('\\');
            string previousPath = "";
            for (int i = 0; i < splitString.Length - 1; i++) {
                previousPath += splitString[i] + '\\';
            }
            command = command.Replace("#PATH2#", previousPath + name2);
            command = command.Replace("#PATH3#", previousPath + name3);

            //다클생성기 bat파일을 생성합니다.
            string batchFileName = "ClientCreator.bat";
            System.IO.File.WriteAllText(batchFileName, command, Encoding.Default);

            ProcessStartInfo psInfo = new ProcessStartInfo(batchFileName) {
                Verb = "runas", //관리자 권한 실행
                CreateNoWindow = false, //cmd창이 보이도록 합니다.
                UseShellExecute = false
            }; //다클생성기 bat파일 실행 준비

            Process process = new Process() {
                StartInfo = psInfo
            };
            process.Start(); //다클생성기 실행
            process.WaitForExit();

            //다클라가 생성된 폴더를 열어준다
            Process.Start(new ProcessStartInfo() {
                FileName = original_path + "\\..\\",
                UseShellExecute = true,
                Verb = "open"
            });
        }

        //성공 여부를 반환합니다.
        public static bool CreateClient_Default(Form owner, string originalPath, string name2, string name3) {
            if (false == Directory.Exists(originalPath)) {
                owner.BeginInvoke(() => {
                    MessageBox.Show(owner, "거상 폴더를 찾을 수 없습니다. 경로를 다시 지정해주세요.", "경로 인식 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
                return false;
            }

            string pathDrive = originalPath.Substring(0, 3).ToUpper();
            Trace.WriteLine("거상 폴더 드라이브 : " + pathDrive);
            foreach (DriveInfo item in DriveInfo.GetDrives()) {
                if (pathDrive != item.Name) { continue; }
                if (false == item.IsReady) {
                    owner.BeginInvoke(() => {
                        MessageBox.Show(owner, "다클라 생성(패치 적용)이 불가능한 경로입니다.\n원인 : 준비된 드라이브가 아닙니다.\n관리자에게 문의해주세요.", "다클라 생성(패치 적용) 불가", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                    return false;
                }

                if (item.DriveFormat != "NTFS") {
                    owner.BeginInvoke(() => {
                        MessageBox.Show(owner, "다클라 생성(패치 적용)이 불가능한 경로입니다.\n원인 : 드라이브 포맷이 NTFS가 아닙니다.\n관리자에게 문의해주세요.", "다클라 생성(패치 적용) 불가", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                    return false;
                }
                break;
            }
            Trace.WriteLine("드라이브 유효성 검사 완료");

            FileInfo pathInfo = new FileInfo(originalPath + "\\char");
            if (true == pathInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                owner.BeginInvoke(() => {
                    MessageBox.Show(owner, "잘못된 본클라 경로입니다. 다시 지정해주세요.\n원인 : 원본 폴더가 아닌 생성기로 생성된 폴더입니다.", "경로 인식 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
                return false;
            }

            string originalOnlinePath = originalPath + "\\Online";
            string secondPath = originalPath + "\\..\\" + name2;
            string thirdPath = originalPath + "\\..\\" + name3;

            //거상 폴더 생성
            Directory.CreateDirectory(secondPath);
            Directory.CreateDirectory(thirdPath);

            //거상 폴더 내 파일 복사
            foreach (string eachFilePath in Directory.GetFiles(originalPath)) {
                string fileName = eachFilePath.Substring(eachFilePath.LastIndexOf('\\')); // \파일이름
                string extension = Path.GetExtension(eachFilePath); // '.' 포함
                if (extension == ".tmp" || extension == ".bmp") continue;
                Trace.WriteLine("COPY : " + eachFilePath + " -> " + secondPath + fileName);
                File.Copy(eachFilePath, secondPath + fileName, true);
                Trace.WriteLine("COPY : " + eachFilePath + " -> " + thirdPath + fileName);
                File.Copy(eachFilePath, thirdPath + fileName, true);
            }

            //거상 폴더 내 폴더 심볼릭링크 생성 (XIGNCODE, Online 제외)
            foreach (string eachDirPath in Directory.GetDirectories(originalPath)) {
                string? dirName = new DirectoryInfo(eachDirPath).Name;
                if (dirName == "XIGNCODE" || dirName == "Online") continue;
                string secondLinkPath = secondPath + '\\' + dirName;
                string thirdLinkPath = thirdPath + '\\' + dirName;
                Trace.WriteLine("SYMLINK_DIR" + secondLinkPath + ", " + eachDirPath);
                if (Directory.Exists(secondLinkPath)) { Directory.Delete(secondLinkPath); }
                Directory.CreateSymbolicLink(secondLinkPath, eachDirPath);
                Trace.WriteLine("SYMLINK_DIR" + thirdLinkPath + ", " + eachDirPath);
                if (Directory.Exists(thirdLinkPath)) { Directory.Delete(thirdLinkPath); }
                Directory.CreateSymbolicLink(thirdLinkPath, eachDirPath);
            }

            //XIGNCODE 폴더 복사
            Trace.WriteLine("COPY_DIR : " + originalPath + "\\XIGNCODE" + " -> " + secondPath + "\\XIGNCODE");
            DirectoryCopy(originalPath + "\\XIGNCODE", secondPath + "\\XIGNCODE", true);
            Trace.WriteLine("COPY_DIR : " + originalPath + "\\XIGNCODE" + " -> " + thirdPath + "\\XIGNCODE");
            DirectoryCopy(originalPath + "\\XIGNCODE", thirdPath + "\\XIGNCODE", true);

            //Online 폴더 생성
            string secondOnlinePath = secondPath + "\\Online";
            string thirdOnlinePath = thirdPath + "\\Online";
            Directory.CreateDirectory(secondOnlinePath);
            Directory.CreateDirectory(thirdOnlinePath);

            //Online 폴더 내 파일 심볼릭링크 생성 (KeySetting.dat , PetSetting.dat은 없을 경우에만 복사)
            foreach (string eachFilePath in Directory.GetFiles(originalOnlinePath)) {
                string fileName = eachFilePath.Substring(eachFilePath.LastIndexOf('\\')); // \파일이름
                if (fileName == "\\KeySetting.dat" || fileName == "\\PetSetting.dat") {
                    if (File.Exists(secondOnlinePath + fileName)) continue;
                    Trace.WriteLine("COPY : " + eachFilePath + " -> " + secondOnlinePath + fileName);
                    File.Copy(eachFilePath, secondOnlinePath + fileName, false);
                    if (File.Exists(thirdOnlinePath + fileName)) continue;
                    Trace.WriteLine("COPY : " + eachFilePath + " -> " + thirdOnlinePath + fileName);
                    File.Copy(eachFilePath, thirdOnlinePath + fileName, false);
                    continue;
                }
                Trace.WriteLine("SYMLINK_FILE : " + eachFilePath + " -> " + secondOnlinePath + fileName);
                if (File.Exists(secondOnlinePath + fileName)) { File.Delete(secondOnlinePath + fileName); }
                File.CreateSymbolicLink(secondOnlinePath + fileName, eachFilePath);
                Trace.WriteLine("SYMLINK_FILE : " + eachFilePath + " -> " + thirdOnlinePath + fileName);
                if (File.Exists(thirdOnlinePath + fileName)) { File.Delete(thirdOnlinePath + fileName); }
                File.CreateSymbolicLink(thirdOnlinePath + fileName, eachFilePath);
            }

            //Online 폴더 내 폴더 심볼릭링크 생성
            foreach (string eachDirPath in Directory.GetDirectories(originalOnlinePath)) {
                string? dirName = new DirectoryInfo(eachDirPath).Name;
                string secondLinkPath = secondOnlinePath + '\\' + dirName;
                string thirdLinkPath = thirdOnlinePath + '\\' + dirName;
                Trace.WriteLine("SYMLINK_DIR : " + eachDirPath + " -> " + secondLinkPath);
                if (Directory.Exists(secondLinkPath)) { Directory.Delete(secondLinkPath); }
                Directory.CreateSymbolicLink(secondLinkPath, eachDirPath);
                Trace.WriteLine("SYMLINK_DIR : " + eachDirPath + " -> " + thirdLinkPath);
                if (Directory.Exists(thirdLinkPath)) { Directory.Delete(thirdLinkPath); }
                Directory.CreateSymbolicLink(thirdLinkPath, eachDirPath);
            }

            Trace.WriteLine("다클라 생성 끝");
            return true;
        }

        //private로 바꿀 것
        public static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs) {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists) {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the destination directory doesn't exist, create it.       
            Directory.CreateDirectory(destDirName);

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files) {
                string tempPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, true);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs) {
                foreach (DirectoryInfo subdir in dirs) {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }
    }
}
