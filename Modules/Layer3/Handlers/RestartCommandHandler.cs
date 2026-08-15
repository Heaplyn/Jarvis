// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles application commands to restart the active Jarvis launcher thread or environment.

using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace JarvisLauncher
{
    public class RestartCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "restart" || query == "re" || query == "reload" || query == "restart jarvis" || query == "fresh boot";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();

            suggestions.Add(new CommandResult
            {
                TITLE = "🔄 Restart Jarvis HUD (Fresh Boot)",
                DESCRIPTION = "Purge caches, clean build, and cold-start the application",
                EXECUTE = () =>
                {
                    TextOverlay.Show("Initiating Fresh Restart...", 1000);
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    timer.Tick += (s, ev) =>
                    {
                        timer.Stop();
                        NativeMethods.Restart(freshBoot: true);
                    };
                    timer.Start();
                },
                SIMILARITY = 8.0
            });

            return suggestions;
        }
    }
}
