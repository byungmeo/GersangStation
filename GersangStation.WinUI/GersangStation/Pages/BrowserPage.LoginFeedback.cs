using GersangStation.Diagnostics;
using GersangStation.Main.Setting;
using GersangStation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using Windows.System;

namespace GersangStation.Main;

public sealed partial class WebViewPage
{
    private int _browserLoginFeedbackGeneration;
    private bool _browserLoginFeedbackActive;
    private bool _browserLoginShowHelp;
    private (string Title, string Message, bool ShowHelp, bool IsError, bool ShowAccountSettings, bool ShowPhoneVerification)? _pendingBrowserLoginNotice;
    private readonly HashSet<TeachingTip> _closingBrowserLoginTips = [];
    private TeachingTip? _activeBrowserLoginTip;

    private void ClearBrowserLoginNotice()
    {
        _browserLoginFeedbackGeneration++;
        _pendingBrowserLoginNotice = null;
        foreach (TeachingTip tip in new[] { BrowserLoginTeachingTip, BrowserLoginFailureTeachingTip })
        {
            if (!tip.IsOpen) continue;
            _closingBrowserLoginTips.Add(tip);
            try { tip.IsOpen = false; }
            catch (Exception ex)
            {
                _closingBrowserLoginTips.Remove(tip);
                App.ExceptionHandler.RecordRecoverable(ex, "BrowserPage.ClearLoginFeedback");
            }
        }
    }

    private void ShowBrowserLoginNotice(string title, string message, bool showHelp,
        bool isError = false, bool showAccountSettings = true, bool showPhoneVerification = false)
    {
        if (!_browserLoginFeedbackActive) return;
        ClearBrowserLoginNotice();
        try
        {
            _pendingBrowserLoginNotice = (title, message, showHelp, isError || showHelp, showAccountSettings, showPhoneVerification);
            OpenPendingBrowserLoginNotice();
        }
        catch (Exception ex) { App.ExceptionHandler.RecordRecoverable(ex, "BrowserPage.ShowLoginFeedback"); }
    }

    private void OpenPendingBrowserLoginNotice()
    {
        if (_closingBrowserLoginTips.Count != 0 || !_browserLoginFeedbackActive || !IsLoaded ||
            _pendingBrowserLoginNotice is not { } notice) return;
        _pendingBrowserLoginNotice = null;
        _browserLoginShowHelp = notice.ShowHelp;
        TeachingTip tip = notice.IsError ? BrowserLoginFailureTeachingTip : BrowserLoginTeachingTip;
        BrowserLoginAccountSettingsButton.Visibility = BrowserLoginFailureAccountSettingsButton.Visibility =
            notice.ShowAccountSettings ? Visibility.Visible : Visibility.Collapsed;
        BrowserLoginHelpButton.Visibility = notice.ShowHelp ? Visibility.Visible : Visibility.Collapsed;
        BrowserPhoneVerificationButton.Visibility = BrowserFailurePhoneVerificationButton.Visibility =
            notice.ShowPhoneVerification ? Visibility.Visible : Visibility.Collapsed;
        BrowserLoginWarningActions.Visibility = BrowserLoginFailureActions.Visibility =
            notice.ShowAccountSettings || notice.ShowHelp || notice.ShowPhoneVerification ? Visibility.Visible : Visibility.Collapsed;
        tip.Title = notice.Title;
        tip.Subtitle = notice.Message;
        _activeBrowserLoginTip = tip;
        tip.IsOpen = true;
    }

    private void BrowserLoginTeachingTip_Closing(TeachingTip sender, TeachingTipClosingEventArgs args)
        => _closingBrowserLoginTips.Add(sender);

    private void BrowserLoginTeachingTip_Closed(TeachingTip sender, TeachingTipClosedEventArgs args)
    {
        _closingBrowserLoginTips.Remove(sender);
        if (_pendingBrowserLoginNotice is null && ReferenceEquals(_activeBrowserLoginTip, sender))
            _browserLoginFeedbackGeneration++;
        if (_pendingBrowserLoginNotice is null) return;
        // Closed 콜백의 Popup 정리가 끝난 다음 UI 턴에 다시 엽니다.
        try
        {
            if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                try { OpenPendingBrowserLoginNotice(); }
                catch (Exception ex) { App.ExceptionHandler.RecordRecoverable(ex, "BrowserPage.ReopenLoginFeedback"); }
            }))
            {
                _pendingBrowserLoginNotice = null;
                App.ExceptionHandler.RecordRecoverable(new InvalidOperationException("로그인 안내 재표시를 UI에 예약하지 못했습니다."),
                    "BrowserPage.ReopenLoginFeedback");
            }
        }
        catch (Exception ex)
        {
            _pendingBrowserLoginNotice = null;
            App.ExceptionHandler.RecordRecoverable(ex, "BrowserPage.ReopenLoginFeedback");
        }
    }

    private void BrowserPage_LoginFeedbackUnloaded(object sender, RoutedEventArgs args)
    {
        _browserLoginFeedbackActive = false;
        ClearBrowserLoginNotice();
    }

    private void BrowserPage_LoginFeedbackLoaded(object sender, RoutedEventArgs args)
    {
        try { OpenPendingBrowserLoginNotice(); }
        catch (Exception ex) { App.ExceptionHandler.RecordRecoverable(ex, "BrowserPage.LoadLoginFeedback"); }
    }

    internal void ShowBrowserLoginFailure(string message, bool retryBlocked)
        => ShowBrowserLoginNotice("로그인 실패", message, showHelp: false,
            isError: !retryBlocked, showAccountSettings: false);

    internal void ShowBrowserPhoneVerificationGuide()
        => ShowBrowserLoginNotice("휴대폰 본인 인증 필요", "거상 웹페이지에서 휴대폰 본인 인증을 완료해 주세요.",
            showHelp: false, showAccountSettings: false, showPhoneVerification: true);

    private void OpenPhoneVerificationButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if ((_shellWindow ?? App.CurrentWindow as MainWindow) is not { } window)
                throw new InvalidOperationException("본인 인증 페이지를 열 창을 찾지 못했습니다.");
            window.NavigateToWebViewPage();
            ClearBrowserLoginNotice();
        }
        catch (Exception ex)
        {
            App.ExceptionHandler.RecordRecoverable(ex, "BrowserPage.OpenPhoneVerification");
            ShowBrowserLoginNotice("본인 인증 페이지 열기 실패", "잠시 후 다시 시도해 주세요.",
                showHelp: false, isError: true, showAccountSettings: false, showPhoneVerification: true);
        }
    }

    private void OpenLoginAccountSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if ((_shellWindow ?? App.CurrentWindow as MainWindow) is not { } window)
                throw new InvalidOperationException("계정 설정을 열 창을 찾지 못했습니다.");
            window.NavigateToSettingPage(SettingSection.Account);
        }
        catch (Exception ex)
        {
            App.ExceptionHandler.RecordRecoverable(ex, "BrowserPage.OpenLoginAccountSettings");
            ShowBrowserLoginNotice("계정 설정을 열지 못했습니다",
                "잠시 후 다시 시도하거나 사이드바에서 계정 설정을 열어 주세요.",
                _browserLoginShowHelp, isError: true);
        }
    }

    private async void OpenBrowserLoginHelpButton_Click(object sender, RoutedEventArgs e)
    {
        int generation = _browserLoginFeedbackGeneration;
        try
        {
            if (!App.LinkManager.TryGetUrl(AppLinkKeys.HelpCredentialVault, out string url) ||
                !await Launcher.LaunchUriAsync(new Uri(url)))
                throw new InvalidOperationException("자격 증명 관리자 도움말을 외부 브라우저에서 열지 못했습니다.");
        }
        catch (Exception ex)
        {
            App.ExceptionHandler.RecordRecoverable(ex, "BrowserPage.OpenLoginCredentialHelp");
            if (generation == _browserLoginFeedbackGeneration && _activeBrowserLoginTip?.IsOpen == true)
                ShowBrowserLoginNotice("해결 방법을 열지 못했습니다",
                    "잠시 후 다시 시도해 주세요. 계정 설정에서도 저장된 계정을 확인할 수 있습니다.", true);
        }
    }
}
