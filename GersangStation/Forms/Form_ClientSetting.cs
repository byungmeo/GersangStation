using GersangStation.Modules;
using MaterialSkin.Controls;
using System.Diagnostics;
using System.Text.RegularExpressions;
using static GersangStation.Form1;

namespace GersangStation;
public partial class Form_ClientSetting : MaterialForm {
    private bool isChanged = false;
    private Server server;
    private string Opt => (server == Server.Main) ? "" : (server == Server.Test) ? "test_" : "rnd_";

    public Form_ClientSetting(Server server) {
        InitializeComponent();
        this.server = server;
    }

    private void Form_ClientSetting_Load(object sender, EventArgs e) {
        comboBox_selectServer.SelectedIndex = (int)server;
    }

    private void materialButton_findPath_Click(object sender, EventArgs e) {
        MaterialButton button = (MaterialButton)sender;
        folderBrowserDialog.ShowDialog();
        if(folderBrowserDialog.SelectedPath.Length == 0) { return; }

        Action<TextBox> selectDir = (textBox) => {
            textBox.Text = folderBrowserDialog.SelectedPath;
            isChanged = true;
        };

        if(button.Equals(materialButton_findPath_1)) selectDir(textBox_path_1);
        else if(button.Equals(materialButton_findPath_2)) selectDir(textBox_path_2);
        else if(button.Equals(materialButton_findPath_3)) selectDir(textBox_path_3);
    }

    private void materialButton_save_Click(object sender, EventArgs e) {
        Save();
        this.DialogResult = DialogResult.OK;
    }

    private void Save() {
        ConfigManager.SetConfig($"client_path_{Opt}1", textBox_path_1.Text);
        ConfigManager.SetConfig($"client_path_{Opt}2", textBox_path_2.Text);
        ConfigManager.SetConfig($"client_path_{Opt}3", textBox_path_3.Text);
        ConfigManager.SetConfig("is_auto_update", materialCheckbox_autoUpdate.Checked.ToString());
        isChanged = false;
    }

    private void materialButton_createClient_Click(object sender, EventArgs e) {
        MaterialButton button = (MaterialButton)sender;

        string mainClientPathConfigKey;
        string nameConfigKey;
        string recommendName;
        string path;
        string mainClientPath;
        TextBox textBoxPath2;
        TextBox textBoxPath3;

        mainClientPath = textBox_path_1.Text;
        textBoxPath2 = textBox_path_2;
        textBoxPath3 = textBox_path_3;
        mainClientPathConfigKey = $"client_path_{Opt}1";
        nameConfigKey = $"directory_name_client_{Opt}";

        if(server == Server.Main) recommendName = "권장 폴더명 : Gersang2, Gersang3";
        else if(server == Server.Test) recommendName = "권장 폴더명 : GerTest2, GerTest3";
        else recommendName = "권장 폴더명 : CheonRa2, CheonRa3";

        path = ConfigManager.GetConfig(mainClientPathConfigKey);

        if(path != mainClientPath) {
            DialogResult dr = MessageBox.Show("현재 변경사항을 저장 후 생성하시겠습니까?", "변경사항 감지", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if(dr == DialogResult.OK) {
                Save();
                path = ConfigManager.GetConfig(mainClientPathConfigKey);
            }
        }

        if(path == "") {
            MessageBox.Show("본클라 경로가 설정되지 않았습니다.", "생성 불가", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        string secondName = "";
        string thirdName = "";

        Form backgroundForm = Form1.InitBackgroundForm(this);
        backgroundForm.Show();

        //클라 폴더이름 지정 대화상자
        MaterialForm dialog_name = new MaterialForm() {
            FormStyle = FormStyles.ActionBar_None,
            Sizable = false,
            StartPosition = FormStartPosition.CenterParent,
            //Size = new Size(200, 270),
            Size = new Size(200, 255),
            MaximizeBox = false,
            MinimizeBox = false,
            TopMost = true,
            ShowInTaskbar = false,
            Owner = this
        };

        //2클라 폴더명
        MaterialTextBox2 textBox_second = new MaterialTextBox2() {
            Hint = "2클라 폴더명 입력",
            UseAccent = false,
            Size = new Size(170, 50),
            Location = new Point(15, 40),
            Text = ConfigManager.GetConfig(nameConfigKey + '2')
        };
        dialog_name.Controls.Add(textBox_second);

        //3클라 폴더명
        MaterialTextBox2 textBox_third = new MaterialTextBox2() {
            Hint = "3클라 폴더명 입력",
            UseAccent = false,
            Size = new Size(170, 50),
            Location = new Point(15, 100),
            Text = ConfigManager.GetConfig(nameConfigKey + '3')
        };
        dialog_name.Controls.Add(textBox_third);

        //클라 폴더 이름 권장사항
        Label label_information = new Label() {
            Font = new Font("Noto Sans KR", 8),
            ForeColor = Color.Red,
            Location = new Point(15, 160),
            AutoSize = false,
            Size = new Size(200, 20),
            Text = recommendName
        };
        dialog_name.Controls.Add(label_information);

        CheckBox checkBox_apply = new CheckBox() {
            Location = new Point(25, 180),
            Size = new Size(200, 20),
            Checked = true,
            Text = "2,3클라 자동 경로 설정"
        };
        dialog_name.Controls.Add(checkBox_apply);

        //생성 버튼
        MaterialButton button_ok = new MaterialButton() {
            Text = "생성",
            AutoSize = false,
            Size = new Size(64, 36),
            Location = new Point(68, 205)
        };
        button_ok.Click += (sender, e) => {
            Regex regex = new Regex("^([a-zA-Z0-9][^*/><?\"|:]*)$");
            if(!regex.IsMatch(textBox_second.Text) || !regex.IsMatch(textBox_third.Text)) {
                MessageBox.Show(this, "폴더 이름으로 사용할 수 없는 문자가 포함되어 있습니다.\n다시 입력해주세요.", "잘못된 폴더명", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            secondName = textBox_second.Text;
            thirdName = textBox_third.Text;

            ConfigManager.SetConfig(nameConfigKey + '2', secondName);
            ConfigManager.SetConfig(nameConfigKey + '3', thirdName);

            if(checkBox_apply.Checked) {
                textBoxPath2.Text = mainClientPath + "\\..\\" + secondName;
                textBoxPath3.Text = mainClientPath + "\\..\\" + thirdName;

                ConfigManager.SetConfig($"client_path_{Opt}1", textBox_path_1.Text);
                ConfigManager.SetConfig($"client_path_{Opt}2", textBox_path_2.Text);
                ConfigManager.SetConfig($"client_path_{Opt}3", textBox_path_3.Text);
            }

            dialog_name.DialogResult = DialogResult.OK;
        };
        dialog_name.Controls.Add(button_ok);
        dialog_name.AcceptButton = button_ok; //엔터 버튼을 누르면 이 버튼을 클릭합니다.

        if(dialog_name.ShowDialog() != DialogResult.OK) {
            backgroundForm.Dispose();
            return;
        } else {
            backgroundForm.Dispose();
        }

        try {
            if(false == ClientCreator.CreateClient(this, path, secondName, thirdName))
                return;
            MessageBox.Show(this, "다클라 생성을 완료하였습니다.\n다클라 폴더의 이름은 " + secondName + ", " + thirdName + " 입니다.", "다클라 생성", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Save();
        } catch (Exception ex) {
            Trace.WriteLine(ex.Message);
            MessageBox.Show(this, "다클라 생성중 오류가 발생한 것 같습니다. 문의해주세요.\n" + ex.StackTrace, "다클라 생성", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void materialButton_patch_Click(object sender, EventArgs e) {
        string mainClientPathConfigKey = $"client_path_{Opt}1";
        string mainClientPath = textBox_path_1.Text;

        string path = ConfigManager.GetConfig(mainClientPathConfigKey);

        if (path != mainClientPath) {
            DialogResult dr = MessageBox.Show(this, "현재 변경사항을 저장 후 생성하시겠습니까?", "변경사항 감지", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (dr == DialogResult.OK) {
                Save();
                path = ConfigManager.GetConfig(mainClientPathConfigKey);
            }
        }

        if (path == "") {
            MessageBox.Show(this, "본클라 경로가 설정되지 않았습니다.", "패치 불가", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Form backgroundForm = Form1.InitBackgroundForm(this);

        Form_Patcher form_Patcher = new Form_Patcher(server) {
            Owner = this
        };

        try {
            backgroundForm.Show();
            form_Patcher.ShowDialog();
        } catch (Exception ex) {
            Trace.WriteLine(ex.StackTrace);
        } finally {
            backgroundForm.Dispose();
        }
    }

    private void Form_ClientSetting_FormClosing(object sender, FormClosingEventArgs e) {
        if(isChanged) {
            DialogResult dr = MessageBox.Show(this, "변경 사항을 저장 하시겠습니까?", "변경사항 감지", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (dr == DialogResult.OK)
                Save();
        }
    }

    private void materialCheckbox_autoUpdate_CheckedChanged(object sender, EventArgs e) {
        isChanged = true;
    }

    private void comboBox_selectServer_SelectedIndexChanged(object sender, EventArgs e) {
        ComboBox comboBox = (ComboBox)sender;
        server = (Server)comboBox.SelectedIndex;
        textBox_path_1.Text = ConfigManager.GetConfig($"client_path_{Opt}1");
        textBox_path_2.Text = ConfigManager.GetConfig($"client_path_{Opt}2");
        textBox_path_3.Text = ConfigManager.GetConfig($"client_path_{Opt}3");
        materialCheckbox_autoUpdate.Checked = bool.Parse(ConfigManager.GetConfig("is_auto_update"));
    }
}
