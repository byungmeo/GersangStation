using GersangStation.Modules;
using MaterialSkin.Controls;

namespace GersangStation
{
    public partial class Form_AdvancedSetting : MaterialForm
    {
        public Form_AdvancedSetting()
        {
            InitializeComponent();
        }

        private void Form_AdvancedSetting_Load(object sender, EventArgs e)
        {
            materialCheckbox_useBAT.Checked = bool.Parse(ConfigManager.getConfig("use_bat_creator"));
            mouseClipCheckBox.Checked = bool.Parse(ConfigManager.getConfig("use_clip_mouse"));
        }

        private void materialButton_save_Click(object sender, EventArgs e)
        {
            ConfigManager.setConfig("use_bat_creator", materialCheckbox_useBAT.Checked.ToString());

            this.DialogResult = DialogResult.OK;
        }

        private void materialCheckbox_useBAT_CheckedChanged(object sender, EventArgs e)
        {
            ConfigManager.setConfig("use_bat_creator", materialCheckbox_useBAT.Checked.ToString());
        }

        private void runMouseClipThread()
        {
            ClipMouse.Run();
        }

        private void stopMouseClipThread()
        {
            ClipMouse.Stop();
        }

        private void mouseClipCheckBox_CheckedChanged_1(object sender, EventArgs e)
        {
            ConfigManager.setConfig("use_clip_mouse", mouseClipCheckBox.Checked.ToString());

            if (mouseClipCheckBox.Checked == true)
            {
                runMouseClipThread();
            }
            else
            {
                stopMouseClipThread();
            }
        }

        public void updateClipCheckBox()
        {
            //It will raise duplicated update but it will be ignored
            mouseClipCheckBox.Checked = bool.Parse(ConfigManager.getConfig("use_clip_mouse"));
        }

    }
}
