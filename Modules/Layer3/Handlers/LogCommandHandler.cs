// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles 'log' and 'logs' queries by allowing viewing, opening in notepad, or clearing the persistent execution logs.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace JarvisLauncher
{
    public class LogCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "log" || query == "logs" || query.StartsWith("log ") || query.StartsWith("logs ");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim().ToLower();

            double similarity = SearchUtil.GetSimilarity(query, "logs");
            string logPath = GetLogPath();

            // Suggestion 1: View logs in Jarvis Terminal
            suggestions.Add(new CommandResult
            {
                Title       = "View System Logs",
                Description = "Read Jarvis execution history inside the System Terminal",
                Similarity  = similarity + 0.1,
                Execute     = () => ShowLogsInTerminal(logPath)
            });

            // Suggestion 2: Open log file in Notepad
            suggestions.Add(new CommandResult
            {
                Title       = "Open Logs in Notepad",
                Description = "Open the raw Jarvis.log file in your system text editor",
                Similarity  = similarity,
                Execute     = () => OpenLogInNotepad(logPath)
            });

            // Suggestion 3: Clear logs
            suggestions.Add(new CommandResult
            {
                Title       = "Clear System Logs",
                Description = "Permanently empty the Jarvis.log file on disk",
                Similarity  = similarity - 0.2,
                Execute     = () => ClearLogs(logPath)
            });

            return suggestions;
        }

        private static string GetLogPath()
        {
            string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            if (!Directory.Exists(dataDir))
            {
                string devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\Data"));
                if (Directory.Exists(devPath))
                {
                    dataDir = devPath;
                }
            }
            return Path.Combine(dataDir, "Jarvis.log");
        }

        private static void ShowLogsInTerminal(string logPath)
        {
            try
            {
                string logs = File.Exists(logPath) ? File.ReadAllText(logPath) : "[No Logs Found]";
                CliOutputOverlay.Show("System History Logs", logs);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to read logs:\n{ex.Message}", "Jarvis Log Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void OpenLogInNotepad(string logPath)
        {
            try
            {
                if (!File.Exists(logPath))
                {
                    // Create empty log file if missing
                    File.WriteAllText(logPath, "=== JARVIS INITIALIZED LOGS ===\n");
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName        = "notepad.exe",
                    Arguments       = $"\"{logPath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open log file:\n{ex.Message}", "Jarvis Log Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void ClearLogs(string logPath)
        {
            try
            {
                if (File.Exists(logPath))
                {
                    File.Delete(logPath);
                }
                TextOverlay.Show("🧹 System logs cleared successfully!", 2500);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to clear logs:\n{ex.Message}", "Jarvis Log Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
