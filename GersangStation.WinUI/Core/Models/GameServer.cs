namespace Core.Models;

public record GameServerOption(GameServer Server, string DisplayName);

public enum GameServer
{
    Korea_Live,
    Korea_Test,
    Korea_RnD
}

public class GameServerHelper
{
    public static List<GameServerOption> ServerOptions { get; } =
    [
        new(GameServer.Korea_Live, "본섭"),
        new(GameServer.Korea_Test, "테섭"),
        new(GameServer.Korea_RnD, "천라섭")
    ];

    public static List<GameServer> GameServers { get; } =
    [
        GameServer.Korea_Live,
        GameServer.Korea_Test,
        GameServer.Korea_RnD
    ];

    public static string GetGameStartParam(GameServer server) => server switch
    {
        GameServer.Korea_Live => "main",
        GameServer.Korea_Test => "test",
        GameServer.Korea_RnD => "inak",
        _ => throw new ArgumentOutOfRangeException(nameof(server), server, null)
    };

    public static string GetInstallPathRegKey(GameServer server) => server switch
    {
        GameServer.Korea_Live => "InstallPath",
        GameServer.Korea_Test => "TestPath",
        GameServer.Korea_RnD => "RNDPath",
        _ => throw new ArgumentOutOfRangeException(nameof(server), server, null)
    };

    public static string GetReadMeUrl(GameServer server) => server switch
    {
        GameServer.Korea_Live => "https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Readme/readme.txt",
        GameServer.Korea_Test => "https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Test_Server/Client_Readme/readme.txt",
        GameServer.Korea_RnD => "https://akgersang.xdn.kinxcdn.com/Gersang/Patch/RnD_Server/Client_Readme/readme.txt",
        _ => throw new ArgumentOutOfRangeException(nameof(server), server, null)
    };

    public static string GetFullClientUrl(GameServer server) => server switch
    {
        GameServer.Korea_Live => "http://ak-gersangkr.xcache.kinxcdn.com/FullClient/Gersang_Install.7z",
        GameServer.Korea_Test => "https://ak-gersangkr.xcache.kinxcdn.com/FullClient_Test/GerTest_Install.7z",
        GameServer.Korea_RnD => "http://ak-gersangkr.xcache.kinxcdn.com/FullClient_CheonRa/CheonRa_Install.7z",
        _ => throw new ArgumentOutOfRangeException(nameof(server), server, null)
    };

    public static string GetVersionInfoUrl(GameServer server, int version) => server switch
    {
        GameServer.Korea_Live => $"https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_info_File/{version}",
        GameServer.Korea_Test => $"https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Test_Server/Client_info_File/{version}",
        GameServer.Korea_RnD => $"https://akgersang.xdn.kinxcdn.com/Gersang/Patch/RnD_Server/Client_info_File/{version}",
        _ => throw new ArgumentOutOfRangeException(nameof(server), server, null)
    };

    public static string GetVsnUrl(GameServer server) => server switch
    {
        GameServer.Korea_Live => "https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/Online/vsn.dat.gsz",
        GameServer.Korea_Test => "https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Test_Server/Client_Patch_File/Online/vsn.dat.gsz",
        GameServer.Korea_RnD => "https://akgersang.xdn.kinxcdn.com/Gersang/Patch/RnD_Server/Client_Patch_File/Online/vsn.dat.gsz",
        _ => throw new ArgumentOutOfRangeException(nameof(server), server, null)
    };

    public static string GetPatchFileUrl(GameServer server, string relativePath) => server switch
    {
        GameServer.Korea_Live => $"https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/{relativePath}",
        GameServer.Korea_Test => $"https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Test_Server/Client_Patch_File/{relativePath}",
        GameServer.Korea_RnD => $"https://akgersang.xdn.kinxcdn.com/Gersang/Patch/RnD_Server/Client_Patch_File/{relativePath}",
        _ => throw new ArgumentOutOfRangeException(nameof(server), server, null)
    };
}
