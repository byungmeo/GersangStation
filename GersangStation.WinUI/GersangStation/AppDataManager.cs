using System;
using Windows.Storage;

namespace GersangStation;

internal static class AppDataManager
{
    private const string KeySetupCompleted = "SetupCompleted";

    public static bool IsSetupCompleted
    {
        get => GetValue(KeySetupCompleted, false);
        set => SetValue(KeySetupCompleted, value);
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