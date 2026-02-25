namespace GersangStation.Setup;

internal static class SetupFlowState
{
    public static string InstallPath { get; set; } = "";

    public static bool ShouldAutoSkipMultiClient { get; set; }

    public static void Reset()
    {
        InstallPath = "";
        ShouldAutoSkipMultiClient = false;
    }
}
