using GersangStation.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SideDrawer.Controls;
using System;
using System.Threading.Tasks;

namespace GersangStation.Main;

public sealed partial class MainWindow
{
    private string PreviousCrashNoticeMessage => _previousFatalError?.IsUncleanExit == true
        ? "이전 실행의 정상 종료 기록을 확인하지 못했습니다. 강제 종료나 시스템 종료 등이 원인일 수 있으며, 예외 정보는 확보되지 않았습니다."
        : "이전 실행 중 오류로 앱이 종료되었습니다. 문제가 반복되면 오류 기록을 확인해 주세요.";
    private bool _hasShownPreviousCrash;
    private PendingFatalError? _previousFatalError;

    private void ShowPreviousCrashNotice()
    {
        if (_hasShownPreviousCrash || FatalErrorHandler.Current.PreviousError is not { } previous)
            return;

        _hasShownPreviousCrash = true;
        _previousFatalError = previous;
        PreviousCrashInfoBar.Title = previous.IsUncleanExit
            ? "이전 실행의 종료 상태를 확인하지 못했습니다."
            : "이전 실행이 정상적으로 종료되지 않았습니다.";
        PreviousCrashInfoBar.Message = PreviousCrashNoticeMessage;
        PreviousCrashDetailsButton.Content = previous.IsUncleanExit ? "종료 기록 보기" : "오류 기록 보기";
        PreviousCrashDrawerTitleTextBlock.Text = previous.IsUncleanExit ? "이전 실행 종료 기록" : "이전 실행 오류 기록";
        PreviousCrashDescriptionTextBlock.Text = previous.IsUncleanExit
            ? "정상 종료 기록이 남지 않은 실행입니다. 예외 상세 정보는 없어 종료 원인을 특정할 수 없습니다."
            : "이전 실행에서 앱을 종료시킨 오류입니다. 문제가 반복되면 아래 기록을 복사해 전달해 주세요.";
        PreviousCrashInfoBar.Visibility = Visibility.Visible;
        PreviousCrashInfoBar.IsOpen = true;
        UpdateShellNoticeHostVisibility();
        // 표시한 안내 표식만 소비합니다. 로그는 보존하고 다음 시작 시 같은 안내를 반복하지 않습니다.
        FatalErrorHandler.Current.AcknowledgePreviousError();
    }

    private void PreviousCrashInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        PreviousCrashInfoBar.Visibility = Visibility.Collapsed;
        UpdateShellNoticeHostVisibility();
    }

    private async void PreviousCrashDetailsButton_Click(object sender, RoutedEventArgs e)
        => await ShowPreviousCrashDetailsAsync();

    private async Task ShowPreviousCrashDetailsAsync()
    {
        if (_previousFatalError is not { } previous || !PreviousCrashDrawer.IsLoaded ||
            _isOpeningDiagnosticDetails || PreviousCrashDrawer.State != DrawerState.Closed)
            return;

        _isOpeningDiagnosticDetails = true;
        PreviousCrashDetailsButton.IsEnabled = false;
        try
        {
            if (!await RecoverableErrorDrawer.CloseAsync())
                return;
            string details = await Task.Run(() => FatalErrorStore.ReadDetails(previous));
            // 파일을 읽는 사이 창이 닫혔다면 Popup을 다시 만들지 않습니다.
            if (!PreviousCrashDrawer.IsLoaded || Root.XamlRoot is null)
                return;

            PreviousCrashDetailsView.DisplayDetails(details, previous.LogPath);
            await PreviousCrashDrawer.OpenAsync();
            PreviousCrashInfoBar.Message = PreviousCrashNoticeMessage;
        }
        catch (Exception ex)
        {
            ReportPreviousCrashViewerFailure(ex, "오류 기록을 열지 못했습니다. 잠시 후 다시 시도해 주세요.");
        }
        finally
        {
            _isOpeningDiagnosticDetails = false;
            if (System.Threading.Volatile.Read(ref _diagnosticsClosed) == 0)
            {
                try { PreviousCrashDetailsButton.IsEnabled = true; }
                catch (Exception ex) { RecordDiagnosticViewerFailure(ex); }
            }
        }
    }

    private async void PreviousCrashCloseButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (await PreviousCrashDrawer.CloseAsync())
                PreviousCrashInfoBar.Message = PreviousCrashNoticeMessage;
        }
        catch (Exception ex)
        {
            ReportPreviousCrashViewerFailure(ex, "오류 기록 패널을 닫지 못했습니다.");
        }
    }

    private void PreviousCrashDrawer_InteractionFailed(object? sender, DrawerInteractionFailedEventArgs e)
        => ReportPreviousCrashViewerFailure(e.Exception, "오류 기록 패널을 조작하지 못했습니다.");

    private void ReportPreviousCrashViewerFailure(Exception exception, string message)
    {
        // 기록 뷰어 자체의 실패로 오류 Dialog를 재귀적으로 띄우지 않습니다.
        RecordDiagnosticViewerFailure(exception);
        if (System.Threading.Volatile.Read(ref _diagnosticsClosed) != 0)
            return;
        try
        {
            PreviousCrashInfoBar.Message = message;
            PreviousCrashInfoBar.Visibility = Visibility.Visible;
            PreviousCrashInfoBar.IsOpen = true;
            UpdateShellNoticeHostVisibility();
        }
        catch (Exception presentationException) { RecordDiagnosticViewerFailure(presentationException); }
    }
}
