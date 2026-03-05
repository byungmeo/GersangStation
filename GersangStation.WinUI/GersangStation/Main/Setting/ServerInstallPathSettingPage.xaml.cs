using Core;
using Core.Models;
using GersangStation.Controls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.Storage.Pickers;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace GersangStation.Main.Setting
{
    public sealed partial class ServerInstallPathSettingPage : Page, INotifyPropertyChanged, IConfirmLeave
    {
        private GameServer currentGameServer = GameServer.Korea_Live;

        private bool _isDirty = false;
        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (_isDirty != value)
                {
                    _isDirty = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowSymbolInfoBar => CanUseSymbol;
        public bool ShowSymbolErrorBar => !CanUseSymbol;
        private bool _canUseSymbol = false;
        public bool CanUseSymbol
        {
            get => _canUseSymbol;
            private set
            {
                if (_canUseSymbol != value)
                {
                    _canUseSymbol = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowSymbolInfoBar));
                    OnPropertyChanged(nameof(ShowSymbolErrorBar));
                }
            }
        }

        private ClientSettings _clientSettings = new();
        public ClientSettings ClientSettings
        {
            get => _clientSettings;
            private set
            {
                if (ReferenceEquals(_clientSettings, value))
                    return;

                if (_clientSettings is INotifyPropertyChanged oldNpc)
                    oldNpc.PropertyChanged -= ClientSettings_PropertyChanged;

                _clientSettings = value;

                if (_clientSettings is INotifyPropertyChanged newNpc)
                    newNpc.PropertyChanged += ClientSettings_PropertyChanged;

                OnPropertyChanged(nameof(ClientSettings));
                OnPropertyChanged(nameof(TextBox_Path2_IsEnabled));
                OnPropertyChanged(nameof(TextBox_Path3_IsEnabled));
            }
        }

        public bool TextBox_Path2_IsEnabled => ClientSettings.UseMultiClient && ClientSettings.UseClient2;
        public bool TextBox_Path3_IsEnabled => ClientSettings.UseMultiClient && ClientSettings.UseClient3;

        public ServerInstallPathSettingPage()
        {
            InitializeComponent();

            if (ClientSettings is INotifyPropertyChanged npc)
                npc.PropertyChanged += ClientSettings_PropertyChanged;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            currentGameServer = (GameServer)e.Parameter;
            ClientSettings = AppDataManager.LoadServerClientSettings(currentGameServer);
            CanUseSymbol = GameClientHelper.CanUseSymbol(ClientSettings.InstallPath, out _);
            TextBox_Path1.PlaceholderText = $"예시) {GameServerHelper.GetInstallPathPlaceholder(currentGameServer)}";
        }

        private async Task<bool> ShowSaveDialog()
        {
            bool CanLeaveThisPage = true;

            ContentDialog saveDialog = new()
            {
                XamlRoot = XamlRoot,
                Title = "저장",
                Content = "변경 내용을 저장하시겠습니까?",
                IsPrimaryButtonEnabled = true,
                PrimaryButtonText = "저장",
                IsSecondaryButtonEnabled = true,
                SecondaryButtonText = "저장 안 함",
                CloseButtonText = "취소",
                DefaultButton = ContentDialogButton.Primary
            };
            ContentDialogResult result = await saveDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                AppDataManager.SaveServerClientSettings(currentGameServer, ClientSettings);
                IsDirty = false;
            }
            else if (result == ContentDialogResult.Secondary)
            {
                IsDirty = false;
            }
            else
            {
                CanLeaveThisPage = false;
            }

            return CanLeaveThisPage;
        }

        protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);

            e.Cancel = !await ConfirmLeaveAsync();
        }

        public async Task<bool> ConfirmLeaveAsync()
        {
            if (IsDirty)
                return await ShowSaveDialog();

            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
           => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void ClientSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            IsDirty = true;

            if (e.PropertyName == nameof(ClientSettings.InstallPath))
            {
                CanUseSymbol = GameClientHelper.CanUseSymbol(ClientSettings.InstallPath, out _);
            }

            if (e.PropertyName == nameof(ClientSettings.UseMultiClient) ||
                e.PropertyName == nameof(ClientSettings.UseClient2))
            {
                OnPropertyChanged(nameof(TextBox_Path2_IsEnabled));
            }

            if (e.PropertyName == nameof(ClientSettings.UseMultiClient) ||
                e.PropertyName == nameof(ClientSettings.UseClient3))
            {
                OnPropertyChanged(nameof(TextBox_Path3_IsEnabled));
            }
        }

        private void ToggleSwitch_UseMultiClient_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var toggleSwitch = (ToggleSwitch)sender;
            Expander_MultiClient.IsExpanded = toggleSwitch.IsOn;
        }

        private void Button_Save_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            AppDataManager.SaveServerClientSettings(currentGameServer, ClientSettings);
            IsDirty = false;
        }

        private void Button_CreateMultiClient_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (!ClientSettings.UseSymbol)
                return;

            bool success = GameClientHelper.CreateSymbolMultiClient(new CreateSymbolMultiClientArgs
            {
                InstallPath = TextBox_Path1.Text,
                DestPath2 = ClientSettings.UseClient2 ? ClientSettings.Client2Path : string.Empty,
                DestPath3 = ClientSettings.UseClient3 ? ClientSettings.Client3Path : string.Empty,
                OverwriteConfig = false
            }, out string reason);

            if (success) 
            {
                TeachingTip_General.Title = "다클라 생성 성공";
                TeachingTip_General.Subtitle = "";

            }
            else
            {
                TeachingTip_General.Title = "다클라 생성 실패";
                TeachingTip_General.Subtitle = reason;
            }
            TeachingTip_General.IsOpen = true;
        }

        private async void Button_SymbolErrorAction_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var uri = new Uri("https://github.com/byungmeo/GersangStation/discussions/39");
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        private void TextBox_InstallPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (ValidatedTextBox)sender;
            bool isValid = GameClientHelper.IsValidInstallPath(currentGameServer, textBox.Text, out string reason);
            textBox.IsValid = isValid;
            textBox.ErrorText = reason;
            OnPropertyChanged(nameof(textBox.IsValid));
            if (isValid)
            {
                CheckBox_UseClient2.Content = $"2클라 사용 {textBox.Text}2";
                CheckBox_UseClient3.Content = $"3클라 사용 {textBox.Text}3";
            } 
            else
            {
                CheckBox_UseClient2.Content = $"2클라 사용";
                CheckBox_UseClient3.Content = $"3클라 사용";
            }
        }

        private async void Button_Path_PickFolder_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                // disable the button to avoid double-clicking
                button.IsEnabled = false;

                // Clear previous returned folder name
                // PickedFolderTextBlock.Text = "";

                var picker = new FolderPicker(button.XamlRoot.ContentIslandEnvironment.AppWindowId)
                {
                    CommitButtonText = "폴더 선택",
                    SuggestedStartLocation = PickerLocationId.ComputerFolder,
                    ViewMode = PickerViewMode.List
                };

                // Show the picker dialog window
                var folder = await picker.PickSingleFolderAsync();
                if (folder is not null)
                {
                    TextBox_Path1.Text = folder.Path;
                }

                // re-enable the button
                button.IsEnabled = true;
            }
        }
    }
}
