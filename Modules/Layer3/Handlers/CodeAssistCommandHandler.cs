// Developer: heaplyn
// Date: 2026-08-13
// Summary: Handles CLI/HUD command suggestions for turning on/off or displaying the Code Assist mode.

using System;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class CodeAssistCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "code assist" || query == "codeassist" || query == "code pilot" ||
                   query == "turn on code assist" || query == "enable code assist" ||
                   query == "turn off code assist" || query == "disable code assist";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            string lower = query.Trim().ToLower();

            // Option 1: Turn On Code Assist
            if (!CodeAssistManager.IsRunning && (lower.Contains("on") || lower.Contains("enable") || lower == "code assist" || lower == "codeassist"))
            {
                suggestions.Add(new CommandResult
                {
                    Title = "🚀 Turn On Real-Time Code Assist",
                    Description = "Launches 8s Vision + Workspace File scanning loop with sidebar advisor panel",
                    Similarity = 6.8,
                    Execute = () =>
                    {
                        CodeAssistManager.Start();
                        CodeAssistOverlay.ShowOverlay();
                        TextOverlay.Show("🟢 Real-Time Code Assist Enabled", 2500);
                    }
                });
            }

            // Option 2: Turn Off Code Assist
            if (CodeAssistManager.IsRunning && (lower.Contains("off") || lower.Contains("disable") || lower == "code assist" || lower == "codeassist"))
            {
                suggestions.Add(new CommandResult
                {
                    Title = "🛑 Turn Off Real-Time Code Assist",
                    Description = "Stops background project scanning and queries",
                    Similarity = 6.8,
                    Execute = () =>
                    {
                        CodeAssistManager.Stop();
                        CodeAssistOverlay.HideOverlay();
                        TextOverlay.Show("🛑 Code Assist Disabled", 2500);
                    }
                });
            }

            // Option 3: Show Sidebar
            suggestions.Add(new CommandResult
            {
                Title = "🤖 Show AI Code Assist Sidebar",
                Description = "Dock Code Assist sidebar layout on your desktop",
                Similarity = 6.0,
                Execute = () => CodeAssistOverlay.ShowOverlay()
            });

            return suggestions;
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("turn on code assist", "Enable real-time screen & project files visual assistant", "turn on code assist"),
                new CommandDesc("turn off code assist", "Disable background screen & file assistance", "turn off code assist")
            };
        }
    }
}
