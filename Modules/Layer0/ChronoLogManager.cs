// Developer: heaplyn
// Date: 2026-08-19
// Summary: Master Chronology and Activity Logging Manager.
//          Automatically records user actions, window transitions, commands, and major system events
//          into a persistent "External Brain" history to allow Jarvis to answer "What did I do yesterday?".

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public static class ChronoLogManager
    {
        private static readonly object _lock = new object();
        private static string LogDir => Path.Combine(PathHandler.GetDataDirectory(), "Context", "History");

        static ChronoLogManager()
        {
            if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);
        }

        public static void LogEvent(string category, string detail)
        {
            Task.Run(async () => {
                try {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string entry = $"[{timestamp}] [{category.ToUpper()}] {detail}";

                    string dateFile = Path.Combine(LogDir, $"{DateTime.Now:yyyy-MM-dd}.log");

                    lock (_lock) {
                        File.AppendAllText(dateFile, entry + Environment.NewLine);
                    }

                    // Also sync to the master Chronology.md for AI visibility
                    var memory = new MemoryNode {
                        Category = "Activity",
                        Content = $"User Activity: {category} - {detail}",
                        Timestamp = DateTime.Now
                    };
                    await ContextNotesManager.SyncMemoryToNotesAsync(memory);
                } catch { }
            });
        }

        public static string GetHistoryForDate(DateTime date)
        {
            string dateFile = Path.Combine(LogDir, $"{date:yyyy-MM-dd}.log");
            if (!File.Exists(dateFile)) return "No activity recorded for this date, Sir.";

            try {
                lock (_lock) {
                    var lines = File.ReadAllLines(dateFile);
                    if (lines.Length > 200) return string.Join(Environment.NewLine, lines.TakeLast(200)) + "\n... (Log truncated)";
                    return string.Join(Environment.NewLine, lines);
                }
            } catch { return "Error reading chronology logs."; }
        }

        public static void StartAutoTracker()
        {
            Task.Run(async () => {
                string lastWin = "";
                while (true) {
                    try {
                        string currentWin = CoreRegistry.Memory.GetCurrentWindowTitle();
                        if (currentWin != lastWin && !string.IsNullOrWhiteSpace(currentWin)) {
                            LogEvent("Window", $"Switched to: {currentWin}");
                            lastWin = currentWin;
                        }
                    } catch { }
                    await Task.Delay(10000); // Check every 10s
                }
            });
        }
    }
}
