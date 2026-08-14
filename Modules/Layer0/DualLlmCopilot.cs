// Developer: heaplyn
// Date: 2026-08-13
// Summary: Dual LLM Co-Pilot Query Processor.
// Runs an optional secondary LLM in parallel to analyze queries, generate intent enrichment, and suggest recommended follow-up actions. Default disabled.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace JarvisLauncher
{
    public static class DualLlmCopilot
    {
        public static readonly List<string> RecommendedModels = new()
        {
            "deepseek-r1:7b (Recommended - Deep Reasoning & Code Intent)",
            "llama3.2:3b (Recommended - Ultra-Fast Local Response)",
            "gemini-1.5-flash (Recommended - Fast Cloud Intelligence)",
            "qwen2.5-coder:7b (Recommended - Code & System Scripts)",
            "gemma2:9b (Recommended - High Accuracy Assistant)"
        };

        /// <summary>
        /// Executes secondary Co-Pilot LLM in parallel with the primary query pipeline.
        /// </summary>
        public static void ProcessQueryParallel(string query)
        {
            var settings = SettingsManager.Current;
            if (!settings.EnableDualLlmCopilot || string.IsNullOrWhiteSpace(query)) return;

            Task.Run(async () =>
            {
                try
                {
                    DebugConsoleOverlay.Log("Dual-LLM Co-Pilot", $"Processing parallel query with {settings.DualLlmBackend} [{settings.DualLlmModel}]: \"{query}\"");

                    string prompt = $"You are Jarvis Dual-LLM Co-Pilot. Analyze this user query: \"{query}\". Provide a 1-sentence smart recommendation or follow-up suggestion.";

                    string rawModel = ExtractModelName(settings.DualLlmModel);
                    string copilotInsight = "";

                    if (settings.DualLlmBackend.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
                    {
                        copilotInsight = await LlmRouter.AskOllamaAsync(prompt);
                    }
                    else if (settings.DualLlmBackend.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
                    {
                        copilotInsight = await AiAPI.AskGemini(prompt);
                    }
                    else if (settings.DualLlmBackend.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
                    {
                        copilotInsight = await LlmRouter.AskOpenAIAsync(prompt);
                    }
                    else
                    {
                        copilotInsight = await LlmRouter.AskAsync(prompt);
                    }

                    if (!string.IsNullOrWhiteSpace(copilotInsight))
                    {
                        string cleanInsight = copilotInsight.Trim();
                        DebugConsoleOverlay.Log("Dual-LLM Insight", cleanInsight);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            TextOverlay.Show($"💡 Co-Pilot [{rawModel}]: {cleanInsight}", 4500);
                        });
                    }
                }
                catch (Exception ex)
                {
                    DebugConsoleOverlay.Log("Dual-LLM Error", ex.Message);
                }
            });
        }

        public static string ExtractModelName(string fullModelString)
        {
            if (string.IsNullOrWhiteSpace(fullModelString)) return "deepseek-r1:7b";
            int spaceIdx = fullModelString.IndexOf(' ');
            return spaceIdx > 0 ? fullModelString.Substring(0, spaceIdx) : fullModelString;
        }
    }
}
