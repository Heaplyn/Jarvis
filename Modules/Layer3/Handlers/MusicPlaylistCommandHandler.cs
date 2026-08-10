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
            return query == "music" || query == "playlist" || query == "player" || query == "songs" || query == "song" || query.StartsWith("download") || query.StartsWith("dl ");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            string raw = query.Trim();
            string lower = raw.ToLower();

            if (lower.StartsWith("download ") || lower.StartsWith("dl "))
            {
                string targetUrl = raw.Substring(raw.IndexOf(' ') + 1).Trim();
                suggestions.Add(new CommandResult
                {
                    Title = $"📥 Download Media to Playlist: {targetUrl}",
                    Description = "Downloads URL via DownloadMediaRunner TS engine directly to active playlist folder",
                    Similarity = 3.0,
                    Execute = () => MusicPlaylistOverlay.DownloadTrackFromUrl(targetUrl)
                });
                return suggestions;
            }
            else if (lower == "download" || lower == "dl")
            {
                suggestions.Add(new CommandResult
                {
                    Title = "📥 Download Media to Playlist (Prompt)...",
                    Description = "Prompt for music/video URL (Spotify, YouTube, Deezer, etc.)",
                    Similarity = 3.0,
                    Execute = () => MusicPlaylistOverlay.DownloadTrackFromUrl(string.Empty)
                });
                return suggestions;
            }

            suggestions.Add(new CommandResult
            {
                Title       = "🎵 Open Music Player & Playlist Manager",
                Description = "Manage song folders, add audio files/links, and play music",
                Similarity  = 2.0,
                Execute     = () => MusicPlaylistOverlay.OpenPlayer()
            });

            return suggestions;
        }
    }
}
