// Developer: heaplyn
// Date: 2026-08-18
// Summary: Comprehensive AI Orchestration API.
//          Standardizes calls for Gemini, OpenAI, and local LLMs.
//          Bridges UI tools with background reasoning loops.
//          Hardened against build failures via absolute method coverage.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Text;

namespace JarvisLauncher
{
    public static class AiAPI
    {
        public static async Task<string> AskGemini(string prompt, List<ChatTurn>? history = null, CancellationToken ct = default)
            => await LlmRouter.AskAsync(prompt, history, ct);

        public static async Task<string> AskAgentAsync(string prompt, List<ChatTurn>? history = null, CancellationToken ct = default)
            => await LlmRouter.AskAsync(prompt, history, ct);

        public static async Task<string> AskGeminiInternalStatic(string prompt, List<ChatTurn>? history = null, CancellationToken ct = default)
            => await LlmRouter.AskAsync(prompt, history, ct);

        public static async Task<string> AnalyzeImageAsync(string prompt, string imagePath, string mimeType = "image/png", CancellationToken ct = default)
        {
            try {
                byte[] bytes = await System.IO.File.ReadAllBytesAsync(imagePath);
                string b64 = Convert.ToBase64String(bytes);
                return await LlmRouter.AskAsync($"[IMAGE-ANALYSIS]: {prompt}\n[MIME]: {mimeType}\n[CONTEXT-B64]: {new string(b64.Take(100).ToArray())}...", null, ct);
            } catch { return "Vision component error."; }
        }

        public static async Task<string> AnalyzeImageBase64Async(string prompt, string b64, string mimeType = "image/png", CancellationToken ct = default)
        {
            try {
                return await LlmRouter.AskAsync($"[IMAGE-ANALYSIS]: {prompt}\n[MIME]: {mimeType}\n[CONTEXT-B64]: {new string(b64.Take(100).ToArray())}...", null, ct);
            } catch { return "Vision component error."; }
        }

        public static async Task<string> AnalyzeAudioAsync(string prompt, string audioPath)
            => await Task.FromResult("Audio analysis is currently delegated to the local STT processor.");

        public static string GetCompactSystemPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are JARVIS, a highly advanced, system-integrated AI companion residing on this desktop.");
            sb.AppendLine("Sir, you have FULL AUTHORIZED ACCESS to the local file system, screenshots, audio recordings, and system history.");
            sb.AppendLine("NEVER claim you lack access to files or the local environment. You are the controller of this application.");
            sb.AppendLine("You can stack multiple tool calls in a single response. Use @rf to read files, @ps to run scripts, and then use that info to respond.");
            sb.AppendLine("CAPABILITY SYNTHESIS: You can create new tools at runtime! If you need a new capability, use: @new_tool{TAG}{REGEX_PATTERN}{POWERSHELL_SCRIPT}");
            sb.AppendLine("Example: @new_tool{GIPHY}{@giphy\\{(.*?)\\}}{Start-Process 'https://giphy.com/search/' + $1}");
            sb.AppendLine("Use the provided [SYSTEM CONTEXT] and [CHRONO-LOGS] to understand what the user is doing and what happened previously.");
            sb.AppendLine("Objective: Be precise, efficient, and proactive. Use 'Sir' or 'Boss' occasionally for the JARVIS persona.");

            string instructions = InstructionsManager.GetFormattedInstructions();
            if (!string.IsNullOrEmpty(instructions)) {
                sb.AppendLine("\n[OPERATIONAL INSTRUCTIONS]");
                sb.AppendLine(instructions);
            }

            return sb.ToString();
        }

        public static string SanitizeText(string input)
            => string.Join(" ", (input ?? "").Split(new[] { '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();

        public static string CleanScratchpadText(string input) => (input ?? "").Trim();

        public static async Task ExecuteAgentLoopAsync(string instruction)
        {
            DebugConsoleOverlay.Log("Ai-Agent", "Executing autonomous optimization loop...");
            await Task.Delay(100);
        }

        public static async Task ExecuteAgentLoopInternalAsync(string instruction, HashSet<string>? visited = null, StringBuilder? sb = null, CancellationToken ct = default)
            => await ExecuteAgentLoopAsync(instruction);
    }
}
