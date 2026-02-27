using Core.Models;
using System.Diagnostics;
using System.Text.Json;
using Windows.Storage;

namespace Core;

public static class AppDataManager
{
    private const string SetupCompleted_SettingKey = "SetupCompleted";
    private const string UseSymbol_SettingKey = "useSymbol";
    private const string SelectedPreset_SettingKey = "SelectedPreset";
    private const string SelectedServer_SettingKey = "SelectedServer";
    private const string AccountsFileName = "accounts.json";
    private const string ClientSettingsFileName = "client-settings.json";
    private const string PresetListFileName = "preset-list.json"; // LocalFolder 저장

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    #region LocalSettings Properties
    public static bool IsSetupCompleted
    {
        get => LoadLocalSetting(SetupCompleted_SettingKey, false);
        set => SaveLocalSetting(SetupCompleted_SettingKey, value);
    }
    public static bool UseSymbol
    {
        get => LoadLocalSetting(UseSymbol_SettingKey, true);
        set => SaveLocalSetting(UseSymbol_SettingKey, value);
    }
    public static int SelectedPreset
    {
        get => LoadLocalSetting(SelectedPreset_SettingKey, 0);
        set => SaveLocalSetting(SelectedPreset_SettingKey, value);
    }
    public static GameServer SelectedServer
    {
        get => LoadLocalSetting(SelectedServer_SettingKey, GameServer.Korea_Live);
        set => SaveLocalSetting(SelectedServer_SettingKey, value);
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

    // -------------------------
    // PresetContainer (LocalFolder)
    // 정책:
    // - ComboBox ItemSource = 모든 계정 목록
    // - PresetContainer에 저장된 Id가 계정 목록에 없으면 "" 로 정규화(선택 안 함)
    // - 정규화로 변경이 발생하면 즉시 SavePresetContainer()로 다시 저장
    // -------------------------
    public static void SavePresetList(PresetList presetList)
    {
        string json = JsonSerializer.Serialize(presetList, JsonOptions);
        WriteTextToLocalFolder(PresetListFileName, json);
    }

    /// <summary>
    /// PresetContainer를 불러오고, Accounts 기준으로 Id 유효성 검사 후
    /// 없는 Id는 ""로 정규화합니다. 정규화로 변경이 발생하면 재저장합니다.
    /// </summary>
    public static PresetList LoadPresetList()
    {
        PresetList presetList;

        string? json = ReadTextFromLocalFolder(PresetListFileName);
        if (string.IsNullOrWhiteSpace(json))
        {
            presetList = new PresetList();
            SavePresetList(presetList);
            return presetList;
        }

        try
        {
            presetList = JsonSerializer.Deserialize<PresetList>(json) ?? new PresetList();
        }
        catch
        {
            presetList = new PresetList();
            SavePresetList(presetList);
            return presetList;
        }

        var accounts = LoadAccounts();
        HashSet<string> validIds = accounts
            .Select(a => a.Id ?? "")
            .Where(id => id.Length != 0)
            .ToHashSet(StringComparer.Ordinal);

        bool changed = EnsurePresetListShape(presetList);
        changed |= NormalizePresetIds(presetList, validIds);
        if (changed)
            SavePresetList(presetList);

        return presetList;
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

    private static void WriteTextToLocalFolder(string fileName, string content)
    {
        StorageFolder folder = ApplicationData.Current.LocalFolder;

        StorageFile file = folder
            .CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        Debug.WriteLine($"[AppDataManager] WriteTextToLocalFolder FileName: {fileName}, Content:\n{content}");
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