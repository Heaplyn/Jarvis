// Developer: copilot
// Date: 2026-08-13
// Summary: Handles CLI commands to open/view the Calendar GUI or quickly add events directly via the input bar.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace JarvisLauncher
{
    public class CalendarCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "cal" || query.StartsWith("cal ") || query == "calendar" || query.StartsWith("calendar ");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim();

            var parts = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return suggestions;

            string cmd = parts[0].ToLower();
            double similarity = 2.0;

            // Check if user is typing a direct event logging statement: calendar add yyyy-MM-dd hh:mm event name
            var addMatch = Regex.Match(query, @"^(?:calendar|cal)\s+add\s+(\d{4}-\d{2}-\d{2})\s+(\S+)\s+(.+)$", RegexOptions.IgnoreCase);
            if (addMatch.Success)
            {
                string dateStr = addMatch.Groups[1].Value;
                string timeStr = addMatch.Groups[2].Value;
                string title = addMatch.Groups[3].Value.Trim();

                suggestions.Add(new CommandResult
                {
                    Title = $"📅 Create Event: \"{title}\" on {dateStr} at {timeStr}",
                    Description = "Add a calendar event directly into the Jarvis Planner database",
                    Similarity = similarity + 1.0,
                    Execute = () => CalendarOverlay.LogEvent(title, dateStr, timeStr)
                });
                return suggestions;
            }

            // Suggest opening the overlay
            suggestions.Add(new CommandResult
            {
                Title = "📅 Open Calendar Overlay",
                Description = "Launch Jarvis visual Month Calendar and daily planners",
                Similarity = similarity,
                Execute = () => CalendarOverlay.Open()
            });

            // Help info hint
            suggestions.Add(new CommandResult
            {
                Title = "calendar add [yyyy-mm-dd] [time] [event details]...",
                Description = "Quickly log a calendar event (e.g. cal add 2026-08-15 14:00 Standup meeting)",
                Similarity = similarity - 0.5,
                Execute = null
            });

            return suggestions;
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("calendar / cal", "Open calendar overlay or log events directly", "cal add 2026-08-15 14:00 Meetup")
            };
        }
    }
}
