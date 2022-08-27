using MaterialSkin.Controls;

namespace GersangStation {
    public partial class Form_AdvancedSetting : MaterialForm {
        public Form_AdvancedSetting() {
            InitializeComponent();
        }

        private void Form_AdvancedSetting_Load(object sender, EventArgs e) {
            materialCheckbox_useBAT.Checked = bool.Parse(ConfigManager.getConfig("use_bat_creator"));
        }

        private void materialButton_save_Click(object sender, EventArgs e) {
            ConfigManager.setConfig("use_bat_creator", materialCheckbox_useBAT.Checked.ToString());

            this.DialogResult = DialogResult.OK;
        }

        private void materialCheckbox_useBAT_CheckedChanged(object sender, EventArgs e) {
            ConfigManager.setConfig("use_bat_creator", materialCheckbox_useBAT.Checked.ToString());
        }
    }
}
