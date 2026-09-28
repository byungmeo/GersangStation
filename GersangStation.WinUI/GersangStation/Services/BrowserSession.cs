using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GersangStation.Services;

internal enum BrowserSessionState
{
    Created,
    Initializing,
    Ready,
    Faulted,
    Disposed
}

/// <summary>
/// MainWindow 수명의 단일 WebView2. 초기화, 문서 유효성, 메모리 정책과 종료를 소유합니다.
/// 모든 접근은 WebView2를 생성한 UI 스레드에서 수행합니다.
/// </summary>
internal sealed class BrowserSession : IDisposable
{
    private const int TargetViewportWidth = 1152;
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _initialization;
    private CoreWebView2? _core;
    private CoreWebView2MemoryUsageTargetLevel _memoryTarget = CoreWebView2MemoryUsageTargetLevel.Low;
    private bool _applyingViewport;
    private bool _viewportPending;
    private bool _viewportOverridden;

    internal BrowserSession(WebView2 view)
    {
        View = view;
        View.SizeChanged += OnSizeChanged;
    }

    internal WebView2 View { get; }
    internal BrowserSessionState State { get; private set; }
    internal Exception? Failure { get; private set; }
    internal bool IsReady => State == BrowserSessionState.Ready;
    internal bool IsDocumentReady { get; private set; }
    internal ulong NavigationId { get; private set; }
    internal CancellationToken LifetimeToken => _lifetime.Token;
    internal event EventHandler? Failed;

    internal Task InitializeAsync()
    {
        VerifyAccess();
        if (State == BrowserSessionState.Disposed)
            return Task.FromCanceled(_lifetime.Token);
        if (State == BrowserSessionState.Faulted)
            return Task.FromException(new InvalidOperationException("브라우저를 사용할 수 없습니다. 앱을 다시 실행해 주세요.", Failure));

        // 여러 호출자가 같은 초기화 결과를 기다리며, 실패도 다시 실행하지 않고 보존합니다.
        return _initialization ??= InitializeCoreAsync();
    }

    private async Task InitializeCoreAsync()
    {
        State = BrowserSessionState.Initializing;
        try
        {
            var options = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = "--disable-features=msSmartScreenProtection"
            };
            var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, null, options);
            _lifetime.Token.ThrowIfCancellationRequested();
            await View.EnsureCoreWebView2Async(environment);
            _lifetime.Token.ThrowIfCancellationRequested();

            _core = View.CoreWebView2 ?? throw new InvalidOperationException("WebView2 초기화 후 Core가 생성되지 않았습니다.");
            _core.Settings.AreDefaultScriptDialogsEnabled = false;
            _core.Settings.IsPasswordAutosaveEnabled = false;
            _core.NavigationStarting += OnNavigationStarting;
            _core.SourceChanged += OnSourceChanged;
            _core.DOMContentLoaded += OnDocumentLoaded;
            _core.NavigationCompleted += OnNavigationCompleted;
            _core.ProcessFailed += OnProcessFailed;
            State = BrowserSessionState.Ready;
            ApplyMemoryTarget();
            await ApplyViewportAsync();
        }
        catch (Exception) when (State == BrowserSessionState.Disposed)
        {
            throw new OperationCanceledException(_lifetime.Token);
        }
        catch (Exception ex)
        {
            Failure = ex;
            State = BrowserSessionState.Faulted;
            throw;
        }
    }

    internal void VerifyAccess()
    {
        if (!View.DispatcherQueue.HasThreadAccess)
            throw new InvalidOperationException("BrowserSession은 UI 스레드에서만 사용할 수 있습니다.");
    }

    internal CoreWebView2 GetReadyCore()
    {
        VerifyAccess();
        if (State == BrowserSessionState.Faulted)
            throw new InvalidOperationException("브라우저를 사용할 수 없습니다. 앱을 다시 실행해 주세요.", Failure);
        _lifetime.Token.ThrowIfCancellationRequested();
        if (!IsReady || _core is null)
            throw new InvalidOperationException("브라우저 준비가 완료되지 않았습니다.", Failure);
        return _core;
    }

    internal bool IsCurrentDocument(ulong navigationId)
        => IsReady && NavigationId == navigationId;

    internal void EnsureCurrentDocument(ulong navigationId)
    {
        if (!IsDocumentReady || !IsCurrentDocument(navigationId))
            throw new OperationCanceledException("브라우저 문서가 변경되었거나 세션이 종료되었습니다.");
    }

    internal async Task<string> ExecuteScriptAsync(string script)
    {
        var core = GetReadyCore();
        EnsureCurrentDocument(NavigationId);
        string result = await core.ExecuteScriptAsync(script).AsTask().WaitAsync(_lifetime.Token);
        // 탐색을 시작하는 스크립트도 있으므로 문서 일치 검사는 호출자가 작업 단계 사이에 수행합니다.
        return result;
    }

    internal void Navigate(string uri)
    {
        var core = GetReadyCore();
        IsDocumentReady = false;
        core.Navigate(uri);
    }

    internal void NavigateToHtml(string html)
    {
        var core = GetReadyCore();
        IsDocumentReady = false;
        core.NavigateToString(html);
    }

    internal void Reload()
    {
        var core = GetReadyCore();
        IsDocumentReady = false;
        core.Reload();
    }

    internal void SetMemoryTarget(CoreWebView2MemoryUsageTargetLevel target)
    {
        VerifyAccess();
        _memoryTarget = target;
        if (IsReady)
            ApplyMemoryTarget();
    }

    private void ApplyMemoryTarget()
    {
        try
        {
            if (_core is not null && _core.MemoryUsageTargetLevel != _memoryTarget)
                _core.MemoryUsageTargetLevel = _memoryTarget;
        }
        catch (Exception ex)
        {
            // 메모리 힌트 실패는 문서나 세션의 사용 가능 여부를 바꾸지 않습니다.
            Debug.WriteLine($"[BrowserSession] Memory target: {ex}");
        }
    }

    private void OnNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        NavigationId = args.NavigationId;
        IsDocumentReady = false;
    }

    private void OnDocumentLoaded(CoreWebView2 sender, CoreWebView2DOMContentLoadedEventArgs args)
    {
        if (IsCurrentDocument(args.NavigationId))
            IsDocumentReady = true;
    }

    private void OnSourceChanged(CoreWebView2 sender, CoreWebView2SourceChangedEventArgs args)
    {
        // fragment/history 이동은 새 DOMContentLoaded 없이 끝납니다.
        if (IsReady && !args.IsNewDocument)
            IsDocumentReady = true;
    }

    private async void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        => await ApplyViewportAsync();

    private async void OnSizeChanged(object sender, SizeChangedEventArgs args)
        => await ApplyViewportAsync();

    private void OnProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs args)
    {
        Debug.WriteLine($"[BrowserSession] ProcessFailed: {args.ProcessFailedKind}");
        // GPU/utility 프로세스는 런타임이 복구합니다. 문서를 잃은 경우만 세션 사용을 중단합니다.
        if (!IsReady || args.ProcessFailedKind is not (CoreWebView2ProcessFailedKind.BrowserProcessExited
            or CoreWebView2ProcessFailedKind.RenderProcessExited))
            return;

        Failure = new InvalidOperationException($"브라우저 프로세스가 종료되었습니다 ({args.ProcessFailedKind}). 앱을 다시 실행해 주세요.");
        State = BrowserSessionState.Faulted;
        IsDocumentReady = false;
        _lifetime.Cancel();
        Failed?.Invoke(this, EventArgs.Empty);
    }

    private async Task ApplyViewportAsync()
    {
        if (!IsReady)
            return;
        if (_applyingViewport)
        {
            _viewportPending = true;
            return;
        }

        _applyingViewport = true;
        try
        {
            do
            {
                _viewportPending = false;
                if (!IsReady || View.ActualWidth <= 0 || View.ActualHeight <= 0)
                    return;
                var core = GetReadyCore();
                if (View.ActualWidth >= TargetViewportWidth)
                {
                    if (_viewportOverridden)
                    {
                        await core.CallDevToolsProtocolMethodAsync("Emulation.clearDeviceMetricsOverride", "{}");
                        _viewportOverridden = false;
                    }
                    continue;
                }

                double scale = View.ActualWidth / TargetViewportWidth;
                string parameters = JsonSerializer.Serialize(new
                {
                    width = TargetViewportWidth,
                    height = Math.Max(1, (int)Math.Round(View.ActualHeight / scale)),
                    deviceScaleFactor = 0,
                    mobile = false,
                    scale,
                    dontSetVisibleSize = true
                });
                await core.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride", parameters);
                _viewportOverridden = true;
            }
            while (_viewportPending);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BrowserSession] Viewport: {ex}");
        }
        finally
        {
            _applyingViewport = false;
        }
    }

    public void Dispose()
    {
        VerifyAccess();
        if (State == BrowserSessionState.Disposed)
            return;
        State = BrowserSessionState.Disposed;
        IsDocumentReady = false;
        _lifetime.Cancel();
        View.SizeChanged -= OnSizeChanged;
        if (_core is not null)
        {
            _core.NavigationStarting -= OnNavigationStarting;
            _core.SourceChanged -= OnSourceChanged;
            _core.DOMContentLoaded -= OnDocumentLoaded;
            _core.NavigationCompleted -= OnNavigationCompleted;
            _core.ProcessFailed -= OnProcessFailed;
            _core = null;
        }
        View.Close();
        // 비동기 continuation이 LifetimeToken을 읽을 수 있어 CTS는 여기서 Dispose하지 않습니다.
    }
}
