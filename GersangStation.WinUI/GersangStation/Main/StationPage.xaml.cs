using Core;
using Core.Models;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Windows.Storage;

namespace GersangStation.Main
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class StationPage : Page, INotifyPropertyChanged
    {
        private IReadOnlyList<Account> Accounts = AppDataManager.LoadAccounts();

        // SelectedServer Property
        private int _selectedServerIndex = (int)AppDataManager.SelectedServer;
        public int SelectedServerIndex
        {
            get => _selectedServerIndex;
            set
            {
                if (_selectedServerIndex != value)
                {
                    _selectedServerIndex = value;
                    OnPropertyChanged(); // UI 업데이트 알림
                    AppDataManager.SelectedServer = (GameServer)value;
                }
            }
        }

        // SelectedPreset Property
        private int _selectedPreset = AppDataManager.SelectedPreset;
        public int SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (_selectedPreset != value)
                {
                    _selectedPreset = value;
                    OnPropertyChanged(); // UI 업데이트 알림
                    AppDataManager.SelectedPreset = value;
                }
            }
        }

        // Key: "AccountPreset1" (프리셋 이름)
        // Value: ["id1", "id2", "id3"] (계정 ID 리스트)
        private Dictionary<string, List<string>> _presets = new();
        public void SavePresets()
        {
            // 데이터를 JSON 문자열로 변환 (직렬화)
            string json = JsonSerializer.Serialize(_presets);

            // LocalSettings에 저장
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["AccountPresets"] = json;
        }
        public void LoadPresets()
        {
            var settings = ApplicationData.Current.LocalSettings;
            string json = settings.Values["AccountPresets"] as string;

            if (!string.IsNullOrEmpty(json))
            {
                // JSON을 다시 딕셔너리로 복구 (역직렬화)
                _presets = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
            }
        }
        private void ApplyPreset(string presetName)
        {
            if (_presets.TryGetValue(presetName, out var ids) && ids.Count >= 3)
            {
                // 1, 2, 3클라 ID 속성에 순서대로 꽂아넣기
                // (앞서 만든 AccId1, AccId2, AccId3 속성이 있다면 UI도 자동으로 바뀝니다)
                SelectedAccount1Id = ids[0];
                SelectedAccount2Id = ids[1];
                SelectedAccount3Id = ids[2];

                // UI 강제 갱신
                this.Bindings.Update();
            }
        }

        public string SelectedAccount1Id { get => GetId(0); set => SetId(0, value); }
        public string SelectedAccount2Id { get => GetId(1); set => SetId(1, value); }
        public string SelectedAccount3Id { get => GetId(2); set => SetId(2, value); }

        private string GetId(int i) => "1";
        private void SetId(int i, string id) { }

        public StationPage()
        {
            InitializeComponent();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private async void Button_Client1_Execute_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            string Id = (string)ComboBox_Account1.SelectedValue;
            if (App.CurrentWindow is MainWindow window)
            {
                Debug.WriteLine("App.CurrentWindow is MainWindow window");
                if (window.WebViewManager is not null)
                {
                    Debug.WriteLine("window.WebViewManager is not null");
                    bool result = await window.WebViewManager.TryGameStart(Id, 0);
                }
            }
        }

        private async void Button_Client2_Execute_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            string Id = (string)ComboBox_Account2.SelectedValue;
            if (App.CurrentWindow is MainWindow window)
            {
                Debug.WriteLine("App.CurrentWindow is MainWindow window");
                if (window.WebViewManager is not null)
                {
                    Debug.WriteLine("window.WebViewManager is not null");
                    bool result = await window.WebViewManager.TryGameStart(Id, 1);
                }
            }
        }

        private async void Button_Client3_Execute_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            string Id = (string)ComboBox_Account3.SelectedValue;
            if (App.CurrentWindow is MainWindow window)
            {
                Debug.WriteLine("App.CurrentWindow is MainWindow window");
                if (window.WebViewManager is not null)
                {
                    Debug.WriteLine("window.WebViewManager is not null");
                    bool result = await window.WebViewManager.TryGameStart(Id, 2);
                }
            }
        }
    }
}
