using Core.Models;
using Windows.Storage;

namespace Core;

public static partial class AppDataManager
{
    private const string MainWindowPlacementSettingKey = "MainWindowPlacement";

    public static WindowPlacementSettings? MainWindowPlacement
    {
        get
        {
            var value = LoadLocalSetting<ApplicationDataCompositeValue?>(MainWindowPlacementSettingKey, null);
            if (value is null ||
                !value.TryGetValue(nameof(WindowPlacementSettings.WidthInDips), out object? widthValue) || widthValue is not double width ||
                !value.TryGetValue(nameof(WindowPlacementSettings.HeightInDips), out object? heightValue) || heightValue is not double height ||
                !value.TryGetValue(nameof(WindowPlacementSettings.IsMaximized), out object? maximizedValue) || maximizedValue is not bool isMaximized ||
                !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
                return null;

            return new WindowPlacementSettings(width, height, isMaximized);
        }
        set
        {
            if (value is not { } placement)
            {
                LocalSettings.Values.Remove(MainWindowPlacementSettingKey);
                return;
            }

            if (!double.IsFinite(placement.WidthInDips) || !double.IsFinite(placement.HeightInDips) ||
                placement.WidthInDips <= 0 || placement.HeightInDips <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            SaveLocalSetting(MainWindowPlacementSettingKey, new ApplicationDataCompositeValue
            {
                [nameof(WindowPlacementSettings.WidthInDips)] = placement.WidthInDips,
                [nameof(WindowPlacementSettings.HeightInDips)] = placement.HeightInDips,
                [nameof(WindowPlacementSettings.IsMaximized)] = placement.IsMaximized
            });
        }
    }
}
