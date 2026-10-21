using CommunityToolkit.WinUI.Controls;
using Core;
using Core.Models;
using GersangStation.Diagnostics;
using GersangStation.Services;
using GameMaintenance;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SideDrawer.Controls;
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
    private const int InitialPatchReadmeRenderCount = 12;
    private const int PatchReadmeRenderBatchCount = 8;
    private const double PatchReadmeRenderThreshold = 240d;

    private GameServer _selectedGameServer = GameServer.Korea_Live;
    private string _displayLatestVersion = string.Empty;
    private bool _shouldDeleteTemp = true;
    private string _tempPath = string.Empty;
    private double _progressMaximum = 100;
    private double _progressValue = 0;
    private Brush? _patchProgressForeground;
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
    private LastPatchRecord? _lastPatchRecord;
    private bool _patchHistoryUnavailable;
    private bool _suppressServerSelectionChanged;
    private List<PatchReadmeInfoItem> _allPatchReadmeItems = [];
    private int _nextPatchReadmeRenderIndex;
    private bool _isRenderingPatchReadmeBatch;
    private bool _isPatchReadmeScrollResetPending;
    private bool _hasRequestedPatchReadmeScrollReset;
    private string? _patchResultExplorerPath;
    private bool _patchResultPermissionHelp;

    public ObservableCollection<PatchReadmeInfoItem> Versions { get; } = [];

    public bool IsNotBusy => !IsBusy;
    public bool CanOpenPatchReadme => !IsLoadingPatchInfo && _allPatchReadmeItems.Count > 0;
    public string PatchReadmeTitle => $"{GameServerHelper.GetServerDisplayName(_selectedGameServer)} 패치 내역";
    public static string ServerSelectionGlyph(bool isSelected) => isSelected ? "\uE73A" : "\uE739";
    public bool IsPatchStartEnabled => IsNotBusy && CanStartPatch;
    public string LastPatchDescription => _lastPatchRecord is { } record
        ? $"기준 버전 v{record.FromVersion} → 패치 버전 v{record.ToVersion}\n당시 기준으로 다시 패치를 해서 문제를 해결할 수 있습니다. (패치가 문제였던 경우)"
        : _patchHistoryUnavailable ? "마지막 패치 기록을 불러오지 못했습니다."
        : "아직 완료된 패치 기록이 없습니다.";
    public bool IsRepatchEnabled => IsPatchStartEnabled && _lastPatchRecord is { } record
        && record.CanReapply(_currentInstallPath, _currentClientVersion, _latestServerVersion)
        && (record.FromVersion == ManualUpgradeBoundaryVersion
            || _allPatchReadmeItems.Any(item => item.Version == record.FromVersion));
    public string PatchStatusHeader => GetPatchStatus().Header;
    public string PatchStatusDescription => GetPatchStatus().Description;
    public Visibility PatchStatusLinkVisibility => !IsLoadingPatchInfo && RequiresManualUpgradeLink()
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
    public Brush? PatchProgressForeground
    {
        get => _patchProgressForeground;
        set => SetProperty(ref _patchProgressForeground, value);
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
                OnPropertyChanged(nameof(IsPatchStartEnabled));
                OnPropertyChanged(nameof(IsRepatchEnabled));
                OnPropertyChanged(nameof(PatchStatusHeader));
                OnPropertyChanged(nameof(PatchStatusDescription));
                OnPropertyChanged(nameof(LoadingOverlayVisibility));
                OnPropertyChanged(nameof(CanOpenPatchReadme));
                OnPropertyChanged(nameof(NotBusyVisibility));
                OnPropertyChanged(nameof(PatchStatusLinkVisibility));
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
                OnPropertyChanged(nameof(IsPatchStartEnabled));
                OnPropertyChanged(nameof(IsRepatchEnabled));
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
            {
                OnPropertyChanged(nameof(IsPatchStartEnabled));
                OnPropertyChanged(nameof(IsRepatchEnabled));
            }
        }
    }
    #endregion Properties

    public GamePatchSettingPage()
    {
        InitializeComponent();
        Unloaded += (_, _) => TeachingTip_PatchResult.IsOpen = false;
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

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);

        // 셸이 비동기 확인을 담당하며, 이 이벤트에서는 즉시 취소만 결정합니다.
        e.Cancel |= IsBusy || PatchReadmeDrawer.State != DrawerState.Closed;
        if (!e.Cancel)
        {
            CancelPatchInfoLoad();
            ResetPatchReadmeRendering();
            Versions.Clear();
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
            LoadLastPatchRecord();
            (ClientSettings clientSetting, AppDataManager.AppDataOperationResult loadResult) =
                await AppDataManager.LoadServerClientSettingsAsync(_selectedGameServer);

            if (!loadResult.Success)
            {
                ReportPatchSettingsLoadFailure(loadResult);
                _currentClientVersion = null;
                _latestServerVersion = null;
                _allPatchReadmeItems = [];
                DisplayLatestVersion = string.Empty;
                SelectedVersionItem = null;
                Versions.Clear();
                ResetPatchReadmeRendering();
                NotifyPatchStateChanged();
                OnPropertyChanged(nameof(CanOpenPatchReadme));
                return;
            }

            _currentInstallPath = clientSetting.InstallPath;
            ApplyCurrentVersionState(clientSetting.InstallPath);
            _latestServerVersion = null;
            DisplayLatestVersion = string.Empty;

            SelectedVersionItem = null;
            Versions.Clear();
            ResetPatchReadmeRendering();
            RebuildVersionChoices();
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
                if (ex is OperationCanceledException)
                    ex = new IOException("패치 정보 확인이 예기치 않게 중단되었습니다.", ex);
                App.ExceptionHandler.RecordRecoverable(ex, $"GamePatchSettingPage.LoadPatchInfo.{_selectedGameServer}");
                _latestServerVersion = null;
                DisplayLatestVersion = "최신 버전 확인 불가";
                NotifyPatchStateChanged();
                return;
            }

            currentLoadCts.Token.ThrowIfCancellationRequested();

            PatchReadmeInfoItem? latestPatchInfo = items.FirstOrDefault();
            if (latestPatchInfo is null)
            {
                _latestServerVersion = null;
                DisplayLatestVersion = "최신 버전 확인 불가";
                NotifyPatchStateChanged();
                return;
            }

            _latestServerVersion = latestPatchInfo.Version;
            DisplayLatestVersion = latestPatchInfo.Display;

            _allPatchReadmeItems = items;
            RebuildVersionChoices();
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

    private (string Header, string Description) GetPatchStatus()
    {
        if (IsLoadingPatchInfo)
            return ("버전 확인 중", "현재 게임 버전과 최신 버전을 확인하고 있습니다.");
        if (_currentClientVersion is not int currentVersion)
            return ("현재 버전 확인 불가", "현재 게임 버전을 확인할 수 없습니다. 설치 경로를 다시 확인해주세요.");
        if (_latestServerVersion is not int latestVersion)
            return ("최신 버전 확인 불가", "최신 버전을 확인할 수 없습니다. 새로고침으로 다시 확인해주세요.");
        if (RequiresManualUpgradeLink())
            return ("재설치 필요", "v34100 이전 버전 클라이언트입니다. 삭제 후 재설치 하세요.");

        // TODO: [#140] v34100은 패치 이력 누락과 관계없이 패치를 허용하는 기존 예외를 유지합니다.
        if (currentVersion < latestVersion
            && currentVersion != 34100
            && !_allPatchReadmeItems.Any(item => item.Version == currentVersion))
            return ("재설치 필요", "클라이언트가 너무 오래되었습니다. 삭제 후 재설치 하세요.");

        return currentVersion < latestVersion
            ? ("업데이트 필요", "최신 버전으로 업데이트가 필요합니다.")
            : ("최신 버전", "현재 설치된 게임은 최신 버전입니다.");
    }

    private void RebuildVersionChoices()
    {
        SelectedVersionItem = null;
        Versions.Clear();
        if (_currentClientVersion is not int currentVersion)
            return;

        foreach (PatchReadmeInfoItem item in _allPatchReadmeItems)
        {
            if (item.Version <= currentVersion)
                Versions.Add(item);
        }

        // Keep the actual disk version selectable even if the readme omits it.
        // Patch eligibility is checked against the readme, not this display fallback.
        if (!Versions.Any(item => item.Version == currentVersion))
            Versions.Insert(0, new PatchReadmeInfoItem(default, currentVersion, []));
        SelectedVersionItem = Versions.First(item => item.Version == currentVersion);
    }

    private void RefreshInstalledVersionAfterPatch(string installPath)
    {
        _currentInstallPath = installPath;
        ApplyCurrentVersionState(installPath);
        RebuildVersionChoices();
        ResetPatchReadmeDocument();
        if (PatchReadmeDrawer.IsOpen)
            RenderNextPatchReadmeBatch(InitialPatchReadmeRenderCount);
        NotifyPatchStateChanged();
    }

    /// <summary>
    /// 현재 설치 경로에서 클라이언트 버전을 읽고 상태를 갱신합니다.
    /// </summary>
    private void ApplyCurrentVersionState(string installPath)
    {
        ClientVersionReadResult currentVersionResult = PatchManager.TryGetCurrentClientVersion(installPath);
        if (!currentVersionResult.Success || currentVersionResult.Version is null or <= 0)
        {
            _currentClientVersion = null;
            Debug.WriteLine(
                $"[GamePatchSettingPage] Failed to read current version. Server: {_selectedGameServer}, InstallPath: '{installPath}', Result: {currentVersionResult}");
            return;
        }

        _currentClientVersion = currentVersionResult.Version.Value;
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

    /// <summary>
    /// 패치 내역 렌더링 상태를 초기화하고 현재 문서를 비웁니다.
    /// </summary>
    private void ResetPatchReadmeRendering()
    {
        _allPatchReadmeItems = [];
        ResetPatchReadmeDocument();
    }

    private void ResetPatchReadmeDocument()
    {
        // The ScrollViewer survives drawer closure and server changes. Reset the
        // view with the document, once its popup content has a valid layout.
        _isPatchReadmeScrollResetPending = true;
        _hasRequestedPatchReadmeScrollReset = false;
        _nextPatchReadmeRenderIndex = 0;
        _isRenderingPatchReadmeBatch = false;
        RichTextBlock_PatchReadme.Blocks.Clear();
        if (ScrollViewer_PatchReadme.IsLoaded)
            SchedulePatchReadmeScrollReset();
    }

    private void ScrollViewer_PatchReadme_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_isPatchReadmeScrollResetPending)
            return;
        SchedulePatchReadmeScrollReset();
        TryResetPatchReadmeScrollPosition();
    }

    private void ScrollViewer_PatchReadme_Unloaded(object sender, RoutedEventArgs e)
    {
        _hasRequestedPatchReadmeScrollReset = false;
        ScrollViewer_PatchReadme.LayoutUpdated -= ScrollViewer_PatchReadme_ResetLayoutUpdated;
    }

    private void SchedulePatchReadmeScrollReset()
    {
        if (!_isPatchReadmeScrollResetPending)
            return;
        ScrollViewer_PatchReadme.LayoutUpdated -= ScrollViewer_PatchReadme_ResetLayoutUpdated;
        ScrollViewer_PatchReadme.LayoutUpdated += ScrollViewer_PatchReadme_ResetLayoutUpdated;
    }

    private void ScrollViewer_PatchReadme_ResetLayoutUpdated(object? sender, object e)
        => TryResetPatchReadmeScrollPosition();

    private void TryResetPatchReadmeScrollPosition()
    {
        if (!_isPatchReadmeScrollResetPending || _hasRequestedPatchReadmeScrollReset
            || !ScrollViewer_PatchReadme.IsLoaded
            || ScrollViewer_PatchReadme.ActualWidth <= 0 || ScrollViewer_PatchReadme.ActualHeight <= 0)
            return;

        _hasRequestedPatchReadmeScrollReset = true;
        bool accepted = ScrollViewer_PatchReadme.ChangeView(0, 0, null, disableAnimation: true);
        if (!accepted)
            _hasRequestedPatchReadmeScrollReset = false;
        if (accepted)
            ScrollViewer_PatchReadme.LayoutUpdated -= ScrollViewer_PatchReadme_ResetLayoutUpdated;

        // An accepted request can finish on the next ViewChanged event. Keep
        // lazy loading paused until the view actually reaches the new origin.
        if (ScrollViewer_PatchReadme.HorizontalOffset == 0 && ScrollViewer_PatchReadme.VerticalOffset == 0)
            CompletePatchReadmeScrollReset();
    }

    private void CompletePatchReadmeScrollReset()
    {
        _isPatchReadmeScrollResetPending = false;
        _hasRequestedPatchReadmeScrollReset = false;
        ScrollViewer_PatchReadme.LayoutUpdated -= ScrollViewer_PatchReadme_ResetLayoutUpdated;
    }

    /// <summary>
    /// 아직 그리지 않은 패치 내역을 일정 개수만큼 RichTextBlock에 추가합니다.
    /// </summary>
    private void RenderNextPatchReadmeBatch(int batchSize)
    {
        if (_isRenderingPatchReadmeBatch || _nextPatchReadmeRenderIndex >= _allPatchReadmeItems.Count)
            return;

        _isRenderingPatchReadmeBatch = true;
        try
        {
            Brush headerBrush = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"];
            Brush contentBrush = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
            int renderUntil = Math.Min(_nextPatchReadmeRenderIndex + batchSize, _allPatchReadmeItems.Count);

            for (int index = _nextPatchReadmeRenderIndex; index < renderUntil; index++)
                AppendPatchReadmeBlock(_allPatchReadmeItems[index], headerBrush, contentBrush);

            _nextPatchReadmeRenderIndex = renderUntil;
        }
        finally
        {
            _isRenderingPatchReadmeBatch = false;
        }
    }

    /// <summary>
    /// 개별 패치 항목 하나를 제목과 세부 문단들로 변환해 현재 문서에 추가합니다.
    /// </summary>
    private void AppendPatchReadmeBlock(PatchReadmeInfoItem item, Brush headerBrush, Brush contentBrush)
    {
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
            detailParagraph.Inlines.Add(new Run
            {
                Text = item.Details[i],
                FontSize = 12,
                Foreground = contentBrush
            });

            if (i == item.Details.Count - 1)
                detailParagraph.Inlines.Add(new LineBreak());

            RichTextBlock_PatchReadme.Blocks.Add(detailParagraph);
        }
    }

    /// <summary>
    /// 패치 내역 스크롤이 하단 근처로 오면 다음 배치를 이어서 렌더링합니다.
    /// </summary>
    private void ScrollViewer_PatchReadme_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
            return;

        if (_isPatchReadmeScrollResetPending)
        {
            if (_hasRequestedPatchReadmeScrollReset && scrollViewer.IsLoaded
                && scrollViewer.HorizontalOffset == 0 && scrollViewer.VerticalOffset == 0)
                CompletePatchReadmeScrollReset();
            return;
        }

        if (!PatchReadmeDrawer.IsOpen)
            return;

        if (_isRenderingPatchReadmeBatch || _nextPatchReadmeRenderIndex >= _allPatchReadmeItems.Count)
            return;

        double remainingDistance = scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset;
        if (remainingDistance <= PatchReadmeRenderThreshold)
            RenderNextPatchReadmeBatch(PatchReadmeRenderBatchCount);
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
            && _allPatchReadmeItems.Any(item => item.Version == currentVersion || currentVersion == 34100)
            && !RequiresManualUpgradeLink()
            && SelectedVersionItem is not null;

    /// <summary>
    /// 패치 가능 여부와 최상위 상태 안내를 함께 갱신합니다.
    /// </summary>
    private void NotifyPatchStateChanged()
    {
        OnPropertyChanged(nameof(CanOpenPatchReadme));
        OnPropertyChanged(nameof(PatchReadmeTitle));
        OnPropertyChanged(nameof(IsPatchStartEnabled));
        OnPropertyChanged(nameof(IsRepatchEnabled));
        OnPropertyChanged(nameof(PatchStatusHeader));
        OnPropertyChanged(nameof(PatchStatusDescription));
        OnPropertyChanged(nameof(PatchStatusLinkVisibility));
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

    private async Task<PatchArchiveReuseMode?> AskArchiveReuseModeAsync(string tempRoot)
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
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowManagedAsync();

        return result switch
        {
            ContentDialogResult.Primary => PatchArchiveReuseMode.ResumeIfPossible,
            ContentDialogResult.Secondary => PatchArchiveReuseMode.RestartFromScratch,
            _ => null
        };
    }

    /// <summary>
    /// 선택한 버전 기준으로 메인 클라이언트를 패치하고, 생성된 다클라를 설정 파일 보존 정책으로 갱신합니다.
    /// </summary>
    private void Button_PatchStart_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => SafeExecution.RunHandledAsync(StartPatchAsync, $"{nameof(GamePatchSettingPage)}.{nameof(Button_PatchStart_Click)}")
            .FireAndForgetHandled($"{nameof(GamePatchSettingPage)}.{nameof(Button_PatchStart_Click)}");

    private Task StartPatchAsync() => StartPatchAsync(reapply: null);

    private void Button_Repatch_Click(object sender, RoutedEventArgs e)
        => SafeExecution.RunHandledAsync(() => IsRepatchEnabled && _lastPatchRecord is { } record
                ? StartPatchAsync(record) : Task.CompletedTask,
                $"{nameof(GamePatchSettingPage)}.{nameof(Button_Repatch_Click)}")
            .FireAndForgetHandled($"{nameof(GamePatchSettingPage)}.{nameof(Button_Repatch_Click)}");

    private async Task StartPatchAsync(LastPatchRecord? reapply)
    {
        if (reapply is null ? !IsPatchStartEnabled : !IsRepatchEnabled)
            return;

        if (SelectedVersionItem is null)
            return;

        int fromVersion = reapply?.FromVersion ?? SelectedVersionItem.Version;
        GameServer server = _selectedGameServer;
        IsDownloading = true;
        try
        {
            (ClientSettings clientSetting, AppDataManager.AppDataOperationResult loadResult) =
                await AppDataManager.LoadServerClientSettingsAsync(server);

            if (!loadResult.Success)
            {
                ReportPatchSettingsLoadFailure(loadResult);
                return;
            }

            if (_latestServerVersion is not int latestClientVersion)
            {
                ShowPatchResultTeachingTip(
                    "패치 불가",
                    "최신 버전을 확인할 수 없습니다.", isError: true);
                return;
            }

            if (reapply is not null && !reapply.CanReapply(clientSetting.InstallPath,
                    PatchManager.TryGetCurrentClientVersion(clientSetting.InstallPath).Version, latestClientVersion))
            {
                ShowPatchResultTeachingTip("다시 패치 불가",
                    "설치 경로나 게임 버전이 변경되었습니다. 새로고침 후 다시 확인해주세요.", isError: true);
                return;
            }

            string tempRoot = PatchManager.GetPatchTempRoot(
                clientSetting.InstallPath,
                fromVersion,
                latestClientVersion);

            TempPath = $"임시 파일 경로: {tempRoot}";

            PatchArchiveReuseMode archiveReuseMode = PatchArchiveReuseMode.ResumeIfPossible;
            if (Directory.Exists(tempRoot))
            {
                if (await AskArchiveReuseModeAsync(tempRoot) is not { } selectedMode)
                {
                    ProgressText = "패치가 취소되었습니다.";
                    return;
                }
                archiveReuseMode = selectedMode;
            }

            _patchCts = new CancellationTokenSource();

            ShowPatchProgress();
            ResetPatchProgressForeground();

            ProgressMaximum = 1;
            ProgressValue = 0;
            ProgressText = "관리자 권한 승인 후 패치를 시작합니다...";

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
                ArchiveReuseMode: archiveReuseMode);

            await GameMaintenanceService.PatchAsync(
                server,
                fromVersion,
                clientSetting,
                option,
                patchProgress,
                _patchCts.Token);

            RefreshInstalledVersionAfterPatch(clientSetting.InstallPath);
            string? historyWarning = TrySaveLastPatchRecord(server, fromVersion, clientSetting.InstallPath);
            ProgressValue = ProgressMaximum;
            ProgressText = historyWarning ?? "패치가 완료되었습니다.";
            ShowPatchResultTeachingTip("패치 성공", ProgressText);
        }
        catch (MaintenanceCanceledException ex)
        {
            ProgressText = ex.Message;
        }
        catch (OperationCanceledException) when (_patchCts?.IsCancellationRequested == true)
        {
            ProgressText = "패치가 취소되었습니다.";
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
                ex = new IOException("패치가 예기치 않게 중단되었습니다. 작업 상태를 확인해 주세요.", ex);
            App.ExceptionHandler.RecordRecoverable(ex, $"GamePatchSettingPage.StartPatch.{_selectedGameServer}");
            MarkPatchProgressAsFailed();

            if (TryShowMultiClientSymbolicDirectoryConflictTeachingTip(ex))
                return;

            if (PathPermissionDialog.IsPermissionFailure(ex))
            {
                string failureMessage = "패치에 실패했습니다.\n권한 문제 해결 방법을 확인해 주세요.";
                ProgressText = "패치 실패";
                ShowPatchResultTeachingTip("패치 실패", failureMessage, isError: true);
                _patchResultPermissionHelp = true;
                TeachingTip_PatchResult.ActionButtonContent = "해결 방법 확인하기";
                return;
            }

            string defaultFailureMessage = $"패치에 실패했습니다.\n{ex.Message}";
            ProgressText = "패치 실패";
            ShowPatchResultTeachingTip("패치 실패", defaultFailureMessage, isError: true);
        }
        finally
        {
            IsDownloading = false;

            _patchCts?.Dispose();
            _patchCts = null;
        }
    }

    private void LoadLastPatchRecord()
    {
        try
        {
            _lastPatchRecord = AppDataManager.LoadLastPatchRecord(_selectedGameServer);
            _patchHistoryUnavailable = false;
        }
        catch (Exception ex)
        {
            // Optional history must never prevent normal patching or version discovery.
            _lastPatchRecord = null;
            _patchHistoryUnavailable = true;
            App.ExceptionHandler.RecordRecoverable(ex, $"GamePatchSettingPage.LoadLastPatchRecord.{_selectedGameServer}");
        }
        OnPropertyChanged(nameof(LastPatchDescription));
        OnPropertyChanged(nameof(IsRepatchEnabled));
    }

    private string? TrySaveLastPatchRecord(GameServer server, int fromVersion, string installPath)
    {
        // A no-op patch must not erase the last useful range needed for reapplication.
        if (_currentClientVersion is int currentVersion && currentVersion <= fromVersion)
            return null;
        try
        {
            if (_currentClientVersion is not int toVersion)
                throw new InvalidOperationException("패치 후 게임 버전을 확인할 수 없습니다.");
            var record = new LastPatchRecord(fromVersion, toVersion, installPath);
            AppDataManager.SaveLastPatchRecord(server, record);
            _lastPatchRecord = record;
            _patchHistoryUnavailable = false;
            OnPropertyChanged(nameof(LastPatchDescription));
            OnPropertyChanged(nameof(IsRepatchEnabled));
            return null;
        }
        catch (Exception ex)
        {
            App.ExceptionHandler.RecordRecoverable(ex, $"GamePatchSettingPage.SaveLastPatchRecord.{server}");
            return "패치는 완료되었지만 마지막 패치 기록을 저장하지 못했습니다.";
        }
    }

    private void MarkPatchProgressAsFailed()
    {
        ShowPatchProgress();
        if (ProgressMaximum <= 0)
            ProgressMaximum = 1;

        ProgressValue = ProgressMaximum;
        PatchProgressForeground = GetResourceBrush("SystemFillColorCriticalBrush");
    }

    private void ResetPatchProgressForeground()
    {
        PatchProgressForeground =
            GetResourceBrush("AccentFillColorDefaultBrush")
            ?? GetResourceBrush("AccentTextFillColorPrimaryBrush");
    }

    private void ShowPatchResultTeachingTip(string title, string subtitle, bool isError = false)
    {
        _patchResultExplorerPath = null;
        _patchResultPermissionHelp = false;
        TeachingTip_PatchResult.Title = title;
        TeachingTip_PatchResult.Subtitle = subtitle;
        TeachingTip_PatchResult.IconSource = isError ? CreateErrorBadgeIconSource() : null;
        TeachingTip_PatchResult.ActionButtonContent = null;
        TeachingTip_PatchResult.CloseButtonContent = "닫기";
        TeachingTip_PatchResult.IsOpen = true;
    }

    private bool TryShowMultiClientSymbolicDirectoryConflictTeachingTip(Exception exception)
    {
        GameClientHelper.MultiClientSymbolicDirectoryConflictException? conflictException =
            FindException<GameClientHelper.MultiClientSymbolicDirectoryConflictException>(exception);
        if (conflictException is null)
            return false;

        GameClientHelper.MultiClientCreationException? creationException =
            FindException<GameClientHelper.MultiClientCreationException>(exception);

        string failureMessage = BuildMultiClientSymbolicDirectoryConflictMessage(creationException, conflictException);
        ProgressText = "패치 실패";
        _patchResultPermissionHelp = false;
        _patchResultExplorerPath = ResolveMultiClientConflictExplorerPath(creationException, conflictException);
        TeachingTip_PatchResult.Title = "패치 실패";
        TeachingTip_PatchResult.Subtitle = failureMessage;
        TeachingTip_PatchResult.IconSource = CreateErrorBadgeIconSource();
        TeachingTip_PatchResult.ActionButtonContent = "상위 폴더 열기";
        TeachingTip_PatchResult.CloseButtonContent = "닫기";
        TeachingTip_PatchResult.IsOpen = true;
        return true;
    }

    private void TeachingTip_PatchResult_ActionButtonClick(TeachingTip sender, object args)
    {
        try
        {
            if (_patchResultPermissionHelp)
            {
                TeachingTip_PatchResult.IsOpen = false;
                if (App.CurrentWindow is MainWindow window)
                    window.NavigateToWebViewPageByLinkKey(AppLinkKeys.HelpPermissionMultiClient);
                return;
            }

            if (string.IsNullOrWhiteSpace(_patchResultExplorerPath))
                return;

            if (!Directory.Exists(_patchResultExplorerPath))
            {
                TeachingTip_PatchResult.Subtitle = "대상 폴더를 찾을 수 없습니다.";
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_patchResultExplorerPath}\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            App.ExceptionHandler.RecordRecoverable(ex, "GamePatchSettingPage.PatchResultAction");
            TeachingTip_PatchResult.Subtitle = "해결 방법을 열지 못했습니다. 잠시 후 다시 시도해 주세요.";
            TeachingTip_PatchResult.IsOpen = true;
        }
    }

    private static FontIconSource CreateErrorBadgeIconSource()
    {
        return new FontIconSource
        {
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            Glyph = "\uEA39"
        };
    }

    private static Brush? GetResourceBrush(string resourceKey)
        => Application.Current.Resources[resourceKey] as Brush;

    private static string BuildMultiClientSymbolicDirectoryConflictMessage(
        GameClientHelper.MultiClientCreationException? creationException,
        GameClientHelper.MultiClientSymbolicDirectoryConflictException conflictException)
    {
        string targetPath = creationException?.DestinationPath ?? conflictException.DestinationPath;
        string targetFolderName = TryGetDirectoryName(targetPath) ?? targetPath;
        return $"{targetFolderName}{Environment.NewLine}위 폴더를 삭제하신 후 다시 다클라를 생성해주세요.";
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
            ShowPatchResultTeachingTip(reason == LeaveReason.AppExit ? "종료 불가" : "이동 불가",
                "작업이 끝난 뒤 다시 시도해 주세요.");
            return false;
        }

        // Shell section changes can hide the page without unloading it. Close its
        // root-hosted popup before allowing navigation, so it cannot outlive this page.
        if (PatchReadmeDrawer.State != DrawerState.Closed && !await PatchReadmeDrawer.CloseAsync())
            return false;

        if (reason == LeaveReason.AppExit && XamlRoot is not null
            && !await ExitConfirmationDialog.ShowAsync(XamlRoot))
            return false;

        TeachingTip_PatchResult.IsOpen = false;
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
    /// 설정 읽기 실패는 원인을 기록하고 페이지 안에서만 안내합니다.
    /// </summary>
    private void ReportPatchSettingsLoadFailure(AppDataManager.AppDataOperationResult result)
    {
        App.ExceptionHandler.RecordRecoverable(new IOException(
            $"패치용 서버 설정을 불러오지 못했습니다. Operation={result.Operation}, Target={result.Target}, ErrorKind={result.ErrorKind}", result.Exception),
            $"GamePatchSettingPage.LoadSettings.{_selectedGameServer}");
        ShowPatchResultTeachingTip("설정 불러오기 실패", "서버 설정을 불러올 수 없습니다. 다시 시도해 주세요.", isError: true);
    }

    #region Segmented server selection
    private void Segmented_Server_SelectionChanged(object sender, SelectionChangedEventArgs args)
        => SafeExecution.RunHandledAsync(async () =>
        {
            if (_suppressServerSelectionChanged
                || sender is not Segmented { SelectedItem: SegmentedItem selectedItem }
                || selectedItem.Tag is not GameServer server
                || _selectedGameServer == server)
                return;

            if (IsBusy || PatchReadmeDrawer.State != DrawerState.Closed)
            {
                ApplySelectedServer(_selectedGameServer);
                return;
            }
            ApplySelectedServer(server);
            await LoadPatchInfoAsync();
        }, $"{nameof(GamePatchSettingPage)}.{nameof(Segmented_Server_SelectionChanged)}")
            .FireAndForgetHandled($"{nameof(GamePatchSettingPage)}.{nameof(Segmented_Server_SelectionChanged)}");

    private void ApplySelectedServer(GameServer server)
    {
        _selectedGameServer = server;
        _suppressServerSelectionChanged = true;
        try { Segmented_Server.SelectedItem = GetSegmentedItem(server); }
        finally { _suppressServerSelectionChanged = false; }
        OnPropertyChanged(nameof(PatchReadmeTitle));
    }

    private SegmentedItem GetSegmentedItem(GameServer server)
        => server switch
        {
            GameServer.Korea_Live => SegmentedItem_Live,
            GameServer.Korea_Test => SegmentedItem_Test,
            GameServer.Korea_RnD => SegmentedItem_RnD,
            _ => throw new ArgumentOutOfRangeException(nameof(server), server, null),
        };
    #endregion Segmented server selection

    private void Button_ViewPatchReadme_Click(object sender, RoutedEventArgs e)
        => SafeExecution.RunHandledAsync(OpenPatchReadmeAsync,
            $"{nameof(GamePatchSettingPage)}.{nameof(Button_ViewPatchReadme_Click)}")
            .FireAndForgetHandled($"{nameof(GamePatchSettingPage)}.{nameof(Button_ViewPatchReadme_Click)}");

    private async Task OpenPatchReadmeAsync()
    {
        if (!CanOpenPatchReadme || !IsLoaded || PatchReadmeDrawer.State != DrawerState.Closed)
            return;

        // Build the initial document before opening, so the first animated frame
        // already has content. Reopening preserves the existing document and position.
        if (_nextPatchReadmeRenderIndex == 0)
            RenderNextPatchReadmeBatch(InitialPatchReadmeRenderCount);

        PatchReadmeDrawer.Host = XamlRoot.Content as FrameworkElement ?? PatchPageRoot;
        await PatchReadmeDrawer.OpenAsync();
    }

    private void Button_ClosePatchReadme_Click(object sender, RoutedEventArgs e)
        => SafeExecution.RunHandledAsync(async () => { await PatchReadmeDrawer.CloseAsync(); },
            $"{nameof(GamePatchSettingPage)}.{nameof(Button_ClosePatchReadme_Click)}")
            .FireAndForgetHandled($"{nameof(GamePatchSettingPage)}.{nameof(Button_ClosePatchReadme_Click)}");

    private void PatchReadmeDrawer_InteractionFailed(object? sender, DrawerInteractionFailedEventArgs e)
        => Task.FromException(e.Exception)
            .FireAndForgetHandled($"{nameof(GamePatchSettingPage)}.{nameof(PatchReadmeDrawer_InteractionFailed)}");

    private void HyperlinkButton_DeprecatedVersionInfo_Click(object sender, RoutedEventArgs e)
    {
        if (App.CurrentWindow is MainWindow mainWindow)
            mainWindow.NavigateToWebViewPageByLinkKey(AppLinkKeys.HelpPatchLegacyVersion);
    }
}
