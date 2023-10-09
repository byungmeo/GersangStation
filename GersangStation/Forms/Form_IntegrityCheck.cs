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
        public Form_IntegrityCheck()
        {
            InitializeComponent();
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
                checker.Run(out reportFileName);

                MessageBox.Show($"유효성 검사를 완료하였습니다. \n\r 거상스테이션 아래의 {reportFileName}를 확인해주세요.");

            }
            catch (Exception except)
            {
                MessageBox.Show(except.Message, "유효성 검사에 실패하였습니다", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (Directory.Exists(Directory.GetCurrentDirectory() + @"\Temp"))
                {
                    Directory.Delete(Directory.GetCurrentDirectory() + @"\Temp", true);
                }
            }

            //Enable all controls
            materialButton1.Enabled = true;
            materialButton2.Enabled = true;
            materialButton2.Text = "실행";
        }

        private void materialLabel1_Click(object sender, EventArgs e)
        {

        }
    }
}
