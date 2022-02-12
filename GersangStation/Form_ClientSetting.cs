using MaterialSkin.Controls;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GersangStation {
    public partial class Form_ClientSetting : MaterialForm {
        public Form_ClientSetting() {
            InitializeComponent();
        }

        private void Form_ClientSetting_Load(object sender, EventArgs e) {
            textBox_path_1.Text = ConfigManager.getConfig("client_path_1");
            textBox_path_2.Text = ConfigManager.getConfig("client_path_2");
            textBox_path_3.Text = ConfigManager.getConfig("client_path_3");

            textBox_path_test_1.Text = ConfigManager.getConfig("client_path_test_1");
            textBox_path_test_2.Text = ConfigManager.getConfig("client_path_test_2");
            textBox_path_test_3.Text = ConfigManager.getConfig("client_path_test_3");

            materialCheckbox_autoUpdate.Checked = bool.Parse(ConfigManager.getConfig("is_auto_update"));
        }

        private void materialButton_findPath_Click(object sender, EventArgs e) {
            MaterialButton button = (MaterialButton)sender;
            folderBrowserDialog.ShowDialog();
            if (folderBrowserDialog.SelectedPath.Length == 0) { return; }
            
            if (button.Equals(materialButton_findPath_1)) { textBox_path_1.Text = folderBrowserDialog.SelectedPath; } 
            else if (button.Equals(materialButton_findPath_2)) { textBox_path_2.Text = folderBrowserDialog.SelectedPath; } 
            else if (button.Equals(materialButton_findPath_3)) { textBox_path_3.Text = folderBrowserDialog.SelectedPath; } 
            else if (button.Equals(materialButton_findPath_test_1)) { textBox_path_test_1.Text = folderBrowserDialog.SelectedPath; } 
            else if (button.Equals(materialButton_findPath_test_2)) { textBox_path_test_2.Text = folderBrowserDialog.SelectedPath; } 
            else if (button.Equals (materialButton_findPath_test_3)) { textBox_path_test_3.Text = folderBrowserDialog.SelectedPath; }
        }
        private void materialButton_save_Click(object sender, EventArgs e) {
            SavePath();
            ConfigManager.setConfig("is_auto_update", materialCheckbox_autoUpdate.Checked.ToString());
            this.DialogResult = DialogResult.OK;
        }

        private void SavePath() {
            ConfigManager.setConfig("client_path_1", textBox_path_1.Text);
            ConfigManager.setConfig("client_path_2", textBox_path_2.Text);
            ConfigManager.setConfig("client_path_3", textBox_path_3.Text);

            ConfigManager.setConfig("client_path_test_1", textBox_path_test_1.Text);
            ConfigManager.setConfig("client_path_test_2", textBox_path_test_2.Text);
            ConfigManager.setConfig("client_path_test_3", textBox_path_test_3.Text);
        }

        private void materialButton_createClient_Click(object sender, EventArgs e) {
            MaterialButton button = (MaterialButton)sender;

            string mainClientPathConfigKey = "";
            string nameConfigKey = "";
            string recommendName = "";
            string path = "";
            string mainClientPath = "";
            TextBox obj_path_2;
            TextBox obj_path_3;

            if (button.Equals(materialButton_createClient)) {
                mainClientPathConfigKey = "client_path_1";
                nameConfigKey = "directory_name_client_";
                recommendName = "권장 폴더명 : Gersang2, Gersang3";
                mainClientPath = textBox_path_1.Text;
                obj_path_2 = textBox_path_2;
                obj_path_3 = textBox_path_3;
            } else {
                mainClientPathConfigKey = "client_path_test_1";
                nameConfigKey = "directory_name_client_test_";
                recommendName = "권장 폴더명 : GerTest2, GerTest3";
                mainClientPath = textBox_path_test_1.Text;
                obj_path_2 = textBox_path_test_2;
                obj_path_3 = textBox_path_test_3;
            }

            path = ConfigManager.getConfig(mainClientPathConfigKey);

            if (path != mainClientPath) {
                DialogResult dr = MessageBox.Show("현재 변경사항을 저장 후 생성하시겠습니까?", "변경사항 감지", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                if (dr == DialogResult.OK) {
                    SavePath();
                    path = ConfigManager.getConfig(mainClientPathConfigKey);
                }
            }

            if (path == "") {
                MessageBox.Show("본클라 경로가 설정되지 않았습니다.", "생성 불가", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            MaterialTextBox textBox_second = new MaterialTextBox() {
                Hint = "2클라 폴더명 입력",
                UseAccent = false,
                Size = new Size(170, 50),
                Location = new Point(15, 40),
                Text = ConfigManager.getConfig(nameConfigKey + '2')
            };
            dialog_name.Controls.Add(textBox_second);

            //3클라 폴더명
            MaterialTextBox textBox_third = new MaterialTextBox() {
                Hint = "3클라 폴더명 입력",
                UseAccent = false,
                Size = new Size(170, 50),
                Location = new Point(15, 100),
                Text = ConfigManager.getConfig(nameConfigKey + '3')
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
                if (!regex.IsMatch(textBox_second.Text) || !regex.IsMatch(textBox_third.Text)) {
                    MessageBox.Show(this, "폴더 이름으로 사용할 수 없는 문자가 포함되어 있습니다.\n다시 입력해주세요.", "잘못된 폴더명", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                    
                secondName = textBox_second.Text;
                thirdName = textBox_third.Text;

                ConfigManager.setConfig(nameConfigKey + '2', secondName);
                ConfigManager.setConfig(nameConfigKey + '3', thirdName);

                if (checkBox_apply.Checked) {
                    obj_path_2.Text = mainClientPath + "\\..\\" + secondName;
                    obj_path_3.Text = mainClientPath + "\\..\\" + thirdName;
                }

                dialog_name.DialogResult = DialogResult.OK;
            };
            dialog_name.Controls.Add(button_ok);
            dialog_name.AcceptButton = button_ok; //엔터 버튼을 누르면 이 버튼을 클릭합니다.
            if (dialog_name.ShowDialog() != DialogResult.OK) {
                backgroundForm.Dispose();
                return;
            } else {
                backgroundForm.Dispose();
            }

            try {
                ClientCreator.client_create(path, secondName, thirdName);
                MessageBox.Show(this, "다클라 생성을 완료하였습니다.\n다클라 폴더의 이름은 " + secondName + ", " + thirdName + " 입니다.", "다클라 생성", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } catch (Exception ex) {
                Trace.WriteLine(ex.Message);
                MessageBox.Show(this, "다클라 생성중 오류가 발생한 것 같습니다. 문의해주세요.\n" + ex.StackTrace, "다클라 생성", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
