// Developer: heaplyn
// Date: 2026-08-13
// Summary: Command handler for interactive Help Center, command guide, and keyboard shortcut reference.

using System;
using System.Collections.Generic;

namespace JarvisLauncher.Modules.Layer3.Handlers
{
    public class HelpCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.ToLower().Trim();
            return query == "help" || query == "guide" || query == "shortcuts" ||
                   query == "docs" || query == "commands" || query == "manual" ||
                   query.StartsWith("help ") || query.StartsWith("guide ");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var results = new List<CommandResult>();

            results.Add(new CommandResult
            {
                TITLE = "📖 Open Interactive Help & Documentation Center",
                DESCRIPTION = "Browse all commands, global hotkeys, voice shortcuts, and pipeline tips",
                SIMILARITY = 5.8,
                EXECUTE = () => HelpCenterOverlay.ShowOverlay()
            });

            results.Add(new CommandResult
            {
                TITLE = "🛠️ Repair Jarvis Documentation",
                DESCRIPTION = "Force restore and link guide files if they are missing",
                SIMILARITY = 5.0,
                EXECUTE = () => RepairDocumentation()
            });

            return results;
        }

        private void RepairDocumentation()
        {
            try
            {
                string root = PathHandler.GetProjectRoot();
                string source = System.IO.Path.Combine(root, "Data", "user_guide.md");
                string dest = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_guide.md");

                if (System.IO.File.Exists(source))
                {
                    System.IO.File.Copy(source, dest, true);
                    TextOverlay.Show("✅ Documentation repaired and restored.", 3000);
                }
                else
                {
                    TextOverlay.Show("❌ Source guide file missing. Please rebuild.", 3000);
                }
            }
            catch (System.Exception ex) { TextOverlay.Show($"⚠️ Repair failed: {ex.Message}", 3000); }
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("help", "Open Help & Documentation Center", "help"),
                new CommandDesc("shortcuts", "View Global Keyboard Hotkey Shortcuts", "shortcuts"),
                new CommandDesc("guide", "Read the Master User Manual", "guide"),
                new CommandDesc("manual", "Open Jarvis technical documentation", "manual"),
                new CommandDesc("docs", "Browse system architecture and guides", "docs")
            };
        }

        public void OnStart() { }
    }
}
