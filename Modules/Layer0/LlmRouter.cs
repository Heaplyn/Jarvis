// Developer: heaplyn
// Date: 2026-08-10
// Summary: Central LLM dispatcher supporting Gemini, OpenAI-compatible APIs, Ollama local models,
//          custom HTTP endpoints, and P2P peer offloading. Falls back gracefully on failure.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public static class LlmRouter
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        /// <summary>
        /// Route a prompt to the configured LLM backend.
        /// Falls back to Gemini if the selected backend fails.
        /// </summary>
        public static async Task<string> AskAsync(string prompt, List<ChatTurn>? history = null)
        {
            string backend = SettingsManager.Current.LlmBackend;

            try
            {
                return backend switch
                {
                    "OpenAI"   => await AskOpenAIAsync(prompt, history),
                    "Ollama"   => await AskOllamaAsync(prompt, history),
                    "Custom"   => await AskCustomAsync(prompt, history),
                    "P2P"      => await JarvisP2PClient.AskBestPeerAsync(prompt, history),
                    _          => await AiAPI.AskGemini(prompt, history)  // Default: Gemini
                };
            }
            catch (Exception ex)
            {
                ChatOverlay.LogConsoleAction("LlmRouter Fallback", $"{backend} failed: {ex.Message}. Falling back to Gemini.");
                // Graceful fallback to Gemini
                if (backend != "Gemini")
                {
                    try { return await AiAPI.AskGemini(prompt, history); }
                    catch { }
                }
                return $"⚠️ LLM Error ({backend}): {ex.Message}";
            }
        }

        // ── OpenAI-Compatible ─────────────────────────────────────────────────────

        public static async Task<string> AskOpenAIAsync(string prompt, List<ChatTurn>? history = null)
        {
            var s = SettingsManager.Current;
            if (string.IsNullOrEmpty(s.OpenAIKey))
                throw new Exception("OpenAI API key not set. Use 'llm' settings to configure.");

            string baseUrl = s.OpenAIBaseUrl.TrimEnd('/');
            string model = s.OpenAIModel;

            var messages = new List<object>
            {
                new { role = "system", content = "You are Jarvis, a sharp, direct AI assistant embedded in a Windows HUD. Be concise and helpful." }
            };

            if (history != null)
                foreach (var turn in history)
                    messages.Add(new { role = turn.Role == "model" ? "assistant" : turn.Role, content = turn.Text });

            messages.Add(new { role = "user", content = prompt });

            var payload = new { model, messages, max_tokens = 2000 };
            string json = JsonSerializer.Serialize(payload);

            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
            req.Headers.Add("Authorization", $"Bearer {s.OpenAIKey}");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(req);
            string body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"OpenAI API error {(int)resp.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "(empty response)";
        }

        // ── Ollama Local ──────────────────────────────────────────────────────────

        public static async Task<string> AskOllamaAsync(string prompt, List<ChatTurn>? history = null)
        {
            var s = SettingsManager.Current;
            string endpoint = s.OllamaEndpoint.TrimEnd('/');
            string model = s.OllamaModel;

            // Use Ollama's OpenAI-compatible /v1/chat/completions endpoint
            var messages = new List<object>
            {
                new { role = "system", content = "You are Jarvis, a sharp, direct AI assistant. Be concise." }
            };

            if (history != null)
                foreach (var turn in history)
                    messages.Add(new { role = turn.Role == "model" ? "assistant" : turn.Role, content = turn.Text });

            messages.Add(new { role = "user", content = prompt });

            var payload = new { model, messages, stream = false };
            string json = JsonSerializer.Serialize(payload);

            var req = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/api/chat");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(req);
            string body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Ollama error {(int)resp.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "(empty response)";
        }

        // ── Custom OpenAI-Compatible Endpoint ─────────────────────────────────────

        public static async Task<string> AskCustomAsync(string prompt, List<ChatTurn>? history = null)
        {
            var s = SettingsManager.Current;
            if (string.IsNullOrEmpty(s.CustomLlmEndpoint))
                throw new Exception("Custom LLM endpoint not set. Use 'llm' settings to configure.");

            string endpoint = s.CustomLlmEndpoint.TrimEnd('/');
            string model = s.CustomLlmModel;

            var messages = new List<object>
            {
                new { role = "system", content = "You are Jarvis, a sharp, direct AI assistant. Be concise." }
            };

            if (history != null)
                foreach (var turn in history)
                    messages.Add(new { role = turn.Role == "model" ? "assistant" : turn.Role, content = turn.Text });

            messages.Add(new { role = "user", content = prompt });

            var payload = new { model, messages, max_tokens = 2000 };
            string json = JsonSerializer.Serialize(payload);

            var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
            if (!string.IsNullOrEmpty(s.CustomLlmKey))
                req.Headers.Add("Authorization", $"Bearer {s.CustomLlmKey}");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(req);
            string body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Custom LLM error {(int)resp.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "(empty response)";
        }

        // ── Ollama Model Discovery ─────────────────────────────────────────────────

        public static async Task<List<string>> GetOllamaModelsAsync()
        {
            try
            {
                string endpoint = SettingsManager.Current.OllamaEndpoint.TrimEnd('/');
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
    }
}
