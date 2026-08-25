// Developer: heaplyn
// Date: 2026-08-20
// Summary: Command handler that opens the visual developer command deck overlay.

using System;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class DevCommandsCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "dev" || query == "devcommands" || query == "dev commands" || query == "commands dev" || query == "cheatsheet" || query == "programming" || query == "developer";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim().ToLower();

            double similarity = 0;
            if (query == "dev" || query == "developer" || query == "programming") similarity = 3.0;
            else if (query == "cheatsheet" || query == "dev commands") similarity = 2.5;

            suggestions.Add(new CommandResult
            {
                TITLE = "🛠️ Open Developer Command Deck",
                DESCRIPTION = "Access, search, and run common programming commands (Git, Docker, Dotnet, Python, NPM)",
                SIMILARITY = similarity + 0.5,
                EXECUTE = () => DevCommandsOverlay.Open()
            });

            return suggestions;
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("dev", "Open Jarvis Developer Command Deck", "dev"),
                new CommandDesc("cheatsheet", "Open Jarvis Developer Command Deck", "cheatsheet")
            };
        }
    }
}
