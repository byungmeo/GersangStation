using GersangStation.Modules;
using MaterialSkin.Controls;

namespace GersangStation
{
    public partial class Form_AdvancedSetting : MaterialForm {
        public Form_AdvancedSetting() {
            InitializeComponent();

            toolTip1.SetToolTip(img_help, 
                "창모드 환경에서 마우스 가두기를 하였음에도 마우스 커서가 밖으로 삐져나오는 현상을 개선합니다." +
                "\n\n※ 게임 내 마우스 가두기 기능을 OFF 하시고 사용하셔야 합니다." +
                "\n\nF11: 마우스 가두기 ON, OFF\nAlt: 일시적으로 OFF");
        }

        private void Form_AdvancedSetting_Load(object sender, EventArgs e) {
            materialCheckbox_useBAT.Checked = bool.Parse(ConfigManager.getConfig("use_bat_creator"));
            materialCheckbox_mouseClip.Checked = bool.Parse(ConfigManager.getConfig("use_clip_mouse"));
        }

        private void materialButton_save_Click(object sender, EventArgs e) {
            ConfigManager.setConfig("use_bat_creator", materialCheckbox_useBAT.Checked.ToString());

            this.DialogResult = DialogResult.OK;
        }

        private void materialCheckbox_useBAT_CheckedChanged(object sender, EventArgs e) {
            ConfigManager.setConfig("use_bat_creator", materialCheckbox_useBAT.Checked.ToString());
        }

        private void runMouseClipThread() {
            ClipMouse.Run();
        }

        private void stopMouseClipThread() {
            ClipMouse.Stop();
        }

        private void mouseClipCheckBox_CheckedChanged_1(object sender, EventArgs e) {
            ConfigManager.setConfig("use_clip_mouse", materialCheckbox_mouseClip.Checked.ToString());

            if(materialCheckbox_mouseClip.Checked == true) {
                runMouseClipThread();
            } else {
                stopMouseClipThread();
            }
        }

        public void updateClipCheckBox() {
            //It will raise duplicated update but it will be ignored
            materialCheckbox_mouseClip.Checked = bool.Parse(ConfigManager.getConfig("use_clip_mouse"));
        }
    }
}
