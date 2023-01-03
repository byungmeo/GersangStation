namespace GersangStation {
    internal static class Program {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            bool flagMutex; //중복 실행 여부 확인

            Mutex mutex = new Mutex(true, "GersangStation", out flagMutex);
            if (flagMutex) {
                // To customize application configuration such as set high DPI settings or default font,
                // see https://aka.ms/applicationconfiguration.
                //ApplicationConfiguration.Initialize();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetHighDpiMode(HighDpiMode.DpiUnawareGdiScaled); // DpiUnaware와 비슷하지만 GDI/GDI+ 기반 콘텐츠의 품질을 향상시킵니다.
                //Application.SetHighDpiMode(HighDpiMode.DpiUnaware); // 애플리케이션 창은 DPI 변경에 맞게 조정되지 않으며 항상 100% 배율을 가정합니다.

                // MaterialSkin.2 컴포넌트들은 Dpi가 변경되면 원래 크기를 유지하지만, 직접 추가한 버튼 등은 아래 모드를 사용 시 혼자서 크기가 커져버림
                //Application.SetHighDpiMode(HighDpiMode.SystemAware); // 창은 기본 모니터의 DPI를 한 번 쿼리하고 모든 모니터의 애플리케이션에 이를 사용합니다.
                //Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); // PerMonitor와 비슷하지만 자식 창 DPI 변경 알림, comctl32.dll 컨트롤 크기 조정 개선, 대화 상자 크기 조정이 가능합니다.


                Application.Run(new Form1());
                mutex.ReleaseMutex();
            } else {
                MessageBox.Show("중복 실행은 불가능 합니다.", "중복 실행", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
    }
}