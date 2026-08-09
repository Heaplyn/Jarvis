// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles application commands to restart the active Jarvis launcher thread or environment.

using System;
using System.Collections.Generic;
using System.Windows.Threading;

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
                        // Display visual overlay directly
                        TextOverlay.Show("Restarting Jarvis...", 1000);

                        // Wait 1 second before performing the actual process restart
                        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                        timer.Tick += (s, ev) =>
                        {
                            timer.Stop();
                            NativeMethods.Restart();
                        };
                        timer.Start();
                    },
                    Similarity = similarity
                }
            };
        }
    }
}
