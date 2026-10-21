using GersangStation.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SideDrawer.Controls;
using System;
using System.Threading;

namespace GersangStation.Main;

public sealed partial class MainWindow
{
    private bool _recoverableErrorsAttached;
    private int _diagnosticsClosed;
    private int _recoverableRefreshQueued;
    private Guid? _lastNotifiedRecordId;
    private DiagnosticRecord? _selectedRecoverableRecord;
    private bool _updatingRecoverableSelection;
    private bool _isOpeningDiagnosticDetails;

    private void AttachRecoverableErrors()
    {
        if (_recoverableErrorsAttached)
            return;
        _recoverableErrorsAttached = true;
        App.ExceptionHandler.RecoverableErrors.Changed += RecoverableErrors_Changed;
        // 창 로드 전에 생긴 기록도 같은 예약 경로로 안내합니다.
        RecoverableErrors_Changed(this, EventArgs.Empty);
    }

    private void DetachRecoverableErrors()
    {
        Interlocked.Exchange(ref _diagnosticsClosed, 1);
        App.ExceptionHandler.RecoverableErrors.Changed -= RecoverableErrors_Changed;
    }

    private void RecoverableErrors_Changed(object? sender, EventArgs e)
    {
        if (Volatile.Read(ref _diagnosticsClosed) != 0 ||
            Interlocked.Exchange(ref _recoverableRefreshQueued, 1) != 0)
            return;

        try
        {
            if (!DispatcherQueue.TryEnqueue(() =>
            {
                Interlocked.Exchange(ref _recoverableRefreshQueued, 0);
                if (Volatile.Read(ref _diagnosticsClosed) != 0)
                    return;
                try { RefreshRecoverableErrors(); }
                catch (Exception ex) { RecordDiagnosticViewerFailure(ex); }
            }))
            {
                Interlocked.Exchange(ref _recoverableRefreshQueued, 0);
                RecordDiagnosticViewerFailure(new InvalidOperationException("오류 안내 UI 예약이 거부되었습니다."));
            }
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _recoverableRefreshQueued, 0);
            RecordDiagnosticViewerFailure(ex);
        }
    }

    private void RefreshRecoverableErrors()
    {
        DiagnosticRecord[] records = App.ExceptionHandler.RecoverableErrors.Snapshot();
        _updatingRecoverableSelection = true;
        try
        {
            RecoverableErrorSelector.ItemsSource = records;
            // 최근 목록에서 밀려나도 현재 읽는 스냅샷 하나와 본문은 그대로 보존합니다.
            RecoverableErrorSelector.SelectedItem = Array.Find(records, r => r.Id == _selectedRecoverableRecord?.Id);
        }
        finally { _updatingRecoverableSelection = false; }

        if (records.Length == 0 || records[0].Id == _lastNotifiedRecordId)
            return;
        _lastNotifiedRecordId = records[0].Id;
        RecoverableErrorInfoBar.Message = records[0].Summary;
        RecoverableErrorInfoBar.Visibility = Visibility.Visible;
        RecoverableErrorInfoBar.IsOpen = true;
        UpdateDiagnosticNoticeHostVisibility();
    }

    private void UpdateDiagnosticNoticeHostVisibility()
        => DiagnosticNoticeHost.Visibility = PreviousCrashInfoBar.IsOpen || RecoverableErrorInfoBar.IsOpen
            ? Visibility.Visible : Visibility.Collapsed;

    private void RecoverableErrorInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        RecoverableErrorInfoBar.Visibility = Visibility.Collapsed;
        UpdateDiagnosticNoticeHostVisibility();
    }

    private async void RecoverableErrorDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isOpeningDiagnosticDetails || !RecoverableErrorDrawer.IsLoaded)
            return;
        _isOpeningDiagnosticDetails = true;
        try
        {
            if (!await PreviousCrashDrawer.CloseAsync() || Volatile.Read(ref _diagnosticsClosed) != 0)
                return;
            RefreshRecoverableErrors();
            DiagnosticRecord[] records = App.ExceptionHandler.RecoverableErrors.Snapshot();
            if (records.Length == 0)
                return;
            SelectRecoverableRecord(records[0]);
            if (RecoverableErrorDrawer.State == DrawerState.Closed)
                await RecoverableErrorDrawer.OpenAsync();
        }
        catch (Exception ex) { ReportRecoverableViewerFailure(ex, "오류 기록을 열지 못했습니다. 잠시 후 다시 시도해 주세요."); }
        finally { _isOpeningDiagnosticDetails = false; }
    }

    private void RecoverableErrorSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingRecoverableSelection || RecoverableErrorSelector.SelectedItem is not DiagnosticRecord record)
            return;
        try { SelectRecoverableRecord(record); }
        catch (Exception ex) { ReportRecoverableViewerFailure(ex, "선택한 기록을 표시하지 못했습니다."); }
    }

    private void SelectRecoverableRecord(DiagnosticRecord record)
    {
        _selectedRecoverableRecord = record;
        _updatingRecoverableSelection = true;
        try { RecoverableErrorSelector.SelectedItem = record; }
        finally { _updatingRecoverableSelection = false; }
        SelectedRecoverableRecordTextBlock.Text = record.DisplayName;
        RecoverableErrorDetailsView.DisplayDetails(record.Details, record.LogPath);
    }

    private async void RecoverableErrorCloseButton_Click(object sender, RoutedEventArgs e)
    {
        try { await RecoverableErrorDrawer.CloseAsync(); }
        catch (Exception ex) { ReportRecoverableViewerFailure(ex, "오류 기록 패널을 닫지 못했습니다."); }
    }

    private void RecoverableErrorDrawer_InteractionFailed(object? sender, DrawerInteractionFailedEventArgs e)
        => ReportRecoverableViewerFailure(e.Exception, "오류 기록 패널을 조작하지 못했습니다.");

    private void ReportRecoverableViewerFailure(Exception exception, string message)
    {
        RecordDiagnosticViewerFailure(exception);
        if (Volatile.Read(ref _diagnosticsClosed) != 0)
            return;
        try
        {
            RecoverableErrorInfoBar.Message = message;
            RecoverableErrorInfoBar.Visibility = Visibility.Visible;
            RecoverableErrorInfoBar.IsOpen = true;
            UpdateDiagnosticNoticeHostVisibility();
        }
        catch (Exception presentationException) { RecordDiagnosticViewerFailure(presentationException); }
    }

    private static void RecordDiagnosticViewerFailure(Exception exception)
        => App.ExceptionHandler.RecordRecoverable(exception, "MainWindow.DiagnosticViewer");
}
