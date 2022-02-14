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
                //Application.SetHighDpiMode(HighDpiMode.SystemAware); -> 100% 보다 높은 DPI 사용 시 인터페이스 위치가 깨짐
                Application.SetHighDpiMode(HighDpiMode.DpiUnaware);

                Application.Run(new Form1());
                mutex.ReleaseMutex();
            } else {
                MessageBox.Show("중복 실행은 불가능 합니다.", "중복 실행", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
    }
}