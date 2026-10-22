using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GersangStation.Services;

public sealed partial class WebViewManager
{
    public BrowserAddressState AddressState { get; } = new(Url_Gersang_Main);
    private ImageSource? _addressFavicon;
    private int _addressFaviconVersion;
    public ImageSource? AddressFavicon
    {
        get => _addressFavicon;
        private set
        {
            _addressFavicon = value;
            OnPropertyChanged(nameof(AddressFavicon));
        }
    }

    private async void OnAddressFaviconChanged(CoreWebView2 sender, object args)
        => await RefreshAddressFaviconAsync(sender);

    private async Task RefreshAddressFaviconAsync(CoreWebView2 core)
    {
        ulong navigationId = AddressState.NavigationId;
        int version = ++_addressFaviconVersion;
        bool IsCurrent() => !_disposed && version == _addressFaviconVersion
            && _session.IsCurrentDocument(navigationId) && !AddressState.HasNavigationFailed;
        try
        {
            if (!IsCurrent()) return;
            AddressFavicon = null;
            AddressState.SetFavicon(navigationId, false);
            if (string.IsNullOrWhiteSpace(core.FaviconUri)) return;

            // WebView2가 내려받은 아이콘을 PNG로 읽습니다. 별도 HTTP 요청/쿠키 복제는 필요 없습니다.
            using var stream = await core.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png);
            if (!IsCurrent()) return;
            var image = new BitmapImage();
            await image.SetSourceAsync(stream);
            if (!IsCurrent()) return;
            AddressFavicon = image;
            AddressState.SetFavicon(navigationId, true);
        }
        catch (Exception ex)
        {
            if (!IsCurrent()) return;
            AddressFavicon = null;
            AddressState.SetFavicon(navigationId, false);
            // 아이콘 실패는 탐색 실패나 로그인 실패가 아닙니다. 주소창의 대체 아이콘만 표시합니다.
            Debug.WriteLine($"[WebViewManager] 주소창 아이콘 로드 실패: {ex.Message}");
        }
    }
}
