// Developer: heaplyn
// Date: 2026-08-18
// Summary: Central LLM dispatcher with EXHAUSTIVE Failover.
//          Supports: Gemini, Groq, OpenAI, Anthropic, Mistral, OpenRouter, DeepSeek, X-AI (Grok), Ollama.
//          Enhanced error parsing and robust JSON navigation.

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
            DebugConsoleOverlay.Log("AI-Router", ">>> GLOBAL Failover active.");

            // SPECIAL CASE: History retrieval
            string lowerPrompt = prompt.ToLower();
            if (lowerPrompt.Contains("yesterday") || lowerPrompt.Contains("history") || lowerPrompt.Contains("did i do")) {
                string historyContext = ChronoLogManager.GetHistoryForDate(DateTime.Now.AddDays(-1));
                prompt = $"[SYSTEM CONTEXT: USER HISTORY LOGS]\n{historyContext}\n\n### USER REQUEST\n{prompt}";
            }

            try {
                string? local = await HeuristicIntentParser.TryHandleLocallyAsync(prompt);
                if (local != null) return local;
            } catch { }

            var s = CoreRegistry.Data.Settings.Current;
            var chain = new List<string> {
                s.LLM_BACKEND, "Gemini", "Groq", "DeepSeek", "OpenAI", "Anthropic",
                "Mistral", "OpenRouter", "X-AI", "Ollama", "Godellian"
            }.Distinct().ToList();

            string lastError = "No providers attempted.";
            foreach (var backend in chain)
            {
                try {
                    if (!IsBackendConfigured(backend)) continue;
                    DebugConsoleOverlay.Log("AI-Router", $"Attempting: {backend}");
                    string result = await CallBackendInternalAsync(backend, prompt, history, ct);

                    if (!string.IsNullOrEmpty(result) && !result.StartsWith("⚠️")) {
                        DebugConsoleOverlay.Log("AI-Router", $"Success: {backend} ({timer.ElapsedMilliseconds}ms)");
                        return result;
                    }
                } catch (Exception ex) {
                    lastError = ex.Message;
                    DebugConsoleOverlay.Log("AI-Fail", $"[{backend}]: {ex.Message}");
                }
            }
            return $"⚠️ [FATAL] AI Pipeline collapsed. Last Error: {lastError}";
        }

        private static bool IsBackendConfigured(string b) {
            var s = CoreRegistry.Data.Settings.Current;
            return b switch {
                "Gemini" => !string.IsNullOrEmpty(s.GOOGLE_AI_KEY) || !string.IsNullOrEmpty(s.GOOGLE_OAUTH_ACCESS_TOKEN),
                "Groq" => !string.IsNullOrEmpty(s.GROQ_KEY),
                "OpenAI" => !string.IsNullOrEmpty(s.OPENAI_KEY),
                "Anthropic" => !string.IsNullOrEmpty(s.ANTHROPIC_KEY),
                "DeepSeek" => !string.IsNullOrEmpty(s.CUSTOM_LLM_KEY),
                "X-AI" => !string.IsNullOrEmpty(s.CUSTOM_LLM_KEY),
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
                "Groq"       => await AskGroqAsync(prompt, history, ct),
                "X-AI"       => await AskGrokAsync(prompt, history, ct),
                "Mistral"    => await AskMistralAsync(prompt, history, ct),
                "OpenRouter" => await AskOpenRouterAsync(prompt, history, ct),
                "Ollama"     => await AskOllamaAsync(prompt, history, ct),
                "LM Studio"  => await AskLMStudioAsync(prompt, history, ct),
                "Bionic"     => await AskBionicAsync(prompt, history, ct),
                "Godellian"  => await AskGodellianAsync(prompt),
                _            => await AskGeminiAsync(prompt, history, ct)
            };
        }

        public static async Task<string> AskLMStudioAsync(string p, List<ChatTurn>? h, CancellationToken ct)
            => await AskGenericOpenAICompatibleAsync("http://localhost:1234/v1", "", "local-model", p, h, ct);

        public static async Task<string> AskBionicAsync(string p, List<ChatTurn>? h, CancellationToken ct)
            => await AskGenericOpenAICompatibleAsync("http://localhost:18080/v1", "", "bionic-model", p, h, ct);

        public static async Task<string> AskGeminiAsync(string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            var s = CoreRegistry.Data.Settings.Current;
            string key = s.GOOGLE_AI_KEY;
            if (string.IsNullOrEmpty(key)) throw new Exception("Gemini API Key missing.");

            string model = s.GEMINI_MODEL ?? "gemini-1.5-flash";
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={key}";

            var contents = new List<object>();
            if (history != null) {
                foreach (var t in history.TakeLast(10))
                    contents.Add(new { role = (t.Role == "model" ? "model" : "user"), parts = new[] { new { text = t.Text } } });
            }
            contents.Add(new { role = "user", parts = new[] { new { text = prompt } } });

            var payload = new { contents, generationConfig = new { temperature = 0.7, maxOutputTokens = 2048 } };
            var json = JsonSerializer.Serialize(payload);

            DebugConsoleOverlay.Log("AI-Gemini", $"Querying {model}...");

            using var resp = await _http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"), ct);
            string body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode) throw new Exception($"Gemini Error {resp.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
        }

        private static async Task<string> AskGodellianAsync(string prompt) {
            double[] v = NeuralVectorizationKernels.VectorizeSystemState(prompt, "", "");
            return CoreRegistry.Intelligence.MainBrain.ThinkInWords(v);
        }

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

        private static async Task<string> AskGenericOpenAICompatibleAsync(string baseUrl, string key, string model, string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            var msgs = new List<object> { new { role = "system", content = AiAPI.GetCompactSystemPrompt() } };
            if (history != null) {
                foreach (var t in history.TakeLast(5))
                    msgs.Add(new { role = (t.Role == "model" ? "assistant" : "user"), content = t.Text ?? "" });
            }
            msgs.Add(new { role = "user", content = prompt });

            var payload = new { model, messages = msgs, temperature = 0.5 };
            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions") {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrEmpty(key)) req.Headers.Add("Authorization", $"Bearer {key}");
            var resp = await _http.SendAsync(req, ct);
            string body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) throw new Exception($"{resp.StatusCode}");
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        }

        public static async Task<string> AskAnthropicAsync(string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            var s = CoreRegistry.Data.Settings.Current;
            var msgs = new List<object>();
            if (history != null) {
                foreach (var t in history.TakeLast(3))
                    msgs.Add(new { role = (t.Role == "model" ? "assistant" : "user"), content = t.Text ?? "" });
            }
            msgs.Add(new { role = "user", content = prompt });
            var payload = new { model = "claude-3-5-sonnet-latest", system = AiAPI.GetCompactSystemPrompt(), messages = msgs, max_tokens = 1024 };
            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages") {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("x-api-key", s.ANTHROPIC_KEY);
            req.Headers.Add("anthropic-version", "2023-06-01");
            var resp = await _http.SendAsync(req, ct);
            string body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) throw new Exception($"Anthropic {resp.StatusCode}: {body}");
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
        }

        public static async Task<string> AskOllamaAsync(string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            var s = CoreRegistry.Data.Settings.Current;
            var msgs = new List<object>();
            if (history != null) {
                foreach (var t in history.TakeLast(10))
                    msgs.Add(new { role = (t.Role == "model" ? "assistant" : "user"), content = t.Text ?? "" });
            }
            msgs.Add(new { role = "user", content = prompt });

            var payload = new { model = s.OLLAMA_MODEL, messages = msgs, stream = false };
            var resp = await _http.PostAsync($"{s.OLLAMA_ENDPOINT.TrimEnd('/')}/api/chat", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);
            string body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) throw new Exception($"Ollama {resp.StatusCode}: {body}");
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
        }

        public static async Task<string> AskOllamaStreamAsync(string prompt, List<ChatTurn>? history, Action<string> onToken, CancellationToken ct)
        {
            var s = CoreRegistry.Data.Settings.Current;
            var msgs = new List<object>();
            if (history != null) {
                foreach (var t in history)
                    msgs.Add(new { role = (t.Role == "model" ? "assistant" : "user"), content = t.Text ?? "" });
            }
            msgs.Add(new { role = "user", content = prompt });

            var payload = new { model = s.OLLAMA_MODEL, messages = msgs, stream = true };
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
                try {
                    using var doc = JsonDocument.Parse(line);
                    if (doc.RootElement.TryGetProperty("message", out var msg)) {
                        string t = msg.GetProperty("content").GetString() ?? "";
                        sb.Append(t); onToken(t);
                    }
                    if (doc.RootElement.TryGetProperty("done", out var done) && done.GetBoolean()) break;
                } catch { }
            }
            return sb.ToString();
        }

        public static async Task<string> AskOpenAIAsync(string p, List<ChatTurn>? h, CancellationToken ct)
            => await AskGenericOpenAICompatibleAsync(CoreRegistry.Data.Settings.Current.OPENAI_BASE_URL, CoreRegistry.Data.Settings.Current.OPENAI_KEY, CoreRegistry.Data.Settings.Current.OPENAI_MODEL, p, h, ct);

        public static async Task<string> AskMistralAsync(string p, List<ChatTurn>? h, CancellationToken ct)
            => await AskGenericOpenAICompatibleAsync("https://api.mistral.ai/v1", CoreRegistry.Data.Settings.Current.MISTRAL_KEY, "mistral-large-latest", p, h, ct);

        public static async Task<string> AskOpenRouterAsync(string p, List<ChatTurn>? h, CancellationToken ct)
            => await AskGenericOpenAICompatibleAsync("https://openrouter.ai/api/v1", CoreRegistry.Data.Settings.Current.OPENROUTER_KEY, CoreRegistry.Data.Settings.Current.OPENROUTER_MODEL, p, h, ct);

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
