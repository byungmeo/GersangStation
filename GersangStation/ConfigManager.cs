using System.Configuration;
using System.Reflection;
using System.Text;

namespace GersangStation {
    internal static class ConfigManager {
        public static Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

        public static void Validation() {
            if (false == ExistsConfig()) {
                CreateConfigFile();
            }
        }

        private static void CreateConfigFile() {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
            sb.AppendLine("<configuration>");
            sb.AppendLine("<appSettings>");

            AddKey(sb, "current_preset", "1");
            AddKey(sb, "account_list", "");
            AddKey(sb, "current_comboBox_index_preset_1", "0;0;0");
            AddKey(sb, "current_comboBox_index_preset_2", "0;0;0");
            AddKey(sb, "current_comboBox_index_preset_3", "0;0;0");
            AddKey(sb, "current_comboBox_index_preset_4", "0;0;0");
            AddKey(sb, "is_test_server", "False");
            AddKey(sb, "client_path_1", "");
            AddKey(sb, "client_path_2", "");
            AddKey(sb, "client_path_3", "");
            AddKey(sb, "client_path_test_1", "");
            AddKey(sb, "client_path_test_2", "");
            AddKey(sb, "client_path_test_3", "");
            AddKey(sb, "shortcut_name", "거상홈페이지;홈페이지2;홈페이지3;홈페이지4;");
            AddKey(sb, "shortcut_1", "https://www.gersang.co.kr/main/index.gs");
            AddKey(sb, "shortcut_2", "");
            AddKey(sb, "shortcut_3", "");
            AddKey(sb, "shortcut_4", "");
            AddKey(sb, "directory_name_client_2", "Gersang2");
            AddKey(sb, "directory_name_client_3", "Gersang3");
            AddKey(sb, "directory_name_client_test_2", "GerTest2");
            AddKey(sb, "directory_name_client_test_3", "GerTest3");
            AddKey(sb, "is_auto_update", "True");

            sb.AppendLine("</appSettings>");
            sb.AppendLine("</configuration>");

            File.WriteAllText(Assembly.GetEntryAssembly().Location + ".config",  sb.ToString());
        }

        private static void AddKey(StringBuilder sb, string key, string value) {
            sb.AppendLine(@"<add key=""" + key + @""" value=""" + value + @""" />");
        }

        private static bool ExistsConfig() {
            return File.Exists(Assembly.GetEntryAssembly().Location + ".config");
    }

        public static string getConfig(string key) {
            string? value = ConfigurationManager.AppSettings[key];
            if(value == null) { return ""; }
            return value;
        }

        public static void setConfig(string key, string value) {
            configuration.AppSettings.Settings[key].Value = value;
            saveConfig();
        }

        public static void addConfig(string key, string value) {
            configuration.AppSettings.Settings.Remove(key);
            configuration.AppSettings.Settings.Add(key, value);
            saveConfig();
        }

        public static void removeConfig(string key) {
            configuration.AppSettings.Settings.Remove(key);
            saveConfig();
        }

        private static void saveConfig() {
            configuration.Save(ConfigurationSaveMode.Full, true);
            ConfigurationManager.RefreshSection("appSettings");
        }
    }
}
