using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using GersangStation.Modules;
using MaterialSkin.Controls;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace GersangStation
{
    public partial class Form_IntegrityCheck : MaterialForm
    {
        Thread? checkThread = null;
        Dictionary<string, string>? result = null;

        public Form_IntegrityCheck()
        {
            InitializeComponent();
            this.FormClosing += Form_IntegrityCheck_FormClosing;
            this.FormClosed += Form_IntegrityCheck_FormClosed;
            materialExpansionPanel1.Hide();
        }

        private void Form_IntegrityCheck_FormClosed(object? sender, FormClosedEventArgs e)
        {
            if (sender != this) return;
            if (checkThread != null)
            {
            }
        }

        private void Form_IntegrityCheck_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (sender != this || checkThread == null) return;
            if (checkThread.IsAlive == false) return;
            DialogResult dr = MessageBox.Show("유효성 검사 중에는 중단할 수 없습니다.", "", MessageBoxButtons.OK);
            e.Cancel = true;
        }


        private void Form_IntegrityCheck_Load(object sender, EventArgs e)
        {
            ClientPathTextBox.Text = ConfigManager.getConfig("client_path_1");
        }

        private void materialButton1_Click(object sender, EventArgs e)
        {
            MaterialButton button = (MaterialButton)sender;
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.ShowDialog();
            if (dlg.SelectedPath.Length == 0) { return; }

            Action<MaterialTextBox2> selectDir = (textBox) =>
            {
                textBox.Text = dlg.SelectedPath;
            };
            selectDir(ClientPathTextBox);
        }

        private void materialButton2_Click(object sender, EventArgs e)
        {
            //Check path
            if (!Directory.Exists(ClientPathTextBox.Text))
            {
                MessageBox.Show("해당 폴더가 존재하지 않습니다.", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            //Disable all controls
            materialButton1.Enabled = false;
            materialButton2.Enabled = false;
            materialButton2.Text = "실행중";

            string reportFileName = "";
            //Run checker
            try
            {
                if (Directory.Exists(Directory.GetCurrentDirectory() + @"\Temp"))
                {
                    Directory.Delete(Directory.GetCurrentDirectory() + @"\Temp", true);
                }

                IntegrityChecker? checker = IntegrityChecker.CreateIntegrityChecker(ClientPathTextBox.Text, Directory.GetCurrentDirectory() + @"\Temp");
                checker.ProgressChanged += IntegrityCheckerEventHandler;
                result = new();
                checkThread = new Thread(() => { checker.Run(out reportFileName, ref result); });
                checkThread.Start();

            }
            catch (Exception except)
            {
                MessageBox.Show(except.Message, "유효성 검사에 실패하였습니다", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void IntegrityCheckerEventHandler(object sender, ProgressChangedEventArgs e)
        {
            if (this.InvokeRequired)
            {
                materialButton2.Invoke((Action)(() => materialButton2.Text = e.UserState.ToString()));
                progressBar.Invoke((Action)(() => progressBar.Value = e.ProgressPercentage));
            }
            else
            {
                materialButton2.Text = e.UserState.ToString();
                progressBar.Value = e.ProgressPercentage;
            }

            if (e.ProgressPercentage == 100)
            {
                foreach (var item in result)
                {
                    checkedListBox1.Invoke((Action)((() => checkedListBox1.Items.Add($"{item.Key.ReplaceLineEndings().Replace(Environment.NewLine, "")}{item.Value}", true))));
                }

                if (this.InvokeRequired)
                {
                    if (result.Count > 0)
                    {
                        materialButton2.Invoke((Action)(() => materialButton2.Text = $"{result.Count}개의 파일 복원하기"));
                        materialButton2.Invoke((Action)(() => materialButton2.Enabled = true));
                    }
                    else {
                        materialButton2.Invoke((Action)(() => materialButton2.Text = $"모든 파일이 일치합니다"));
                        materialButton2.Invoke((Action)(() => materialButton2.Enabled = false));
                    }
                    materialExpansionPanel1.Invoke((Action)((() => materialExpansionPanel1.Show())));
                }
                else
                {
                    materialButton2.Text = "완료";
                    materialExpansionPanel1.Show();
                }

                if (Directory.Exists(Directory.GetCurrentDirectory() + @"\Temp"))
                {
                    Directory.Delete(Directory.GetCurrentDirectory() + @"\Temp", true);
                }
            }
        }

        private void materialLabel1_Click(object sender, EventArgs e)
        {

        }

        private void checkedListBox1_ItemCheck(object sender, ItemCheckEventArgs e)
        {            
            materialButton2.Text = $"{checkedListBox1.Items.Count - checkedListBox1.CheckedItems.Count}개의 파일 복원하기";
            if (checkedListBox1.Items.Count - checkedListBox1.CheckedItems.Count > 0)
                materialButton2.Enabled = true;
            else
                materialButton2.Enabled = false;
        }
    }
}
