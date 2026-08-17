// Developer: heaplyn
// Date: 2026-08-17
// Summary: Core implementation of ILlmService.
//          Handles failover routing between Gemini, Groq, Ollama, and OpenAI backends.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public class LlmService : ILlmService
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };

        public async Task<bool> IsLocalAvailableAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
                string endpoint = SettingsManager.Current.OLLAMA_ENDPOINT.TrimEnd('/');
                var resp = await _http.GetAsync($"{endpoint}/api/tags", cts.Token);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<List<string>> GetLocalModelsAsync()
        {
            try
            {
                string endpoint = SettingsManager.Current.OLLAMA_ENDPOINT.TrimEnd('/');
                var resp = await _http.GetAsync($"{endpoint}/api/tags");
                if (!resp.IsSuccessStatusCode) return new();

                string body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                var models = new List<string>();
                foreach (var model in doc.RootElement.GetProperty("models").EnumerateArray())
                {
                    string? name = model.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (name != null) models.Add(name);
                }
                return models;
            }
            catch { return new(); }
        }

        public async Task<string> AskAsync(string prompt, List<ChatTurn>? history = null, CancellationToken ct = default)
        {
            try
            {
                // 1. Local Heuristic Bypass
                string? localResult = await HeuristicIntentParser.TryHandleLocallyAsync(prompt);
                if (localResult != null) return localResult;

                string cleanPrompt = AiAPI.SanitizeText(prompt);

                // Add specific persona instruction to stabilize responses
                cleanPrompt = "Persona: You are Jarvis, a highly efficient, technical, and slightly witty AI assistant. Be concise. " + cleanPrompt;

                string contextSummary = BackgroundContextManager.GetActiveContextSummary();
                if (!string.IsNullOrEmpty(contextSummary)) cleanPrompt = $"[Active Workspace Context: {contextSummary}]\n\n" + cleanPrompt;

                string primaryBackend = SettingsManager.Current.LLM_BACKEND;
                bool isLocalLlmAvailable = await IsLocalAvailableAsync();

                var failoverChain = new List<string> { primaryBackend, "Groq", "Gemini", "Ollama" }.Distinct().ToList();
                string lastError = "";

                foreach (var backend in failoverChain)
                {
                    try
                    {
                        if (backend == "Ollama" && !isLocalLlmAvailable) continue;
                        if (backend == "Gemini" && !OfflineCacheManager.CanUseGemini()) continue;
                        if (backend == "Groq" && string.IsNullOrEmpty(SettingsManager.Current.GROQ_KEY)) continue;

                        return await CallBackendInternalAsync(backend, cleanPrompt, history, ct);
                    }
                    catch (Exception ex)
                    {
                        if (ex is OperationCanceledException) throw;
                        lastError = ex.Message;
                        DebugConsoleOverlay.Log("LlmService", $"{backend} failover: {ex.Message}");
                    }
                }

                return $"⚠️ LLM ENGINE ERROR: All providers exhausted.\nLast error: {lastError}";
            }
            catch (Exception ex)
            {
                return "❌ AI Fault: " + ex.Message;
            }
        }

        private async Task<string> CallBackendInternalAsync(string backend, string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            return backend switch
            {
                "OpenAI" => await AskGenericOpenAICompatibleAsync(SettingsManager.Current.OPENAI_BASE_URL, SettingsManager.Current.OPENAI_KEY, SettingsManager.Current.OPENAI_MODEL, prompt, history, ct),
                "Groq" => await AskGroqAsync(prompt, history, ct),
                "Ollama" => await AskOllamaAsync(prompt, history, ct),
                "P2P" => await JarvisP2PClient.AskBestPeerAsync(prompt, history),
                _ => await AiAPI.AskGemini(prompt, history, ct)
            };
        }

        private async Task<string> AskGroqAsync(string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            string model = SettingsManager.Current.GROQ_MODEL;
            if (model == "llama-3.1-70b-versatile") model = "llama-3.3-70b-versatile"; // Self-healing
            return await AskGenericOpenAICompatibleAsync("https://api.groq.com/openai/v1", SettingsManager.Current.GROQ_KEY, model, prompt, history, ct);
        }

        private async Task<string> AskOllamaAsync(string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            string endpoint = SettingsManager.Current.OLLAMA_ENDPOINT.TrimEnd('/');
            string model = SettingsManager.Current.OLLAMA_MODEL;

            var payload = new {
                model,
                messages = BuildOpenAiMessages(prompt, history),
                stream = false,
                options = new { num_ctx = 32768, temperature = 0.3 }
            };

            var req = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/api/chat");
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(req, ct);
            string body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) throw new Exception($"Ollama error: {body}");

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
        }

        private async Task<string> AskGenericOpenAICompatibleAsync(string baseUrl, string key, string model, string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            var payload = new { model, messages = BuildOpenAiMessages(prompt, history), temperature = 0.5 };
            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions");
            req.Headers.Add("Authorization", $"Bearer {key}");
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(req, ct);
            string body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) throw new Exception($"API Error: {body}");

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        }

        private List<object> BuildOpenAiMessages(string prompt, List<ChatTurn>? history)
        {
            var messages = new List<object> { new { role = "system", content = AiAPI.GetCompactSystemPrompt() } };
            if (history != null)
            {
                foreach (var turn in history.TakeLast(20))
                    messages.Add(new { role = turn.Role == "model" ? "assistant" : "user", content = turn.Text });
            }
            messages.Add(new { role = "user", content = prompt });
            return messages;
        }
    }
}
