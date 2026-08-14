// Developer: heaplyn
// Date: 2026-08-13
// Summary: User System Activity & History Context Manager.
// Gathers real-time active window, command history, recent debug logs, and clipboard context for AI queries.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace JarvisLauncher
{
    public static class UserActivityContextManager
    {
        private static readonly List<string> _recentQueries = new List<string>();
        private static readonly object _lock = new object();

        public static void TrackUserQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            lock (_lock)
            {
                _recentQueries.Add($"[{DateTime.Now:HH:mm:ss}] \"{query.Trim()}\"");
                if (_recentQueries.Count > 20) _recentQueries.RemoveAt(0);
            }
        }

        public static string BuildFullActivityContext()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[USER ENVIRONMENT & RECENT ACTIVITY CONTEXT]");
            sb.AppendLine($"• Current Local Time: {DateTime.Now:F}");

            // 1. Active Window Context
            try
            {
                ScreenMonitorEngine.UpdateActiveWindowInfo();
                string activeWin = ScreenMonitorEngine.ActiveWindowTitle;
                string activeProc = ScreenMonitorEngine.ActiveProcessName;
                if (!string.IsNullOrEmpty(activeWin))
                {
                    sb.AppendLine($"• Active Window: '{activeWin}' ({activeProc})");
                }
            }
            catch { }

            // 2. Recent Search & Command Queries
            lock (_lock)
            {
                if (_recentQueries.Count > 0)
                {
                    sb.AppendLine($"• Recent HUD Queries & Commands:");
                    foreach (var q in _recentQueries.TakeLast(5))
                    {
                        sb.AppendLine($"  - {q}");
                    }
                }
            }

            // 3. Clipboard Context (If Text)
            try
            {
                if (Application.Current != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (Clipboard.ContainsText())
                        {
                            string clip = Clipboard.GetText().Trim();
                            if (!string.IsNullOrEmpty(clip))
                            {
                                string preview = clip.Length > 200 ? clip.Substring(0, 200) + "..." : clip;
                                preview = preview.Replace("\r", " ").Replace("\n", " ");
                                sb.AppendLine($"• Current Clipboard Content: \"{preview}\"");
                            }
                        }
                    });
                }
            }
            catch { }

            sb.AppendLine("--------------------------------------------------");
            return sb.ToString();
        }
    }
}
