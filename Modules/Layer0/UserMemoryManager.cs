// Developer: heaplyn
// Date: 2026-08-15
// Summary: Long-Term Semantic User Memory.
//          Extracts and stores facts about the user (preferences, nicknames, projects) from conversations.
//          Unifies disparate data points into a cohesive "User Mental Model" for the AI.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public class UserFact
    {
        public string Fact { get; set; } = string.Empty;
        public DateTime DiscoveredAt { get; set; } = DateTime.Now;
        public string Category { get; set; } = "General"; // Preference, Project, Personal, etc.
        public double Importance { get; set; } = 0.5;
    }

    public static class UserMemoryManager
    {
        private static readonly string MemoryPath = Path.Combine(PathHandler.GetDataDirectory(), "UserMemory.json");
        private static List<UserFact> _memories = new List<UserFact>();
        private static readonly object _lock = new object();

        static UserMemoryManager()
        {
            LoadMemories();
        }

        public static void LoadMemories()
        {
            try
            {
                if (File.Exists(MemoryPath))
                {
                    string json = File.ReadAllText(MemoryPath);
                    _memories = JsonSerializer.Deserialize<List<UserFact>>(json) ?? new List<UserFact>();
                }
            }
            catch { }
        }

        public static void SaveMemories()
        {
            try
            {
                string json = JsonSerializer.Serialize(_memories, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(MemoryPath, json);
            }
            catch { }
        }

        public static void AddFact(string fact, string category = "General", double importance = 0.5)
        {
            lock (_lock)
            {
                // Simple duplicate prevention
                if (_memories.Any(m => m.Fact.Equals(fact, StringComparison.OrdinalIgnoreCase))) return;

                _memories.Add(new UserFact { Fact = fact, Category = category, Importance = importance });
                if (_memories.Count > 100) _memories.RemoveAt(0); // Keep top 100 most recent facts
                SaveMemories();
            }
        }

        public static string GetMemoryContextForAi()
        {
            lock (_lock)
            {
                if (_memories.Count == 0) return "I don't know much about you yet, Boss. Let's get to work.";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("## USER LONG-TERM MEMORY");
                foreach (var group in _memories.GroupBy(m => m.Category))
                {
                    sb.AppendLine($"### {group.Key}");
                    foreach (var fact in group.TakeLast(5))
                    {
                        sb.AppendLine($"- {fact.Fact}");
                    }
                }
                return sb.ToString();
            }
        }

        public static async Task ExtractFactsFromChatAsync(string userMessage, string aiResponse)
        {
            string prompt = "You are the Jarvis Memory Extractor. Analyze the exchange below.\n" +
                            "If the user revealed any long-term facts about themselves (preferences, names, current projects, etc.), extract them as short bullet points.\n" +
                            "If no new facts were revealed, return 'NONE'.\n\n" +
                            $"USER: {userMessage}\n" +
                            $"JARVIS: {aiResponse}";

            try
            {
                string result = await LlmRouter.AskAsync(prompt, null);
                if (!string.IsNullOrWhiteSpace(result) && result != "NONE")
                {
                    var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        string cleanFact = line.TrimStart('-', '*', ' ').Trim();
                        if (!string.IsNullOrEmpty(cleanFact)) AddFact(cleanFact);
                    }
                }
            }
            catch { }
        }
    }
}
