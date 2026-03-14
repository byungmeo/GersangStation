using Core;
using Core.Models;
using GersangStation.Diagnostics;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace GersangStation.Main.Setting;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class GamePatchSettingPage : Page, INotifyPropertyChanged, IConfirmLeave
{
    #region Properties
    private const int ManualUpgradeBoundaryVersion = 34100;
    private const string VersionWarningLinkPlaceholderText = "참고 링크";
    private const string DeprecatedVersionInfoUrl = "https://github.com/byungmeo/GersangStation/wiki/v34100-%EB%AF%B8%EB%A7%8C-%EB%B2%84%EC%A0%84-%EA%B4%80%EB%A0%A8";

    private GameServer _selectedGameServer = GameServer.Korea_Live;
    private string _displayLatestVersion = string.Empty;
    private bool _shouldPatchMultiClient = true;
    private bool _shouldOverwriteMultiClientConfig = false;
    private bool _shouldDeleteTemp = true;
    private string _tempPath = string.Empty;
    private double _progressMaximum = 100;
    private double _progressValue = 0;
    private string _progressText = string.Empty;
    private bool _isLoadingPatchInfo = false;
    private bool _isDownloadingPatch = false;
    private bool _hasPatchProgress = false;
    private PatchReadmeInfoItem? _selectedVersionItem;
    private CancellationTokenSource? _patchCts;
    private CancellationTokenSource? _patchInfoLoadCts;
    private int? _currentClientVersion;
    private int? _latestServerVersion;
    private string _currentInstallPath = string.Empty;
    private string _currentVersionStatusMessage = string.Empty;
    private string _latestVersionStatusMessage = string.Empty;
    private bool _suppressServerSelectionChanged;
    private bool _suppressClientSettingsPersistence;

    public ObservableCollection<PatchReadmeInfoItem> Versions { get; } = [];

    public bool IsNotBusy => !IsBusy;
    public bool IsOverwriteMultiClientConfigEnabled => IsNotBusy && ShouldPatchMultiClient;
    public bool IsPatchStartEnabled => IsNotBusy && CanStartPatch;
    public string VersionWarningMessage => GetVersionWarningMessage();
    public Visibility VersionWarningVisibility
        => IsLoadingPatchInfo || string.IsNullOrWhiteSpace(VersionWarningMessage)
            ? Visibility.Collapsed
            : Visibility.Visible;
    public string VersionWarningLinkText => RequiresManualUpgradeLink()
        ? VersionWarningLinkPlaceholderText
        : string.Empty;
    public Visibility VersionWarningLinkVisibility => !IsLoadingPatchInfo && RequiresManualUpgradeLink()
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility DownloadingVisibility => IsDownloading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NotBusyVisibility => IsBusy ? Visibility.Collapsed : Visibility.Visible;
    public Visibility LoadingOverlayVisibility => IsLoadingPatchInfo ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PatchProgressVisibility => _hasPatchProgress ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PatchCancelVisibility => IsDownloading ? Visibility.Visible : Visibility.Collapsed;
    public bool IsBusy => _isLoadingPatchInfo || _isDownloadingPatch;

    public string DisplayLatestVersion
    {
        get => _displayLatestVersion;
        set => SetProperty(ref _displayLatestVersion, value);
    }
    public bool ShouldPatchMultiClient
    {
        get => _shouldPatchMultiClient;
        set
        {
            if (!SetProperty(ref _shouldPatchMultiClient, value))
                return;

            OnPropertyChanged(nameof(IsOverwriteMultiClientConfigEnabled));
        }
    }
    public bool ShouldOverwriteMultiClientConfig
    {
        get => _shouldOverwriteMultiClientConfig;
        set
        {
            if (!SetProperty(ref _shouldOverwriteMultiClientConfig, value))
                return;

            PersistOverwriteMultiClientConfigSetting();
        }
    }
    public bool ShouldDeleteTemp
    {
        get => _shouldDeleteTemp;
        set => SetProperty(ref _shouldDeleteTemp, value);
    }
    public string TempPath
    {
        get => _tempPath;
        set => SetProperty(ref _tempPath, value);
    }
    public double ProgressMaximum
    {
        get => _progressMaximum;
        set => SetProperty(ref _progressMaximum, value);
    }
    public double ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }
    public string ProgressText
    {
        get => _progressText;
        set => SetProperty(ref _progressText, value);
    }
    public bool IsLoadingPatchInfo
    {
        get => _isLoadingPatchInfo;
        set
        {
            if (SetProperty(ref _isLoadingPatchInfo, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsNotBusy));
                OnPropertyChanged(nameof(IsOverwriteMultiClientConfigEnabled));
                OnPropertyChanged(nameof(IsPatchStartEnabled));
                OnPropertyChanged(nameof(LoadingOverlayVisibility));
                OnPropertyChanged(nameof(NotBusyVisibility));
                OnPropertyChanged(nameof(VersionWarningVisibility));
                OnPropertyChanged(nameof(VersionWarningLinkVisibility));
            }
        }
    }
    public bool IsDownloading
    {
        get => _isDownloadingPatch;
        set
        {
            if (SetProperty(ref _isDownloadingPatch, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsNotBusy));
                OnPropertyChanged(nameof(IsOverwriteMultiClientConfigEnabled));
                OnPropertyChanged(nameof(IsPatchStartEnabled));
                OnPropertyChanged(nameof(DownloadingVisibility));
                OnPropertyChanged(nameof(NotBusyVisibility));
                OnPropertyChanged(nameof(PatchCancelVisibility));
            }
        }
    }
    public PatchReadmeInfoItem? SelectedVersionItem
    {
        get => _selectedVersionItem;
        set
        {
            if (SetProperty(ref _selectedVersionItem, value))
                OnPropertyChanged(nameof(IsPatchStartEnabled));
        }
    }
    #endregion Properties

    public GamePatchSettingPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is GamePatchSettingNavigationParameter parameter)
            ApplySelectedServer(parameter.Server);
        else
            ApplySelectedServer(_selectedGameServer);

        await LoadPatchInfoAsync();
    }

    protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);

        e.Cancel = !await ConfirmLeaveAsync();
        if (!e.Cancel)
        {
            CancelPatchInfoLoad();
            Versions.Clear();
            RichTextBlock_PatchReadme.Blocks.Clear();
        }
    }

    private async Task LoadPatchInfoAsync()
    {
        CancellationTokenSource currentLoadCts = new();
        CancellationTokenSource? previousLoadCts = _patchInfoLoadCts;
        _patchInfoLoadCts = currentLoadCts;
        previousLoadCts?.Cancel();
        previousLoadCts?.Dispose();

        var startAt = DateTime.UtcNow;
        IsLoadingPatchInfo = true;
        try
        {
            (ClientSettings clientSetting, AppDataManager.AppDataOperationResult loadResult) =
                await AppDataManager.LoadServerClientSettingsAsync(_selectedGameServer);

            if (!loadResult.Success)
            {
                await AppDataOperationDialog.ShowFailureAsync(
                    XamlRoot,
                    "설정 불러오기 실패",
                    "패치 설정을 준비하는 동안 서버 설정을 모두 불러오지 못했습니다.",
                    loadResult);
            }

            LoadPatchOptions(clientSetting);
            _currentInstallPath = clientSetting.InstallPath;
            ApplyCurrentVersionState(clientSetting.InstallPath);
            _latestServerVersion = null;
            _latestVersionStatusMessage = string.Empty;
            DisplayLatestVersion = string.Empty;

            SelectedVersionItem = null;
            Versions.Clear();
            RichTextBlock_PatchReadme.Blocks.Clear();
            NotifyPatchStateChanged();

            List<PatchReadmeInfoItem> items;
            try
            {
                items = await PatchReadmeHelper.GetPatchInfoList(_selectedGameServer, currentLoadCts.Token);
            }
            catch (OperationCanceledException) when (currentLoadCts.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GamePatchSettingPage] Failed to load patch info. Server: {_selectedGameServer}, Error: {ex}");
                _latestServerVersion = null;
                _latestVersionStatusMessage = "최신 버전을 확인할 수 없습니다. 지금은 패치를 진행할 수 없습니다.";
                DisplayLatestVersion = "최신 버전 확인 불가";
                NotifyPatchStateChanged();
                return;
            }

            currentLoadCts.Token.ThrowIfCancellationRequested();

            PatchReadmeInfoItem? latestPatchInfo = items.FirstOrDefault();
            if (latestPatchInfo is null)
            {
                _latestServerVersion = null;
                _latestVersionStatusMessage = "최신 버전을 확인할 수 없습니다. 지금은 패치를 진행할 수 없습니다.";
                DisplayLatestVersion = "최신 버전 확인 불가";
                NotifyPatchStateChanged();
                return;
            }

            _latestServerVersion = latestPatchInfo.Version;
            _latestVersionStatusMessage = string.Empty;
            DisplayLatestVersion = latestPatchInfo.Display;

            Brush headerBrush = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"];
            Brush contentBrush = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
            foreach (PatchReadmeInfoItem item in items)
            {
                if ((_currentClientVersion ?? -1) >= item.Version)
                    Versions.Add(item);

                bool shouldHighlightHeader = item.Version > (_currentClientVersion ?? 0);
                string headerText = $"[{item.Date:yyyy-MM-dd} 거상 업데이트 v{item.Version}]";

                Run headerRun = new()
                {
                    Text = headerText,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 18
                };

                Paragraph headerParagraph = new();
                if (shouldHighlightHeader)
                {
                    headerParagraph.Inlines.Add(new InlineUIContainer
                    {
                        Child = new FontIcon
                        {
                            Glyph = "\uE896"
                        }
                    });
                    headerRun.Foreground = headerBrush;
                }
                headerParagraph.Inlines.Add(headerRun);
                RichTextBlock_PatchReadme.Blocks.Add(headerParagraph);

                for (int i = 0; i < item.Details.Count; i++)
                {
                    Paragraph detailParagraph = new();
                    detailParagraph.Inlines.Add(new Run { Text = item.Details[i], FontSize = 12, Foreground = contentBrush });
                    if (i == item.Details.Count - 1)
                        detailParagraph.Inlines.Add(new LineBreak());
                    RichTextBlock_PatchReadme.Blocks.Add(detailParagraph);
                }
            }
            SelectedVersionItem = Versions.FirstOrDefault();
            NotifyPatchStateChanged();
        }
        catch (OperationCanceledException) when (currentLoadCts.IsCancellationRequested)
        {
        }
        finally
        {
            // TODO: [#104] 임시조치
            TimeSpan elapsed = DateTime.UtcNow - startAt;
            TimeSpan minDuration = TimeSpan.FromMilliseconds(500);

            if (!currentLoadCts.IsCancellationRequested && elapsed < minDuration)
                await Task.Delay(minDuration - elapsed);

            if (ReferenceEquals(_patchInfoLoadCts, currentLoadCts))
            {
                _patchInfoLoadCts = null;
                IsLoadingPatchInfo = false;
            }

            currentLoadCts.Dispose();
        }
    }

    /// <summary>
    /// 현재 선택된 서버의 다클라 설정 덮어쓰기 옵션을 페이지 상태로 반영합니다.
    /// </summary>
    private void LoadPatchOptions(ClientSettings clientSetting)
    {
        _suppressClientSettingsPersistence = true;
        try
        {
            ShouldOverwriteMultiClientConfig = clientSetting.OverwriteMultiClientConfig;
        }
        finally
        {
            _suppressClientSettingsPersistence = false;
        }
    }

    /// <summary>
    /// 현재 설치 경로에서 클라이언트 버전을 읽고, 읽기 실패 시 경로 재설정 유도 메시지를 준비합니다.
    /// </summary>
    private void ApplyCurrentVersionState(string installPath)
    {
        ClientVersionReadResult currentVersionResult = PatchManager.TryGetCurrentClientVersion(installPath);
        if (!currentVersionResult.Success || currentVersionResult.Version is null or <= 0)
        {
            _currentClientVersion = null;
            _currentVersionStatusMessage = "현재 버전을 확인할 수 없습니다. 설치 경로를 다시 설정해주세요.";
            Debug.WriteLine(
                $"[GamePatchSettingPage] Failed to read current version. Server: {_selectedGameServer}, InstallPath: '{installPath}', Result: {currentVersionResult}");
            return;
        }

        _currentClientVersion = currentVersionResult.Version.Value;
        _currentVersionStatusMessage = string.Empty;
    }

    /// <summary>
    /// 다클라 설정 파일 덮어쓰기 옵션을 현재 서버 설정에 즉시 반영합니다.
    /// </summary>
    private async void PersistOverwriteMultiClientConfigSetting()
    {
        if (_suppressClientSettingsPersistence)
            return;

        (ClientSettings clientSetting, AppDataManager.AppDataOperationResult loadResult) =
            await AppDataManager.LoadServerClientSettingsAsync(_selectedGameServer);

        if (!loadResult.Success)
        {
            await AppDataOperationDialog.ShowFailureAsync(
                XamlRoot,
                "설정 불러오기 실패",
                "다클라 덮어쓰기 옵션을 저장하기 전에 현재 서버 설정을 불러오지 못했습니다.",
                loadResult);
        }

        if (clientSetting.OverwriteMultiClientConfig == ShouldOverwriteMultiClientConfig)
            return;

        clientSetting.OverwriteMultiClientConfig = ShouldOverwriteMultiClientConfig;
        AppDataManager.AppDataOperationResult saveResult =
            await AppDataManager.SaveServerClientSettingsAsync(_selectedGameServer, clientSetting);

        if (!saveResult.Success)
        {
            await AppDataOperationDialog.ShowFailureAsync(
                XamlRoot,
                "설정 저장 실패",
                "다클라 덮어쓰기 옵션을 저장하지 못했습니다.",
                saveResult);
        }
    }

    /// <summary>
    /// 진행 중인 패치 정보 로드를 취소합니다.
    /// </summary>
    private void CancelPatchInfoLoad()
    {
        _patchInfoLoadCts?.Cancel();
        _patchInfoLoadCts?.Dispose();
        _patchInfoLoadCts = null;
        IsLoadingPatchInfo = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void ShowPatchProgress()
    {
        if (_hasPatchProgress)
            return;

        _hasPatchProgress = true;
        OnPropertyChanged(nameof(PatchProgressVisibility));
    }

    /// <summary>
    /// 현재 클라이언트 버전을 확인할 수 있고, 그 버전이 패치 이력 목록에 있을 때만 패치를 허용합니다.
    /// </summary>
    private bool CanStartPatch
        => _currentClientVersion is int currentVersion
            && _latestServerVersion is int
            && Versions.Any(item => item.Version == currentVersion)
            && !RequiresManualUpgradeLink()
            && SelectedVersionItem is not null;

    /// <summary>
    /// 패치 가능 여부와 현재 버전 경고 표시 상태를 함께 갱신합니다.
    /// </summary>
    private void NotifyPatchStateChanged()
    {
        OnPropertyChanged(nameof(IsPatchStartEnabled));
        OnPropertyChanged(nameof(VersionWarningMessage));
        OnPropertyChanged(nameof(VersionWarningVisibility));
        OnPropertyChanged(nameof(VersionWarningLinkText));
        OnPropertyChanged(nameof(VersionWarningLinkVisibility));
    }

    /// <summary>
    /// 현재 버전 상태에 맞는 안내 문구를 반환합니다.
    /// </summary>
    private string GetVersionWarningMessage()
    {
        if (!string.IsNullOrWhiteSpace(_currentVersionStatusMessage))
            return _currentVersionStatusMessage;

        if (!string.IsNullOrWhiteSpace(_latestVersionStatusMessage))
            return _latestVersionStatusMessage;

        if (RequiresManualUpgradeLink())
            return "v34100 이전 버전 클라이언트입니다. 삭제 후 재설치 하세요.";

        if (_currentClientVersion is not int currentVersion)
            return "현재 버전을 확인할 수 없습니다. 설치 경로를 다시 설정해주세요.";

        return Versions.Any(item => item.Version == currentVersion)
            ? string.Empty
            : "클라이언트가 너무 오래되었습니다. 삭제 후 재설치 하세요.";
    }

    /// <summary>
    /// 현재 버전과 최신 버전이 경계 버전을 사이에 두고 갈라져 별도 안내 링크가 필요한지 판단합니다.
    /// </summary>
    private bool RequiresManualUpgradeLink()
    {
        if (_currentClientVersion is not int currentVersion)
            return false;

        if (_latestServerVersion is not int latestVersion)
            return false;

        return latestVersion >= ManualUpgradeBoundaryVersion
            && currentVersion < ManualUpgradeBoundaryVersion;
    }

    private async Task<PatchArchiveReuseMode> AskArchiveReuseModeAsync(string tempRoot)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "이전 임시 파일 발견",
            Content =
                $"이전 패치 임시 파일이 존재합니다.\n\n" +
                $"{tempRoot}\n\n" +
                $"이어받기를 진행할까요?",
            PrimaryButtonText = "이어받기",
            SecondaryButtonText = "처음부터",
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowAsync();

        return result == ContentDialogResult.Primary
            ? PatchArchiveReuseMode.ResumeIfPossible
            : PatchArchiveReuseMode.RestartFromScratch;
    }

    /// <summary>
    /// 선택한 버전 기준으로 메인 클라이언트 패치를 실행하고 옵션에 따라 다클라 재구성까지 이어갑니다.
    /// </summary>
    private async void Button_PatchStart_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (!IsPatchStartEnabled)
            return;

        if (SelectedVersionItem is null)
            return;

        PatchManager patchManager = new();
        (ClientSettings clientSetting, AppDataManager.AppDataOperationResult loadResult) =
            await AppDataManager.LoadServerClientSettingsAsync(_selectedGameServer);

        if (!loadResult.Success)
        {
            await AppDataOperationDialog.ShowFailureAsync(
                XamlRoot,
                "설정 불러오기 실패",
                "패치를 시작하기 전에 서버 설정을 모두 불러오지 못했습니다.",
                loadResult);
        }

        if (_latestServerVersion is not int latestClientVersion)
        {
            await ShowSimpleDialogAsync(
                "패치 불가",
                "최신 버전을 확인할 수 없어 지금은 패치를 진행할 수 없습니다.");
            return;
        }

        string tempRoot = PatchManager.GetPatchTempRoot(
            clientSetting.InstallPath,
            SelectedVersionItem.Version,
            latestClientVersion);

        TempPath = $"임시 파일 경로: {tempRoot}";

        PatchArchiveReuseMode archiveReuseMode = PatchArchiveReuseMode.ResumeIfPossible;
        if (Directory.Exists(tempRoot))
        {
            archiveReuseMode = await AskArchiveReuseModeAsync(tempRoot);
        }

        _patchCts = new CancellationTokenSource();

        try
        {
            IsDownloading = true;
            ShowPatchProgress();

            ProgressMaximum = 1;
            ProgressValue = 0;
            ProgressText = "패치를 준비하는 중...";

            Progress<PatchProgress> patchProgress = new(p =>
            {
                int completedCount = p.DownloadedCount + p.ExtractedCount;
                int phaseTotalCount = p.TotalCount / 2;

                double downloadingPercent = p.DownloadingPercent ?? 0;
                double extractingPercent = p.ExtractingPercent ?? 0;

                string downloadingFileName = string.IsNullOrWhiteSpace(p.DownloadingFileName)
                    ? "-"
                    : p.DownloadingFileName;

                string extractingFileName = string.IsNullOrWhiteSpace(p.ExtractingFileName)
                    ? "-"
                    : p.ExtractingFileName;

                ProgressMaximum = p.TotalCount;
                ProgressValue = completedCount;

                ProgressText =
                    $"다운로드: {p.DownloadedCount} / {phaseTotalCount}  {downloadingFileName}  {downloadingPercent:F1}%\n" +
                    $"압축 해제: {p.ExtractedCount} / {phaseTotalCount}  {extractingFileName}  {extractingPercent:F1}%";
            });

            PatchRunOptions option = new(
                DeleteTempFilesAfterPatch: ShouldDeleteTemp,
                ArchiveReuseMode: archiveReuseMode,
                ApplyMultiClientPatch: ShouldPatchMultiClient);

            await patchManager.RunAsync(
                _selectedGameServer,
                SelectedVersionItem.Version,
                clientSetting.InstallPath,
                option,
                patchProgress,
                _patchCts.Token);

            ProgressValue = ProgressMaximum;
            ProgressText = "패치가 완료되었습니다.";
        }
        catch (OperationCanceledException)
        {
            ProgressText = "패치가 취소되었습니다.";
        }
        catch (Exception ex)
        {
            ProgressText = $"패치에 실패했습니다.\n{ex.Message}";
        }
        finally
        {
            IsDownloading = false;

            _patchCts?.Dispose();
            _patchCts = null;
        }
    }

    private void Button_PatchStop_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (!IsDownloading)
            return;

        _patchCts?.Cancel();
    }

    private async void Button_Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadPatchInfoAsync();
    }

    /// <summary>
    /// 패치 작업 상태에 따라 페이지 이탈 또는 앱 종료 가능 여부를 판단합니다.
    /// </summary>
    public async Task<bool> ConfirmLeaveAsync(LeaveReason reason = LeaveReason.Navigation)
    {
        if (IsBusy)
        {
            ContentDialog contentDialog = new()
            {
                XamlRoot = XamlRoot,
                Title = reason == LeaveReason.AppExit ? "종료 불가" : "이동 불가",
                Content = reason == LeaveReason.AppExit
                    ? "작업 진행 중에는 프로그램을 종료할 수 없습니다."
                    : "작업 진행 중 페이지를 이동할 수 없습니다.",
                IsPrimaryButtonEnabled = true,
                PrimaryButtonText = "확인",
                DefaultButton = ContentDialogButton.Primary
            };
            await contentDialog.ShowAsync();
            return false;
        }

        if (reason == LeaveReason.AppExit && XamlRoot is not null)
            return await ExitConfirmationDialog.ShowAsync(XamlRoot);

        return true;
    }

    private void ComboBox_CurrentVersion_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_latestServerVersion is not int latestClientVersion)
        {
            TempPath = "임시 파일 경로: 최신 버전을 확인할 수 없습니다.";
            return;
        }

        var selectedPatchItem = ComboBox_CurrentVersion.SelectedItem as PatchReadmeInfoItem;
        int selectedCurrentClientVersion = selectedPatchItem?.Version ?? 0;
        TempPath = $"임시 파일 경로: {PatchManager.GetPatchTempRoot(_currentInstallPath, selectedCurrentClientVersion, latestClientVersion)}";
    }

    /// <summary>
    /// 페이지에서 사용하는 단순 안내 대화상자를 표시합니다.
    /// </summary>
    private async Task ShowSimpleDialogAsync(string title, string content)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = content,
            CloseButtonText = "확인",
            DefaultButton = ContentDialogButton.Close
        };

        await dialog.ShowAsync();
    }

    #region SelectorBar
    private async void SelectorBar_Server_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        SelectorBarItem? selectedItem = sender.SelectedItem;
        UpdateSelectorBarVisualState(sender, selectedItem);

        if (_suppressServerSelectionChanged)
            return;

        if (selectedItem.Tag is not GameServer selectedGameServer)
            return;

        if (_selectedGameServer == selectedGameServer)
            return;

        _selectedGameServer = selectedGameServer;
        await LoadPatchInfoAsync();
    }

    /// <summary>
    /// 외부에서 지정한 서버를 SelectorBar 선택 상태와 내부 서버 상태에 함께 반영합니다.
    /// </summary>
    private void ApplySelectedServer(GameServer server)
    {
        _selectedGameServer = server;

        _suppressServerSelectionChanged = true;
        SelectorBar_Server.SelectedItem = GetSelectorBarItem(server);
        UpdateSelectorBarVisualState(SelectorBar_Server, SelectorBar_Server.SelectedItem);
        _suppressServerSelectionChanged = false;
    }

    /// <summary>
    /// 서버 열거형에 대응하는 SelectorBarItem 인스턴스를 반환합니다.
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
    /// SelectorBar의 모든 항목 스타일을 초기화하고 선택 항목만 강조합니다.
    /// </summary>
    private static void UpdateSelectorBarVisualState(SelectorBar selectorBar, SelectorBarItem? selectedItem)
    {
        foreach (SelectorBarItem item in selectorBar.Items.OfType<SelectorBarItem>())
        {
            item.FontWeight = FontWeights.Normal;
            item.FontSize = 14;
        }

        if (selectedItem is null)
            return;

        selectedItem.FontWeight = FontWeights.SemiBold;
        selectedItem.FontSize = 20;
    }
    #endregion SelectorBar

    private void HyperlinkButton_DeprecatedVersionInfo_Click(object sender, RoutedEventArgs e)
    {
        if (App.CurrentWindow is MainWindow mainWindow)
            mainWindow.NavigateToWebViewPage(DeprecatedVersionInfoUrl);
    }
}
