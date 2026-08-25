// Developer: copilot
// Date: 2026-08-13
// Summary: Handles CLI commands to open/toggle the visual screen analyzer overlay or trigger window grid auto-tiling instantly.

using System;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class ScreenAnalysisCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "analyze" || query == "screen" || query == "analyzer" || query == "tile" || query == "tiling" ||
                   query == "screenvision" || query == "screenmonitor" || query == "screen ai" || query == "analyze screen" ||
                   query == "what is on my screen" || query == "explain screen" || query == "start screen monitoring" || query == "stop screen monitoring";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            string lower = query.Trim().ToLower();

            // Option 1: AI Screen Vision & Continuous Monitoring Studio
            suggestions.Add(new CommandResult
            {
                TITLE = "📹 Open AI Screen Vision & Continuous Monitoring Studio",
                DESCRIPTION = "Live screen preview, active window tracker, continuous screen watcher, & Gemini Vision AI analysis",
                SIMILARITY = 6.0,
                EXECUTE = () => ScreenVisionStudioOverlay.ShowOverlay()
            });

            // Option 2: Direct 1-Click AI Vision Screen Analysis
            if (lower.Contains("analyze") || lower.Contains("what is on my screen") || lower.Contains("explain"))
            {
                suggestions.Add(new CommandResult
                {
                    TITLE = "🧠 Analyze Current Screen with Gemini Vision AI",
                    DESCRIPTION = "Captures instant screen snapshot and explains visible code, windows, or applications",
                    SIMILARITY = 6.5,
                    EXECUTE = () => ScreenVisionStudioOverlay.ShowOverlay()
                });
            }

            // Option 3: Toggle Continuous Screen Monitor
            if (lower.Contains("start screen monitoring") || lower.Contains("stop screen monitoring"))
            {
                suggestions.Add(new CommandResult
                {
                    TITLE = ScreenMonitorEngine.IsMonitoring ? "🛑 Stop Continuous Screen Monitoring" : "📹 Start Continuous Screen Monitoring",
                    DESCRIPTION = "Toggle background automatic screen snapshot tracking (every 5 seconds)",
                    SIMILARITY = 6.5,
                    EXECUTE = () =>
                    {
                        ScreenMonitorEngine.Toggle();
                        TextOverlay.Show(ScreenMonitorEngine.IsMonitoring ? "📹 Screen Monitoring STARTED" : "🛑 Screen Monitoring STOPPED", 2500);
                    }
                });
            }

            // Option 4: Instantly tile windows
            suggestions.Add(new CommandResult
            {
                TITLE = "🧩 Auto-Tile Windows",
                DESCRIPTION = "Arrange all visible open desktop windows in a clean side-by-side grid layout",
                SIMILARITY = 4.5,
                EXECUTE = () => ScreenAnalyzer.TileActiveWindows()
            });

            return suggestions;
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("analyze / screen", "Open Workspace & Palette Analyzer dashboard", "analyze"),
                new CommandDesc("tile / tiling", "Instantly grid-tile all visible desktop windows", "tile")
            };
        }
    }
}
