using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace GersangStation {
    internal class ClientCreator {

        public static void client_create(string original_path, string name2, string name3) {
            // 파일 복사 시 제외할 확장자명이 담긴 txt파일을 생성합니다.
            string excludeList = ".tmp\n.bmp";
            System.IO.File.WriteAllText("exclude.txt", excludeList);

            //다클생성기 bat파일의 초안을 불러옵니다.
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "GersangStation.ClientCreatorCommand.txt";
            string command = "";
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream)) {
                command = reader.ReadToEnd();
            }

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
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo() {
                FileName = original_path + "\\..\\",
                UseShellExecute = true,
                Verb = "open"
            });
            //System.Diagnostics.Process.Start(original_path + @"\..");
        }
    }
}
