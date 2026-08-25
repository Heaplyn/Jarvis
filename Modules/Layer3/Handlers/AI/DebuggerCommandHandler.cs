// Developer: heaplyn
// Date: 2026-08-17
// Summary: Handles CLI commands for system debugging, diagnostics, and process inspection.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace JarvisLauncher
{
    public class DebuggerCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            string q = query.Trim().ToLower();
            return q.StartsWith("debug") || q.StartsWith("inspect") || q.StartsWith("diagnostics") ||
                   q.StartsWith("perf") || q.StartsWith("monitor") || q.StartsWith("console");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            string q = query.Trim().ToLower();

            if (q.Contains("console") || q.Contains("logs"))
            {
                suggestions.Add(new CommandResult
                {
                    TITLE = "📝 View AI Execution Logs (Debug Console)",
                    DESCRIPTION = "Show the raw internal monologue and tool execution steps of the agent",
                    SIMILARITY = 9.5,
                    EXECUTE = () => DebugConsoleOverlay.ShowOverlay()
                });
            }

            suggestions.Add(new CommandResult
            {
                TITLE = "🛠️ Open Jarvis System Debugger",
                DESCRIPTION = "Real-time process inspection, memory tracking, and performance logs",
                SIMILARITY = 9.0,
                EXECUTE = () => SystemMonitorOverlay.ShowOverlay()
            });

            suggestions.Add(new CommandResult
            {
                TITLE = "📊 Open Performance Monitor",
                DESCRIPTION = "Track CPU, RAM, and Disk IO in a floating HUD panel",
                SIMILARITY = 8.0,
                EXECUTE = () => SystemSpecsOverlay.ShowOverlay()
            });

            suggestions.Add(new CommandResult
            {
                TITLE = "🔍 Inspect Active Processes",
                DESCRIPTION = "View and manage all running background tasks",
                SIMILARITY = 7.5,
                EXECUTE = () => ProcessManagerOverlay.ShowOverlay()
            });

            return suggestions;
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("debug", "Open system debugging suite", "debug"),
                new CommandDesc("debug console", "View AI internal execution logs", "debug console"),
                new CommandDesc("monitor", "Monitor system performance", "monitor"),
                new CommandDesc("inspect", "Inspect active processes", "inspect"),
                new CommandDesc("diagnostics", "Run system diagnostics", "diagnostics"),
                new CommandDesc("perf", "Show performance statistics", "perf")
            };
        }
    }
}
