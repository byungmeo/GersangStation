using Core;
using Core.Models;
using Core.Patch;
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
    private string _displayLatestVersion = string.Empty;
    private bool _shouldPatchMultiClient = true;
    private bool _shouldDeleteTemp = true;
    private string _tempPath = string.Empty;
    private double _progressMaximum = 100;
    private double _progressValue = 0;
    private string _progressText = string.Empty;
    private bool _isLoadingPatchInfo = false;
    private bool _isDownloadingPatch = false;
    private PatchReadmeInfoItem? _selectedVersionItem;
    private CancellationTokenSource? _patchCts;
    private int _currentClientVersion;

    public ObservableCollection<PatchReadmeInfoItem> Versions { get; } = [];

    public bool IsNotBusy => !IsBusy;
    public Visibility DownloadingVisibility => IsDownloading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NotBusyVisibility => IsBusy ? Visibility.Collapsed : Visibility.Visible;
    public Visibility LoadingOverlayVisibility => IsLoadingPatchInfo ? Visibility.Visible : Visibility.Collapsed;
    public bool IsBusy => _isLoadingPatchInfo || _isDownloadingPatch;

    public string DisplayLatestVersion
    {
        get => _displayLatestVersion;
        set => SetProperty(ref _displayLatestVersion, value);
    }
    public bool ShouldPatchMultiClient
    {
        get => _shouldPatchMultiClient;
        set => SetProperty(ref _shouldPatchMultiClient, value);
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
                OnPropertyChanged(nameof(LoadingOverlayVisibility));
                OnPropertyChanged(nameof(NotBusyVisibility));
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
                OnPropertyChanged(nameof(DownloadingVisibility));
                OnPropertyChanged(nameof(NotBusyVisibility));
            }
        }
    }
    public PatchReadmeInfoItem? SelectedVersionItem
    {
        get => _selectedVersionItem;
        set => SetProperty(ref _selectedVersionItem, value);
    }
    #endregion Properties

    public GamePatchSettingPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        await LoadPatchInfoAsync();
    }

    protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);

        e.Cancel = !await ConfirmLeaveAsync();
    }

    private async Task LoadPatchInfoAsync()
    {
        IsLoadingPatchInfo = true;
        try
        {
            _currentClientVersion = PatchHelper.GetCurrentClientVersion(AppDataManager.SelectedServer);
            TempPath = $"임시 파일 경로: {AppDataManager.LoadServerClientSettings(AppDataManager.SelectedServer).TempPath}";

            SelectedVersionItem = null;
            Versions.Clear();
            RichTextBlock_PatchReadme.Blocks.Clear();

            List<PatchReadmeInfoItem> items = await PatchReadmeHelper.GetPatchInfoList(AppDataManager.SelectedServer);
            var latestPatchInfo = items.FirstOrDefault() ?? new PatchReadmeInfoItem(new DateTime(), 0, []);
            DisplayLatestVersion = latestPatchInfo.Display;

            Brush headerBrush = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"];
            Brush contentBrush = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
            foreach (PatchReadmeInfoItem item in items)
            {
                if (_currentClientVersion >= item.Version)
                    Versions.Add(item);

                bool shouldHighlightHeader = item.Version > 34000;
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
        }
        finally
        {
            IsLoadingPatchInfo = false;
        }
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

    private async void Button_PatchStart_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (IsBusy)
            return;

        _patchCts = new CancellationTokenSource();

        try
        {
            IsDownloading = true;
            ProgressMaximum = 100;
            ProgressValue = 0;
            ProgressText = "패치를 준비하는 중...";

            var patchProgress = new Progress<PatchProgress>(p =>
            {
                ProgressValue = Math.Clamp(p.Percentage, 0, ProgressMaximum);
                ProgressText = p.Message;
            });

            await PatchHelper.PatchAsync(
                server: AppDataManager.SelectedServer,
                currentClientVersion: _currentClientVersion,
                cleanupTemp: ShouldDeleteTemp,
                progress: patchProgress,
                ct: _patchCts.Token);

            ProgressValue = ProgressMaximum;
            ProgressText = "패치가 완료되었습니다.";
        }
        catch (OperationCanceledException)
        {
            ProgressText = "패치가 취소되었습니다.";
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

    public async Task<bool> ConfirmLeaveAsync()
    {
        if (IsBusy)
        {
            ContentDialog contentDialog = new()
            {
                XamlRoot = XamlRoot,
                Title = "이동 불가",
                Content = "작업 진행 중 페이지를 이동할 수 없습니다.",
                IsPrimaryButtonEnabled = true,
                PrimaryButtonText = "확인",
                DefaultButton = ContentDialogButton.Primary
            };
            await contentDialog.ShowAsync();
            return false;
        }

        return true;
    }
}
