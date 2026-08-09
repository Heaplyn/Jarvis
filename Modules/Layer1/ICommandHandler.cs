// Developer: heaplyn
// Date: 2026-08-08
// Summary: Interface standardizing the methods concrete query matching handlers must implement.

using System.Collections.Generic;

namespace JarvisLauncher
{
    public interface ICommandHandler
    {
        bool CanHandle(string query);
        List<CommandResult> GetSuggestions(string query);
        void OnStart() { }
    }
}
