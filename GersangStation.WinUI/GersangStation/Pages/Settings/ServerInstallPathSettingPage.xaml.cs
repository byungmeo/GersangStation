using Core;
using Core.Models;
using GersangStation.Controls;
using GersangStation.Diagnostics;
using GersangStation.Services;
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
    public sealed class GameClientSettingsNavigationParameter
    {
        public GameServer Server { get; init; }
        public GameInstallSettingPage? InstallPage { get; init; }
    }

    public sealed partial class ServerInstallPathSettingPage : Page, INotifyPropertyChanged, IConfirmLeave
    {
        private GameServer currentGameServer = GameServer.Korea_Live;
        private MultiClientCreationFeedback? _multiClientFeedback;
        private GameInstallSettingPage? _installPage;
        private bool _settingsLoaded;
        private bool _isLoadingSettings;
        private bool _hasSettingsLoadFailure;
        private bool _isCreatingMultiClient;
        private string _savedInstallPath = string.Empty;
        private bool _isInitializing = true;
        private Task _saveTask = Task.CompletedTask;
        private bool _isSaving;
        private bool _hasAutoSaveFailure;
        private string _saveStatusText = string.Empty;
        private bool _client2Created;
        private bool _client3Created;

        internal Task Initialization { get; private set; } = Task.CompletedTask;
        internal bool SettingsLoaded => _settingsLoaded;
        public bool IsEditorEnabled => !_isCreatingMultiClient && _settingsLoaded && _installPage?.IsBusy != true;
        public bool IsEditorDisabled => !IsEditorEnabled;
        private bool HasPendingSave => _settingsLoaded && ClientSettings.InstallPath != _savedInstallPath;
        internal bool HasBlockingOperation => _isCreatingMultiClient || _isSaving || HasPendingSave;
        public string SaveStatusText => _saveStatusText;
        public Visibility SaveStatusVisibility => string.IsNullOrEmpty(SaveStatusText) ? Visibility.Collapsed : Visibility.Visible;
        public bool HasAutoSaveFailure => _hasAutoSaveFailure;
        public bool CanRetryAutoSave => HasAutoSaveFailure && HasPendingSave && !_isSaving && IsEditorEnabled;
        public bool HasSettingsLoadFailure => _hasSettingsLoadFailure;
        public bool CanRetrySettingsLoad => HasSettingsLoadFailure && !_isLoadingSettings;
        public string Client2StatusText => _client2Created ? "2클라 생성됨" : "2클라 미생성";
        public string Client3StatusText => _client3Created ? "3클라 생성됨" : "3클라 미생성";
        public string Client2StatusGlyph => _client2Created ? "\uE73E" : "\uE711";
        public string Client3StatusGlyph => _client3Created ? "\uE73E" : "\uE711";
        public string Client2PathText => ClientSettings.Client2Path;
        public string Client3PathText => ClientSettings.Client3Path;
        public FrameworkElement ResultTeachingTipAnchor
            => _installPage?.ResultTeachingTipAnchor ?? PathSettingsTeachingTipAnchor;

        private ClientSettings _clientSettings = new();
        public ClientSettings ClientSettings
        {
            get => _clientSettings;
            private set
            {
                if (ReferenceEquals(_clientSettings, value))
                    return;
                _clientSettings.PropertyChanged -= ClientSettings_PropertyChanged;
                _clientSettings = value;
                _clientSettings.PropertyChanged += ClientSettings_PropertyChanged;
                OnPropertyChanged();
                RefreshMultiClientStatus();
            }
        }

        public ServerInstallPathSettingPage()
        {
            InitializeComponent();
            ClientSettings.PropertyChanged += ClientSettings_PropertyChanged;
            Loaded += (_, _) =>
            {
                // Server panels may leave and re-enter the visual tree during a transition.
                ClientSettings.PropertyChanged -= ClientSettings_PropertyChanged;
                ClientSettings.PropertyChanged += ClientSettings_PropertyChanged;
                if (_installPage is not null)
                {
                    _installPage.PropertyChanged -= InstallPage_PropertyChanged;
                    _installPage.PropertyChanged += InstallPage_PropertyChanged;
                }
                RefreshMultiClientStatus();
            };
            Unloaded += (_, _) =>
            {
                TeachingTip_General.IsOpen = false;
                ClientSettings.PropertyChanged -= ClientSettings_PropertyChanged;
                if (_installPage is not null)
                    _installPage.PropertyChanged -= InstallPage_PropertyChanged;
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is GameClientSettingsNavigationParameter parameter)
            {
                currentGameServer = parameter.Server;
                _installPage = parameter.InstallPage;
                if (_installPage is not null)
                {
                    _installPage.PropertyChanged -= InstallPage_PropertyChanged;
                    _installPage.PropertyChanged += InstallPage_PropertyChanged;
                }
            }
            else
                currentGameServer = e.Parameter is GameServer server ? server : GameServer.Korea_Live;

            OnPropertyChanged(nameof(ResultTeachingTipAnchor));

            Initialization = SafeExecution.RunHandledAsync(InitializeSettingsAsync,
                $"{nameof(ServerInstallPathSettingPage)}.{nameof(OnNavigatedTo)}");
        }

        private async Task InitializeSettingsAsync()
        {
            if (!IsLoaded)
            {
                TaskCompletionSource loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
                void OnLoaded(object sender, RoutedEventArgs args) => loaded.TrySetResult();
                Loaded += OnLoaded;
                try { await loaded.Task; }
                finally { Loaded -= OnLoaded; }
            }

            await LoadSettingsAsync();
        }

        private async Task LoadSettingsAsync()
        {
            // 성공한 편집 세션을 재시도로 덮어쓰거나 동일 읽기를 중복 실행하지 않습니다.
            if (_isLoadingSettings || _settingsLoaded)
                return;
            _isLoadingSettings = true;
            _isInitializing = true;
            OnPropertyChanged(nameof(CanRetrySettingsLoad));
            try
            {
                var (settings, result) = await AppDataManager.LoadServerClientSettingsAsync(currentGameServer);
                if (!result.Success)
                {
                    App.ExceptionHandler.RecordRecoverable(new IOException(
                        $"설치 경로 설정 불러오기 실패: {result.Operation}, {result.Target}, {result.ErrorKind}", result.Exception),
                        $"ServerInstallPathSettingPage.LoadSettings.{currentGameServer}");
                    _hasSettingsLoadFailure = true;
                    return;
                }

                ClientSettings = settings;
                _savedInstallPath = settings.InstallPath;
                TextBox_Path1.PlaceholderText = $"예시) {GameServerHelper.GetInstallPathPlaceholder(currentGameServer)}";
                TextBox_Path1.Text = settings.InstallPath;
                ValidateInstallPath();
                _settingsLoaded = true;
                _hasSettingsLoadFailure = false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                App.ExceptionHandler.RecordRecoverable(ex, $"ServerInstallPathSettingPage.LoadSettings.{currentGameServer}");
                _hasSettingsLoadFailure = true;
            }
            finally
            {
                _isLoadingSettings = false;
                _isInitializing = false;
                OnPropertyChanged(nameof(HasSettingsLoadFailure));
                OnPropertyChanged(nameof(CanRetrySettingsLoad));
                NotifyEditorState();
            }
        }

        private async void RetrySettingsLoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CanRetrySettingsLoad)
                return;
            Initialization = SafeExecution.RunHandledAsync(LoadSettingsAsync,
                $"{nameof(ServerInstallPathSettingPage)}.RetrySettingsLoad");
            await Initialization;
        }

        private void NotifyEditorState()
        {
            OnPropertyChanged(nameof(IsEditorEnabled));
            OnPropertyChanged(nameof(IsEditorDisabled));
            OnPropertyChanged(nameof(CanRetryAutoSave));
        }

        private void InstallPage_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GameInstallSettingPage.IsBusy))
                NotifyEditorState();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            // The owning page awaits ConfirmLeaveAsync before navigation. Never await in this
            // synchronous cancellation callback: a late e.Cancel cannot stop Frame navigation.
            e.Cancel |= HasBlockingOperation;
        }

        public async Task<bool> ConfirmLeaveAsync(LeaveReason reason = LeaveReason.Navigation)
        {
            if (_isCreatingMultiClient)
            {
                TeachingTip_General.Title = "작업 진행 중";
                TeachingTip_General.Subtitle = "다클라 생성이 끝난 뒤 다시 시도해 주세요.";
                TeachingTip_General.ActionButtonContent = null;
                TeachingTip_General.IconSource = null;
                TeachingTip_General.CloseButtonContent = "닫기";
                TeachingTip_General.IsOpen = true;
                return false;
            }
            if (!await FlushAutoSaveAsync())
                return false;

            if (reason == LeaveReason.AppExit && XamlRoot is not null
                && !await ExitConfirmationDialog.ShowAsync(XamlRoot))
                return false;
            TeachingTip_General.IsOpen = false;
            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void ClientSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ClientSettings.InstallPath))
                return;
            RefreshMultiClientStatus();
            if (!_isInitializing && _settingsLoaded)
                QueueAutoSave();
        }

        private void SetSaveStatus(string text)
        {
            _saveStatusText = text;
            OnPropertyChanged(nameof(SaveStatusText));
            OnPropertyChanged(nameof(SaveStatusVisibility));
        }

        private void QueueAutoSave()
        {
            if (_isSaving)
                return;
            if (!HasPendingSave)
            {
                SetAutoSaveFailure(false);
                SetSaveStatus(string.Empty);
                return;
            }
            _isSaving = true;
            OnPropertyChanged(nameof(CanRetryAutoSave));
            _saveTask = SafeExecution.RunHandledAsync(SavePendingPathAsync,
                $"{nameof(ServerInstallPathSettingPage)}.AutoSave");
        }

        private async Task SavePendingPathAsync()
        {
            // Publish _saveTask before yielding to any new TextChanged event.
            await Task.Yield();
            try
            {
                SetSaveStatus("자동 저장 중...");
                while (HasPendingSave)
                {
                    string path = ClientSettings.InstallPath;
                    var result = await AppDataManager.SaveServerInstallPathAsync(currentGameServer, path);
                    if (!result.Success)
                    {
                        App.ExceptionHandler.RecordRecoverable(new IOException(
                            $"설치 경로 자동 저장 실패: {result.Operation}, {result.Target}, {result.ErrorKind}", result.Exception),
                            $"ServerInstallPathSettingPage.AutoSave.{currentGameServer}");
                        // An edit received while awaiting this write must not be lost.
                        if (path != ClientSettings.InstallPath)
                            continue;
                        SetSaveStatus(string.Empty);
                        SetAutoSaveFailure(true);
                        return;
                    }
                    _savedInstallPath = path;
                    SetAutoSaveFailure(false);
                }
                SetAutoSaveFailure(false);
                SetSaveStatus(string.Empty);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                App.ExceptionHandler.RecordRecoverable(ex, $"ServerInstallPathSettingPage.AutoSave.{currentGameServer}");
                SetSaveStatus(string.Empty);
                SetAutoSaveFailure(true);
            }
            finally
            {
                _isSaving = false;
                OnPropertyChanged(nameof(CanRetryAutoSave));
            }
        }

        private void SetAutoSaveFailure(bool failed)
        {
            _hasAutoSaveFailure = failed;
            OnPropertyChanged(nameof(HasAutoSaveFailure));
            OnPropertyChanged(nameof(CanRetryAutoSave));
        }

        private async void RetryAutoSaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CanRetryAutoSave)
                return;
            QueueAutoSave();
            await _saveTask;
        }

        private async Task<bool> FlushAutoSaveAsync()
        {
            // Await a running write, then retry once if the last write failed.
            await _saveTask;
            if (!HasPendingSave)
                return true;
            QueueAutoSave();
            await _saveTask;
            if (!HasPendingSave)
                return true;
            return false;
        }

        /// <summary>새 설치와 기존 경로 편집이 같은 자동 저장 흐름을 사용합니다.</summary>
        internal async Task<bool> SaveInstalledPathAsync(string installPath)
        {
            TextBox_Path1.Text = installPath;
            ClientSettings.InstallPath = installPath;
            ValidateInstallPath();
            return await FlushAutoSaveAsync();
        }

        internal void RefreshMultiClientStatus()
        {
            _client2Created = GameClientHelper.TryValidateCloneInstallPath(currentGameServer, ClientSettings.Client2Path).Success;
            _client3Created = GameClientHelper.TryValidateCloneInstallPath(currentGameServer, ClientSettings.Client3Path).Success;
            OnPropertyChanged(nameof(Client2StatusText));
            OnPropertyChanged(nameof(Client3StatusText));
            OnPropertyChanged(nameof(Client2StatusGlyph));
            OnPropertyChanged(nameof(Client3StatusGlyph));
            OnPropertyChanged(nameof(Client2PathText));
            OnPropertyChanged(nameof(Client3PathText));
        }

        private void Button_CreateMultiClient_Click(object sender, RoutedEventArgs e)
            => SafeExecution.RunHandledAsync(CreateMultiClientAsync, $"{nameof(ServerInstallPathSettingPage)}.{nameof(Button_CreateMultiClient_Click)}")
                .FireAndForgetHandled($"{nameof(ServerInstallPathSettingPage)}.{nameof(Button_CreateMultiClient_Click)}");

        private MultiClientCreationFeedback MultiClientFeedback
            => _multiClientFeedback ??= new(TeachingTip_General);

        private async Task CreateMultiClientAsync()
        {
            if (!IsEditorEnabled)
                return;

            _isCreatingMultiClient = true;
            NotifyEditorState();
            try
            {
                if (!await FlushAutoSaveAsync())
                    return;

                MultiClientCreationResult result = await MultiClientCreationService.CreateAsync(currentGameServer, ClientSettings);
                MultiClientFeedback.Show(currentGameServer, result);
            }
            finally
            {
                _isCreatingMultiClient = false;
                RefreshMultiClientStatus();
                NotifyEditorState();
            }
        }

        private void TeachingTip_General_ActionButtonClick(TeachingTip sender, object args)
            => _multiClientFeedback?.InvokeAction();

        private void Button_InstallNew_Click(object sender, RoutedEventArgs e)
            => _installPage?.InstallSelectedServerAsync().FireAndForgetHandled(
                $"{nameof(ServerInstallPathSettingPage)}.{nameof(Button_InstallNew_Click)}");

        private void TextBox_InstallPath_TextChanged(object sender, TextChangedEventArgs e)
            => ValidateInstallPath();

        private void ValidateInstallPath()
        {
            var textBox = TextBox_Path1;
            GameClientHelper.InstallPathValidationResult validationResult =
                GameClientHelper.TryValidateInstallPath(currentGameServer, textBox.Text);

            textBox.IsValid = validationResult.Success;
            textBox.ErrorText = validationResult.Success
                ? string.Empty
                : BuildInstallPathValidationMessage(currentGameServer, textBox.Text, validationResult);
            OnPropertyChanged(nameof(textBox.IsValid));
            RefreshMultiClientStatus();
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
                    "설치 경로의 디렉터리 링크 상태를 확인하지 못했습니다. 잠시 후 다시 시도해주세요.",
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
            if (_installPage is not null)
                await SafeExecution.RunHandledAsync(() => _installPage.PickExistingInstallPathAsync(TextBox_Path1),
                    $"{nameof(ServerInstallPathSettingPage)}.{nameof(Button_Path_PickFolder_Click)}");
            else if (sender is Button button)
            {
                var picker = new FolderPicker(button.XamlRoot.ContentIslandEnvironment.AppWindowId)
                {
                    CommitButtonText = "폴더 선택",
                    SuggestedStartLocation = PickerLocationId.ComputerFolder,
                    ViewMode = PickerViewMode.List
                };

                var folder = await picker.PickSingleFolderAsync();
                if (folder is not null)
                {
                    TextBox_Path1.Text = folder.Path;
                }

            }
        }
    }
}
