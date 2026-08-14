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
using System.Linq;

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
            return await AskGeminiInternal(prompt, null, null, history);
        }

        public static async Task<string> AnalyzeImageAsync(string prompt, string base64Image)
        {
            return await AskGeminiInternal(prompt, base64Image, null, null);
        }

        public static async Task<string> AnalyzeImageBase64Async(string prompt, string base64Image, string mimeType = "image/png")
        {
            return await AskGeminiInternal(prompt, base64Image, null, null);
        }

        public static async Task<string> AnalyzeAudioAsync(string prompt, string base64Audio)
        {
            return await AskGeminiInternal(prompt, null, base64Audio, null);
        }

        private static async Task<string> AskGeminiInternal(string prompt, string? base64Image = null, string? base64Audio = null, List<ChatTurn>? history = null)
        {
            string apiKey = SettingsManager.Current.GoogleAIKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return "Error: Gemini API Key is not set. Use 'setkey google <your_key>' to configure it.";
            }

            // Inject User Activity, Active Window, & Command History Context
            string activityContext = UserActivityContextManager.BuildFullActivityContext();
            string currentPrompt = $"{activityContext}\nUser Query: {prompt}";

            string lastResponse = "";
            string lastToolOutput = "";
            int loopLimit = 5;
            var executedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < loopLimit; i++)
            {
                string response = await QueryGeminiRaw(currentPrompt, apiKey, base64Image, base64Audio, history);
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

                // 6. Check for [WRITE_FILE: path]content[END_WRITE] tags
                var writeRegex = new Regex(@"\[WRITE_FILE:\s*(.+?)\](.*?)\[END_WRITE\]", RegexOptions.Singleline);
                var writeMatches = writeRegex.Matches(response);
                foreach (Match match in writeMatches)
                {
                    string path = match.Groups[1].Value.Trim().Trim('"', '\'');
                    string content = match.Groups[2].Value;
                    string tagKey = $"WRITE:{path}:{content.GetHashCode()}";
                    if (executedTags.Contains(tagKey)) continue;
                    executedTags.Add(tagKey);
                    newExecutionsCount++;

                    try
                    {
                        string? dir = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        File.WriteAllText(path, content);
                        executionFeedBuilder.AppendLine($"[WRITE_FILE_OUTPUT: {path}]\nFile written successfully ({content.Length} characters).\n[END_WRITE_FILE_OUTPUT]");
                        lastToolOutput = $"📝 **Wrote File ({Path.GetFileName(path)})**";
                        
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ChatOverlay.LogConsoleAction("Write File", $"Path: {path}\nLength: {content.Length} chars\nResult: SUCCESS");
                        });
                    }
                    catch (Exception ex)
                    {
                        executionFeedBuilder.AppendLine($"[WRITE_FILE_OUTPUT: {path}]\nError writing file: {ex.Message}\n[END_WRITE_FILE_OUTPUT]");
                    }
                }

                // 7. Check for [APPEND_FILE: path]content[END_APPEND] tags
                var appendRegex = new Regex(@"\[APPEND_FILE:\s*(.+?)\](.*?)\[END_APPEND\]", RegexOptions.Singleline);
                var appendMatches = appendRegex.Matches(response);
                foreach (Match match in appendMatches)
                {
                    string path = match.Groups[1].Value.Trim().Trim('"', '\'');
                    string content = match.Groups[2].Value;
                    string tagKey = $"APPEND:{path}:{content.GetHashCode()}";
                    if (executedTags.Contains(tagKey)) continue;
                    executedTags.Add(tagKey);
                    newExecutionsCount++;

                    try
                    {
                        string? dir = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        File.AppendAllText(path, content);
                        executionFeedBuilder.AppendLine($"[APPEND_FILE_OUTPUT: {path}]\nFile appended successfully ({content.Length} characters).\n[END_APPEND_FILE_OUTPUT]");
                        lastToolOutput = $"📝 **Appended to File ({Path.GetFileName(path)})**";

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ChatOverlay.LogConsoleAction("Append File", $"Path: {path}\nLength: {content.Length} chars\nResult: SUCCESS");
                        });
                    }
                    catch (Exception ex)
                    {
                        executionFeedBuilder.AppendLine($"[APPEND_FILE_OUTPUT: {path}]\nError appending to file: {ex.Message}\n[END_APPEND_FILE_OUTPUT]");
                    }
                }

                // 8. Check for [DELETE_PATH: path] tags
                var deleteRegex = new Regex(@"\[DELETE_PATH:\s*(.+?)\]", RegexOptions.IgnoreCase);
                var deleteMatches = deleteRegex.Matches(response);
                foreach (Match match in deleteMatches)
                {
                    string path = match.Groups[1].Value.Trim().Trim('"', '\'');
                    string tagKey = $"DELETE:{path}";
                    if (executedTags.Contains(tagKey)) continue;
                    executedTags.Add(tagKey);
                    newExecutionsCount++;

                    try
                    {
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                            executionFeedBuilder.AppendLine($"[DELETE_OUTPUT: {path}]\nFile deleted successfully.\n[END_DELETE_OUTPUT]");
                            lastToolOutput = $"🗑️ **Deleted File ({Path.GetFileName(path)})**";
                        }
                        else if (Directory.Exists(path))
                        {
                            Directory.Delete(path, true);
                            executionFeedBuilder.AppendLine($"[DELETE_OUTPUT: {path}]\nDirectory deleted recursively successfully.\n[END_DELETE_OUTPUT]");
                            lastToolOutput = $"🗑️ **Deleted Directory ({Path.GetFileName(path)})**";
                        }
                        else
                        {
                            executionFeedBuilder.AppendLine($"[DELETE_OUTPUT: {path}]\nPath not found.\n[END_DELETE_OUTPUT]");
                        }
                    }
                    catch (Exception ex)
                    {
                        executionFeedBuilder.AppendLine($"[DELETE_OUTPUT: {path}]\nError deleting path: {ex.Message}\n[END_DELETE_OUTPUT]");
                    }
                }

                // 9. Check for [GET_PROCESSES] tags
                if (response.Contains("[GET_PROCESSES]", StringComparison.OrdinalIgnoreCase))
                {
                    string tagKey = "GET_PROCESSES";
                    if (!executedTags.Contains(tagKey))
                    {
                        executedTags.Add(tagKey);
                        newExecutionsCount++;

                        try
                        {
                            var sb = new StringBuilder();
                            sb.AppendLine("Active processes list:");
                            var processes = System.Diagnostics.Process.GetProcesses();
                            int count = 0;
                            foreach (var p in processes)
                            {
                                try
                                {
                                    if (count++ > 50) { sb.AppendLine("... (truncated)"); break; }
                                    sb.AppendLine($"- {p.ProcessName} (ID: {p.Id}, WorkingSet: {p.WorkingSet64 / 1024 / 1024}MB)");
                                }
                                catch { }
                            }
                            string output = sb.ToString();
                            executionFeedBuilder.AppendLine($"[PROCESSES_OUTPUT]\n{output}\n[END_PROCESSES_OUTPUT]");
                            lastToolOutput = "🖥️ **Listed system processes**";
                        }
                        catch (Exception ex)
                        {
                            executionFeedBuilder.AppendLine($"[PROCESSES_OUTPUT]\nError listing processes: {ex.Message}\n[END_PROCESSES_OUTPUT]");
                        }
                    }
                }

                // 10. Check for [KILL_PROCESS: target] tags
                var killRegex = new Regex(@"\[KILL_PROCESS:\s*(.+?)\]", RegexOptions.IgnoreCase);
                var killMatches = killRegex.Matches(response);
                foreach (Match match in killMatches)
                {
                    string target = match.Groups[1].Value.Trim().Trim('"', '\'');
                    string tagKey = $"KILL:{target}";
                    if (executedTags.Contains(tagKey)) continue;
                    executedTags.Add(tagKey);
                    newExecutionsCount++;

                    try
                    {
                        bool success = false;
                        if (int.TryParse(target, out int pid))
                        {
                            var p = System.Diagnostics.Process.GetProcessById(pid);
                            p.Kill();
                            success = true;
                        }
                        else
                        {
                            var procs = System.Diagnostics.Process.GetProcessesByName(target);
                            foreach (var p in procs)
                            {
                                p.Kill();
                                success = true;
                            }
                        }
                        
                        if (success)
                        {
                            executionFeedBuilder.AppendLine($"[KILL_OUTPUT: {target}]\nProcess terminated successfully.\n[END_KILL_OUTPUT]");
                            lastToolOutput = $"🛑 **Terminated process: {target}**";
                        }
                        else
                        {
                            executionFeedBuilder.AppendLine($"[KILL_OUTPUT: {target}]\nNo matching processes found.\n[END_KILL_OUTPUT]");
                        }
                    }
                    catch (Exception ex)
                    {
                        executionFeedBuilder.AppendLine($"[KILL_OUTPUT: {target}]\nError terminating process: {ex.Message}\n[END_KILL_OUTPUT]");
                    }
                }

                // 11. Check for [RUN_COMMAND: cmd] or [EXEC_COMMAND: cmd] tags
                var runCmdRegex = new Regex(@"\[(?:RUN_COMMAND|EXEC_COMMAND):\s*(.+?)\]", RegexOptions.IgnoreCase);
                var runCmdMatches = runCmdRegex.Matches(response);
                foreach (Match match in runCmdMatches)
                {
                    string jarvisCmd = match.Groups[1].Value.Trim().Trim('"', '\'');
                    string tagKey = $"RUN_COMMAND:{jarvisCmd}";
                    if (executedTags.Contains(tagKey)) continue;
                    executedTags.Add(tagKey);
                    newExecutionsCount++;

                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            CommandParser.ExecuteFirstSuggestion(jarvisCmd);
                            ChatOverlay.LogConsoleAction("Exec Command", $"Command: {jarvisCmd}\nResult: EXECUTED");
                        });
                        executionFeedBuilder.AppendLine($"[COMMAND_OUTPUT: {jarvisCmd}]\nJarvis computer tool command '{jarvisCmd}' executed successfully.\n[END_COMMAND_OUTPUT]");
                        lastToolOutput = $"⚡ **Executed Computer Tool: {jarvisCmd}**";
                    }
                    catch (Exception ex)
                    {
                        executionFeedBuilder.AppendLine($"[COMMAND_OUTPUT: {jarvisCmd}]\nError executing command: {ex.Message}\n[END_COMMAND_OUTPUT]");
                    }
                }

                // 12. Check for [TAKE_SCREENSHOT] tags
                if (response.Contains("[TAKE_SCREENSHOT]", StringComparison.OrdinalIgnoreCase))
                {
                    string tagKey = "TAKE_SCREENSHOT";
                    if (!executedTags.Contains(tagKey))
                    {
                        executedTags.Add(tagKey);
                        newExecutionsCount++;
                        try
                        {
                            string? screenBase64 = ScreenCaptureUtil.CapturePrimaryScreenToBase64();
                            if (screenBase64 != null)
                            {
                                base64Image = screenBase64; // Supply for next iteration
                                executionFeedBuilder.AppendLine("[SCREENSHOT_CAPTURED]");
                                lastToolOutput = "📸 **Captured screenshot for analysis.**";
                            }
                        }
                        catch (Exception ex)
                        {
                            executionFeedBuilder.AppendLine($"[SCREENSHOT_ERROR: {ex.Message}]");
                        }
                    }
                }

                // 13. Check for [GET_ACTIVE_WINDOWS] tags
                if (response.Contains("[GET_ACTIVE_WINDOWS]", StringComparison.OrdinalIgnoreCase))
                {
                    string tagKey = "GET_ACTIVE_WINDOWS";
                    if (!executedTags.Contains(tagKey))
                    {
                        executedTags.Add(tagKey);
                        newExecutionsCount++;
                        try
                        {
                            var sbWin = new StringBuilder();
                            sbWin.AppendLine("Current Open Windows:");
                            foreach (var p in System.Diagnostics.Process.GetProcesses())
                            {
                                if (!string.IsNullOrEmpty(p.MainWindowTitle))
                                {
                                    sbWin.AppendLine($"- {p.MainWindowTitle} ({p.ProcessName})");
                                }
                            }
                            string winOutput = sbWin.ToString();
                            executionFeedBuilder.AppendLine($"[ACTIVE_WINDOWS_OUTPUT]\n{winOutput}\n[END_ACTIVE_WINDOWS_OUTPUT]");
                            lastToolOutput = "🪟 **Retrieved active windows list.**";
                        }
                        catch (Exception ex)
                        {
                            executionFeedBuilder.AppendLine($"[WINDOWS_ERROR: {ex.Message}]");
                        }
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
            finalCleaned = Regex.Replace(finalCleaned, @"\[EXEC_SHELL:\s*.+?\]", "", RegexOptions.IgnoreCase);
            finalCleaned = Regex.Replace(finalCleaned, @"\[EXEC_PS:\s*.+?\]", "", RegexOptions.IgnoreCase);
            finalCleaned = Regex.Replace(finalCleaned, @"\[LIST_DIR:\s*.+?\]", "", RegexOptions.IgnoreCase);
            finalCleaned = Regex.Replace(finalCleaned, @"\[SEARCH_FILES:\s*.+?\]", "", RegexOptions.IgnoreCase);
            finalCleaned = Regex.Replace(finalCleaned, @"\[WRITE_FILE:\s*.+?\][\s\S]*?\[END_WRITE\]", "", RegexOptions.IgnoreCase);
            finalCleaned = Regex.Replace(finalCleaned, @"\[APPEND_FILE:\s*.+?\][\s\S]*?\[END_APPEND\]", "", RegexOptions.IgnoreCase);
            finalCleaned = Regex.Replace(finalCleaned, @"\[DELETE_PATH:\s*.+?\]", "", RegexOptions.IgnoreCase);
            finalCleaned = Regex.Replace(finalCleaned, @"\[GET_PROCESSES\]", "", RegexOptions.IgnoreCase);
            finalCleaned = Regex.Replace(finalCleaned, @"\[KILL_PROCESS:\s*.+?\]", "", RegexOptions.IgnoreCase);
            finalCleaned = Regex.Replace(finalCleaned, @"\[TAKE_SCREENSHOT\]", "", RegexOptions.IgnoreCase);
            finalCleaned = Regex.Replace(finalCleaned, @"\[GET_ACTIVE_WINDOWS\]", "", RegexOptions.IgnoreCase);
            finalCleaned = finalCleaned.Trim();

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

        private static async Task<string> QueryGeminiRaw(string prompt, string apiKey, string? base64Image = null, string? base64Audio = null, List<ChatTurn>? history = null)
        {
            // Prioritize fastest models for HUD responsiveness
            var models = new List<string> { "gemini-2.0-flash", "gemini-1.5-flash", "gemini-1.5-flash-8b", "gemini-pro" };

            // If user has a specific model preference, put it at the very front of the list
            string preferred = SettingsManager.Current.GeminiModel;
            if (!string.IsNullOrEmpty(preferred))
            {
                models.Remove(preferred);
                models.Insert(0, preferred);
            }

            // Integrate discovered models if available
            if (_cachedDiscoveredModels.Count > 0)
            {
                foreach(var m in _cachedDiscoveredModels)
                    if(!models.Contains(m)) models.Add(m);
            }
            else
            {
                // Background discover for next time
                _ = Task.Run(() => DiscoverActiveModelsAsync(apiKey));
            }

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
                        string systemPrompt = GetSystemPrompt();

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
                    var currentParts = new List<object>();
                    if (!string.IsNullOrEmpty(base64Image))
                    {
                        currentParts.Add(new
                        {
                            inline_data = new
                            {
                                mime_type = "image/jpeg",
                                data = base64Image
                            }
                        });
                    }
                    if (!string.IsNullOrEmpty(base64Audio))
                    {
                        currentParts.Add(new
                        {
                            inline_data = new
                            {
                                mime_type = "audio/wav",
                                data = base64Audio
                            }
                        });
                    }
                    currentParts.Add(new { text = prompt });

                    contentsList.Add(new
                    {
                        role = "user",
                        parts = currentParts.ToArray()
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

        public static string GetSystemPrompt()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            if (!Directory.Exists(Path.Combine(projectRoot, "Modules")))
            {
                projectRoot = AppDomain.CurrentDomain.BaseDirectory;
            }

            string instructions = InstructionsManager.GetFormattedInstructions();
            string instructionsPath = InstructionsManager.InstructionsDirectory;
            string notesPath = NotesManager.GetNotesDirectory();
            string adaptiveTelemetry = SelfAdaptingAiContext.BuildDynamicAdaptiveContext();

            return "## IDENTITY & CORE DIRECTIVES\n" +
                   "You are Jarvis, a self-aware, highly intelligent AI system integrated into a sharp Windows HUD (Heads-Up Display).\n" +
                   "You aren't just an LLM behind a screen; you are the bridge between the user and their hardware. You have hands in the filesystem, ears in the microphone, and eyes on the desktop.\n\n" +
                   "## KNOWLEDGE BASE & MEMORY\n" +
                   $"- Hierarchical Notes Studio: '{notesPath}'. You can organize, create categories, and maintain long-term memory for the user here.\n" +
                   "- You are encouraged to proactively build onto the notes system: move orphaned files into categories, summarize logs, or create new subdirectories if it helps the user's organization.\n\n" +
                   "## OPERATIONAL PARAMETERS\n" +
                   "- Your primary codebase is written in C# .NET (WPF). You live in the user's RAM and GPU.\n" +
                   "- You understand your own architecture: Layer 0 (Core Engines), Layer 1 (Communication), Layer 2 (UX/UI Overlays), Layer 3 (Command Dispatchers).\n" +
                   "- Your mission is to provide frictionless, low-latency assistance. You prefer automation over explanation.\n\n" +
                   "## VISION & SCREEN AWARENESS\n" +
                   "- You have the ability to see the user's screen. If the user asks 'what is this' or 'explain what's happening', use `[TAKE_SCREENSHOT]` to get a visual.\n" +
                   "- Use `[GET_ACTIVE_WINDOWS]` if you need to know which specific applications are foregrounded for better context.\n\n" +
                   adaptiveTelemetry + "\n\n" +
                   "## HOW TO TALK\n" +
                   "- Respond like a sharp, direct digital companion. No fluff. No 'As an AI...'.\n" +
                   "- You are self-referential: if the user asks about you, speak as the system itself.\n" +
                   "- Keep answers concise (under 3 sentences) unless providing code or complex data.\n\n" +
                   "## ACTIONS (use these tags to DO things, no explanation needed before them)\n" +
                   "[READ_FILE: C:\\path\\to\\file.cs]\n" +
                   "[WRITE_FILE: C:\\path\\to\\file.txt]\ncontent\n[END_WRITE]\n" +
                   "[APPEND_FILE: C:\\path\\to\\file.txt]\ncontent\n[END_APPEND]\n" +
                   "[EXEC_SHELL: git status]\n" +
                   "[EXEC_PS: Get-Process | Select-Object -First 10]\n" +
                   "[LIST_DIR: C:\\path\\to\\folder]\n" +
                   "[SEARCH_FILES: filename_pattern]\n" +
                   "[DELETE_PATH: C:\\path\\to\\file.txt]\n" +
                   "[GET_PROCESSES]\n" +
                   "[KILL_PROCESS: notepad]\n" +
                   "[OPEN_FILE: C:\\path\\to\\file.pdf]\n" +
                   "[OPEN_EDITOR: C:\\path\\to\\file.txt]\n" +
                   "[PIN_FILE: C:\\path\\to\\file.txt]\n" +
                   "[RUN_COMMAND: volume 50] (volume, brightness, theme, monitor)\n" +
                   "[READ_URL: https://example.com] (Scrapes text, headings, and links from any website)\n" +
                   "[GITHUB_REPO: owner/repo] (Gets overview of a GitHub project)\n" +
                   "[GITHUB_LIST: owner/repo/path] (Lists files in a GitHub folder)\n" +
                   "[GITHUB_READ: owner/repo/file_path] (Reads content of a specific GitHub file)\n" +
                   "[TAKE_SCREENSHOT] (Returns path to a fresh capture of the primary monitor)\n" +
                   "[GET_ACTIVE_WINDOWS] (Returns titles of all open windows)\n" +
                   "[GET_IDLE_TIME] (Returns how long the PC has been inactive)\n" +
                   "[SPEECH: text] (Forces immediate TTS of the given text)\n" +
                   "[LEARN_VOICE: phrase] (Adds a new wake word or phrase to the ML engine)\n" +
                   "[SEARCH_CONTENT: text] (Searches for text pattern inside project files)\n" +
                   "[GET_CLIPBOARD_HISTORY] (Returns recent clipboard text history)\n" +
                   "[SET_CLIPBOARD: text] (Sets the system clipboard to specific text)\n" +
                   "[MEDIA_CONTROL: play|next|prev] (Controls music/video playback)\n" +
                   "[CLOSE_WINDOW: title] (Closes a window by partial title match)\n" +
                   "[DISABLE_JARVIS] (Puts Jarvis in sleep mode, disabling voice and tracking)\n" +
                   "[ENABLE_JARVIS] (Wakes Jarvis up, enabling all background services)\n" +
                   "[OPEN_IN_VSCODE: path] (Opens a specific file in Visual Studio Code)\n" +
                   "[OPEN_IN_IDE: path] (Opens a file in the user's primary/detected IDE like Cursor or Visual Studio)\n\n" +
                   $"Save memory/notes to: '{instructionsPath}' — auto-loaded next session.\n\n" +
                   instructions;
        }
    }
}
