// Developer: heaplyn
// Date: 2026-08-13
// Summary: Command handler for interactive Help Center, command guide, and keyboard shortcut reference.

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
                Title = "📖 Open Interactive Help & Documentation Center",
                Description = "Browse all commands, global hotkeys, voice shortcuts, and pipeline tips",
                Similarity = 5.8,
                Execute = () => HelpCenterOverlay.ShowOverlay()
            });

            return results;
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("help", "Open Help & Documentation Center", "help"),
                new CommandDesc("shortcuts", "View Global Keyboard Hotkey Shortcuts", "shortcuts")
            };
        }

        public void OnStart() { }
    }
}
