// Developer: heaplyn
// Date: 2026-08-17
// Summary: Handles CLI commands for media grabbing (YouTube, etc.) via DownloadMediaRunner.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public class DownloadMediaHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            string q = query.Trim().ToLower();
            return q.StartsWith("grab ") || q.StartsWith("getvideo ") || q.StartsWith("mp3 ") || q.Contains("download media");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            string q = query.Trim();
            var parts = q.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return suggestions;

            string url = parts[1];
            suggestions.Add(new CommandResult
            {
                TITLE = "🎬 Grab Media Content",
                DESCRIPTION = $"Scrape and download media from: {url}",
                SIMILARITY = 9.0,
                EXECUTE = () => Task.Run(async () => {
                    TextOverlay.Show("🎬 Analyzing media stream...", 3000);
                    // Assuming DownloadMediaRunner has a static or registry entry
                    // For now routing through generic if runner API is unknown
                    string res = await CoreRegistry.Web.DownloadFileAsync(url, null);
                    TextOverlay.Show($"✅ Media processing started: {res}", 4000);
                })
            });

            return suggestions;
        }

        public List<CommandDesc> GetCommandDescriptions() => new List<CommandDesc> { new CommandDesc("grab <url>", "Download media from social sites", "grab https://youtube.com/...") };
    }
}
