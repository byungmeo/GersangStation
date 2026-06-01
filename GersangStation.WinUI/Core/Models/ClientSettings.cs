using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Core.Models;

public sealed class AllServerClientSettings
{
    [JsonPropertyName("Servers")]
    public Dictionary<GameServer, ClientSettings> Servers { get; set; } = [];
}

public sealed class ClientSettings : INotifyPropertyChanged
{
    public const string StationManagedMultiClientSuffix = "_CreatedByStation";

    [JsonIgnore] private string _installPath = string.Empty;
    [JsonPropertyName("InstallPath")]
    public string InstallPath
    {
        get => _installPath;
        set => SetProperty(ref _installPath, value);
    }

    [JsonIgnore] private bool _useMultiClient = false;
    [JsonPropertyName("UseMultiClient")]
    public bool UseMultiClient
    {
        get => _useMultiClient;
        set => SetProperty(ref _useMultiClient, value);
    }

    [JsonIgnore] private bool _useSymbol = true;
    [JsonPropertyName("UseSymbol")]
    public bool UseSymbol
    {
        get => _useSymbol;
        set => SetProperty(ref _useSymbol, value);
    }

    [JsonIgnore] private bool _useClient2 = true;
    [JsonPropertyName("UseClient2")]
    public bool UseClient2
    {
        get => _useClient2;
        set => SetProperty(ref _useClient2, value);
    }

    [JsonIgnore] private bool _useClient3 = true;
    [JsonPropertyName("UseClient3")]
    public bool UseClient3
    {
        get => _useClient3;
        set => SetProperty(ref _useClient3, value);
    }

    [JsonIgnore] private bool _overwriteMultiClientConfig = false;
    [JsonPropertyName("OverwriteMultiClientConfig")]
    public bool OverwriteMultiClientConfig
    {
        get => _overwriteMultiClientConfig;
        set => SetProperty(ref _overwriteMultiClientConfig, value);
    }

    [JsonIgnore] public string TempPathRoot => $"{InstallPath}\\PatchTemp";
    [JsonIgnore] public string Client2Path => BuildStationManagedMultiClientPath(2);
    [JsonIgnore] public string Client3Path => BuildStationManagedMultiClientPath(3);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 메인 클라이언트의 형제 폴더로 거상스테이션 관리 다클라 경로를 만듭니다.
    /// </summary>
    private string BuildStationManagedMultiClientPath(int clientNumber)
    {
        if (string.IsNullOrWhiteSpace(InstallPath))
            return string.Empty;

        string installPath = Path.TrimEndingDirectorySeparator(InstallPath.Trim());
        return $"{installPath}{clientNumber}{StationManagedMultiClientSuffix}";
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
