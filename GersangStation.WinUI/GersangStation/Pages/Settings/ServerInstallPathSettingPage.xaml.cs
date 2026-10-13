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
using System.Diagnostics;
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
        private string? _multiClientConflictExplorerPath;
        private GameInstallSettingPage? _installPage;
        private bool _settingsLoaded;
        private bool _isCreatingMultiClient;
        private string _savedInstallPath = string.Empty;
        private bool _isInitializing = true;
        private Task _saveTask = Task.CompletedTask;
        private bool _isSaving;
        private AppDataManager.AppDataOperationResult? _saveFailure;
        private string _saveStatusText = string.Empty;
        private bool _client2Created;
        private bool _client3Created;

        internal Task Initialization { get; private set; } = Task.CompletedTask;
        internal bool SettingsLoaded => _settingsLoaded;
        public bool IsEditorEnabled => !_isCreatingMultiClient && _settingsLoaded && _installPage?.IsBusy != true;
        private bool HasPendingSave => _settingsLoaded && ClientSettings.InstallPath != _savedInstallPath;
        public string SaveStatusText => _saveStatusText;
        public Visibility SaveStatusVisibility => string.IsNullOrEmpty(SaveStatusText) ? Visibility.Collapsed : Visibility.Visible;
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

            var (settings, result) = await AppDataManager.LoadServerClientSettingsAsync(currentGameServer);
            ClientSettings = settings;
            _savedInstallPath = settings.InstallPath;
            TextBox_Path1.PlaceholderText = $"예시) {GameServerHelper.GetInstallPathPlaceholder(currentGameServer)}";
            TextBox_Path1.Text = settings.InstallPath;
            ValidateInstallPath();
            _settingsLoaded = result.Success;
            _isInitializing = false;
            NotifyEditorState();
            if (!result.Success)
                await AppDataOperationDialog.ShowFailureAsync(XamlRoot, "설정 불러오기 실패",
                    "서버별 설치 경로 설정을 불러오지 못했습니다. 기존 설정 보호를 위해 편집을 중단합니다.", result);
        }

        private void NotifyEditorState() => OnPropertyChanged(nameof(IsEditorEnabled));

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
            e.Cancel = _isCreatingMultiClient || HasPendingSave;
        }

        public async Task<bool> ConfirmLeaveAsync(LeaveReason reason = LeaveReason.Navigation)
        {
            if (_isCreatingMultiClient)
            {
                ContentDialog busy = new() { XamlRoot = XamlRoot, Title = "작업 진행 중", Content = "다클라 생성이 끝난 뒤 이동하거나 종료해 주세요.", CloseButtonText = "확인" };
                await busy.ShowManagedAsync();
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
                _saveFailure = null;
                SetSaveStatus(string.Empty);
                return;
            }
            _isSaving = true;
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
                        _saveFailure = result;
                        // An edit received while awaiting this write must not be lost.
                        if (path != ClientSettings.InstallPath)
                            continue;
                        SetSaveStatus("자동 저장 실패. 경로 변경 또는 페이지 이동 시 다시 시도합니다.");
                        return;
                    }
                    _saveFailure = null;
                    _savedInstallPath = path;
                }
                SetSaveStatus(string.Empty);
            }
            finally { _isSaving = false; }
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
            if (_saveFailure is { } failure)
                await AppDataOperationDialog.ShowFailureAsync(XamlRoot, "자동 저장 실패",
                    "설치 경로를 저장하지 못했습니다. 입력한 경로는 그대로 유지됩니다. 문제를 해결한 뒤 다시 시도해 주세요.", failure);
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

                GameClientHelper.InstallPathValidationResult installPathValidation =
                    GameClientHelper.TryValidateInstallPath(currentGameServer, TextBox_Path1.Text);

                if (!installPathValidation.Success)
                {
                    ResetGeneralTeachingTipAction();
                    TeachingTip_General.Title = "다클라 생성 실패";
                    TeachingTip_General.Subtitle = BuildInstallPathValidationMessage(currentGameServer, TextBox_Path1.Text, installPathValidation);
                    TeachingTip_General.IsOpen = true;
                    return;
                }

                ClientVersionReadResult currentVersionResult = PatchManager.TryGetCurrentClientVersion(TextBox_Path1.Text);
                if (!currentVersionResult.Success || currentVersionResult.Version is null or <= 0)
                {
                    ResetGeneralTeachingTipAction();
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

                CreateSymbolMultiClientResult createResult;
                try
                {
                    await GameMaintenanceService.CreateMultiClientAsync(currentGameServer, ClientSettings, new CreateSymbolMultiClientArgs
                    {
                        InstallPath = TextBox_Path1.Text,
                        DestPath2 = ClientSettings.Client2Path,
                        DestPath3 = ClientSettings.Client3Path,
                        LayoutPolicy = layoutPolicy
                    });
                    createResult = new(true, string.Empty, null, null, null);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex) { createResult = new(false, ex.Message, null, null, ex); }

                if (createResult.Success)
                {
                    ResetGeneralTeachingTipAction();
                    TeachingTip_General.Title = "다클라 생성 성공";
                    TeachingTip_General.Subtitle = "";
                }
                else if (TryShowMultiClientSymbolicDirectoryConflictTeachingTip(createResult.Exception))
                {
                    return;
                }
                else
                {
                    Exception exception = createResult.Exception ?? new IOException(createResult.Reason);
                    if (await PathPermissionDialog.ShowFailureGuidanceWhenPermissionMissingAsync(XamlRoot, exception, "다클라 생성"))
                        return;

                    await App.ExceptionHandler.ShowRecoverableAsync(exception, $"ServerInstallPathSettingPage.Button_CreateMultiClient_Click");
                    return;
                }

                TeachingTip_General.IsOpen = true;
            }
            finally
            {
                _isCreatingMultiClient = false;
                RefreshMultiClientStatus();
                NotifyEditorState();
            }
        }

        /// <summary>
        /// 심볼릭 링크로 대체해야 하는 폴더가 일반 폴더로 존재하는 충돌을 경로 설정 화면의 TeachingTip으로 안내합니다.
        /// </summary>
        private bool TryShowMultiClientSymbolicDirectoryConflictTeachingTip(Exception? exception)
        {
            GameClientHelper.MultiClientSymbolicDirectoryConflictException? conflictException =
                FindException<GameClientHelper.MultiClientSymbolicDirectoryConflictException>(exception);
            if (conflictException is null)
                return false;

            GameClientHelper.MultiClientCreationException? creationException =
                FindException<GameClientHelper.MultiClientCreationException>(exception);

            _multiClientConflictExplorerPath = ResolveMultiClientConflictExplorerPath(creationException, conflictException);
            TeachingTip_General.IconSource = new FontIconSource
            {
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
                Glyph = "\uEA39"
            };
            TeachingTip_General.ActionButtonContent = "상위 폴더 열기";
            TeachingTip_General.CloseButtonContent = "닫기";
            TeachingTip_General.Title = "다클라 생성 실패";
            TeachingTip_General.Subtitle = BuildMultiClientSymbolicDirectoryConflictMessage(creationException, conflictException);
            TeachingTip_General.IsOpen = true;
            return true;
        }

        private static string BuildMultiClientSymbolicDirectoryConflictMessage(
            GameClientHelper.MultiClientCreationException? creationException,
            GameClientHelper.MultiClientSymbolicDirectoryConflictException conflictException)
        {
            string targetPath = creationException?.DestinationPath ?? conflictException.DestinationPath;
            string targetFolderName = TryGetDirectoryName(targetPath) ?? targetPath;
            return $"{targetFolderName}{Environment.NewLine}위 폴더를 삭제하신 후 다시 다클라를 생성해주세요.";
        }

        private void ResetGeneralTeachingTipAction()
        {
            _multiClientConflictExplorerPath = null;
            TeachingTip_General.IconSource = null;
            TeachingTip_General.ActionButtonContent = null;
            TeachingTip_General.CloseButtonContent = "네, 확인했습니다.";
        }

        private void TeachingTip_General_ActionButtonClick(TeachingTip sender, object args)
        {
            if (string.IsNullOrWhiteSpace(_multiClientConflictExplorerPath))
                return;

            try
            {
                if (!Directory.Exists(_multiClientConflictExplorerPath))
                {
                    TeachingTip_General.Subtitle = "대상 폴더를 찾을 수 없습니다.";
                    return;
                }

                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_multiClientConflictExplorerPath}\"")
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                TeachingTip_General.Subtitle = $"폴더 열기 실패: {ex.Message}";
            }
        }

        private static string ResolveMultiClientConflictExplorerPath(
            GameClientHelper.MultiClientCreationException? creationException,
            GameClientHelper.MultiClientSymbolicDirectoryConflictException conflictException)
        {
            string sourcePath = creationException?.SourcePath ?? conflictException.SourcePath;
            string destinationPath = creationException?.DestinationPath ?? conflictException.DestinationPath;
            string? sourceParent = TryGetParentDirectory(sourcePath);
            string? destinationParent = TryGetParentDirectory(destinationPath);

            if (!string.IsNullOrWhiteSpace(sourceParent)
                && string.Equals(sourceParent, destinationParent, StringComparison.OrdinalIgnoreCase))
            {
                return sourceParent;
            }

            return TryGetCommonDirectory(sourcePath, destinationPath)
                ?? destinationParent
                ?? sourceParent
                ?? TryGetParentDirectory(conflictException.DestinationPath)
                ?? conflictException.DestinationPath;
        }

        private static string? TryGetParentDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                string fullPath = Path.GetFullPath(Path.TrimEndingDirectorySeparator(path.Trim()));
                return Directory.GetParent(fullPath)?.FullName;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }

        private static string? TryGetDirectoryName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                string fullPath = Path.GetFullPath(Path.TrimEndingDirectorySeparator(path.Trim()));
                return Path.GetFileName(fullPath);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }

        private static string? TryGetCommonDirectory(string firstPath, string secondPath)
        {
            try
            {
                string firstFullPath = Path.GetFullPath(Path.TrimEndingDirectorySeparator(firstPath.Trim()));
                string secondFullPath = Path.GetFullPath(Path.TrimEndingDirectorySeparator(secondPath.Trim()));
                DirectoryInfo? current = Directory.Exists(firstFullPath)
                    ? new DirectoryInfo(firstFullPath)
                    : Directory.GetParent(firstFullPath);

                while (current is not null)
                {
                    string candidate = current.FullName;
                    if (secondFullPath.StartsWith(
                        Path.TrimEndingDirectorySeparator(candidate) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }

                    current = current.Parent;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or PathTooLongException)
            {
            }

            return null;
        }

        private static TException? FindException<TException>(Exception? exception)
            where TException : Exception
        {
            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                if (current is TException matched)
                    return matched;
            }

            return null;
        }

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
