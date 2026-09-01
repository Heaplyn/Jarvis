// Developer: heaplyn
// Date: 2026-08-19
// Summary: Universal AI Action Orchestrator with Autonomous Evolution.
//          Supports stacked/cascading tool calls and real-time capability synthesis.

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using JarvisLauncher.AiTools;

namespace JarvisLauncher
{
    public static class AgentExecutor
    {
        private const int MAX_TOOL_RECURSION = 5;

        public static async Task<string> ProcessAIResponseAsync(string aiResponse)
        {
            if (string.IsNullOrEmpty(aiResponse)) return aiResponse;
            aiResponse = AiAPI.CleanScratchpadText(aiResponse);

            // SECURITY: runtime tool synthesis (@new_tool) is DISABLED. Letting the model register
            // arbitrary regex->PowerShell tools at runtime is remote code execution by prompt.
            // Do not re-enable without a real sandbox (separate process, no shell, allow-list).

            if (!SettingsManager.Current.ENABLE_PC_CONTROL)
            {
                ProcessSafeIntents(aiResponse);
                return StripAllInternalTags(aiResponse);
            }

            string currentContext = aiResponse;
            var executedTags = new HashSet<string>();
            int iteration = 0;

            // --- UNIVERSAL TOOL LOOP (Supports Stacking/Chaining) ---
            while (iteration < MAX_TOOL_RECURSION)
            {
                var tools = AiToolRegistry.GetAllTools();
                var toolResults = new StringBuilder();
                bool anyExecuted = false;

                foreach (var tool in tools)
                {
                    try
                    {
                        var regex = new Regex(tool.RegexPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        var matches = regex.Matches(currentContext);

                        foreach (Match match in matches)
                        {
                            string result = await tool.ExecuteAsync(match, executedTags);
                            if (!string.IsNullOrEmpty(result))
                            {
                                toolResults.AppendLine(result);
                                anyExecuted = true;
                                ChatOverlay.LogConsoleAction("Tool Executed", $"[{tool.Tag}]: {match.Value}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugConsoleOverlay.Log("Tool-Error", $"[{tool.Tag}]: {ex.Message}");
                    }
                }

                // Process legacy hardcoded tags that return context
                string legacyOutput = await ProcessLegacyTagsWithContextAsync(currentContext, executedTags);
                if (!string.IsNullOrEmpty(legacyOutput))
                {
                    toolResults.AppendLine(legacyOutput);
                    anyExecuted = true;
                }

                if (!anyExecuted) break;

                // Stacked execution: The output of these tools is fed back to the orchestrator
                // This allows the AI to react to file contents, command results, etc.
                currentContext = toolResults.ToString();
                iteration++;

                DebugConsoleOverlay.Log("Ai-Agent", $"Cascading tool chain depth: {iteration}");
            }

            // Final pass for side-effect-only tags (Speech, UI)
            ProcessSafeIntents(aiResponse);

            return StripAllInternalTags(aiResponse);
        }

        private static async Task<string> ProcessLegacyTagsWithContextAsync(string response, HashSet<string> executed)
        {
            var sb = new StringBuilder();

            // SECURITY: model-emitted PowerShell (@ps{...} / [EXEC_PS:...]) is DISABLED. The model
            // must never run arbitrary shell. Internal fixed-script callers of ExecutePowerShellDirect
            // (firewall rule, diagnostics) are unaffected because they pass constant scripts, not model text.

            // 2. Process INGEST_DOCS
            var ingestRegex = new Regex(@"(?:\[INGEST_DOCS:\s*(?<url>.+?)\]|@ingest\{(?<url>.+?)\})", RegexOptions.IgnoreCase);
            foreach (Match m in ingestRegex.Matches(response))
            {
                string url = m.Groups["url"].Value.Trim();
                if (executed.Add("INGEST:" + url))
                {
                    _ = Task.Run(() => WebOperationManager.IngestDocumentationAsync(url));
                    sb.AppendLine($"[SYSTEM]: Triggered documentation ingestion for {url}");
                }
            }

            return sb.ToString();
        }

        public static string ProcessAIResponse(string aiResponse)
        {
             var task = Task.Run(() => ProcessAIResponseAsync(aiResponse));
             task.Wait();
             return task.Result;
        }

        private static void ProcessSafeIntents(string aiResponse)
        {
            // 5. Process SPEECH tags
            var speechRegex = new Regex(@"(?:\[SPEECH:\s*(?<text>[\s\S]+?)\]|@say\{(?<text>.*?)\})", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in speechRegex.Matches(aiResponse))
            {
                TtsManager.Speak(m.Groups["text"].Value.Trim().Trim('"', '\''));
            }

            // 6. Process SET_CLIPBOARD tags
            var clipRegex = new Regex(@"\[SET_CLIPBOARD:\s*(?<text>[\s\S]+?)\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in clipRegex.Matches(aiResponse))
            {
                string text = m.Groups["text"].Value.Trim().Trim('"', '\'');
                Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(text));
            }

            // 11. Handling UI commands
            var cmdRegex = new Regex(@"(?:\[RUN_COMMAND:\s*(?<cmd>.+?)\]|@run\{(?<cmd>.+?)\})", RegexOptions.IgnoreCase);
            foreach (Match m in cmdRegex.Matches(aiResponse))
            {
                string cmd = m.Groups["cmd"].Value.Trim();
                Application.Current.Dispatcher.Invoke(() => CommandParser.ExecuteFirstSuggestion(cmd));
            }

            // 12. Handle REBUILD / FRESH START
            if (aiResponse.Contains("[REBUILD_PROJECT]", StringComparison.OrdinalIgnoreCase) ||
                aiResponse.Contains("[FRESH_START]", StringComparison.OrdinalIgnoreCase))
            {
                Application.Current.Dispatcher.Invoke(() => NativeMethods.Restart(freshBoot: true));
            }
        }

        public static string StripAllInternalTags(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string cleaned = text;

            var appResponseRegex = new Regex(@"\{\{\{\{APP_RESPONSE:::(?<content>.*?):::APP_RESPONSE\}\}\}\}", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var match = appResponseRegex.Match(cleaned);
            if (match.Success) cleaned = match.Groups["content"].Value.Trim();

            cleaned = Regex.Replace(cleaned, @"\[WRITE_FILE:\s*.+?\][\s\S]*?\[END_WRITE\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[[A-Z0-9_]{3,}(?::\s*[\s\S]*?)?\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"@[a-z0-9_]{2,}\{.*?\}(\{.*?\})?", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var lines = cleaned.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l) && !Regex.IsMatch(l, @"^[\.\s\?\!]+$"));
            return string.Join("\n", lines).Trim();
        }

        public static string ExecutePowerShellDirect(string cmd)
        {
            try
            {
                string tempFile = Path.Combine(Path.GetTempPath(), $"jarvis_script_{Guid.NewGuid():N}.ps1");
                File.WriteAllText(tempFile, cmd, new UTF8Encoding(false));
                var psi = new ProcessStartInfo { FileName = "powershell.exe", Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempFile}\"", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                using var proc = Process.Start(psi);
                if (proc != null) {
                    string outText = proc.StandardOutput.ReadToEnd();
                    string errText = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(15000);
                    try { File.Delete(tempFile); } catch { }
                    return (outText + "\n" + errText).Trim();
                }
                return "[ERROR] Failed to launch script runner.";
            } catch (Exception ex) { return $"[ERROR] {ex.Message}"; }
        }
    }
}
