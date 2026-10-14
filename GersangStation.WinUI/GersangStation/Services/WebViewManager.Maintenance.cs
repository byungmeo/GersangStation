using Microsoft.UI.Xaml.Controls;
using GersangStation.Diagnostics;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GersangStation.Services;

public sealed partial class WebViewManager
{
    private bool _isMaintenanceInProgress;
    public bool IsMaintenanceInProgress
    {
        get => _isMaintenanceInProgress;
        private set
        {
            if (_isMaintenanceInProgress == value) return;
            _isMaintenanceInProgress = value;
            OnPropertyChanged(nameof(IsMaintenanceInProgress));
        }
    }

    /// <summary>
    /// Quiesce the shared browser, leave its document at about:blank, then clear only its profile.
    /// Do not delete the user data directory or touch app accounts, PasswordVault or running games.
    /// </summary>
    internal async Task ClearBrowsingDataAsync(bool allData)
    {
        _session.VerifyAccess();
        if (IsMaintenanceInProgress) throw new InvalidOperationException("브라우저 데이터 정리가 이미 진행 중입니다.");
        _lifetime.Token.ThrowIfCancellationRequested();

        IsMaintenanceInProgress = true; // Also cancels preflight launch attempts in the coordinator.
        try
        {
            CancelLaunchAttempt(_cachedGameStartServer, _cachedGameStartClientIndex, "브라우저 데이터 정리");
            await _session.SuspendOperationsAsync();
            // An initialization already in flight must not start a delayed home navigation.
            await InitializeAsync();
            _initialHomeNavigationCompleted = true;
            await _session.ClearProfileAsync(allData);
        }
        finally
        {
            // Even a partially failed deletion invalidates the old login snapshot.
            LoggedIn = false;
            LoggedInMemberId = string.Empty;
            _wasRoughLoggedIn = false;
            _pendingNavigationUri = null;
            _pendingHtmlContent = null;
            _isDisplayingHtmlDocument = false;
            _session.ResumeOperations();
            IsBusy = false;
            IsMaintenanceInProgress = false;
            LoggedInChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<ContentDialogResult> ShowBrowserDialogAsync(ContentDialog dialog,
        GameLaunchAttempt? attempt = null)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _session.OperationToken, _lifetime.Token, attempt?.Token ?? CancellationToken.None);
        return await dialog.ShowManagedAsync(cancellation.Token);
    }

    private void QueueBrowserWork(Func<Task> action, string context)
    {
        // Dispatcher queues can outlive the document/cleanup that scheduled them.
        CancellationToken generation = _session.OperationToken;
        _webview.DispatcherQueue.TryEnqueueHandled(async () =>
        {
            generation.ThrowIfCancellationRequested();
            await _session.RunOperationAsync(action);
        }, context);
    }
}
