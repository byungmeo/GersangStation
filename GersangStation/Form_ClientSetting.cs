using MaterialSkin.Controls;

namespace GersangStation {
    public partial class Form_ClientSetting : MaterialForm {
        public Form_ClientSetting() {
            InitializeComponent();
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
            ConfigManager.setConfig("client_path_1", textBox_path_1.Text);
            ConfigManager.setConfig("client_path_2", textBox_path_2.Text);
            ConfigManager.setConfig("client_path_3", textBox_path_3.Text);

            ConfigManager.setConfig("client_path_test_1", textBox_path_test_1.Text);
            ConfigManager.setConfig("client_path_test_2", textBox_path_test_2.Text);
            ConfigManager.setConfig("client_path_test_3", textBox_path_test_3.Text);

            this.DialogResult = DialogResult.OK;
        }

        private void materialButton_createClient_Click(object sender, EventArgs e) {
            MaterialButton button = (MaterialButton)sender;
            if (button.Equals(materialButton_createClient)) {
                //새로운 폼 띄우고 2클, 3클 폴더명 정하고 클라생성버튼
                //textBox_path_1
            } else if (button.Equals(materialButton_createClient_test)) {
                //새로운 폼 띄우고 2클, 3클 폴더명 정하고 클라생성버튼
                //textBox_path_test_1
            }
        }

        private void Form_ClientSetting_Load(object sender, EventArgs e) {
            textBox_path_1.Text = ConfigManager.getConfig("client_path_1");
            textBox_path_2.Text = ConfigManager.getConfig("client_path_2");
            textBox_path_3.Text = ConfigManager.getConfig("client_path_3");

            textBox_path_test_1.Text = ConfigManager.getConfig("client_path_test_1");
            textBox_path_test_2.Text = ConfigManager.getConfig("client_path_test_2");
            textBox_path_test_3.Text = ConfigManager.getConfig("client_path_test_3");
        }
    }
}
