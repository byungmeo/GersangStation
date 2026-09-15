using Core;
using GersangStation.Diagnostics;
using GersangStation.Shared.Input;
using Microsoft.UI.Dispatching;
using System;
using System.Diagnostics;

namespace GersangStation.Services;

/// <summary>Connects the shared cursor engine to WinUI permissions, settings and diagnostics.</summary>
public sealed class ClipMouseService : IDisposable
{
    private readonly MouseConfinementService _engine;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private bool _isDisposed;

    public ClipMouseService(bool isEnabled)
    {
        _engine = new MouseConfinementService(new Progress<MouseConfinementDiagnostic>(OnDiagnostic));
        SetEnabled(isEnabled);
    }

    /// <summary>Preserves the WinUI administrator-only activation policy.</summary>
    public void SetEnabled(bool isEnabled)
        => _engine.SetEnabled(isEnabled && App.IsRunningAsAdministrator);

    /// <summary>Suspends correction while the user browses game windows.</summary>
    public void SetExternalSuspended(bool isSuspended) => _engine.SetExternalSuspended(isSuspended);

    public void Dispose()
    {
        _isDisposed = true;
        _engine.Dispose();
    }

    private void OnDiagnostic(MouseConfinementDiagnostic diagnostic)
    {
        Trace.TraceWarning("Mouse confinement: {0}: {1}", diagnostic.Operation, diagnostic.Exception);
        if (!diagnostic.MonitoringStopped)
            return;

        _dispatcher.TryEnqueue(() =>
        {
            if (_isDisposed || _engine.IsEnabled)
                return;
            SafeExecution.RunHandledAsync(() =>
            {
                AppDataManager.IsMouseConfinementEnabled = false;
                App.ExceptionHandler.ShowRecoverableAsync(diagnostic.Exception, "MouseConfinement." + diagnostic.Operation)
                    .FireAndForgetHandled("MouseConfinement.Diagnostic");
            }, "MouseConfinement.Stop").FireAndForgetHandled("MouseConfinement.Stop");
        });
    }
}
