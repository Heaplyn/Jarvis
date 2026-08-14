// Developer: heaplyn
// Date: 2026-08-13
// Summary: Catch-all process launching handler that executes commands, URL links, and shortcut references safely without triggering Windows shell error popups.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace JarvisLauncher
{
    public class AppLauncherCommandHandler : ICommandHandler
    {
        private static readonly HashSet<string> IncompleteCommands = new(StringComparer.OrdinalIgnoreCase)
        {
            "open", "start", "run", "launch", "search", "show", "play", "kill"
        };

        private static readonly string[] CommandKeywords = new[]
        {
            "setting", "option", "gui", "studio", "overlay", "voice", "llm", "mode", "help",
            "power", "restart", "shutdown", "alias", "volume", "notes", "music", "playlist", "debug"
        };

        public bool CanHandle(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return false;
            string q = query.Trim().ToLower();

            if (IncompleteCommands.Contains(q)) return false;

            // If query contains internal command keywords and isn't a file/URL, let specific handlers take precedence
            if (!q.Contains("://") && !q.EndsWith(".exe") && !q.EndsWith(".lnk") && !q.EndsWith(".bat"))
            {
                foreach (var kw in CommandKeywords)
                {
                    if (q.Contains(kw)) return false;
                }
            }

            return true;
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim();

            if (!CanHandle(query)) return suggestions;

            suggestions.Add(new CommandResult
            {
                Title = $"Run: {query}",
                Description = $"Execute '{query}' via Windows Shell",
                Execute = () => LaunchProcess(query),
                Similarity = 0.05
            });

            return suggestions;
        }

        private static void LaunchProcess(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;
            string trimmed = command.Trim();

            if (IncompleteCommands.Contains(trimmed))
            {
                TextOverlay.Show($"⚠️ Please specify what to open (e.g. 'open google' or 'open chrome')", 3000);
                return;
            }

            // Only attempt shell start if it's a URL, file path, or executable
            bool isUrl = trimmed.Contains("://");
            bool isFileOrPath = File.Exists(trimmed) || Directory.Exists(trimmed) || trimmed.EndsWith(".exe") || trimmed.EndsWith(".lnk") || trimmed.EndsWith(".bat");

            if (!isUrl && !isFileOrPath)
            {
                // Verify if command is in system PATH before passing to Shell
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c start \"\" \"{trimmed}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    TextOverlay.Show($"⚠️ '{trimmed}' not found: {ex.Message}", 3000);
                }
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = trimmed,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Cannot launch '{trimmed}': {ex.Message}", 3000);
            }
        }
    }
}
