// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles 'download <url>' commands by spawning the DownloadMedia TypeScript CLI and surfacing output.

using System;
using System.Collections.Generic;
using System.Windows;

namespace JarvisLauncher
{
    public class DownloadCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query.StartsWith("download") || query.StartsWith("dl ");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            var parts = query.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

            double similarity = SearchUtil.GetSimilarity(parts[0].ToLower(), "download");

            if (parts.Length > 1)
            {
                string url = parts[1].Trim();
                bool looksLikeUrl = url.StartsWith("http://") || url.StartsWith("https://");

                if (looksLikeUrl)
                {
                    suggestions.Add(new CommandResult
                    {
                        Title       = $"Download: {url}",
                        Description = "Download audio via Lucida or yt-dlp",
                        Similarity  = similarity,
                        Execute     = () => RunDownload(url)
                    });
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title       = "Download...",
                        Description = $"'{url}' doesn't look like a URL. Try: download https://...",
                        Similarity  = similarity,
                        Execute     = null
                    });
                }
            }
            else
            {
                suggestions.Add(new CommandResult
                {
                    Title       = "Download Media...",
                    Description = "Type a URL after 'download' (e.g. download https://deezer.com/track/123)",
                    Similarity  = similarity,
                    Execute     = null
                });
            }

            return suggestions;
        }

        private static void RunDownload(string url)
        {
            TextOverlay.Show("⬇️ Starting download...", 2500);

            // Run on a background thread so the UI never freezes
            System.Threading.Tasks.Task.Run(async () =>
            {
                string result;
                try
                {
                    result = await DownloadMediaRunner.DownloadAsync(url);
                }
                catch (Exception ex)
                {
                    result = $"Exception: {ex.Message}";
                }

                // Dispatch result back to the UI thread to show overlay
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CliOutputOverlay.Show("Download Result", result);
                });
            });
        }
    }
}
