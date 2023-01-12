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
            if (sender.Equals(materialButton_changeAccount)) {
                if (materialListBox1.SelectedIndex == -1) {
                    return;
                }
            }

            Form backgroundForm = Form1.InitBackgroundForm(this);
            backgroundForm.Show();

            //계정 추가 대화상자용 폼
            MaterialForm dialog_addAccount = new MaterialForm() {
                FormStyle = FormStyles.ActionBar_None,
                Sizable = false,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(240, 268),
                MaximizeBox = false,
                MinimizeBox = false,
                TopMost = true,
                ShowInTaskbar = false,
                Owner = this,
                ImeMode = ImeMode.NoControl // 전각 반각 오류 방지
            };

            //id 입력 텍스트박스
            MaterialTextBox2 textBox_id = new MaterialTextBox2() {
                Hint = "ID 입력",
                Size = new Size(206, 48),
                Location = new Point(17, 40),
                ImeMode = ImeMode.NoControl // 전각 반각 오류 방지
            };
            dialog_addAccount.Controls.Add(textBox_id);

            //패스워드 입력 텍스트박스
            MaterialTextBox2 textBox_pw = new MaterialTextBox2() {
                Hint = "패스워드 입력",
                Size = new Size(206, 48),
                Location = new Point(17, 100),
                UseSystemPasswordChar = true,
                PasswordChar = '●',
                ImeMode = ImeMode.NoControl // 전각 반각 오류 방지
            };
            dialog_addAccount.Controls.Add(textBox_pw);

            //별명 입력
            MaterialTextBox2 textBox_nickname = new MaterialTextBox2() {
                Hint = "별명 입력 (선택사항)",
                Size = new Size(162, 48),
                Location = new Point(17, 160),
                Enabled = false,
                ImeMode = ImeMode.NoControl // 전각 반각 오류 방지
            };
            dialog_addAccount.Controls.Add(textBox_nickname);

            MaterialCheckbox checkBox_useNickname = new MaterialCheckbox() {
                Text = "",
                Checked = false,
                Location = new Point(184, 166),
                TabStop = false,
                ImeMode = ImeMode.NoControl // 전각 반각 오류 방지
            };
            checkBox_useNickname.CheckedChanged += (sender, e) => {
                if (true == checkBox_useNickname.Checked) {
                    textBox_nickname.Enabled = true;
                } else {
                    textBox_nickname.Enabled = false;
                    textBox_nickname.Text = textBox_id.Text;
                }
            };
            textBox_id.TextChanged += (sender, e) => {
                if (false == checkBox_useNickname.Checked) {
                    textBox_nickname.Text = textBox_id.Text;
                }
            };
            dialog_addAccount.Controls.Add(checkBox_useNickname);

            //계정 추가 버튼
            CustomButton button_confirm = new CustomButton() {
                Text = "추가",
                AutoSize = false,
                Size = new Size(64, 36),
                Location = new Point(88, 219),
                ImeMode = ImeMode.NoControl // 전각 반각 오류 방지
            };
            
            dialog_addAccount.Controls.Add(button_confirm);
            dialog_addAccount.AcceptButton = button_confirm; //엔터 버튼을 누르면 이 버튼을 클릭합니다.

            if (sender.Equals(materialButton_addAccount)) {
                button_confirm.Click += (sender, e) => {
                    if (textBox_id.Text.Length == 0 || textBox_pw.Text.Length == 0) {
                        MessageBox.Show(dialog_addAccount, "아이디 또는 비밀번호를 입력 해주세요.");
                        return;
                    }

                    if (textBox_id.Text.Contains(' ')) {
                        MessageBox.Show(dialog_addAccount, "아이디에 공백이 포함되어 있습니다.\n다시 입력 해주세요.");
                        textBox_id.Text = "";
                        return;
                    }

                    if (textBox_pw.Text.Contains(' ')) {
                        MessageBox.Show(dialog_addAccount, "패스워드에 공백이 포함되어 있습니다.\n다시 입력 해주세요.");
                        textBox_pw.Text = "";
                        return;
                    }

                    if (ConfigManager.getConfig("account_list").Split(';').Contains(textBox_id.Text)) {
                        MessageBox.Show(dialog_addAccount, "이미 동일한 계정이 존재합니다.");
                        return;
                    }

                    if (ConfigManager.getKeyByValue(textBox_nickname.Text) != "") {
                        MessageBox.Show(dialog_addAccount, "이미 동일한 별명이 존재합니다.");
                        return;
                    }

                    dialog_addAccount.DialogResult = DialogResult.OK;
                };

                //계정 추가 버튼 클릭 시
                if (dialog_addAccount.ShowDialog() == DialogResult.OK) {
                    string id = textBox_id.Text;
                    string pw = EncryptionSupporter.Protect(textBox_pw.Text);
                    string nickname = textBox_nickname.Text;
                    //Trace.WriteLine("ShowDialog ID : " + id);
                    //Trace.WriteLine("ShowDialog PW : " + pw);

                    ConfigManager.addConfig(id, pw);
                    ConfigManager.addConfig(id + "_nickname", nickname);
                    ConfigManager.setConfig("account_list", ConfigManager.getConfig("account_list") + textBox_id.Text + ";");

                    LoadListBox();

                    //SNS 로그인을 선택한 프리셋의 index를 1씩 증가 (계정이 추가되어서 뒤로 밀리니까)
                    //0번은 선택 없음, count - 2, 3, 4은 SNS 로그인 자리 (이미 하나가 추가되었으니까 -2 부터)
                    int count = materialListBox1.Count;
                    int[] preset1 = Array.ConvertAll(ConfigManager.getConfig("current_comboBox_index_preset_1").Split(';'), int.Parse);
                    int[] preset2 = Array.ConvertAll(ConfigManager.getConfig("current_comboBox_index_preset_2").Split(';'), int.Parse);
                    int[] preset3 = Array.ConvertAll(ConfigManager.getConfig("current_comboBox_index_preset_3").Split(';'), int.Parse);
                    int[] preset4 = Array.ConvertAll(ConfigManager.getConfig("current_comboBox_index_preset_4").Split(';'), int.Parse);
                    int[][] preset_list = { preset1, preset2, preset3, preset4 };
                    foreach (int[] preset in preset_list) {
                        for(int i = 0; i < preset.Length; i++) {
                            if (preset[i] >= count - 4) preset[i]++;
                        }
                    }
                    ConfigManager.setConfig("current_comboBox_index_preset_1", String.Join(';', preset1));
                    ConfigManager.setConfig("current_comboBox_index_preset_2", String.Join(';', preset2));
                    ConfigManager.setConfig("current_comboBox_index_preset_3", String.Join(';', preset3));
                    ConfigManager.setConfig("current_comboBox_index_preset_4", String.Join(';', preset4));
                }
            } else {
                string original_id = materialListBox1.SelectedItem.Text;
                string original_nickname;
                if (original_id.Contains(" (") && original_id.Contains(")")) {
                    original_id = original_id.Substring(0, original_id.IndexOf(" "));
                    original_nickname = ConfigManager.getConfig(original_id + "_nickname");
                } else {
                    original_nickname = original_id;
                }

                dialog_addAccount.Load += (sender, e) => {
                    if (original_id == original_nickname) { checkBox_useNickname.Checked = false; }
                    else { checkBox_useNickname.Checked = true; }
                    textBox_id.Text = original_id;
                    textBox_pw.Text = EncryptionSupporter.Unprotect(ConfigManager.getConfig(original_id));
                    textBox_nickname.Text = original_nickname;
                };

                button_confirm.Click += (sender, e) => {
                    if (textBox_id.Text.Length == 0 || textBox_pw.Text.Length == 0) {
                        MessageBox.Show(dialog_addAccount, "아이디 또는 비밀번호를 입력 해주세요.");
                        return;
                    }

                    if (textBox_id.Text.Contains(' ')) {
                        MessageBox.Show(dialog_addAccount, "아이디에 공백이 포함되어 있습니다.\n다시 입력 해주세요.");
                        textBox_id.Text = "";
                        return;
                    }

                    if (textBox_pw.Text.Contains(' ')) {
                        MessageBox.Show(dialog_addAccount, "패스워드에 공백이 포함되어 있습니다.\n다시 입력 해주세요.");
                        textBox_pw.Text = "";
                        return;
                    }

                    if (original_id != textBox_id.Text) {
                        if (ConfigManager.getConfig("account_list").Split(';').Contains(textBox_id.Text)) {
                            MessageBox.Show(dialog_addAccount, "이미 동일한 계정이 존재합니다.");
                            return;
                        }
                    }
                    
                    if (original_nickname != textBox_nickname.Text) {
                        if (ConfigManager.getKeyByValue(textBox_nickname.Text) != "") {
                            MessageBox.Show(dialog_addAccount, "이미 동일한 별명이 존재합니다.");
                            return;
                        }
                    } 

                    dialog_addAccount.DialogResult = DialogResult.OK;
                };

                if (dialog_addAccount.ShowDialog() == DialogResult.OK) {
                    string id = textBox_id.Text;
                    string pw = EncryptionSupporter.Protect(textBox_pw.Text);
                    string nickname = textBox_nickname.Text;
                    //Trace.WriteLine("ShowDialog ID : " + id);
                    //Trace.WriteLine("ShowDialog PW : " + pw);

                    ConfigManager.removeConfig(original_id); //기존 아이디,비번 삭제
                    ConfigManager.addConfig(id, pw); //새로운 아이디, 비번 등록

                    ConfigManager.removeConfig(original_id + "_nickname"); //기존 닉네임 삭제
                    ConfigManager.addConfig(id + "_nickname", nickname); //새로운 닉네임 등록

                    string list = ConfigManager.getConfig("account_list");
                    list = list.Replace(original_id, id);
                    ConfigManager.setConfig("account_list", list);

                    LoadListBox();
                }
            }

            backgroundForm.Dispose();
        }

        private void materialButton_removeAccount_Click(object sender, EventArgs e) {
            DialogResult dr = MessageBox.Show(this, "정말로 삭제하시겠습니까?", "계정 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr == DialogResult.No) { return; }
            if (materialListBox1.SelectedIndex == -1) { return; }
            int index = materialListBox1.SelectedIndex;
            byte current_preset = Byte.Parse(ConfigManager.getConfig("current_preset"));
            int[] temp = Array.ConvertAll(ConfigManager.getConfig("current_comboBox_index_preset_" + current_preset).Split(';'), s => int.Parse(s));
            StringBuilder sb = new StringBuilder();
            foreach (var item in temp) {
                if (item > index) { sb.Append((item - 1).ToString() + ';'); } else { sb.Append(item.ToString() + ';'); }
            }
            sb.Remove(sb.Length - 1, 1);
            ConfigManager.setConfig("current_comboBox_index_preset_" + current_preset, sb.ToString());
            string id = materialListBox1.SelectedItem.Text;
            if (id.Contains(" (") && id.Contains(")")) {
                id = id.Substring(0, id.IndexOf(" "));
            }
            ConfigManager.removeConfig(id);
            ConfigManager.removeConfig(id + "_nickname");
            string account_list = ConfigManager.getConfig("account_list");
            account_list = account_list.Remove(account_list.IndexOf(id), id.Length + 1);
            ConfigManager.setConfig("account_list", account_list);
            materialListBox1.RemoveItemAt(index);

            LoadListBox();
        }

        private void LoadListBox() {
            materialListBox1.Clear();
            string[] accountList = ConfigManager.getConfig("account_list").Split(';');
            foreach (var item in accountList) {
                if (item == "") continue;

                string nickname = ConfigManager.getConfig(item + "_nickname");
                if (nickname == "" || nickname == item) { materialListBox1.AddItem(item); } 
                else { materialListBox1.AddItem(item + " (" + nickname + ")"); }
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            Process.Start(new ProcessStartInfo("https://blog.naver.com/kog5071/222650978419") { UseShellExecute = true });
        }
    }
}
