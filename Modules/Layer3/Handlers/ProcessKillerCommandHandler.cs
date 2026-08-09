// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles task management commands to terminate running applications/processes by name.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace JarvisLauncher
{
    public class ProcessKillerCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            var parts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;

            string cmd = parts[0];
            return SearchUtil.IsClose(cmd, "kill") || SearchUtil.IsClose(cmd, "terminate") || SearchUtil.IsClose(cmd, "stop");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim();

            var parts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return suggestions;

            string cmd = parts[0].ToLower();
            double similarity = SearchUtil.GetSimilarity(cmd, "kill");

            if (parts.Length > 1)
            {
                string targetProcess = query.Substring(cmd.Length).Trim().ToLower();
                if (targetProcess.EndsWith(".exe"))
                {
                    targetProcess = targetProcess.Substring(0, targetProcess.Length - 4);
                }

                suggestions.Add(new CommandResult
                {
                    Title = $"Kill Process: \"{targetProcess}\"",
                    Description = $"Terminate all running instances of '{targetProcess}'",
                    Execute = () => KillProcessByName(targetProcess),
                    Similarity = similarity
                });
            }
            else
            {
                suggestions.Add(new CommandResult
                {
                    Title = "Kill Process (Prompt)...",
                    Description = "Prompt for a running application name to terminate",
                    Execute = () => InputPromptOverlay.Show("Enter process name to terminate (e.g. chrome, discord):", (procName) => KillProcessByName(procName)),
                    Similarity = similarity + 0.5
                });

                suggestions.Add(new CommandResult
                {
                    Title = "Kill Process...",
                    Description = "Type a process name (e.g. 'kill chrome')",
                    Execute = null,
                    Similarity = similarity
                });
            }

            return suggestions;
        }

        private static void KillProcessByName(string processName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length == 0)
                {
                    TextOverlay.Show($"⚠️ No running process found matching '{processName}'", 3000);
                    return;
                }

                int killedCount = 0;
                foreach (var proc in processes)
                {
                    try
                    {
                        proc.Kill();
                        proc.Dispose();
                        killedCount++;
                    }
                    catch
                    {
                        // Swallow individual permission errors
                    }
                }

                if (killedCount > 0)
                {
                    TextOverlay.Show($"💀 Terminated {killedCount} instance(s) of '{processName}'", 3000);
                }
                else
                {
                    TextOverlay.Show($"⚠️ Found '{processName}' but failed to kill (Access Denied)", 3500);
                }
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Process killer error: {ex.Message}", 3000);
            }
        }
    }
}
