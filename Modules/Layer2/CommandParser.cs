// Developer: heaplyn
// Date: 2026-08-08
// Summary: Coordinates query dispatching, executes active handlers, and ranks the suggestion list by similarity index.

using System;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public static class CommandParser
    {
        private static readonly List<ICommandHandler> _handlers = new List<ICommandHandler>
        {
            new MathCommandHandler(),
            new VolumeCommandHandler(),
            new LockCommandHandler(),
            new RestartCommandHandler(),
            // AppLauncher is a catch-all, so it must be evaluated last
            new AppLauncherCommandHandler()
        };

        public static List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();

            if (string.IsNullOrWhiteSpace(query))
            {
                return suggestions;
            }

            query = query.Trim();

            foreach (var handler in _handlers)
            {
                try
                {
                    if (handler.CanHandle(query))
                    {
                        var results = handler.GetSuggestions(query);
                        if (results != null && results.Count > 0)
                        {
                            suggestions.AddRange(results);
                        }
                    }
                }
                catch
                {
                    // Fail-safe for individual handler errors
                }
            }

            // Sort suggestions in descending order of similarity
            suggestions.Sort((a, b) => b.Similarity.CompareTo(a.Similarity));

            return suggestions;
        }
    }
}
