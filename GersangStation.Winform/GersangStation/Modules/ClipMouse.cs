using System.Runtime.InteropServices;
using GersangStation.Shared.Input;

namespace GersangStation.Modules;

/// <summary>Adapts WinForms settings, hotkeys and tray notifications to the shared WinUI-based engine.</summary>
public static class ClipMouse {
    private const int HotKeyId = 33333;
    private static MouseConfinementService? engine;
    public static NotifyIcon? icon;
    public static event Action? MonitoringStopped;

    private static MouseConfinementService Engine => engine ??= CreateEngine();

    private static MouseConfinementService CreateEngine() {
        var result = new MouseConfinementService(new Progress<MouseConfinementDiagnostic>(OnDiagnostic));
        result.ConfigureCompatibility(
            bool.Parse(ConfigManager.GetConfig("use_clip_disable_hotkey")),
            bool.Parse(ConfigManager.GetConfig("use_clip_only_first")));
        return result;
    }

    private static void OnDiagnostic(MouseConfinementDiagnostic diagnostic) {
        Logger.Log("MouseConfinement." + diagnostic.Operation, diagnostic.Exception);
        if(!diagnostic.MonitoringStopped || engine == null || engine.IsEnabled) return;
        ConfigManager.SetConfig("use_clip_mouse", false.ToString());
        MonitoringStopped?.Invoke();
        if(icon != null) {
            icon.BalloonTipTitle = "마우스 가두기 중단";
            icon.BalloonTipText = "마우스 가두기 오류가 발생해 기능을 중단했습니다. 로그를 확인해주세요.";
            icon.ShowBalloonTip(3000);
        }
    }

    public static int GetHotKeyId() => HotKeyId;
    public static bool isRunning() => engine?.IsEnabled == true;

    /// <summary>Applies the WinForms-only compatibility options without duplicating cursor logic.</summary>
    public static void UpdateOptions() {
        Engine.ConfigureCompatibility(
            bool.Parse(ConfigManager.GetConfig("use_clip_disable_hotkey")),
            bool.Parse(ConfigManager.GetConfig("use_clip_only_first")));
    }

    public static void ResetFirstWindow() => Engine.ResetFirstWindow();

    public static bool Run() {
        if(isRunning()) return false;
        Engine.SetEnabled(true);
        if(icon != null) {
            icon.Visible = true;
            icon.BalloonTipTitle = "향상된 마우스 가두기 ON";
            icon.BalloonTipText = "Alt 버튼을 누르면 일시적으로 빠져나올 수 있습니다.";
            icon.ShowBalloonTip(3000);
        }
        return true;
    }

    public static bool Stop(bool isFormClosed) {
        bool wasRunning = isRunning();
        if(isFormClosed) {
            engine?.Dispose();
            engine = null;
            icon = null;
            MonitoringStopped = null;
            return wasRunning;
        }
        engine?.SetEnabled(false);
        if(wasRunning && icon != null) {
            icon.Visible = true;
            icon.BalloonTipTitle = "향상된 마우스 가두기 OFF";
            icon.BalloonTipText = "설정 또는 지정한 단축키로 다시 활성화할 수 있습니다.";
            icon.ShowBalloonTip(3000);
        }
        return wasRunning;
    }

    /// <summary>Registers the persisted WinForms key or modifier/key combination.</summary>
    public static bool RegisterHotKey(IntPtr window, string value) {
        string[] parts = value.Split(',');
        int modifiers = parts.Length == 2 ? parts[0] switch {
            "Ctrl" => 0x0002, "Alt" => 0x0001, "Shift" => 0x0004, _ => 0
        } : 0;
        return RegisterHotKey(window, HotKeyId, modifiers | 0x4000, int.Parse(parts[^1]));
    }

    public static bool UnregisterHotKey(IntPtr window) => UnregisterHotKey(window, HotKeyId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, int modifiers, int key);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
