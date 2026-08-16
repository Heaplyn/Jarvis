// Developer: heaplyn
// Date: 2026-08-16
// Summary: Long-Term Semantic & Contextual Memory System.
//          Stores user facts, codebase details, environmental audio history, and activity timelines.
//          Provides a unified mental model for the AI to understand the user's life and work.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public class MemoryNode
    {
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Category { get; set; } = "General"; // Personal, Project, Audio, Activity
        public string SubCategory { get; set; } = string.Empty; // e.g. Project Name, Sound Type
        public double Importance { get; set; } = 0.5;
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public static class SemanticMemoryManager
    {
        private static readonly string MemoryPath = Path.Combine(PathHandler.GetDataDirectory(), "SemanticMemory.json");
        private static List<MemoryNode> _nodes = new List<MemoryNode>();
        private static readonly object _lock = new object();

        static SemanticMemoryManager()
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
                    _nodes = JsonSerializer.Deserialize<List<MemoryNode>>(json) ?? new List<MemoryNode>();
                }
            }
            catch { }
        }

        public static void SaveMemories()
        {
            try
            {
                string json = JsonSerializer.Serialize(_nodes, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(MemoryPath, json);
            }
            catch { }
        }

        public static void AddMemory(string content, string category = "General", string subCategory = "", double importance = 0.5, Dictionary<string, string>? metadata = null)
        {
            lock (_lock)
            {
                // Prevent identical spam (especially for audio/activity)
                if (_nodes.Any(n => n.Content.Equals(content, StringComparison.OrdinalIgnoreCase) && (DateTime.Now - n.Timestamp).TotalMinutes < 5)) return;

                _nodes.Add(new MemoryNode
                {
                    Content = content,
                    Category = category,
                    SubCategory = subCategory,
                    Importance = importance,
                    Metadata = metadata ?? new Dictionary<string, string>()
                });

                // Keep memory size manageable (last 1000 nodes)
                if (_nodes.Count > 1000) _nodes.RemoveAt(0);
                SaveMemories();
            }
        }

        public static string GetMemoryContextForAi()
        {
            lock (_lock)
            {
                if (_nodes.Count == 0) return "I'm still learning about your environment and projects, Boss.";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("## SEMANTIC MEMORY & BACKGROUND CONTEXT");

                // 1. Personal Facts
                var personal = _nodes.Where(n => n.Category == "Personal").OrderByDescending(n => n.Importance).Take(10).ToList();
                if (personal.Any())
                {
                    sb.AppendLine("### Personal Identity");
                    foreach (var n in personal) sb.AppendLine($"- {n.Content}");
                }

                // 2. Project Knowledge
                var projects = _nodes.Where(n => n.Category == "Project").GroupBy(n => n.SubCategory);
                if (projects.Any())
                {
                    sb.AppendLine("### Known Codebases");
                    foreach (var g in projects)
                    {
                        var last = g.OrderByDescending(n => n.Timestamp).First();
                        sb.AppendLine($"- Project '{g.Key}': {last.Content}");
                    }
                }

                // 3. Environmental Timeline (Last 5 events)
                var timeline = _nodes.Where(n => n.Category == "Audio" || n.Category == "Activity").OrderByDescending(n => n.Timestamp).Take(5).Reverse();
                if (timeline.Any())
                {
                    sb.AppendLine("### Background Activity Timeline");
                    foreach (var n in timeline) sb.AppendLine($"- [{n.Timestamp:HH:mm:ss}] {n.Content}");
                }

                return sb.ToString();
            }
        }

        public static async Task ExtractFactsFromChatAsync(string userMessage, string aiResponse)
        {
            string prompt = "Task: Extract long-term user facts or project details from this chat.\n" +
                            "Output ONLY short bullet points of facts. If none, return 'NONE'.\n\n" +
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
                        string clean = line.TrimStart('-', '*', ' ').Trim();
                        if (!string.IsNullOrEmpty(clean)) AddMemory(clean, "Personal", importance: 0.7);
                    }
                }
            }
            catch { }
        }

        public static void LogAudioEvent(string soundType, double confidence)
        {
            if (confidence < 0.6) return;
            string content = $"Detected background sound: {soundType}";
            AddMemory(content, "Audio", soundType, 0.4);
        }

        public static void LogProjectActivity(string projectName, string action)
        {
            string content = $"Working on {projectName}: {action}";
            AddMemory(content, "Project", projectName, 0.6);
        }
    }
}
