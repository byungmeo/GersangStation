using Core.Models;

namespace Core;

public static partial class AppDataManager
{
    public static int EventUrgencyThresholdDays
    {
        get => Math.Max(0, LoadLocalSetting(EventUrgencyThresholdDays_SettingKey, defaultValue: 3));
        set => SaveEventHighlightSetting(EventUrgencyThresholdDays_SettingKey, Math.Max(0, value), EventUrgencyThresholdDays);
    }

    public static bool EventUrgencyEnabled
    {
        get => LoadLocalSetting("EventUrgencyEnabled", true);
        set => SaveEventHighlightSetting("EventUrgencyEnabled", value, EventUrgencyEnabled);
    }

    public static uint EventUrgencyColorArgb
    {
        get => LoadLocalSetting("EventUrgencyColorArgb", EventHighlightOptions.DefaultColorArgb) | 0xFF000000;
        set => SaveEventHighlightSetting("EventUrgencyColorArgb", value | 0xFF000000, EventUrgencyColorArgb);
    }

    public static double EventUrgencyBorderThickness
    {
        get => EventHighlightOptions.NormalizeThickness(LoadLocalSetting("EventUrgencyBorderThickness", 6d));
        set => SaveEventHighlightSetting("EventUrgencyBorderThickness", EventHighlightOptions.NormalizeThickness(value), EventUrgencyBorderThickness);
    }

    public static EventHighlightRepeatSpeed EventUrgencyRepeatSpeed
    {
        get
        {
            int stored = LoadLocalSetting("EventUrgencyRepeatSpeed", (int)EventHighlightRepeatSpeed.Normal);
            return Enum.IsDefined(typeof(EventHighlightRepeatSpeed), stored)
                ? (EventHighlightRepeatSpeed)stored : EventHighlightRepeatSpeed.Normal;
        }
        set => SaveEventHighlightSetting("EventUrgencyRepeatSpeed",
            Enum.IsDefined(value) ? (int)value : (int)EventHighlightRepeatSpeed.Normal, (int)EventUrgencyRepeatSpeed);
    }

    public static bool EventUrgencyMessageEnabled
    {
        get => LoadLocalSetting("EventUrgencyMessageEnabled", true);
        set => SaveEventHighlightSetting("EventUrgencyMessageEnabled", value, EventUrgencyMessageEnabled);
    }

    public static EventHighlightOptions EventHighlightSettings => new(EventUrgencyEnabled, EventUrgencyThresholdDays,
        EventUrgencyColorArgb, EventUrgencyBorderThickness, EventUrgencyRepeatSpeed, EventUrgencyMessageEnabled);

    private static void SaveEventHighlightSetting<T>(string key, T value, T previous)
    {
        if (EqualityComparer<T>.Default.Equals(value, previous)) return;
        SaveLocalSetting(key, value);
        EventHighlightSettingsChanged?.Invoke(null, EventArgs.Empty);
    }

    public static event EventHandler? EventHighlightSettingsChanged;
}
