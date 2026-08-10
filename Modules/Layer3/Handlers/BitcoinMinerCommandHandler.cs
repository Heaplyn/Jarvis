// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles CLI commands to toggle interactive Bitcoin mining simulator GUI (`btc`, `mine`, `bitcoin`).

using System;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class BitcoinMinerCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "btc" || query == "mine" || query == "bitcoin" || query == "miner";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim().ToLower();

            double similarity = 2.0;

            suggestions.Add(new CommandResult
            {
                Title       = "⛏️ Toggle Bitcoin Miner Simulator",
                Description = "Launch live matrix SHA-256 Bitcoin mining overlay GUI",
                Similarity  = similarity,
                Execute     = () => BitcoinMinerOverlay.ToggleMiner()
            });

            return suggestions;
        }
    }
}
