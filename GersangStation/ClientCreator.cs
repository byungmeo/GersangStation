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

                if (item.DriveFormat == "FAT32") {
                    owner.BeginInvoke(() => {
                        MessageBox.Show(owner, "다클라 생성(패치 적용)이 불가능한 경로입니다.\n원인 : 드라이브 파일 시스템이 FAT32 입니다.\n관리자에게 문의해주세요.", "다클라 생성(패치 적용) 불가", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                    return false;
                }
                break;
            }
            Trace.WriteLine("드라이브 유효성 검사 완료");

            string originalOnlinePath = originalPath + "\\Online";
            DirectoryInfo pathInfo = new DirectoryInfo(originalPath + "\\char");
            if (true == pathInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                owner.BeginInvoke(() => {
                    MessageBox.Show(owner, "잘못된 본클라 경로입니다. 다시 지정해주세요.\n원인 : 원본 폴더가 아닌 생성기로 생성된 폴더입니다.", "경로 인식 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
                return false;
            }

            // 복사-붙여넣기로 생성한 다클라인지 체크
            bool flag = true;
            Action<string, string> check = (name, path) => {
                DirectoryInfo pathInfo = new DirectoryInfo(path + "\\char");
                if (pathInfo.Exists) {
                    if (false == pathInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                        owner.BeginInvoke(() => {
                            MessageBox.Show(owner, "복사-붙여넣기를 통해 다클 생성 시\n다클라 생성 기능 사용이 불가능합니다.\n확인 버튼을 누르면 열리는 홈페이지를 참고해주세요.", "다클라 생성 기능 사용 불가", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Process.Start(new ProcessStartInfo("https://github.com/byungmeo/GersangStation/discussions/8") { UseShellExecute = true });
                        });
                        flag = false;
                    }
                }
            };

            // 거상 폴더 내 파일 복사
            Action<string> copy = (path) => {
                foreach (string eachFilePath in Directory.GetFiles(originalPath)) {
                    string fileName = eachFilePath.Substring(eachFilePath.LastIndexOf('\\')); // \파일이름
                    string extension = Path.GetExtension(eachFilePath); // '.' 포함
                    if (extension == ".tmp" || extension == ".bmp" || extension == ".dmp") continue;
                    Trace.WriteLine("COPY : " + eachFilePath + " -> " + path + fileName);
                    File.Copy(eachFilePath, path + fileName, true);
                }
            };

            // 거상 폴더 내 폴더 심볼릭링크 생성 (XIGNCODE, Online는 별도 복사 또는 생성)
            Action<string> symbolic = (path) => {
                foreach (string eachDirPath in Directory.GetDirectories(originalPath)) {
                    string? dirName = new DirectoryInfo(eachDirPath).Name;
                    if (dirName == "XIGNCODE" || dirName == "Online") continue;
                    string linkPath = path + '\\' + dirName;
                    Trace.WriteLine("SYMLINK_DIR" + linkPath + ", " + eachDirPath);
                    if (Directory.Exists(linkPath)) { Directory.Delete(linkPath); }
                    Directory.CreateSymbolicLink(linkPath, eachDirPath);
                }

                // XIGNCODE 폴더 복사
                Trace.WriteLine("COPY_DIR : " + originalPath + "\\XIGNCODE" + " -> " + path + "\\XIGNCODE");
                DirectoryCopy(originalPath + "\\XIGNCODE", path + "\\XIGNCODE", true);

                // Online 폴더 생성
                string onlinePath = path + "\\Online";
                if (true == Directory.Exists(onlinePath)) {
                    if (true == File.GetAttributes(onlinePath).HasFlag(FileAttributes.ReparsePoint)) { Directory.Delete(onlinePath); };
                }
                Directory.CreateDirectory(onlinePath);

                // Online 폴더 내 파일 심볼릭링크 생성 (KeySetting.dat , PetSetting.dat은 없을 경우에만 복사)
                foreach (string eachFilePath in Directory.GetFiles(originalOnlinePath)) {
                    string fileName = eachFilePath.Substring(eachFilePath.LastIndexOf('\\')); // \파일이름
                    if (fileName == "\\KeySetting.dat" || fileName == "\\PetSetting.dat" || fileName == "\\AKinteractive.cfg") {
                        if (File.Exists(onlinePath + fileName)) continue;
                        Trace.WriteLine("COPY : " + eachFilePath + " -> " + onlinePath + fileName);
                        File.Copy(eachFilePath, onlinePath + fileName, false);
                        continue;
                    }
                    Trace.WriteLine("SYMLINK_FILE : " + eachFilePath + " -> " + onlinePath + fileName);
                    if (File.Exists(onlinePath + fileName)) { File.Delete(onlinePath + fileName); }
                    File.CreateSymbolicLink(onlinePath + fileName, eachFilePath);
                }

                //Online 폴더 내 폴더 심볼릭링크 생성
                foreach (string eachDirPath in Directory.GetDirectories(originalOnlinePath)) {
                    string? dirName = new DirectoryInfo(eachDirPath).Name;
                    string linkPath = onlinePath + '\\' + dirName;
                    Trace.WriteLine("SYMLINK_DIR : " + eachDirPath + " -> " + linkPath);
                    if (Directory.Exists(linkPath)) { Directory.Delete(linkPath, true); }
                    Directory.CreateSymbolicLink(linkPath, eachDirPath);
                }
            };

            // 2클라 생성
            if (name2 != "") {
                string secondPath = originalPath + "\\..\\" + name2;
                check(name2, secondPath);
                if (!flag) return false;
                Directory.CreateDirectory(secondPath); // 거상 폴더 생성
                copy(secondPath); // 거상 폴더 내 파일 복사
                symbolic(secondPath);
            }

            // 3클라 생성
            if (name3 != "") {
                string thirdPath = originalPath + "\\..\\" + name3;
                check(name3, thirdPath);
                if (!flag) return false;
                Directory.CreateDirectory(thirdPath); // 거상 폴더 생성
                copy(thirdPath); // 거상 폴더 내 파일 복사
                symbolic(thirdPath);
            }

            Trace.WriteLine("다클라 생성 끝");
            return true;
        }

        //private로 바꿀 것
        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs) {
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
