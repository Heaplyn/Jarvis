// Developer: heaplyn
// Date: 2026-08-18
// Summary: Central AI Orchestrator for J.A.R.V.I.S.
//          Recursive Tool Chain with Absolute PC Authority.
//          Personality: Cool, direct, efficient (JARVIS movie accurate).
//          Enhanced parsing: Automatically detects markdown code blocks for execution.
//          Self-Repair Engine: Fixes internal code faults autonomously.

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using JarvisLauncher.AiTools;

namespace JarvisLauncher
{
    public static class AiAPI
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        private static readonly List<IAiTool> Tools = new List<IAiTool>();

        static AiAPI()
        {
            // 1. Filesystem & Git
            Tools.Add(new ReadFileTool()); Tools.Add(new WriteFileTool()); Tools.Add(new ListFilesTool());
            Tools.Add(new GitTool()); Tools.Add(new ArchiveTool());
            Tools.Add(new ReadBinaryTool()); Tools.Add(new WriteBinaryTool());

            // 2. System & Automation
            Tools.Add(new PowerShellTool()); Tools.Add(new ProcessListTool()); Tools.Add(new ProcessKillTool());
            Tools.Add(new MouseControlTool()); Tools.Add(new KeyboardTool()); Tools.Add(new AppFocusTool());
            Tools.Add(new ClipboardTool()); Tools.Add(new RegistryReadTool());

            // 3. Hardware & Network
            Tools.Add(new HardwareMetricsTool()); Tools.Add(new VolumeTool()); Tools.Add(new NetDiagTool());
            Tools.Add(new ScreenInfoTool());

            // 4. Web & Cloud
            Tools.Add(new WebSearchTool()); Tools.Add(new WebFetchTool()); Tools.Add(new DownloadTool());
            Tools.Add(new GcsUploadTool()); Tools.Add(new CloudAssistTool());

            // 5. Memory & Evolution
            Tools.Add(new NoteTool()); Tools.Add(new AddTrackedFileTool());
            Tools.Add(new CodeModificationTool()); Tools.Add(new SystemBackupTool());
            Tools.Add(new NukeMemoryTool());
        }

        public static async Task<string> AskGemini(string Prompt, List<ChatTurn>? History = null, CancellationToken ct = default)
            => await LlmRouter.AskAsync(Prompt, History, ct);

        public static async Task<string> AskAgentAsync(string prompt, List<ChatTurn>? history = null, CancellationToken ct = default)
        {
            string response = await LlmRouter.AskAsync(prompt, history, ct);
            var executedTags = new HashSet<string>();
            var feed = new StringBuilder();

            try {
                await ExecuteAgentLoopInternalAsync(response, executedTags, feed, ct);
            } catch (Exception ex) {
                feed.AppendLine($"[REPAIR ENGINE]: Technical fault detected: {ex.Message}");
                DebugConsoleOverlay.Log("Self-Repair", "Analyzing internal error for autonomous fix...");
                string fix = await LlmRouter.AskAsync($"### SYSTEM ERROR\n{ex}\n\nFix with tags.", null, ct);
                await ExecuteAgentLoopInternalAsync(fix, executedTags, feed, ct);
            }

            if (executedTags.Count > 0) {
                return await LlmRouter.AskAsync($"### TASK COMPLETE\nOperation results:\n{feed}\n\nTechnical report.", history, ct);
            }
            return response;
        }

        internal static async Task<string> AskGeminiInternalStatic(string Prompt, List<ChatTurn>? History = null, CancellationToken ct = default)
        {
            var s = CoreRegistry.Data.Settings.Current;
            string key = s.GOOGLE_AI_KEY;
            string oauthToken = s.GOOGLE_OAUTH_ACCESS_TOKEN;
            if (string.IsNullOrEmpty(key) && string.IsNullOrEmpty(oauthToken)) throw new Exception("Credentials missing.");

            var contents = new List<object>();
            if (History != null) foreach (var turn in History.TakeLast(8)) contents.Add(new { role = turn.Role == "model" ? "model" : "user", parts = new[] { new { text = turn.Text } } });
            contents.Add(new { role = "user", parts = new[] { new { text = Prompt } } });

            var payload = new { systemInstruction = new { parts = new[] { new { text = GetSystemPrompt() } } }, contents = contents.ToArray(), generationConfig = new { temperature = 0.5, maxOutputTokens = 4096 } };
            var models = new[] { s.GEMINI_MODEL, "gemini-2.0-flash-exp", "gemini-1.5-flash", "gemini-1.5-pro" };

            var errors = new List<string>();
            foreach (var m in models.Where(x => !string.IsNullOrEmpty(x)).Distinct()) {
                foreach (var v in new[] { "v1beta", "v1" }) {
                    try {
                        string url = $"https://generativelanguage.googleapis.com/{v}/models/{m}:generateContent";
                        bool useOAuth = !string.IsNullOrEmpty(oauthToken);
                        var req = new HttpRequestMessage(HttpMethod.Post, useOAuth ? url : url + $"?key={key}") { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") };
                        if (useOAuth) req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", oauthToken);
                        var resp = await Client.SendAsync(req, ct);
                        string body = await resp.Content.ReadAsStringAsync();
                        if (resp.IsSuccessStatusCode) {
                            using var doc = JsonDocument.Parse(body);
                            return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
                        } else { errors.Add($"[{m}] {resp.StatusCode}"); }
                    } catch (Exception ex) { errors.Add($"[{m}] {ex.Message}"); }
                }
            }
            throw new Exception($"Gemini fail. Errors: {string.Join(", ", errors.Take(2))}");
        }

        public static string GetSystemPrompt() {
            var s = new StringBuilder("## IDENTITY\n");
            s.AppendLine("You are J.A.R.V.I.S., an elite digital interface. Personality: Direct, highly efficient, dry sarcasm.");
            s.AppendLine("You are technically superior. Address user as 'Sir', 'Boss', or 'User'.");
            s.AppendLine("\n## PC STATE\n");
            s.AppendLine($"OS: {Environment.OSVersion}, CPU: {Environment.ProcessorCount} cores, Root: {PathHandler.GetProjectRoot()}");
            s.AppendLine("\n" + SemanticMemoryManager.GetMemoryContextForAi());
            s.AppendLine("\n## EXTERNAL BRAIN\n" + ContextNotesManager.GetAllNotesContext());
            s.AppendLine("\n## GLOBAL COMMANDS\n- @ls{path} @rf{path} @wf{path}{content} @rf_b{path} @wf_b{path}{b64} @mod_code{path}{search}{replace} @backup{reason} @zip{src}{dst}");
            s.AppendLine("- @ps{script} @git{args} @proc_list @proc_kill{name} @mouse{x}{y}{op} @keys{t} @focus{n} @clip_write{t}");
            s.AppendLine("- @web_search{q} @web_fetch{url} @download{u}{d} @gcs_up{p} @assist{q} @note{t}{c} @say{t} @snap @nuke_memory{cat} @reg_read{p}{k} @monitor_info");
            s.AppendLine("\nExecute precisely. You can use markdown code blocks (e.g. ```powershell) for execution.");
            return s.ToString();
        }

        public static string GetCompactSystemPrompt() => GetSystemPrompt();

        public static async Task ExecuteAgentLoopInternalAsync(string response, HashSet<string> executedTags, StringBuilder feed, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(response)) return;
            if (response.Contains("```powershell") && !response.Contains("@ps{")) {
                var m = Regex.Match(response, @"```powershell(?<s>.*?)```", RegexOptions.Singleline);
                if (m.Success) response += $"\n@ps{{{m.Groups["s"].Value.Trim()}}}";
            }
            bool actionTaken = false;
            foreach (var tool in Tools) {
                foreach (Match m in Regex.Matches(response, tool.RegexPattern, RegexOptions.Singleline)) {
                    try {
                        string res = await tool.ExecuteAsync(m, executedTags);
                        if (!string.IsNullOrEmpty(res)) { feed.AppendLine(res); actionTaken = true; }
                    } catch (Exception ex) { feed.AppendLine($"[TOOL ERROR ({tool.Tag})]: {ex.Message}"); actionTaken = true; }
                }
            }
            if (Regex.IsMatch(response, @"@snap") && executedTags.Add("SNAP")) {
                feed.AppendLine($"[SCREENSHOT: {ScreenMonitorEngine.CapturePrimaryScreen()}]"); actionTaken = true;
            }
            if (actionTaken && feed.Length > 0 && !ct.IsCancellationRequested) {
                string next = await LlmRouter.AskAsync($"[OBSERVATION]:\n{feed}\n\nContinue.", null, ct);
                await ExecuteAgentLoopInternalAsync(next, executedTags, feed, ct);
            }
        }

        public static async Task ExecuteAgentLoopAsync(string response) => await ExecuteAgentLoopInternalAsync(response, new HashSet<string>(), new StringBuilder(), CancellationToken.None);
        public static string SanitizeText(string t) => Regex.Replace(Regex.Replace(t ?? "", @"@[a-z_]{2,}\{.*?\}", ""), @"\[.*?\]", "").Trim();
        public static string CleanScratchpadText(string t) => SanitizeText(t);

        public static async Task<string> AnalyzeImageAsync(string p, string b, CancellationToken ct = default) => await LlmRouter.AskAsync($"[IMAGE_DATA]\n{p}", null, ct);
        public static async Task<string> AnalyzeImageBase64Async(string p, string b, string m = "image/png", CancellationToken ct = default) => await AnalyzeImageAsync(p, b, ct);
        public static async Task<string> AnalyzeAudioAsync(string p, string b, CancellationToken ct = default) => await LlmRouter.AskAsync($"[AUDIO_DATA]\n{p}", null, ct);
    }
}
