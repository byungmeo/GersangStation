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
            Logger.Log("Click : (" + this.Name + ") " + materialButton_save.Name);
            ConfigManager.setConfig("use_bat_creator", materialCheckbox_useBAT.Checked.ToString());
        }

        private void materialCheckbox_useBAT_CheckedChanged(object sender, EventArgs e) {
            Logger.Log("CheckedChanged : (" + this.Name + ") " + materialButton_save.Name);
            ConfigManager.setConfig("use_bat_creator", materialCheckbox_useBAT.Checked.ToString());
        }
    }
}
