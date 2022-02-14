namespace GersangStation {
    internal static class Program {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            bool flagMutex; //�ߺ� ���� ���� Ȯ��

            Mutex mutex = new Mutex(true, "GersangStation", out flagMutex);
            if (flagMutex) {
                // To customize application configuration such as set high DPI settings or default font,
                // see https://aka.ms/applicationconfiguration.
                //ApplicationConfiguration.Initialize();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                //Application.SetHighDpiMode(HighDpiMode.SystemAware); -> 100% ���� ���� DPI ��� �� �������̽� ��ġ�� ����
                Application.SetHighDpiMode(HighDpiMode.DpiUnaware);

                Application.Run(new Form1());
                mutex.ReleaseMutex();
            } else {
                MessageBox.Show("�ߺ� ������ �Ұ��� �մϴ�.", "�ߺ� ����", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
    }
}