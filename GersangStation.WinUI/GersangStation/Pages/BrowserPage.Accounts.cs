using Core;
using GersangStation.Diagnostics;
using GersangStation.Main.Setting;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;

namespace GersangStation.Main;

public sealed partial class WebViewPage
{
    private bool _isBrowserAccountsLoading;
    private bool _hasBrowserAccountsLoadFailure;
    public bool CanRetryBrowserAccounts => _hasBrowserAccountsLoadFailure && !_isBrowserAccountsLoading;
    public bool CanOpenBrowserAccountSettings => !_isBrowserAccountsLoading;

    private void NotifyBrowserAccountsRecoveryState()
    {
        OnPropertyChanged(nameof(CanRetryBrowserAccounts));
        OnPropertyChanged(nameof(CanOpenBrowserAccountSettings));
    }

    private async Task LoadAccountsAsync()
    {
        if (_isBrowserAccountsLoading)
            return;
        _isBrowserAccountsLoading = true;
        NotifyBrowserAccountsRecoveryState();
        try
        {
            var (accounts, result) = await AppDataManager.LoadAccountsAsync();
            if (!result.Success)
            {
                ReportBrowserAccountsLoadFailure(BuildAppDataLoadException("계정 목록", result),
                    result.ErrorKind == AppDataManager.AppDataErrorKind.Serialization);
                return;
            }
            ApplyAccounts(accounts);
            _hasBrowserAccountsLoadFailure = false;
            try { BrowserAccountsInfoBar.IsOpen = false; }
            catch (Exception ex) { App.ExceptionHandler.RecordRecoverable(ex, "BrowserPage.ClearAccountsFeedback"); }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ReportBrowserAccountsLoadFailure(ex, invalidFormat: false);
        }
        finally
        {
            _isBrowserAccountsLoading = false;
            NotifyBrowserAccountsRecoveryState();
        }
    }

    private void ReportBrowserAccountsLoadFailure(Exception exception, bool invalidFormat)
    {
        _hasBrowserAccountsLoadFailure = true;
        App.ExceptionHandler.RecordRecoverable(exception, "BrowserPage.LoadAccounts");
        ShowBrowserAccountsNotice(invalidFormat
            ? "저장된 계정 파일 형식을 읽을 수 없습니다. 기존 목록은 유지하지만 새 로그인에는 사용하지 않습니다. 다시 불러오거나 계정 설정에서 확인해 주세요."
            : "계정 목록을 불러오지 못했습니다. 기존 목록은 유지하지만 새 로그인에는 사용하지 않습니다. 파일 접근 문제를 확인한 후 다시 불러오거나 계정 설정에서 확인해 주세요.");
    }

    private void ShowBrowserAccountsNotice(string message)
    {
        try
        {
            BrowserAccountsInfoBar.Message = message;
            BrowserAccountsInfoBar.IsOpen = true;
        }
        catch (Exception ex) { App.ExceptionHandler.RecordRecoverable(ex, "BrowserPage.ShowAccountsFeedback"); }
    }

    private async void ReloadBrowserAccountsButton_Click(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(LoadAccountsAsync, "BrowserPage.ReloadAccounts");

    private void OpenBrowserAccountSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if ((_shellWindow ?? App.CurrentWindow as MainWindow) is not { } window)
                throw new InvalidOperationException("계정 설정을 열 창을 찾지 못했습니다.");
            window.NavigateToSettingPage(SettingSection.Account);
        }
        catch (Exception ex)
        {
            App.ExceptionHandler.RecordRecoverable(ex, "BrowserPage.OpenAccountSettings");
            ShowBrowserAccountsNotice("계정 설정을 열지 못했습니다. 잠시 후 다시 시도해 주세요.");
        }
    }
}
