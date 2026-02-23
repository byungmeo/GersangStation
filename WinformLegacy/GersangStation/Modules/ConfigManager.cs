using IWshRuntimeLibrary;
using System.Configuration;
using System.Text;

namespace GersangStation.Modules;
internal static class ConfigManager {
    public static Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

    public static void Validation() {
         Dictionary<string, string> keyValuePairs = new Dictionary<string, string> {
            { "current_preset", "1" },
            { "account_list", "" },
            { "current_comboBox_index_preset_1", "0;0;0" },
            { "current_comboBox_index_preset_2", "0;0;0" },
            { "current_comboBox_index_preset_3", "0;0;0" },
            { "current_comboBox_index_preset_4", "0;0;0" },
            { "current_server", "0" },
            { "client_path_1", "" },
            { "client_path_2", "" },
            { "client_path_3", "" },
            { "client_path_test_1", "" },
            { "client_path_test_2", "" },
            { "client_path_test_3", "" },
            { "client_path_rnd_1", "" },
            { "client_path_rnd_2", "" },
            { "client_path_rnd_3", "" },
            { "shortcut_name", "거상공홈;미설정;미설정;미설정;" },
            { "shortcut_1", "https://www.gersang.co.kr/main/index.gs" },
            { "shortcut_2", "" },
            { "shortcut_3", "" },
            { "shortcut_4", "" },
            { "directory_name_client_2", "Gersang2" },
            { "directory_name_client_3", "Gersang3" },
            { "directory_name_client_test_2", "GerTest2" },
            { "directory_name_client_test_3", "GerTest3" },
            { "directory_name_client_rnd_2", "CheonRa2" },
            { "directory_name_client_rnd_3", "CheonRa3" },
            { "is_auto_update", "True" },
            { "prev_announcement", "" },
            { "use_clip_mouse", "False" },
            { "use_clip_disable_hotkey", "True" }, // Alt 키 고정
            { "use_clip_toggle_hotkey", "False" },
            { "clip_toggle_hotkey", "122" }, // Keys::F11
            { "use_clip_only_first", "False" },
            { "use_symbolic", "True" }
        };

        if(false == ExistsConfigFile()) {
            string msg = "바탕화면에 바로가기를 생성하시겠습니까?";
            DialogResult dr = MessageBox.Show(msg, "바로가기 생성", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if(dr == DialogResult.Yes) {
                try {
                    string path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    string shortcutFileName = path + "\\GersangStation.exe - 바로 가기.lnk";
                    FileInfo shortcutFile = new FileInfo(shortcutFileName);
                    // 이미 바탕화면에 바로가기가 있다면 삭제
                    if(shortcutFile.Exists) System.IO.File.Delete(shortcutFile.FullName);
                    // 바로가기 생성
                    WshShell wsh = new WshShell();
                    IWshShortcut Link = wsh.CreateShortcut(shortcutFile.FullName);
                    // 원본 파일의 경로 설정
                    Link.TargetPath = Application.ExecutablePath;
                    Link.Save();
                } catch(Exception ex) {
                    MessageBox.Show("바로가기 생성 도중 오류가 발생하였습니다.\n" + ex.Message, "오류 발생", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    CreateConfigFile(keyValuePairs);
                    return;
                }
            }

            msg = "이전 버전에서 입력한 계정정보를 불러오시겠습니까?\n자세한 방법은 설치 웹사이트를 참고 바랍니다.";
            dr = MessageBox.Show(msg, "이전 설정 불러오기", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if(dr == DialogResult.Yes) {
                OpenFileDialog openFileDialog = new OpenFileDialog() { Filter = "설정파일|*.config" };
                dr = openFileDialog.ShowDialog();
                if(dr == DialogResult.OK) {
                    //File경로와 File명을 모두 가지고 온다.
                    string fileFullPath = openFileDialog.FileName;
                    string destPath = Application.StartupPath + "\\GersangStation.dll.config";
                    System.IO.File.Copy(fileFullPath, destPath, true);
                    CheckKey(keyValuePairs);
                    return;
                }
            }
            CreateConfigFile(keyValuePairs);
        }
        else CheckKey(keyValuePairs);
    }

    private static void CheckKey(Dictionary<string, string> keyValuePairs) {
        foreach(var item in keyValuePairs) {
            KeyValueConfigurationElement element = configuration.AppSettings.Settings[item.Key];
            if(element == null)
                AddConfig(item.Key, item.Value);
            else if(item.Value != "" && element.Value == "")
                SetConfig(item.Key, item.Value);
        }
    }

    private static void CreateConfigFile(Dictionary<string, string> keyValuePairs) {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
        sb.AppendLine("<configuration>");
        sb.AppendLine("<appSettings>");

        foreach(var item in keyValuePairs) AddKey(sb, item.Key, item.Value);

        sb.AppendLine("</appSettings>");
        sb.AppendLine("</configuration>");

        System.IO.File.WriteAllText(Application.StartupPath + @"\GersangStation.dll.config", sb.ToString());
    }

    private static void AddKey(StringBuilder sb, string key, string value) {
        sb.AppendLine(@"<add key=""" + key + @""" value=""" + value + @""" />");
    }

    private static bool ExistsConfigFile() {
        return System.IO.File.Exists(Application.StartupPath + @"\GersangStation.dll.config");
    }

    public static string GetConfig(string key) {
        string? value = ConfigurationManager.AppSettings[key];
        if(value == null) { return ""; }
        return value;
    }

    public static void SetConfig(string key, string value) {
        configuration.AppSettings.Settings[key].Value = value;
        SaveConfig();
    }

    public static void AddConfig(string key, string value) {
        configuration.AppSettings.Settings.Remove(key);
        configuration.AppSettings.Settings.Add(key, value);
        SaveConfig();
    }

    public static void RemoveConfig(string key) {
        configuration.AppSettings.Settings.Remove(key);
        SaveConfig();
    }

    private static void SaveConfig() {
        configuration.Save(ConfigurationSaveMode.Full, true);
        ConfigurationManager.RefreshSection("appSettings");
    }

    public static string GetKeyByValue(string value) {
        //중복되지 않는 Value를 사용하는 Key여야만 원하는 결과를 얻을 수 있습니다.
        foreach(KeyValueConfigurationElement item in configuration.AppSettings.Settings) {
            if(item.Value == value) {
                return item.Key;
            }
        }
        return "";
    }
}
