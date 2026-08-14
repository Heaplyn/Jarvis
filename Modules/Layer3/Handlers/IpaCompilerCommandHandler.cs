// Developer: heaplyn
// Date: 2026-08-14
// Summary: Handles CLI/HUD command suggestions for starting the C# to iOS compilation GUI.

using System;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class IpaCompilerCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "compile ipa" || query == "compile ios" || query == "compile csharp to ios" ||
                   query == "ipa" || query == "ipa compiler" || query == "ipa studio";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();

            suggestions.Add(new CommandResult
            {
                Title = "🍎 Open C# to iOS IPA Compiler Studio",
                Description = "Select a C# project, build into an IPA, and download directly to your connected phone",
                Similarity = 7.0,
                Execute = () => IpaCompilerOverlay.ShowOverlay()
            });

            return suggestions;
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("compile ipa", "Open iOS compilation studio to build C# projects into IPA packages", "compile ipa")
            };
        }
    }
}
