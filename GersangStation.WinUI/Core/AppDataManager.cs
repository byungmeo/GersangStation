using Core.Models;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Storage;

namespace Core;

public static class AppDataManager
{
    public readonly record struct AppDataOperationResult(bool Success, Exception? Exception)
    {
        public static AppDataOperationResult Ok() => new(true, null);
        public static AppDataOperationResult Fail(Exception exception) => new(false, exception);
    }

    private const string SetupCompleted_SettingKey = "SetupCompleted";
    private const string UseSymbol_SettingKey = "useSymbol";
    private const string SelectedPreset_SettingKey = "SelectedPreset";
    private const string SelectedServer_SettingKey = "SelectedServer";
    private const string AccountsFileName = "accounts.json";
    private const string ClientSettingsFileName = "client-settings.json";
    private const string PresetListFileName = "preset-list.json"; // LocalFolder 저장

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    #region LocalSettings Properties
    public static bool IsSetupCompleted
    {
        get => LoadLocalSetting(SetupCompleted_SettingKey, defaultValue: false);
        set => SaveLocalSetting(SetupCompleted_SettingKey, value);
    }
    public static bool UseSymbol
    {
        get => LoadLocalSetting(UseSymbol_SettingKey, defaultValue: true);
        set => SaveLocalSetting(UseSymbol_SettingKey, value);
    }
    public static int SelectedPreset
    {
        get => LoadLocalSetting(SelectedPreset_SettingKey, defaultValue: 0);
        set => SaveLocalSetting(SelectedPreset_SettingKey, value);
    }
    public static GameServer SelectedServer
    {
        get => (GameServer)LoadLocalSetting(SelectedServer_SettingKey, defaultValue: (int)GameServer.Korea_Live);
        set => SaveLocalSetting(SelectedServer_SettingKey, (int)value);
    }
    #endregion

    public static void SaveAccounts(IEnumerable<Account> accounts)
        => SaveAccountsAsync(accounts).GetAwaiter().GetResult();

    public static async Task<AppDataOperationResult> SaveAccountsAsync(IEnumerable<Account> accounts)
    {
        try
        {
            var list = accounts?.ToList() ?? [];
            string json = JsonSerializer.Serialize(list, JsonOptions);
            return await WriteTextToLocalFolderAsync(AccountsFileName, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return AppDataOperationResult.Fail(ex);
        }
    }

    public static IList<Account> LoadAccounts()
        => LoadAccountsAsync().GetAwaiter().GetResult().Accounts;

    public static async Task<(IList<Account> Accounts, AppDataOperationResult Result)> LoadAccountsAsync()
    {
        (string? json, AppDataOperationResult readResult) = await ReadTextFromLocalFolderAsync(AccountsFileName).ConfigureAwait(false);
        if (!readResult.Success)
            return ([], readResult);

        if (string.IsNullOrWhiteSpace(json))
            return ([], AppDataOperationResult.Ok());

        try
        {
            var result = JsonSerializer.Deserialize<List<Account>>(json);
            return (result ?? [], AppDataOperationResult.Ok());
        }
        catch (Exception ex)
        {
            return ([], AppDataOperationResult.Fail(ex));
        }
    }

    private static AllServerClientSettings LoadAllServerClientSettings()
        => LoadAllServerClientSettingsAsync().GetAwaiter().GetResult().Settings;

    private static async Task<(AllServerClientSettings Settings, AppDataOperationResult Result)> LoadAllServerClientSettingsAsync()
    {
        (string? json, AppDataOperationResult readResult) = await ReadTextFromLocalFolderAsync(ClientSettingsFileName).ConfigureAwait(false);
        if (!readResult.Success)
            return (new AllServerClientSettings(), readResult);

        if (string.IsNullOrWhiteSpace(json))
            return (new AllServerClientSettings(), AppDataOperationResult.Ok());

        try
        {
            var result = JsonSerializer.Deserialize<AllServerClientSettings>(json, JsonOptions);
            return (result ?? new AllServerClientSettings(), AppDataOperationResult.Ok());
        }
        catch (Exception ex)
        {
            return (new AllServerClientSettings(), AppDataOperationResult.Fail(ex));
        }
    }

    public static ClientSettings LoadServerClientSettings(GameServer server)
        => LoadServerClientSettingsAsync(server).GetAwaiter().GetResult().Settings;

    public static async Task<(ClientSettings Settings, AppDataOperationResult Result)> LoadServerClientSettingsAsync(GameServer server)
    {
        var (all, result) = await LoadAllServerClientSettingsAsync().ConfigureAwait(false);
        if (!all.Servers.TryGetValue(server, out var s))
            return (new ClientSettings(), result.Success ? AppDataOperationResult.Ok() : result);
        return (s, result.Success ? AppDataOperationResult.Ok() : result);
    }

    private static void SaveAllServerClientSettings(AllServerClientSettings settings)
        => SaveAllServerClientSettingsAsync(settings).GetAwaiter().GetResult();

    private static async Task<AppDataOperationResult> SaveAllServerClientSettingsAsync(AllServerClientSettings settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            return await WriteTextToLocalFolderAsync(ClientSettingsFileName, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return AppDataOperationResult.Fail(ex);
        }
    }

    public static void SaveServerClientSettings(GameServer server, ClientSettings settings)
        => SaveServerClientSettingsAsync(server, settings).GetAwaiter().GetResult();

    public static async Task<AppDataOperationResult> SaveServerClientSettingsAsync(GameServer server, ClientSettings settings)
    {
        var (all, loadResult) = await LoadAllServerClientSettingsAsync().ConfigureAwait(false);
        if (!loadResult.Success)
            return loadResult;

        all.Servers[server] = settings;
        return await SaveAllServerClientSettingsAsync(all).ConfigureAwait(false);
    }

    // -------------------------
    // PresetContainer (LocalFolder)
    // 정책:
    // - ComboBox ItemSource = 모든 계정 목록
    // - PresetContainer에 저장된 Id가 계정 목록에 없으면 "" 로 정규화(선택 안 함)
    // - 정규화로 변경이 발생하면 즉시 SavePresetContainer()로 다시 저장
    // -------------------------
    public static void SavePresetList(PresetList presetList)
        => SavePresetListAsync(presetList).GetAwaiter().GetResult();

    public static async Task<AppDataOperationResult> SavePresetListAsync(PresetList presetList)
    {
        try
        {
            string json = JsonSerializer.Serialize(presetList, JsonOptions);
            return await WriteTextToLocalFolderAsync(PresetListFileName, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return AppDataOperationResult.Fail(ex);
        }
    }

    /// <summary>
    /// PresetContainer를 불러오고, Accounts 기준으로 Id 유효성 검사 후
    /// 없는 Id는 ""로 정규화합니다. 정규화로 변경이 발생하면 재저장합니다.
    /// </summary>
    public static PresetList LoadPresetList()
        => LoadPresetListAsync().GetAwaiter().GetResult().PresetList;

    public static async Task<(PresetList PresetList, AppDataOperationResult Result)> LoadPresetListAsync()
    {
        PresetList presetList;

        (string? json, AppDataOperationResult readResult) = await ReadTextFromLocalFolderAsync(PresetListFileName).ConfigureAwait(false);
        if (!readResult.Success)
            return (new PresetList(), readResult);

        if (string.IsNullOrWhiteSpace(json))
        {
            presetList = new PresetList();
            AppDataOperationResult saveResult = await SavePresetListAsync(presetList).ConfigureAwait(false);
            return (presetList, saveResult);
        }

        try
        {
            presetList = JsonSerializer.Deserialize<PresetList>(json) ?? new PresetList();
        }
        catch (Exception ex)
        {
            presetList = new PresetList();
            AppDataOperationResult saveResult = await SavePresetListAsync(presetList).ConfigureAwait(false);
            return (presetList, saveResult.Success ? AppDataOperationResult.Fail(ex) : saveResult);
        }

        var (accounts, accountLoadResult) = await LoadAccountsAsync().ConfigureAwait(false);
        HashSet<string> validIds = accounts
            .Select(a => a.Id ?? "")
            .Where(id => id.Length != 0)
            .ToHashSet(StringComparer.Ordinal);

        bool changed = EnsurePresetListShape(presetList);
        changed |= NormalizePresetIds(presetList, validIds);
        if (changed)
        {
            AppDataOperationResult saveResult = await SavePresetListAsync(presetList).ConfigureAwait(false);
            return (presetList, saveResult.Success ? accountLoadResult : saveResult);
        }

        return (presetList, accountLoadResult.Success ? AppDataOperationResult.Ok() : accountLoadResult);
    }

    private static bool EnsurePresetListShape(PresetList presetList)
    {
        bool changed = false;

        if (presetList.Presets is null || presetList.Presets.Length != 4)
        {
            Preset[] existing = presetList.Presets ?? [];
            Preset[] normalized = new Preset[4];

            for (int i = 0; i < normalized.Length; i++)
                normalized[i] = i < existing.Length ? existing[i] ?? new Preset() : new Preset();

            presetList.Presets = normalized;
            changed = true;
        }

        for (int p = 0; p < presetList.Presets.Length; p++)
        {
            Preset preset = presetList.Presets[p] ?? new Preset();
            if (presetList.Presets[p] is null)
            {
                presetList.Presets[p] = preset;
                changed = true;
            }

            if (preset.Items is null || preset.Items.Length != 3)
            {
                PresetItem[] existingItems = preset.Items ?? [];
                PresetItem[] normalizedItems = new PresetItem[3];

                for (int i = 0; i < normalizedItems.Length; i++)
                    normalizedItems[i] = i < existingItems.Length ? existingItems[i] ?? new PresetItem() : new PresetItem();

                preset.Items = normalizedItems;
                changed = true;
            }

            for (int i = 0; i < preset.Items.Length; i++)
            {
                if (preset.Items[i] is null)
                {
                    preset.Items[i] = new PresetItem();
                    changed = true;
                }

                if (preset.Items[i].Id is null)
                {
                    preset.Items[i].Id = string.Empty;
                    changed = true;
                }
            }
        }

        return changed;
    }

    private static bool NormalizePresetIds(PresetList presetList, HashSet<string> validIds)
    {
        bool changed = false;

        // 전제: Presets = 4개, 각 preset = 3개
        // container.Presets: PresetItem[4][3] 형태(또는 동등 구조)라고 가정
        for (int p = 0; p < presetList.Presets.Length; p++)
        {
            var preset = presetList.Presets[p];
            var items = preset.Items;
            for (int i = 0; i < items.Length; i++)
            {
                PresetItem item = items[i] ?? new PresetItem();
                if (items[i] is null)
                {
                    items[i] = item;
                    changed = true;
                }

                item.Id ??= string.Empty;

                // 정책: id가 없거나, 계정 목록에 없으면 ""로
                if (item.Id.Length == 0 || !validIds.Contains(item.Id))
                {
                    item.Id = "";
                    changed = true;
                }
            }
        }

        return changed;
    }

    private static async Task<AppDataOperationResult> WriteTextToLocalFolderAsync(string fileName, string content)
    {
        try
        {
            StorageFolder folder = ApplicationData.Current.LocalFolder;

            StorageFile file = await folder
                .CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting)
                .AsTask()
                .ConfigureAwait(false);

            Debug.WriteLine($"[AppDataManager] WriteTextToLocalFolder FileName: {fileName}, Content:\n{content}");
            await FileIO.WriteTextAsync(file, content)
                .AsTask()
                .ConfigureAwait(false);

            return AppDataOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return AppDataOperationResult.Fail(ex);
        }
    }

    private static async Task<(string? Content, AppDataOperationResult Result)> ReadTextFromLocalFolderAsync(string fileName)
    {
        try
        {
            StorageFolder folder = ApplicationData.Current.LocalFolder;

            IStorageItem? item = await folder
                .TryGetItemAsync(fileName)
                .AsTask()
                .ConfigureAwait(false);

            if (item is not StorageFile file)
                return (null, AppDataOperationResult.Ok());

            string? content = await FileIO.ReadTextAsync(file)
                .AsTask()
                .ConfigureAwait(false);

            return (content, AppDataOperationResult.Ok());
        }
        catch (Exception ex)
        {
            return (null, AppDataOperationResult.Fail(ex));
        }
    }

    private static ApplicationDataContainer LocalSettings
        => ApplicationData.Current.LocalSettings;

    private static T LoadLocalSetting<T>(string key, T defaultValue)
    {
        if (LocalSettings.Values.TryGetValue(key, out object? value) && value is T typed)
        {
            return typed;
        }
        return defaultValue;
    }

    private static void SaveLocalSetting<T>(string key, T value)
    {
        ValidateSupportedLocalSettingsType(key, value);

        try
        {
            LocalSettings.Values[key] = value;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppDataManager] SaveLocalSetting failed. Key: {key}, Error: {ex}");
            throw;
        }
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
