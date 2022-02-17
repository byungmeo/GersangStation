using System.Diagnostics;

namespace GersangStation {
    internal class Logger {
        private static string logFile = "./log/" + DateTime.Now.ToShortDateString() + "_" + DateTime.Now.Hour + "시 " + DateTime.Now.Minute + "분 " + DateTime.Now.Second + "초_log.txt";

        public static void DeleteOldLogFile() {
            try {
                string today = DateTime.Now.ToShortDateString();
                if (Directory.Exists("./log")) {
                    string[] files = Directory.GetFiles("./log");
                    if (files.Length >= 2) {
                        foreach (string filePath in files) {
                            string fileName = new FileInfo(filePath).Name;
                            string logDate = fileName.Split('_')[0];
                            TimeSpan span = DateTime.Parse(today).Subtract(DateTime.Parse(logDate));
                            if (span.TotalDays > 7) {
                                Log("Log: " + "작성된지 1주일이 지난 로그 파일을 삭제\r\n  :" + filePath);
                                File.Delete(filePath);
                            }
                        }
                    }
                }
            } catch (Exception e) {
                Log("Log: " + "작성된지 1주일이 지난 로그 파일을 삭제하려던 중 예외가 발생하였습니다.\r\n  :" + e.Message);
                Trace.WriteLine(e.Message);
                Trace.WriteLine(e.StackTrace);
            }
        }

        public static void Log(string logMessage) {
            Directory.CreateDirectory("./log");
            using (StreamWriter w = File.AppendText(logFile)) {
                w.Write("Log Entry : ");
                w.WriteLine($"{DateTime.Now.ToLongTimeString()} {DateTime.Now.ToLongDateString()}");
                w.WriteLine($"  :{logMessage}");
                w.WriteLine("-------------------------------\r\n");
            }
        }
    }
}
