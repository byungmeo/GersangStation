using GersangStation.Modules;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GersangStation;

internal static class Program {
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("User32.dll")]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string strClassName, string strWindowName);
    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int ProcessId);

    // nCmdShow
    private const int SW_NORMAL = 1;
    private const int SW_SHOWMINIMIZED = 2;
    private const int SW_MAXIMIZE = 3;
    private const int SW_RESTORE = 9;

    [STAThread]
    static void Main() {
        Process current = Process.GetCurrentProcess();
        Process[] processes = Process.GetProcessesByName("GersangStation");

        // 중복 실행인 경우 기존 프로세스를 찾아 창을 활성화 시킨다.
        if(processes.Length > 1) {
            if (processes.Length > 2) return; // 이미 다른 프로세스가 일련의 작업 수행 중일 수 있음
            // 기존 프로세스를 찾는다
            Process? other = null;
            foreach(var process in processes) {
                if (process != current) {
                    other = process;
                    break;
                }
            }
            if(other == null) return;

            // MainWindowHandle이 Zero인 경우 Tray 모드라는 것을 의미한다 (Minimize & ShowInTaskbar = false 상태)
            // https://stackoverflow.com/questions/25961231/unhide-process-by-its-process-name
            if (other.MainWindowHandle == IntPtr.Zero) { 
                IntPtr handle = IntPtr.Zero;
                int prcsId = 0;
                do {
                    handle = FindWindowEx(IntPtr.Zero, handle, null, "GersangStation");
                    GetWindowThreadProcessId(handle, out prcsId); //get ProcessId from "handle"

                    //if it matches what we are looking
                    if(prcsId == other.Id) {
                        ShowWindow(handle, SW_RESTORE); //Show Window
                        SetForegroundWindow(handle);
                        break;
                    }
                } while(handle != IntPtr.Zero);
            } else {
                // Tray 모드가 아닌 이상 HWND가 살아있으므로, Normal 모드로 창을 띄운다.
                ShowWindow(other.MainWindowHandle, SW_NORMAL);
                SetForegroundWindow(other.MainWindowHandle);
            }
        } else {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            //ApplicationConfiguration.Initialize();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 애플리케이션 창은 DPI 변경에 맞게 조정되지 않으며 항상 100% 배율을 가정합니다. (기본값)
            // Application.SetHighDpiMode(HighDpiMode.DpiUnaware);

            // Dpi가 높아지면 메인화면의 1번설정버튼을 누를 수 없음 (밑에도 마찬가지, 높아질수록 뭔가가 가리는듯)
            // Application.SetHighDpiMode(HighDpiMode.DpiUnawareGdiScaled); // DpiUnaware와 비슷하지만 GDI/GDI+ 기반 콘텐츠의 품질을 향상시킵니다.

            // MaterialSkin.2 컴포넌트들은 Dpi가 변경되면 원래 크기를 유지하지만, 직접 추가한 버튼 등은 아래 모드를 사용 시 혼자서 크기가 커져버림
            // Application.SetHighDpiMode(HighDpiMode.SystemAware); // 창은 기본 모니터의 DPI를 한 번 쿼리하고 모든 모니터의 애플리케이션에 이를 사용합니다.
            // Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); // PerMonitor와 비슷하지만 자식 창 DPI 변경 알림, comctl32.dll 컨트롤 크기 조정 개선, 대화 상자 크기 조정이 가능합니다.

            Application.Run(new Form1());
            ClipMouse.Stop(true);
        }
    }
}