// Developer: heaplyn
// Date: 2026-08-10
// Summary: Handles CLI commands to launch the Game Dev Creator Toolbox (Roblox/Blender utilities).

using System;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class GameDevToolboxCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "dev" || query == "toolbox" || query == "game" || query == "roblox" || query == "blender" || query == "rings" || query == "validator";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            double similarity = 2.0;

            suggestions.Add(new CommandResult
            {
                Title = "🎮 Open Game Creator Toolbox",
                Description = "Roblox Rings validator, Luau anim generators, and Blender texture bakers",
                Execute = () => GameDevToolboxOverlay.OpenToolbox(),
                Similarity = similarity + 1.0
            });

            return suggestions;
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("dev / roblox / blender", "Roblox & Blender game creator toolbox GUI", "dev")
            };
        }
    }
}
