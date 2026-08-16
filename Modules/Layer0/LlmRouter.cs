// Developer: heaplyn
// Date: 2026-08-10
// Summary: Central LLM dispatcher supporting Gemini, OpenAI-compatible APIs, Ollama local models,
//          custom HTTP endpoints, and P2P peer offloading. Falls back gracefully on failure.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public static class LlmRouter
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) }; // Reduced from 5 for snappier timeouts

        public static async Task<bool> IsOllamaAvailableAsync()
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMilliseconds(800));
                string endpoint = SettingsManager.Current.OLLAMA_ENDPOINT.TrimEnd('/');
                var resp = await _http.GetAsync($"{endpoint}/api/tags", cts.Token);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<string> AskAsync(string prompt, List<ChatTurn>? history = null, CancellationToken ct = default)
        {
            // 1. Try Local Heuristic Bypass first
            string? localResult = await HeuristicIntentParser.TryHandleLocallyAsync(prompt);
            if (localResult != null) return localResult;

            prompt = AiAPI.SanitizeText(prompt);

            string contextSummary = BackgroundContextManager.GetActiveContextSummary();
            if (!string.IsNullOrEmpty(contextSummary)) prompt = $"[Active Workspace Context: {contextSummary}]\n\n" + prompt;

            string infoPass = PredictiveStreamManager.GetInfoPass();
            string prediction = PredictiveStreamManager.GetCurrentPrediction();
            prompt = $"[PREDICTIVE_STATE: {infoPass}]\n[AI_PREDICTION: {prediction}]\n\n" + prompt;

            string primaryBackend = SettingsManager.Current.LLM_BACKEND;
            bool isLocalLlmAvailable = await IsOllamaAvailableAsync();

            // --- LLM FAILOVER CHAIN (TIERED) ---
            var failoverChain = new List<string> { primaryBackend, "Groq", "Gemini", "Ollama" };
            failoverChain = failoverChain.Distinct().ToList();

            string lastError = "";

            foreach (var backend in failoverChain)
            {
                try
                {
                    // Validation checks before attempting backend
                    if (backend == "Ollama" && !isLocalLlmAvailable) continue;
                    if (backend == "Gemini" && !OfflineCacheManager.CanUseGemini()) continue;
                    if (backend == "Groq" && string.IsNullOrEmpty(SettingsManager.Current.GROQ_KEY)) continue;

                    DebugConsoleOverlay.LogVerbose("LlmRouter", $"Attempting AI backend: {backend}", isMinimal: true);
                    return await CallBackendAsync(backend, prompt, history, ct);
                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException) throw;

                    lastError = ex.Message;
                    DebugConsoleOverlay.Log("LlmRouter", $"{backend} failed: {ex.Message}. Rotating to fallback...");

                    // Smart Groq handling: If we hit a rate limit, try the ultra-fast 8B model instead of 70B
                    if (backend == "Groq" && ex.Message.Contains("rate_limit_exceeded"))
                    {
                        try {
                            DebugConsoleOverlay.Log("LlmRouter", "Groq overload. Trying ultra-fast 8B model...");
                            return await AskGenericOpenAICompatibleAsync("https://api.groq.com/openai/v1", SettingsManager.Current.GROQ_KEY, "llama-3.1-8b-instant", prompt, history, ct);
                        } catch { }
                    }
                }
            }

            return $"⚠️ LLM ENGINE ERROR: All providers exhausted.\nLast error: {lastError}";
        }

        private static async Task<string> CallBackendAsync(string backend, string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            return backend switch
            {
                "OpenAI"     => await AskOpenAIAsync(prompt, history, ct),
                "Anthropic"  => await AskAnthropicAsync(prompt, history, ct),
                "Groq"       => await AskGroqAsync(prompt, history, ct),
                "Perplexity" => await AskPerplexityAsync(prompt, history, ct),
                "Mistral"    => await AskMistralAsync(prompt, history, ct),
                "OpenRouter" => await AskOpenRouterAsync(prompt, history, ct),
                "Ollama"     => await AskOllamaAsync(prompt, history, ct),
                "Custom"     => await AskCustomAsync(prompt, history, ct),
                "P2P"        => await JarvisP2PClient.AskBestPeerAsync(prompt, history),
                _            => await AiAPI.AskGemini(prompt, history, ct)
            };
        }

        // ── Anthropic (Claude) ──────────────────────────────────────────────────

        public static async Task<string> AskAnthropicAsync(string prompt, List<ChatTurn>? history = null, CancellationToken ct = default)
        {
            var s = SettingsManager.Current;
            if (string.IsNullOrEmpty(s.ANTHROPIC_KEY))
                throw new Exception("Anthropic API key not set.");

            var messages = new List<object>();
            if (history != null)
            {
                foreach (var turn in history.TakeLast(30))
                    messages.Add(new { role = turn.Role == "model" ? "assistant" : "user", content = turn.Text });
            }
            messages.Add(new { role = "user", content = prompt });

            var payload = new
            {
                model = s.ANTHROPIC_MODEL,
                system = AiAPI.GetSystemPrompt(),
                messages = messages,
                max_tokens = 2000
            };

            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            req.Headers.Add("x-api-key", s.ANTHROPIC_KEY);
            req.Headers.Add("anthropic-version", "2023-06-01");
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(req, ct);
            string body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) throw new Exception($"Anthropic Error: {body}");

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
        }

        // ── Groq (Ultra-Fast Llama/Mixtral) ──────────────────────────────────────

        public static async Task<string> AskGroqAsync(string prompt, List<ChatTurn>? history = null, CancellationToken ct = default)
        {
            var s = SettingsManager.Current;
            if (string.IsNullOrEmpty(s.GROQ_KEY)) throw new Exception("Groq API key not set.");

            // Self-Healing: Auto-correct decommissioned models
            if (s.GROQ_MODEL == "llama-3.1-70b-versatile")
            {
                s.GROQ_MODEL = "llama-3.3-70b-versatile";
                SettingsManager.Save();
                DebugConsoleOverlay.Log("Groq-Heal", "Auto-updated decommissioned model to llama-3.3-70b-versatile");
            }

            // Use the configured model from settings
            string model = string.IsNullOrEmpty(s.GROQ_MODEL) ? "llama-3.3-70b-versatile" : s.GROQ_MODEL;

            return await AskGenericOpenAICompatibleAsync("https://api.groq.com/openai/v1", s.GROQ_KEY, model, prompt, history, ct);
        }

        // ── Perplexity (Online Search AI) ─────────────────────────────────────────

        public static async Task<string> AskPerplexityAsync(string prompt, List<ChatTurn>? history = null, CancellationToken ct = default)
        {
            var s = SettingsManager.Current;
            if (string.IsNullOrEmpty(s.PERPLEXITY_KEY)) throw new Exception("Perplexity API key not set.");

            return await AskGenericOpenAICompatibleAsync("https://api.perplexity.ai", s.PERPLEXITY_KEY, s.PERPLEXITY_MODEL, prompt, history, ct);
        }

        // ── Mistral AI ────────────────────────────────────────────────────────────

        public static async Task<string> AskMistralAsync(string prompt, List<ChatTurn>? history = null, CancellationToken ct = default)
        {
            var s = SettingsManager.Current;
            if (string.IsNullOrEmpty(s.MISTRAL_KEY)) throw new Exception("Mistral API key not set.");

            return await AskGenericOpenAICompatibleAsync("https://api.mistral.ai/v1", s.MISTRAL_KEY, s.MISTRAL_MODEL, prompt, history, ct);
        }

        // ── OpenRouter (Unified API) ──────────────────────────────────────────────

        public static async Task<string> AskOpenRouterAsync(string prompt, List<ChatTurn>? history = null, CancellationToken ct = default)
        {
            var s = SettingsManager.Current;
            if (string.IsNullOrEmpty(s.OPENROUTER_KEY)) throw new Exception("OpenRouter API key not set.");

            return await AskGenericOpenAICompatibleAsync("https://openrouter.ai/api/v1", s.OPENROUTER_KEY, s.OPENROUTER_MODEL, prompt, history, ct);
        }

        private static async Task<string> AskGenericOpenAICompatibleAsync(string baseUrl, string key, string model, string prompt, List<ChatTurn>? history, CancellationToken ct)
        {
            var compressedHistory = await CompressHistoryAsync(history);
            var messages = new List<object> { new { role = "system", content = AiAPI.GetCompactSystemPrompt() } };

            foreach (var turn in compressedHistory)
                messages.Add(new { role = turn.Role == "model" ? "assistant" : "user", content = turn.Text });

            messages.Add(new { role = "user", content = prompt });

            var payload = new { model, messages, max_tokens = 1500, temperature = 0.5 };
            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions");
            req.Headers.Add("Authorization", $"Bearer {key}");
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(req, ct);
            string body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) throw new Exception($"API Error: {body}");

            using var doc = JsonDocument.Parse(body);
            string content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

            if (doc.RootElement.TryGetProperty("usage", out var usage))
            {
                int p = usage.TryGetProperty("prompt_tokens", out var pp) ? pp.GetInt32() : 0;
                int c = usage.TryGetProperty("completion_tokens", out var cc) ? cc.GetInt32() : 0;
                int t = usage.TryGetProperty("total_tokens", out var tt) ? tt.GetInt32() : 0;
                content += $"\n[METADATA_USAGE: {p},{c},{t}]";
            }
            return content;
        }

        // ── OpenAI-Compatible ─────────────────────────────────────────────────────

        public static async Task<string> AskOpenAIAsync(string prompt, List<ChatTurn>? history = null, CancellationToken ct = default)
        {
            var s = SettingsManager.Current;
            if (string.IsNullOrEmpty(s.OPENAI_KEY))
                throw new Exception("OpenAI API key not set.");

            return await AskGenericOpenAICompatibleAsync(s.OPENAI_BASE_URL, s.OPENAI_KEY, s.OPENAI_MODEL, prompt, history, ct);
        }

        // ── Ollama Local ──────────────────────────────────────────────────────────

        /// <summary>
        /// Streams an Ollama response token-by-token. Calls <paramref name="onToken"/> for each
        /// content token and <paramref name="onThinkingToken"/> for DeepSeek-R1-style thinking tokens.
        /// Returns the full assembled content string.
        /// </summary>
        public static async Task<string> AskOllamaStreamAsync(
            string prompt,
            List<ChatTurn>? history,
            Action<string> onToken,
            CancellationToken ct = default,
            Action<string>? onThinkingToken = null)
        {
            // Sanitize incoming prompt
            prompt = AiAPI.SanitizeText(prompt);

            string contextSummary = BackgroundContextManager.GetActiveContextSummary();
            if (!string.IsNullOrEmpty(contextSummary))
            {
                prompt = $"[Active Workspace Context: {contextSummary}]\n\n" + prompt;
            }

            var s = SettingsManager.Current;
            string endpoint = s.OLLAMA_ENDPOINT.TrimEnd('/');
            string model = s.OLLAMA_MODEL;

            var compressedHistory = await CompressHistoryAsync(history);

            var messages = new List<object> { new { role = "system", content = GetCompactSystemPrompt() } };
            foreach (var turn in compressedHistory)
                messages.Add(new { role = turn.Role == "model" ? "assistant" : turn.Role, content = turn.Text });
            messages.Add(new { role = "user", content = prompt });

            int adaptiveCtx = GetAdaptiveNumCtx(history?.Count ?? 0);
            var payload = new {
                model, messages,
                stream = true,   // KEY: streaming mode
                options = new {
                    num_ctx    = adaptiveCtx,
                    num_batch  = 512,      // Larger batch = faster prompt processing
                    temperature = 0.3,
                    num_predict = 500,
                    top_k       = 20,
                    top_p       = 0.85,
                    use_mlock  = true,    // Pin model weights in RAM to prevent paging
                    f16_kv     = true     // Half-precision KV cache = 2× memory efficiency
                }
            };

            DebugConsoleOverlay.Log("LlmRouter", $"Ollama stream: endpoint={endpoint} model={model} num_ctx={adaptiveCtx} history={history?.Count ?? 0}t");
            var json = JsonSerializer.Serialize(payload);
            var req = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/api/chat");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage resp;
            try
            {
                resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (Exception ex)
            {
                DebugConsoleOverlay.Log("LlmRouter", $"Ollama stream HTTP FAILED: {ex.Message}");
                throw;
            }

            DebugConsoleOverlay.Log("LlmRouter", $"Ollama stream HTTP {(int)resp.StatusCode} {resp.StatusCode}");
            if (!resp.IsSuccessStatusCode)
            {
                string err = await resp.Content.ReadAsStringAsync();
                DebugConsoleOverlay.Log("LlmRouter", $"Ollama stream error body: {err}");
                throw new Exception($"Ollama error {(int)resp.StatusCode}: {err}");
            }

            var sb = new StringBuilder();
            int lineCount = 0, tokenCount = 0;
            using var stream = await resp.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            // 15 second total time limit for cold-start/loading before giving up
            var overallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            overallCts.CancelAfter(TimeSpan.FromSeconds(30));

            while (!reader.EndOfStream)
            {
                overallCts.Token.ThrowIfCancellationRequested();
                string? line = await reader.ReadLineAsync();
                lineCount++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Log first 3 raw lines so we can see the format Ollama is actually using
                if (lineCount <= 3)
                    DebugConsoleOverlay.Log("LlmRouter", $"Ollama stream line[{lineCount}]: {line.Substring(0, Math.Min(120, line.Length))}");

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("message", out var msg))
                    {
                        // ── DeepSeek-R1 thinking tokens ─────────────────────────────
                        // Thinking models stream reasoning in message.thinking with empty message.content
                        if (msg.TryGetProperty("thinking", out var thinkChunk))
                        {
                            string thought = thinkChunk.GetString() ?? "";
                            if (!string.IsNullOrEmpty(thought))
                                onThinkingToken?.Invoke(thought);
                        }

                        // ── Normal content tokens ────────────────────────────────────
                        if (msg.TryGetProperty("content", out var chunk))
                        {
                            string token = chunk.GetString() ?? "";
                            if (!string.IsNullOrEmpty(token))
                            {
                                sb.Append(token);
                                tokenCount++;
                                onToken(token);
                            }
                        }
                    }
                    // done signal
                    if (root.TryGetProperty("done", out var done) && done.GetBoolean())
                    {
                        DebugConsoleOverlay.Log("LlmRouter", $"Ollama stream done. lines={lineCount} tokens={tokenCount} chars={sb.Length}");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    DebugConsoleOverlay.Log("LlmRouter", $"Ollama stream parse error on line[{lineCount}]: {ex.Message}");
                }
            }

            if (sb.Length == 0)
                DebugConsoleOverlay.Log("LlmRouter", $"Ollama stream WARNING: 0 chars returned after {lineCount} lines");

            return sb.ToString();
        }

        public static async Task<string> AskOllamaAsync(string prompt, List<ChatTurn>? history = null, CancellationToken ct = default)
        {
            var s = SettingsManager.Current;
            string endpoint = s.OLLAMA_ENDPOINT.TrimEnd('/');
            string model = s.OLLAMA_MODEL;

            var compressedHistory = await CompressHistoryAsync(history);

            // Use compact system prompt for local LLMs to prevent context overload & timeouts
            var messages = new List<object>
            {
                new { role = "system", content = GetCompactSystemPrompt() }
            };

            foreach (var turn in compressedHistory)
                messages.Add(new { role = turn.Role == "model" ? "assistant" : turn.Role, content = turn.Text });


            messages.Add(new { role = "user", content = prompt });

            int adaptiveCtx = GetAdaptiveNumCtx(history?.Count ?? 0);
            var payload = new { 
                model, 
                messages, 
                stream = false,
                options = new { 
                    num_ctx     = adaptiveCtx,
                    num_batch   = 512,     // Larger batch = faster prompt eval
                    temperature = 0.3,
                    num_predict = 500,
                    top_k       = 20,
                    top_p       = 0.85,
                    use_mlock   = true,   // Pin weights in RAM
                    f16_kv      = true    // Half-precision KV cache
                }
            };
            DebugConsoleOverlay.Log("LlmRouter", $"Ollama: num_ctx={adaptiveCtx} (history={history?.Count ?? 0} turns)");
            string json = JsonSerializer.Serialize(payload);

            var req = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/api/chat");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(req, ct);
            string body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Ollama error {(int)resp.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "(empty response)";
        }

        // ── Custom OpenAI-Compatible Endpoint ─────────────────────────────────────

        public static async Task<string> AskCustomAsync(string prompt, List<ChatTurn>? history = null, CancellationToken ct = default)
        {
            var s = SettingsManager.Current;
            if (string.IsNullOrEmpty(s.CUSTOM_LLM_ENDPOINT))
                throw new Exception("Custom LLM endpoint not set. Use 'llm' settings to configure.");

            string endpoint = s.CUSTOM_LLM_ENDPOINT.TrimEnd('/');
            string model = s.CUSTOM_LLM_MODEL;

            // Use compact system prompt for local LLMs to prevent context overload & timeouts
            var messages = new List<object>
            {
                new { role = "system", content = GetCompactSystemPrompt() }
            };

            if (history != null)
            {
                var relevantHistory = history.Count > 120 ? history.GetRange(history.Count - 120, 120) : history;
                foreach (var turn in relevantHistory)
                    messages.Add(new { role = turn.Role == "model" ? "assistant" : turn.Role, content = turn.Text });
            }

            messages.Add(new { role = "user", content = prompt });

            var payload = new { model, messages, max_tokens = 2000 };
            string json = JsonSerializer.Serialize(payload);

            var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
            if (!string.IsNullOrEmpty(s.CUSTOM_LLM_KEY))
                req.Headers.Add("Authorization", $"Bearer {s.CUSTOM_LLM_KEY}");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(req, ct);
            string body = await resp.Content.ReadAsStringAsync(ct);

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
        /// <summary>
        /// Compresses conversation history to save tokens.
        /// Keeps the last 6 turns verbatim (for recency), summarizes older turns into a single memory block.
        /// </summary>
        private static async Task<List<ChatTurn>> CompressHistoryAsync(List<ChatTurn>? history)
        {
            if (history == null || history.Count <= 12)
                return history ?? new List<ChatTurn>();

            const int RECENT_KEEP = 6;  // Always keep last N turns verbatim
            var recent = history.GetRange(history.Count - RECENT_KEEP, RECENT_KEEP);

            // PRUNE INSTEAD OF SUMMARIZE: If history is getting very deep, just prune to avoid recursive stalls
            if (history.Count > 40) return recent;

            var old   = history.GetRange(0, history.Count - RECENT_KEEP);
            var oldText = new StringBuilder();
            foreach (var t in old)
                oldText.AppendLine($"{(t.Role == "user" ? "User" : "Jarvis")}: {t.Text}");

            string summaryPrompt =
                "Summarize this conversation in 3-5 short bullet points (key facts only):\n" +
                oldText.ToString();

            string summary;
            try
            {
                // Set a strict timeout and use Gemini (parallel) if possible
                summary = await AiAPI.AskGemini(summaryPrompt, null);
            }
            catch { summary = "[prior conversation context]"; }

            var compressed = new List<ChatTurn>
            {
                new ChatTurn { Role = "assistant", Text = $"[Memory from earlier in this session]:\n{summary}" }
            };
            compressed.AddRange(recent);
            return compressed;
        }

        /// <summary>
        /// Computes an adaptive Ollama num_ctx based on conversation depth.
        /// New sessions start fast with 8k, scaling up to 128k for long deep sessions.
        /// </summary>
        private static int GetAdaptiveNumCtx(int historyTurnCount)
        {
            if (historyTurnCount <= 2)  return 8192;   // Cold start — fast
            if (historyTurnCount <= 6)  return 16384;  // Light session
            if (historyTurnCount <= 12) return 32768;  // Growing conversation
            if (historyTurnCount <= 24) return 65536;  // Deep session
            return 131072;                             // Long-running session — full 128k
        }

        private static string GetCompactSystemPrompt()
        {
            string activeWindow = ScreenMonitorEngine.ActiveWindowTitle;
            return
                "You are Jarvis, a sharp AI assistant in a Windows HUD. Be concise and direct.\n" +
                $"Active window: {activeWindow}\n" +
                "## ACTIONS\n" +
                "[EXEC_SHELL: cmd] [EXEC_PS: cmd] [READ_FILE: path] [WRITE_FILE: path]\ncontent\n[END_WRITE]\n" +
                "[OPEN_FILE: path] [OPEN_EDITOR: path] [LIST_DIR: path] [GET_PROCESSES] [KILL_PROCESS: name]\n" +
                "[RUN_COMMAND: cmd] [TAKE_SCREENSHOT] [SPEECH: text] [SET_CLIPBOARD: text]\n" +
                "Never narrate. Never explain steps. Just respond or act.";
        }
    }
}
