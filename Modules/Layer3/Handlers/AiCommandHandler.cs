// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles CLI commands to query the Gemini AI API, returning scrollable terminal outputs.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public class AiCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim();
            var parts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;

            string cmd = parts[0].ToLower();
            return SearchUtil.IsClose(cmd, "ai") || 
                   SearchUtil.IsClose(cmd, "ask") || 
                   SearchUtil.IsClose(cmd, "gemini") ||
                   SearchUtil.IsClose(cmd, "chat") ||
                   SearchUtil.IsClose(cmd, "companion");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim();

            var parts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return suggestions;

            string cmd = parts[0].ToLower();
            double similarity = SearchUtil.GetSimilarity(cmd, "ai");

            if (parts.Length > 1)
            {
                string prompt = query.Substring(cmd.Length).Trim();
                suggestions.Add(new CommandResult
                {
                    Title = $"Ask Jarvis AI: \"{prompt}\"",
                    Description = "Sends query to Gemini API and displays output in console panel",
                    Execute = () => RunAiQuery(prompt),
                    Similarity = similarity
                });
            }
            else
            {
                suggestions.Add(new CommandResult
                {
                    Title = "Open AI Chat Companion",
                    Description = "Open the floating interactive chat window",
                    Execute = () => ChatOverlay.ShowChat(),
                    Similarity = similarity
                });
            }

            return suggestions;
        }

        private static void RunAiQuery(string prompt)
        {
            // Instantly notify querying state
            TextOverlay.Show("🧠 Querying Jarvis AI, please wait...", 2000);

            Task.Run(async () =>
            {
                try
                {
                    string response = await AiAPI.AskGemini(prompt);
                    
                    // Parse response for files modifications and run actions!
                    string finalOutput = AgentExecutor.ProcessAIResponse(response);

                    // Display response in our scrollable retro terminal overlay!
                    CliOutputOverlay.Show($"AI RESPONSE: {prompt}", finalOutput);
                }
                catch (Exception ex)
                {
                    CliOutputOverlay.Show("AI Error", $"An error occurred during query: {ex.Message}");
                }
            });
        }
    }
}
