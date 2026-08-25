// Developer: heaplyn
// Date: 2026-08-16
// Summary: Command handler for the Universal Build Studio.

using System;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class BuildCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "build" || query == "compile" || query == "make" || query == "build hub";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();

            suggestions.Add(new CommandResult
            {
                TITLE = "🛠️ Open Universal Build Studio",
                DESCRIPTION = "Compile multi-language projects (C#, Python, JS, C++) with AI analysis",
                SIMILARITY = 9.0,
                EXECUTE = () => BuildStudioOverlay.ShowOverlay()
            });

            return suggestions;
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("build", "Launch Universal Build & Compile Studio", "build")
            };
        }

        public void OnStart() { }
    }
}
