using Core;
using Core.Models;
using GersangStation.Diagnostics;
using GersangStation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace GersangStation.Main.Setting;

public sealed partial class BrowserSettingPage : Page, INotifyPropertyChanged
{
    private WebViewManager? _browser;
    private bool _loading = true;
    private bool _confirming;
    private CancellationTokenSource? _pageLifetime;
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool IsClearingData => _browser?.IsMaintenanceInProgress == true;
    public bool CanClearData => _browser is not null && !IsClearingData && !_confirming;
    public Visibility ProgressVisibility => IsClearingData ? Visibility.Visible : Visibility.Collapsed;

    public BrowserSettingPage()
    {
        InitializeComponent();
        SearchProviderComboBox.SelectedIndex = (int)AppDataManager.BrowserSearchProvider;
        HardwareAccelerationToggle.IsOn = AppDataManager.BrowserHardwareAccelerationEnabled;
        _loading = false;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _pageLifetime = new CancellationTokenSource();
        _browser = (App.CurrentWindow as MainWindow)?.WebViewManager;
        if (_browser is not null) _browser.PropertyChanged += OnBrowserChanged;
        NotifyState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _pageLifetime?.Cancel();
        _pageLifetime?.Dispose();
        _pageLifetime = null;
        if (_browser is not null) _browser.PropertyChanged -= OnBrowserChanged;
        ResultTip.IsOpen = false;
    }

    private async void SearchProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => await SafeExecution.RunHandledAsync(() =>
        {
            if (!_loading && SearchProviderComboBox.SelectedIndex >= 0)
                AppDataManager.BrowserSearchProvider = (BrowserSearchProvider)SearchProviderComboBox.SelectedIndex;
            return Task.CompletedTask;
        }, "BrowserSettingPage.SearchProviderChanged");

    private async void HardwareAccelerationToggle_Toggled(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(() =>
        {
            if (!_loading) AppDataManager.BrowserHardwareAccelerationEnabled = HardwareAccelerationToggle.IsOn;
            return Task.CompletedTask;
        }, "BrowserSettingPage.HardwareAccelerationChanged");

    private async void ClearCookiesAndCache_Click(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(() => ClearDataAsync(false), "BrowserSettingPage.ClearCookiesAndCache");

    private async void ResetBrowserData_Click(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(() => ClearDataAsync(true), "BrowserSettingPage.ResetBrowserData");

    private async Task ClearDataAsync(bool allData)
    {
        if (!CanClearData || _browser is not WebViewManager browser || _pageLifetime is null) return;
        CancellationToken pageToken = _pageLifetime.Token;
        _confirming = true;
        NotifyState();
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = allData ? "브라우저 데이터를 초기화할까요?" : "쿠키 및 캐시를 정리할까요?",
                Content = (allData
                    ? "캐시, 쿠키, 사이트 저장소, 방문·다운로드 기록, 자동완성, 웹사이트 저장 암호와 사이트 권한 등 프로필 데이터를 삭제합니다.\n\n"
                    : "일반·웹앱 캐시, 쿠키, 사이트 저장소와 방문·다운로드 기록을 삭제합니다. 자동완성과 웹사이트 저장 암호, 사이트 권한은 유지합니다.\n\n")
                    + "진행 중인 로그인·OTP·게임 실행 준비·다운로드를 중단하고 빈 페이지로 이동합니다. 사이트에서 로그아웃되며 삭제는 되돌릴 수 없습니다.\n\n"
                    + "실행 중인 게임, 완료된 다운로드 파일, 앱의 등록 계정·저장 비밀번호·즐겨찾기는 보존됩니다.",
                PrimaryButtonText = allData ? "초기화" : "정리",
                CloseButtonText = "취소",
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowManagedAsync(pageToken) != ContentDialogResult.Primary) return;
            pageToken.ThrowIfCancellationRequested();
            // Once confirmed, the window-owned service finishes even if this page is left.
            await browser.ClearBrowsingDataAsync(allData);
            if (!pageToken.IsCancellationRequested)
                ShowResult("브라우저 데이터 정리 완료", "브라우저를 빈 페이지로 이동하고 데이터를 정리했습니다. 필요한 사이트에서 다시 로그인해 주세요.");
        }
        catch (OperationCanceledException) when (pageToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            App.ExceptionHandler.RecordRecoverable(ex, allData
                ? "BrowserSettingPage.ResetBrowserData"
                : "BrowserSettingPage.ClearCookiesAndCache");
            if (!pageToken.IsCancellationRequested)
                ShowResult("브라우저 데이터 정리 실패", "일부 데이터가 남아 있을 수 있습니다. 앱을 다시 실행한 후 시도해 주세요.");
        }
        finally { _confirming = false; NotifyState(); }
    }

    private void ShowResult(string title, string message)
    {
        ResultTip.Title = title;
        ResultTip.Subtitle = message;
        ResultTip.IsOpen = true;
    }

    private void OnBrowserChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WebViewManager.IsMaintenanceInProgress)) NotifyState();
    }

    private void NotifyState()
    {
        PropertyChanged?.Invoke(this, new(nameof(IsClearingData)));
        PropertyChanged?.Invoke(this, new(nameof(CanClearData)));
        PropertyChanged?.Invoke(this, new(nameof(ProgressVisibility)));
    }
}
