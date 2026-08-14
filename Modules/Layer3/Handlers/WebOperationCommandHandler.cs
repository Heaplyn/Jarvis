// Developer: heaplyn
// Date: 2026-08-14
// Summary: Handles web operation commands: download [url], scrape [url], search [query]

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public class WebOperationCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query.StartsWith("download ") || 
                   query.StartsWith("download-list ") || 
                   query.StartsWith("scrape ") || 
                   query.StartsWith("search ") || 
                   query.StartsWith("google ") || 
                   query.StartsWith("websearch ");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            string lower = query.Trim().ToLower();

            // 0. Download List
            if (lower.StartsWith("download-list "))
            {
                string url = query.Substring(14).Trim();
                suggestions.Add(new CommandResult
                {
                    Title = $"📥 Download/Clone Dataset List: {url}",
                    Description = "Parses page links/repos and downloads top voice datasets in background",
                    Similarity = 9.0,
                    Execute = () =>
                    {
                        Task.Run(async () =>
                        {
                            string result = await WebOperationManager.DownloadListAsync(url);
                            ChatOverlay.ShowChat();
                            await ChatOverlay.SubmitTextMessage($"web operation report:\n{result}");
                        });
                    }
                });
            }

            // 1. Download
            if (lower.StartsWith("download "))
            {
                string url = query.Substring(9).Trim();
                suggestions.Add(new CommandResult
                {
                    Title = $"📥 Download File: {url}",
                    Description = "Downloads this file directly to your User Downloads folder",
                    Similarity = 8.5,
                    Execute = () =>
                    {
                        Task.Run(async () =>
                        {
                            string result = await WebOperationManager.DownloadFileAsync(url);
                            ChatOverlay.ShowChat();
                            await ChatOverlay.SubmitTextMessage($"web operation report:\n{result}");
                        });
                    }
                });
            }

            // 2. Scrape
            else if (lower.StartsWith("scrape "))
            {
                string url = query.Substring(7).Trim();
                suggestions.Add(new CommandResult
                {
                    Title = $"🌐 Scrape Webpage: {url}",
                    Description = "Downloads and extracts plain readable text from this webpage",
                    Similarity = 8.5,
                    Execute = () =>
                    {
                        Task.Run(async () =>
                        {
                            string result = await WebOperationManager.ScrapeWebpageAsync(url);
                            ChatOverlay.ShowChat();
                            await ChatOverlay.SubmitTextMessage($"web operation report:\n{result}");
                        });
                    }
                });
            }

            // 3. Search
            else if (lower.StartsWith("search ") || lower.StartsWith("google ") || lower.StartsWith("websearch "))
            {
                int prefixLen = lower.StartsWith("websearch ") ? 10 : (lower.StartsWith("google ") ? 7 : 7);
                string term = query.Substring(prefixLen).Trim();
                suggestions.Add(new CommandResult
                {
                    Title = $"🔍 Search Web for: '{term}'",
                    Description = "Executes DuckDuckGo search and summarizes top pages",
                    Similarity = 8.5,
                    Execute = () =>
                    {
                        Task.Run(async () =>
                        {
                            string result = await WebOperationManager.SearchWebAsync(term);
                            ChatOverlay.ShowChat();
                            await ChatOverlay.SubmitTextMessage($"web operation report:\n{result}");
                        });
                    }
                });
            }

            return suggestions;
        }
    }
}
