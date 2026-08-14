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
            return query == "analyze" || query == "screen" || query == "analyzer" || query == "tile" || query == "tiling";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim().ToLower();

            double similarity = 0;
            if (query == "analyze" || query == "screen" || query == "analyzer") similarity = 3.0;
            else if (query == "tile" || query == "tiling") similarity = 2.8;

            // Option 1: Open the screen analysis dashboard
            suggestions.Add(new CommandResult
            {
                Title = "🖥️ Open Screen & Window Analyzer",
                Description = "Launch dominant palette extractor and workspace clutter audit dashboard",
                Similarity = similarity + 0.5,
                Execute = () => ScreenAnalysisOverlay.Open()
            });

            // Option 2: Instantly tile windows
            suggestions.Add(new CommandResult
            {
                Title = "🧩 Auto-Tile Windows",
                Description = "Arrange all visible open desktop windows in a clean side-by-side grid layout",
                Similarity = similarity + 0.2,
                Execute = () => ScreenAnalyzer.TileActiveWindows()
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
