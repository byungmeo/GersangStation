using GersangStation.Modules;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GersangStation;

internal static class Program {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private static readonly IntPtr HWND_BROADCAST = new(0xffff);
    internal static readonly uint WM_SHOW_GERSANGSTATION =
        RegisterWindowMessage("GersangStation.ShowMainWindow");

    [STAThread]
    static void Main() {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Logger.Log("Application.ThreadException", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => {
            if(e.ExceptionObject is Exception ex) {
                Logger.Log("AppDomain.CurrentDomain.UnhandledException", ex);
            } else {
                Logger.Log($"AppDomain.CurrentDomain.UnhandledException (non-exception): {e.ExceptionObject}");
            }
        };
        TaskScheduler.UnobservedTaskException += (_, e) => {
            Logger.Log("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
        Logger.Log("Program.Main start");

        Process current = Process.GetCurrentProcess();
        string currentProcessPath = current.MainModule?.FileName ?? Application.ExecutablePath;

        Process? other = null;
        foreach(var process in Process.GetProcessesByName("GersangStation")) {
            if(process.Id == current.Id) {
                continue;
            }

            if(IsSameLegacyProcess(process, current.Id, currentProcessPath)) {
                other = process;
                break;
            }
        }

        // 중복 실행인 경우 기존 프로세스에게 자신을 표시하라고 요청한다.
        if(other != null) {
            PostMessage(HWND_BROADCAST, WM_SHOW_GERSANGSTATION, IntPtr.Zero, IntPtr.Zero);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Logger.Log("Application.Run(new Form1())");
        Application.Run(new Form1());
        ClipMouse.Stop(true);
    }

    private static bool IsSameLegacyProcess(Process process, int currentProcessId, string currentProcessPath) {
        try {
            if(process.Id == currentProcessId) {
                return true;
            }

            string? processPath = process.MainModule?.FileName;
            return string.Equals(processPath, currentProcessPath, StringComparison.OrdinalIgnoreCase);
        } catch(Exception ex) {
            Logger.Log($"IsSameLegacyProcess skipped. pid={process.Id}, name={process.ProcessName}, message={ex.Message}");
            return false;
        }
    }
}