using MaterialSkin.Controls;
using System.Diagnostics;
using System.Text;

namespace GersangStation {
    public partial class Form_AccountSetting : MaterialForm {
        public Form_AccountSetting() {
            InitializeComponent();
        }

        private void Form_AccountSetting_Load(object sender, EventArgs e) {
            LoadListBox();
        }

        private void materialButton_close_Click(object sender, EventArgs e) {
            this.DialogResult = DialogResult.OK;
        }

        private void materialButton_addAccount_Click(object sender, EventArgs e) {
            Form backgroundForm = new Form() {
                StartPosition = FormStartPosition.Manual,
                FormBorderStyle = FormBorderStyle.None,
                Opacity = .50d,
                BackColor = Color.Black,
                Location = this.Location,
                Size = this.Size,
                ShowInTaskbar = false,
                TopMost = true,
                Owner = this
            };
            backgroundForm.Show();

            //계정 추가 대화상자용 폼
            MaterialForm dialog_addAccount = new MaterialForm() {
                FormStyle = FormStyles.ActionBar_None,
                Sizable = false,
                StartPosition = FormStartPosition.CenterParent,
                //Size = new Size(200, 270),
                Size = new Size(200, 210),
                Text = "계정 추가",
                MaximizeBox = false,
                MinimizeBox = false,
                TopMost = true,
                ShowInTaskbar = false,
                Owner = this
            };

            //id 입력 텍스트박스
            MaterialTextBox2 textBox_id = new MaterialTextBox2() {
                Hint = "ID 입력",
                UseAccent = false,
                Size = new Size(170, 50),
                Location = new Point(15, 40),
            };
            dialog_addAccount.Controls.Add(textBox_id);

            //패스워드 입력 텍스트박스
            MaterialTextBox2 textBox_pw = new MaterialTextBox2() {
                Hint = "패스워드 입력",
                UseAccent = false,
                Size = new Size(170, 50),
                Location = new Point(15, 100),
                UseSystemPasswordChar = true,
                PasswordChar = '●'
            };
            dialog_addAccount.Controls.Add(textBox_pw);

            /*
            //별명 입력
            MaterialTextBox2 textBox_nickname = new MaterialTextBox2() {
                Hint = "별명 입력",
                UseAccent = false,
                Size = new Size(170, 50),
                Location = new Point(15, 160),
            };
            dialog_addAccount.Controls.Add(textBox_nickname);
            */

            //계정 추가 버튼
            MaterialButton button_confirm = new MaterialButton() {
                Text = "추가",
                AutoSize = false,
                Size = new Size(64, 36),
                Location = new Point(68, 160)
            };
            button_confirm.Click += (sender, e) => {
                if (textBox_id.Text.Length == 0 || textBox_pw.Text.Length == 0) {
                    MessageBox.Show("아이디 또는 비밀번호를 입력 해주세요.");
                    return;
                }

                if (textBox_id.Text.Contains(' ')) {
                    MessageBox.Show("아이디에 공백이 포함되어 있습니다.\n다시 입력 해주세요.");
                    textBox_id.Text = "";
                    return;
                }

                if (textBox_pw.Text.Contains(' ')) {
                    MessageBox.Show("패스워드에 공백이 포함되어 있습니다.\n다시 입력 해주세요.");
                    textBox_pw.Text = "";
                    return;
                }

                if (ConfigManager.getConfig("account_list").Split(';').Contains(textBox_id.Text)) {
                    MessageBox.Show("이미 동일한 계정이 존재합니다.");
                    return;
                }

                dialog_addAccount.DialogResult = DialogResult.OK;
            };
            dialog_addAccount.Controls.Add(button_confirm);
            dialog_addAccount.AcceptButton = button_confirm; //엔터 버튼을 누르면 이 버튼을 클릭합니다.

            //계정 추가 버튼 클릭 시
            if (dialog_addAccount.ShowDialog() == DialogResult.OK) {
                string id = textBox_id.Text;
                string pw = EncryptionSupporter.Protect(textBox_pw.Text);
                Trace.WriteLine("ShowDialog ID : " + id);
                Trace.WriteLine("ShowDialog PW : " + pw);

                ConfigManager.addConfig(id, pw);
                ConfigManager.setConfig("account_list", ConfigManager.getConfig("account_list") + textBox_id.Text + ";");

                LoadListBox();
            }
            backgroundForm.Dispose();
        }

        private void materialButton2_Click(object sender, EventArgs e) {
            Trace.WriteLine(materialListBox1.SelectedItem.Text);
            int index = materialListBox1.SelectedIndex;
            byte current_preset = Byte.Parse(ConfigManager.getConfig("current_preset"));
            int[] temp = Array.ConvertAll(ConfigManager.getConfig("current_comboBox_index_preset_" + current_preset).Split(';'), s => int.Parse(s));
            StringBuilder sb = new StringBuilder();
            foreach (var item in temp) {
                if (item > index) { sb.Append((item - 1).ToString() + ';'); } else { sb.Append(item.ToString() + ';'); }
            }
            sb.Remove(sb.Length - 1, 1);
            ConfigManager.setConfig("current_comboBox_index_preset_" + current_preset, sb.ToString());
            ConfigManager.removeConfig(materialListBox1.SelectedItem.Text);
            string account_list = ConfigManager.getConfig("account_list");
            account_list = account_list.Remove(account_list.IndexOf(materialListBox1.SelectedItem.Text), materialListBox1.SelectedItem.Text.Length + 1);
            ConfigManager.setConfig("account_list", account_list);
            materialListBox1.RemoveItemAt(index);

            LoadListBox();
        }

        private void LoadListBox() {
            materialListBox1.Clear();
            string[] accountList = ConfigManager.getConfig("account_list").Split(';');
            foreach (var item in accountList) { if (!item.Equals("")) { materialListBox1.AddItem(item); } }
        }
    }
}
