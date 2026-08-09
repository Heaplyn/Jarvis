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

namespace JarvisLauncher
{
    public static class AiAPI
    {
        private static readonly HttpClient _client = new HttpClient();

        public static async Task<string> AskGemini(string prompt)
        {
            string apiKey = SettingsManager.Current.GoogleAIKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return "Error: Gemini API Key is not set. Use 'setkey google <your_key>' to configure it.";
            }

            string currentPrompt = prompt;
            string lastResponse = "";
            int loopLimit = 4;

            for (int i = 0; i < loopLimit; i++)
            {
                string response = await QueryGeminiRaw(currentPrompt, apiKey);
                lastResponse = response;

                // Check for [READ_FILE: path] tags
                var readRegex = new Regex(@"\[READ_FILE:\s*(.+?)\]");
                var matches = readRegex.Matches(response);

                if (matches.Count == 0)
                {
                    break;
                }

                var fileContentsBuilder = new StringBuilder();
                foreach (Match match in matches)
                {
                    string path = match.Groups[1].Value.Trim().Trim('"', '\'');
                    try
                    {
                        if (File.Exists(path))
                        {
                            string fileText = File.ReadAllText(path);
                            fileContentsBuilder.AppendLine($"[FILE_CONTENT: {path}]");
                            fileContentsBuilder.AppendLine(fileText);
                            fileContentsBuilder.AppendLine("[END_FILE_CONTENT]");
                        }
                        else
                        {
                            fileContentsBuilder.AppendLine($"[FILE_CONTENT: {path}]");
                            fileContentsBuilder.AppendLine("Error: File not found.");
                            fileContentsBuilder.AppendLine("[END_FILE_CONTENT]");
                        }
                    }
                    catch (Exception ex)
                    {
                        fileContentsBuilder.AppendLine($"[FILE_CONTENT: {path}]");
                        fileContentsBuilder.AppendLine($"Error reading file: {ex.Message}");
                        fileContentsBuilder.AppendLine("[END_FILE_CONTENT]");
                    }
                }

                currentPrompt = $"{currentPrompt}\n\nHere is the content of the files you requested:\n{fileContentsBuilder}\n\nPlease proceed with your response based on this information.";

                // Show a quick visual notification
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TextOverlay.Show("📖 Jarvis is reading project files...", 1500);
                });
            }

            return lastResponse;
        }

        private static async Task<string> QueryGeminiRaw(string prompt, string apiKey)
        {
            // List of candidate models to try in sequence upon receiving rate-limit (429) or not-found (404) errors
            string[] models = new[] {
                "gemini-1.5-flash-latest",
                "gemini-1.5-flash",
                "gemini-2.0-flash-exp",
                "gemini-1.5-pro-latest",
                "gemini-3.5-flash"
            };

            string lastError = "";

            foreach (var model in models)
            {
                try
                {
                    var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                    
                    string instructions = InstructionsManager.GetFormattedInstructions();
                    string instructionsPath = InstructionsManager.InstructionsDirectory;
                    string systemPrompt = 
                        "You are Jarvis, a powerful AI assistant running locally on the user's Windows machine. " +
                        "You have direct access to read and modify the filesystem. " +
                        "If you need to inspect or read the contents of any local file to answer a question or write code, output the read request in this exact tag format:\n" +
                        "[READ_FILE: C:\\path\\to\\file.cs]\n" +
                        "The system will automatically read the file and feed its content back to you, allowing you to formulate your final response.\n\n" +
                        "To write or overwrite a file, output your file content wrapped in this exact tag format:\n" +
                        "[WRITE_FILE: C:\\path\\to\\file.txt]\n" +
                        "File content goes here...\n" +
                        "[END_WRITE]\n\n" +
                        "To append content to an existing file, wrap it in:\n" +
                        "[APPEND_FILE: C:\\path\\to\\file.txt]\n" +
                        "Content to append...\n" +
                        "[END_APPEND]\n\n" +
                        "Provide file paths exactly as requested (usually absolute Windows paths). " +
                        "Acknowledge what actions you are performing in your chat response.\n\n" +
                        "**AI COMPANION DYNAMIC MEMORY PERSISTENCE**:\n" +
                        $"You can persist facts, user preferences, project summaries, or custom guidelines by writing memory files to your own instructions directory: '{instructionsPath}'.\n" +
                        $"Any file you write inside this directory (like '{Path.Combine(instructionsPath, "memory.txt")}' or '{Path.Combine(instructionsPath, "guidelines.md")}') will automatically load as part of your system instructions on all subsequent chat turns.\n" +
                        "Use this capability to remember user details, ongoing tasks, and preferences automatically. Make your own decision on when and where to write files in this directory to update your context!\n\n" +
                        "Below are additional instructions from your local files:\n" +
                        instructions;

                    var payload = new
                    {
                        systemInstruction = new
                        {
                            parts = new[]
                            {
                                new { text = systemPrompt }
                            }
                        },
                        contents = new[]
                        {
                            new
                            {
                                parts = new[]
                                {
                                    new { text = prompt }
                                }
                            }
                        }
                    };

                    string jsonBody = JsonSerializer.Serialize(payload);
                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                    var response = await _client.PostAsync(url, content);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        // If model is NotFound (404) or QuotaExhausted (429), log and fall back to the next model in sequence
                        if (response.StatusCode == System.Net.HttpStatusCode.NotFound || 
                            response.StatusCode == (System.Net.HttpStatusCode)429)
                        {
                            lastError = $"Model '{model}' failed with status {response.StatusCode}.\nResponse: {responseBody}";
                            continue;
                        }
                        return $"Error: API returned status {response.StatusCode}\n{responseBody}";
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
                    lastError = $"Exception querying model '{model}': {ex.Message}";
                    continue;
                }
            }

            return $"Error: All candidate Gemini models failed to respond.\nLast error details:\n{lastError}";
        }
    }
}
