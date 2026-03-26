using Core.Models;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Windows.Storage;

namespace Core;

public static class AppDataManager
{
    public enum WindowMinimizeBehavior
    {
        HideToSystemTray = 0,
        MinimizeToTaskbar = 1
    }

    public enum WindowCloseBehavior
    {
        ExitApplication = 0,
        HideToSystemTray = 1
    }

    /// <summary>
    /// 앱 데이터 작업 실패를 원인 범주별로 구분합니다.
    /// </summary>
    public enum AppDataErrorKind
    {
        None,
        Validation,
        Serialization,
        Storage,
        CredentialVault,
        Unexpected
    }

    /// <summary>
    /// AppDataManager 작업의 성공 여부와 실패 메타데이터를 함께 전달합니다.
    /// </summary>
    public readonly record struct AppDataOperationResult(bool Success, Exception? Exception)
    {
        public string Operation { get; init; } = string.Empty;
        public string Target { get; init; } = string.Empty;
        public AppDataErrorKind ErrorKind { get; init; } = Success ? AppDataErrorKind.None : AppDataErrorKind.Unexpected;

        public static AppDataOperationResult Ok(string operation = "", string target = "")
            => new(true, null)
            {
                Operation = operation,
                Target = target,
                ErrorKind = AppDataErrorKind.None
            };

        public static AppDataOperationResult Fail(string operation, Exception exception, string target = "")
            => new(false, exception)
            {
                Operation = operation,
                Target = target,
                ErrorKind = ClassifyErrorKind(exception)
            };

        public static AppDataOperationResult Fail(string operation, Exception exception, AppDataErrorKind errorKind, string target = "")
            => new(false, exception)
            {
                Operation = operation,
                Target = target,
                ErrorKind = errorKind
            };

        public string GetMessageOrDefault(string fallbackMessage)
            => Exception?.Message ?? fallbackMessage;
    }

    public readonly record struct AccountCredential(string UserName, string Password);
    public readonly record struct AccountCredentialRename(string CurrentUserName, string NewUserName);

    private const string SetupCompleted_SettingKey = "SetupCompleted";
    private const string LatestVersion_SettingKey = "PrevVersion";
    private const string UseSymbol_SettingKey = "useSymbol";
    private const string DeveloperToolEnabled_SettingKey = "DeveloperToolEnabled";
    private const string StartupAdminPromptEnabled_SettingKey = "StartupAdminPromptEnabled";
    private const string MouseConfinementEnabled_SettingKey = "MouseConfinementEnabled";
    private const string WindowSwitchingEnabled_SettingKey = "WindowSwitchingEnabled";
    private const string WindowMinimizeBehavior_SettingKey = "WindowMinimizeBehavior";
    private const string WindowCloseBehavior_SettingKey = "WindowCloseBehavior";
    private const string SelectedPreset_SettingKey = "SelectedPreset";
    private const string SelectedServer_SettingKey = "SelectedServer";
    private const string EventUrgencyThresholdDays_SettingKey = "EventUrgencyThresholdDays";
    private const string AccountsFileName = "accounts.json";
    private const string ClientSettingsFileName = "client-settings.json";
    private const string PresetListFileName = "preset-list.json"; // LocalFolder 저장
    private const string BrowserFavoritesFileName = "browser-favorites.json";

    #region LocalSettings Properties
    public static string PrevVersion
    {
        get => LoadLocalSetting(LatestVersion_SettingKey, defaultValue: "1.0.0.0");
        set => SaveLocalSetting(LatestVersion_SettingKey, value);
    }
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
    public static bool IsDeveloperToolEnabled
    {
        get => LoadLocalSetting(DeveloperToolEnabled_SettingKey, defaultValue: false);
        set
        {
            bool previous = IsDeveloperToolEnabled;
            SaveLocalSetting(DeveloperToolEnabled_SettingKey, value);

            if (previous != value)
                DeveloperToolEnabledChanged?.Invoke(null, value);
        }
    }
    public static bool IsStartupAdminPromptEnabled
    {
        get => LoadLocalSetting(StartupAdminPromptEnabled_SettingKey, defaultValue: true);
        set => SaveLocalSetting(StartupAdminPromptEnabled_SettingKey, value);
    }
    public static bool IsMouseConfinementEnabled
    {
        get => LoadLocalSetting(MouseConfinementEnabled_SettingKey, defaultValue: false);
        set
        {
            bool previous = IsMouseConfinementEnabled;
            SaveLocalSetting(MouseConfinementEnabled_SettingKey, value);

            if (previous != value)
                MouseConfinementEnabledChanged?.Invoke(null, value);
        }
    }
    public static bool IsWindowSwitchingEnabled
    {
        get => LoadLocalSetting(WindowSwitchingEnabled_SettingKey, defaultValue: false);
        set
        {
            bool previous = IsWindowSwitchingEnabled;
            SaveLocalSetting(WindowSwitchingEnabled_SettingKey, value);

            if (previous != value)
                WindowSwitchingEnabledChanged?.Invoke(null, value);
        }
    }
    public static WindowMinimizeBehavior MinimizeBehavior
    {
        get
        {
            int storedValue = LoadLocalSetting(
                WindowMinimizeBehavior_SettingKey,
                defaultValue: (int)WindowMinimizeBehavior.HideToSystemTray);
            return Enum.IsDefined(typeof(WindowMinimizeBehavior), storedValue)
                ? (WindowMinimizeBehavior)storedValue
                : WindowMinimizeBehavior.HideToSystemTray;
        }
        set
        {
            WindowMinimizeBehavior sanitizedValue = Enum.IsDefined(typeof(WindowMinimizeBehavior), value)
                ? value
                : WindowMinimizeBehavior.HideToSystemTray;
            WindowMinimizeBehavior previous = MinimizeBehavior;
            SaveLocalSetting(WindowMinimizeBehavior_SettingKey, (int)sanitizedValue);

            if (previous != sanitizedValue)
                MinimizeBehaviorChanged?.Invoke(null, sanitizedValue);
        }
    }
    public static WindowCloseBehavior CloseBehavior
    {
        get
        {
            int storedValue = LoadLocalSetting(
                WindowCloseBehavior_SettingKey,
                defaultValue: (int)WindowCloseBehavior.ExitApplication);
            return Enum.IsDefined(typeof(WindowCloseBehavior), storedValue)
                ? (WindowCloseBehavior)storedValue
                : WindowCloseBehavior.ExitApplication;
        }
        set
        {
            WindowCloseBehavior sanitizedValue = Enum.IsDefined(typeof(WindowCloseBehavior), value)
                ? value
                : WindowCloseBehavior.ExitApplication;
            WindowCloseBehavior previous = CloseBehavior;
            SaveLocalSetting(WindowCloseBehavior_SettingKey, (int)sanitizedValue);

            if (previous != sanitizedValue)
                CloseBehaviorChanged?.Invoke(null, sanitizedValue);
        }
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
    public static int EventUrgencyThresholdDays
    {
        get => Math.Max(0, LoadLocalSetting(EventUrgencyThresholdDays_SettingKey, defaultValue: 3));
        set => SaveLocalSetting(EventUrgencyThresholdDays_SettingKey, Math.Max(0, value));
    }
    #endregion

    public static event EventHandler<bool>? DeveloperToolEnabledChanged;
    public static event EventHandler<bool>? MouseConfinementEnabledChanged;
    public static event EventHandler<bool>? WindowSwitchingEnabledChanged;
    public static event EventHandler<WindowMinimizeBehavior>? MinimizeBehaviorChanged;
    public static event EventHandler<WindowCloseBehavior>? CloseBehaviorChanged;

    private static AppDataOperationResult Ok(string operation, string target = "")
        => AppDataOperationResult.Ok(operation, target);

    private static AppDataOperationResult Fail(string operation, Exception exception, string target = "")
        => AppDataOperationResult.Fail(operation, exception, target);

    private static AppDataOperationResult Fail(string operation, Exception exception, AppDataErrorKind errorKind, string target = "")
        => AppDataOperationResult.Fail(operation, exception, errorKind, target);

    private static AppDataErrorKind ClassifyErrorKind(Exception exception)
        => exception switch
        {
            JsonException or NotSupportedException => AppDataErrorKind.Serialization,
            IOException or UnauthorizedAccessException => AppDataErrorKind.Storage,
            ArgumentException or InvalidOperationException => AppDataErrorKind.Validation,
            _ => AppDataErrorKind.Unexpected
        };

    /// <summary>
    /// 계정 목록을 동기적으로 저장하고 결과 메타데이터를 함께 반환합니다.
    /// </summary>
    public static AppDataOperationResult TrySaveAccounts(IEnumerable<Account> accounts)
        => SaveAccountsAsync(accounts).GetAwaiter().GetResult();

    /// <summary>
    /// 저장 결과가 필요 없는 기존 호출부를 위한 편의 래퍼입니다.
    /// </summary>
    public static void SaveAccounts(IEnumerable<Account> accounts)
        => TrySaveAccounts(accounts);

    public static async Task<AppDataOperationResult> SaveAccountsAsync(IEnumerable<Account> accounts)
    {
        try
        {
            List<Account> list = NormalizeAccountsForPersistence(accounts);
            string json = JsonSerializer.Serialize(list, AppDataJsonSerializerContext.Default.ListAccount);
            return await WriteTextToLocalFolderAsync(AccountsFileName, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Fail(nameof(SaveAccountsAsync), ex, AppDataErrorKind.Serialization, AccountsFileName);
        }
    }

    /// <summary>
    /// 계정 목록 저장과 비밀번호 저장을 한 흐름으로 묶고, 저장 후 프리셋/자격 증명 정합성도 함께 맞춥니다.
    /// </summary>
    public static async Task<(IList<Account> Accounts, AppDataOperationResult Result)> SaveAccountsWithCredentialsAsync(
        IEnumerable<Account> accounts,
        IEnumerable<AccountCredential>? credentials = null,
        IEnumerable<AccountCredentialRename>? credentialRenames = null)
    {
        // 정책:
        // - 비밀번호는 계정과 항상 1:1로 존재해야 합니다.
        // - 계정 저장 후에는 고아 자격 증명과 잘못된 프리셋 참조를 남기지 않습니다.
        List<Account> normalizedAccounts = NormalizeAccountsForPersistence(accounts);
        List<AccountCredential> normalizedCredentials = NormalizeCredentials(credentials);
        List<AccountCredentialRename> normalizedCredentialRenames = NormalizeCredentialRenames(credentialRenames);
        Dictionary<string, string?> previousPasswords = new(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (AccountCredentialRename rename in normalizedCredentialRenames)
            {
                if (!TryCapturePreviousPassword(rename.CurrentUserName, previousPasswords, out AppDataOperationResult captureCurrentResult))
                {
                    RollbackCredentialUpserts(previousPasswords);
                    return (normalizedAccounts, captureCurrentResult);
                }

                if (!TryCapturePreviousPassword(rename.NewUserName, previousPasswords, out AppDataOperationResult captureNewResult))
                {
                    RollbackCredentialUpserts(previousPasswords);
                    return (normalizedAccounts, captureNewResult);
                }

                PasswordVaultHelper.PasswordVaultMoveResult moveResult =
                    PasswordVaultHelper.TryMove(rename.CurrentUserName, rename.NewUserName);
                if (!moveResult.Success)
                {
                    RollbackCredentialUpserts(previousPasswords);
                    Exception moveException = moveResult.Exception ?? new InvalidOperationException(
                        $"Failed to move credential. CurrentUserName='{rename.CurrentUserName}', NewUserName='{rename.NewUserName}', Stage={moveResult.FailureStage}");
                    return (normalizedAccounts, Fail(nameof(SaveAccountsWithCredentialsAsync), moveException, AppDataErrorKind.CredentialVault, AccountsFileName));
                }
            }

            foreach (AccountCredential credential in normalizedCredentials)
            {
                if (!TryCapturePreviousPassword(credential.UserName, previousPasswords, out AppDataOperationResult captureResult))
                {
                    RollbackCredentialUpserts(previousPasswords);
                    return (normalizedAccounts, captureResult);
                }

                PasswordVaultHelper.PasswordVaultOperationResult saveCredentialResult =
                    PasswordVaultHelper.TrySave(credential.UserName, credential.Password);
                if (!saveCredentialResult.Success)
                {
                    RollbackCredentialUpserts(previousPasswords);
                    return (normalizedAccounts, Fail(nameof(SaveAccountsWithCredentialsAsync), saveCredentialResult.Exception!, AppDataErrorKind.CredentialVault, AccountsFileName));
                }

                if (saveCredentialResult.Status == PasswordVaultHelper.PasswordVaultOperationStatus.IgnoredInvalidInput)
                {
                    RollbackCredentialUpserts(previousPasswords);
                    return (
                        normalizedAccounts,
                        Fail(
                            nameof(SaveAccountsWithCredentialsAsync),
                            CreateUnexpectedPasswordVaultStateException(
                                $"Password vault save ignored a normalized account credential input. UserName='{credential.UserName}'",
                                saveCredentialResult.Exception),
                            AppDataErrorKind.CredentialVault,
                            AccountsFileName));
                }
            }
        }
        catch (Exception ex)
        {
            RollbackCredentialUpserts(previousPasswords);
            return (normalizedAccounts, Fail(nameof(SaveAccountsWithCredentialsAsync), ex, AppDataErrorKind.CredentialVault, AccountsFileName));
        }

        AppDataOperationResult saveResult = await SaveAccountsAsync(normalizedAccounts).ConfigureAwait(false);
        if (!saveResult.Success)
        {
            RollbackCredentialUpserts(previousPasswords);
            return (normalizedAccounts, saveResult);
        }

        AppDataOperationResult syncResult = await SyncAccountsAndPresetsAfterSaveAsync(normalizedAccounts).ConfigureAwait(false);
        if (!syncResult.Success)
        {
            Debug.WriteLine(
                $"[AppDataManager] Non-blocking account dependency sync failed. Operation: {syncResult.Operation}, Target: {syncResult.Target}, Kind: {syncResult.ErrorKind}, Exception: {syncResult.Exception}");
        }

        return (normalizedAccounts, Ok(nameof(SaveAccountsWithCredentialsAsync), AccountsFileName));
    }

    /// <summary>
    /// 저장된 계정 목록을 동기적으로 불러오고 결과 메타데이터를 함께 반환합니다.
    /// </summary>
    public static (IList<Account> Accounts, AppDataOperationResult Result) TryLoadAccounts()
        => LoadAccountsAsync().GetAwaiter().GetResult();

    /// <summary>
    /// 저장된 계정 목록만 필요한 기존 호출부를 위한 편의 래퍼입니다.
    /// </summary>
    public static IList<Account> LoadAccounts()
        => TryLoadAccounts().Accounts;

    /// <summary>
    /// 저장된 계정 목록을 불러오고, 비밀번호가 없는 계정이나 중복/공백 ID를 정리합니다.
    /// </summary>
    public static async Task<(IList<Account> Accounts, AppDataOperationResult Result)> LoadAccountsAsync()
    {
        (string? json, AppDataOperationResult readResult) = await ReadTextFromLocalFolderAsync(AccountsFileName).ConfigureAwait(false);
        if (!readResult.Success)
            return ([], readResult);

        if (string.IsNullOrWhiteSpace(json))
            return ([], Ok(nameof(LoadAccountsAsync), AccountsFileName));

        try
        {
            List<Account> result = JsonSerializer.Deserialize(json, AppDataJsonSerializerContext.Default.ListAccount) ?? [];
            bool changed;
            List<Account> normalizedAccounts = NormalizeAccountsForPersistence(result, requireCredentialPair: false, out changed);
            if (changed)
            {
                AppDataOperationResult saveResult = await SaveAccountsAsync(normalizedAccounts).ConfigureAwait(false);
                return (normalizedAccounts, saveResult.Success ? Ok(nameof(LoadAccountsAsync), AccountsFileName) : saveResult);
            }

            return (normalizedAccounts, Ok(nameof(LoadAccountsAsync), AccountsFileName));
        }
        catch (Exception ex)
        {
            return ([], Fail(nameof(LoadAccountsAsync), ex, AppDataErrorKind.Serialization, AccountsFileName));
        }
    }

    /// <summary>
    /// 전체 서버 클라이언트 설정을 동기적으로 불러오고 결과 메타데이터를 함께 반환합니다.
    /// </summary>
    private static (AllServerClientSettings Settings, AppDataOperationResult Result) TryLoadAllServerClientSettings()
        => LoadAllServerClientSettingsAsync().GetAwaiter().GetResult();

    private static AllServerClientSettings LoadAllServerClientSettings()
        => TryLoadAllServerClientSettings().Settings;

    private static async Task<(AllServerClientSettings Settings, AppDataOperationResult Result)> LoadAllServerClientSettingsAsync()
    {
        (string? json, AppDataOperationResult readResult) = await ReadTextFromLocalFolderAsync(ClientSettingsFileName).ConfigureAwait(false);
        if (!readResult.Success)
            return (new AllServerClientSettings(), readResult);

        if (string.IsNullOrWhiteSpace(json))
            return (new AllServerClientSettings(), Ok(nameof(LoadAllServerClientSettingsAsync), ClientSettingsFileName));

        try
        {
            var result = JsonSerializer.Deserialize(json, AppDataJsonSerializerContext.Default.AllServerClientSettings);
            return (result ?? new AllServerClientSettings(), Ok(nameof(LoadAllServerClientSettingsAsync), ClientSettingsFileName));
        }
        catch (Exception ex)
        {
            return (new AllServerClientSettings(), Fail(nameof(LoadAllServerClientSettingsAsync), ex, AppDataErrorKind.Serialization, ClientSettingsFileName));
        }
    }

    /// <summary>
    /// 지정한 서버의 클라이언트 설정을 동기적으로 불러오고 결과 메타데이터를 함께 반환합니다.
    /// </summary>
    public static (ClientSettings Settings, AppDataOperationResult Result) TryLoadServerClientSettings(GameServer server)
        => LoadServerClientSettingsAsync(server).GetAwaiter().GetResult();

    /// <summary>
    /// 지정한 서버의 클라이언트 설정만 필요한 기존 호출부를 위한 편의 래퍼입니다.
    /// </summary>
    public static ClientSettings LoadServerClientSettings(GameServer server)
        => TryLoadServerClientSettings(server).Settings;

    public static async Task<(ClientSettings Settings, AppDataOperationResult Result)> LoadServerClientSettingsAsync(GameServer server)
    {
        var (all, result) = await LoadAllServerClientSettingsAsync().ConfigureAwait(false);
        if (!all.Servers.TryGetValue(server, out var s))
            return (new ClientSettings(), result.Success ? Ok(nameof(LoadServerClientSettingsAsync), ClientSettingsFileName) : result);
        return (s, result.Success ? Ok(nameof(LoadServerClientSettingsAsync), ClientSettingsFileName) : result);
    }

    /// <summary>
    /// 전체 서버 클라이언트 설정을 동기적으로 저장하고 결과 메타데이터를 함께 반환합니다.
    /// </summary>
    private static AppDataOperationResult TrySaveAllServerClientSettings(AllServerClientSettings settings)
        => SaveAllServerClientSettingsAsync(settings).GetAwaiter().GetResult();

    private static void SaveAllServerClientSettings(AllServerClientSettings settings)
        => TrySaveAllServerClientSettings(settings);

    private static async Task<AppDataOperationResult> SaveAllServerClientSettingsAsync(AllServerClientSettings settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, AppDataJsonSerializerContext.Default.AllServerClientSettings);
            return await WriteTextToLocalFolderAsync(ClientSettingsFileName, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Fail(nameof(SaveAllServerClientSettingsAsync), ex, AppDataErrorKind.Serialization, ClientSettingsFileName);
        }
    }

    /// <summary>
    /// 지정한 서버의 클라이언트 설정을 동기적으로 저장하고 결과 메타데이터를 함께 반환합니다.
    /// </summary>
    public static AppDataOperationResult TrySaveServerClientSettings(GameServer server, ClientSettings settings)
        => SaveServerClientSettingsAsync(server, settings).GetAwaiter().GetResult();

    /// <summary>
    /// 저장 결과가 필요 없는 기존 호출부를 위한 편의 래퍼입니다.
    /// </summary>
    public static void SaveServerClientSettings(GameServer server, ClientSettings settings)
        => TrySaveServerClientSettings(server, settings);

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
    /// <summary>
    /// 프리셋 목록을 동기적으로 저장하고 결과 메타데이터를 함께 반환합니다.
    /// </summary>
    public static AppDataOperationResult TrySavePresetList(PresetList presetList)
        => SavePresetListAsync(presetList).GetAwaiter().GetResult();

    /// <summary>
    /// 저장 결과가 필요 없는 기존 호출부를 위한 편의 래퍼입니다.
    /// </summary>
    public static void SavePresetList(PresetList presetList)
        => TrySavePresetList(presetList);

    public static async Task<AppDataOperationResult> SavePresetListAsync(PresetList presetList)
    {
        try
        {
            string json = JsonSerializer.Serialize(presetList, AppDataJsonSerializerContext.Default.PresetList);
            return await WriteTextToLocalFolderAsync(PresetListFileName, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Fail(nameof(SavePresetListAsync), ex, AppDataErrorKind.Serialization, PresetListFileName);
        }
    }

    /// <summary>
    /// 프리셋 목록을 동기적으로 불러오고 결과 메타데이터를 함께 반환합니다.
    /// </summary>
    public static (PresetList PresetList, AppDataOperationResult Result) TryLoadPresetList()
        => LoadPresetListAsync().GetAwaiter().GetResult();

    /// <summary>
    /// 프리셋 목록만 필요한 기존 호출부를 위한 편의 래퍼입니다.
    /// </summary>
    public static PresetList LoadPresetList()
        => TryLoadPresetList().PresetList;

    /// <summary>
    /// PresetContainer를 불러오고, Accounts 기준으로 Id 유효성 검사 후
    /// 없는 Id는 ""로 정규화합니다. 정규화로 변경이 발생하면 재저장합니다.
    /// </summary>
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
            presetList = JsonSerializer.Deserialize(json, AppDataJsonSerializerContext.Default.PresetList) ?? new PresetList();
        }
        catch (Exception ex)
        {
            presetList = new PresetList();
            AppDataOperationResult saveResult = await SavePresetListAsync(presetList).ConfigureAwait(false);
            return (presetList, saveResult.Success ? Fail(nameof(LoadPresetListAsync), ex, AppDataErrorKind.Serialization, PresetListFileName) : saveResult);
        }

        var (accounts, accountLoadResult) = await LoadAccountsAsync().ConfigureAwait(false);
        HashSet<string> validIds = accounts
            .Select(a => a.Id?.Trim() ?? string.Empty)
            .Where(id => id.Length != 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool changed = EnsurePresetListShape(presetList);
        changed |= NormalizePresetIds(presetList, validIds);
        if (changed)
        {
            AppDataOperationResult saveResult = await SavePresetListAsync(presetList).ConfigureAwait(false);
            return (presetList, saveResult.Success ? accountLoadResult : saveResult);
        }

        return (presetList, accountLoadResult.Success ? Ok(nameof(LoadPresetListAsync), PresetListFileName) : accountLoadResult);
    }

    /// <summary>
    /// 브라우저 즐겨찾기 목록을 저장하며 URL 정규화와 중복 제거를 함께 수행합니다.
    /// </summary>
    /// <summary>
    /// 브라우저 즐겨찾기 목록을 동기적으로 저장하고 결과 메타데이터를 함께 반환합니다.
    /// </summary>
    public static AppDataOperationResult TrySaveBrowserFavorites(IEnumerable<BrowserFavorite> favorites)
        => SaveBrowserFavoritesAsync(favorites).GetAwaiter().GetResult();

    /// <summary>
    /// 저장 결과가 필요 없는 기존 호출부를 위한 편의 래퍼입니다.
    /// </summary>
    public static void SaveBrowserFavorites(IEnumerable<BrowserFavorite> favorites)
        => TrySaveBrowserFavorites(favorites);

    /// <summary>
    /// 브라우저 즐겨찾기 목록을 저장하며 URL 정규화와 중복 제거를 함께 수행합니다.
    /// </summary>
    public static async Task<AppDataOperationResult> SaveBrowserFavoritesAsync(IEnumerable<BrowserFavorite> favorites)
    {
        try
        {
            List<BrowserFavorite> normalizedFavorites = NormalizeBrowserFavorites(favorites);
            string json = JsonSerializer.Serialize(normalizedFavorites, AppDataJsonSerializerContext.Default.ListBrowserFavorite);
            return await WriteTextToLocalFolderAsync(BrowserFavoritesFileName, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Fail(nameof(SaveBrowserFavoritesAsync), ex, AppDataErrorKind.Serialization, BrowserFavoritesFileName);
        }
    }

    /// <summary>
    /// 브라우저 즐겨찾기 목록을 동기적으로 불러오고 결과 메타데이터를 함께 반환합니다.
    /// </summary>
    public static (IList<BrowserFavorite> Favorites, AppDataOperationResult Result) TryLoadBrowserFavorites()
        => LoadBrowserFavoritesAsync().GetAwaiter().GetResult();

    /// <summary>
    /// 브라우저 즐겨찾기 목록만 필요한 기존 호출부를 위한 편의 래퍼입니다.
    /// </summary>
    public static IList<BrowserFavorite> LoadBrowserFavorites()
        => TryLoadBrowserFavorites().Favorites;

    /// <summary>
    /// 저장된 브라우저 즐겨찾기 목록을 불러오고, 잘못된 항목은 자동으로 정리합니다.
    /// </summary>
    /// <summary>
    /// 저장된 브라우저 즐겨찾기 목록을 불러오고, 잘못된 항목은 자동으로 정리합니다.
    /// </summary>
    public static async Task<(IList<BrowserFavorite> Favorites, AppDataOperationResult Result)> LoadBrowserFavoritesAsync()
    {
        (string? json, AppDataOperationResult readResult) = await ReadTextFromLocalFolderAsync(BrowserFavoritesFileName).ConfigureAwait(false);
        if (!readResult.Success)
            return ([], readResult);

        if (string.IsNullOrWhiteSpace(json))
        {
            List<BrowserFavorite> emptyFavorites = [];
            AppDataOperationResult saveResult = await SaveBrowserFavoritesAsync(emptyFavorites).ConfigureAwait(false);
            return (emptyFavorites, saveResult.Success ? Ok(nameof(LoadBrowserFavoritesAsync), BrowserFavoritesFileName) : saveResult);
        }

        try
        {
            List<BrowserFavorite> parsedFavorites = JsonSerializer.Deserialize(json, AppDataJsonSerializerContext.Default.ListBrowserFavorite) ?? [];
            List<BrowserFavorite> normalizedFavorites = NormalizeBrowserFavorites(parsedFavorites);
            bool changed = parsedFavorites.Count != normalizedFavorites.Count;
            if (!changed)
            {
                for (int i = 0; i < parsedFavorites.Count; i++)
                {
                    BrowserFavorite parsed = parsedFavorites[i];
                    BrowserFavorite normalized = normalizedFavorites[i];
                    if (!string.Equals(parsed.Name, normalized.Name, StringComparison.Ordinal)
                        || !string.Equals(parsed.Url, normalized.Url, StringComparison.Ordinal)
                        || !string.Equals(parsed.FaviconUrl, normalized.FaviconUrl, StringComparison.Ordinal))
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                AppDataOperationResult saveResult = await SaveBrowserFavoritesAsync(normalizedFavorites).ConfigureAwait(false);
                return (normalizedFavorites, saveResult.Success ? Ok(nameof(LoadBrowserFavoritesAsync), BrowserFavoritesFileName) : saveResult);
            }

            return (normalizedFavorites, Ok(nameof(LoadBrowserFavoritesAsync), BrowserFavoritesFileName));
        }
        catch (Exception ex)
        {
            List<BrowserFavorite> emptyFavorites = [];
            AppDataOperationResult saveResult = await SaveBrowserFavoritesAsync(emptyFavorites).ConfigureAwait(false);
            return (emptyFavorites, saveResult.Success ? Fail(nameof(LoadBrowserFavoritesAsync), ex, AppDataErrorKind.Serialization, BrowserFavoritesFileName) : saveResult);
        }
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

                string normalizedId = item.Id?.Trim() ?? string.Empty;
                if (!string.Equals(item.Id, normalizedId, StringComparison.Ordinal))
                {
                    item.Id = normalizedId;
                    changed = true;
                }

                // 정책: id가 없거나, 계정 목록에 없으면 ""로
                if (normalizedId.Length != 0 && !validIds.Contains(normalizedId))
                {
                    item.Id = "";
                    changed = true;
                }
            }
        }

        return changed;
    }

    /// <summary>
    /// 즐겨찾기 목록을 저장 가능한 형태로 정규화하고, 잘못된 URL과 중복 항목을 제거합니다.
    /// </summary>
    private static List<BrowserFavorite> NormalizeBrowserFavorites(IEnumerable<BrowserFavorite>? favorites)
    {
        List<BrowserFavorite> normalized = [];
        HashSet<string> seenUrls = new(StringComparer.OrdinalIgnoreCase);

        foreach (BrowserFavorite? favorite in favorites ?? [])
        {
            string rawUrl = favorite?.Url?.Trim() ?? string.Empty;
            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out Uri? uri))
                continue;

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                continue;

            string normalizedUrl = uri.AbsoluteUri;
            if (!seenUrls.Add(normalizedUrl))
                continue;

            string name = favorite?.Name?.Trim() ?? string.Empty;
            if (name.Length == 0)
                name = normalizedUrl;

            string faviconUrl = favorite?.FaviconUrl?.Trim() ?? string.Empty;
            if (!Uri.TryCreate(faviconUrl, UriKind.Absolute, out _))
                faviconUrl = string.Empty;

            normalized.Add(new BrowserFavorite(name, normalizedUrl, faviconUrl));
        }

        return normalized;
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

            Debug.WriteLine($"[AppDataManager] WriteTextToLocalFolder FileName: {fileName}, Length: {content.Length}");
            await FileIO.WriteTextAsync(file, content)
                .AsTask()
                .ConfigureAwait(false);

            return Ok(nameof(WriteTextToLocalFolderAsync), fileName);
        }
        catch (Exception ex)
        {
            return Fail(nameof(WriteTextToLocalFolderAsync), ex, AppDataErrorKind.Storage, fileName);
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
                return (null, Ok(nameof(ReadTextFromLocalFolderAsync), fileName));

            string? content = await FileIO.ReadTextAsync(file)
                .AsTask()
                .ConfigureAwait(false);

            return (content, Ok(nameof(ReadTextFromLocalFolderAsync), fileName));
        }
        catch (Exception ex)
        {
            return (null, Fail(nameof(ReadTextFromLocalFolderAsync), ex, AppDataErrorKind.Storage, fileName));
        }
    }

    private static ApplicationDataContainer LocalSettings
        => ApplicationData.Current.LocalSettings;

    /// <summary>
    /// 저장 가능한 계정 형태로 정규화하고 중복/공백 ID를 제거합니다.
    /// </summary>
    private static List<Account> NormalizeAccountsForPersistence(IEnumerable<Account>? accounts)
        => NormalizeAccountsForPersistence(accounts, credentialUserNames: null, requireCredentialPair: false, out _);

    /// <summary>
    /// 계정 목록을 정규화하면서 필요 시 비밀번호와 1:1 관계가 없는 계정도 제거합니다.
    /// </summary>
    private static List<Account> NormalizeAccountsForPersistence(
        IEnumerable<Account>? accounts,
        bool requireCredentialPair,
        out bool changed)
        => NormalizeAccountsForPersistence(accounts, credentialUserNames: null, requireCredentialPair, out changed);

    /// <summary>
    /// 계정 목록을 정규화하면서 필요 시 비밀번호와 1:1 관계가 없는 계정도 제거합니다.
    /// </summary>
    private static List<Account> NormalizeAccountsForPersistence(
        IEnumerable<Account>? accounts,
        IReadOnlyCollection<string>? credentialUserNames,
        bool requireCredentialPair,
        out bool changed)
    {
        changed = false;
        List<Account> normalizedAccounts = [];
        HashSet<string> seenIds = new(StringComparer.OrdinalIgnoreCase);

        foreach (Account? account in accounts ?? [])
        {
            if (account is null)
            {
                changed = true;
                continue;
            }

            string id = account.Id?.Trim() ?? string.Empty;
            string nickname = account.Nickname?.Trim() ?? string.Empty;
            string groupName = account.GroupName?.Trim() ?? string.Empty;

            if (!string.Equals(account.Id ?? string.Empty, id, StringComparison.Ordinal) ||
                !string.Equals(account.Nickname ?? string.Empty, nickname, StringComparison.Ordinal) ||
                !string.Equals(account.GroupName ?? string.Empty, groupName, StringComparison.Ordinal))
            {
                changed = true;
            }

            if (id.Length == 0)
            {
                changed = true;
                continue;
            }

            if (!seenIds.Add(id))
            {
                changed = true;
                continue;
            }

            if (requireCredentialPair &&
                credentialUserNames is not null &&
                !credentialUserNames.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                changed = true;
                continue;
            }

            normalizedAccounts.Add(new Account(id, nickname, groupName));
        }

        return normalizedAccounts;
    }

    /// <summary>
    /// 비밀번호 업데이트 입력을 정규화하고 마지막 입력 기준으로 중복 ID를 정리합니다.
    /// </summary>
    private static List<AccountCredential> NormalizeCredentials(IEnumerable<AccountCredential>? credentials)
    {
        Dictionary<string, string> normalized = new(StringComparer.OrdinalIgnoreCase);

        foreach (AccountCredential credential in credentials ?? [])
        {
            string userName = credential.UserName?.Trim() ?? string.Empty;
            string password = credential.Password ?? string.Empty;

            if (userName.Length == 0 || password.Length == 0)
                continue;

            normalized[userName] = password;
        }

        return normalized
            .Select(pair => new AccountCredential(pair.Key, pair.Value))
            .ToList();
    }

    /// <summary>
    /// 계정 ID 변경 입력을 정규화하고 마지막 입력 기준으로 중복 이동을 정리합니다.
    /// </summary>
    private static List<AccountCredentialRename> NormalizeCredentialRenames(IEnumerable<AccountCredentialRename>? credentialRenames)
    {
        Dictionary<string, string> normalized = new(StringComparer.OrdinalIgnoreCase);

        foreach (AccountCredentialRename rename in credentialRenames ?? [])
        {
            string currentUserName = rename.CurrentUserName?.Trim() ?? string.Empty;
            string newUserName = rename.NewUserName?.Trim() ?? string.Empty;

            if (currentUserName.Length == 0 || newUserName.Length == 0)
                continue;

            if (string.Equals(currentUserName, newUserName, StringComparison.OrdinalIgnoreCase))
                continue;

            normalized[currentUserName] = newUserName;
        }

        return normalized
            .Select(pair => new AccountCredentialRename(pair.Key, pair.Value))
            .ToList();
    }

    /// <summary>
    /// 롤백을 위해 현재 자격 증명 상태를 한 번만 읽어 둡니다.
    /// </summary>
    private static bool TryCapturePreviousPassword(
        string userName,
        IDictionary<string, string?> previousPasswords,
        out AppDataOperationResult result)
    {
        result = Ok(nameof(SaveAccountsWithCredentialsAsync), AccountsFileName);

        if (previousPasswords.ContainsKey(userName))
            return true;

        PasswordVaultHelper.PasswordVaultReadResult readResult = PasswordVaultHelper.TryGetPassword(userName);
        if (!readResult.Success)
        {
            result = Fail(nameof(SaveAccountsWithCredentialsAsync), readResult.Exception!, AppDataErrorKind.CredentialVault, AccountsFileName);
            return false;
        }

        if (readResult.Status == PasswordVaultHelper.PasswordVaultReadStatus.IgnoredInvalidInput)
        {
            result = Fail(
                nameof(SaveAccountsWithCredentialsAsync),
                CreateUnexpectedPasswordVaultStateException(
                    $"Password vault read ignored a normalized account credential input. UserName='{userName}'",
                    readResult.Exception),
                AppDataErrorKind.CredentialVault,
                AccountsFileName);
            return false;
        }

        previousPasswords[userName] = readResult.HasCredential ? readResult.Password : null;
        return true;
    }

    /// <summary>
    /// 비밀번호 저장 중간에 실패했을 때 이전 자격 증명 상태로 되돌립니다.
    /// </summary>
    private static void RollbackCredentialUpserts(IReadOnlyDictionary<string, string?> previousPasswords)
    {
        foreach ((string userName, string? previousPassword) in previousPasswords)
        {
            if (string.IsNullOrWhiteSpace(previousPassword))
            {
                PasswordVaultHelper.PasswordVaultOperationResult deleteResult = PasswordVaultHelper.TryDelete(userName);
                if (!deleteResult.Success)
                {
                    Debug.WriteLine($"[AppDataManager] Credential rollback delete failed. UserName: {userName}, Error: {deleteResult.Exception}");
                }
                else if (deleteResult.Status == PasswordVaultHelper.PasswordVaultOperationStatus.IgnoredInvalidInput)
                {
                    Debug.WriteLine($"[AppDataManager] Credential rollback delete ignored unexpectedly. UserName: {userName}");
                }

                continue;
            }

            PasswordVaultHelper.PasswordVaultOperationResult saveResult = PasswordVaultHelper.TrySave(userName, previousPassword);
            if (!saveResult.Success)
            {
                Debug.WriteLine($"[AppDataManager] Credential rollback save failed. UserName: {userName}, Error: {saveResult.Exception}");
            }
            else if (saveResult.Status == PasswordVaultHelper.PasswordVaultOperationStatus.IgnoredInvalidInput)
            {
                Debug.WriteLine($"[AppDataManager] Credential rollback save ignored unexpectedly. UserName: {userName}");
            }
        }
    }

    /// <summary>
    /// 정규화된 계정 자격 증명 경로에서 발생하면 안 되는 PasswordVault 상태를 예외로 변환합니다.
    /// </summary>
    private static Exception CreateUnexpectedPasswordVaultStateException(string message, Exception? innerException = null)
        => innerException is null
            ? new InvalidOperationException(message)
            : new InvalidOperationException(message, innerException);

    /// <summary>
    /// 계정 저장 후 프리셋 참조와 자격 증명을 현재 계정 목록 기준으로 다시 정리합니다.
    /// </summary>
    private static async Task<AppDataOperationResult> SyncAccountsAndPresetsAfterSaveAsync(IReadOnlyList<Account> accounts)
    {
        try
        {
            PasswordVaultHelper.PasswordVaultOperationResult removeOrphansResult =
                PasswordVaultHelper.TryRemoveOrphans(accounts.Select(account => account.Id));
            if (!removeOrphansResult.Success)
                return Fail(nameof(SyncAccountsAndPresetsAfterSaveAsync), removeOrphansResult.Exception!, AppDataErrorKind.CredentialVault, AccountsFileName);

            return await NormalizePresetListAgainstAccountsAsync(accounts).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Fail(nameof(SyncAccountsAndPresetsAfterSaveAsync), ex, AppDataErrorKind.CredentialVault, AccountsFileName);
        }
    }

    /// <summary>
    /// 현재 계정 목록에 없는 프리셋 ID를 비워서 프리셋 유효성을 유지합니다.
    /// </summary>
    private static async Task<AppDataOperationResult> NormalizePresetListAgainstAccountsAsync(IEnumerable<Account> accounts)
    {
        try
        {
            (string? json, AppDataOperationResult readResult) = await ReadTextFromLocalFolderAsync(PresetListFileName).ConfigureAwait(false);
            if (!readResult.Success)
                return readResult;

            PresetList presetList;
            Exception? deserializeException = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                presetList = new PresetList();
            }
            else
            {
                try
                {
                    presetList = JsonSerializer.Deserialize(json, AppDataJsonSerializerContext.Default.PresetList) ?? new PresetList();
                }
                catch (Exception ex)
                {
                    deserializeException = ex;
                    presetList = new PresetList();
                }
            }

            HashSet<string> validIds = accounts
                .Select(account => account.Id?.Trim() ?? string.Empty)
                .Where(id => id.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            bool changed = EnsurePresetListShape(presetList);
            changed |= NormalizePresetIds(presetList, validIds);

            if (!changed)
                return deserializeException is null
                    ? Ok(nameof(NormalizePresetListAgainstAccountsAsync), PresetListFileName)
                    : Fail(nameof(NormalizePresetListAgainstAccountsAsync), deserializeException, AppDataErrorKind.Serialization, PresetListFileName);

            AppDataOperationResult saveResult = await SavePresetListAsync(presetList).ConfigureAwait(false);
            if (!saveResult.Success)
                return saveResult;

            return deserializeException is null
                ? Ok(nameof(NormalizePresetListAgainstAccountsAsync), PresetListFileName)
                : Fail(nameof(NormalizePresetListAgainstAccountsAsync), deserializeException, AppDataErrorKind.Serialization, PresetListFileName);
        }
        catch (Exception ex)
        {
            return Fail(nameof(NormalizePresetListAgainstAccountsAsync), ex, PresetListFileName);
        }
    }

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
