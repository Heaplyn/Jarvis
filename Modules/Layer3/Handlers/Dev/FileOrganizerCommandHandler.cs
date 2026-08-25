// Developer: copilot
// Date: 2026-08-13
// Summary: Handles CLI commands to open/toggle the visual file organizer dashboard.

using System;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class FileOrganizerCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "organize" || query == "organizer" || query == "duplicates" || query == "cleanup" || query == "clean";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim().ToLower();

            double similarity = 0;
            if (query == "organize" || query == "organizer") similarity = 3.0;
            else if (query == "duplicates" || query == "cleanup" || query == "clean") similarity = 2.5;

            suggestions.Add(new CommandResult
            {
                TITLE = "📂 Open File Organizer",
                DESCRIPTION = "Run file categorization, duplicate audits, and empty directory purges",
                SIMILARITY = similarity + 0.5,
                EXECUTE = () => FileOrganizerOverlay.Open()
            });

            return suggestions;
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("organize / cleanup", "Launch visual File Organizer utility", "organize")
            };
        }
    }
}
