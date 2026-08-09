// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles PC power operations (sleep, shutdown, restart) using shell executing process scripts.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace JarvisLauncher
{
    public class PowerCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return SearchUtil.IsClose(query, "sleep") || 
                   SearchUtil.IsClose(query, "shutdown") || 
                   SearchUtil.IsClose(query, "rebootpc") ||
                   SearchUtil.IsClose(query, "restartpc");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim().ToLower();

            if (SearchUtil.IsClose(query, "sleep"))
            {
                suggestions.Add(new CommandResult
                {
                    Title = "Put PC to Sleep",
                    Description = "Enter standby/sleep mode immediately",
                    Execute = () => TriggerPowerState("sleep"),
                    Similarity = SearchUtil.GetSimilarity(query, "sleep")
                });
            }
            else if (SearchUtil.IsClose(query, "shutdown"))
            {
                suggestions.Add(new CommandResult
                {
                    Title = "Shut Down Computer",
                    Description = "Close all apps and turn off the PC",
                    Execute = () => TriggerPowerState("shutdown"),
                    Similarity = SearchUtil.GetSimilarity(query, "shutdown")
                });
            }
            else if (SearchUtil.IsClose(query, "rebootpc") || SearchUtil.IsClose(query, "restartpc"))
            {
                suggestions.Add(new CommandResult
                {
                    Title = "Restart Computer",
                    Description = "Reboot the operating system",
                    Execute = () => TriggerPowerState("restart"),
                    Similarity = Math.Max(SearchUtil.GetSimilarity(query, "rebootpc"), SearchUtil.GetSimilarity(query, "restartpc"))
                });
            }

            return suggestions;
        }

        private static void TriggerPowerState(string state)
        {
            try
            {
                if (state == "sleep")
                {
                    TextOverlay.Show("💤 Putting PC to sleep...", 2000);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "rundll32.exe",
                        Arguments = "powrprof.dll,SetSuspendState 0,1,0",
                        UseShellExecute = true
                    });
                }
                else if (state == "shutdown")
                {
                    TextOverlay.Show("🔌 Shutting down system...", 2000);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "shutdown.exe",
                        Arguments = "/s /t 0",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
                else if (state == "restart")
                {
                    TextOverlay.Show("🔄 Restarting system...", 2000);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "shutdown.exe",
                        Arguments = "/r /t 0",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Failed to trigger power command: {ex.Message}", 3000);
            }
        }
    }
}
