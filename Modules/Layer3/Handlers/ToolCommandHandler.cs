// Developer: heaplyn
// Date: 2026-08-19
// Summary: Command handler for AI Tool Orchestrator and Registry.

using System;
using System.Collections.Generic;

namespace JarvisLauncher.Modules.Layer3.Handlers
{
    public class ToolCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            string q = query.ToLower();
            return q == "tools" || q == "orchestrator" || q == "registry" || q == "tool manager";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            return new List<CommandResult>
            {
                new CommandResult
                {
                    TITLE = "🛠️ Open AI Tool Orchestrator",
                    DESCRIPTION = "Manage and register autonomous AI tools and capabilities.",
                    SIMILARITY = 1.0,
                    EXECUTE = () => ToolManagerOverlay.ShowOverlay()
                }
            };
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("tools", "Open AI Tool Orchestrator", "tools")
            };
        }
    }
}
