using System.Text.Json;
using Windows.Storage;

namespace Core;

public static class AppDataManager
{
    private const string KeySetupCompleted = "SetupCompleted";
    private const string KeyUseSymbol = "useSymbol";
    private const string AccountsFileName = "accounts.json";
    private const string ClientSettingsFileName = "client-settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public sealed class AccountProfile
    {
        public string Id { get; set; } = "";
        public string Nickname { get; set; } = "";
    }

    public sealed class ClientSettingsProfile
    {
        public string InstallPath { get; set; } = "";
        public string Client2Path { get; set; } = "";
        public string Client3Path { get; set; } = "";
    }

    public static bool IsSetupCompleted
    {
        get => GetValue(KeySetupCompleted, false);
        set => SetValue(KeySetupCompleted, value);
    }

    public static bool LoadUseSymbol()
    {
        if (LocalSettings.Values.TryGetValue(KeyUseSymbol, out object? value) && value is bool useSymbol)
            return useSymbol;

        return true;
    }

    public static void SaveUseSymbol(bool useSymbol) => SetValue(KeyUseSymbol, useSymbol);

    /// <summary>
    /// 계정 아이디/별명 목록을 LocalFolder(accounts.json)에 저장합니다.
    /// </summary>
    public static void SaveAccounts(IEnumerable<AccountProfile> accounts)
    {
        var list = accounts?.ToList() ?? [];
        string json = JsonSerializer.Serialize(list, JsonOptions);
        WriteTextToLocalFolder(AccountsFileName, json);
    }

    /// <summary>
    /// LocalFolder(accounts.json)에서 계정 아이디/별명 목록을 읽어옵니다.
    /// </summary>
    public static IReadOnlyList<AccountProfile> LoadAccounts()
    {
        string? json = ReadTextFromLocalFolder(AccountsFileName);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var result = JsonSerializer.Deserialize<List<AccountProfile>>(json);
            return result ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// 클라이언트 설정을 LocalFolder(client-settings.json)에 저장합니다.
    /// </summary>
    public static void SaveClientSettings(ClientSettingsProfile? settings)
    {
        var payload = settings ?? new ClientSettingsProfile();
        payload.InstallPath ??= "";
        payload.Client2Path ??= "";
        payload.Client3Path ??= "";

        string json = JsonSerializer.Serialize(payload, JsonOptions);
        WriteTextToLocalFolder(ClientSettingsFileName, json);
    }

    /// <summary>
    /// LocalFolder(client-settings.json)에서 클라이언트 설정을 읽어옵니다.
    /// </summary>
    public static ClientSettingsProfile LoadClientSettings()
    {
        string? json = ReadTextFromLocalFolder(ClientSettingsFileName);
        if (string.IsNullOrWhiteSpace(json))
            return new ClientSettingsProfile();

        try
        {
            var payload = JsonSerializer.Deserialize<ClientSettingsProfile>(json) ?? new ClientSettingsProfile();
            payload.InstallPath ??= "";
            payload.Client2Path ??= "";
            payload.Client3Path ??= "";
            return payload;
        }
        catch
        {
            return new ClientSettingsProfile();
        }
    }

    /// <summary>
    /// 설치 경로만 업데이트하고 나머지 클라이언트 설정은 유지합니다.
    /// </summary>
    public static void SaveInstallPath(string installPath)
    {
        var payload = LoadClientSettings();
        payload.InstallPath = installPath ?? "";
        SaveClientSettings(payload);
    }

    /// <summary>
    /// 클라이언트 설정에서 설치 경로만 읽어옵니다.
    /// </summary>
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

    private static T GetValue<T>(string key, T defaultValue)
    {
        if (LocalSettings.Values.TryGetValue(key, out object? value) && value is T typed)
        {
            return typed;
        }
        return defaultValue;
    }

    private static void SetValue<T>(string key, T value)
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
