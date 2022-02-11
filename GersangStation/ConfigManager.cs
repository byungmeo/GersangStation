using System.Configuration;

namespace GersangStation {
    internal static class ConfigManager {
        public static Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

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
