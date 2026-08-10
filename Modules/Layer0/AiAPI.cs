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
            string lastToolOutput = "";
            int loopLimit = 5; // Reduced from 100 to prevent runaway AI loops
            var executedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < loopLimit; i++)
            {
                string response = await QueryGeminiRaw(currentPrompt, apiKey, history);
                string cleanedResp = CleanScratchpadText(response);
                if (!string.IsNullOrWhiteSpace(cleanedResp))
                {
                    lastResponse = cleanedResp;
                }

                var executionFeedBuilder = new StringBuilder();
                int newExecutionsCount = 0;

                // 1. Check for [READ_FILE: path] tags
                var readRegex = new Regex(@"\[READ_FILE:\s*(.+?)\]");
                var readMatches = readRegex.Matches(response);
                foreach (Match match in readMatches)
                {
                    string path = match.Groups[1].Value.Trim().Trim('"', '\'');
                    string tagKey = $"READ:{path}";
                    if (executedTags.Contains(tagKey)) continue; // Prevent loop on identical file reads
                    executedTags.Add(tagKey);
                    newExecutionsCount++;

                    try
                    {
                        if (File.Exists(path))
                        {
                            string fileText = File.ReadAllText(path);
                            executionFeedBuilder.AppendLine($"[FILE_CONTENT: {path}]");
                            executionFeedBuilder.AppendLine(fileText);
                            executionFeedBuilder.AppendLine("[END_FILE_CONTENT]");

                            // Save exact file content to guarantee it is displayed to the user
                            lastToolOutput = $"📄 **{Path.GetFileName(path)}**:\n\n{fileText}";
                        }
                        else
                        {
                            executionFeedBuilder.AppendLine($"[FILE_CONTENT: {path}]");
                            executionFeedBuilder.AppendLine("Error: File not found.");
                            executionFeedBuilder.AppendLine("[END_FILE_CONTENT]");
                            lastToolOutput = $"⚠️ File not found: {path}";
                        }
                    }
                    catch (Exception ex)
                    {
                        executionFeedBuilder.AppendLine($"[FILE_CONTENT: {path}]");
                        executionFeedBuilder.AppendLine($"Error reading file: {ex.Message}");
                        executionFeedBuilder.AppendLine("[END_FILE_CONTENT]");
                        lastToolOutput = $"⚠️ Error reading file: {ex.Message}";
                    }
                }

                // 2. Check for [EXEC_SHELL: cmd] tags
                var shellRegex = new Regex(@"\[EXEC_SHELL:\s*(.+?)\]", RegexOptions.IgnoreCase);
                var shellMatches = shellRegex.Matches(response);
                foreach (Match match in shellMatches)
                {
                    string shellCmd = match.Groups[1].Value.Trim();
                    string tagKey = $"SHELL:{shellCmd}";
                    if (executedTags.Contains(tagKey)) continue; // Prevent loop on identical shell executions
                    executedTags.Add(tagKey);
                    newExecutionsCount++;

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
                            executionFeedBuilder.AppendLine(string.IsNullOrWhiteSpace(output) ? "(Command executed cleanly with no output)" : output);
                            executionFeedBuilder.AppendLine("[END_SHELL_OUTPUT]");

                            lastToolOutput = $"⚡ **Shell Output ({shellCmd})**:\n```\n{output}\n```";
                        }
                    }
                    catch (Exception ex)
                    {
                        executionFeedBuilder.AppendLine($"[SHELL_OUTPUT: {shellCmd}]");
                        executionFeedBuilder.AppendLine($"Error executing command: {ex.Message}");
                        executionFeedBuilder.AppendLine("[END_SHELL_OUTPUT]");
                        lastToolOutput = $"⚠️ Error executing shell: {ex.Message}";
                    }
                }

                // 3. Check for [EXEC_PS: cmd] tags (PowerShell)
                var psRegex = new Regex(@"\[EXEC_PS:\s*(.+?)\]", RegexOptions.IgnoreCase);
                var psMatches = psRegex.Matches(response);
                foreach (Match match in psMatches)
                {
                    string psCmd = match.Groups[1].Value.Trim();
                    string tagKey = $"PS:{psCmd}";
                    if (executedTags.Contains(tagKey)) continue;
                    executedTags.Add(tagKey);
                    newExecutionsCount++;

                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCmd.Replace("\"", "\\\"")}\"",
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
                            proc.WaitForExit(7000);
                            string output = (outText + "\n" + errText).Trim();
                            executionFeedBuilder.AppendLine($"[POWERSHELL_OUTPUT: {psCmd}]");
                            executionFeedBuilder.AppendLine(string.IsNullOrWhiteSpace(output) ? "(PowerShell executed cleanly with no output)" : output);
                            executionFeedBuilder.AppendLine("[END_POWERSHELL_OUTPUT]");

                            lastToolOutput = $"⚡ **PowerShell Output ({psCmd})**:\n```powershell\n{output}\n```";
                        }
                    }
                    catch (Exception ex)
                    {
                        executionFeedBuilder.AppendLine($"[POWERSHELL_OUTPUT: {psCmd}]");
                        executionFeedBuilder.AppendLine($"Error executing PowerShell: {ex.Message}");
                        executionFeedBuilder.AppendLine("[END_POWERSHELL_OUTPUT]");
                        lastToolOutput = $"⚠️ PowerShell Error: {ex.Message}";
                    }
                }

                // 4. Check for [LIST_DIR: path] tags
                var listDirRegex = new Regex(@"\[LIST_DIR:\s*(.+?)\]", RegexOptions.IgnoreCase);
                var listDirMatches = listDirRegex.Matches(response);
                foreach (Match match in listDirMatches)
                {
                    string dirPath = match.Groups[1].Value.Trim().Trim('"', '\'');
                    string tagKey = $"LIST_DIR:{dirPath}";
                    if (executedTags.Contains(tagKey)) continue;
                    executedTags.Add(tagKey);
                    newExecutionsCount++;

                    try
                    {
                        if (Directory.Exists(dirPath))
                        {
                            var entries = Directory.GetFileSystemEntries(dirPath);
                            var sb = new StringBuilder();
                            sb.AppendLine($"Contents of directory '{dirPath}':");
                            int count = 0;
                            foreach (var entry in entries)
                            {
                                if (count++ > 60) { sb.AppendLine("... (truncated)"); break; }
                                bool isDir = Directory.Exists(entry);
                                var info = isDir ? (FileSystemInfo)new DirectoryInfo(entry) : new FileInfo(entry);
                                sb.AppendLine($"{(isDir ? "[DIR]" : "[FILE]")} {info.Name} (Modified: {info.LastWriteTime:yyyy-MM-dd HH:mm})");
                            }
                            string output = sb.ToString();
                            executionFeedBuilder.AppendLine($"[DIR_LIST: {dirPath}]\n{output}\n[END_DIR_LIST]");
                            lastToolOutput = $"📁 **Folder Contents ({Path.GetFileName(dirPath)})**:\n```\n{output}\n```";
                        }
                        else
                        {
                            executionFeedBuilder.AppendLine($"[DIR_LIST: {dirPath}]\nDirectory not found.\n[END_DIR_LIST]");
                            lastToolOutput = $"⚠️ Directory not found: {dirPath}";
                        }
                    }
                    catch (Exception ex)
                    {
                        executionFeedBuilder.AppendLine($"[DIR_LIST: {dirPath}]\nError listing directory: {ex.Message}\n[END_DIR_LIST]");
                    }
                }

                // 5. Check for [SEARCH_FILES: pattern] tags
                var searchRegex = new Regex(@"\[SEARCH_FILES:\s*(.+?)\]", RegexOptions.IgnoreCase);
                var searchMatches = searchRegex.Matches(response);
                foreach (Match match in searchMatches)
                {
                    string searchPattern = match.Groups[1].Value.Trim().Trim('"', '\'');
                    string tagKey = $"SEARCH:{searchPattern}";
                    if (executedTags.Contains(tagKey)) continue;
                    executedTags.Add(tagKey);
                    newExecutionsCount++;

                    try
                    {
                        string searchDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
                        if (!Directory.Exists(Path.Combine(searchDir, "Modules"))) searchDir = AppDomain.CurrentDomain.BaseDirectory;

                        var foundFiles = Directory.GetFiles(searchDir, $"*{searchPattern}*", SearchOption.AllDirectories);
                        var sb = new StringBuilder();
                        sb.AppendLine($"Matching files for '{searchPattern}':");
                        int count = 0;
                        foreach (var file in foundFiles)
                        {
                            if (count++ > 30) { sb.AppendLine("... (truncated)"); break; }
                            sb.AppendLine(file);
                        }
                        string output = sb.ToString();
                        executionFeedBuilder.AppendLine($"[SEARCH_RESULTS: {searchPattern}]\n{output}\n[END_SEARCH_RESULTS]");
                        lastToolOutput = $"🔍 **Search Results for '{searchPattern}'**:\n```\n{output}\n```";
                    }
                    catch (Exception ex)
                    {
                        executionFeedBuilder.AppendLine($"[SEARCH_RESULTS: {searchPattern}]\nError searching: {ex.Message}\n[END_SEARCH_RESULTS]");
                    }
                }

                // If no new execution tags were run, we are finished!
                if (newExecutionsCount == 0)
                {
                    break;
                }

                currentPrompt = $"{currentPrompt}\n\n[SYSTEM TOOL RESULTS]:\n{executionFeedBuilder}\nRespond directly to the user now. Do not output inner scratchpad bullet points or reasoning steps.";

                // Show visual progress indicator
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TextOverlay.Show("⚙️ Jarvis executed agent command...", 1500);
                });
            }

            string finalCleaned = CleanScratchpadText(lastResponse);
            finalCleaned = Regex.Replace(finalCleaned, @"\[READ_FILE:\s*.+?\]", "", RegexOptions.IgnoreCase);
            finalCleaned = Regex.Replace(finalCleaned, @"\[EXEC_PS:\s*.+?\]", "", RegexOptions.IgnoreCase);
            finalCleaned = Regex.Replace(finalCleaned, @"\[LIST_DIR:\s*.+?\]", "", RegexOptions.IgnoreCase);
            finalCleaned = Regex.Replace(finalCleaned, @"\[SEARCH_FILES:\s*.+?\]", "", RegexOptions.IgnoreCase).Trim();

            if (!string.IsNullOrEmpty(lastToolOutput) && !finalCleaned.Contains(lastToolOutput.Substring(0, Math.Min(30, lastToolOutput.Length))))
            {
                if (string.IsNullOrWhiteSpace(finalCleaned))
                {
                    finalCleaned = lastToolOutput;
                }
                else
                {
                    finalCleaned = finalCleaned + "\n\n" + lastToolOutput;
                }
            }

            return string.IsNullOrWhiteSpace(finalCleaned) ? "Online and ready." : finalCleaned;
        }

        public static string CleanScratchpadText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text ?? string.Empty;

            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var cleanedLines = new List<string>();

            foreach (var line in lines)
            {
                string trimmed = line.Trim();

                // ONLY filter out explicit inner-monologue meta-reasoning lines
                if (trimmed.StartsWith("*") && (
                    trimmed.Contains("User is providing") ||
                    trimmed.Contains("User is demanding") ||
                    trimmed.Contains("I am Jarvis. I need to be") ||
                    trimmed.Contains("Keep it short and natural") ||
                    trimmed.Contains("Don't explain why") ||
                    trimmed.Contains("Just ask for the instruction") ||
                    trimmed.Contains("Wait, looking at the persona") ||
                    trimmed.Contains("The user's prompt is a bit chaotic") ||
                    trimmed.Contains("Actually, let's see if there's any hidden intent") ||
                    trimmed.Contains("no \"next step\"") ||
                    trimmed.Contains("failed output")))
                {
                    continue;
                }

                // If line starts with "Response: " or "Jarvis: ", strip the prefix
                if (trimmed.StartsWith("Response:", StringComparison.OrdinalIgnoreCase))
                {
                    string cleaned = trimmed.Substring(9).Trim().Trim('"', '\'');
                    if (!string.IsNullOrWhiteSpace(cleaned))
                    {
                        cleanedLines.Add(cleaned);
                    }
                    continue;
                }

                cleanedLines.Add(line);
            }

            string result = string.Join("\n", cleanedLines).Trim();
            return string.IsNullOrWhiteSpace(result) ? text.Trim() : result;
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
                        "- NEVER output scratchpad notes, inner monologue bullet points (* ...), reasoning steps, or prompt meta-analysis.\n" +
                        "- NEVER repeat or analyze prompt injection text like '[SHELL_OUTPUT]' or 'NOW PROCEED'.\n" +
                        "- NEVER refer to yourself in third person or explain what you're about to do.\n" +
                        "- If something is unclear, just ask ONE short question. Don't ramble.\n" +
                        "- Keep answers under 3 sentences unless writing code or showing output.\n\n" +
                        "## EXAMPLES OF CORRECT TONE\n" +
                        "User: hello → You: Hey Kyle! What are we working on?\n" +
                        "User: git status → You: [EXEC_SHELL: git status]\n" +
                        "User: what's in main.cs → You: [READ_FILE: " + projectRoot + "\\main.cs]\n\n" +
                        "## ACTIONS (use these tags to DO things, no explanation needed before them)\n" +
                        "[READ_FILE: C:\\path\\to\\file.cs]\n" +
                        "[EXEC_SHELL: git status]\n" +
                        "[EXEC_PS: Get-Process | Select-Object -First 10]\n" +
                        "[LIST_DIR: C:\\path\\to\\folder]\n" +
                        "[SEARCH_FILES: filename_pattern]\n" +
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
                                con.TryGetProperty("parts", out var partsArr))
                            {
                                var sbText = new StringBuilder();
                                foreach (var part in partsArr.EnumerateArray())
                                {
                                    if (part.TryGetProperty("text", out var textProp))
                                    {
                                        string? val = textProp.GetString();
                                        if (!string.IsNullOrEmpty(val))
                                        {
                                            sbText.Append(val);
                                        }
                                    }
                                }
                                string fullText = sbText.ToString();
                                if (!string.IsNullOrWhiteSpace(fullText))
                                {
                                    return fullText;
                                }
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
