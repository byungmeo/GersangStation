using System.Diagnostics;

namespace GersangStation.Modules {
    public static class Logger {
        static string FILE_NAME = $"LOG_{DateTime.Now}.txt".Replace(':', '-');
        public static void Log(string msg) {
            try {
                if(!Directory.Exists("log")) Directory.CreateDirectory("log");
                using StreamWriter sw = new StreamWriter($"log/{FILE_NAME}", true, System.Text.Encoding.UTF8);
                sw.WriteLine($"[{DateTime.Now}] : {msg}");
            } catch (Exception e) {
                Trace.WriteLine(e.Message);
            }
        }
        public static void Log(Exception ex) {
            try {
                if(!Directory.Exists("log")) Directory.CreateDirectory("log");
                using StreamWriter sw = new StreamWriter($"log/{FILE_NAME}", true, System.Text.Encoding.UTF8);
                sw.WriteLine($"[{DateTime.Now}] : " +
                    $"\nMessage ---\n{ex.Message}\n" +
                    $"\nStackTrace ---\n{ex.StackTrace}");
            } catch(Exception e) {
                Trace.WriteLine(e.Message);
            }
        }

        public static void Log(string msg, Exception ex) {
            try {
                if(!Directory.Exists("log")) Directory.CreateDirectory("log");
                using StreamWriter sw = new StreamWriter($"log/{FILE_NAME}", true, System.Text.Encoding.UTF8);
                sw.WriteLine($"[{DateTime.Now}] : " +
                    $"\n{msg}\n" +
                    $"\nMessage ---\n{ex.Message}\n" +
                    $"\nStackTrace ---\n{ex.StackTrace}");
            } catch(Exception e) {
                Trace.WriteLine(e.Message);
            }
        }
    }
}