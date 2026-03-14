using Core;
using Core.Models;
using GersangStation.Diagnostics;
using GersangStation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;

namespace GersangStation.Main;

/// <summary>
/// 브라우저 페이지 진입 시 함께 전달할 초기 URL 정보를 나타냅니다.
/// </summary>
public sealed record WebViewPageNavigationParameter(string Url);

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class WebViewPage : Page, INotifyPropertyChanged, IDisposable
{
    private const string FavoriteFaviconFolderName = "browser-favicons";
    bool _initialized = false;
    private bool _isCurrentPageFavorited;
    private bool _suppressUserSelectionChanged;
    private WebViewManager? _webviewManager;

    public ObservableCollection<Account> Accounts { get; } = [];
    public ObservableCollection<BrowserFavorite> Favorites { get; } = [];

    public Visibility FavoritesVisibility => Favorites.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    public string FavoriteButtonGlyph => _isCurrentPageFavorited ? "\uE735" : "\uE734";
    public string FavoriteButtonToolTip => _isCurrentPageFavorited ? "현재 페이지 즐겨찾기 해제" : "현재 페이지 즐겨찾기 추가";

    private Account? _selectedAccount;

    public Account? SelectedAccount
    {
        get => _selectedAccount;
        set => SetProperty(ref _selectedAccount, value);
    }

    public WebViewPage()
    {
        InitializeComponent();
        Favorites.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(FavoritesVisibility));
            UpdateFavoriteButtonState();
        };
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
            if (loggedIn && LoginIdComparer.EqualsForComparison(account.Id, loggedInId))
            {
                _suppressUserSelectionChanged = true;
                SelectedAccount = account;
                _suppressUserSelectionChanged = false;
            }
        }
    }

    /// <summary>
    /// 페이지 진입 시 WebViewManager를 초기화하고, 필요하면 전달받은 URL로 이동합니다.
    /// </summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        UpdateComboBox();
        _ = LoadFavoritesAsync();

        MainWindow? window = e.Parameter as MainWindow ?? App.CurrentWindow as MainWindow;
        if (!_initialized && window is not null)
        {
            _initialized = true;
            _webviewManager = new WebViewManager(webview: WebView, window, window.GameStarter) ?? throw new NullReferenceException();
            _webviewManager.SourceChanged += OnSourceChanged;
            _webviewManager.LoggedInChanged += OnLoggedInChanged;
            window.RegisterWebViewManager(_webviewManager);
        }

        if (e.Parameter is WebViewPageNavigationParameter parameter
            && Uri.TryCreate(parameter.Url, UriKind.Absolute, out Uri? targetUri))
        {
            _webviewManager?.Navigate(targetUri);
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

    private async void Button_Favorite_Click(object sender, RoutedEventArgs e)
    {
        Uri? currentUri = GetCurrentPageUri();
        if (currentUri is null)
            return;

        BrowserFavorite? existingFavorite = FindFavoriteByUrl(currentUri.AbsoluteUri);
        List<BrowserFavorite> previousFavorites = SnapshotFavorites();
        if (existingFavorite is not null)
        {
            int existingIndex = Favorites.IndexOf(existingFavorite);
            if (existingIndex < 0)
                return;

            Favorites.RemoveAt(existingIndex);
            await PersistFavoritesAsync(previousFavorites, existingFavorite);
            return;
        }

        string? name = await ShowFavoriteNameDialogAsync(
            title: "즐겨찾기 추가",
            primaryButtonText: "추가",
            initialName: GetSuggestedFavoriteName(currentUri));

        if (string.IsNullOrWhiteSpace(name))
            return;

        string faviconUrl = await CaptureCurrentPageFaviconAsync(currentUri);
        Favorites.Add(new BrowserFavorite(name, currentUri.AbsoluteUri, faviconUrl));
        await PersistFavoritesAsync(previousFavorites);
    }

    private async void Button_WebPreview_LaunchExternal_Click(object sender, RoutedEventArgs e)
    {
        Uri? targetUri = WebView.Source;
        if (targetUri is null)
            return;

        await Launcher.LaunchUriAsync(targetUri);
    }

    private async void ComboBox_Account_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUserSelectionChanged)
            return;

        if (ComboBox_Account.SelectedItem is Account account && _webviewManager is not null)
        {
            TryLoginResult result = await _webviewManager.TryLogin(account.Id);
            if (result == TryLoginResult.NotFoundPw)
            {
                await ShowSimpleDialogAsync(
                    "비밀번호가 필요합니다",
                    $"계정 '{account.DisplayNickname}'의 저장된 비밀번호를 찾지 못했습니다.\n계정 설정에서 비밀번호를 다시 입력해 주세요.");
            }
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

        _webviewManager?.Navigate(target);
        _ = WebView.Focus(FocusState.Programmatic);
    }

    private void FavoriteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not BrowserFavorite favorite)
            return;

        if (!TryCreateBrowsableUri(favorite.Url, out Uri? targetUri))
            return;

        _webviewManager?.Navigate(targetUri!);
        _ = WebView.Focus(FocusState.Programmatic);
    }

    private async void FavoriteRename_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not BrowserFavorite favorite)
            return;

        int existingIndex = Favorites.IndexOf(favorite);
        if (existingIndex < 0)
            return;

        string? name = await ShowFavoriteNameDialogAsync(
            title: "즐겨찾기 이름 변경",
            primaryButtonText: "저장",
            initialName: favorite.Name);

        if (string.IsNullOrWhiteSpace(name))
            return;

        List<BrowserFavorite> previousFavorites = SnapshotFavorites();
        Favorites[existingIndex] = new BrowserFavorite(name, favorite.Url, favorite.FaviconUrl);
        await PersistFavoritesAsync(previousFavorites);
    }

    private async void FavoriteDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not BrowserFavorite favorite)
            return;

        int existingIndex = Favorites.IndexOf(favorite);
        if (existingIndex < 0)
            return;

        List<BrowserFavorite> previousFavorites = SnapshotFavorites();
        Favorites.RemoveAt(existingIndex);
        await PersistFavoritesAsync(previousFavorites, favorite);
    }

    /// <summary>
    /// 저장된 즐겨찾기 목록을 페이지 컬렉션으로 반영합니다.
    /// </summary>
    private async Task LoadFavoritesAsync()
    {
        (IList<BrowserFavorite> favorites, AppDataManager.AppDataOperationResult result) = await AppDataManager.LoadBrowserFavoritesAsync();

        Favorites.Clear();
        foreach (BrowserFavorite favorite in favorites)
            Favorites.Add(favorite);

        UpdateFavoriteButtonState();

        if (!result.Success)
        {
            await AppDataOperationDialog.ShowFailureAsync(
                XamlRoot,
                "즐겨찾기 불러오기 실패",
                "저장된 즐겨찾기 목록을 모두 불러오지 못했습니다.",
                result);
        }
    }

    /// <summary>
    /// 변경된 즐겨찾기 목록을 저장하고, 실패 시 이전 스냅샷으로 되돌립니다.
    /// </summary>
    private async Task PersistFavoritesAsync(IList<BrowserFavorite> previousFavorites, BrowserFavorite? removedFavorite = null)
    {
        AppDataManager.AppDataOperationResult result = await AppDataManager.SaveBrowserFavoritesAsync(Favorites);
        if (result.Success)
        {
            if (removedFavorite is not null)
                await DeleteFavoriteFaviconFileIfNeededAsync(removedFavorite);

            UpdateFavoriteButtonState();
            return;
        }

        ReplaceFavorites(previousFavorites);
        await AppDataOperationDialog.ShowFailureAsync(
            XamlRoot,
            "즐겨찾기 저장 실패",
            "즐겨찾기 정보를 저장하지 못했습니다.",
            result);
    }

    private List<BrowserFavorite> SnapshotFavorites()
    {
        List<BrowserFavorite> snapshot = [];
        foreach (BrowserFavorite favorite in Favorites)
            snapshot.Add(new BrowserFavorite(favorite.Name, favorite.Url, favorite.FaviconUrl));

        return snapshot;
    }

    private void ReplaceFavorites(IList<BrowserFavorite> favorites)
    {
        Favorites.Clear();
        foreach (BrowserFavorite favorite in favorites)
            Favorites.Add(new BrowserFavorite(favorite.Name, favorite.Url, favorite.FaviconUrl));
    }

    private async Task<string?> ShowFavoriteNameDialogAsync(string title, string primaryButtonText, string initialName)
    {
        TextBox input = new()
        {
            Text = initialName,
            PlaceholderText = "즐겨찾기 이름"
        };

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = title,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Primary,
            Content = input
        };

        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(input.Text))
                args.Cancel = true;
        };

        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? input.Text.Trim() : null;
    }

    private async Task ShowSimpleDialogAsync(string title, string message)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "확인",
            DefaultButton = ContentDialogButton.Close
        };

        await dialog.ShowAsync();
    }

    private BrowserFavorite? FindFavoriteByUrl(string absoluteUrl)
    {
        for (int i = 0; i < Favorites.Count; i++)
        {
            BrowserFavorite favorite = Favorites[i];
            if (string.Equals(favorite.Url, absoluteUrl, StringComparison.OrdinalIgnoreCase))
                return favorite;
        }

        return null;
    }

    private void UpdateFavoriteButtonState()
    {
        Uri? currentUri = GetCurrentPageUri();
        bool isFavorited = currentUri is not null && FindFavoriteByUrl(currentUri.AbsoluteUri) is not null;
        if (_isCurrentPageFavorited == isFavorited)
            return;

        _isCurrentPageFavorited = isFavorited;
        OnPropertyChanged(nameof(FavoriteButtonGlyph));
        OnPropertyChanged(nameof(FavoriteButtonToolTip));
    }

    private Uri? GetCurrentPageUri()
    {
        if (WebView.Source is Uri source && IsBrowsableUri(source))
            return source;

        return TryCreateBrowsableUri(_committedUrlText, out Uri? targetUri) ? targetUri : null;
    }

    private string GetSuggestedFavoriteName(Uri currentUri)
    {
        string title = WebView.CoreWebView2?.DocumentTitle?.Trim() ?? string.Empty;
        if (title.Length != 0)
            return title;

        return currentUri.Host.Length != 0 ? currentUri.Host : currentUri.AbsoluteUri;
    }

    /// <summary>
    /// 현재 페이지의 favicon을 PNG로 캐시하고, 표시용 로컬 URI를 반환합니다.
    /// </summary>
    private async Task<string> CaptureCurrentPageFaviconAsync(Uri currentUri)
    {
        CoreWebView2? core = WebView.CoreWebView2;
        if (core is null)
            return string.Empty;

        try
        {
            using IRandomAccessStream faviconStream = await core.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png);
            using IInputStream inputStream = faviconStream.GetInputStreamAt(0);
            using DataReader reader = new(inputStream);
            await reader.LoadAsync((uint)faviconStream.Size);
            byte[] bytes = new byte[faviconStream.Size];
            reader.ReadBytes(bytes);
            if (bytes.Length == 0)
                return string.Empty;

            return await SaveFavoriteFaviconAsync(currentUri, bytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool TryCreateBrowsableUri(string? raw, out Uri? uri)
    {
        if (Uri.TryCreate(raw, UriKind.Absolute, out Uri? parsed) && IsBrowsableUri(parsed))
        {
            uri = parsed;
            return true;
        }

        uri = null;
        return false;
    }

    private static bool IsBrowsableUri(Uri? uri)
        => uri is not null && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private void OnSourceChanged(CoreWebView2 sender, CoreWebView2SourceChangedEventArgs args)
    {
        // 사용자가 주소창을 편집 중이면 덮어쓰지 않음
        if (_isUserEditing)
            return;

        // 실제 URL 반영 (WebView2 기준)
        string url = sender.Source;
        _committedUrlText = url;
        TextBox_Search.Text = url;
        UpdateFavoriteButtonState();
        _ = RefreshCurrentFavoriteFaviconAsync();
    }

    /// <summary>
    /// 현재 페이지가 즐겨찾기인 경우, 원격 favicon URL을 로컬 PNG 캐시로 교체합니다.
    /// </summary>
    private async Task RefreshCurrentFavoriteFaviconAsync()
    {
        Uri? currentUri = GetCurrentPageUri();
        if (currentUri is null)
            return;

        BrowserFavorite? existingFavorite = FindFavoriteByUrl(currentUri.AbsoluteUri);
        if (existingFavorite is null || IsCachedFaviconUrl(existingFavorite.FaviconUrl))
            return;

        string cachedFaviconUrl = await CaptureCurrentPageFaviconAsync(currentUri);
        if (string.IsNullOrWhiteSpace(cachedFaviconUrl))
            return;

        int existingIndex = Favorites.IndexOf(existingFavorite);
        if (existingIndex < 0)
            return;

        List<BrowserFavorite> previousFavorites = SnapshotFavorites();
        Favorites[existingIndex] = new BrowserFavorite(existingFavorite.Name, existingFavorite.Url, cachedFaviconUrl);
        await PersistFavoritesAsync(previousFavorites);
    }

    private static bool IsCachedFaviconUrl(string? faviconUrl)
        => !string.IsNullOrWhiteSpace(faviconUrl)
           && faviconUrl.StartsWith("ms-appdata:///local/" + FavoriteFaviconFolderName + "/", StringComparison.OrdinalIgnoreCase);

    private static string BuildFavoriteFaviconFileName(Uri pageUri)
    {
        byte[] hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(pageUri.AbsoluteUri));
        return $"{Convert.ToHexString(hashBytes).ToLowerInvariant()}.png";
    }

    private static async Task<string> SaveFavoriteFaviconAsync(Uri pageUri, byte[] bytes)
    {
        StorageFolder folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
            FavoriteFaviconFolderName,
            CreationCollisionOption.OpenIfExists);

        string fileName = BuildFavoriteFaviconFileName(pageUri);
        StorageFile file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
        await FileIO.WriteBytesAsync(file, bytes);
        return $"ms-appdata:///local/{FavoriteFaviconFolderName}/{fileName}";
    }

    private static async Task DeleteFavoriteFaviconFileIfNeededAsync(BrowserFavorite favorite)
    {
        if (!IsCachedFaviconUrl(favorite.FaviconUrl))
            return;

        if (!Uri.TryCreate(favorite.Url, UriKind.Absolute, out Uri? pageUri))
            return;

        try
        {
            StorageFolder? folder = await ApplicationData.Current.LocalFolder.TryGetItemAsync(FavoriteFaviconFolderName) as StorageFolder;
            if (folder is null)
                return;

            string fileName = BuildFavoriteFaviconFileName(pageUri);
            IStorageItem? item = await folder.TryGetItemAsync(fileName);
            if (item is StorageFile file)
                await file.DeleteAsync();
        }
        catch
        {
            // placeholder fallback is sufficient when cache cleanup fails
        }
    }
}
