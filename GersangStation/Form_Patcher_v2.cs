using MaterialSkin.Controls;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace GersangStation {
    public partial class Form_Patcher_v2 : MaterialForm {
        private const string url_main = @"https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/";
        private const string url_test = @"https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Test_Server/";
        private const string url_main_info = url_main + @"Client_info_File/"; // + "00000"
        private const string url_test_info = url_test + @"Client_info_File/"; // + "00000"
        private const string url_main_patch = url_main + @"Client_Patch_File/"; // + "{경로}/{파일명.확장자}"
        private const string url_test_patch = url_test + @"Client_Patch_File/"; // + "{경로}/{파일명.확장자}"
        private const string url_main_vsn = url_main_patch + @"Online/vsn.dat.gsz";
        private const string url_test_vsn = url_test_patch + @"Online/vsn.dat.gsz";

        private string path_main;
        private string name_client_2;
        private string name_client_3;
        private string url_info;
        private string url_patch;
        private string url_vsn;
        private string version_current;
        private string version_latest;
        private Server server;
        
        private enum Server {
            Main,
            Test
        };

        Dictionary<string, string> list_retry = new Dictionary<string, string>();

        public Form_Patcher_v2(bool isTest) {
            InitializeComponent();

            if (true == isTest) {
                Logger.Log("Log : (" + this.Name + ") " + "테스트서버 패치 시작 전");
                //테섭
                path_main = ConfigManager.getConfig("client_path_test_1");
                name_client_2 = ConfigManager.getConfig("client_path_test_2");
                if (name_client_2 != "") { name_client_2 = name_client_2.Substring(name_client_2.LastIndexOf('\\') + 1); }
                name_client_3 = ConfigManager.getConfig("client_path_test_3");
                if (name_client_3 != "") { name_client_3 = name_client_3.Substring(name_client_3.LastIndexOf('\\') + 1); }
                url_info = url_test_info;
                url_patch = url_test_patch;
                url_vsn = url_test_vsn;
                server = Server.Test;
            }
            else {
                Logger.Log("Log : (" + this.Name + ") " + "본서버 패치 시작 전");
                //본섭
                path_main = ConfigManager.getConfig("client_path_1");
                name_client_2 = ConfigManager.getConfig("client_path_2");
                if (name_client_2 != "") { name_client_2 = name_client_2.Substring(name_client_2.LastIndexOf('\\') + 1); }
                name_client_3 = ConfigManager.getConfig("client_path_3");
                if (name_client_3 != "") { name_client_3 = name_client_3.Substring(name_client_3.LastIndexOf('\\') + 1); }
                url_info = url_main_info;
                url_patch = url_main_patch;
                url_vsn = url_main_vsn;
                server = Server.Main;
            }

            version_current = Form_Patcher.GetCurrentVersion(this, path_main);
            version_latest = Form_Patcher.GetLatestVersion(this, url_vsn);
            if (version_current == "" || version_latest == "") {
                this.Close();
                return;
            }
        }

        private void Form_Patcher_v2_Load(object sender, EventArgs e) {
            textBox_currentVersion.Text = version_current;
            textBox_latestVersion.Text = version_latest;

            if(name_client_2 == "" && name_client_3 == "") {
                materialCheckbox_apply.Checked = false;
                materialCheckbox_apply.Enabled = false;
                toolTip1.Active = true;
                toolTip1.SetToolTip(materialCheckbox_apply, "다클라 경로가 설정되어있지 않아 비활성화 되었습니다.");
            }
        }

        private void materialButton_close_Click(object sender, EventArgs e) {
            Logger.Log("Click : (" + this.Name + ") " + materialButton_close.Name);
            this.DialogResult = DialogResult.OK;
        }

        private void materialButton_startPatch_Click(object sender, EventArgs e) {
            DirectoryInfo pathInfo = new DirectoryInfo(path_main + "\\char");
            if (true == pathInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                Logger.Log("Log : (" + "Form_Patcher_v2" + ") " + "본클라 경로가 다클라 생성기로 생성된 경로");
                MessageBox.Show("잘못된 본클라 경로입니다. 다시 지정해주세요.\n원인 : 원본 폴더가 아닌 생성기로 생성된 폴더입니다.", "경로 인식 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Logger.Log("Click : (" + this.Name + ") " + materialButton_startPatch.Name);
            int equal = 1;
            if (version_current == version_latest) {
                Logger.Log("Log : (" + this.Name + ") " + "현재 버전과 최신 버전이 같아 패치 여부를 묻는 메시지 출력");
                DialogResult dr = MessageBox.Show(this, "현재 버전과 최신 버전이 같습니다.\n그래도 패치 하시겠습니까?", "버전 같음", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (dr == DialogResult.No) {
                    this.DialogResult = DialogResult.OK;
                    return;
                }
                else {
                    equal = 0;
                }
            }

            materialButton_startPatch.Enabled = false;
            materialButton_close.Enabled = false;

            Logger.Log("Log : (" + this.Name + ") " + "패치 시작");
            Trace.WriteLine("패치 시작!");

            ////////////////////////////////////////////////////////////////////////////////////////////////////////////
            Logger.Log("Log : (" + this.Name + ") " + "패치에 필요한 디렉토리 미리 생성");

            DirectoryInfo directory_patch = new DirectoryInfo(Application.StartupPath + @"\patch");
            if (!directory_patch.Exists) { directory_patch.Create(); }

            DirectoryInfo directory_info = new DirectoryInfo(directory_patch + @"\info");
            if (!directory_info.Exists) { directory_info.Create(); }

            DirectoryInfo directory_file = new DirectoryInfo(directory_patch + @"\" + server + "_" + version_current + "-" + version_latest);
            if (!directory_file.Exists) { directory_file.Create(); }

            Dictionary<string, string> list_patchFile = new Dictionary<string, string>(); //key값으로 다운로드주소, value값으로 경로및파일명 저장

            label_status.Text = "패치 파일 목록을 추출하는 중...";
            list_patchFile = GetPatchFileList(equal, directory_info, directory_file);
            label_total.Text = "파일 개수 : " + list_patchFile.Count.ToString() + "개";
            Trace.WriteLine("패치 정보 파일 병합 완료"); //////////////////////////////////////////////////////////////////////////////////////--

            label_status.Text = "패치 파일을 다운로드 중... (오래 걸릴 수 있음)";
            DownloadAll(list_patchFile);
            Trace.WriteLine("패치 파일 다운로드 완료"); //////////////////////////////////////////////////////////////////////////////////////--

            label_status.Text = "압축 해제 및 적용 중... (오래 걸릴 수 있음)";
            ExtractAll(directory_file.FullName, path_main);
            Trace.WriteLine("압축 해제 완료"); //////////////////////////////////////////////////////////////////////////////////////--

            //다클라 패치 적용
            if (materialCheckbox_apply.Checked) {
                label_status.Text = "다클라 패치 적용 중...";
                Logger.Log("Log : (" + this.Name + ") " + "다클라 패치 적용 옵션이 체크되어있어 다클라 패치 적용 시작 (다클생성과 동일)");
                if (bool.Parse(ConfigManager.getConfig("use_bat_creator"))) { ClientCreator.CreateClient_BAT(path_main, name_client_2, name_client_3); }
                else { ClientCreator.CreateClient_Default(this, path_main, name_client_2, name_client_3); }
            }

            //패치 후 파일 삭제
            if (materialCheckbox_delete.Checked) {
                label_status.Text = "패치 후 파일 삭제 중...";
                try {
                    Logger.Log("Log : (" + this.Name + ") " + "남은 패치 파일 삭제 시도");
                    directory_file.Delete(true);
                    Trace.WriteLine("패치 파일 폴더 삭제 완료");
                }
                catch (Exception ex) {
                    Logger.Log("Exception : (" + this.Name + ") " + "남은 패치 파일 삭제 도중 예외가 발생 -> " + ex.Message);
                    Trace.WriteLine("패치 파일 폴더 삭제 실패\n" + ex.Message);
                }
            }

            label_status.Text = "패치가 모두 완료되었습니다!";
            label_status.ForeColor = Color.SeaGreen;
            materialButton_startPatch.Enabled = true;
            materialButton_close.Enabled = true;

            MessageBox.Show(this, "패치가 모두 완료되었습니다.", "패치 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public Dictionary<string, string> GetPatchFileList(int equal, DirectoryInfo directory_info, DirectoryInfo directory_file) {
            List<string> list_infoFile = new List<string>();

            Logger.Log("Log : (" + this.Name + ") " + "현재 버전부터 최신 버전까지 필요한 패치정보파일 다운로드");

            using (var webClient = new WebClient()) {
                for (int i = Int16.Parse(version_current) + equal; i <= Int16.Parse(version_latest); i++) {
                    string url = url_info + i;
                    try {
                        webClient.DownloadFile(new Uri(url), directory_info + @"\" + server + "_" + i + ".txt");
                        Trace.WriteLine(i + " 버전 패치정보 파일 다운로드 성공\n");
                        list_infoFile.Add(i.ToString());
                    }
                    catch (Exception ex) {
                        //다운로드 실패 시 다음 버전으로 넘어갑니다
                        Trace.WriteLine("버전 " + i + " 이 존재하지 않아 다음 버전으로 넘어갑니다.\n");
                        Trace.WriteLine(ex.Message);
                    }
                }
                Trace.WriteLine("모든 패치정보 파일 다운로드 성공"); ////////////////////////////////////////////////////////////////////////////////--

                Dictionary<string, string> list_patchFile = new Dictionary<string, string>(); //key값으로 다운로드주소, value값으로 경로및파일명 저장

                //몇번의 패치가 존재하든, 한꺼번에 패치하기위해 여러 패치정보파일에서 중복없이 파일 리스트를 뽑아옵니다.
                using (var wr = new StreamWriter(directory_info + @"\" + server + "_" + version_current + "-" + version_latest + ".txt")) { //디버깅용으로 새로운 정보 파일을 생성합니다.
                    wr.WriteLine("파일명\t다운로드주소\t경로"); //디버깅용
                    foreach (string item in list_infoFile) {
                        string[] lines = File.ReadAllLines(directory_info + @"\" + server + "_" + item + ".txt", Encoding.Default); //패치정보파일에서 모든 텍스트를 읽어옵니다.

                        //패치정보파일의 첫 4줄은 쓸모없으므로 생략하고, 5번째 줄부터 읽습니다.
                        for (int i = 4; i < lines.Length; i++) {
                            string[] row = lines[i].Split('\t'); //한 줄을 탭을 간격으로 나눕니다. (디버깅용)

                            //만약 EOF가 등장했다면 루프를 빠져나갑니다.
                            if (row[0] == ";EOF") {
                                break;
                            }

                            //row[1] == 파일명
                            //row[3] == 내부경로
                            string file_name = row[1];
                            string file_inner_path = row[3];
                            string file_download_address = url_patch + file_inner_path + file_name;
                            string file_full_path = directory_file.FullName + @"\" + file_inner_path + file_name;

                            if (!list_patchFile.ContainsKey(file_download_address)) {
                                //내부 폴더 생성
                                DirectoryInfo fileInnerDirectory = new DirectoryInfo(new FileInfo(file_full_path).DirectoryName);
                                if (!fileInnerDirectory.Exists) { fileInnerDirectory.Create(); }

                                if (File.Exists(file_full_path))
                                    Trace.WriteLine(file_name + " 는 이미 존재합니다!");
                                else
                                    list_patchFile.Add(file_download_address, file_full_path);

                                wr.WriteLine(file_name + "\t" + file_download_address + "\t" + file_full_path); //디버깅용
                            }
                        }
                    }
                }

                return list_patchFile;
            }
        }

        public void ExtractAll(string patch_dir, string main_dir) {
            foreach (string file in Directory.EnumerateFiles(patch_dir, "*.*", SearchOption.AllDirectories)) {
                string dest = path_main + file.Remove(0, patch_dir.Length);
                dest = dest.Remove(dest.LastIndexOf('\\'));
                Trace.WriteLine(file + " -> " + dest);
                try {
                    ZipFile.ExtractToDirectory(file, dest, true);
                }
                catch (Exception ex) {
                    Trace.WriteLine("압축 오류 발생 : " + file);
                    Trace.WriteLine(ex.StackTrace);
                }
            }
        }

        public void DownloadAll(Dictionary<string, string> list) {
            Parallel.ForEach(
                list,
                new ParallelOptions { MaxDegreeOfParallelism = 10 },
                DownloadFile);

            if (list_retry.Count > 0) {
                foreach (var item in list_retry) {
                    Trace.WriteLine("다운로드 실패한 파일 주소 : " + item.Key);
                }

                Trace.WriteLine("다운로드 실패한 파일을 재다운로드 합니다.");

                for (int i = 0; i < 10; i++) {
                    if (list_retry.Count == 0) {
                        break;
                    }

                    Trace.WriteLine(i + 1 + "번째 재다운로드 시도... 남은 파일 수 : " + list_retry.Count + "개");

                    Parallel.ForEach(
                    list_retry,
                    new ParallelOptions { MaxDegreeOfParallelism = 10 },
                    RetryDownloadFile);
                }

                if (list_retry.Count > 0) {
                    Trace.WriteLine("10번의 재다운로드 시도 결과 모든 파일을 재다운로드 하는데 실패하였습니다.");
                    Logger.Log("10번의 재다운로드 시도 결과 모든 파일을 재다운로드 하는데 실패하였습니다.");
                    foreach (var item in list_retry) {
                        Trace.WriteLine("다운로드 실패한 파일 주소 : " + item.Key);
                    }
                }
                else {
                    Trace.WriteLine("모든 파일을 성공적으로 다운로드 하였습니다.");
                }
            }
            else {
                Trace.WriteLine("모든 파일을 성공적으로 다운로드 하였습니다.");
            }

            Trace.WriteLine("다운로드 종료");
            list_retry.Clear();
        }

        public void DownloadFile(KeyValuePair<string, string> item) {
            using (var client = new WebClient()) {
                client.Headers.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 8.0)");

                string file_name = item.Value.Substring(item.Value.LastIndexOf('\\') + 1);

                //내부 폴더 생성
                DirectoryInfo fileInnerDirectory = new DirectoryInfo(new FileInfo(item.Value).DirectoryName);
                if (!fileInnerDirectory.Exists) { fileInnerDirectory.Create(); }

                Trace.WriteLine($"[다운로드 시작] {file_name}");

                try {
                    client.DownloadFile(item.Key, item.Value);
                    Trace.WriteLine($"[다운로드 성공] {file_name}");
                }
                catch (WebException e) {
                    Trace.WriteLine($"[다운로드 실패] {file_name}\n" + e.StackTrace);
                    list_retry.Add(item.Key, item.Value);
                }
            }
        }

        public void RetryDownloadFile(KeyValuePair<string, string> item) {
            using (var client = new WebClient()) {
                client.Headers.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 8.0)");

                string file_name = item.Value.Substring(item.Value.LastIndexOf('\\') + 1);

                //내부 폴더 생성
                DirectoryInfo fileInnerDirectory = new DirectoryInfo(new FileInfo(item.Value).DirectoryName);
                if (!fileInnerDirectory.Exists) { fileInnerDirectory.Create(); }

                Trace.WriteLine($"[재다운로드 시작] {file_name}");

                try {
                    client.DownloadFile(item.Key, item.Value);
                    Trace.WriteLine($"[재다운로드 성공] {file_name}");
                    list_retry.Remove(item.Key);
                }
                catch (WebException e) {
                    Trace.WriteLine($"[재다운로드 실패] {file_name}\n" + e.StackTrace);
                }
            }
        }
    }
}
