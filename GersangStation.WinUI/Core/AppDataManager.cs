using Core.Models;
using System.Text.Json;
using Windows.Storage;

namespace Core;

public static class AppDataManager
{
    private const string KeySetupCompleted = "SetupCompleted";
    private const string KeyUseSymbol = "useSymbol";
    private const string KeySelectedPreset = "SelectedPreset";
    private const string KeySelectedServer = "SelectedServer";
    private const string AccountsFileName = "accounts.json";
    private const string ClientSettingsFileName = "client-settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    #region LocalSettings Properties
    public static bool IsSetupCompleted
    {
        get => GetLocalSetting(KeySetupCompleted, false);
        set => SetLocalSetting(KeySetupCompleted, value);
    }
    public static bool UseSymbol
    {
        get => GetLocalSetting(KeyUseSymbol, true);
        set => SetLocalSetting(KeyUseSymbol, value);
    }
    public static int SelectedPreset
    {
        get => GetLocalSetting(KeySelectedPreset, 0);
        set => SetLocalSetting(KeySelectedPreset, value);
    }
    public static GameServer SelectedServer
    {
        get => (GameServer)GetLocalSetting(KeySelectedServer, (int)GameServer.Korea_Live);
        set => SetLocalSetting(KeySelectedServer, (int)value);
    }
    #endregion

    public static void SaveAccounts(IEnumerable<Account> accounts)
    {
        var list = accounts?.ToList() ?? [];
        string json = JsonSerializer.Serialize(list, JsonOptions);
        WriteTextToLocalFolder(AccountsFileName, json);
    }
    public static IReadOnlyList<Account> LoadAccounts()
    {
        string? json = ReadTextFromLocalFolder(AccountsFileName);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var result = JsonSerializer.Deserialize<List<Account>>(json);
            return result ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void SaveClientSettings(ClientSettings? settings)
    {
        var payload = settings ?? new ClientSettings();
        string json = JsonSerializer.Serialize(payload, JsonOptions);
        WriteTextToLocalFolder(ClientSettingsFileName, json);
    }
    public static ClientSettings LoadClientSettings()
    {
        string? json = ReadTextFromLocalFolder(ClientSettingsFileName);
        if (string.IsNullOrWhiteSpace(json))
            return new ClientSettings();

        try
        {
            var payload = JsonSerializer.Deserialize<ClientSettings>(json) ?? new ClientSettings();
            return payload;
        }
        catch
        {
            return new ClientSettings();
        }
    }

    public static void SaveInstallPath(string installPath)
    {
        var payload = LoadClientSettings();
        ClientSettings updated = payload with { InstallPath = installPath ?? "" };
        SaveClientSettings(updated);
    }
    public static string LoadInstallPath() => LoadClientSettings().InstallPath;

    private static void WriteTextToLocalFolder(string fileName, string content)
    {
        StorageFolder folder = ApplicationData.Current.LocalFolder;

        // StorageFolder API를 사용해 파일 생성/덮어쓰기를 처리합니다.
        StorageFile file = folder
            .CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        FileIO.WriteTextAsync(file, content)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    private static string? ReadTextFromLocalFolder(string fileName)
    {
        StorageFolder folder = ApplicationData.Current.LocalFolder;

        try
        {
            // 파일이 없을 수 있는 일반 흐름에서는 예외를 만들지 않도록 TryGetItemAsync를 사용합니다.
            IStorageItem? item = folder
                .TryGetItemAsync(fileName)
                .AsTask()
                .GetAwaiter()
                .GetResult();

            if (item is not StorageFile file)
                return null;

            return FileIO.ReadTextAsync(file)
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            return null;
        }
    }

    private static ApplicationDataContainer LocalSettings
        => ApplicationData.Current.LocalSettings;

    private static T GetLocalSetting<T>(string key, T defaultValue)
    {
        if (LocalSettings.Values.TryGetValue(key, out object? value) && value is T typed)
        {
            return typed;
        }
        return defaultValue;
    }

    private static void SetLocalSetting<T>(string key, T value)
    {
        ValidateSupportedLocalSettingsType(key, value);
        LocalSettings.Values[key] = value;
    }

    private static void ValidateSupportedLocalSettingsType(string key, object? value)
    {
        if (value is null)
            return;

        if (value is byte) return;
        if (value is short) return;
        if (value is ushort) return;
        if (value is int) return;
        if (value is uint) return;
        if (value is long) return;
        if (value is ulong) return;
        if (value is float) return;
        if (value is double) return;

        if (value is bool) return;
        if (value is char) return;
        if (value is string) return;

        if (value is DateTimeOffset) return;
        if (value is TimeSpan) return;

        if (value is Guid) return;
        if (value is Windows.Foundation.Point) return;
        if (value is Windows.Foundation.Size) return;
        if (value is Windows.Foundation.Rect) return;

        if (value is ApplicationDataCompositeValue) return;

        throw new ArgumentException(
            $"Unsupported LocalSettings value type for key '{key}': {value.GetType().FullName}",
            nameof(value));
    }
}
