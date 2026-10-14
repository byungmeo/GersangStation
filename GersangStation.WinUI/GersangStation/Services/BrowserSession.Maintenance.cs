using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GersangStation.Services;

internal sealed partial class BrowserSession
{
    private CancellationTokenSource _workCancellation;
    private readonly AsyncLocal<CancellationToken?> _operationContext = new();
    private readonly HashSet<Task> _operations = [];
    private readonly HashSet<CoreWebView2DownloadOperation> _downloads = [];
    private bool _allowBlankNavigation;

    internal bool IsSuspended { get; private set; }
    internal CancellationToken OperationToken => _operationContext.Value ?? _workCancellation.Token;

    private void EnsureOperationsAllowed()
    {
        VerifyAccess();
        OperationToken.ThrowIfCancellationRequested();
        _lifetime.Token.ThrowIfCancellationRequested();
        if (IsSuspended)
            throw new OperationCanceledException("브라우저 데이터 정리 중입니다.");
    }

    internal Task RunOperationAsync(Func<Task> action)
        => RunOperationAsync(async () => { await action(); return true; });

    internal async Task<T> RunOperationAsync<T>(Func<Task<T>> action)
    {
        EnsureOperationsAllowed();
        // Nested login/logout/launch callbacks belong to the same cancellation generation.
        if (_operationContext.Value.HasValue)
            return await action();

        Task<T> operation = ExecuteAsync();
        _operations.Add(operation);
        try { return await operation; }
        finally { _operations.Remove(operation); }

        async Task<T> ExecuteAsync()
        {
            _operationContext.Value = _workCancellation.Token;
            try
            {
                T result = await action();
                OperationToken.ThrowIfCancellationRequested();
                return result;
            }
            finally { _operationContext.Value = null; }
        }
    }

    internal async Task SuspendOperationsAsync()
    {
        VerifyAccess();
        IsSuspended = true;
        IsDocumentReady = false;
        _workCancellation.Cancel();
        _core?.Stop();
        foreach (var download in _downloads.ToArray())
            if (download.State == CoreWebView2DownloadState.InProgress) download.Cancel();

        // Wait for canceled dialogs (including their deferrals), scripts and socket listeners
        // to unwind before touching the profile. Their original callers own error reporting.
        foreach (Task operation in _operations.ToArray())
        {
            try { await operation; }
            catch (Exception) { }
        }
    }

    internal void ResumeOperations()
    {
        VerifyAccess();
        var previous = _workCancellation;
        _workCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        previous.Dispose();
        IsSuspended = false;
    }

    internal async Task ClearProfileAsync(bool allData)
    {
        if (!IsSuspended) throw new InvalidOperationException("브라우저 작업을 먼저 중단해야 합니다.");
        await InitializeAsync();
        var core = GetCoreForMaintenance();
        await NavigateToBlankForMaintenanceAsync(core);
        // Persistent browsing history and the live Back/Forward stack are separate.
        // Do not let Back restore an old authenticated document after a cleanup.
        await core.CallDevToolsProtocolMethodAsync("Page.resetNavigationHistory", "{}");

        var profile = core.Profile;
        if (allData)
        {
            await profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllProfile);
            // Explicitly reset persisted origin permissions, including runtimes where Settings
            // clearing does not reset every permission kind.
            foreach (var permission in await profile.GetNonDefaultPermissionSettingsAsync())
                await profile.SetPermissionStateAsync(permission.PermissionKind, permission.PermissionOrigin,
                    CoreWebView2PermissionState.Default);
            await core.ClearServerCertificateErrorActionsAsync();
        }
        else
        {
            // AllSite includes cookies, DOM storage, CacheStorage and service workers.
            await profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllSite
                | CoreWebView2BrowsingDataKinds.DiskCache
                | CoreWebView2BrowsingDataKinds.BrowsingHistory
                | CoreWebView2BrowsingDataKinds.DownloadHistory);
        }
        _lifetime.Token.ThrowIfCancellationRequested();
        // Full profile reset must not undo application-owned safety settings.
        core.Settings.AreDefaultScriptDialogsEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false;
    }

    private void OnDownloadStarting(CoreWebView2 sender, CoreWebView2DownloadStartingEventArgs args)
    {
        if (IsSuspended) { args.Cancel = true; return; }
        if (_downloads.Add(args.DownloadOperation))
            args.DownloadOperation.StateChanged += OnDownloadStateChanged;
    }

    private void OnDownloadStateChanged(CoreWebView2DownloadOperation sender, object args)
    {
        if (sender.State == CoreWebView2DownloadState.InProgress) return;
        sender.StateChanged -= OnDownloadStateChanged;
        _downloads.Remove(sender);
    }
}
