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
                new CommandDesc("help", "Open Interactive Help & Documentation Hub", "help"),
                new CommandDesc("guide", "Read the Master User Manual", "guide"),
                new CommandDesc("shortcuts", "View Global Keyboard Hotkey Cheat Sheet", "shortcuts"),
                new CommandDesc("docs", "Browse system architecture and dev guides", "docs"),
                new CommandDesc("commands", "Search the full system command directory", "commands"),
                new CommandDesc("reindex", "Force refresh the Windows app database", "reindex"),
                new CommandDesc("repair", "Auto-fix missing documentation files", "repair"),
                new CommandDesc("llm", "Open AI Engine Studio", "llm"),
                new CommandDesc("voice", "Configure acoustic training & TTS", "voice"),
                new CommandDesc("process", "Open Advanced Process Manager", "process"),
                new CommandDesc("network", "Run connectivity diagnostics", "network"),
                new CommandDesc("specs", "View detailed hardware specifications", "specs"),
                new CommandDesc("edit .", "Open current workspace in AI Code Studio", "edit ."),
                new CommandDesc("push ai", "Stage, Commit & Push with AI messages", "push ai"),
                new CommandDesc("ipa", "Compile project for iOS (IPA)", "ipa")
            };
        }

        public void OnStart() { }
    }
}
