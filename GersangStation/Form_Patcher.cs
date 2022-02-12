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
                url_info = url_test_info;
                url_patch = url_test_patch;
                url_vsn = url_test_vsn;
            } else {
                //본섭
                path_main = ConfigManager.getConfig("client_path_1");
                url_info = url_main_info;
                url_patch = url_main_patch;
                url_vsn = url_main_vsn;
            }

            version_current = GetCurrentVersion();
            version_latest = GetLatestVersion();

            isPatching = false;
        }
        private string GetCurrentVersion() {
            string version;
            try {
                FileStream fs = File.OpenRead(path_main + @"\Online\vsn.dat");
                BinaryReader br = new BinaryReader(fs);
                version = (-(br.ReadInt32() + 1)).ToString();
                fs.Close();
                br.Close();
            } catch (Exception e) {
                MessageBox.Show(this, "현재 거상 버전 확인 중 오류가 발생하였습니다.\n문의해주세요." + e.Message
                    , "거상 경로 확인 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                return "";
            }

            return version;
        }

        private string GetLatestVersion() {
            string version;
            try {
                //현재 거상 최신 버전을 확인합니다
                using (WebClient client = new WebClient()) {
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
                MessageBox.Show(this, "거상 최신 버전 확인 중 오류가 발생하였습니다.\n문의해주세요." + e.Message
                    , "거상 경로 확인 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                return "";
            }
        }

        private void Form_Patcher_Load(object sender, EventArgs e) {
            textBox_currentVersion.Text = version_current;
            textBox_latestVersion.Text = version_latest;
            textBox_mainPath.Text = path_main;
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

            DirectoryInfo fileDirectory = new DirectoryInfo(directory_patch + @"\" + version_current + "-" + version_latest);
            if (!fileDirectory.Exists) { fileDirectory.Create(); }

            List<string> list_infoFile = new List<string>();

            using (WebClient webClient = new WebClient()) {
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
            }
        }

        private void Form_Patcher_FormClosing(object sender, FormClosingEventArgs e) {
            if (true == isPatching) {
                DialogResult dr = MessageBox.Show(this, "현재 패치가 진행 중입니다.\n그래도 종료하시겠습니까?", "패치 중", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (dr == DialogResult.Yes) {
                    //패치 종료 처리
                } else {
                    e.Cancel = true;
                }
            }
        }
    }
}
