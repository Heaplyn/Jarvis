// Developer: heaplyn
// Date: 2026-08-19
// Summary: Command handler for Backup & Synchronization between PCs.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JarvisLauncher.Modules.Layer3.Handlers
{
    public class BackupCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            string q = query.ToLower();
            return q.Contains("backup") || q.Contains("sync pc") || q.Contains("pull training");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var results = new List<CommandResult>();

            results.Add(new CommandResult
            {
                TITLE = "🔄 Synchronize Training Data",
                DESCRIPTION = "Connect to Backup PC and pull latest models/training files.",
                SIMILARITY = 1.0,
                EXECUTE = () => Task.Run(async () => {
                    TextOverlay.Show("🔄 Initiating PC-to-PC Sync...", 3000);
                    string res = await BackupSyncManager.RunSyncCycleAsync();
                    TextOverlay.Show(res, 4000);
                })
            });

            results.Add(new CommandResult
            {
                TITLE = "⚙️ Configure Backup PC",
                DESCRIPTION = "Set URL and credentials for the remote Backup PC.",
                SIMILARITY = 0.8,
                EXECUTE = () => SettingsOverlay.ShowOverlay()
            });

            return results;
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("sync pc", "Pull latest training data from designated Backup PC", "sync pc"),
                new CommandDesc("backup settings", "Configure PC-to-PC synchronization", "backup settings")
            };
        }
    }
}
