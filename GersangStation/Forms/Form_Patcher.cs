using GersangStation.Modules;
using MaterialSkin.Controls;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace GersangStation;

public partial class Form_Patcher : MaterialForm {
    private const int NUM_RETRY = 15; // 모든 파일 다운로드 실패 시 다운로드 재시도 최대 횟수

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

    private readonly object listLock = new object();
    Dictionary<string, string> list_retry = new Dictionary<string, string>();

    private BackgroundWorker worker = new BackgroundWorker();

    public Form_Patcher(bool isTest) {
        InitializeComponent();

        // .NET에서 지원하는 인코딩 공급자를 가져와서 등록 (없으면 euc-kr을 못불러와서 패치 파일 목록 받아올 때 한글이 깨짐)
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        if(true == isTest) {
            //테섭
            path_main = ConfigManager.getConfig("client_path_test_1");
            name_client_2 = ConfigManager.getConfig("client_path_test_2");
            if(name_client_2 != "") { name_client_2 = name_client_2.Substring(name_client_2.LastIndexOf('\\') + 1); }
            name_client_3 = ConfigManager.getConfig("client_path_test_3");
            if(name_client_3 != "") { name_client_3 = name_client_3.Substring(name_client_3.LastIndexOf('\\') + 1); }
            url_info = url_test_info;
            url_patch = url_test_patch;
            url_vsn = url_test_vsn;
            server = Server.Test;
        } else {
            //본섭
            path_main = ConfigManager.getConfig("client_path_1");
            name_client_2 = ConfigManager.getConfig("client_path_2");
            if(name_client_2 != "") { name_client_2 = name_client_2.Substring(name_client_2.LastIndexOf('\\') + 1); }
            name_client_3 = ConfigManager.getConfig("client_path_3");
            if(name_client_3 != "") { name_client_3 = name_client_3.Substring(name_client_3.LastIndexOf('\\') + 1); }
            url_info = url_main_info;
            url_patch = url_main_patch;
            url_vsn = url_main_vsn;
            server = Server.Main;
        }

        version_current = VersionChecker.GetCurrentVersion(this, path_main);
        version_latest = VersionChecker.GetLatestVersion(this, url_vsn);
        if(version_current == "" || version_latest == "") {
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

    private void materialButton_startPatch_Click(object sender, EventArgs e) {
        // 현재 버전을 조작한 경우
        if(version_current != textBox_currentVersion.Text) {
            string text = textBox_currentVersion.Text;
            // 비어있으면 안되며, 숫자 형식이어야 하고, 반드시 5자여야 한다
            if(text == "" || false == int.TryParse(text, out int result) || text.Length != 5) {
                MessageBox.Show("현재 버전을 확인해주세요.\n버전은 5자리 숫자로 이루어져 있습니다.", "현재 버전 확인", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            version_current = textBox_currentVersion.Text;
        }

        DirectoryInfo pathInfo = new DirectoryInfo(path_main + "\\char");
        if(true == pathInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
            MessageBox.Show("잘못된 본클라 경로입니다. 다시 지정해주세요.\n원인 : 원본 폴더가 아닌 생성기로 생성된 폴더입니다.", "경로 인식 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        bool flag = true;
        Action<DirectoryInfo> check = (pathInfo) => {
            if(false == pathInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                DialogResult dr = MessageBox.Show("복사-붙여넣기를 통해 다클 생성 시\n거상 스테이션으로 패치가 불가능합니다.\n확인 버튼을 누르면 열리는 홈페이지를 참고해주세요.", "잘못된 다클라 생성 방식", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if(dr == DialogResult.OK) {
                    Process.Start(new ProcessStartInfo("https://github.com/byungmeo/GersangStation/discussions/8") { UseShellExecute = true });
                }
                flag = false;
            }
        };

        if(name_client_2 != "") {
            pathInfo = new DirectoryInfo(ConfigManager.getConfig((server == Server.Main) ? "client_path_2" : "client_path_test_2") + "\\char");
            check(pathInfo);
            if(!flag) return;
        }

        if(name_client_3 != "") {
            pathInfo = new DirectoryInfo(ConfigManager.getConfig((server == Server.Main) ? "client_path_3" : "client_path_test_3") + "\\char");
            check(pathInfo);
            if(!flag) return;
        }

        int equal = 1;
        if(version_current == version_latest) {
            DialogResult dr = MessageBox.Show(this, "현재 버전과 최신 버전이 같습니다.\n그래도 패치 하시겠습니까?", "버전 같음", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if(dr == DialogResult.No) {
                this.DialogResult = DialogResult.OK;
                return;
            } else {
                equal = 0;
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////

        worker.DoWork += new DoWorkEventHandler((object? sender, DoWorkEventArgs e) => StartPatch(equal));
        worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
        worker.ProgressChanged += ProgressChanged;
        materialButton_startPatch.Enabled = false;
        materialButton_startPatch.Text = "패치 진행중...";
        worker.RunWorkerAsync();
    }

    private void ProgressChanged(object? sender, ProgressChangedEventArgs e) {
        if(this.InvokeRequired) {
            materialButton_startPatch.Invoke((Action)(() => materialButton_startPatch.Text = e.UserState.ToString()));
            progressBar.Invoke((Action)(() => progressBar.Value = e.ProgressPercentage));
        } else {
            materialButton_startPatch.Text = e.UserState.ToString();
            progressBar.Value = e.ProgressPercentage;
        }
    }

    private void Worker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e) {
        materialButton_startPatch.Text = "패치 완료";
        MessageBox.Show(this, "패치가 모두 완료되었습니다.", "패치 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        this.DialogResult = DialogResult.OK;
    }

    public void StartPatch(int equal) {
        // ProgressChanged(this, new ProgressChangedEventArgs(currProgress, $"클라이언트의 파일을 읽어오는 중 입니다.  ({currCount} / {tasks.Count})")
        Trace.WriteLine("패치 시작!");
        DirectoryInfo directory_patch = new DirectoryInfo(Application.StartupPath + @"\patch");
        if(!directory_patch.Exists) { directory_patch.Create(); }

        DirectoryInfo directory_info = new DirectoryInfo(directory_patch + @"\info");
        if(!directory_info.Exists) { directory_info.Create(); }

        DirectoryInfo directory_file = new DirectoryInfo(directory_patch + @"\" + server + "_" + version_current + "-" + version_latest);
        if(!directory_file.Exists) { directory_file.Create(); }

        Dictionary<string, string> list_patchFile = new Dictionary<string, string>(); //key값으로 다운로드주소, value값으로 경로및파일명 저장

        ProgressChanged(this, new ProgressChangedEventArgs(0, "패치 파일 목록 추출 중..."));
        list_patchFile = GetPatchFileList(equal, directory_info, directory_file);

        Trace.WriteLine("패치 정보 파일 병합 완료");

        ProgressChanged(this, new ProgressChangedEventArgs(10, $"다운로드 중... (파일 개수 : {list_patchFile.Count})"));
        bool isSuccess = DownloadAll(list_patchFile);
        if(!isSuccess) return; // 다운로드 실패 시 패치 적용하지 않음.
        Trace.WriteLine("패치 파일 다운로드 완료");

        ProgressChanged(this, new ProgressChangedEventArgs(60, $"압축 해제 중..."));
        ExtractAll(directory_file.FullName);
        Trace.WriteLine("압축 해제 완료");

        //다클라 패치 적용
        if(materialCheckbox_apply.Checked) {
            ProgressChanged(this, new ProgressChangedEventArgs(80, $"다클라 패치 적용 중..."));
            ClientCreator.CreateClient(this, path_main, name_client_2, name_client_3);
        }

        //패치 후 파일 삭제
        if(materialCheckbox_delete.Checked) {
            ProgressChanged(this, new ProgressChangedEventArgs(90, $"패치 후 파일 삭제 중..."));
            try {
                directory_file.Delete(true);
                Trace.WriteLine("패치 파일 폴더 삭제 완료");
            } catch(Exception ex) {
                Trace.WriteLine("패치 파일 폴더 삭제 실패\n" + ex.Message);
            }
        }

        ProgressChanged(this, new ProgressChangedEventArgs(100, $"패치 완료!"));
    }

    public Dictionary<string, string> GetPatchFileList(int equal, DirectoryInfo directory_info, DirectoryInfo directory_file) {
        List<string> list_infoFile = new List<string>();

        using(var webClient = new WebClient()) {
            for(int i = Int16.Parse(version_current) + equal; i <= Int16.Parse(version_latest); i++) {
                string url = url_info + i;
                try {
                    webClient.DownloadFile(new Uri(url), directory_info + @"\" + server + "_" + i + ".txt");
                    Trace.WriteLine(i + " 버전 패치정보 파일 다운로드 성공\n");
                    list_infoFile.Add(i.ToString());
                } catch(Exception ex) {
                    //다운로드 실패 시 다음 버전으로 넘어갑니다
                    Trace.WriteLine("버전 " + i + " 이 존재하지 않아 다음 버전으로 넘어갑니다.\n");
                    Trace.WriteLine(ex.Message);
                }
            }
            Trace.WriteLine("모든 패치정보 파일 다운로드 성공"); ////////////////////////////////////////////////////////////////////////////////--

            Dictionary<string, string> list_patchFile = new Dictionary<string, string>(); //key값으로 다운로드주소, value값으로 경로및파일명 저장

            //몇번의 패치가 존재하든, 한꺼번에 패치하기위해 여러 패치정보파일에서 중복없이 파일 리스트를 뽑아옵니다.
            Stream FS = new FileStream(directory_info + @"\" + server + "_" + version_current + "-" + version_latest + ".txt", FileMode.Create, FileAccess.Write);
            using(var wr = new StreamWriter(FS, Encoding.GetEncoding("euc-kr"))) { //디버깅용으로 새로운 정보 파일을 생성합니다.
                wr.WriteLine("파일명\t다운로드주소\t경로"); //디버깅용
                foreach(string item in list_infoFile) {
                    string[] lines = File.ReadAllLines(directory_info + @"\" + server + "_" + item + ".txt", Encoding.GetEncoding("euc-kr")); //패치정보파일에서 모든 텍스트를 읽어옵니다.

                    //패치정보파일의 첫 4줄은 쓸모없으므로 생략하고, 5번째 줄부터 읽습니다.
                    for(int i = 4; i < lines.Length; i++) {
                        string[] row = lines[i].Split('\t'); //한 줄을 탭을 간격으로 나눕니다. (디버깅용)

                        //만약 EOF가 등장했다면 루프를 빠져나갑니다.
                        if(row[0] == ";EOF") {
                            break;
                        }

                        //row[1] == 파일명
                        //row[3] == 내부경로
                        string file_name = row[1];
                        string file_inner_path = row[3];
                        string file_download_address = url_patch + file_inner_path + file_name;
                        string file_full_path = directory_file.FullName + @"\" + file_inner_path + file_name;

                        if(!list_patchFile.ContainsKey(file_download_address)) {
                            //내부 폴더 생성
                            DirectoryInfo fileInnerDirectory = new DirectoryInfo(new FileInfo(file_full_path).DirectoryName);
                            if(!fileInnerDirectory.Exists) { fileInnerDirectory.Create(); }

                            if(File.Exists(file_full_path))
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

    public void ExtractAll(string patch_dir) {
        foreach(string file in Directory.EnumerateFiles(patch_dir, "*.*", SearchOption.AllDirectories)) {
            string dest = path_main + file.Remove(0, patch_dir.Length);
            dest = dest.Remove(dest.LastIndexOf('\\'));
            Trace.WriteLine(file + " -> " + dest);
            try {
                ZipFile.ExtractToDirectory(file, dest, true);
            } catch(Exception ex) {
                Trace.WriteLine("압축 오류 발생 : " + file);
                Trace.WriteLine(ex.StackTrace);
            }
        }
    }

    public bool DownloadAll(Dictionary<string, string> list) {
        // 리스트의 모든 파일을 다운로드
        Parallel.ForEach(
            list,
            new ParallelOptions { MaxDegreeOfParallelism = 10 },
            DownloadFile);

        // 아직도 모든 파일을 다운로드 하지 못한 경우
        if(list_retry.Count > 0) {
            foreach(var item in list_retry) {
                Trace.WriteLine("다운로드 실패한 파일 주소 : " + item.Key);
            }

            Trace.WriteLine("다운로드 실패한 파일을 재다운로드 합니다.");

            // NUM_RETRY 만큼 다운로드 재시도
            for(int i = 0; i < NUM_RETRY; i++) {
                if(list_retry.Count == 0) {
                    break;
                }

                Trace.WriteLine(i + 1 + "번째 재다운로드 시도... 남은 파일 수 : " + list_retry.Count + "개");

                Parallel.ForEach(
                list_retry,
                new ParallelOptions { MaxDegreeOfParallelism = 10 },
                RetryDownloadFile);
            }

            // 그럼에도 실패
            if(list_retry.Count > 0) {
                Trace.WriteLine($"{NUM_RETRY}번의 재다운로드 시도 결과 모든 파일을 재다운로드 하는데 실패하였습니다.");
                foreach(var item in list_retry) {
                    Trace.WriteLine("다운로드 실패한 파일 주소 : " + item.Key);
                }
                this.Invoke(new Action(() => {
                    MessageBox.Show(this,
                        $"{NUM_RETRY}번의 패치 재시도에도 불구하고\n" +
                        $"모든 파일을 다운로드하는데 실패하였습니다.\n" +
                        $"인터넷 환경을 확인해주시고 다시 패치를 진행해주세요.",
                        "패치 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));

                list_retry.Clear();
                return false;
            } else {
                Trace.WriteLine("모든 파일을 성공적으로 다운로드 하였습니다.");
            }
        } else {
            Trace.WriteLine("모든 파일을 성공적으로 다운로드 하였습니다.");
        }

        Trace.WriteLine("다운로드 종료");
        list_retry.Clear();
        return true;
    }

    public void DownloadFile(KeyValuePair<string, string> item) {
        using(var client = new WebClient()) {
            client.Headers.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 8.0)");

            string file_name = item.Value.Substring(item.Value.LastIndexOf('\\') + 1);

            //내부 폴더 생성
            DirectoryInfo fileInnerDirectory = new DirectoryInfo(new FileInfo(item.Value).DirectoryName);
            if(!fileInnerDirectory.Exists) { fileInnerDirectory.Create(); }

            Trace.WriteLine($"[다운로드 시작] {file_name}");

            try {
                client.DownloadFile(item.Key, item.Value);
                Trace.WriteLine($"[다운로드 성공] {file_name}");
            } catch(WebException e) {
                Trace.WriteLine($"[다운로드 실패] {file_name}\n" + e.StackTrace);
                lock(listLock) {
                    list_retry.Add(item.Key, item.Value);
                }
            }
        }
    }

    public void RetryDownloadFile(KeyValuePair<string, string> item) {
        using(var client = new WebClient()) {
            client.Headers.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 8.0)");

            string file_name = item.Value.Substring(item.Value.LastIndexOf('\\') + 1);

            //내부 폴더 생성
            DirectoryInfo fileInnerDirectory = new(path: new FileInfo(item.Value).DirectoryName);
            if(!fileInnerDirectory.Exists) { fileInnerDirectory.Create(); }

            Trace.WriteLine($"[재다운로드 시작] {file_name}");

            try {
                client.DownloadFile(item.Key, item.Value);
                Trace.WriteLine($"[재다운로드 성공] {file_name}");
                lock(listLock) {
                    list_retry.Remove(item.Key);
                }
            } catch(WebException e) {
                Trace.WriteLine($"[재다운로드 실패] {file_name}\n" + e.StackTrace);
            }
        }
    }

    private void Form_Patcher_FormClosing(object sender, FormClosingEventArgs e) {
        if(worker.IsBusy) {
            DialogResult dr = MessageBox.Show("패치 중에는 중단할 수 없습니다.", "중단 불가", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            e.Cancel = true;
        }
    }
}
