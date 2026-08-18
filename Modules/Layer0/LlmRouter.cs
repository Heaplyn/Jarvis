// Developer: heaplyn
// Date: 2026-08-18
// Summary: Central LLM dispatcher with EXHAUSTIVE Failover.
//          Unified gateway for all AI requests including streaming and diagnostics.

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

namespace JarvisLauncher
{
    public static class LlmRouter
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };

        public static async Task<bool> IsOllamaAvailableAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                string endpoint = CoreRegistry.Data.Settings.Current.OLLAMA_ENDPOINT.TrimEnd('/');
                var resp = await _http.GetAsync($"{endpoint}/api/tags", cts.Token);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<string> AskAsync(string prompt, List<ChatTurn>? history = null, CancellationToken ct = default)
        {
            var timer = Stopwatch.StartNew();
            DebugConsoleOverlay.Log("AI-Router", ">>> Starting GLOBAL Failover sequence.");

            try {
                string? local = await HeuristicIntentParser.TryHandleLocallyAsync(prompt);
                if (local != null) return local;
            } catch { }

            var s = CoreRegistry.Data.Settings.Current;
            string primary = s.LLM_BACKEND;

            // 1. Prioritized chain: User primary -> Trusted Cloud -> Local -> Heuristic Fallback
            var chain = new List<string> {
                primary, "Gemini", "Groq", "OpenAI", "Anthropic",
                "Mistral", "OpenRouter", "Perplexity", "DeepSeek",
                "OpenClaw", "Ollama", "Godellian"
            }.Distinct().ToList();

            string lastError = "No providers attempted.";
            foreach (var backend in chain)
            {
                try
                {
                    if (!IsBackendConfigured(backend)) continue;

                    DebugConsoleOverlay.Log("AI-Router", $"Attempting: {backend}");
                    string result = await CallBackendInternalAsync(backend, prompt, history, ct);

                    if (!string.IsNullOrEmpty(result) && !result.Contains("Error") && !result.Contains("fail")) {
                        DebugConsoleOverlay.Log("AI-Router", $"Success via {backend} ({timer.ElapsedMilliseconds}ms)");
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    DebugConsoleOverlay.Log("AI-Fail", $"[{backend}] Failed: {ex.Message}");
                }
            }

            return $"⚠️ [PIPELINE CRASH] All AI models failed.\nLast Error: {lastError}";
        }

        private static bool IsBackendConfigured(string b) {
            var s = CoreRegistry.Data.Settings.Current;
            return b switch {
                "Gemini" => !string.IsNullOrEmpty(s.GOOGLE_AI_KEY) || !string.IsNullOrEmpty(s.GOOGLE_OAUTH_ACCESS_TOKEN),
                "Groq" => !string.IsNullOrEmpty(s.GROQ_KEY),
                "OpenAI" => !string.IsNullOrEmpty(s.OPENAI_KEY),
                "Anthropic" => !string.IsNullOrEmpty(s.ANTHROPIC_KEY),
                "Ollama" => true,
                "Godellian" => true,
                _ => true
            };
        }

        private static async Task<string> CallBackendInternalAsync(string backend, string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            return backend switch
            {
                "OpenAI"     => await AskOpenAIAsync(prompt, history, ct),
                "DeepSeek"   => await AskDeepSeekAsync(prompt, history, ct),
                "Anthropic"  => await AskAnthropicAsync(prompt, history, ct),
                "Groq"       => await AskGroqAsync(prompt, history, ct),
                "Perplexity" => await AskPerplexityAsync(prompt, history, ct),
                "Mistral"    => await AskMistralAsync(prompt, history, ct),
                "OpenRouter" => await AskOpenRouterAsync(prompt, history, ct),
                "OpenClaw"   => await AskOpenClawAsync(prompt, history, ct),
                "Ollama"     => await AskOllamaAsync(prompt, history, ct),
                "Godellian"  => await AskGodellianAsync(prompt),
                _            => await AiAPI.AskGeminiInternalStatic(prompt, history, ct)
            };
        }

        private static async Task<string> AskGodellianAsync(string prompt)
        {
            DebugConsoleOverlay.Log("Godellian", "Last-Resort Symbolic Logic active.");
            double[] v = new double[16];
            for (int i = 0; i < Math.Min(prompt.Length, 100); i++) v[i % 16] += (double)prompt[i] / 255.0;
            var brain = CoreRegistry.Intelligence.MainBrain;
            return brain.ThinkInWords(v);
        }

        public static async Task<string> AskDeepSeekAsync(string p, List<ChatTurn>? h, CancellationToken ct)
            => await AskGenericOpenAICompatibleAsync("https://api.deepseek.com", CoreRegistry.Data.Settings.Current.CUSTOM_LLM_KEY, "deepseek-chat", p, h, ct);

        public static async Task<string> AskGroqAsync(string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            var s = CoreRegistry.Data.Settings.Current;
            var models = new[] { "llama-3.3-70b-versatile", "mixtral-8x7b-32768", "llama-3.1-8b-instant" };
            foreach (var m in models) {
                try { return await AskGenericOpenAICompatibleAsync("https://api.groq.com/openai/v1", s.GROQ_KEY, m, prompt, history, ct); } catch { }
            }
            throw new Exception("Groq failed.");
        }

        private static async Task<string> AskGenericOpenAICompatibleAsync(string baseUrl, string key, string model, string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            var msgs = new List<object> { new { role = "system", content = AiAPI.GetCompactSystemPrompt() } };
            if (history != null) foreach (var t in history.TakeLast(3)) msgs.Add(new { role = t.Role == "model" ? "assistant" : "user", content = t.Text });
            msgs.Add(new { role = "user", content = prompt });

            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions") {
                Content = new StringContent(JsonSerializer.Serialize(new { model, messages = msgs, temperature = 0.5 }), Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrEmpty(key)) req.Headers.Add("Authorization", $"Bearer {key}");

            var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) throw new Exception($"HTTP {(int)resp.StatusCode}");
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        }

        public static async Task<string> AskOllamaStreamAsync(string prompt, List<ChatTurn>? history, Action<string> onToken, CancellationToken ct)
        {
            var s = CoreRegistry.Data.Settings.Current;
            var payload = new { model = s.OLLAMA_MODEL, messages = history?.Select(t => new { role = t.Role == "model" ? "assistant" : "user", content = t.Text }).ToList(), stream = true };
            var req = new HttpRequestMessage(HttpMethod.Post, $"{s.OLLAMA_ENDPOINT.TrimEnd('/')}/api/chat") {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            using var stream = await resp.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            var sb = new StringBuilder();
            while (!reader.EndOfStream) {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line)) continue;
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("message", out var msg)) {
                    string t = msg.GetProperty("content").GetString() ?? "";
                    sb.Append(t); onToken(t);
                }
                if (doc.RootElement.TryGetProperty("done", out var done) && done.GetBoolean()) break;
            }
            return sb.ToString();
        }

        public static async Task<string> AskOpenAIAsync(string p, List<ChatTurn>? h, CancellationToken ct)
            => await AskGenericOpenAICompatibleAsync(CoreRegistry.Data.Settings.Current.OPENAI_BASE_URL, CoreRegistry.Data.Settings.Current.OPENAI_KEY, CoreRegistry.Data.Settings.Current.OPENAI_MODEL, p, h, ct);

        public static async Task<string> AskMistralAsync(string p, List<ChatTurn>? h, CancellationToken ct)
            => await AskGenericOpenAICompatibleAsync("https://api.mistral.ai/v1", CoreRegistry.Data.Settings.Current.MISTRAL_KEY, "mistral-large-latest", p, h, ct);

        public static async Task<string> AskOpenRouterAsync(string p, List<ChatTurn>? h, CancellationToken ct)
            => await AskGenericOpenAICompatibleAsync("https://openrouter.ai/api/v1", CoreRegistry.Data.Settings.Current.OPENROUTER_KEY, CoreRegistry.Data.Settings.Current.OPENROUTER_MODEL, p, h, ct);

        public static async Task<string> AskPerplexityAsync(string p, List<ChatTurn>? h, CancellationToken ct)
            => await AskGenericOpenAICompatibleAsync("https://api.perplexity.ai", CoreRegistry.Data.Settings.Current.PERPLEXITY_KEY, "llama-3-sonar-large-32k-online", p, h, ct);

        public static async Task<string> AskAnthropicAsync(string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            var s = CoreRegistry.Data.Settings.Current;
            var msgs = new List<object>();
            if (history != null) foreach (var t in history.TakeLast(3)) msgs.Add(new { role = t.Role == "model" ? "assistant" : "user", content = t.Text });
            msgs.Add(new { role = "user", content = prompt });
            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages") {
                Content = new StringContent(JsonSerializer.Serialize(new { model = "claude-3-5-sonnet-20240620", system = AiAPI.GetCompactSystemPrompt(), messages = msgs, max_tokens = 1000 }), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("x-api-key", s.ANTHROPIC_KEY);
            req.Headers.Add("anthropic-version", "2023-06-01");
            var resp = await _http.SendAsync(req, ct);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
        }

        public static async Task<string> AskOllamaAsync(string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            var s = CoreRegistry.Data.Settings.Current;
            var payload = new { model = s.OLLAMA_MODEL, messages = history?.Select(t => new { role = t.Role == "model" ? "assistant" : "user", content = t.Text }).ToList(), stream = false };
            var resp = await _http.PostAsync($"{s.OLLAMA_ENDPOINT.TrimEnd('/')}/api/chat", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
        }

        public static async Task<string> AskOpenClawAsync(string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            var s = CoreRegistry.Data.Settings.Current;
            var resp = await _http.PostAsync($"{s.OPENCLAW_ENDPOINT.TrimEnd('/')}/sessions", null, ct);
            var sid = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement.GetProperty("session_id").GetString()!;
            await _http.PostAsync($"{s.OPENCLAW_ENDPOINT}/sessions/{sid}/message", new StringContent(JsonSerializer.Serialize(new { message = prompt }), Encoding.UTF8, "application/json"), ct);
            await Task.Delay(1500);
            var res = await _http.GetAsync($"{s.OPENCLAW_ENDPOINT}/sessions/{sid}", ct);
            return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("session").GetProperty("messages").EnumerateArray().Last().GetProperty("content").GetProperty("text").GetString()!;
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
