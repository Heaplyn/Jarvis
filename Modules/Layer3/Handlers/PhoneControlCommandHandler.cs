// Developer: heaplyn
// Date: 2026-08-09
// Summary: Command handler to open the Mobile Companion Hub overlay.

using System;
using System.Collections.Generic;
using System.Linq;

namespace JarvisLauncher
{   
    public class PhoneControlCommandHandler : ICommandHandler
    {
        private static List<string> Aliases = new List<string>
        {
            "phone",
            "mobile",
            "remote",
            "bridge",
            "sync",
            "control"
        };

        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return Aliases.Any(a => SearchUtil.IsClose(query, a));
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim().ToLower();

            double similarity = 0;
            foreach (var alias in Aliases)
            {
                similarity = Math.Max(similarity, SearchUtil.GetSimilarity(query, alias));
            }
            
            suggestions.Add(new CommandResult
            {
                TITLE = "📱 Mobile Companion Hub",
                DESCRIPTION = "Open connection links and remote control settings",
                EXECUTE = () =>
                {
                    MobileOverlay.ShowOverlay();
                },
                SIMILARITY = similarity + 0.5 // Boost it slightly
            });

            return suggestions;
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("phone", "Open Mobile Companion Hub", "phone"),
                new CommandDesc("remote", "Manage phone connectivity", "remote")
            };
        }
    }
}
