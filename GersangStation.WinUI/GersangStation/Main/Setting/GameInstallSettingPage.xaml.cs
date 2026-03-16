using Core;
using Core.Download;
using Core.Models;
using GersangStation.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Windows.Storage.Pickers;

namespace GersangStation.Main.Setting;

/// <summary>
/// 서버별 전체 클라이언트 설치를 다운로드하고 압축 해제하는 설정 페이지입니다.
/// </summary>
public sealed partial class GameInstallSettingPage : Page, INotifyPropertyChanged, IConfirmLeave
{
    private enum InstallProgressPhase
    {
        Downloading,
        Extracting
    }

    private GameServer _selectedGameServer = GameServer.Korea_Live;
    private bool _suppressServerSelectionChanged;
    private string _progressText = string.Empty;
    private bool _isDownloading;
    private bool _hasInstallProgress;
    private CancellationTokenSource? _installCts;
    private bool _deleteArchiveArtifactsOnCancel;
    private bool _isInstallPathValid = true;
    private InstallProgressPhase _progressPhase = InstallProgressPhase.Downloading;
    private double _downloadProgressMaximum = 100;
    private double _downloadProgressValue;
    private double _extractProgressMaximum = 100;
    private double _extractProgressValue;

    public GameInstallSettingPage()
    {
        InitializeComponent();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsNotBusy => !IsBusy;
    public bool IsBusy => IsDownloading;
    public bool IsInstallStartEnabled => IsNotBusy && _isInstallPathValid;
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
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsNotBusy));
                OnPropertyChanged(nameof(IsInstallStartEnabled));
                OnPropertyChanged(nameof(InstallCancelVisibility));
            }
        }
    }

    /// <summary>
    /// 외부에서 전달된 서버 파라미터를 반영하고 현재 저장된 설치 루트를 불러옵니다.
    /// </summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        GameServer initialServer = e.Parameter switch
        {
            GameServerSettingNavigationParameter parameter => parameter.Server,
            GameServer server => server,
            _ => GameServer.Korea_Live
        };

        ApplySelectedServer(initialServer);
    }

    protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);

        e.Cancel = !await ConfirmLeaveAsync();
    }

    /// <summary>
    /// 선택한 서버에 맞게 입력값과 설치 대상 경로를 동기화합니다.
    /// </summary>
    private void ApplySelectedServer(GameServer server)
    {
        _selectedGameServer = server;
        SelectorBarItem selectedItem = GetSelectorBarItem(server);

        _suppressServerSelectionChanged = true;
        SelectorBar_Server.SelectedItem = selectedItem;
        UpdateSelectorBarVisualState(SelectorBar_Server, selectedItem);
        _suppressServerSelectionChanged = false;

        LoadInstallBasePath(server);
        UpdateComputedInstallPath();
    }

    private void SelectorBar_Server_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem is not SelectorBarItem selectedItem)
            return;

        UpdateSelectorBarVisualState(sender, selectedItem);

        if (_suppressServerSelectionChanged)
            return;

        GameServer server = sender.Items.IndexOf(selectedItem) switch
        {
            0 => GameServer.Korea_Live,
            1 => GameServer.Korea_Test,
            2 => GameServer.Korea_RnD,
            _ => throw new ArgumentOutOfRangeException(nameof(sender), sender, null)
        };

        if (_selectedGameServer == server)
            return;

        ApplySelectedServer(server);
    }

    /// <summary>
    /// 게임 설치 페이지의 상위 설치 경로 입력값을 기본 경로로 초기화합니다.
    /// </summary>
    private void LoadInstallBasePath(GameServer server)
    {
        TextBox_InstallPath.Text = @"C:\";
    }

    /// <summary>
    /// 현재 입력된 루트 경로와 서버 조합으로 최종 설치 경로 텍스트와 유효성 표시를 갱신합니다.
    /// </summary>
    private void UpdateComputedInstallPath()
    {
        string finalInstallPath = BuildFinalInstallPath(TextBox_InstallPath.Text, _selectedGameServer);
        TextBlock_InstallPath.Text = $"최종 설치 경로: {finalInstallPath}";

        _isInstallPathValid = !string.IsNullOrWhiteSpace(finalInstallPath) && IsInstallTargetPathAvailable(finalInstallPath);
        TextBox_InstallPath.IsValid = _isInstallPathValid;
        TextBox_InstallPath.ErrorText = _isInstallPathValid
            ? string.Empty
            : "이미 존재하는 경로입니다.";

        OnPropertyChanged(nameof(IsInstallStartEnabled));
    }

    /// <summary>
    /// 설치 대상 폴더가 비어 있거나 이어받기용 다운로드 산출물만 있을 때 설치 가능으로 판단합니다.
    /// </summary>
    private bool IsInstallTargetPathAvailable(string finalInstallPath)
    {
        if (!Directory.Exists(finalInstallPath))
            return true;

        string archivePath = GameInstallManager.GetArchivePath(_selectedGameServer, finalInstallPath);
        HashSet<string> allowedFiles =
        [
            Path.GetFullPath($"{archivePath}.gsdownload"),
            Path.GetFullPath($"{archivePath}.meta")
        ];

        string[] directories = Directory.GetDirectories(finalInstallPath, "*", SearchOption.TopDirectoryOnly);
        if (directories.Length > 0)
            return false;

        string[] files = Directory.GetFiles(finalInstallPath, "*", SearchOption.TopDirectoryOnly);
        if (files.Length == 0)
            return true;

        foreach (string file in files)
        {
            if (!allowedFiles.Contains(Path.GetFullPath(file)))
                return false;
        }

        return true;
    }

    /// <summary>
    /// 사용자가 선택한 루트 경로를 서버별 실제 거상 설치 경로로 변환합니다.
    /// </summary>
    private static string BuildFinalInstallPath(string? baseRoot, GameServer server)
    {
        string trimmed = (baseRoot ?? string.Empty)
            .Trim()
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return string.IsNullOrWhiteSpace(trimmed)
            ? string.Empty
            : Path.Combine(trimmed, "AKInteractive", GameServerHelper.GetClientFolderName(server));
    }

    /// <summary>
    /// 저장된 최종 설치 경로에서 사용자가 입력해야 할 상위 루트 경로만 추출합니다.
    /// </summary>
    private static string? TryGetBaseInstallRoot(string? installPath, GameServer server)
    {
        if (string.IsNullOrWhiteSpace(installPath))
            return null;

        string normalizedPath = Path.GetFullPath(installPath.Trim())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        string expectedSuffix = Path.Combine("AKInteractive", GameServerHelper.GetClientFolderName(server));
        if (!normalizedPath.EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase))
            return normalizedPath;

        return normalizedPath[..^expectedSuffix.Length]
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// 서버 값에 대응하는 SelectorBarItem을 반환합니다.
    /// </summary>
    private SelectorBarItem GetSelectorBarItem(GameServer server)
        => server switch
        {
            GameServer.Korea_Live => SelectorBarItem_Live,
            GameServer.Korea_Test => SelectorBarItem_Test,
            GameServer.Korea_RnD => SelectorBarItem_RnD,
            _ => throw new ArgumentOutOfRangeException(nameof(server), server, null),
        };

    /// <summary>
    /// 현재 선택된 서버 항목만 강조되도록 SelectorBar 시각 상태를 갱신합니다.
    /// </summary>
    private static void UpdateSelectorBarVisualState(SelectorBar selectorBar, SelectorBarItem selectedItem)
    {
        foreach (SelectorBarItem item in selectorBar.Items.OfType<SelectorBarItem>())
        {
            item.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
            item.FontSize = 14;
        }

        selectedItem.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        selectedItem.FontSize = 20;
    }

    private void TextBox_InstallPath_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateComputedInstallPath();
    }

    /// <summary>
    /// 설치 상위 폴더를 고를 수 있도록 시스템 폴더 선택기를 엽니다.
    /// </summary>
    private async void Button_InstallPath_PickFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || IsBusy)
            return;

        button.IsEnabled = false;
        try
        {
            FolderPicker picker = new(button.XamlRoot.ContentIslandEnvironment.AppWindowId)
            {
                CommitButtonText = "선택",
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.List
            };

            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null)
                TextBox_InstallPath.Text = folder.Path;
        }
        finally
        {
            button.IsEnabled = !IsBusy;
        }
    }

    /// <summary>
    /// 다운로드와 압축 해제를 시작하고 성공 시 서버별 설치 경로를 저장합니다.
    /// </summary>
    private async void Button_Install_Click(object sender, RoutedEventArgs e)
    {
        if (IsBusy || !_isInstallPathValid)
            return;

        string finalInstallPath = BuildFinalInstallPath(TextBox_InstallPath.Text, _selectedGameServer);
        if (string.IsNullOrWhiteSpace(finalInstallPath))
        {
            await ShowDialogAsync("설치 경로 필요", "게임을 설치할 상위 경로를 먼저 선택해 주세요.");
            return;
        }

        if (!IsInstallTargetPathAvailable(finalInstallPath))
        {
            UpdateComputedInstallPath();
            await ShowDialogAsync("설치 경로 확인", "최종 설치 경로에 이미 폴더가 존재합니다. 다른 경로를 선택해 주세요.");
            return;
        }

        DirectoryWriteProbeResult writeProbeResult = PathWriteProbe.TryProbeDirectoryWriteAccess(finalInstallPath);
        if (!await PathPermissionDialog.ConfirmContinueWhenPermissionMissingAsync(XamlRoot, writeProbeResult))
            return;

        GameInstallManager installManager = new();

        _installCts?.Dispose();
        _installCts = new CancellationTokenSource();
        _deleteArchiveArtifactsOnCancel = false;

        try
        {
            IsDownloading = true;
            ShowInstallProgress();

            ResetProgressState();
            ProgressText = "설치를 준비하는 중...";

            Progress<GameInstallProgress> installProgress = new(p =>
            {
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

            await installManager.RunAsync(
                _selectedGameServer,
                finalInstallPath,
                DownloadExistingArtifactMode.ResumeIfPossible,
                installProgress,
                _installCts.Token);

            (ClientSettings settings, AppDataManager.AppDataOperationResult loadResult) =
                await AppDataManager.LoadServerClientSettingsAsync(_selectedGameServer);

            if (!loadResult.Success)
            {
                await AppDataOperationDialog.ShowFailureAsync(
                    XamlRoot,
                    "설정 불러오기 실패",
                    "설치 완료 후 서버 설정을 갱신하기 전에 현재 설정을 불러오지 못했습니다.",
                    loadResult);
            }

            settings.InstallPath = finalInstallPath;
            AppDataManager.AppDataOperationResult saveResult =
                await AppDataManager.SaveServerClientSettingsAsync(_selectedGameServer, settings);

            if (!saveResult.Success)
            {
                await AppDataOperationDialog.ShowFailureAsync(
                    XamlRoot,
                    "설정 저장 실패",
                    "게임 설치는 완료되었지만 설치 경로 설정을 저장하지 못했습니다.",
                    saveResult);
            }

            SetExtractProgress(_extractProgressMaximum, _extractProgressMaximum);
            ProgressText = "게임 설치가 완료되었습니다.";
        }
        catch (OperationCanceledException)
        {
            if (_deleteArchiveArtifactsOnCancel)
            {
                try
                {
                    GameInstallManager.DeleteArchiveArtifacts(_selectedGameServer, finalInstallPath);
                }
                catch (Exception ex)
                {
                    ProgressText = $"설치를 취소했지만 임시 파일 정리에 실패했습니다.\n{ex.Message}";
                    return;
                }
            }

            ProgressText = _deleteArchiveArtifactsOnCancel
                ? "설치를 취소하고 임시 파일을 삭제했습니다."
                : "설치를 취소했습니다. 다음에 이어받을 수 있도록 임시 파일을 남겨두었습니다.";
        }
        catch (Exception ex)
        {
            if (await PathPermissionDialog.ShowFailureGuidanceWhenPermissionMissingAsync(XamlRoot, ex, "게임 설치"))
            {
                ProgressText = "설치에 실패했습니다.\n권한 문제 해결 방법을 확인해 주세요.";
                return;
            }

            ProgressText = $"설치에 실패했습니다.\n{ex.Message}";
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

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.None)
            return;

        _deleteArchiveArtifactsOnCancel = result == ContentDialogResult.Secondary;
        _installCts.Cancel();
    }

    /// <summary>
    /// 설치 진행 중에는 설정 페이지를 벗어나지 못하도록 막습니다.
    /// </summary>
    public async Task<bool> ConfirmLeaveAsync(LeaveReason reason = LeaveReason.Navigation)
    {
        if (!IsBusy)
        {
            if (reason == LeaveReason.AppExit && XamlRoot is not null)
                return await ExitConfirmationDialog.ShowAsync(XamlRoot);

            return true;
        }

        await ShowDialogAsync(
            reason == LeaveReason.AppExit ? "종료 불가" : "이동 불가",
            reason == LeaveReason.AppExit
                ? "작업 진행 중에는 프로그램을 종료할 수 없습니다."
                : "작업 진행 중 페이지를 이동할 수 없습니다.");
        return false;
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

    private async Task ShowDialogAsync(string title, string content)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = "확인",
            DefaultButton = ContentDialogButton.Primary
        };

        await dialog.ShowAsync();
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
