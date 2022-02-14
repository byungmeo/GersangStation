using MaterialSkin.Controls;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace GersangStation {
    public partial class Form_Patcher : MaterialForm {
        private const string url_main = @"http://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/";
        private const string url_test = @"http://akgersang.xdn.kinxcdn.com/Gersang/Patch/Test_Server/";
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

        private bool isPatching;

        public Form_Patcher() {
            InitializeComponent();

            if (true == bool.Parse(ConfigManager.getConfig("is_test_server"))) {
                //테섭
                path_main = ConfigManager.getConfig("client_path_test_1");
                name_client_2 = ConfigManager.getConfig("client_path_test_2");
                if (name_client_2 == "") { name_client_2 = ConfigManager.getConfig("directory_name_client_test_2"); } 
                else { name_client_2 = name_client_2.Substring(name_client_2.LastIndexOf('\\') + 1); }
                name_client_3 = ConfigManager.getConfig("client_path_test_3");
                if (name_client_3 == "") { name_client_3 = ConfigManager.getConfig("directory_name_client_test_3"); } 
                else { name_client_3 = name_client_3.Substring(name_client_3.LastIndexOf('\\') + 1); }
                url_info = url_test_info;
                url_patch = url_test_patch;
                url_vsn = url_test_vsn;
            } else {
                //본섭
                path_main = ConfigManager.getConfig("client_path_1");
                name_client_2 = ConfigManager.getConfig("client_path_2");
                if (name_client_2 == "") { name_client_2 = ConfigManager.getConfig("directory_name_client_2"); }
                else { name_client_2 = name_client_2.Substring(name_client_2.LastIndexOf('\\') + 1); }
                name_client_3 = ConfigManager.getConfig("client_path_3");
                if (name_client_3 == "") { name_client_3 = ConfigManager.getConfig("directory_name_client_3"); } 
                else { name_client_3 = name_client_3.Substring(name_client_3.LastIndexOf('\\') + 1); }
                url_info = url_main_info;
                url_patch = url_main_patch;
                url_vsn = url_main_vsn;
            }

            version_current = GetCurrentVersion(this, path_main);
            version_latest = GetLatestVersion(this, url_vsn);
            if (version_current == "" || version_latest == "") {
                this.Close();
                return;
            }

            isPatching = false;
        }
        public static string GetCurrentVersion(Form owner, string path_main) {
            string version;
            try {
                FileStream fs = File.OpenRead(path_main + @"\Online\vsn.dat");
                BinaryReader br = new BinaryReader(fs);
                version = (-(br.ReadInt32() + 1)).ToString();
                fs.Close();
                br.Close();
            } catch (Exception e) {
                MessageBox.Show(owner, "현재 거상 버전 확인 중 오류가 발생하였습니다.\n문의해주세요." + e.Message
                    , "거상 경로 확인 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return "";
            }

            return version;
        }

        public static string GetLatestVersion(Form owner, string url_vsn) {
            string version;
            try {
                //현재 거상 최신 버전을 확인합니다
#pragma warning disable SYSLIB0014 // 형식 또는 멤버는 사용되지 않습니다.
                using (WebClient client = new()) {
#pragma warning restore SYSLIB0014 // 형식 또는 멤버는 사용되지 않습니다.
                    ServicePointManager.Expect100Continue = true;
                    ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

                    //마이크로소프트 권장 사항 : 보안프로토콜의 결정은 운영체제에게 맡겨야 한다.
                    //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

                    client.Headers.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 8.0)");

                    DirectoryInfo binDirectory = new DirectoryInfo(Application.StartupPath + @"\bin");
                    if (!binDirectory.Exists) { binDirectory.Create(); } else {
                        foreach (FileInfo file in binDirectory.GetFiles()) {
                            if (file.Name.Equals("vsn.dat")) {
                                file.Delete();
                            }
                        }
                    }

                    client.DownloadFile(new Uri(url_vsn), Application.StartupPath + @"\bin\vsn.dat.gsz");

                    Trace.WriteLine("vsn.dat.gsz 파일 다운로드 완료");
                    ZipFile.ExtractToDirectory(binDirectory.FullName + @"\vsn.dat.gsz", binDirectory.FullName);
                    Trace.WriteLine("vsn.dat 파일 압축 해제 완료");

                    FileStream fs = File.OpenRead(binDirectory.FullName + @"\vsn.dat");
                    BinaryReader br = new BinaryReader(fs);
                    version = (-(br.ReadInt32() + 1)).ToString();
                    Trace.WriteLine("서버에 게시된 거상 최신 버전 : " + version);
                    fs.Close();
                    br.Close();
                    return version;
                    //client.DownloadFileAsync(new Uri(url_vsn), Application.StartupPath + @"\bin\vsn.dat.gsz");
                }
            } catch (Exception e) {
                MessageBox.Show(owner, "거상 최신 버전 확인 중 오류가 발생하였습니다.\n문의해주세요." + e.Message
                    , "거상 경로 확인 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return "";
            }
        }

        private void Form_Patcher_Load(object sender, EventArgs e) {
            textBox_currentVersion.Text = version_current;
            textBox_latestVersion.Text = version_latest;
            textBox_mainPath.Text = path_main;
            textBox_folderName_2.Text = name_client_2;
            textBox_folderName_3.Text = name_client_3;
        }

        private void materialButton_close_Click(object sender, EventArgs e) {
            this.DialogResult = DialogResult.OK;
        }

        private void materialButton_startPatch_Click(object sender, EventArgs e) {
            int equal = 1;
            if (version_current == version_latest) {
                DialogResult dr = MessageBox.Show(this, "현재 버전과 최신 버전이 같습니다.\n그래도 패치 하시겠습니까?", "버전 같음", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (dr == DialogResult.No) {
                    this.DialogResult = DialogResult.OK;
                    return;
                } else {
                    equal = 0;
                }
            }

            materialButton_startPatch.Enabled = false;
            materialButton_close.Enabled = false;
            isPatching = true;
            Trace.WriteLine("패치 시작!");

            ////////////////////////////////////////////////////////////////////////////////////////////////////////////
            
            DirectoryInfo directory_patch = new DirectoryInfo(Application.StartupPath + @"\patch");
            if (!directory_patch.Exists) { directory_patch.Create(); }

            DirectoryInfo directory_info = new DirectoryInfo(directory_patch + @"\info");
            if (!directory_info.Exists) { directory_info.Create(); }

            DirectoryInfo directory_file = new DirectoryInfo(directory_patch + @"\" + version_current + "-" + version_latest);
            if (!directory_file.Exists) { directory_file.Create(); }

            List<string> list_infoFile = new List<string>();

#pragma warning disable SYSLIB0014 // 형식 또는 멤버는 사용되지 않습니다.
            using (WebClient webClient = new()) {
#pragma warning restore SYSLIB0014 // 형식 또는 멤버는 사용되지 않습니다.
                for (int i = Int16.Parse(version_current) + equal; i <= Int16.Parse(version_latest); i++) {
                    string url = url_info + i;
                    try {
                        webClient.DownloadFile(new Uri(url), directory_info + @"\" + i + ".txt");
                        Trace.WriteLine(i + " 버전 패치정보 파일 다운로드 성공\n");
                        list_infoFile.Add(i.ToString());
                    } catch (Exception ex) {
                        //다운로드 실패 시 다음 버전으로 넘어갑니다
                        Trace.WriteLine("버전 " + i + " 이 존재하지 않아 다음 버전으로 넘어갑니다.\n");
                        Trace.WriteLine(ex.Message);
                    }
                }
                Trace.WriteLine("모든 패치정보 파일 다운로드 성공"); ////////////////////////////////////////////////////////////////////////////////--
            }

            Dictionary<string, string> list_patchFile = new Dictionary<string, string>(); //key값으로 파일이름, value값으로 경로 저장

            //몇번의 패치가 존재하든, 한꺼번에 패치하기위해 여러 패치정보파일에서 중복없이 파일 리스트를 뽑아옵니다.
            if (list_infoFile.Count >= 1) {
                using (StreamWriter wr = new StreamWriter(directory_info + @"\" + version_current + "-" + version_latest + ".txt")) { //디버깅용으로 새로운 정보 파일을 생성합니다.
                    wr.WriteLine("파일명\t경로"); //디버깅용
                    foreach (string item in list_infoFile) {
                        string[] lines = File.ReadAllLines(directory_info + @"\" + item + ".txt", Encoding.Default); //패치정보파일에서 모든 텍스트를 읽어옵니다.

                        //패치정보파일의 첫 4줄은 쓸모없으므로 생략하고, 5번째 줄부터 읽습니다.
                        for (int i = 4; i < lines.Length; i++) {
                            string[] row = lines[i].Split('\t'); //한 줄을 탭을 간격으로 나눕니다. (디버깅용)

                            //만약 EOF가 등장했다면 루프를 빠져나갑니다.
                            if (row[0] == ";EOF") {
                                break;
                            }

                            if (!list_patchFile.ContainsKey(row[1])) {
                                list_patchFile.Add(row[1], row[3].Remove(0, 1));
                                wr.WriteLine(row[1] + "\t" + row[3].Remove(0, 1)); //디버깅용
                            }
                        }
                    }
                }
            }
            Trace.WriteLine("패치 정보 파일 병합 완료"); //////////////////////////////////////////////////////////////////////////////////////--

            Dictionary<string, string> list_readyDownloadFile = new Dictionary<string, string>(); //실질적으로 다운로드 받는 파일입니다.
            List<ListViewItem> list_listViewItem = new List<ListViewItem>();

            foreach (var item in list_patchFile) {
                //string url = url_patch + item.Value + item.Key;
                string path_file = directory_file.FullName + @"\" + item.Value + item.Key;
                string name_file = path_file.Substring(path_file.LastIndexOf('\\') + 1); //파일이름만 추출합니다

                ListViewItem lvi = new ListViewItem(name_file);
                lvi.UseItemStyleForSubItems = false;
                lvi.SubItems.Add(item.Value);
                lvi.SubItems.Add("0");
                lvi.SubItems.Add("0");
                lvi.SubItems.Add("다운로드 대기 중");
                listView.Items.Add(lvi);
                list_listViewItem.Add(lvi);

                if (File.Exists(path_file.Remove(path_file.Length - 4))) {
                    lvi.SubItems[4].Text = "이미 존재하는 파일";
                    lvi.SubItems[4].ForeColor = Color.Green;

                    //label_progress.Text = ++downloadCompletedCount + " / " + patchFileList.Count;
                    //progressBar.Value += 1;
                    Trace.WriteLine(name_file + " 는 이미 존재합니다!");
                } else {
                    list_readyDownloadFile.Add(item.Key, item.Value);
                }
            }

            Trace.WriteLine("총 패치 파일 수 : " + list_patchFile);
            Trace.WriteLine("실제로 다운로드 받는 총 파일 수 : " + list_readyDownloadFile);
            progressBar.Maximum = list_readyDownloadFile.Count;
            label_progress.Text = progressBar.Value + " / " + progressBar.Maximum;

            foreach (var item in list_readyDownloadFile) {
                string url = url_patch + item.Value + item.Key;
                string path_file = directory_file.FullName + @"\" + item.Value + item.Key;
                string name_file = path_file.Substring(path_file.LastIndexOf('\\') + 1); //파일이름만 추출합니다

                //내부 폴더 생성
#pragma warning disable CS8604 // 가능한 null 참조 인수입니다.
                DirectoryInfo fileInnerDirectory = new DirectoryInfo(new FileInfo(path_file).DirectoryName);
#pragma warning restore CS8604 // 가능한 null 참조 인수입니다.
                if (!fileInnerDirectory.Exists) { fileInnerDirectory.Create(); }

                //하나의 WebClient가 하나의 파일 다운로드를 담당
#pragma warning disable SYSLIB0014 // 형식 또는 멤버는 사용되지 않습니다.
                using (WebClient client = new()) {
#pragma warning restore SYSLIB0014 // 형식 또는 멤버는 사용되지 않습니다.
                    client.Headers.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 8.0)");

                    ListViewItem? lvi = null;
                    foreach (var listViewItem in list_listViewItem) {
                        if (listViewItem.Text == name_file) {
                            lvi = listViewItem;
                            break;
                        }
                    }

                    if (lvi == null) {
                        Trace.WriteLine("ListViewItem 탐색 실패!");
                        continue;
                    }

                    //다운로드 진행도가 변경될 때 마다
                    client.DownloadProgressChanged += (object obj, DownloadProgressChangedEventArgs args) => {
                        lvi.SubItems[3].Text = args.TotalBytesToReceive.ToString();
                        lvi.SubItems[4].Text = "다운로드 중";
                        lvi.SubItems[2].Text = args.BytesReceived.ToString();
                    };

                    string errorMessageList = "";
                    client.DownloadFileCompleted += (object? obj, AsyncCompletedEventArgs args) => {
                        progressBar.Value += 1;
                        label_progress.Text = progressBar.Value + " / " + progressBar.Maximum;

                        if (args.Error != null) {
                            lvi.SubItems[4].Text = "다운로드 실패";
                            lvi.SubItems[4].ForeColor = Color.Red;
                            errorMessageList += args.Error.Message + "\n";
                        } else {
                            lvi.SubItems[4].Text = "다운로드 완료";
                            lvi.SubItems[4].ForeColor = Color.Green;
                            Trace.WriteLine(name_file + "다운로드 완료");

                            try {
                                //파일 다운로드가 완료되는 대로 압축 해제 후 압축 파일은 삭제
#pragma warning disable CS8604 // 가능한 null 참조 인수입니다.
                                ZipFile.ExtractToDirectory(path_file, new FileInfo(path_file).DirectoryName);
#pragma warning restore CS8604 // 가능한 null 참조 인수입니다.
                            } catch (Exception ex) {
                                MessageBox.Show(this, "파일 압축 해제 중 오류가 발생하였습니다.\n다시 시도해주세요.\n" + ex.Message, "압축 해제 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }

                            try {
                                File.Delete(path_file);
                            } catch (Exception ex) {
                                Trace.WriteLine("압축 파일 삭제 중 오류가 발생하였습니다.\n패치는 계속해서 진행됩니다.\n" + ex.Message);
                            }
                        }

                        //파일 다운로드가 모두 완료
                        if (progressBar.Value == list_readyDownloadFile.Count) {
                            if (!errorMessageList.Equals("")) {
                                MessageBox.Show(this, "파일 다운로드 중 오류가 발생하였습니다.\n다시 시도해주세요.\n" + errorMessageList, "다운로드 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            } else {
                                //원본 폴더로 패치 파일 복사
                                try {
                                    //Now Create all of the directories
                                    foreach (string dirPath in Directory.GetDirectories(directory_file.FullName, "*", SearchOption.AllDirectories))
                                        Directory.CreateDirectory(dirPath.Replace(directory_file.FullName, path_main));

                                    //Copy all the files & Replaces any files with the same name
                                    foreach (string newPath in Directory.GetFiles(directory_file.FullName, "*.*", System.IO.SearchOption.AllDirectories))
                                        File.Copy(newPath, newPath.Replace(directory_file.FullName, path_main), true);
                                    
                                    MessageBox.Show(this, "패치 다운로드 및 적용이 모두 완료되었습니다.", "패치 적용 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                } catch (Exception ex) {
                                    MessageBox.Show(this, "패치 적용 중 오류가 발생하였습니다.\n다시 시도해주세요.\n" + ex.Message, "패치적용 에러", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    isPatching = false;
                                    materialButton_close.Enabled = true;
                                    return;
                                }

                                //체크여부에 따라 저장된 패치 파일 삭제
                                if (materialCheckbox_delete.Checked) {
                                    try {
                                        directory_file.Delete(true);
                                        Trace.WriteLine("패치 파일 폴더 삭제 완료");
                                    } catch (Exception ex) {
                                        Trace.WriteLine("패치 파일 폴더 삭제 실패\n" + ex.Message);
                                    }
                                }

                                //10. 체크여부에 따라 다클라 패치 적용
                                if (materialCheckbox_apply.Checked) {
                                    if (bool.Parse(ConfigManager.getConfig("use_bat_creator"))) { ClientCreator.CreateClient_BAT(path_main, name_client_2, name_client_3); }
                                    else { ClientCreator.CreateClient_Default(this, path_main, name_client_2, name_client_3); }
                                }
                            }

                            isPatching = false;
                            materialButton_close.Enabled = true;
                        }
                    };

                    client.DownloadFileAsync(new Uri(url), path_file);
                } //using
            } //foreach
        }

        private void Form_Patcher_FormClosing(object sender, FormClosingEventArgs e) {
            if (true == isPatching) {
                DialogResult dr = MessageBox.Show(this, "현재 패치가 진행 중입니다.\n그래도 종료하시겠습니까?", "패치 중", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (!(dr == DialogResult.Yes)) { e.Cancel = true; }
            }
        }

        private void Form_Patcher_FormClosed(object sender, FormClosedEventArgs e) {
            if (true == isPatching) {
                MessageBox.Show("다운로드는 계속해서 진행됩니다.\n다시 시도하시려면 거상 스테이션 폴더 내의\n" + @"patch 폴더를 삭제 해주세요."
                        , "패치가 강제로 종료됨", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
