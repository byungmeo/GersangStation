using System.ComponentModel;
using System.Diagnostics;
using GersangStation.Modules;
using MaterialSkin.Controls;

namespace GersangStation; 
public partial class Form_IntegrityCheck : MaterialForm {
    private BackgroundWorker worker = new BackgroundWorker();
    Thread? checkThread = null;
    Dictionary<string, string>? result = null;
    private const string url_main = @"https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/";
    private const string url_main_patch = url_main + @"Client_Patch_File/"; // + "{경로}/{파일명.확장자}"

    public Form_IntegrityCheck() {
        InitializeComponent();
        this.FormClosing += Form_IntegrityCheck_FormClosing;
        this.FormClosed += Form_IntegrityCheck_FormClosed;
        materialExpansionPanel1.Hide();
    }

    private void Form_IntegrityCheck_FormClosed(object? sender, FormClosedEventArgs e) {
        if(sender != this) return;
        if(checkThread != null) {
        }
    }

    private void Form_IntegrityCheck_FormClosing(object? sender, FormClosingEventArgs e) {
        if(sender != this || checkThread == null) return;
        if(checkThread.IsAlive == false) return;
        DialogResult dr = MessageBox.Show("유효성 검사 중에는 중단할 수 없습니다.", "", MessageBoxButtons.OK);
        e.Cancel = true;
    }


    private void Form_IntegrityCheck_Load(object sender, EventArgs e) {
        textBox_clientPath.Text = ConfigManager.GetConfig("client_path_1");
    }

    private void materialButton_findPath_Click(object sender, EventArgs e) {
        MaterialButton button = (MaterialButton)sender;
        FolderBrowserDialog dlg = new FolderBrowserDialog();
        dlg.ShowDialog();
        if(dlg.SelectedPath.Length == 0) { return; }

        Action<TextBox> selectDir = (textBox) => {
            textBox.Text = dlg.SelectedPath;
        };
        selectDir(textBox_clientPath);
    }

    private void materialButton_start_Click(object sender, EventArgs e) {
        if(ClipMouse.GetAllGameHandles().Count > 0) {
            MessageBox.Show(this, 
                "게임 실행 중에는 유효성 검사를 할 수 없습니다.", 
                "유효성 검사 실패", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if(materialButton_start.Text.Contains("복원")) {
            checkThread = null;
            if(Directory.Exists(Directory.GetCurrentDirectory() + @"\Temp")) {
                Directory.Delete(Directory.GetCurrentDirectory() + @"\Temp", true);
            }

            //Restore mode
            Dictionary<string, string> downloadItems = new();
            foreach(var item in checkedListBox1.CheckedItems) {
                string? currString = item.ToString();
                if(currString != null) {
                    int ends = currString.IndexOf(">");
                    int start = currString.IndexOf("<");

                    currString = currString.Substring(start + 1, ends - (start + 1));

                    string k = url_main_patch + currString.Replace(@"\", "/") + ".gsz";
                    string v = Directory.GetCurrentDirectory() + @"\Temp\" + currString;
                    downloadItems.Add(k, v);

                    Trace.WriteLine(currString + "을 복원합니다");
                }
            }

            PatchFileDownloader downloader = new PatchFileDownloader();
            bool isSucceeded = downloader.DownloadAll(downloadItems, true);
            if(isSucceeded) {
                string clientPath = textBox_clientPath.Text;
                if(clientPath.EndsWith(@"\") == false) {
                    clientPath += @"\";
                }

                downloader.ExtractAll(Directory.GetCurrentDirectory() + @"\Temp\", clientPath, "", "", false);
                materialButton_start.Text = "완료";
                materialButton_start.Enabled = false;
                materialExpansionPanel1.Hide();
                MessageBox.Show(this, $"{downloadItems.Count}개의 파일을 복원했습니다");
            } else {
                MessageBox.Show(this, "거상 서버에서 해당 파일을 가져오지 못했습니다. 재설치를 권장합니다.");
            }
        } else {
            //Check path
            if(!Directory.Exists(textBox_clientPath.Text)) {
                MessageBox.Show(this, "해당 폴더가 존재하지 않습니다.", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            //Disable all controls
            materialButton_findPath.Enabled = false;
            materialButton_start.Enabled = false;
            materialButton_start.Text = "실행중";

            string reportFileName = "";
            //Run checker
            try {
                if(Directory.Exists(Directory.GetCurrentDirectory() + @"\Temp")) {
                    Directory.Delete(Directory.GetCurrentDirectory() + @"\Temp", true);
                }

                IntegrityChecker? checker = IntegrityChecker.CreateIntegrityChecker(textBox_clientPath.Text, Directory.GetCurrentDirectory() + @"\Temp");
                checker.ProgressChanged += IntegrityCheckerEventHandler;
                result = new();
                worker.DoWork += (object? sender, DoWorkEventArgs e) => { checker.Run(out reportFileName, ref result); };
                worker.RunWorkerCompleted += (object? sender, RunWorkerCompletedEventArgs e) => { 
                    MessageBox.Show(this, "유효성 검사를 완료했습니다. \r\n자세히 보기를 참조해주세요.", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };
                worker.RunWorkerAsync();
            } catch(Exception except) {
                MessageBox.Show(this, except.Message, "유효성 검사에 실패하였습니다", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void IntegrityCheckerEventHandler(object sender, ProgressChangedEventArgs e) {
        if(this.InvokeRequired) {
            materialButton_start.Invoke((Action)(() => materialButton_start.Text = e.UserState.ToString()));
            progressBar.Invoke((Action)(() => progressBar.Value = e.ProgressPercentage));
        } else {
            materialButton_start.Text = e.UserState.ToString();
            progressBar.Value = e.ProgressPercentage;
        }

        if(e.ProgressPercentage == 100) {
            foreach(var item in result) {
                checkedListBox1.Invoke((Action)((() => checkedListBox1.Items.Add($"<{item.Key.ReplaceLineEndings().Replace(Environment.NewLine, "")}>{item.Value}", CheckState.Checked))));
            }

            if(this.InvokeRequired) {
                if(result.Count > 0) {
                    materialButton_start.Invoke((Action)(() => materialButton_start.Text = $"{result.Count}개의 파일 복원하기"));
                    materialButton_start.Invoke((Action)(() => materialButton_start.Enabled = true));
                } else {
                    materialButton_start.Invoke((Action)(() => materialButton_start.Text = $"모든 파일이 일치합니다"));
                    materialButton_start.Invoke((Action)(() => materialButton_start.Enabled = false));
                }
                materialExpansionPanel1.Invoke((Action)((() => materialExpansionPanel1.Show())));
            } else {
                if(result.Count > 0) {
                    materialButton_start.Text = $"{result.Count}개의 파일 복원하기";
                    materialButton_start.Enabled = true;
                } else {
                    materialButton_start.Text = $"모든 파일이 일치합니다";
                    materialButton_start.Enabled = false;
                }
                materialExpansionPanel1.Show();
            }

            if(Directory.Exists(Directory.GetCurrentDirectory() + @"\Temp")) {
                Directory.Delete(Directory.GetCurrentDirectory() + @"\Temp", true);
            }
        }
    }

    private void checkedListBox1_ItemCheck(object sender, ItemCheckEventArgs e) {
        int count = checkedListBox1.CheckedItems.Count;
        if(e.NewValue == CheckState.Checked) {
            count++;
        } else {
            count--;
        }
        materialButton_start.Text = $"{count}개의 파일 복원하기";
        if(count > 0)
            materialButton_start.Enabled = true;
        else
            materialButton_start.Enabled = false;
    }
}
