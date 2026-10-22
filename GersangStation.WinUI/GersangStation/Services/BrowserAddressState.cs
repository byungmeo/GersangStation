using System.ComponentModel;

namespace GersangStation.Services;

/// <summary>페이지 수명과 무관한 주소창 표시 상태입니다. 늦은 탐색/아이콘 결과는 무시합니다.</summary>
public sealed class BrowserAddressState(string initialAddress) : INotifyPropertyChanged
{
    public string Address { get; private set; } = initialAddress;
    public bool IsLoading { get; private set; } = true;
    public bool HasNavigationFailed { get; private set; }
    public bool HasFavicon { get; private set; }
    public bool ShowFavicon => !IsLoading && !HasNavigationFailed && HasFavicon;
    public bool ShowError => !IsLoading && !ShowFavicon;
    public string IndicatorDescription => IsLoading ? "페이지를 불러오는 중입니다."
        : HasNavigationFailed ? "페이지를 불러오지 못했습니다."
        : ShowFavicon ? "사이트 아이콘" : "사이트 아이콘을 불러올 수 없습니다.";
    internal ulong NavigationId { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Begin(ulong navigationId, string address)
    {
        NavigationId = navigationId;
        IsLoading = true;
        HasNavigationFailed = false;
        HasFavicon = false;
        UpdateAddress(address);
        NotifyIndicator();
    }

    internal void UpdateAddress(string address)
    {
        if (Address == address) return;
        Address = address;
        PropertyChanged?.Invoke(this, new(nameof(Address)));
    }

    internal void Complete(ulong navigationId, bool success)
    {
        if (navigationId != NavigationId) return;
        IsLoading = false;
        HasNavigationFailed = !success;
        NotifyIndicator();
    }

    internal void SetFavicon(ulong navigationId, bool available)
    {
        if (navigationId != NavigationId) return;
        HasFavicon = available;
        NotifyIndicator();
    }

    internal void Fail()
    {
        IsLoading = false;
        HasNavigationFailed = true;
        HasFavicon = false;
        NotifyIndicator();
    }

    private void NotifyIndicator()
    {
        foreach (string name in new[] { nameof(IsLoading), nameof(HasNavigationFailed), nameof(HasFavicon),
            nameof(ShowFavicon), nameof(ShowError), nameof(IndicatorDescription) })
            PropertyChanged?.Invoke(this, new(name));
    }
}
