using GersangStation.Main;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GersangStation.Controls;

/// <summary>브라우저 페이지의 데이터 복구 안내를 셸의 오버레이 영역에 표시합니다.</summary>
public sealed partial class BrowserNoticeView : UserControl
{
    public WebViewPage Owner { get; }
    internal InfoBar AccountsNotice => BrowserAccountsInfoBar;
    internal InfoBar FavoriteRecoveryNotice => FavoriteRecoveryInfoBar;
    internal InfoBar FavoriteOperationNotice => FavoriteOperationInfoBar;

    internal event RoutedEventHandler? ReloadAccountsRequested;
    internal event RoutedEventHandler? AccountSettingsRequested;
    internal event RoutedEventHandler? ReloadFavoritesRequested;
    internal event RoutedEventHandler? ResetFavoritesRequested;

    public BrowserNoticeView(WebViewPage owner)
    {
        Owner = owner;
        InitializeComponent();
    }

    public static Visibility NoticeVisibility(bool isOpen)
        => isOpen ? Visibility.Visible : Visibility.Collapsed;

    private void ReloadAccounts_Click(object sender, RoutedEventArgs e)
        => ReloadAccountsRequested?.Invoke(sender, e);
    private void AccountSettings_Click(object sender, RoutedEventArgs e)
        => AccountSettingsRequested?.Invoke(sender, e);
    private void ReloadFavorites_Click(object sender, RoutedEventArgs e)
        => ReloadFavoritesRequested?.Invoke(sender, e);
    private void ResetFavorites_Click(object sender, RoutedEventArgs e)
        => ResetFavoritesRequested?.Invoke(sender, e);
}
