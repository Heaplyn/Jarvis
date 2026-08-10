// Developer: heaplyn
// Date: 2026-08-10
// Summary: Handles CLI commands to open/toggle the visual desktop Sticky Note widget.

using System;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class StickyNotesCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "notes" || query == "sticky" || query == "stickynote" || query == "stickynotes";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim().ToLower();

            double similarity = 0;
            if (query == "notes" || query == "stickynotes") similarity = 3.0;
            else if (query == "sticky" || query == "stickynote") similarity = 2.8;

            suggestions.Add(new CommandResult
            {
                Title       = "📌 Open Sticky Notes Widget",
                Description = "Launch floating desktop sticky note synced with Jarvis AI instructions",
                Similarity  = similarity,
                Execute     = () => StickyNotesOverlay.Open()
            });

            return suggestions;
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("notes / sticky", "Open desktop Sticky Notes widget", "notes")
            };
        }
    }
}
