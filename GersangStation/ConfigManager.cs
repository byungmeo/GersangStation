using System.Configuration;
using System.Text;

namespace GersangStation {
    internal static class ConfigManager {
        public static Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

        public static void Validation() {
            Logger.Log("Log : (" + "ConfigManager" + ") " + "vsn.dat 파일 유효성 검사");
            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>();
            keyValuePairs.Add("current_preset", "1");
            keyValuePairs.Add("account_list", "");
            keyValuePairs.Add("current_comboBox_index_preset_1", "0;0;0");
            keyValuePairs.Add("current_comboBox_index_preset_2", "0;0;0");
            keyValuePairs.Add("current_comboBox_index_preset_3", "0;0;0");
            keyValuePairs.Add("current_comboBox_index_preset_4", "0;0;0");
            keyValuePairs.Add("is_test_server", "False");
            keyValuePairs.Add("client_path_1", "");
            keyValuePairs.Add("client_path_2", "");
            keyValuePairs.Add("client_path_3", "");
            keyValuePairs.Add("client_path_test_1", "");
            keyValuePairs.Add("client_path_test_2", "");
            keyValuePairs.Add("client_path_test_3", "");
            keyValuePairs.Add("shortcut_name", "거상홈페이지;홈페이지2;홈페이지3;홈페이지4;");
            keyValuePairs.Add("shortcut_1", "https://www.gersang.co.kr/main/index.gs");
            keyValuePairs.Add("shortcut_2", "");
            keyValuePairs.Add("shortcut_3", "");
            keyValuePairs.Add("shortcut_4", "");
            keyValuePairs.Add("directory_name_client_2", "Gersang2");
            keyValuePairs.Add("directory_name_client_3", "Gersang3");
            keyValuePairs.Add("directory_name_client_test_2", "GerTest2");
            keyValuePairs.Add("directory_name_client_test_3", "GerTest3");
            keyValuePairs.Add("is_auto_update", "True");
            keyValuePairs.Add("use_bat_creator", "False");

            if (false == ExistsConfig()) {  CreateConfigFile(keyValuePairs);  }
            else { CheckKey(keyValuePairs); }
        }

        private static void CheckKey(Dictionary<string, string> keyValuePairs) {
            Logger.Log("Log : (" + "ConfigManager" + ") " + "vsn.dat파일이 존재하며, 필수 Key값이 모두 존재하는지 검사");
            foreach (var item in keyValuePairs) {
                KeyValueConfigurationElement element = configuration.AppSettings.Settings[item.Key];
                if (element == null) { addConfig(item.Key, item.Value); }
            }
        }

        private static void CreateConfigFile(Dictionary<string,string> keyValuePairs) {
            Logger.Log("Log : (" + "ConfigManager" + ") " + "vsn.dat파일이 존재하지 않아 새로 생성");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
            sb.AppendLine("<configuration>");
            sb.AppendLine("<appSettings>");

            foreach (var item in keyValuePairs) { AddKey(sb, item.Key, item.Value); }

            sb.AppendLine("</appSettings>");
            sb.AppendLine("</configuration>");

            File.WriteAllText(@".\GersangStation.dll.config",  sb.ToString());
        }

        private static void AddKey(StringBuilder sb, string key, string value) {
            sb.AppendLine(@"<add key=""" + key + @""" value=""" + value + @""" />");
        }

        private static bool ExistsConfig() {
            return File.Exists(@".\GersangStation.dll.config");
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

        public static string getKeyByValue(string value) {
            //중복되지 않는 Value를 사용하는 Key여야만 원하는 결과를 얻을 수 있습니다.
            foreach (KeyValueConfigurationElement item in configuration.AppSettings.Settings) {
                if (item.Value == value) {
                    return item.Key;
                }
            }
            return "";
        }
    }
}
