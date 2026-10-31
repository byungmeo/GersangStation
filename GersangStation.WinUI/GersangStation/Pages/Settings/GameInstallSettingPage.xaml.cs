using CommunityToolkit.WinUI.Controls;
using Core;
using Core.Download;
using Core.Models;
using GameMaintenance;
using GersangStation.Controls;
using GersangStation.Diagnostics;
using GersangStation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Windows.Storage.Pickers;

namespace GersangStation.Main.Setting;

/// <summary>
/// 서버별 게임 설치, 설치 경로 자동 저장, 다클라 생성 상태를 함께 제공합니다.
/// </summary>
public sealed partial class GameInstallSettingPage : Page, INotifyPropertyChanged, IConfirmLeave
{
    private enum InstallProgressPhase
    {
        Downloading,
        Extracting
    }

    private GameServer _selectedGameServer = GameServer.Korea_Live;
    private readonly ContentTransition _panelTransition;
    private bool _suppressServerSelectionChanged;
    private string _progressText = string.Empty;
    private bool _isDownloading;
    private bool _hasInstallProgress;
    private CancellationTokenSource? _installCts;
    private bool _deleteArchiveArtifactsOnCancel;
    private bool _isPreparingInstall;
    private bool _isLoadingSettings;
    private bool _installResultPermissionHelp;
    private InstallProgressPhase _progressPhase = InstallProgressPhase.Downloading;
    private double _downloadProgressMaximum = 100;
    private double _downloadProgressValue;
    private double _extractProgressMaximum = 100;
    private double _extractProgressValue;

    public GameInstallSettingPage()
    {
        InitializeComponent();
        _panelTransition = new ContentTransition(InstallContentPresenter, slide: true);
        Unloaded += (_, _) => CancelPanelTransition();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public GameServer SelectedGameServer => _selectedGameServer;

    // Nested server editors share the owning page's viewport, not their scrollable content.
    internal FrameworkElement ResultTeachingTipAnchor => InstallTeachingTipAnchor;

    private GameInstallPanel CurrentInstallPanel => _selectedGameServer switch
    {
        GameServer.Korea_Live => InstallPanel_Live,
        GameServer.Korea_Test => InstallPanel_Test,
        GameServer.Korea_RnD => InstallPanel_RnD,
        _ => throw new ArgumentOutOfRangeException(nameof(SelectedGameServer), _selectedGameServer, null)
    };

    public static string ServerSelectionGlyph(bool isSelected) => isSelected ? "\uE73A" : "\uE739";

    public bool IsNotBusy => !IsBusy;
    public bool IsBusy => IsDownloading || _isPreparingInstall || _isLoadingSettings;
    public Visibility InstallProgressVisibility => _hasInstallProgress ? Visibility.Visible : Visibility.Collapsed;
    public Visibility InstallCancelVisibility => IsDownloading ? Visibility.Visible : Visibility.Collapsed;

    public double ProgressMaximum => _progressPhase == InstallProgressPhase.Extracting
        ? _extractProgressMaximum
        : _downloadProgressMaximum;

    public double ProgressValue => _progressPhase == InstallProgressPhase.Extracting
        ? _extractProgressValue
        : _downloadProgressValue;

    public string ProgressText
    {
        get => _progressText;
        set => SetProperty(ref _progressText, value);
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (SetProperty(ref _isDownloading, value))
            {
                NotifyBusyChanged();
                OnPropertyChanged(nameof(InstallCancelVisibility));
            }
        }
    }

    /// <summary>
    /// 외부에서 전달된 서버 파라미터를 반영하고 저장된 클라이언트 설정을 불러옵니다.
    /// </summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        GameServer initialServer = e.Parameter switch
        {
            GameServerSettingNavigationParameter parameter => parameter.Server,
            GameServer server => server,
            int index when Enum.IsDefined(typeof(GameServer), index) => (GameServer)index,
            _ => GameServer.Korea_Live
        };

        ApplySelectedServerAsync(initialServer)
            .FireAndForgetHandled($"{nameof(GameInstallSettingPage)}.{nameof(OnNavigatedTo)}");
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);

        // 비동기 확인/저장은 셸에서 먼저 완료합니다. 직접 Frame 이동도 작업을 건너뛰지 못하게 합니다.
        e.Cancel |= IsBusy || CurrentInstallPanel.SettingsEditor?.HasBlockingOperation == true;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        CancelPanelTransition();
        base.OnNavigatedFrom(e);
    }

    internal void CancelPanelTransition()
    {
        HideInstallResult();
        _panelTransition.Cancel();
        SynchronizeServerSelection();
    }

    internal void RefreshMultiClientStatus()
        => CurrentInstallPanel.SettingsEditor?.RefreshMultiClientStatus();

    /// <summary>
    /// 선택한 서버에 맞게 설치 경로와 다클라 상태를 불러옵니다.
    /// </summary>
    private async Task ApplySelectedServerAsync(GameServer server)
    {
        HideInstallResult();
        SetProperty(ref _selectedGameServer, server, nameof(SelectedGameServer));
        SynchronizeServerSelection();

        _isLoadingSettings = true;
        NotifyBusyChanged();
        try { await CurrentInstallPanel.LoadSettingsAsync(server, this); }
        finally
        {
            _isLoadingSettings = false;
            NotifyBusyChanged();
        }
    }

    private void NotifyBusyChanged()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(IsNotBusy));
    }

    private void SynchronizeServerSelection()
    {
        SegmentedItem selectedItem = GetSegmentedItem(_selectedGameServer);

        _suppressServerSelectionChanged = true;
        try
        {
            Segmented_Server.SelectedItem = selectedItem;
        }
        finally
        {
            _suppressServerSelectionChanged = false;
        }
    }

    private void Segmented_Server_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (sender is not Segmented segmented || segmented.SelectedItem is not SegmentedItem selectedItem)
            return;

        if (_suppressServerSelectionChanged)
            return;

        GameServer server = segmented.Items.IndexOf(selectedItem) switch
        {
            0 => GameServer.Korea_Live,
            1 => GameServer.Korea_Test,
            2 => GameServer.Korea_RnD,
            _ => throw new ArgumentOutOfRangeException(nameof(sender), sender, null)
        };

        ChangeServerAsync(server)
            .FireAndForgetHandled($"{nameof(GameInstallSettingPage)}.{nameof(Segmented_Server_SelectionChanged)}");
    }

    private async Task ChangeServerAsync(GameServer server)
    {
        bool changed = await _panelTransition.RunAsync(async () =>
        {
            if (_selectedGameServer != server)
                await ApplySelectedServerAsync(server);
            return true;
        }, async () => !IsBusy && (CurrentInstallPanel.SettingsEditor is not { } editor
            || await editor.ConfirmLeaveAsync()), () => _selectedGameServer != server);
        if (!changed)
            SynchronizeServerSelection();
    }

    /// <summary>
    /// 사용자가 선택한 루트 경로를 서버별 실제 거상 설치 경로로 변환합니다.
    /// </summary>
    private static string BuildFinalInstallPath(string? baseRoot, GameServer server)
    {
        return string.IsNullOrWhiteSpace(baseRoot)
            ? string.Empty
            : Path.Combine(Path.GetFullPath(baseRoot.Trim()), "AKInteractive", GameServerHelper.GetClientFolderName(server));
    }

    /// <summary>
    /// 서버 값에 대응하는 SegmentedItem을 반환합니다.
    /// </summary>
    private SegmentedItem GetSegmentedItem(GameServer server)
        => server switch
        {
            GameServer.Korea_Live => SegmentedItem_Live,
            GameServer.Korea_Test => SegmentedItem_Test,
            GameServer.Korea_RnD => SegmentedItem_RnD,
            _ => throw new ArgumentOutOfRangeException(nameof(server), server, null),
        };

    /// <summary>
    /// 기존 게임 경로 지정용 폴더 선택기를 엽니다. 다운로드는 수행하지 않습니다.
    /// </summary>
    internal async Task PickExistingInstallPathAsync(ValidatedTextBox pathInput)
    {
        if (IsBusy)
            return;
        CancelPanelTransition();
        _isPreparingInstall = true;
        NotifyBusyChanged();
        try
        {
            FolderPicker picker = new(XamlRoot.ContentIslandEnvironment.AppWindowId)
            {
                CommitButtonText = "선택",
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.List
            };

            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null)
                pathInput.Text = folder.Path;
        }
        finally
        {
            _isPreparingInstall = false;
            NotifyBusyChanged();
        }
    }

    /// <summary>
    /// 별도의 상위 폴더를 선택받아 즉시 설치하고, 성공한 최종 게임 경로만 설정 폼에 반영합니다.
    /// </summary>
    internal async Task InstallSelectedServerAsync()
    {
        if (IsBusy || CurrentInstallPanel.SettingsEditor is not { SettingsLoaded: true } editor)
            return;
        CancelPanelTransition();
        _isPreparingInstall = true;
        NotifyBusyChanged();
        try
        {
            FolderPicker picker = new(XamlRoot.ContentIslandEnvironment.AppWindowId)
            {
                CommitButtonText = "선택 후 설치",
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.List
            };
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null)
                return;
            string finalInstallPath = BuildFinalInstallPath(folder.Path, _selectedGameServer);
            if (!GameInstallManager.IsInstallTargetPathAvailable(_selectedGameServer, finalInstallPath))
            {
                ShowInstallResult("설치 경로 확인",
                    $"최종 설치 경로에 이미 게임 또는 다른 파일이 존재합니다. 다른 상위 폴더를 선택해 주세요.\n{finalInstallPath}", InfoBarSeverity.Warning);
                return;
            }
            await RunInstallAsync(finalInstallPath, editor);
        }
        catch (Exception ex)
        {
            ReportInstallFailure(ex, "PrepareInstall", "설치를 준비하지 못했습니다. 설치할 폴더를 다시 선택한 후 시도해 주세요.");
        }
        finally
        {
            _isPreparingInstall = false;
            NotifyBusyChanged();
        }
    }

    private async Task RunInstallAsync(string finalInstallPath, ServerInstallPathSettingPage editor)
    {
        _installCts?.Dispose();
        _installCts = new CancellationTokenSource();
        CancellationTokenSource installCancellation = _installCts;
        _deleteArchiveArtifactsOnCancel = false;

        try
        {
            IsDownloading = true;
            ShowInstallProgress();

            ResetProgressState();
            ProgressText = "설치를 준비하고 있습니다...";

            Progress<GameInstallProgress> installProgress = new(p =>
            {
                // 완료/취소 후 도착한 이전 작업의 진행 알림이 결과 문구를 덮지 않게 합니다.
                if (!IsDownloading || !ReferenceEquals(_installCts, installCancellation)
                    || installCancellation.IsCancellationRequested)
                    return;
                string downloadFileName = string.IsNullOrWhiteSpace(p.DownloadFileName) ? "-" : p.DownloadFileName;
                string downloadAmount = p.DownloadTotalBytes is long totalBytes && totalBytes > 0
                    ? $"{FormatBytes(p.DownloadedBytes)} / {FormatBytes(totalBytes)}"
                    : FormatBytes(p.DownloadedBytes);

                string extractAmount = p.ExtractTotalEntries is > 0
                    ? $"{p.ExtractedEntries} / {p.ExtractTotalEntries}"
                    : p.ExtractedEntries.ToString();

                string extractEntry = string.IsNullOrWhiteSpace(p.CurrentExtractEntry)
                    ? "-"
                    : p.CurrentExtractEntry;

                if (p.ExtractPercent > 0 || p.ExtractedEntries > 0)
                {
                    SetExtractProgress(100, Math.Clamp(p.ExtractPercent, 0, 100));
                    ProgressText = $"압축 해제: {extractEntry}  {extractAmount}  {p.ExtractPercent:F1}%";
                    return;
                }

                SetDownloadProgress(
                    p.DownloadTotalBytes is long totalBytesForMaximum && totalBytesForMaximum > 0
                        ? totalBytesForMaximum
                        : 100,
                    p.DownloadTotalBytes is long totalBytesForValue && totalBytesForValue > 0
                        ? Math.Clamp(p.DownloadedBytes, 0, totalBytesForValue)
                        : Math.Clamp(p.DownloadPercent ?? 0, 0, 100));
                ProgressText = $"다운로드: {downloadFileName}  {downloadAmount}  {(p.DownloadPercent ?? 0):F1}%";
            });

            await GameMaintenanceService.InstallAsync(
                _selectedGameServer,
                finalInstallPath,
                DownloadExistingArtifactMode.ResumeIfPossible,
                installProgress,
                () => _deleteArchiveArtifactsOnCancel,
                installCancellation.Token);

            // 다운로드/압축 해제는 끝났습니다. 경로 저장 중에는 준비 상태로
            // 이동을 막되, 완료된 설치를 취소하는 버튼은 더 이상 표시하지 않습니다.
            IsDownloading = false;
            bool pathSaved;
            try { pathSaved = await editor.SaveInstalledPathAsync(finalInstallPath); }
            catch (Exception ex)
            {
                App.ExceptionHandler.RecordRecoverable(new IOException("설치 완료 후 경로 저장 실패", ex),
                    $"GameInstallSettingPage.SaveInstalledPath.{_selectedGameServer}");
                pathSaved = false;
            }

            SetExtractProgress(_extractProgressMaximum, _extractProgressMaximum);
            ShowInstallResult("게임 설치 완료", pathSaved ? "게임 설치가 완료되었습니다."
                : "게임 설치는 완료되었지만 설치 경로를 저장하지 못했습니다. 설치된 게임의 경로를 다시 지정해 주세요.",
                pathSaved ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
        }
        catch (MaintenanceCanceledException ex)
        {
            if (ex.Reason != MaintenanceCancellationReason.UserCanceled)
            {
                ShowInstallResult("게임 설치 취소", ex.Message);
                return;
            }
            if (ex.CleanupFailed)
            {
                // 진단은 작업 서비스에서 이미 기록했습니다. 동일 오류를 다시 안내/기록하지 않습니다.
                ShowInstallResult("게임 설치 취소", "설치는 취소했지만 임시 파일 일부를 삭제하지 못했습니다. 다음 설치 시 임시 파일이 남아 있을 수 있습니다.",
                    InfoBarSeverity.Warning);
                return;
            }

            ShowInstallResult("게임 설치 취소", _deleteArchiveArtifactsOnCancel
                ? "설치를 취소하고 임시 파일을 삭제했습니다."
                : "설치를 취소했습니다. 다음에 이어받을 수 있도록 임시 파일을 남겨두었습니다.");
        }
        catch (Exception ex)
        {
            ReportInstallFailure(ex, "Install", "게임 설치를 완료하지 못했습니다. 네트워크 상태와 설치 경로를 확인한 후 다시 시도해 주세요.");
        }
        finally
        {
            IsDownloading = false;

            _installCts?.Dispose();
            _installCts = null;
            _deleteArchiveArtifactsOnCancel = false;
        }
    }

    /// <summary>
    /// 진행 중인 설치를 취소할지와 임시 파일 유지 여부를 사용자에게 묻습니다.
    /// </summary>
    private async void Button_InstallStop_Click(object sender, RoutedEventArgs e)
    {
        if (!IsDownloading || _installCts is null)
            return;

        CancellationTokenSource cancellation = _installCts;

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "설치 취소",
            Content = "현재 다운로드 또는 압축 해제가 진행 중입니다.\n취소하면서 임시 파일을 어떻게 처리할까요?",
            PrimaryButtonText = "임시 파일 유지",
            SecondaryButtonText = "임시 파일 삭제",
            CloseButtonText = "계속 설치",
            DefaultButton = ContentDialogButton.Close
        };

        ContentDialogResult result = await dialog.ShowManagedAsync();
        if (result == ContentDialogResult.None || !IsDownloading
            || !ReferenceEquals(_installCts, cancellation))
            return;

        _deleteArchiveArtifactsOnCancel = result == ContentDialogResult.Secondary;
        cancellation.Cancel();
    }

    /// <summary>
    /// 설치 진행 중에는 설정 페이지를 벗어나지 못하도록 막습니다.
    /// </summary>
    public async Task<bool> ConfirmLeaveAsync(LeaveReason reason = LeaveReason.Navigation)
    {
        if (!IsBusy)
        {
            bool canLeave = CurrentInstallPanel.SettingsEditor is { } editor
                ? await editor.ConfirmLeaveAsync(reason)
                : reason != LeaveReason.AppExit || XamlRoot is null || await ExitConfirmationDialog.ShowAsync(XamlRoot);
            if (canLeave) HideInstallResult();
            return canLeave;
        }

        ShowInstallResult(
            reason == LeaveReason.AppExit ? "종료 불가" : "이동 불가",
            "작업이 끝난 뒤 다시 시도해 주세요.", InfoBarSeverity.Warning);
        return false;
    }

    private void HideInstallResult()
    {
        TeachingTip_InstallResult.IsOpen = false;
        TeachingTip_InstallResult.ActionButtonContent = null;
        _installResultPermissionHelp = false;
    }

    private void ShowInstallResult(string title, string message,
        InfoBarSeverity severity = InfoBarSeverity.Informational, bool permissionHelp = false)
    {
        ProgressText = message;
        _installResultPermissionHelp = permissionHelp;
        TeachingTip_InstallResult.Title = title;
        TeachingTip_InstallResult.Subtitle = $"{GameServerHelper.GetServerDisplayName(_selectedGameServer)}\n{message}";
        TeachingTip_InstallResult.ActionButtonContent = permissionHelp ? "해결 방법 확인하기" : null;
        TeachingTip_InstallResult.IconSource = severity is InfoBarSeverity.Error or InfoBarSeverity.Warning
            ? new FontIconSource { FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Glyph = severity == InfoBarSeverity.Error ? "\uEA39" : "\uE7BA" }
            : null;
        TeachingTip_InstallResult.IsOpen = true;
    }

    private void ReportInstallFailure(Exception exception, string operation, string message)
    {
        if (exception is OperationCanceledException)
            exception = new IOException("설치가 예기치 않게 중단되었습니다.", exception);
        App.ExceptionHandler.RecordRecoverable(exception, $"GameInstallSettingPage.{operation}.{_selectedGameServer}");
        bool permissionFailure = PathPermissionFailure.IsPermissionFailure(exception);
        ShowInstallResult("게임 설치 실패", permissionFailure
            ? "설치 경로에 파일을 쓸 권한이 부족합니다. 해결 방법을 확인한 후 다시 시도해 주세요."
            : message, InfoBarSeverity.Error, permissionHelp: permissionFailure);
    }

    private void TeachingTip_InstallResult_ActionButtonClick(TeachingTip sender, object args)
    {
        if (!_installResultPermissionHelp) return;
        TeachingTip_InstallResult.IsOpen = false;
        try
        {
            if (App.CurrentWindow is MainWindow window)
                window.NavigateToWebViewPageByLinkKey(AppLinkKeys.HelpPermissionMultiClient);
        }
        catch (Exception ex)
        {
            App.ExceptionHandler.RecordRecoverable(ex, "GameInstallSettingPage.ResultAction");
            TeachingTip_InstallResult.Subtitle = "해결 방법을 열지 못했습니다. 잠시 후 다시 시도해 주세요.";
            TeachingTip_InstallResult.IsOpen = true;
        }
    }

    private void ShowInstallProgress()
    {
        if (_hasInstallProgress)
            return;

        _hasInstallProgress = true;
        OnPropertyChanged(nameof(InstallProgressVisibility));
    }

    /// <summary>
    /// 새 설치 시작 전에 다운로드/압축 해제 진행 상태를 초기화합니다.
    /// </summary>
    private void ResetProgressState()
    {
        _progressPhase = InstallProgressPhase.Downloading;
        _downloadProgressMaximum = 100;
        _downloadProgressValue = 0;
        _extractProgressMaximum = 100;
        _extractProgressValue = 0;
        OnPropertyChanged(nameof(ProgressMaximum));
        OnPropertyChanged(nameof(ProgressValue));
    }

    /// <summary>
    /// 다운로드 페이즈용 ProgressBar 값을 갱신합니다.
    /// </summary>
    private void SetDownloadProgress(double maximum, double value)
    {
        _progressPhase = InstallProgressPhase.Downloading;
        _downloadProgressMaximum = Math.Max(1, maximum);
        _downloadProgressValue = Math.Clamp(value, 0, _downloadProgressMaximum);
        OnPropertyChanged(nameof(ProgressMaximum));
        OnPropertyChanged(nameof(ProgressValue));
    }

    /// <summary>
    /// 압축 해제 페이즈용 ProgressBar 값을 갱신합니다.
    /// </summary>
    private void SetExtractProgress(double maximum, double value)
    {
        _progressPhase = InstallProgressPhase.Extracting;
        _extractProgressMaximum = Math.Max(1, maximum);
        _extractProgressValue = Math.Clamp(value, 0, _extractProgressMaximum);
        OnPropertyChanged(nameof(ProgressMaximum));
        OnPropertyChanged(nameof(ProgressValue));
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = Math.Max(0, bytes);
        int unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }
}
