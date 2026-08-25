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
            return query == "restart" || query == "re" || query == "reload" || query == "restart jarvis" || query == "fresh boot" || query == "fresh start";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();

            // 1. Standard Restart (Now includes a lightweight rebuild if in Dev mode)
            suggestions.Add(new CommandResult
            {
                TITLE = "🔄 Restart & Sync (Quick)",
                DESCRIPTION = "Restart Jarvis and sync logic changes (Rebuilds if necessary)",
                EXECUTE = () =>
                {
                    TextOverlay.Show("Restarting & Syncing...", 800);
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.4) };
                    timer.Tick += (s, ev) =>
                    {
                        timer.Stop();
                        NativeMethods.Restart(freshBoot: true, pullFirst: false);
                    };
                    timer.Start();
                },
                SIMILARITY = 9.0
            });

            // 2. Explicit Fresh Start / Sync
            suggestions.Add(new CommandResult
            {
                TITLE = "♻️ Fresh Start (Run.bat)",
                DESCRIPTION = "Clean build, update binaries, and cold-start via run.bat",
                EXECUTE = () =>
                {
                    TextOverlay.Show("Launching run.bat lifecycle...", 1000);
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
                    timer.Tick += (s, ev) =>
                    {
                        timer.Stop();
                        NativeMethods.Restart(freshBoot: true, pullFirst: false);
                    };
                    timer.Start();
                },
                SIMILARITY = 10.0
            });

            // 3. Cloud Sync & Rebuild
            suggestions.Add(new CommandResult
            {
                TITLE = "🚀 Pull, Rebuild & Start",
                DESCRIPTION = "git pull latest main and perform full modular deployment",
                EXECUTE = () =>
                {
                    TextOverlay.Show("Syncing Cloud & Rebuilding...", 800);
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.4) };
                    timer.Tick += (s, ev) =>
                    {
                        timer.Stop();
                        NativeMethods.Restart(freshBoot: true, pullFirst: true);
                    };
                    timer.Start();
                },
                SIMILARITY = 7.5
            });

            return suggestions;
        }
    }
}
