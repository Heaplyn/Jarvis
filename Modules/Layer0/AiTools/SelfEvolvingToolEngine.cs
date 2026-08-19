// Developer: heaplyn
// Date: 2026-08-19
// Summary: Autonomous Tool Synthesis Engine.
//          Allows the AI to design, verify, and register its own tools at runtime.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JarvisLauncher.AiTools
{
    public class DynamicScriptTool : IAiTool
    {
        public string Tag { get; }
        public string RegexPattern { get; }
        public string PowerShellScriptTemplate { get; }
        public bool IsVerified { get; set; } = false;

        public DynamicScriptTool(string tag, string pattern, string script)
        {
            Tag = tag;
            RegexPattern = pattern;
            PowerShellScriptTemplate = script;
        }

        public async Task<string> ExecuteAsync(Match match, HashSet<string> executedTags)
        {
            if (!executedTags.Add(Tag + ":" + match.Value.GetHashCode())) return "";

            string script = PowerShellScriptTemplate;
            // Basic parameter injection from named groups
            foreach (string groupName in match.Groups.Keys)
            {
                if (int.TryParse(groupName, out _)) continue;
                script = script.Replace($"${{{groupName}}}", match.Groups[groupName].Value);
            }

            string result = AgentExecutor.ExecutePowerShellDirect(script);
            return $"[DYNAMIC TOOL {Tag} EXECUTED]:\n{result}\n";
        }
    }

    public static class SelfEvolvingToolEngine
    {
        public static async Task<string> ProcessToolSynthesisAsync(string aiResponse)
        {
            // Tag format: @new_tool{TAG}{REGEX}{PS_SCRIPT}
            var synthesisRegex = new Regex(@"@new_tool\{(?<tag>.*?)\}\{(?<regex>.*?)\}\{(?<script>.*?)\}", RegexOptions.Singleline);
            var matches = synthesisRegex.Matches(aiResponse);

            int created = 0;
            foreach (Match match in matches)
            {
                string tag = match.Groups["tag"].Value.Trim();
                string pattern = match.Groups["regex"].Value.Trim();
                string script = match.Groups["script"].Value.Trim();

                if (string.IsNullOrEmpty(tag) || string.IsNullOrEmpty(pattern)) continue;

                // Verification Phase
                bool safe = await VerifyToolSafetyAsync(tag, script);
                if (safe)
                {
                    var tool = new DynamicScriptTool(tag, pattern, script) { IsVerified = true };
                    AiToolRegistry.Register(tool);
                    created++;
                    DebugConsoleOverlay.Log("Tool-Evolution", $"Synthesized and Registered New Tool: {tag}");
                }
            }

            return created > 0 ? $"[SYSTEM]: {created} new tools were synthesized and integrated." : "";
        }

        private static async Task<bool> VerifyToolSafetyAsync(string tag, string script)
        {
            // In a real scenario, this would use a separate LLM pass or sandbox execution
            // For now, we perform basic static analysis for malicious commands
            var malicious = new[] { "rm -rf", "format", "del /s", "drop table", "mkfs" };
            foreach (var m in malicious)
            {
                if (script.Contains(m, StringComparison.OrdinalIgnoreCase)) return false;
            }

            // Artificial verification delay
            await Task.Delay(500);
            return true;
        }
    }
}
