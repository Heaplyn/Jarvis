// Developer: heaplyn
// Date: 2026-08-08
// Summary: Handles security commands to lock the Windows session workstation using User32 DLL calls.

using System;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class LockCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            return SearchUtil.IsClose(query.Trim(), "lock");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            double similarity = SearchUtil.GetSimilarity(query.Trim(), "lock");
            suggestions.Add(new CommandResult
            {
                Title = "Lock Workstation",
                Description = "Secure the Windows session immediately",
                Execute = () => NativeMethods.LockWorkStation(),
                Similarity = similarity
            });
            return suggestions;
        }
    }
}
