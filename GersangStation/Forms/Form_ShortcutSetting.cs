using GersangStation.Modules;
using MaterialSkin.Controls;
using System.Text;

namespace GersangStation
{
    public partial class Form_ShortcutSetting : MaterialForm {
        public Form_ShortcutSetting() {
            InitializeComponent();
        }

        private void Form_ShortcutSetting_Load(object sender, EventArgs e) {
            string[] names = ConfigManager.getConfig("shortcut_name").Split(';');
            textBox_shortcutName_1.Text = names[0];
            textBox_shortcutName_2.Text = names[1];
            textBox_shortcutName_3.Text = names[2];
            textBox_shortcutName_4.Text = names[3];

            textBox_shortcutLink_1.Text = ConfigManager.getConfig("shortcut_1");
            textBox_shortcutLink_2.Text = ConfigManager.getConfig("shortcut_2");
            textBox_shortcutLink_3.Text = ConfigManager.getConfig("shortcut_3");
            textBox_shortcutLink_4.Text = ConfigManager.getConfig("shortcut_4");
        }

        private void materialButton_save_Click(object sender, EventArgs e) {
            StringBuilder sb = new StringBuilder();
            sb.Append(textBox_shortcutName_1.Text + ";");
            sb.Append(textBox_shortcutName_2.Text + ";");
            sb.Append(textBox_shortcutName_3.Text + ";");
            sb.Append(textBox_shortcutName_4.Text + ";");
            ConfigManager.setConfig("shortcut_name", sb.ToString());
            ConfigManager.setConfig("shortcut_1", textBox_shortcutLink_1.Text);
            ConfigManager.setConfig("shortcut_2", textBox_shortcutLink_2.Text);
            ConfigManager.setConfig("shortcut_3", textBox_shortcutLink_3.Text);
            ConfigManager.setConfig("shortcut_4", textBox_shortcutLink_4.Text);
            this.DialogResult = DialogResult.OK;
        }
    }
}
