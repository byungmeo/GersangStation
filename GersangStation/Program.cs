namespace GersangStation {
    internal static class Program {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            //ApplicationConfiguration.Initialize();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            //Application.SetHighDpiMode(HighDpiMode.SystemAware); -> 100% 보다 높은 DPI 사용 시 인터페이스 위치가 깨짐
            Application.SetHighDpiMode(HighDpiMode.DpiUnaware);

            Application.Run(new Form1());
        }
    }
}