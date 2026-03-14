using Core;
using Core.Models;
using GersangStation.Controls;
using GersangStation.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.Storage.Pickers;
using System;
using System.ComponentModel;
using System.IO;
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
                OnPropertyChanged(nameof(InstallGuideVisibility));
            }
        }

        public bool TextBox_Path2_IsEnabled => ClientSettings.UseMultiClient && ClientSettings.UseClient2;
        public bool TextBox_Path3_IsEnabled => ClientSettings.UseMultiClient && ClientSettings.UseClient3;
        public Visibility InstallGuideVisibility => string.IsNullOrWhiteSpace(ClientSettings.InstallPath)
            ? Visibility.Visible
            : Visibility.Collapsed;

        public ServerInstallPathSettingPage()
        {
            InitializeComponent();

            if (ClientSettings is INotifyPropertyChanged npc)
                npc.PropertyChanged += ClientSettings_PropertyChanged;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            currentGameServer = e.Parameter switch
            {
                GameServer server => server,
                int index => (GameServer)index,
                _ => GameServer.Korea_Live
            };
            await LoadClientSettingsAsync();
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
                if (await SaveClientSettingsAsync())
                    IsDirty = false;
                else
                    CanLeaveThisPage = false;
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
                OnPropertyChanged(nameof(InstallGuideVisibility));
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

        private async void Button_Save_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (await SaveClientSettingsAsync())
                IsDirty = false;
        }

        private async Task LoadClientSettingsAsync()
        {
            (ClientSettings settings, AppDataManager.AppDataOperationResult result) =
                await AppDataManager.LoadServerClientSettingsAsync(currentGameServer);

            ClientSettings = settings;
            if (!result.Success)
            {
                await AppDataOperationDialog.ShowFailureAsync(
                    XamlRoot,
                    "설정 불러오기 실패",
                    "서버별 설치 경로 설정을 모두 불러오지 못했습니다.",
                    result);
            }
        }

        private async Task<bool> SaveClientSettingsAsync()
        {
            AppDataManager.AppDataOperationResult result =
                await AppDataManager.SaveServerClientSettingsAsync(currentGameServer, ClientSettings);

            if (result.Success)
                return true;

            await AppDataOperationDialog.ShowFailureAsync(
                XamlRoot,
                "설정 저장 실패",
                "서버별 설치 경로 설정을 저장하지 못했습니다.",
                result);
            return false;
        }

        private void Button_CreateMultiClient_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (!ClientSettings.UseSymbol)
                return;

            GameClientHelper.InstallPathValidationResult installPathValidation =
                GameClientHelper.TryValidateInstallPath(currentGameServer, TextBox_Path1.Text);

            if (!installPathValidation.Success)
            {
                TeachingTip_General.Title = "다클라 생성 실패";
                TeachingTip_General.Subtitle = BuildInstallPathValidationMessage(currentGameServer, TextBox_Path1.Text, installPathValidation);
                TeachingTip_General.IsOpen = true;
                return;
            }

            ClientVersionReadResult currentVersionResult = PatchManager.TryGetCurrentClientVersion(TextBox_Path1.Text);
            if (!currentVersionResult.Success || currentVersionResult.Version is null or <= 0)
            {
                TeachingTip_General.Title = "다클라 생성 실패";
                TeachingTip_General.Subtitle = "현재 클라이언트 버전을 확인할 수 없습니다. 설치 경로를 다시 확인해주세요.";
                TeachingTip_General.IsOpen = true;
                return;
            }

            int currentClientVersion = currentVersionResult.Version.Value;
            GameClientHelper.MultiClientLayoutPolicy layoutPolicy =
                currentClientVersion >= GameClientHelper.MultiClientLayoutBoundaryVersion
                    ? GameClientHelper.MultiClientLayoutPolicy.V34100OrLater
                    : GameClientHelper.MultiClientLayoutPolicy.Legacy;

            bool success = GameClientHelper.CreateSymbolMultiClient(new CreateSymbolMultiClientArgs
            {
                InstallPath = TextBox_Path1.Text,
                DestPath2 = ClientSettings.UseClient2 ? ClientSettings.Client2Path : string.Empty,
                DestPath3 = ClientSettings.UseClient3 ? ClientSettings.Client3Path : string.Empty,
                OverwriteConfig = ClientSettings.OverwriteMultiClientConfig,
                LayoutPolicy = layoutPolicy
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
            if (App.CurrentWindow is MainWindow window)
                window.NavigateToWebViewPage("https://github.com/byungmeo/GersangStation/discussions/39");
        }

        private void HyperlinkButton_InstallGame_Click(object sender, RoutedEventArgs e)
        {
            if (App.CurrentWindow is not MainWindow window)
                return;

            window.NavigateToSettingPage(
                SettingSection.GameInstall,
                new GameServerSettingNavigationParameter { Server = currentGameServer });
        }

        private void TextBox_InstallPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (ValidatedTextBox)sender;
            GameClientHelper.InstallPathValidationResult validationResult =
                GameClientHelper.TryValidateInstallPath(currentGameServer, textBox.Text);

            textBox.IsValid = validationResult.Success;
            textBox.ErrorText = validationResult.Success
                ? string.Empty
                : BuildInstallPathValidationMessage(currentGameServer, textBox.Text, validationResult);
            OnPropertyChanged(nameof(textBox.IsValid));
            if (validationResult.Success)
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

        /// <summary>
        /// 설치 경로 검증 실패를 사용자가 바로 이해할 수 있는 파일/폴더 기준 안내 문구로 변환합니다.
        /// </summary>
        private static string BuildInstallPathValidationMessage(
            GameServer server,
            string inputPath,
            GameClientHelper.InstallPathValidationResult validationResult)
        {
            string normalizedPath = TryNormalizePath(inputPath);
            string serverFileName = GameServerHelper.GetServerFileName(server);
            string serverDisplayName = GameServerHelper.GetServerDisplayName(server);

            return validationResult.FailureReason switch
            {
                GameClientHelper.InstallPathValidationFailureReason.EmptyPath =>
                    "설치 경로가 비어 있습니다.",
                GameClientHelper.InstallPathValidationFailureReason.InvalidPathFormat =>
                    "설치 경로 형식이 올바르지 않습니다.",
                GameClientHelper.InstallPathValidationFailureReason.MissingDirectory =>
                    $"지정한 폴더를 찾지 못했습니다.\n확인한 경로: {normalizedPath}",
                GameClientHelper.InstallPathValidationFailureReason.MissingRunExe =>
                    $"거상 실행 파일을 찾지 못했습니다.\n확인한 파일: {Path.Combine(normalizedPath, "Run.exe")}",
                GameClientHelper.InstallPathValidationFailureReason.MissingOnlineMapDirectory =>
                    $"거상 기본 데이터 폴더를 찾지 못했습니다.\n확인한 폴더: {Path.Combine(normalizedPath, "Online", "Map")}",
                GameClientHelper.InstallPathValidationFailureReason.MissingVsnDat =>
                    $"거상 버전 파일을 찾지 못했습니다.\n확인한 파일: {Path.Combine(normalizedPath, "Online", "vsn.dat")}",
                GameClientHelper.InstallPathValidationFailureReason.ClonePathUsedAsMainPath =>
                    "다클라 경로는 메인 설치 경로로 사용할 수 없습니다. 메인 거상 폴더를 선택해주세요.",
                GameClientHelper.InstallPathValidationFailureReason.ServerFileMismatch =>
                    $"{serverDisplayName} 서버 식별 파일을 찾지 못했습니다.\n확인한 파일: {Path.Combine(normalizedPath, serverFileName)}",
                GameClientHelper.InstallPathValidationFailureReason.SymbolDirectoryProbeFailed =>
                    "설치 경로의 심볼릭 링크 상태를 확인하지 못했습니다. 잠시 후 다시 시도해주세요.",
                _ => validationResult.Reason
            };
        }

        /// <summary>
        /// 오류 메시지에 표시할 경로를 최대한 사람이 읽기 쉬운 절대 경로로 정규화합니다.
        /// </summary>
        private static string TryNormalizePath(string inputPath)
        {
            string trimmedPath = (inputPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmedPath))
                return "(비어 있음)";

            try
            {
                return Path.GetFullPath(trimmedPath);
            }
            catch
            {
                return trimmedPath;
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
