// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles CLI commands to open interactive Music Player & Playlist Manager GUI (`music`, `playlist`, `player`, `songs`).

using System;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class MusicPlaylistCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "music" || query == "playlist" || query == "player" || query == "songs" || query == "song";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim().ToLower();

            double similarity = 2.0;

            suggestions.Add(new CommandResult
            {
                Title       = "🎵 Open Music Player & Playlist Manager",
                Description = "Manage song folders, add audio files/links, and play music",
                Similarity  = similarity,
                Execute     = () => MusicPlaylistOverlay.OpenPlayer()
            });

            return suggestions;
        }
    }
}
