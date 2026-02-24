using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace GersangStation;

internal sealed partial class WebViewManager : IDisposable, INotifyPropertyChanged
{
    private const string Url_Gersang_Main = "https://www.gersang.co.kr/main/index.gs";

    private WebView2? _webview;
    public string _currentSource = "";
    public string _currentTitle = "";
    private bool _canGoBack = false;
    private bool _canGoForward = false;

    public string CurrentSource
    {
        get => _currentSource;
        private set
        {
            if (_currentSource != value)
            {
                _currentSource = value;
                OnPropertyChanged(nameof(CurrentSource));
            }
        }
    }
    public string CurrentTitle
    {
        get => _currentTitle;
        private set
        {
            if (_currentTitle != value)
            {
                _currentTitle = value;
                OnPropertyChanged(nameof(CurrentTitle));
            }
        }
    }

    public bool CanGoBack
    {
        get => _canGoBack;
        private set
        {
            if (_canGoBack != value)
            {
                _canGoBack = value;
                OnPropertyChanged(nameof(CanGoBack));
            }
        }
    }

    public bool CanGoForward
    {
        get => _canGoForward;
        private set
        {
            if (_canGoForward != value)
            {
                _canGoForward = value;
                OnPropertyChanged(nameof(CanGoForward));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public WebViewManager(WebView2 webview)
    {
        _webview = webview;
        InitWebView();
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        Debug.WriteLine($"[WebViewManager::Dispose]");
        UnsubscribeWebViewEvents();
        UnsubscribeCoreEvents();
        _webview?.Close();
        _webview = null;
    }

    private async void InitWebView()
    {
        if (_webview is null) return;

        SubscribeWebViewEvents();
        await _webview.EnsureCoreWebView2Async();
        SubscribeCoreEvents();
        _webview.Source = new Uri(Url_Gersang_Main);
    }

    private void SubscribeWebViewEvents()
    {
        if (_webview is null) return;

        _webview.CoreWebView2Initialized += OnCoreWebView2Initialized;
        _webview.NavigationStarting += OnNavigationStarting;
        _webview.NavigationCompleted += OnNavigationCompleted;
        _webview.WebMessageReceived += OnWebMessageReceived;
    }

    private void UnsubscribeWebViewEvents()
    {
        if (_webview is null) return;

        _webview.CoreWebView2Initialized -= OnCoreWebView2Initialized;
        _webview.NavigationStarting -= OnNavigationStarting;
        _webview.NavigationCompleted -= OnNavigationCompleted;
        _webview.WebMessageReceived -= OnWebMessageReceived;
    }

    private void SubscribeCoreEvents()
    {
        var core = _webview?.CoreWebView2;
        if (core is null) return;

        core.SourceChanged += OnSourceChanged;
        core.HistoryChanged += OnHistoryChanged;
        core.DOMContentLoaded += OnDOMContentLoaded;
        core.ScriptDialogOpening += OnScriptDialogOpening;
        core.NotificationReceived += OnNotificationReceived;
    }

    private void UnsubscribeCoreEvents()
    {
        var core = _webview?.CoreWebView2;
        if (core is null) return;

        core.HistoryChanged -= OnHistoryChanged;
        core.DOMContentLoaded -= OnDOMContentLoaded;
        core.ScriptDialogOpening -= OnScriptDialogOpening;
        core.NotificationReceived -= OnNotificationReceived;
    }

    private void OnCoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
    {
        Debug.WriteLine($"[WebView::OnCoreWebView2Initialized]");
        if (args.Exception is null)
        {
            Debug.WriteLine("\t- Initialization succeeded.");
            return;
        }
        Debug.WriteLine($"\t- Exception.Message: {args.Exception.Message}");
        Debug.WriteLine($"\t- Exception.StackTrace: {args.Exception.StackTrace}");
        Debug.WriteLine($"\t- Exception.Source: {args.Exception.Source}");
        Debug.WriteLine($"\t- Exception.Data");
        foreach (var key in args.Exception.Data.Keys)
        {
            Debug.WriteLine($"\t\t- [{key}]: {args.Exception.Data[key]}");
        }
    }

    private void OnWebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        Debug.WriteLine($"[WebView::OnWebMessageReceived]");
        Debug.WriteLine($"\t- Source: {args.Source}");
        Debug.WriteLine($"\t- AdditionalObjects.Count: {args.AdditionalObjects.Count}");
        Debug.WriteLine($"\t- WebMessageAsJson: {args.WebMessageAsJson}");
    }

    private void OnNotificationReceived(CoreWebView2 sender, CoreWebView2NotificationReceivedEventArgs args)
    {
        Debug.WriteLine($"[WebView::OnNotificationReceived]");
        Debug.WriteLine($"\t- SenderOrigin: {args.SenderOrigin}");
        Debug.WriteLine($"\t- Notification");
        Debug.WriteLine($"\t\t- Timestamp: {args.Notification.Timestamp}");
        Debug.WriteLine($"\t\t- Tag: {args.Notification.Tag}");
        Debug.WriteLine($"\t\t- Title: {args.Notification.Title}");
        Debug.WriteLine($"\t\t- Body: {args.Notification.Body}");
        Debug.WriteLine($"\t\t- Language: {args.Notification.Language}");
        Debug.WriteLine($"\t\t- ShouldRenotify: {args.Notification.ShouldRenotify}");
        Debug.WriteLine($"\t\t- IsSilent: {args.Notification.IsSilent}");
        Debug.WriteLine($"\t\t- RequiresInteraction: {args.Notification.RequiresInteraction}");
        Debug.WriteLine($"\t\t- VibrationPattern.Count: {args.Notification.VibrationPattern.Count}");
        Debug.WriteLine($"\t\t- IconUri: {args.Notification.IconUri}");
        Debug.WriteLine($"\t\t- BodyImageUri: {args.Notification.BodyImageUri}");
        Debug.WriteLine($"\t\t- BadgeUri: {args.Notification.BadgeUri}");
    }

    private void OnSourceChanged(CoreWebView2 sender, CoreWebView2SourceChangedEventArgs args)
    {
        Debug.WriteLine($"[WebView::OnSourceChanged]");
        Debug.WriteLine($"\t- IsNewDocument: {args.IsNewDocument}");
        Debug.WriteLine($"\t- Source: {sender.Source}");
        CurrentTitle = sender.DocumentTitle;
        CurrentSource = sender.Source;
    }

    private void OnHistoryChanged(CoreWebView2 sender, object args)
    {
        Debug.WriteLine($"[WebView::OnHistoryChanged]");
        Debug.WriteLine($"\t- CanGoBack: {sender.CanGoBack}");
        Debug.WriteLine($"\t- CanGoForward: {sender.CanGoForward}");
        CanGoBack = sender.CanGoBack;
        CanGoForward = sender.CanGoForward;
    }

    private void OnDOMContentLoaded(CoreWebView2 sender, CoreWebView2DOMContentLoadedEventArgs args)
    {
        Debug.WriteLine($"[WebView::OnDOMContentLoaded]");
        Debug.WriteLine($"\t- NavigationId: {args.NavigationId}");
        Debug.WriteLine($"\t- CoreWebView2.Source: {sender.Source}");
        Debug.WriteLine($"\t- CoreWebView2.DocumentTitle: {sender.DocumentTitle}");
        Debug.WriteLine($"\t- CoreWebView2.StatusBarText: {sender.StatusBarText}");
    }

    private void OnScriptDialogOpening(CoreWebView2 sender, CoreWebView2ScriptDialogOpeningEventArgs args)
    {
        Debug.WriteLine($"[WebView::OnScriptDialogOpening]");
        Debug.WriteLine($"\t- Kind: {args.Kind}");
        Debug.WriteLine($"\t- Message: {args.Message}");
        Debug.WriteLine($"\t- DefaultText: {args.DefaultText}");
        Debug.WriteLine($"\t- ResultText: {args.ResultText}");
        Debug.WriteLine($"\t- Uri: {args.Uri}");
    }

    private void OnNavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        Debug.WriteLine($"[WebView::OnNavigationStarting]");
        Debug.WriteLine($"\t- NavigationId: {args.NavigationId}");
        Debug.WriteLine($"\t- NavigationKind: {args.NavigationKind}");
        Debug.WriteLine($"\t- Uri: {args.Uri}");
        Debug.WriteLine($"\t- IsRedirected: {args.IsRedirected}");
        Debug.WriteLine($"\t- IsUserInitiated: {args.IsUserInitiated}");
        Debug.WriteLine($"\t- RequestHeaders");
        foreach (var kvp in args.RequestHeaders)
        {
            Debug.WriteLine($"\t\t- [{kvp.Key}, {kvp.Value}]");
        }
    }

    private void OnNavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        Debug.WriteLine($"[WebView::OnNavigationCompleted]");
        Debug.WriteLine($"\t- NavigationId: {args.NavigationId}");
        Debug.WriteLine($"\t- IsSuccess: {args.IsSuccess}");
        Debug.WriteLine($"\t- HttpStatusCode: {args.HttpStatusCode}");
        Debug.WriteLine($"\t- WebErrorStatus: {args.WebErrorStatus}");
    }

    internal void GoBack()
    {
        _webview?.GoBack();
    }

    internal void GoForward()
    {
        _webview?.GoForward();
    }

    internal void Refresh()
    {
        _webview?.Reload();
    }

    internal void GoHome()
    {
        if (_webview is not null)
        {
            _webview.Source = new Uri(Url_Gersang_Main);
        }
    }
}
