// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles CLI commands to exit, quit, or close the Jarvis HUD launcher completely.

using System;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class ExitCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "exit" || query == "quit" || query == "close" || 
                   query == "exit jarvis" || query == "quit jarvis" || query == "close jarvis";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim().ToLower();

            double similarity = Math.Max(
                SearchUtil.GetSimilarity(query, "exit"), 
                Math.Max(SearchUtil.GetSimilarity(query, "close"), SearchUtil.GetSimilarity(query, "quit"))
            );

            suggestions.Add(new CommandResult
            {
                TITLE       = "Exit Jarvis Launcher",
                DESCRIPTION = "Close and terminate the Jarvis HUD application completely (Ctrl+Shift+C)",
                SIMILARITY  = similarity + 0.5, // High similarity boost for direct commands
                EXECUTE     = () => System.Environment.Exit(0)
            });

            return suggestions;
        }
    }
}
