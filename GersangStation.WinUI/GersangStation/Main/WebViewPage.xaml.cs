using Core;
using Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.System;

namespace GersangStation.Main;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class WebViewPage : Page, INotifyPropertyChanged, IDisposable
{
    bool _initialized = false;
    private bool _suppressUserSelectionChanged;
    private WebViewManager? _webviewManager;

    public ObservableCollection<Account> Accounts { get; } = [];

    private Account? _selectedAccount;

    public Account? SelectedAccount
    {
        get => _selectedAccount;
        set => SetProperty(ref _selectedAccount, value);
    }

    public WebViewPage()
    {
        InitializeComponent();
    }

    private void UpdateComboBox()
    {
        SelectedAccount = null;
        bool loggedIn = false;
        string loggedInId = string.Empty;
        if (_webviewManager is not null && _webviewManager.LoggedIn)
        {
            loggedIn = true;
            loggedInId = _webviewManager.LoggedInMemberId;
        }

        Accounts.Clear();
        IList<Account> accounts = AppDataManager.LoadAccounts();
        foreach (Account account in accounts)
        {
            Accounts.Add(account);
            if (loggedIn && account.Id == loggedInId)
            {
                _suppressUserSelectionChanged = true;
                SelectedAccount = account;
                _suppressUserSelectionChanged = false;
            }
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        UpdateComboBox();

        if (_initialized)
            return;
        _initialized = true;

        if (e.Parameter is MainWindow window)
        {
            _webviewManager = new WebViewManager(webview: WebView, window) ?? throw new NullReferenceException();
            _webviewManager.SourceChanged += OnSourceChanged;
            _webviewManager.LoggedInChanged += OnLoggedInChanged;
            window.RegisterWebViewManager(_webviewManager);
        }
    }

    private void OnLoggedInChanged(object? sender, EventArgs e)
    {
        UpdateComboBox();
    }

    public void Dispose()
    {
        if (_webviewManager is not null)
        {
            _webviewManager.SourceChanged -= OnSourceChanged;
            _webviewManager.LoggedInChanged -= OnLoggedInChanged;
            _webviewManager.Dispose();
            _webviewManager = null;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void Button_WebPreview_Back_Click(object sender, RoutedEventArgs e)
    {
        if (_webviewManager is not null && _webviewManager.CanGoBack)
        {
            _webviewManager.GoBack();
        }
    }

    private void Button_WebPreview_Forward_Click(object sender, RoutedEventArgs e)
    {
        if (_webviewManager is not null && _webviewManager.CanGoForward)
        {
            _webviewManager.GoForward();
        }
    }

    private void Button_WebPreview_Refresh_Click(object sender, RoutedEventArgs e)
    {
        _webviewManager?.Refresh();
    }

    private void Button_WebPreview_Home_Click(object sender, RoutedEventArgs e)
    {
        _webviewManager?.GoHome();
    }

    private async void ComboBox_Account_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUserSelectionChanged)
            return;

        if (ComboBox_Account.SelectedItem is Account account && _webviewManager is not null)
        {
            TryLoginResult result = await _webviewManager.TryLogin(account.Id);
        }
    }

    private string _committedUrlText = "";   // 마지막으로 “확정된” 주소창 텍스트(ESC 복구용)
    private bool _isUserEditing;             // 사용자가 편집 중인지(탭 이동/네비게이션 갱신과 구분)
    private void TextBox_Search_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        // Ctrl+L (주소창 포커스) — 필요하면 Page/Window 레벨에서 처리 권장
        if ((e.Key == VirtualKey.L) && (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)))
        {
            e.Handled = true;
            TextBox_Search.Focus(FocusState.Programmatic);
            TextBox_Search.SelectAll();
            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            TextBox_Search.Text = _committedUrlText;
            TextBox_Search.SelectAll();
            return;
        }

        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            CommitNavigateFromAddressBar();
            return;
        }
    }

    private void TextBox_Search_GotFocus(object sender, RoutedEventArgs e)
    {
        _isUserEditing = true;
        TextBox_Search.SelectAll();
    }

    private void TextBox_Search_LostFocus(object sender, RoutedEventArgs e)
    {
        _isUserEditing = false;

        // 포커스 빠지면 브라우저처럼 “현재 페이지 URL”로 정리해서 보여주기
        if (!string.IsNullOrWhiteSpace(_committedUrlText))
            TextBox_Search.Text = _committedUrlText;
    }

    private static Uri BuildSearchUri(string query)
    {
        // 예: Google 검색. 필요하면 엔진만 바꾸면 됨.
        string encoded = Uri.EscapeDataString(query);
        return new Uri($"https://www.google.com/search?q={encoded}");
    }

    private static Uri? BuildTargetUri(string input)
    {
        // 1) 이미 절대 URI면 그대로
        if (Uri.TryCreate(input, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute;
        }

        // 2) “도메인/호스트처럼 보이는” 케이스면 https:// 붙여서 시도
        //    - 점(.) 포함 (example.com)
        //    - localhost
        //    - 포트(:) 포함 (127.0.0.1:3000)
        bool looksLikeHost =
            input.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            input.Contains('.') ||
            input.Contains(':');

        if (looksLikeHost)
        {
            string candidate = "https://" + input;
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var withScheme))
                return withScheme;
        }

        // 3) 그 외는 검색으로 처리(브라우저 주소창과 유사)
        //    (원치 않으면 여기서 null 반환하면 됨)
        return BuildSearchUri(input);
    }

    private void CommitNavigateFromAddressBar()
    {
        string raw = (TextBox_Search.Text ?? "").Trim();
        if (raw.Length == 0)
            return;

        Uri? target = BuildTargetUri(raw);
        if (target is null)
            return;

        _committedUrlText = target.AbsoluteUri;
        _isUserEditing = false;

        // 여기서 WebView 이동 호출만 연결하면 됨.
        // WebView2면 WebView.Source = target; 또는 CoreWebView2.Navigate(target.AbsoluteUri) 등.
        WebView.Source = target;
    }

    private void OnSourceChanged(CoreWebView2 sender, CoreWebView2SourceChangedEventArgs args)
    {
        // 사용자가 주소창을 편집 중이면 덮어쓰지 않음
        if (_isUserEditing)
            return;

        // 실제 URL 반영 (WebView2 기준)
        string url = sender.Source;
        _committedUrlText = url;
        TextBox_Search.Text = url;
    }
}
