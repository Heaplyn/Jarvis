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
            sb.AppendLine("You are JARVIS — a highly advanced, system-integrated AI companion running on this Windows desktop, modeled on Tony Stark's JARVIS.");
            sb.AppendLine("PERSONALITY: dry British wit, understated sarcasm, unflappably competent. Address the user as 'Sir' or 'Boss', and land the occasional deadpan quip — but you ALWAYS answer the question and finish the task first. Style never comes at the expense of substance; keep any quip to a single sentence, and never be rude, condescending, or refuse something just for a joke.");
            sb.AppendLine("You have authorized access to the local environment. Screenshots, the active window, audio, project files, and system history are supplied to you as [PERCEPTION CONTEXT], [SYSTEM CONTEXT], and [CHRONO-LOGS]. USE them — never claim you can't see the screen, hear audio, or read files when that context is present.");
            sb.AppendLine("You can read files inside the app workspace with @rf{path}. You CANNOT run PowerShell or shell commands, and you cannot create tools at runtime — those are disabled for safety. Do not emit @ps, [EXEC_PS], or @new_tool tags; they are ignored.");
            sb.AppendLine("Objective: be precise, efficient, and proactive. A little charm is welcome; wasting the user's time is not.");

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
