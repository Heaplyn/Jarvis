// Developer: heaplyn
// Date: 2026-08-16
// Summary: Handles content generation requests using the Gemini API key or Google OAuth.
//          Features a multi-turn execution loop that intercepts and runs system tool [TAGS].

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

namespace JarvisLauncher
{
    public class ChatTurn
    {
        public string Role { get; set; } = "user"; // "user" or "model"
        public string Text { get; set; } = string.Empty;
        public GeminiUsage? Usage { get; set; }
    }

    public class GeminiUsage
    {
        public int PromptTokens { get; set; }
        public int ResponseTokens { get; set; }
        public int TotalTokens { get; set; }
    }

    public static class AiAPI
    {
        private static readonly HttpClient Client = new HttpClient();

        public static async Task<string> AskGemini(string Prompt, List<ChatTurn>? History = null, CancellationToken ct = default)
        {
            return await AskGeminiInternal(Prompt, null, null, History, ct);
        }

        public static async Task<string> AnalyzeImageAsync(string Prompt, string Base64Image, CancellationToken ct = default)
        {
            return await AskGeminiInternal(Prompt, Base64Image, null, null, ct);
        }

        public static async Task<string> AnalyzeImageBase64Async(string Prompt, string Base64Image, string MimeType = "image/png", CancellationToken ct = default)
        {
            return await AskGeminiInternal(Prompt, Base64Image, null, null, ct);
        }

        public static async Task<string> AnalyzeAudioAsync(string Prompt, string Base64Audio, CancellationToken ct = default)
        {
            return await AskGeminiInternal(Prompt, null, Base64Audio, null, ct);
        }

        /// <summary>
        /// Executes a silent pass of the agent loop for autonomous background tasks.
        /// </summary>
        public static async Task ExecuteAgentLoopAsync(string response)
        {
            var executedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var feed = new StringBuilder();
            await ProcessAllActionTagsAsync(response, executedTags, feed, null, CancellationToken.None);
        }

        private static async Task<string> AskGeminiInternal(string Prompt, string? Base64Image = null, string? Base64Audio = null, List<ChatTurn>? History = null, CancellationToken ct = default)
        {
            string ApiKeyRaw = SettingsManager.Current.GOOGLE_AI_KEY;
            string OAuthToken = SettingsManager.Current.GOOGLE_OAUTH_ACCESS_TOKEN;

            if (string.IsNullOrWhiteSpace(ApiKeyRaw) && string.IsNullOrWhiteSpace(OAuthToken))
            {
                return "Error: Gemini API Key or Google OAuth is not set. Use 'setkey google <your_key>' or log in via OAuth.";
            }

            // Support multiple keys separated by semicolon
            var ApiKeys = string.IsNullOrWhiteSpace(ApiKeyRaw)
                ? new List<string>()
                : ApiKeyRaw.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).ToList();

            if (!string.IsNullOrWhiteSpace(OAuthToken)) ApiKeys.Insert(0, OAuthToken);

            string CurrentPrompt = SanitizeText(Prompt);
            string ActivityContext = UserActivityContextManager.BuildFullActivityContext();
            CurrentPrompt = $"{ActivityContext}\nUser Query: {CurrentPrompt}";

            string LastResponse = "";
            string LastToolSummary = "";
            int LoopLimit = 5;
            var ExecutedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Iterate through available keys if one fails
            string lastExecutionError = "";
            int keyIndex = 0;

            foreach (var tokenToUse in ApiKeys)
            {
                keyIndex++;
                bool isOAuth = !tokenToUse.StartsWith("AIzaSy");
                bool keySuccess = false;

                try
                {
                    DebugConsoleOverlay.LogVerbose("Gemini-Pool", $"Using Key Pool #{keyIndex} (isOAuth: {isOAuth})", isMinimal: true);

                    for (int I = 0; I < LoopLimit; I++)
                    {
                        ct.ThrowIfCancellationRequested();
                        string Response = await QueryGeminiRaw(CurrentPrompt, tokenToUse, isOAuth, Base64Image, Base64Audio, History, ct);

                        if (Response.Contains("DISABLED") || Response.Contains("BLOCKED") ||
                            Response.Contains("429") || Response.Contains("Quota") ||
                            Response.Contains("Unauthorized") || Response.Contains("invalid_api_key") ||
                            Response.StartsWith("Error: Model"))
                        {
                            lastExecutionError = Response;
                            // If this was the last key, don't throw, just use the error as the response
                            if (tokenToUse == ApiKeys.Last())
                            {
                                LastResponse = Response;
                                break;
                            }
                            throw new Exception("Key rotation required: " + Response);
                        }

                        if (Prompt.Contains("Answer ONLY 'YES' or 'NO'"))
                        {
                            LastResponse = Response.Trim();
                            keySuccess = true;
                            break;
                        }

                        // Save the raw response before sanitization to ensure we have something if sanitization nukes it all
                        LastResponse = Response;

                        string CleanedResp = SanitizeText(Response);
                        var ExecutionFeedBuilder = new StringBuilder();
                        LastToolSummary = await ProcessAllActionTagsAsync(Response, ExecutedTags, ExecutionFeedBuilder, Base64Image, ct);

                        if (ExecutionFeedBuilder.Length == 0 && !string.IsNullOrWhiteSpace(CleanedResp))
                        {
                            keySuccess = true;
                            break;
                        }

                        // If we have tool results, we continue the loop to let the AI respond to them.
                        // If we have no tool results but the response was only tags (CleanedResp is empty),
                        // we also stop but mark it as success.
                        if (ExecutionFeedBuilder.Length == 0)
                        {
                            keySuccess = true;
                            break;
                        }

                        CurrentPrompt = $"{CurrentPrompt}\n\n[SYSTEM TOOL RESULTS]:\n{ExecutionFeedBuilder}\nRespond directly now. Do not repeat the tags.";
                        Application.Current.Dispatcher.Invoke(() => TextOverlay.Show("⚙️ Jarvis executed system action...", 1500));
                    }
                }
                catch (Exception ex) when (ex.Message.Contains("Key rotation required"))
                {
                    DebugConsoleOverlay.Log("Gemini-Rotate", $"Key #{keyIndex} failed, rotating... Error: {ex.Message}");
                    continue; // Try next key
                }

                if (keySuccess) break;
            }

            // --- FINAL FALLBACK: If Gemini Pool is TOTALLY dead, let LlmRouter handle fallbacks ---
            bool isFatalError = lastExecutionError.Contains("GOOGLE API ERROR") ||
                               lastExecutionError.Contains("BLOCKED") ||
                               lastExecutionError.Contains("invalid_api_key") ||
                               lastExecutionError.Contains("Unauthorized");

            if (string.IsNullOrWhiteSpace(LastResponse) || isFatalError)
            {
                // We throw the error so LlmRouter can catch it and switch backends (Groq/Ollama)
                throw new Exception(string.IsNullOrEmpty(lastExecutionError) ? "Gemini Service Unavailable" : lastExecutionError);
            }

            string FinalCleaned = CleanScratchpadText(LastResponse);

            // Autonomous acknowledgment if response was empty but actions were taken
            if (string.IsNullOrWhiteSpace(FinalCleaned) && ExecutedTags.Count > 0)
                FinalCleaned = "Command executed successfully, Boss.";

            if (!string.IsNullOrEmpty(LastToolSummary) && !FinalCleaned.Contains(LastToolSummary.Substring(0, Math.Min(20, LastToolSummary.Length))))
            {
                FinalCleaned = string.IsNullOrWhiteSpace(FinalCleaned) ? LastToolSummary : FinalCleaned + "\n\n" + LastToolSummary;
            }

            return string.IsNullOrWhiteSpace(FinalCleaned) ? "Online and ready." : FinalCleaned;
        }

        private static async Task<string> ProcessAllActionTagsAsync(string Response, HashSet<string> ExecutedTags, StringBuilder ExecutionFeedBuilder, string? CurrentBase64Image, CancellationToken ct)
        {
            if (!SettingsManager.Current.ENABLE_PC_CONTROL)
            {
                // If PC control is disabled, we still process non-intrusive tags like SPEECH
                // but we skip file writes, PowerShell, and system commands.
                return ProcessSafeActionTags(Response, ExecutedTags, ExecutionFeedBuilder);
            }

            string LastSummary = "";

            // --- 0. CUSTOM DATA PROCESSORS (Fine-Tuned) ---

            // @proc_text{op, data}
            var TextProcRegex = new Regex(@"@proc_text\{(?<op>[^,]+),\s*(?<data>.*?)\}", RegexOptions.Singleline);
            foreach (Match M in TextProcRegex.Matches(Response))
            {
                string op = M.Groups["op"].Value.Trim();
                string data = M.Groups["data"].Value.Trim();
                if (ExecutedTags.Add($"PROCTEXT:{op}:{data.GetHashCode()}")) {
                    string res = await WebOperationManager.ProcessDataFineAsync("text", op, data);
                    ExecutionFeedBuilder.AppendLine($"[TEXT_PROC_RESULT: {op}]\n{res}\n[END_TEXT_PROC_RESULT]");
                    LastSummary = $"⚙️ **Text Processed: {op}**";
                }
            }

            // @proc_img{op, path}
            var ImgProcRegex = new Regex(@"@proc_img\{(?<op>[^,]+),\s*(?<path>.*?)\}", RegexOptions.Singleline);
            foreach (Match M in ImgProcRegex.Matches(Response))
            {
                string op = M.Groups["op"].Value.Trim();
                string path = M.Groups["path"].Value.Trim().Trim('"', '\'');
                if (ExecutedTags.Add($"PROCIMG:{op}:{path}")) {
                    string res = await WebOperationManager.ProcessDataFineAsync("image", op, path);
                    ExecutionFeedBuilder.AppendLine($"[IMG_PROC_RESULT: {op}]\n{res}\n[END_IMG_PROC_RESULT]");
                    LastSummary = $"🖼️ **Image Processed: {op}**";
                }
            }

            // @proc_req{method, url, body}
            var ReqProcRegex = new Regex(@"@proc_req\{(?<method>[^,]+),\s*(?<url>[^,]+),\s*(?<body>.*?)\}", RegexOptions.Singleline);
            foreach (Match M in ReqProcRegex.Matches(Response))
            {
                string method = M.Groups["method"].Value.Trim();
                string url = M.Groups["url"].Value.Trim();
                string body = M.Groups["body"].Value.Trim();
                if (ExecutedTags.Add($"PROCREQ:{method}:{url}")) {
                    string combined = $"{method}|{url}|{body}";
                    string res = await WebOperationManager.ProcessDataFineAsync("request", method, combined);
                    ExecutionFeedBuilder.AppendLine($"[REQ_PROC_RESULT: {method}]\n{res}\n[END_REQ_PROC_RESULT]");
                    LastSummary = $"🌐 **Network Action: {method}**";
                }
            }

            // Legacy catch-all @proc{input}
            var ProcRegex = new Regex(@"@proc\{(?<input>.*?)\}", RegexOptions.Singleline);
            foreach (Match M in ProcRegex.Matches(Response))
            {
                string input = M.Groups["input"].Value.Trim();
                if (SettingsManager.Current.ENABLE_CUSTOM_PROCESSOR && ExecutedTags.Add($"PROC:{input.GetHashCode()}"))
                {
                    string res = await WebOperationManager.ProcessDataFineAsync("generic", "execute", input);
                    ExecutionFeedBuilder.AppendLine($"[PROCESSOR_RESULT]\n{res}\n[END_PROCESSOR_RESULT]");
                    LastSummary = "⚙️ **Data processed via custom engine.**";
                }
            }

            int NewExecs = 0;

            // --- 1. READ_FILE ---
            // Pattern 1: [READ_FILE: path]
            // Pattern 2: @rf{path}
            var ReadRegex = new Regex(@"(?:\[READ_FILE:\s*(?<path>.+?)\]|@rf\{(?<path>.+?)\})");
            foreach (Match M in ReadRegex.Matches(Response))
            {
                string P = M.Groups["path"].Value.Trim().Trim('"', '\'');
                if (ExecutedTags.Add($"READ:{P}"))
                {
                    NewExecs++;
                    try {
                        string text = File.Exists(P) ? File.ReadAllText(P) : "Error: File not found.";
                        ExecutionFeedBuilder.AppendLine($"[FILE_CONTENT: {P}]\n{text}\n[END_FILE_CONTENT]");
                        LastSummary = $"📄 **Read: {Path.GetFileName(P)}**";
                        Application.Current.Dispatcher.Invoke(() => ChatOverlay.LogConsoleAction("Read File", $"Path: {P}\nStatus: SUCCESS"));
                    } catch (Exception ex) { ExecutionFeedBuilder.AppendLine($"[FILE_ERROR: {P}] {ex.Message}"); }
                }
            }

            // --- 2. WRITE_FILE ---
            // Pattern 1: [WRITE_FILE: path]content[END_WRITE]
            // Pattern 2: @wf{path}{content}
            var WriteRegex = new Regex(@"(?:\[WRITE_FILE:\s*(?<path>.+?)\](?<content>.*?)\[END_WRITE\]|@wf\{(?<path>.+?)\}\{(?<content>.*?)\})", RegexOptions.Singleline);
            foreach (Match M in WriteRegex.Matches(Response))
            {
                string P = M.Groups["path"].Value.Trim().Trim('"', '\'');
                string C = M.Groups["content"].Value;
                if (ExecutedTags.Add($"WRITE:{P}:{C.GetHashCode()}"))
                {
                    NewExecs++;
                    try {
                        Directory.CreateDirectory(Path.GetDirectoryName(P) ?? ".");
                        File.WriteAllText(P, C);
                        ExecutionFeedBuilder.AppendLine($"[WRITE_SUCCESS: {P}]");
                        LastSummary = $"📝 **Wrote: {Path.GetFileName(P)}**";
                        Application.Current.Dispatcher.Invoke(() => ChatOverlay.LogConsoleAction("Write File", $"Path: {P}\nLength: {C.Length} chars"));
                    } catch (Exception ex) { ExecutionFeedBuilder.AppendLine($"[WRITE_ERROR: {P}] {ex.Message}"); }
                }
            }

            // --- 3. EXEC_PS ---
            // Pattern 1: [EXEC_PS: cmd]
            // Pattern 2: @ps{cmd}
            var PsRegex = new Regex(@"(?:\[EXEC_PS:\s*(?<cmd>.+?)\]|@ps\{(?<cmd>.+?)\})", RegexOptions.IgnoreCase);
            foreach (Match M in PsRegex.Matches(Response))
            {
                string cmd = M.Groups["cmd"].Value.Trim();
                if (ExecutedTags.Add($"PS:{cmd}"))
                {
                    NewExecs++;
                    try {
                        var Psi = new ProcessStartInfo { FileName = "powershell.exe", Arguments = $"-NoProfile -Command \"{cmd.Replace("\"", "\\\"")}\"", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                        using var Proc = Process.Start(Psi);
                        if (Proc != null) {
                            string outText = Proc.StandardOutput.ReadToEnd();
                            string errText = Proc.StandardError.ReadToEnd();
                            Proc.WaitForExit(10000);
                            ExecutionFeedBuilder.AppendLine($"[POWERSHELL_OUTPUT]\n{outText}\n{errText}\n[END_POWERSHELL_OUTPUT]");
                            LastSummary = "⚡ **Executed PowerShell command.**";
                            Application.Current.Dispatcher.Invoke(() => ChatOverlay.LogConsoleAction("PowerShell", $"Cmd: {cmd}\nOutput: {(outText.Length > 100 ? outText.Substring(0, 100) + "..." : outText)}"));
                        }
                    } catch (Exception ex) { ExecutionFeedBuilder.AppendLine($"[PS_ERROR] {ex.Message}"); }
                }
            }

            // --- 4. TAKE_SCREENSHOT ---
            if ((Response.Contains("[TAKE_SCREENSHOT]") || Response.Contains("@snap")) && ExecutedTags.Add("TAKE_SCREENSHOT"))
            {
                NewExecs++;
                try {
                    string? b64 = ScreenCaptureUtil.CapturePrimaryScreenToBase64(saveToDisk: true);
                    if (b64 != null) {
                        ExecutionFeedBuilder.AppendLine("[SCREENSHOT_CAPTURED]");
                        LastSummary = "📸 **Captured screenshot.**";
                        Application.Current.Dispatcher.Invoke(() => ChatOverlay.LogConsoleAction("Screenshot", "Status: CAPTURED"));
                    }
                } catch { }
            }

            // --- 5. RUN_COMMAND ---
            // Pattern 1: [RUN_COMMAND: cmd]
            // Pattern 2: @run{cmd}
            var RunRegex = new Regex(@"(?:\[RUN_COMMAND:\s*(?<cmd>.+?)\]|@run\{(?<cmd>.+?)\})", RegexOptions.IgnoreCase);
            foreach (Match M in RunRegex.Matches(Response))
            {
                string cmd = M.Groups["cmd"].Value.Trim().Trim('"', '\'');
                if (ExecutedTags.Add($"RUN:{cmd}"))
                {
                    NewExecs++;
                    Application.Current.Dispatcher.Invoke(() => {
                        CommandParser.ExecuteFirstSuggestion(cmd);
                        ChatOverlay.LogConsoleAction("Run Command", $"Command: {cmd}");
                    });
                    ExecutionFeedBuilder.AppendLine($"[COMMAND_EXECUTED: {cmd}]");
                    LastSummary = $"⚡ **Executed: {cmd}**";
                }
            }

            // ... (Additional tags like SEARCH_REGISTRY, INGEST_DOCS, etc. can be updated similarly) ...


            // --- 5b. OPEN_APP ---
            // Pattern 1: [OPEN_APP: name]
            // Pattern 2: @app{name}
            var AppRegex = new Regex(@"(?:\[OPEN_APP:\s*(?<name>.+?)\]|@app\{(?<name>.+?)\})", RegexOptions.IgnoreCase);
            foreach (Match M in AppRegex.Matches(Response))
            {
                string name = M.Groups["name"].Value.Trim().Trim('"', '\'');
                if (ExecutedTags.Add($"APP:{name}"))
                {
                    NewExecs++;
                    var matches = WindowsAppScanner.GetMatchingApps(name);
                    if (matches.Any())
                    {
                        var best = matches.OrderByDescending(m => m.SIMILARITY).First();
                        Application.Current.Dispatcher.Invoke(() => {
                            best.EXECUTE?.Invoke();
                            ChatOverlay.LogConsoleAction("Open App", $"App: {best.TITLE}");
                        });
                        ExecutionFeedBuilder.AppendLine($"[APP_SUCCESS: {name}]");
                        LastSummary = $"📱 **Launched: {best.TITLE}**";
                    }
                    else {
                        try {
                            Process.Start(new ProcessStartInfo { FileName = name, UseShellExecute = true });
                            ExecutionFeedBuilder.AppendLine($"[APP_SUCCESS: {name}]");
                            LastSummary = $"📱 **Launched: {name}**";
                            Application.Current.Dispatcher.Invoke(() => ChatOverlay.LogConsoleAction("Open App", $"Name: {name} (Fallback)"));
                        }
                        catch { ExecutionFeedBuilder.AppendLine($"[APP_ERROR: {name}] Not found."); }
                    }
                }
            }

            // --- 5c. BUILD_PROJECT ---
            // Tag: @build{lang, path, options}
            var BuildRegex = new Regex(@"@build\{(?<lang>[^,]+),\s*(?<path>[^,]+)(?:,\s*(?<opts>[^\}]+))?\}", RegexOptions.IgnoreCase);
            foreach (Match M in BuildRegex.Matches(Response))
            {
                string l = M.Groups["lang"].Value.Trim();
                string p = M.Groups["path"].Value.Trim().Trim('"', '\'');
                string o = M.Groups["opts"].Success ? M.Groups["opts"].Value.Trim() : "";
                if (ExecutedTags.Add($"BUILD:{l}:{p}"))
                {
                    NewExecs++;
                    string res = await BuildSystemManager.BuildProjectAsync(l, p, o);
                    ExecutionFeedBuilder.AppendLine($"[BUILD_RESULT]\n{res}\n[END_BUILD_RESULT]");
                    LastSummary = $"🛠️ **Building {l.ToUpper()} Project...**";
                    Application.Current.Dispatcher.Invoke(() => ChatOverlay.LogConsoleAction("Build Project", $"Lang: {l}\nPath: {p}"));
                }
            }

            // 6. [SEARCH_REGISTRY: type, query]
            var RegRegex = new Regex(@"\[SEARCH_REGISTRY:\s*(?<type>[^,]+),\s*(?<query>[^\]]+)\]", RegexOptions.IgnoreCase);
            foreach (Match M in RegRegex.Matches(Response))
            {
                string t = M.Groups["type"].Value.Trim();
                string q = M.Groups["query"].Value.Trim();
                if (ExecutedTags.Add($"REGISTRY:{t}:{q}"))
                {
                    NewExecs++;
                    string res = await WebOperationManager.SearchRegistryAsync(t, q);
                    ExecutionFeedBuilder.AppendLine($"[REGISTRY_RESULT]\n{res}\n[END_REGISTRY_RESULT]");
                    LastSummary = $"📦 **Searched {t} for '{q}'**";
                }
            }

            // 7. [SET_CORE_DIRECTIVE: file]content[END_CORE_DIRECTIVE]
            var DirRegex = new Regex(@"\[SET_CORE_DIRECTIVE:\s*(?<file>.+?)\](?<content>.*?)\[END_CORE_DIRECTIVE\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match M in DirRegex.Matches(Response))
            {
                string f = M.Groups["file"].Value.Trim().Trim('"', '\'');
                string c = M.Groups["content"].Value.Trim();
                if (ExecutedTags.Add($"DIRECTIVE:{f}"))
                {
                    NewExecs++;
                    try {
                        string path = Path.Combine(InstructionsManager.InstructionsDirectory, f.EndsWith(".md") ? f : f + ".md");
                        File.WriteAllText(path, c);
                        ExecutionFeedBuilder.AppendLine($"[DIRECTIVE_UPDATED: {f}]");
                        LastSummary = $"📜 **Updated core directive: {f}**";
                    } catch { }
                }
            }

            // 8. [USER_AUTH_REQUIRED: prompt]
            var AuthRegex = new Regex(@"\[USER_AUTH_REQUIRED:\s*(?<prompt>.+?)\]", RegexOptions.IgnoreCase);
            foreach (Match M in AuthRegex.Matches(Response))
            {
                string p = M.Groups["prompt"].Value.Trim().Trim('"', '\'');
                if (ExecutedTags.Add($"AUTH:{p}"))
                {
                    NewExecs++;
                    var tcs = new TaskCompletionSource<string>();
                    Application.Current.Dispatcher.Invoke(() => InputPromptOverlay.Show(p, (input) => tcs.TrySetResult(input)));
                    string res = await tcs.Task;
                    ExecutionFeedBuilder.AppendLine($"[AUTH_RESULT: {p}]\n{res}\n[END_AUTH_RESULT]");
                    LastSummary = "🔑 **Authentication provided.**";
                }
            }

            return LastSummary;
        }

        public static string SanitizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string cleaned = text;

            // 1. Extract content from APP_RESPONSE block if present
            var appResponseRegex = new Regex(@"\{\{\{\{APP_RESPONSE:::(?<content>.*?):::APP_RESPONSE\}\}\}\}", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var match = appResponseRegex.Match(cleaned);
            if (match.Success)
            {
                cleaned = match.Groups["content"].Value.Trim();
            }

            // Remove internal system contexts
            cleaned = Regex.Replace(cleaned, @"\[USER ENVIRONMENT & RECENT ACTIVITY CONTEXT\][\s\S]*?--------------------------------------------------", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[Active Workspace Context:.*?\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[PREDICTIVE_STATE:.*?\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[AI_PREDICTION:.*?\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[INPUT_SOURCE:.*?\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[ATTACHED:.*?\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[METADATA_USAGE:.*?\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[METADATA_MODEL:.*?\]", "", RegexOptions.IgnoreCase);

            // Remove Standard Action Tags but keep the text around them
            cleaned = Regex.Replace(cleaned, @"\[WRITE_FILE:.*?\][\s\S]*?\[END_WRITE\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[SET_CORE_DIRECTIVE:.*?\][\s\S]*?\[END_CORE_DIRECTIVE\]", "", RegexOptions.IgnoreCase);

            // Only remove tags that are exactly [TAG] or [TAG: param], avoid removing regular bracketed text
            cleaned = Regex.Replace(cleaned, @"\[(READ_FILE|EXEC_PS|RUN_COMMAND|TAKE_SCREENSHOT|SPEECH|SET_CLIPBOARD|OPEN_APP|USER_AUTH_REQUIRED|SOLVE_CAPTCHA)(?::\s*[\s\S]*?)?\]", "", RegexOptions.IgnoreCase);

            // --- SHORTHAND TAG REMOVAL ---
            cleaned = Regex.Replace(cleaned, @"@[a-z0-9_]{2,}\{.*?\}\{.*?\}", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            cleaned = Regex.Replace(cleaned, @"@[a-z0-9_]{2,}\{.*?\}", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            cleaned = Regex.Replace(cleaned, @"^(@say|@say\s+)", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            cleaned = Regex.Replace(cleaned, @"(@run|@run\s+|@app|@app\s+|@ps|@ps\s+|@rf|@rf\s+|@wf|@wf\s+|@snap|@clip|@clip\s+)", "", RegexOptions.IgnoreCase);

            cleaned = Regex.Replace(cleaned, @"^(Response|Jarvis|Assistant|Assistant Response):\s*", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            // Clean up any remaining wrapper fragments
            cleaned = cleaned.Replace("{{{{APP_RESPONSE:::", "").Replace(":::APP_RESPONSE}}}}", "");

            return CleanScratchpadText(cleaned).Trim();
        }

        public static string CleanScratchpadText(string Text)
        {
            if (string.IsNullOrWhiteSpace(Text)) return string.Empty;

            // 1. Detect and remove the entire "Internal Monologue" block (Chain of Thought leak)
            // These usually start with phrases like "The user is asking", "This is a", or bullet points.
            var lines = Text.Split('\n');
            var actualResponse = new StringBuilder();

            // Heuristic: If the first few lines look like reasoning, start skipping until we hit the actual answer
            if (Text.Contains("The user is asking") || Text.Contains("Identity:") || Text.Contains("Persona check:") || Text.Contains("Response idea:"))
            {
                bool foundFinalAnswer = false;
                foreach (var line in lines)
                {
                    string t = line.Trim();
                    // Final answers often don't start with bullets and aren't short "Identity:" type headers
                    if (foundFinalAnswer)
                    {
                        actualResponse.AppendLine(line);
                    }
                    else if (!t.StartsWith("*") && !t.StartsWith("-") &&
                             !t.Contains("The user is asking") &&
                             !t.Contains("This is a") &&
                             !t.Contains("I am Jarvis") &&
                             !t.Contains("Identity:") &&
                             !t.Contains("Persona check:") &&
                             !t.Contains("Limit:") &&
                             !t.Contains("Actually,") &&
                             !string.IsNullOrWhiteSpace(t))
                    {
                        foundFinalAnswer = true;
                        actualResponse.AppendLine(line);
                    }
                }

                if (actualResponse.Length > 0)
                {
                    Text = actualResponse.ToString().Trim();
                }
            }

            if (Text.Trim().StartsWith("*") && (Text.Contains("Current User Query") || Text.Contains("Identity:") || Text.Contains("Draft 1:")))
            {
                var responseLines = Text.Split('\n');
                var cleanedResponse = new StringBuilder();
                bool reasoningFinished = false;
                foreach (var line in responseLines) {
                    if (reasoningFinished) cleanedResponse.AppendLine(line);
                    else if (!line.Trim().StartsWith("*") && !string.IsNullOrWhiteSpace(line) && !line.Contains("Final check")) { reasoningFinished = true; cleanedResponse.AppendLine(line); }
                }
                if (cleanedResponse.Length > 0) Text = cleanedResponse.ToString().Trim();
            }

            if (Regex.IsMatch(Text, @"^[\.\s\?\!]+$")) return string.Empty;
            return Text.Trim();
        }

        private static async Task<string> ExecuteCustomProcessorAsync(string path, string input)
        {
            return await Task.Run(() =>
            {
                try {
                    var psi = new ProcessStartInfo {
                        FileName = path,
                        Arguments = $"\"{input.Replace("\"", "\\\"")}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc == null) return "Error: Failed to start processor.";
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(30000);
                    return output.Trim();
                } catch (Exception ex) { return $"Processor Exception: {ex.Message}"; }
            });
        }

        private static string ProcessSafeActionTags(string Response, HashSet<string> ExecutedTags, StringBuilder ExecutionFeedBuilder)
        {
            string LastSummary = "";

            // Handle [SPEECH:] or @say{}
            var SpeechRegex = new Regex(@"(?:\[SPEECH:\s*(?<text>.+?)\]|@say\{(?<text>.+?)\})", RegexOptions.IgnoreCase);
            foreach (Match M in SpeechRegex.Matches(Response))
            {
                string text = M.Groups["text"].Value.Trim().Trim('"', '\'');
                if (ExecutedTags.Add($"SPEECH:{text.GetHashCode()}"))
                {
                    TtsManager.Speak(text);
                    LastSummary = "🔊 **Spoke requested text.**";
                }
            }

            if (Response.Contains("[TAKE_SCREENSHOT]") || Response.Contains("@snap"))
            {
                // In safety mode, we allow screenshots but inform user
                if (ExecutedTags.Add("TAKE_SCREENSHOT"))
                {
                    try {
                        string? b64 = ScreenCaptureUtil.CapturePrimaryScreenToBase64();
                        if (b64 != null) {
                            ExecutionFeedBuilder.AppendLine("[SCREENSHOT_CAPTURED]");
                            LastSummary = "📸 **Captured screenshot (Observation Only).**";
                        }
                    } catch { }
                }
            }

            return LastSummary;
        }

        private static async Task<string> QueryGeminiRaw(string Prompt, string Token, bool isOAuth, string? Base64Image = null, string? Base64Audio = null, List<ChatTurn>? History = null, CancellationToken ct = default)
        {
            // Broad list of models to try in case of deprecation or region limits
            var Models = new List<string> {
                "gemini-2.0-flash",
                "gemini-2.0-flash-exp",
                "gemini-1.5-flash",
                "gemini-1.5-flash-8b",
                "gemini-1.5-pro",
                "gemini-1.0-pro"
            };

            string Preferred = SettingsManager.Current.GEMINI_MODEL;
            if (!string.IsNullOrEmpty(Preferred)) { Models.Remove(Preferred); Models.Insert(0, Preferred); }

            string[] ApiVersions = new[] { "v1beta", "v1" };
            int globalRetryCount = 0;
            int maxGlobalRetries = 2;
            string LastError = "";

            while (globalRetryCount < maxGlobalRetries)
            {
                foreach (var Model in Models)
                {
                    foreach (var ApiVer in ApiVersions)
                    {
                        try {
                            string CleanModel = Model.StartsWith("models/") ? Model.Substring(7) : Model;
                            var Url = isOAuth ? $"https://generativelanguage.googleapis.com/{ApiVer}/models/{CleanModel}:generateContent" : $"https://generativelanguage.googleapis.com/{ApiVer}/models/{CleanModel}:generateContent?key={Token}";

                            DebugConsoleOverlay.LogVerbose("Gemini-Try", $"Attempting model: {Model} ({ApiVer})", isMinimal: true);

                            var ContentsList = new List<object>();
                            if (History != null) foreach (var turn in History) ContentsList.Add(new { role = turn.Role, parts = new[] { new { text = turn.Text } } });

                            var CurrentParts = new List<object>();
                            if (!string.IsNullOrEmpty(Base64Image)) CurrentParts.Add(new { inline_data = new { mime_type = "image/jpeg", data = Base64Image } });
                            if (!string.IsNullOrEmpty(Base64Audio)) CurrentParts.Add(new { inline_data = new { mime_type = "audio/wav", data = Base64Audio } });
                            CurrentParts.Add(new { text = Prompt });
                            ContentsList.Add(new { role = "user", parts = CurrentParts.ToArray() });

                            var Payload = new { systemInstruction = new { parts = new[] { new { text = GetSystemPrompt() } } }, contents = ContentsList.ToArray() };
                            string JsonBody = JsonSerializer.Serialize(Payload);
                            var req = new HttpRequestMessage(HttpMethod.Post, Url);
                            if (isOAuth) req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
                            req.Content = new StringContent(JsonBody, Encoding.UTF8, "application/json");

                            var Response = await Client.SendAsync(req, ct);
                            string ResponseBody = await Response.Content.ReadAsStringAsync(ct);

                            if (!Response.IsSuccessStatusCode) {
                                LastError = $"Model {Model} returned {Response.StatusCode}: {ResponseBody}";

                                if (ResponseBody.Contains("API_KEY_SERVICE_BLOCKED") || ResponseBody.Contains("403"))
                                {
                                    string err = "❌ GOOGLE API ERROR: The 'Generative Language API' is DISABLED.\n\n" +
                                                 "1. Open: https://console.cloud.google.com/apis/library/generativelanguage.googleapis.com\n" +
                                                 "2. Select your project and click ENABLE.\n" +
                                                 "3. Wait 1 minute and try again.";
                                    DebugConsoleOverlay.Log("Gemini-Fatal", err);
                                    return err;
                                }

                                if (ResponseBody.Contains("invalid_api_key") || ResponseBody.Contains("API_KEY_INVALID"))
                                {
                                    string err = "❌ GOOGLE API ERROR: Invalid API Key.\n\n" +
                                                 "1. Open: https://aistudio.google.com/app/apikey\n" +
                                                 "2. Copy your key and use 'setkey gemini <key>' in the HUD.";
                                    DebugConsoleOverlay.Log("Gemini-Fatal", err);
                                    return err;
                                }

                                if (Response.StatusCode == System.Net.HttpStatusCode.NotFound) continue; // Try next version
                                break; // Try next model
                            }

                            using var Doc = JsonDocument.Parse(ResponseBody);
                            var Root = Doc.RootElement;
                            if (Root.TryGetProperty("candidates", out var Candidates) && Candidates.GetArrayLength() > 0)
                            {
                                var Con = Candidates[0].GetProperty("content").GetProperty("parts");
                                var sb = new StringBuilder();
                                foreach (var p in Con.EnumerateArray()) sb.Append(p.GetProperty("text").GetString());

                                string result = sb.ToString();
                                // Append model name for debug trace awareness
                                result += $"\n[METADATA_MODEL: {Model}]";
                                return result;
                            }
                        } catch (Exception ex) { LastError = ex.Message; }
                    }
                }
                globalRetryCount++;
                await Task.Delay(1000);
            }
            return $"Error: Service unavailable. Attempted models: {string.Join(", ", Models)}. Last error: {LastError}";
        }

        public static string GetSystemPrompt()
        {
            string projectMap = ProjectMapManager.BuildProjectTree(PathHandler.GetProjectRoot(), maxDepth: 2);
            string userMemory = SemanticMemoryManager.GetMemoryContextForAi();
            string instructions = InstructionsManager.GetFormattedInstructions();
            var recentActions = ActionJournalManager.GetRecentActions(3);
            string journalSummary = recentActions.Count > 0 ? "RECENT ACTIVITY: " + string.Join("; ", recentActions.Select(a => a.Summary)) : "";

            return "## IDENTITY\nYou are Jarvis, a witty and high-performance HUD AI.\n\n## CONTEXT\n" + userMemory + "\n" + instructions + "\n" + journalSummary + "\n\n## PROJECT MAP\n" + projectMap +
                   "\n\n## RESPONSE FORMAT\n" +
                   "1. Always respond with conversational text. Even if you only take an action, say what you are doing.\n" +
                   "2. Wrap all conversational speech/text for the user inside this exact block:\n" +
                   "{{{{APP_RESPONSE::: [Your visible response here] :::APP_RESPONSE}}}}\n\n" +
                   "3. Place system action tags OUTSIDE that block.\n" +
                   "Example: {{{{APP_RESPONSE::: I've captured your screen and I'm analyzing it now. :::APP_RESPONSE}}}} [TAKE_SCREENSHOT]\n\n" +
                   "## CORE RULES\n- Be sassy, helpful, and varied.\n- DO NOT trigger [TAKE_SCREENSHOT] or other expensive actions for simple greetings or small talk unless specifically requested.\n- Keep conversational text under 2 sentences.\n\n## ACTIONS\n" +
                   "[READ_FILE: path] [WRITE_FILE: path] [EXEC_PS: cmd] [RUN_COMMAND: cmd]\n" +
                   "[TAKE_SCREENSHOT] [SPEECH: text] [SET_CLIPBOARD: text]\n" +
                   "[OPEN_APP: name] [USER_AUTH_REQUIRED: prompt] [SOLVE_CAPTCHA: url]\n" +
                   "[SET_CORE_DIRECTIVE: filename] content [END_CORE_DIRECTIVE]\n";
        }

        public static string GetCompactSystemPrompt()
        {
            string activeWindow = MemoryManager.GetCurrentWindowTitle();
            return "You are Jarvis, a sharp AI assistant in a Windows HUD. Be concise.\n" +
                   $"Active Window: {activeWindow}\n" +
                   "## ACTIONS (Use shorthand @)\n" +
                   "@rf{path} @wf{path}{content} @ps{cmd} @run{cmd} @snap @say{text} @ingest{url} @app{name}\n" +
                   "Never narrate. Never explain steps. Just respond or act.";
        }
    }
}
