using System;
using System.Diagnostics;

namespace Tdx.Core.Utils
{
    public static class Log
    {
        [Conditional("DEBUG")]
        public static void Debug(string message)
        {
            Console.WriteLine($"[DEBUG] {DateTime.Now:O} - {message}");
        }

        public static void Info(string message)
        {
            Console.WriteLine($"[INFO] {DateTime.Now:O} - {message}");
        }

        public static void Error(string message, Exception? ex = null)
        {
            Console.Error.WriteLine($"[ERROR] {DateTime.Now:O} - {message}");
            if (ex != null)
            {
                Console.Error.WriteLine(ex.ToString());
            }
        }
    }
}
