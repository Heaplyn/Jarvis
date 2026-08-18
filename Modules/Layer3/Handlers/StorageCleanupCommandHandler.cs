// Developer: heaplyn
// Date: 2026-08-17
// Summary: Handles CLI commands for storage analysis, cleaning temp files, and emptying the recycle bin.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public class StorageCleanupCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            string q = query.Trim().ToLower();
            return q == "cleanup" || q == "clean" || q == "storage" || q == "disk" || q == "purge" || q == "empty recycle bin" || q == "clear temp";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            string q = query.Trim().ToLower();

            suggestions.Add(new CommandResult
            {
                TITLE = "🧹 Run Full System Cleanup",
                DESCRIPTION = "Purge temp files, empty recycle bin, and rotate old logs",
                SIMILARITY = 9.0,
                EXECUTE = () => RunFullCleanup()
            });

            suggestions.Add(new CommandResult
            {
                TITLE = "🗑️ Empty Recycle Bin",
                DESCRIPTION = "Permanently delete all items in the Windows Recycle Bin",
                SIMILARITY = 8.5,
                EXECUTE = () => Task.Run(async () => {
                    bool ok = await CoreRegistry.StorageCleanup.EmptyRecycleBinAsync();
                    TextOverlay.Show(ok ? "🗑️ Recycle Bin Emptied!" : "⚠️ Bin is already empty.", 2500);
                })
            });

            suggestions.Add(new CommandResult
            {
                TITLE = "🧹 Clear Temporary Files",
                DESCRIPTION = "Purge the Windows %TEMP% directory to free up space",
                SIMILARITY = 8.0,
                EXECUTE = () => Task.Run(async () => {
                    int cleared = await CoreRegistry.StorageCleanup.ClearTempFilesAsync();
                    TextOverlay.Show($"🧹 Cleared {cleared} temp files/folders!", 3000);
                })
            });

            suggestions.Add(new CommandResult
            {
                TITLE = "📊 Analyze Disk Space",
                DESCRIPTION = "Show free space on all connected drives",
                SIMILARITY = 7.5,
                EXECUTE = () => {
                    var info = CoreRegistry.StorageCleanup.GetDiskSpaceInfo();
                    var sb = new StringBuilder("# Disk Space Report\n\n");
                    foreach (var kvp in info) sb.AppendLine($"- **{kvp.Key}**: {kvp.Value}");
                    ContentPreviewOverlay.Show("Storage Analysis", sb.ToString(), "markdown");
                }
            });

            suggestions.Add(new CommandResult
            {
                TITLE = "🔍 Find Large Files",
                DESCRIPTION = "Scan for files larger than 500MB in your user profile",
                SIMILARITY = 7.0,
                EXECUTE = () => RunLargeFileAnalysis()
            });

            return suggestions;
        }

        private void RunFullCleanup()
        {
            Task.Run(async () => {
                TextOverlay.Show("🧼 Jarvis is cleaning your system...", 4000);
                int temp = await CoreRegistry.StorageCleanup.ClearTempFilesAsync();
                await CoreRegistry.StorageCleanup.EmptyRecycleBinAsync();
                int logs = await CoreRegistry.StorageCleanup.CleanOldLogsAsync(7);
                TextOverlay.Show($"✅ Cleanup Complete! Purged {temp + logs} items.", 4000);
            });
        }

        private void RunLargeFileAnalysis()
        {
            Task.Run(async () => {
                TextOverlay.Show("🔍 Scanning for large files...", 3000);
                string userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var files = await CoreRegistry.StorageCleanup.FindLargeFilesAsync(userPath, 500 * 1024 * 1024, 15);

                var sb = new StringBuilder("# Large Files Discovery (>500MB)\n\n");
                if (files.Count == 0) sb.AppendLine("No files larger than 500MB found in user profile.");
                else {
                    foreach (var f in files) sb.AppendLine($"- **{f.Name}** ({f.ReadableSize})\n  `{f.Path}`\n");
                }
                ContentPreviewOverlay.Show("Storage Analysis", sb.ToString(), "markdown");
            });
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("cleanup", "Run full system storage cleanup", "cleanup"),
                new CommandDesc("disk", "Show disk space and large files", "disk"),
                new CommandDesc("empty recycle bin", "Purge the trash", "empty recycle bin")
            };
        }
    }
}
