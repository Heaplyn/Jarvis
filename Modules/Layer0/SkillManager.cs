// Developer: heaplyn
// Date: 2026-08-16
// Summary: Autonomous Skill Evolution System.
//          Allows Jarvis to define, chain, and persist complex multi-step "Skills".
//          Features a recursion-protected execution engine and automated scaffolding.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public class JarvisSkill
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Triggers { get; set; } = new List<string>();
        public string ActionChain { get; set; } = string.Empty; // Chained Jarvis commands or @ shorthand
        public string SystemInstruction { get; set; } = string.Empty; // Extra logic for the LLM
        public string Layer { get; set; } = "Dynamic"; // Placement context
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public static class SkillManager
    {
        private static readonly string SkillsPath = Path.Combine(PathHandler.GetDataDirectory(), "Skills.json");
        private static List<JarvisSkill> _skills = new List<JarvisSkill>();
        private static readonly object _lock = new object();

        // Recursion Protection
        private static readonly ThreadLocal<int> _callDepth = new ThreadLocal<int>(() => 0);
        private const int MaxCallDepth = 3;

        static SkillManager()
        {
            LoadSkills();
        }

        public static void LoadSkills()
        {
            try
            {
                if (File.Exists(SkillsPath))
                {
                    string json = File.ReadAllText(SkillsPath);
                    _skills = JsonSerializer.Deserialize<List<JarvisSkill>>(json) ?? new List<JarvisSkill>();
                }
            }
            catch { }
        }

        public static void SaveSkills()
        {
            try
            {
                string json = JsonSerializer.Serialize(_skills, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SkillsPath, json);
            }
            catch { }
        }

        public static bool CreateSkill(JarvisSkill skill)
        {
            lock (_lock)
            {
                var existing = _skills.FirstOrDefault(s => s.Name.Equals(skill.Name, StringComparison.OrdinalIgnoreCase));
                if (existing != null) _skills.Remove(existing);

                _skills.Add(skill);
                SaveSkills();

                DebugConsoleOverlay.Log("Skill-System", $"Created/Updated Skill: {skill.Name} ({skill.Triggers.Count} triggers)");
                return true;
            }
        }

        public static async Task<string> ExecuteSkillAsync(string skillName, string? input = null)
        {
            if (_callDepth.Value >= MaxCallDepth)
            {
                return "[RECURSION_PREVENTED]: Skill execution depth limit reached.";
            }

            _callDepth.Value++;
            try
            {
                var skill = _skills.FirstOrDefault(s => s.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));
                if (skill == null) return $"Error: Skill '{skillName}' not found.";

                DebugConsoleOverlay.Log("Skill-Exec", $"Executing Chain: {skill.Name}");

                // 1. Process Action Chain (Shorthand or Commands)
                string[] steps = skill.ActionChain.Split('|', StringSplitOptions.RemoveEmptyEntries);
                foreach (var step in steps)
                {
                    string cleanStep = step.Trim();
                    if (string.IsNullOrEmpty(cleanStep)) continue;

                    // If it's a shorthand @ tag, execute via AiAPI
                    if (cleanStep.StartsWith("@"))
                    {
                        await AiAPI.ExecuteAgentLoopAsync(cleanStep);
                    }
                    else
                    {
                        // Otherwise treat as a standard Jarvis command
                        System.Windows.Application.Current.Dispatcher.Invoke(() => CommandParser.ExecuteFirstSuggestion(cleanStep));
                    }

                    await Task.Delay(200); // Brief step delay
                }

                return $"SUCCESS: Skill '{skill.Name}' executed.";
            }
            finally
            {
                _callDepth.Value--;
            }
        }

        public static List<CommandResult> GetSkillSuggestions(string query)
        {
            var results = new List<CommandResult>();
            string q = query.ToLower().Trim();

            lock (_lock)
            {
                foreach (var skill in _skills)
                {
                    if (skill.Triggers.Any(t => t.ToLower().Contains(q) || q.Contains(t.ToLower())))
                    {
                        results.Add(new CommandResult
                        {
                            TITLE = $"✨ Skill: {skill.Name}",
                            DESCRIPTION = skill.Description,
                            SIMILARITY = 5.0,
                            EXECUTE = () => _ = ExecuteSkillAsync(skill.Name)
                        });
                    }
                }
            }

            return results;
        }

        public static string GetSkillContextForAi()
        {
            if (!_skills.Any()) return "";
            var sb = new StringBuilder();
            sb.AppendLine("## LEARNED SKILLS & AUTOMATIONS");
            foreach (var s in _skills)
            {
                sb.AppendLine($"- {s.Name}: {s.Description} (Triggers: {string.Join(", ", s.Triggers)})");
            }
            return sb.ToString();
        }
    }
}
