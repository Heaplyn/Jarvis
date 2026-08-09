// Developer: heaplyn
// Date: 2026-08-08
// Summary: Handles application commands to restart the active Jarvis launcher thread or environment.

using System.Collections.Generic;

namespace JarvisLauncher
{
    public class RestartCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            return SearchUtil.IsClose(query.Trim(), "restart");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            double similarity = SearchUtil.GetSimilarity(query.Trim(), "restart");
            return new List<CommandResult>
            {
                new CommandResult
                {
                    Title = "Restart Jarvis",
                    Description = "Restarts the Jarvis application",
                    Execute = () => {
                        Console.WriteLine("Restarting Jarvis...");
                        NativeMethods.Restart();
                    },
                    Similarity = similarity
                }
            };
        }
    }
}