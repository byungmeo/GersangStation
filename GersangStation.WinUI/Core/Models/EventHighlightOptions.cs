namespace Core.Models;

public enum EventHighlightRepeatSpeed { Slow, Normal, Fast }

public sealed record EventHighlightOptions(
    bool Enabled,
    int ThresholdDays,
    uint ColorArgb,
    double BorderThickness,
    EventHighlightRepeatSpeed RepeatSpeed,
    bool ShowMessage)
{
    public const uint DefaultColorArgb = 0xFFCD5C5C;
    public const string DefaultMessage = "종료 임박! 보상을 수령하세요.";
    public static EventHighlightOptions Default { get; } = new(true, 3, DefaultColorArgb, 6,
        EventHighlightRepeatSpeed.Normal, true);

    public bool ShouldHighlight(int remainingDays) => Enabled && remainingDays >= 0 && remainingDays <= ThresholdDays;

    public TimeSpan GetPhaseDuration(int phase) => TimeSpan.FromMilliseconds(phase switch
    {
        0 or 2 => 192,
        1 => 128,
        _ => RepeatSpeed switch
        {
            EventHighlightRepeatSpeed.Slow => 4288,
            EventHighlightRepeatSpeed.Fast => 1088,
            _ => 1888
        }
    });

    public static double NormalizeThickness(double value)
        => double.IsFinite(value) ? Math.Clamp(Math.Round(value), 1, 24) : 6;
}
