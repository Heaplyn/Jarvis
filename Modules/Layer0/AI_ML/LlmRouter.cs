// Developer: Heaplyn
// Date: 2026-08-21
// Summary: Central LLM dispatcher with EXHAUSTIVE Failover & Token Optimization.
//          Supports: Gemini, Groq, OpenAI, Anthropic, Mistral, OpenRouter, DeepSeek, X-AI (Grok), Ollama, Perplexity, Lemonade.
//          Enhanced with Token Saver logic: Dynamically prunes history based on prompt complexity.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace JarvisLauncher
{
    public static class LlmRouter
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        public static async Task<bool> IsOllamaAvailableAsync()
        {
            try {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                string endpoint = CoreRegistry.Data.Settings.Current.OLLAMA_ENDPOINT.TrimEnd('/');
                var resp = await _http.GetAsync($"{endpoint}/api/tags", cts.Token);
                return resp.IsSuccessStatusCode;
            } catch { return false; }
        }

        public static async Task<string> AskAsync(string prompt, List<ChatTurn>? history = null, CancellationToken ct = default)
        {
            var timer = Stopwatch.StartNew();
            var s = CoreRegistry.Data.Settings.Current;
            DebugConsoleOverlay.Log("AI-Router", $">>> GLOBAL Dispatcher active. Primary: {s.LLM_BACKEND}");

            // Inject instructions notice
            string systemInstruction = "[SYSTEM INSTRUCTION: You must strictly read and adhere to all AI manuals, guidelines, and layer rules located in the 'AI_README/' directory when analyzing code or making changes.]\n";
            prompt = systemInstruction + prompt;

            // --- TOKEN SAVER: Optimize History ---
            var optimizedHistory = GetOptimizedHistory(prompt, history);
            int turnsSaved = (history?.Count ?? 0) - optimizedHistory.Count;
            if (turnsSaved > 0) DebugConsoleOverlay.Log("AI-Router", $"Token Saver: Pruned {turnsSaved} turns from history.");

            // SPECIAL CASE: History retrieval
            string lowerPrompt = prompt.ToLower();
            if (lowerPrompt.Contains("yesterday") || lowerPrompt.Contains("history") || lowerPrompt.Contains("did i do")) {
                string historyContext = ChronoLogManager.GetHistoryForDate(DateTime.Now.AddDays(-1));
                prompt = $"[SYSTEM CONTEXT: USER HISTORY LOGS]\n{historyContext}\n\n### USER REQUEST\n{prompt}";
            }

            // LIVE WEB CONTEXT: scrape pasted URLs / run explicit web searches (read-only, safe).
            try {
                string webCtx = await WebContextInjector.MaybeFetchAsync(prompt, ct);
                if (!string.IsNullOrEmpty(webCtx)) {
                    DebugConsoleOverlay.Log("AI-Web", "Injected live web context into prompt.");
                    prompt = $"{webCtx}\n### USER REQUEST\n{prompt}";
                }
            } catch { }

            try {
                string? local = await HeuristicIntentParser.TryHandleLocallyAsync(prompt);
                if (local != null) return local;
            } catch { }

            // Build the chain starting with the user's preferred backend
            var chain = new List<string> { s.LLM_BACKEND };

            // Failover targets (Cloud nodes first)
            if (s.LLM_BACKEND != "Perplexity" && IsBackendConfigured("Perplexity")) chain.Add("Perplexity");
            if (s.LLM_BACKEND != "Gemini" && IsBackendConfigured("Gemini")) chain.Add("Gemini");
            if (s.LLM_BACKEND != "Groq" && IsBackendConfigured("Groq")) chain.Add("Groq");

            // Local fallback (Ollama)
            if (s.LLM_BACKEND != "Ollama" && await IsOllamaAvailableAsync()) chain.Add("Ollama");

            chain = chain.Distinct().ToList();
            var errorReports = new List<string>();

            foreach (var backend in chain)
            {
                try {
                    if (!IsBackendConfigured(backend)) continue;
                    DebugConsoleOverlay.Log("AI-Router", $"Attempting: {backend}");

                    // Per-attempt timeout so ONE hanging backend can't eat the whole 60s HttpClient
                    // timeout and stall failover — cancel at 35s and move to the next provider.
                    using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    attemptCts.CancelAfter(TimeSpan.FromSeconds(35));
                    string result = await CallBackendInternalAsync(backend, prompt, optimizedHistory, attemptCts.Token);

                    if (!string.IsNullOrEmpty(result) && !result.StartsWith("⚠️")) {
                        DebugConsoleOverlay.Log("AI-Router", $"Success: {backend} ({timer.ElapsedMilliseconds}ms)");
                        return result;
                    }
                } catch (Exception ex) {
                    string errorMsg = ex.Message;
                    if (errorMsg.Contains("429") || errorMsg.Contains("quota")) errorMsg = "Quota Exceeded (429)";
                    else if (errorMsg.Contains("404") || errorMsg.Contains("not found")) errorMsg = "Model Not Found (404)";

                    string err = $"[{backend}]: {errorMsg}";
                    errorReports.Add(err);
                    DebugConsoleOverlay.Log("AI-Fail", err);
                    continue;
                }
            }

            string finalError = string.Join("\n", errorReports);
            return $"⚠️ [FATAL] AI Pipeline collapsed. No configured models responded.\n\nERRORS:\n{finalError}";
        }

        private static List<ChatTurn> GetOptimizedHistory(string prompt, List<ChatTurn>? history)
        {
            if (history == null || history.Count == 0) return new List<ChatTurn>();

            string lower = prompt.ToLower();
            bool needsContext = prompt.Split(' ').Length < 6 ||
                               Regex.IsMatch(lower, @"\b(it|this|that|them|those|they|he|she|him|her|why|how|what|elaborate|explain|more|previous|above|before|again|summarize|recap|yes|no|ok|repeat|earlier|latter|former|continue|elaborate|expand)\b");

            int takeCount = needsContext ? 12 : 2;
            var recentHistory = history.TakeLast(takeCount).ToList();

            const int maxHistoryChars = 16000;
            const int maxIndividualTurnChars = 2000;
            
            var optimized = new List<ChatTurn>();
            int currentTotalChars = 0;

            for (int i = recentHistory.Count - 1; i >= 0; i--) {
                var turn = recentHistory[i];
                string turnText = turn.Text ?? "";
                if (turnText.Length > maxIndividualTurnChars) {
                    string head = turnText.Substring(0, maxIndividualTurnChars / 2);
                    string tail = turnText.Substring(turnText.Length - (maxIndividualTurnChars / 2));
                    turnText = $"{head}\n\n... [TRUNCATED] ...\n\n{tail}";
                }
                if (currentTotalChars + turnText.Length > maxHistoryChars) break;
                optimized.Insert(0, new ChatTurn { Role = turn.Role, Text = turnText });
                currentTotalChars += turnText.Length;
            }
            return optimized;
        }

        private static bool IsBackendConfigured(string b) {
            var s = CoreRegistry.Data.Settings.Current;
            return b switch {
                "Gemini" => !string.IsNullOrEmpty(s.GOOGLE_AI_KEY) || !string.IsNullOrEmpty(s.GOOGLE_OAUTH_ACCESS_TOKEN),
                "Groq" => !string.IsNullOrEmpty(s.GROQ_KEY),
                "OpenAI" => !string.IsNullOrEmpty(s.OPENAI_KEY),
                "Anthropic" => !string.IsNullOrEmpty(s.ANTHROPIC_KEY),
                "ClaudeCode" => IsClaudeCodeAvailable(),
                "DeepSeek" => !string.IsNullOrEmpty(s.CUSTOM_LLM_KEY),
                "X-AI" => !string.IsNullOrEmpty(s.CUSTOM_LLM_KEY),
                "Mistral" => !string.IsNullOrEmpty(s.MISTRAL_KEY),
                "OpenRouter" => !string.IsNullOrEmpty(s.OPENROUTER_KEY),
                "Perplexity" => !string.IsNullOrEmpty(s.PERPLEXITY_KEY),
                "Lemonade" => !string.IsNullOrEmpty(s.LEMONADE_ENDPOINT),
                _ => true
            };
        }

        private static async Task<string> CallBackendInternalAsync(string backend, string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            return backend switch {
                "Gemini"     => await AskGeminiAsync(prompt, history, ct),
                "OpenAI"     => await AskOpenAIAsync(prompt, history, ct),
                "DeepSeek"   => await AskDeepSeekAsync(prompt, history, ct),
                "Anthropic"  => await AskAnthropicAsync(prompt, history, ct),
                "ClaudeCode" => await AskClaudeCodeAsync(prompt, history, ct),
                "Groq"       => await AskGroqAsync(prompt, history, ct),
                "X-AI"       => await AskGrokAsync(prompt, history, ct),
                "Mistral"    => await AskMistralAsync(prompt, history, ct),
                "OpenRouter" => await AskOpenRouterAsync(prompt, history, ct),
                "Perplexity" => await AskPerplexityAsync(prompt, history, ct),
                "Lemonade"   => await AskLemonadeAsync(prompt, history, ct),
                "Ollama"     => await AskOllamaAsync(prompt, history, ct),
                _            => await AskGeminiAsync(prompt, history, ct)
            };
        }

        public static async Task<string> AskGeminiAsync(string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            var s = CoreRegistry.Data.Settings.Current;
            var keys = (s.GOOGLE_AI_KEY ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).Where(k => k.Length > 0).ToList();
            // Easy setup: with NO API key, fall back to the connected Google account's OAuth token
            // (the cloud-platform scope lets us call the Gemini API with a Bearer token). So a user
            // who just clicks "Connect Google" gets working Gemini with nothing to paste.
            string oauthToken = keys.Count == 0 ? await OAuth2Manager.GetValidAccessTokenAsync() : "";
            if (keys.Count == 0 && string.IsNullOrEmpty(oauthToken))
                throw new Exception("No Gemini credential — add a Google AI key or connect a Google account.");

            string model = string.IsNullOrWhiteSpace(s.GEMINI_MODEL) ? "gemini-flash-latest" : s.GEMINI_MODEL.Trim();
            if (model.StartsWith("models/")) model = model.Substring("models/".Length);
            // Google keeps RETIRING versioned flash models (1.5, 2.0, ...). Steer old configs to the
            // always-current alias so they never 404 again. gemini-2.5/3.x are kept as-is.
            if (model.Contains("1.5") || model.Contains("1.0") || model.Contains("2.0") || model == "gemini-pro")
                model = "gemini-flash-latest";

            var contents = new List<object>();
            string lastRole = ""; string lastText = "";
            // Gemini REQUIRES the first content to be role 'user'. Drop any leading assistant/greeting
            // turns — this is why chat (with history) 400s while a bare test prompt (no history) works.
            var trimmedHistory = history?.SkipWhile(t => t.Role == "model" || t.Role == "assistant").ToList();
            if (trimmedHistory != null) {
                foreach (var t in trimmedHistory) {
                    string currentRole = (t.Role == "model" || t.Role == "assistant" ? "model" : "user");
                    if (currentRole == lastRole) lastText += "\n" + (t.Text ?? "");
                    else { if (!string.IsNullOrEmpty(lastRole)) contents.Add(new { role = lastRole, parts = new[] { new { text = lastText } } }); lastRole = currentRole; lastText = t.Text ?? ""; }
                }
            }
            if (lastRole == "user") { lastText += "\n" + prompt; contents.Add(new { role = "user", parts = new[] { new { text = lastText } } }); }
            else { if (!string.IsNullOrEmpty(lastRole)) contents.Add(new { role = lastRole, parts = new[] { new { text = lastText } } }); contents.Add(new { role = "user", parts = new[] { new { text = prompt } } }); }

            var payload = new { systemInstruction = new { parts = new[] { new { text = AiAPI.GetCompactSystemPrompt() } } }, contents, generationConfig = new { temperature = 0.7, maxOutputTokens = 4096 } };
            string json = JsonSerializer.Serialize(payload);

            // Build attempts: API-key query param when keys exist, else OAuth Bearer (no ?key=).
            var attempts = new List<(string url, string? bearer)>();
            if (keys.Count > 0) {
                foreach (var key in keys) {
                    attempts.Add(($"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={key}", null));
                    attempts.Add(($"https://generativelanguage.googleapis.com/v1/models/{model}:generateContent?key={key}", null));
                }
            } else {
                attempts.Add(($"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent", oauthToken));
                attempts.Add(($"https://generativelanguage.googleapis.com/v1/models/{model}:generateContent", oauthToken));
            }

            string lastError = "";
            foreach (var (url, bearer) in attempts) {
                try {
                    using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                    if (!string.IsNullOrEmpty(bearer)) req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearer);
                    using var resp = await _http.SendAsync(req, ct);
                    string body = await resp.Content.ReadAsStringAsync();
                    if (resp.IsSuccessStatusCode) { using var doc = JsonDocument.Parse(body); return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? ""; }
                    lastError = body;
                } catch (Exception ex) { lastError = ex.Message; }
            }
            throw new Exception($"Gemini Error: {lastError}");
        }

        public static async Task<string> AskGeminiWithAudioAsync(byte[] audioBytes, string prompt, CancellationToken ct = default)
        {
            var s = CoreRegistry.Data.Settings.Current;
            string rawKeys = s.GOOGLE_AI_KEY;
            if (string.IsNullOrEmpty(rawKeys)) throw new Exception("API Key is empty.");
            var keys = rawKeys.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).ToList();

            string model = "gemini-flash-latest";
            string b64Data = Convert.ToBase64String(audioBytes);
            var payload = new { systemInstruction = new { parts = new[] { new { text = AiAPI.GetCompactSystemPrompt() } } }, contents = new[] { new { role = "user", parts = new object[] { new { inlineData = new { mimeType = "audio/mp3", data = b64Data } }, new { text = prompt } } } }, generationConfig = new { temperature = 0.4, maxOutputTokens = 4096 } };
            string json = JsonSerializer.Serialize(payload);
            string lastError = "";
            foreach (var key in keys) {
                string[] endpoints = { $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={key}", $"https://generativelanguage.googleapis.com/v1/models/{model}:generateContent?key={key}" };
                foreach (var url in endpoints) {
                    try {
                        using var resp = await _http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"), ct);
                        string body = await resp.Content.ReadAsStringAsync();
                        if (resp.IsSuccessStatusCode) { using var doc = JsonDocument.Parse(body); return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? ""; }
                        lastError = body;
                    } catch (Exception ex) { lastError = ex.Message; }
                }
            }
            throw new Exception($"Gemini Audio Error: {lastError}");
        }

        public static async Task<string> AskPerplexityAsync(string p, List<ChatTurn>? h, CancellationToken ct)
            => await AskGenericOpenAICompatibleAsync("https://api.perplexity.ai", CoreRegistry.Data.Settings.Current.PERPLEXITY_KEY, CoreRegistry.Data.Settings.Current.PERPLEXITY_MODEL, p, h, ct);

        public static async Task<string> AskLemonadeAsync(string p, List<ChatTurn>? h, CancellationToken ct)
            => await AskGenericOpenAICompatibleAsync(CoreRegistry.Data.Settings.Current.LEMONADE_ENDPOINT, "", CoreRegistry.Data.Settings.Current.LEMONADE_MODEL, p, h, ct);

        public static async Task<string> AskOllamaAsync(string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            var s = CoreRegistry.Data.Settings.Current;
            var msgs = new List<object>();
            if (history != null) { foreach (var t in history) msgs.Add(new { role = (t.Role == "model" ? "assistant" : "user"), content = t.Text ?? "" }); }
            msgs.Add(new { role = "user", content = prompt });
            var payload = new { model = s.OLLAMA_MODEL, messages = msgs, stream = false };
            var resp = await _http.PostAsync($"{s.OLLAMA_ENDPOINT.TrimEnd('/')}/api/chat", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);
            string body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) throw new Exception($"Ollama: {body}");
            using var doc = JsonDocument.Parse(body); return doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
        }

        public static async Task<string> AskOllamaStreamAsync(string prompt, List<ChatTurn>? history, Action<string> onToken, CancellationToken ct)
        {
            var s = CoreRegistry.Data.Settings.Current;
            var msgs = new List<object>();
            if (history != null) { foreach (var t in history) msgs.Add(new { role = (t.Role == "model" ? "assistant" : "user"), content = t.Text ?? "" }); }
            msgs.Add(new { role = "user", content = prompt });
            var payload = new { model = s.OLLAMA_MODEL, messages = msgs, stream = true };
            var req = new HttpRequestMessage(HttpMethod.Post, $"{s.OLLAMA_ENDPOINT.TrimEnd('/')}/api/chat") { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") };
            var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            using var stream = await resp.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            var sb = new StringBuilder(); string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null) {
                if (string.IsNullOrEmpty(line)) continue;
                try {
                    using var doc = JsonDocument.Parse(line);
                    if (doc.RootElement.TryGetProperty("message", out var msg)) { string t = msg.GetProperty("content").GetString() ?? ""; sb.Append(t); onToken(t); }
                    if (doc.RootElement.TryGetProperty("done", out var done) && done.GetBoolean()) break;
                } catch { }
            }
            return sb.ToString();
        }


        private static async Task<string> AskGenericOpenAICompatibleAsync(string baseUrl, string key, string model, string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) throw new Exception("Base URL empty.");
            var msgs = new List<object> { new { role = "system", content = AiAPI.GetCompactSystemPrompt() } };
            if (history != null) { foreach (var t in history) msgs.Add(new { role = (t.Role == "model" ? "assistant" : "user"), content = t.Text ?? "" }); }
            msgs.Add(new { role = "user", content = prompt });
            var payload = new { model, messages = msgs, temperature = 0.5 };
            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions") { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") };
            if (!string.IsNullOrEmpty(key)) req.Headers.Add("Authorization", $"Bearer {key}");
            var resp = await _http.SendAsync(req, ct);
            string body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) throw new Exception($"{resp.StatusCode}: {body}");
            using var doc = JsonDocument.Parse(body); return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        }

        public static async Task<string> AskOpenAIAsync(string p, List<ChatTurn>? h, CancellationToken ct)
            => await AskGenericOpenAICompatibleAsync(CoreRegistry.Data.Settings.Current.OPENAI_BASE_URL, CoreRegistry.Data.Settings.Current.OPENAI_KEY, CoreRegistry.Data.Settings.Current.OPENAI_MODEL, p, h, ct);

        public static async Task<string> AskAnthropicAsync(string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            var s = CoreRegistry.Data.Settings.Current;
            var msgs = new List<object>();
            if (history != null) { foreach (var t in history) msgs.Add(new { role = (t.Role == "model" ? "assistant" : "user"), content = t.Text ?? "" }); }
            msgs.Add(new { role = "user", content = prompt });
            var payload = new { model = "claude-3-5-sonnet-latest", system = AiAPI.GetCompactSystemPrompt(), messages = msgs, max_tokens = 1024 };
            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages") { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") };
            req.Headers.Add("x-api-key", s.ANTHROPIC_KEY); req.Headers.Add("anthropic-version", "2023-06-01");
            var resp = await _http.SendAsync(req, ct);
            string body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) throw new Exception($"Anthropic: {body}");
            using var doc = JsonDocument.Parse(body); return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
        }

        public static async Task<string> AskMistralAsync(string p, List<ChatTurn>? h, CancellationToken ct)
            => await AskGenericOpenAICompatibleAsync("https://api.mistral.ai/v1", CoreRegistry.Data.Settings.Current.MISTRAL_KEY, "mistral-large-latest", p, h, ct);

        public static async Task<string> AskOpenRouterAsync(string p, List<ChatTurn>? h, CancellationToken ct)
            => await AskGenericOpenAICompatibleAsync("https://openrouter.ai/api/v1", CoreRegistry.Data.Settings.Current.OPENROUTER_KEY, CoreRegistry.Data.Settings.Current.OPENROUTER_MODEL, p, h, ct);

        public static async Task<string> AskDeepSeekAsync(string p, List<ChatTurn>? h, CancellationToken ct)
            => await AskGenericOpenAICompatibleAsync("https://api.deepseek.com", CoreRegistry.Data.Settings.Current.CUSTOM_LLM_KEY, "deepseek-chat", p, h, ct);

        public static async Task<string> AskGrokAsync(string p, List<ChatTurn>? h, CancellationToken ct)
            => await AskGenericOpenAICompatibleAsync("https://api.x.ai/v1", CoreRegistry.Data.Settings.Current.CUSTOM_LLM_KEY, "grok-beta", p, h, ct);

        public static async Task<string> AskGroqAsync(string p, List<ChatTurn>? h, CancellationToken ct)
        {
            var models = new[] { "llama-3.3-70b-versatile", "mixtral-8x7b-32768" };
            foreach (var m in models) { try { return await AskGenericOpenAICompatibleAsync("https://api.groq.com/openai/v1", CoreRegistry.Data.Settings.Current.GROQ_KEY, m, p, h, ct); } catch { } }
            throw new Exception("Groq failed.");
        }

        // === Streaming (token-by-token) for OpenAI-compatible providers + Ollama ===
        private static readonly HttpClient _streamHttp = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        public static bool IsStreamingBackend(string b) => b switch
        {
            "Ollama" or "OpenAI" or "Groq" or "Mistral" or "OpenRouter" or "DeepSeek" or "X-AI" or "Perplexity" => true,
            _ => false
        };

        /// <summary>Streams tokens via onToken as they arrive; returns the full accumulated text.
        /// Falls back to a single non-streamed emit for backends without a stream path.</summary>
        public static async Task<string> AskStreamAsync(string prompt, List<ChatTurn>? history, Action<string> onToken, CancellationToken ct = default)
        {
            var s = CoreRegistry.Data.Settings.Current;
            switch (s.LLM_BACKEND)
            {
                case "Ollama":     return await AskOllamaStreamAsync(prompt, history, onToken, ct);
                case "OpenAI":     return await AskGenericStreamAsync(s.OPENAI_BASE_URL, s.OPENAI_KEY, s.OPENAI_MODEL, prompt, history, onToken, ct);
                case "Groq":       return await AskGenericStreamAsync("https://api.groq.com/openai/v1", s.GROQ_KEY, string.IsNullOrWhiteSpace(s.GROQ_MODEL) ? "llama-3.3-70b-versatile" : s.GROQ_MODEL, prompt, history, onToken, ct);
                case "OpenRouter": return await AskGenericStreamAsync("https://openrouter.ai/api/v1", s.OPENROUTER_KEY, s.OPENROUTER_MODEL, prompt, history, onToken, ct);
                case "Mistral":    return await AskGenericStreamAsync("https://api.mistral.ai/v1", s.MISTRAL_KEY, string.IsNullOrWhiteSpace(s.MISTRAL_MODEL) ? "mistral-large-latest" : s.MISTRAL_MODEL, prompt, history, onToken, ct);
                case "DeepSeek":   return await AskGenericStreamAsync("https://api.deepseek.com", s.CUSTOM_LLM_KEY, "deepseek-chat", prompt, history, onToken, ct);
                case "X-AI":       return await AskGenericStreamAsync("https://api.x.ai/v1", s.CUSTOM_LLM_KEY, "grok-beta", prompt, history, onToken, ct);
                case "Perplexity": return await AskGenericStreamAsync("https://api.perplexity.ai", s.PERPLEXITY_KEY, string.IsNullOrWhiteSpace(s.PERPLEXITY_MODEL) ? "sonar" : s.PERPLEXITY_MODEL, prompt, history, onToken, ct);
                default:
                    string full = await AskAsync(prompt, history, ct);
                    onToken(full);
                    return full;
            }
        }

        private static async Task<string> AskGenericStreamAsync(string baseUrl, string key, string model, string prompt, List<ChatTurn>? history, Action<string> onToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) throw new Exception("Base URL empty.");

            var msgs = new List<object> { new { role = "system", content = AiAPI.GetCompactSystemPrompt() } };
            if (history != null) foreach (var t in history) msgs.Add(new { role = (t.Role == "model" ? "assistant" : "user"), content = t.Text ?? "" });
            msgs.Add(new { role = "user", content = prompt });

            var payload = new { model, messages = msgs, stream = true };
            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions")
            { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") };
            if (!string.IsNullOrEmpty(key)) req.Headers.Add("Authorization", $"Bearer {key}");

            using var resp = await _streamHttp.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"{resp.StatusCode}: {await resp.Content.ReadAsStringAsync(ct)}");

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);
            var sb = new StringBuilder();
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                if (ct.IsCancellationRequested) break;
                if (!line.StartsWith("data:")) continue;
                string data = line.Substring(5).Trim();
                if (data == "[DONE]") break;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var choices = doc.RootElement.GetProperty("choices");
                    if (choices.GetArrayLength() == 0) continue;
                    var delta = choices[0].GetProperty("delta");
                    if (delta.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                    {
                        string tok = c.GetString() ?? "";
                        if (tok.Length > 0) { sb.Append(tok); onToken(tok); }
                    }
                }
                catch { }
            }
            return sb.ToString();
        }

        // === Claude Code (Claude Max/Pro subscription via the headless `claude` CLI) ===
        // This does NOT use api.anthropic.com or ANTHROPIC_KEY (that is separate pay-per-token
        // billing). It shells out to the Claude Code CLI, which is authenticated by the user's
        // Max/Pro login (`claude` -> /login). Install: npm i -g @anthropic-ai/claude-code.
        public static string? ResolveClaudeCli()
        {
            var s = CoreRegistry.Data.Settings.Current;
            if (!string.IsNullOrWhiteSpace(s.CLAUDE_CLI_PATH) && File.Exists(s.CLAUDE_CLI_PATH))
                return s.CLAUDE_CLI_PATH;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var candidates = new[]
            {
                Path.Combine(appData, "npm", "claude.cmd"),
                Path.Combine(appData, "npm", "claude.exe"),
                Path.Combine(home, ".local", "bin", "claude.exe"),
                Path.Combine(home, ".local", "bin", "claude"),
                Path.Combine(home, ".claude", "local", "claude.exe"),
            };
            foreach (var c in candidates) if (File.Exists(c)) return c;

            // Fall back to PATH resolution via `where`.
            try
            {
                var psi = new ProcessStartInfo("where", "claude")
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);
                    var first = outp.Split('\n').Select(l => l.Trim())
                        .FirstOrDefault(l => l.EndsWith(".cmd") || l.EndsWith(".exe"));
                    if (!string.IsNullOrEmpty(first) && File.Exists(first)) return first;
                }
            }
            catch { }
            return null;
        }

        public static bool IsClaudeCodeAvailable() => ResolveClaudeCli() != null;

        public static async Task<string> AskClaudeCodeAsync(string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            string? cli = ResolveClaudeCli();
            if (cli == null)
                throw new Exception("Claude Code CLI not found. Install it (npm i -g @anthropic-ai/claude-code), run `claude` once to log in with your Max subscription, then set it as the backend.");

            var s = CoreRegistry.Data.Settings.Current;

            // `claude -p` is one-shot, so fold history + system prompt into a single prompt piped via stdin
            // (stdin avoids Windows command-line length limits).
            var sb = new StringBuilder();
            sb.AppendLine(AiAPI.GetCompactSystemPrompt());
            if (history != null)
                foreach (var t in history)
                    sb.AppendLine($"{(t.Role == "model" ? "Assistant" : "User")}: {t.Text}");
            sb.AppendLine($"User: {prompt}");

            var psi = new ProcessStartInfo
            {
                FileName = cli,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            psi.ArgumentList.Add("-p");
            psi.ArgumentList.Add("--output-format");
            psi.ArgumentList.Add("json");
            if (!string.IsNullOrWhiteSpace(s.CLAUDE_CODE_MODEL))
            {
                psi.ArgumentList.Add("--model");
                psi.ArgumentList.Add(s.CLAUDE_CODE_MODEL);
            }

            using var proc = Process.Start(psi)
                ?? throw new Exception("Failed to start Claude CLI process.");
            await proc.StandardInput.WriteAsync(sb.ToString());
            proc.StandardInput.Close();

            string outText = await proc.StandardOutput.ReadToEndAsync();
            string errText = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0)
                throw new Exception($"Claude CLI exit {proc.ExitCode}: {(string.IsNullOrWhiteSpace(errText) ? outText : errText)}");

            // --output-format json => { "type":"result", "subtype":"success", "result":"...", ... }
            try
            {
                using var doc = JsonDocument.Parse(outText);
                if (doc.RootElement.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.String)
                    return r.GetString() ?? "";
            }
            catch { /* not JSON — return raw */ }
            return outText.Trim();
        }

        public static async Task<List<string>> GetOllamaModelsAsync()
        {
            try {
                var resp = await _http.GetAsync($"{CoreRegistry.Data.Settings.Current.OLLAMA_ENDPOINT.TrimEnd('/')}/api/tags");
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                return doc.RootElement.GetProperty("models").EnumerateArray().Select(m => m.GetProperty("name").GetString() ?? "").ToList();
            } catch { return new List<string>(); }
        }
    }
}
