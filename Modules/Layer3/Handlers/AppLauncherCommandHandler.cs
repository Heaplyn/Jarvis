// Developer: heaplyn
// Date: 2026-08-08
// Summary: Catch-all process launching handler that executes commands, URL links, and shortcut references via cmd shell.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace JarvisLauncher
{
    public class AppLauncherCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            return !string.IsNullOrWhiteSpace(query);
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim();

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
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c start \"\" {command}",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                if (command.Contains("://") || command.EndsWith(".exe") || command.EndsWith(".lnk"))
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = command,
                        UseShellExecute = true
                    };
                }
                else
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = command,
                        UseShellExecute = true
                    };
                }

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to launch process: {ex.Message}");
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c start {command}",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
                catch
                {
                    // Swallow
                }
            }
        }
    }
}
