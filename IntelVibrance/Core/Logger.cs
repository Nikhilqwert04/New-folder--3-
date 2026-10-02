using System;
using System.IO;

namespace IntelVibrance.Core
{
    /// <summary>
    /// Simple logger that writes to a log file in AppData.
    /// Intentionally lightweight - no external dependencies.
    /// </summary>
    public static class Logger
    {
        private static readonly string LogPath;
        private static readonly object _lock = new object();
        private static bool _initialized = false;

        static Logger()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "IntelVibrance");
            Directory.CreateDirectory(dir);
            LogPath = Path.Combine(dir, "vibrance.log");
        }

        public static void Initialize()
        {
            lock (_lock)
            {
                if (!_initialized)
                {
                    // Rotate log if > 1MB
                    if (File.Exists(LogPath))
                    {
                        var fi = new FileInfo(LogPath);
                        if (fi.Length > 1024 * 1024)
                        {
                            string backup = LogPath.Replace(".log", ".old.log");
                            if (File.Exists(backup)) File.Delete(backup);
                            File.Move(LogPath, backup);
                        }
                    }
                    _initialized = true;
                    Log("[INFO] ─────────────────────────────────");
                    Log("[INFO] Intel Vibrance — Session Started");
                    Log($"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    Log("[INFO] ─────────────────────────────────");
                }
            }
        }

        public static void Log(string message)
        {
            lock (_lock)
            {
                try
                {
                    string entry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
                    File.AppendAllText(LogPath, entry + Environment.NewLine);
                    System.Diagnostics.Debug.WriteLine(entry);
                }
                catch { /* Never let logging crash the app */ }
            }
        }

        public static string GetLogPath() => LogPath;

        public static string GetRecentLogs(int lines = 50)
        {
            try
            {
                if (!File.Exists(LogPath)) return "(No log file yet)";
                var allLines = File.ReadAllLines(LogPath);
                int start = Math.Max(0, allLines.Length - lines);
                return string.Join(Environment.NewLine, allLines, start, allLines.Length - start);
            }
            catch
            {
                return "(Could not read log)";
            }
        }
    }
}
