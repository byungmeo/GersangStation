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
            ProgressLabel.Text = "";
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
                checkThread = new Thread(() => { result = checker.Run(out reportFileName); });
                checkThread.Start();
            }
            catch (Exception except)
            {
                MessageBox.Show(except.Message, "유효성 검사에 실패하였습니다", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void IntegrityCheckerEventHandler(object sender, ProgressChangedEventArgs e)
        {
            ProgressLabel.Invoke((Action)(() => ProgressLabel.Text = e.UserState.ToString()));
            progressBar.Invoke((Action)(() => progressBar.Value = e.ProgressPercentage));
            if (e.ProgressPercentage == 100)
            {
                Thread.Sleep(100);
                //Enable all controls
                materialButton1.Invoke((Action)(() => materialButton1.Enabled = true));
                materialButton2.Invoke((Action)(() => materialButton2.Enabled = true));
                materialButton2.Invoke((Action)(() => materialButton2.Text = "실행"));
                
                MessageBox.Show($"유효성 검사를 완료하였습니다.\r\n {result.Count}개의 파일이 다릅니다");

                if (Directory.Exists(Directory.GetCurrentDirectory() + @"\Temp"))
                {
                    Directory.Delete(Directory.GetCurrentDirectory() + @"\Temp", true);
                }
            }
        }

        private void materialLabel1_Click(object sender, EventArgs e)
        {

        }
    }
}
