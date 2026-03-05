using Core;
using Core.Models;
using GersangStation.Controls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.Storage.Pickers;
using System;
using System.ComponentModel;
using System.Diagnostics;
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
            CanUseSymbol = InstallPathHelper.CanUseSymbol(ClientSettings.InstallPath, out _);
            TextBox_Path1.PlaceholderText = $"예시) {GameServerHelper.GetInstallPathPlaceholder(currentGameServer)}";
            TextBox_Path2.PlaceholderText = $"예시) {GameServerHelper.GetInstallPathPlaceholder(currentGameServer)}2";
            TextBox_Path3.PlaceholderText = $"예시) {GameServerHelper.GetInstallPathPlaceholder(currentGameServer)}3";
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
                CanUseSymbol = InstallPathHelper.CanUseSymbol(ClientSettings.InstallPath, out _);
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
            // TODO: 다클라 생성 기능 연결
        }

        private async void Button_SymbolErrorAction_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var uri = new Uri("https://github.com/byungmeo/GersangStation/discussions/39");
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        private void TextBox_InstallPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (ValidatedTextBox)sender;

            bool isOrgPath = textBox == TextBox_Path1;
            bool useSymbol = CheckBox_UseSymbol.IsEnabled && (CheckBox_UseSymbol.IsChecked ?? false);
            bool isValid = InstallPathHelper.IsValidInstallPath(currentGameServer, textBox.Text, isOrgPath, useSymbol, out string reason);
            textBox.IsValid = isValid;
            textBox.ErrorText = reason;
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
                    if (button == Button_Path1_PickFolder)
                    {
                        TextBox_Path1.Text = folder.Path;
                    }
                    else if (button == Button_Path2_PickFolder)
                    {
                        TextBox_Path2.Text = folder.Path;
                    }
                    else
                    {
                        TextBox_Path3.Text = folder.Path;
                    }
                }

                // re-enable the button
                button.IsEnabled = true;
            }

        }

        private void Button_AutoFind_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            string teachingTipTitle;
            string teachingTipSubtitle;

            string? foundPath = RegistryHelper.GetInstallPathFromRegistry(currentGameServer);
            if (foundPath is null)
            {
                teachingTipTitle = "찾기 실패";
                teachingTipSubtitle = "거상을 플레이한 적이 없어 경로를 찾는데 실패했습니다.";
            }
            else
            {
                bool isValid = InstallPathHelper.IsValidInstallPath(currentGameServer, foundPath, true, ClientSettings.UseSymbol, out string reason);
                if (isValid)
                {
                    teachingTipTitle = "찾기 성공";
                    teachingTipSubtitle = "거상 플레이 이력을 바탕으로 경로를 찾는데 성공했습니다.";
                }
                else
                {
                    teachingTipTitle = "찾기 실패";
                    teachingTipSubtitle = $"경로를 찾았지만 유효한 메인 클라이언트 경로가 아닙니다.\n직접 폴더를 선택해주세요.\n{foundPath}";
                }
            }

            TeachingTip_Button_AutoFind.Title = teachingTipTitle;
            TeachingTip_Button_AutoFind.Subtitle = teachingTipSubtitle;
            TeachingTip_Button_AutoFind.IsOpen = true;
        }
    }
}
