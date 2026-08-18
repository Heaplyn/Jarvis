// Developer: heaplyn
// Date: 2026-08-17
// Summary: Handles CLI commands for generic file downloads via WebOperationManager.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public class DownloadCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            string q = query.Trim().ToLower();
            return q.StartsWith("download ") || q.StartsWith("dl ");
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
                TITLE = $"📥 Download File: {System.IO.Path.GetFileName(url)}",
                DESCRIPTION = $"Source: {url}",
                SIMILARITY = 9.0,
                EXECUTE = () => Task.Run(async () => {
                    TextOverlay.Show("📥 Initiating download...", 3000);
                    string res = await CoreRegistry.Web.DownloadFileAsync(url, null);
                    TextOverlay.Show($"✅ {res}", 4000);
                })
            });

            return suggestions;
        }

        public List<CommandDesc> GetCommandDescriptions() => new List<CommandDesc> { new CommandDesc("download <url>", "Download file from web", "download https://...") };
    }
}
