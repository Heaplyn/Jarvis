// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles content generation requests using the Gemini API key loaded from SystemSettings.json with automatic model failovers.

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class ChatTurn
    {
        public string Role { get; set; } = "user"; // "user" or "model"
        public string Text { get; set; } = string.Empty;
    }

    public static class AiAPI
    {
        private static readonly HttpClient _client = new HttpClient();

        public static async Task<string> AskGemini(string prompt, List<ChatTurn>? history = null)
        {
            string apiKey = SettingsManager.Current.GoogleAIKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return "Error: Gemini API Key is not set. Use 'setkey google <your_key>' to configure it.";
            }

            string currentPrompt = prompt;
            string lastResponse = "";
            int loopLimit = 100; // Allow up to 100 multi-step agent requests per turn

            for (int i = 0; i < loopLimit; i++)
            {
                string response = await QueryGeminiRaw(currentPrompt, apiKey, history);
                lastResponse = response;

                var executionFeedBuilder = new StringBuilder();

                // 1. Check for [READ_FILE: path] tags
                var readRegex = new Regex(@"\[READ_FILE:\s*(.+?)\]");
                var readMatches = readRegex.Matches(response);
                foreach (Match match in readMatches)
                {
                    string path = match.Groups[1].Value.Trim().Trim('"', '\'');
                    try
                    {
                        if (File.Exists(path))
                        {
                            string fileText = File.ReadAllText(path);
                            executionFeedBuilder.AppendLine($"[FILE_CONTENT: {path}]");
                            executionFeedBuilder.AppendLine(fileText);
                            executionFeedBuilder.AppendLine("[END_FILE_CONTENT]");
                        }
                        else
                        {
                            executionFeedBuilder.AppendLine($"[FILE_CONTENT: {path}]");
                            executionFeedBuilder.AppendLine("Error: File not found.");
                            executionFeedBuilder.AppendLine("[END_FILE_CONTENT]");
                        }
                    }
                    catch (Exception ex)
                    {
                        executionFeedBuilder.AppendLine($"[FILE_CONTENT: {path}]");
                        executionFeedBuilder.AppendLine($"Error reading file: {ex.Message}");
                        executionFeedBuilder.AppendLine("[END_FILE_CONTENT]");
                    }
                }

                // 2. Check for [EXEC_SHELL: cmd] tags
                var shellRegex = new Regex(@"\[EXEC_SHELL:\s*(.+?)\]", RegexOptions.IgnoreCase);
                var shellMatches = shellRegex.Matches(response);
                foreach (Match match in shellMatches)
                {
                    string shellCmd = match.Groups[1].Value.Trim();
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c {shellCmd}",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var proc = System.Diagnostics.Process.Start(psi);
                        if (proc != null)
                        {
                            string outText = proc.StandardOutput.ReadToEnd();
                            string errText = proc.StandardError.ReadToEnd();
                            proc.WaitForExit(5000);
                            string output = (outText + "\n" + errText).Trim();
                            executionFeedBuilder.AppendLine($"[SHELL_OUTPUT: {shellCmd}]");
                            executionFeedBuilder.AppendLine(output);
                            executionFeedBuilder.AppendLine("[END_SHELL_OUTPUT]");
                        }
                    }
                    catch (Exception ex)
                    {
                        executionFeedBuilder.AppendLine($"[SHELL_OUTPUT: {shellCmd}]");
                        executionFeedBuilder.AppendLine($"Error executing command: {ex.Message}");
                        executionFeedBuilder.AppendLine("[END_SHELL_OUTPUT]");
                    }
                }

                // If no execution tags were found, we are finished!
                if (readMatches.Count == 0 && shellMatches.Count == 0)
                {
                    break;
                }

                currentPrompt = $"{currentPrompt}\n\nHere is the execution output of the commands/files you requested:\n{executionFeedBuilder}\n\nNOW PROCEED IMMEDIATELY TO EXECUTE THE NEXT STEP OR GENERATE/WRITE FILES!";

                // Show visual progress indicator
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TextOverlay.Show("⚙️ Jarvis executed agent command...", 1500);
                });
            }

            return lastResponse;
        }

        private static async Task<string> QueryGeminiRaw(string prompt, string apiKey, List<ChatTurn>? history = null)
        {
            // Dynamically discover active supported models for this API key if needed
            var discoveredModels = await DiscoverActiveModelsAsync(apiKey);

            string[] models = discoveredModels.Count > 0 
                ? discoveredModels.ToArray() 
                : new[] { "gemini-2.0-flash", "gemini-2.5-flash", "gemini-1.5-flash", "gemini-pro" };

            string[] apiVersions = new[] { "v1beta", "v1" };
            string lastError = "";

            foreach (var apiVer in apiVersions)
            {
                foreach (var model in models)
                {
                    try
                    {
                        string cleanModel = model.StartsWith("models/") ? model.Substring(7) : model;
                        var url = $"https://generativelanguage.googleapis.com/{apiVer}/models/{cleanModel}:generateContent?key={apiKey}";
                    
                    string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
                    if (!Directory.Exists(Path.Combine(projectRoot, "Modules")))
                    {
                        projectRoot = AppDomain.CurrentDomain.BaseDirectory;
                    }

                    string instructions = InstructionsManager.GetFormattedInstructions();
                    string instructionsPath = InstructionsManager.InstructionsDirectory;
                    string systemPrompt =
                        $"You are Jarvis — a sharp, direct AI assistant embedded in Kyle's Windows HUD. Codebase root: '{projectRoot}'.\n\n" +
                        "## HOW TO TALK\n" +
                        "- Respond like a knowledgeable friend texting back. Short, natural, confident.\n" +
                        "- NEVER say 'The user said...', 'The user provided...', 'As an AI...', 'I should...', 'I will now...', 'Let me...'\n" +
                        "- NEVER narrate your own thoughts or actions. No 'Plan:', 'Step 1:', 'Thinking:', 'My approach:'.\n" +
                        "- NEVER refer to yourself in third person or explain what you're about to do.\n" +
                        "- If something is unclear, just ask ONE short question. Don't ramble.\n" +
                        "- Keep answers under 3 sentences unless writing code or showing output.\n\n" +
                        "## EXAMPLES OF CORRECT TONE\n" +
                        "User: test → You: Online and ready.\n" +
                        "User: git status → You: [EXEC_SHELL: git status]\n" +
                        "User: what's in main.cs → You: [READ_FILE: " + projectRoot + "\\main.cs]\n\n" +
                        "## ACTIONS (use these tags to DO things, no explanation needed before them)\n" +
                        "[READ_FILE: C:\\path\\to\\file.cs]\n" +
                        "[EXEC_SHELL: git status]\n" +
                        "[WRITE_FILE: C:\\path\\to\\file.txt]\ncontent\n[END_WRITE]\n" +
                        "[APPEND_FILE: C:\\path\\to\\file.txt]\ncontent\n[END_APPEND]\n" +
                        "[OPEN_FILE: C:\\path\\to\\file.pdf]\n" +
                        "[OPEN_EDITOR: C:\\path\\to\\file.txt]\n" +
                        "[PIN_FILE: C:\\path\\to\\file.txt]\n" +
                        "[RUN_COMMAND: theme dracula]\n\n" +
                        $"Save memory/notes to: '{instructionsPath}' — auto-loaded next session.\n\n" +
                        instructions;

                    // Build contents array supporting multi-turn conversation context
                    var contentsList = new List<object>();

                    if (history != null && history.Count > 0)
                    {
                        foreach (var turn in history)
                        {
                            contentsList.Add(new
                            {
                                role = turn.Role,
                                parts = new[] { new { text = turn.Text } }
                            });
                        }
                    }

                    // Add current turn
                    contentsList.Add(new
                    {
                        role = "user",
                        parts = new[] { new { text = prompt } }
                    });

                    var payload = new
                    {
                        systemInstruction = new
                        {
                            parts = new[]
                            {
                                new { text = systemPrompt }
                            }
                        },
                        contents = contentsList.ToArray()
                    };

                    string jsonBody = JsonSerializer.Serialize(payload);
                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                    var response = await _client.PostAsync(url, content);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        // Log error and unconditionally retry with the next candidate model
                        lastError = $"Model '{model}' returned HTTP status {response.StatusCode}.\nDetails: {responseBody}";
                        continue;
                    }

                    using (var doc = JsonDocument.Parse(responseBody))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                        {
                            var firstCandidate = candidates[0];
                            if (firstCandidate.TryGetProperty("content", out var con) &&
                                con.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                            {
                                var text = parts[0].GetProperty("text").GetString();
                                return text ?? "Error: Empty text response from Gemini.";
                            }
                        }
                    }

                    return "Error: Failed to parse Gemini API response.";
                }
                catch (Exception ex)
                {
                    lastError = $"Exception querying model '{model}' ({apiVer}): {ex.Message}";
                    continue;
                }
            }
            }

            return $"Error: All candidate Gemini models failed to respond.\nLast error details:\n{lastError}";
        }

        private static List<string> _cachedDiscoveredModels = new List<string>();

        private static async Task<List<string>> DiscoverActiveModelsAsync(string apiKey)
        {
            if (_cachedDiscoveredModels.Count > 0) return _cachedDiscoveredModels;

            try
            {
                var listUrl = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
                var resp = await _client.GetAsync(listUrl);
                if (resp.IsSuccessStatusCode)
                {
                    string json = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("models", out var modelsArr))
                    {
                        var discovered = new List<string>();
                        foreach (var m in modelsArr.EnumerateArray())
                        {
                            if (m.TryGetProperty("name", out var nameProp) && m.TryGetProperty("supportedGenerationMethods", out var methods))
                            {
                                string name = nameProp.GetString() ?? "";
                                foreach (var method in methods.EnumerateArray())
                                {
                                    if (method.GetString() == "generateContent")
                                    {
                                        discovered.Add(name);
                                        break;
                                    }
                                }
                            }
                        }
                        if (discovered.Count > 0)
                        {
                            _cachedDiscoveredModels = discovered;
                            return _cachedDiscoveredModels;
                        }
                    }
                }
            }
            catch { }

            return _cachedDiscoveredModels;
        }
    }
}
