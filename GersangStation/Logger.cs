using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GersangStation {
    internal class Logger {
        private static string logFile = "./log/" + DateTime.Now.ToShortDateString() + "-" + DateTime.Now.Hour + "시 " + DateTime.Now.Minute + "분 " + DateTime.Now.Second + "초_log.txt";

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
