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
            return query == "notes" || query == "sticky" || query == "stickynote" || query == "stickynotes" || query == "curate notes";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim().ToLower();

            if (query == "curate notes")
            {
                suggestions.Add(new CommandResult
                {
                    TITLE = "🤖 Trigger AI Notes Curation",
                    DESCRIPTION = "Have Jarvis review and organize your hierarchical notes and categories now",
                    SIMILARITY = 5.0,
                    EXECUTE = () => _ = NotesCuratorManager.PerformAutonomousCurationAsync()
                });
                return suggestions;
            }

            double similarity = 0;
            if (query == "notes" || query == "stickynotes") similarity = 3.0;
            else if (query == "sticky" || query == "stickynote") similarity = 2.8;

            suggestions.Add(new CommandResult
            {
                TITLE       = "📓 Open Notes Studio",
                DESCRIPTION = "Launch advanced hierarchical note manager with categories and subcategories",
                SIMILARITY  = similarity,
                EXECUTE     = () => NoteManagerOverlay.ShowOverlay()
            });

            return suggestions;
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("notes", "Open hierarchical Notes Studio manager", "notes")
            };
        }
    }
}
