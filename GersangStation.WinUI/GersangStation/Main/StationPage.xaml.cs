using Core;
using Core.Models;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace GersangStation.Main
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class StationPage : Page, INotifyPropertyChanged
    {
        private bool _isInitializing = true;

        public IReadOnlyList<Account> Accounts { get; private set; } = [];

        // SelectedServer Property
        private int _selectedServerIndex;
        public int SelectedServerIndex
        {
            get => _selectedServerIndex;
            set
            {
                if (_selectedServerIndex != value)
                {
                    _selectedServerIndex = value;
                    OnPropertyChanged(nameof(SelectedServerIndex)); // UI 업데이트 알림

                    if (!_isInitializing)
                        AppDataManager.SelectedServer = (GameServer)value;
                }
            }
        }

        // SelectedPreset Property
        private int _selectedPreset;
        public int SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                int normalizedValue = NormalizePresetIndex(value);
                if (_selectedPreset != normalizedValue)
                {
                    _selectedPreset = normalizedValue;

                    if (!_isInitializing)
                        AppDataManager.SelectedPreset = normalizedValue;

                    OnPropertyChanged(nameof(SelectedPreset));
                    OnPropertyChanged(nameof(SelectedAccount1Id));
                    OnPropertyChanged(nameof(SelectedAccount2Id));
                    OnPropertyChanged(nameof(SelectedAccount3Id));
                }
            }
        }

        // Key: "AccountPreset1" (프리셋 이름)
        // Value: ["id1", "id2", "id3"] (계정 ID 리스트)
        private PresetList _presetList = new();
        public PresetList PresetList
        {
            get => _presetList;
            set
            {
                _presetList = value;

                if (!_isInitializing)
                    AppDataManager.SavePresetList(PresetList);
            }
        }

        public string SelectedAccount1Id { get => GetId(0); set => SetId(0, value); }
        public string SelectedAccount2Id { get => GetId(1); set => SetId(1, value); }
        public string SelectedAccount3Id { get => GetId(2); set => SetId(2, value); }

        private string GetId(int comboBoxIndex)
        {
            int presetIndex = NormalizePresetIndex(_selectedPreset);
            return _presetList.Presets[presetIndex].Items[comboBoxIndex].Id;
        }

        private void SetId(int comboBoxIndex, string selectedValue)
        {
            selectedValue ??= string.Empty;

            int presetIndex = NormalizePresetIndex(_selectedPreset);
            if (_presetList.Presets[presetIndex].Items[comboBoxIndex].Id == selectedValue)
                return;

            _presetList.Presets[presetIndex].Items[comboBoxIndex].Id = selectedValue;
            if (!_isInitializing)
                AppDataManager.SavePresetList(_presetList);

            switch (comboBoxIndex)
            {
                case 0:
                    OnPropertyChanged(nameof(SelectedAccount1Id));
                    break;
                case 1:
                    OnPropertyChanged(nameof(SelectedAccount2Id));
                    break;
                case 2:
                    OnPropertyChanged(nameof(SelectedAccount3Id));
                    break;
            }
        }

        private int NormalizePresetIndex(int value)
        {
            if (_presetList?.Presets is null || _presetList.Presets.Length == 0)
                return 0;

            if (value < 0)
                return 0;

            if (value >= _presetList.Presets.Length)
                return _presetList.Presets.Length - 1;

            return value;
        }

        private void InitializeState()
        {
            // x:Bind가 동작하기 전에 바인딩 원본 상태를 먼저 확정합니다.
            Accounts = AppDataManager.LoadAccounts();
            _presetList = AppDataManager.LoadPresetList();

            _selectedServerIndex = (int)AppDataManager.SelectedServer;
            _selectedPreset = NormalizePresetIndex(AppDataManager.SelectedPreset);

            if (_selectedPreset != AppDataManager.SelectedPreset)
                AppDataManager.SelectedPreset = _selectedPreset;
        }

        public StationPage()
        {
            InitializeState();
            InitializeComponent();
            _isInitializing = false;
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
