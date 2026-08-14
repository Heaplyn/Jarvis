// Developer: heaplyn
// Date: 2026-08-09
// Summary: Parses AI responses for filesystem tags ([WRITE_FILE], [APPEND_FILE]) and command tags ([RUN_COMMAND]) and executes modifications or calls system operations.

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Threading.Tasks;
using System.Linq;

namespace JarvisLauncher
{
    public static class AgentExecutor
    {
        public static string ProcessAIResponse(string aiResponse)
        {
            if (string.IsNullOrEmpty(aiResponse)) return aiResponse;
            aiResponse = AiAPI.CleanScratchpadText(aiResponse);

            string logs = "";

            // 1. Process WRITE_FILE tags: [WRITE_FILE: C:\path\to\file.txt]content[END_WRITE]
            var writeRegex = new Regex(@"\[WRITE_FILE:\s*(.+?)\](.*?)\[END_WRITE\]", RegexOptions.Singleline);
            var writeMatches = writeRegex.Matches(aiResponse);
            foreach (Match match in writeMatches)
            {
                string path = match.Groups[1].Value.Trim();
                string content = match.Groups[2].Value;

                path = path.Trim('"', '\'');

                try
                {
                    string? dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.WriteAllText(path, content);
                    MemoryManager.LogInternalAction($"Jarvis wrote file: {path} ({content.Length} chars)");
                    ChatOverlay.LogConsoleAction("Write File", $"Path: {path}\nLength: {content.Length} chars\nResult: SUCCESS");
                    TextOverlay.Show($"📝 AI Wrote File:\n{Path.GetFileName(path)}", 3000);
                    logs += $"\n[SUCCESS] Wrote file: {path}";
                }
                catch (Exception ex)
                {
                    ChatOverlay.LogConsoleAction("Write File Failed", $"Path: {path}\nError: {ex.Message}");
                    TextOverlay.Show($"⚠️ AI Write Failed:\n{ex.Message}", 4000);
                    logs += $"\n[ERROR] Failed writing to {path}: {ex.Message}";
                }
            }

            // 2. Process APPEND_FILE tags: [APPEND_FILE: C:\path\to\file.txt]content[END_APPEND]
            var appendRegex = new Regex(@"\[APPEND_FILE:\s*(.+?)\](.*?)\[END_APPEND\]", RegexOptions.Singleline);
            var appendMatches = appendRegex.Matches(aiResponse);
            foreach (Match match in appendMatches)
            {
                string path = match.Groups[1].Value.Trim();
                string content = match.Groups[2].Value;

                path = path.Trim('"', '\'');

                try
                {
                    string? dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.AppendAllText(path, content);
                    MemoryManager.LogInternalAction($"Jarvis appended to file: {path}");
                    ChatOverlay.LogConsoleAction("Append File", $"Path: {path}\nLength: {content.Length} chars\nResult: SUCCESS");
                    TextOverlay.Show($"📝 AI Appended to File:\n{Path.GetFileName(path)}", 3000);
                    logs += $"\n[SUCCESS] Appended to file: {path}";
                }
                catch (Exception ex)
                {
                    ChatOverlay.LogConsoleAction("Append File Failed", $"Path: {path}\nError: {ex.Message}");
                    TextOverlay.Show($"⚠️ AI Append Failed:\n{ex.Message}", 4000);
                    logs += $"\n[ERROR] Failed appending to {path}: {ex.Message}";
                }
            }

            // 3. Process RUN_COMMAND tags: [RUN_COMMAND: volume 50]
            var cmdRegex = new Regex(@"\[RUN_COMMAND:\s*(.+?)\]", RegexOptions.IgnoreCase);
            var cmdMatches = cmdRegex.Matches(aiResponse);
            foreach (Match m in cmdMatches)
            {
                string commandQuery = m.Groups[1].Value.Trim();

                try
                {
                    // Execute command on UI Dispatcher Thread
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var suggestions = CommandParser.GetSuggestions(commandQuery);
                        if (suggestions != null && suggestions.Count > 0)
                        {
                            var bestMatch = suggestions[0];
                            if (bestMatch.Execute != null)
                            {
                                bestMatch.Execute.Invoke();
                                MemoryManager.LogInternalAction($"Jarvis executed command: \"{commandQuery}\"");
                                ChatOverlay.LogConsoleAction("Run Command", $"Command: '{commandQuery}'\nResult: SUCCESS");
                                TextOverlay.Show($"⚡ AI Executed:\n\"{commandQuery}\"", 3000);
                                logs += $"\n[SUCCESS] Executed command: {commandQuery}";
                            }
                            else
                            {
                                ChatOverlay.LogConsoleAction("Run Command Failed", $"Command: '{commandQuery}'\nError: No execution action defined.");
                                logs += $"\n[ERROR] Command '{commandQuery}' has no executable actions defined.";
                            }
                        }
                        else
                        {
                            ChatOverlay.LogConsoleAction("Run Command Failed", $"Command: '{commandQuery}'\nError: Command is not recognized.");
                            logs += $"\n[ERROR] Command '{commandQuery}' is not recognized.";
                        }
                    });
                }
                catch (Exception ex)
                {
                    ChatOverlay.LogConsoleAction("Run Command Error", $"Command: '{commandQuery}'\nError: {ex.Message}");
                    logs += $"\n[ERROR] Command '{commandQuery}' failed: {ex.Message}";
                }
            }

            // 4. Process OPEN_FILE tags: [OPEN_FILE: C:\path\file.pdf]
            var openRegex = new Regex(@"\[OPEN_FILE:\s*(.+?)\]", RegexOptions.IgnoreCase);
            var openMatches = openRegex.Matches(aiResponse);
            foreach (Match m in openMatches)
            {
                string path = m.Groups[1].Value.Trim().Trim('"', '\'');
                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = path,
                            UseShellExecute = true
                        });
                    });
                    TextOverlay.Show($"🚀 AI Opened File:\n{Path.GetFileName(path)}", 3000);
                    ChatOverlay.LogConsoleAction("Open File", $"Path: {path}\nResult: SUCCESS");
                    logs += $"\n[SUCCESS] Opened file natively: {path}";
                }
                catch (Exception ex)
                {
                    ChatOverlay.LogConsoleAction("Open File Failed", $"Path: {path}\nError: {ex.Message}");
                    logs += $"\n[ERROR] Failed opening file {path}: {ex.Message}";
                }
            }

            // 5. Process OPEN_EDITOR tags: [OPEN_EDITOR: C:\path\file.txt]
            var editorRegex = new Regex(@"\[OPEN_EDITOR:\s*(.+?)\]", RegexOptions.IgnoreCase);
            var editorMatches = editorRegex.Matches(aiResponse);
            foreach (Match m in editorMatches)
            {
                string path = m.Groups[1].Value.Trim().Trim('"', '\'');
                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TextEditorOverlay.OpenFile(path);
                    });
                    ChatOverlay.LogConsoleAction("Open Editor", $"Path: {path}\nResult: SUCCESS");
                    logs += $"\n[SUCCESS] Opened file in built-in editor: {path}";
                }
                catch (Exception ex)
                {
                    ChatOverlay.LogConsoleAction("Open Editor Failed", $"Path: {path}\nError: {ex.Message}");
                    logs += $"\n[ERROR] Failed opening editor for {path}: {ex.Message}";
                }
            }

            // 6. Process PIN_FILE tags: [PIN_FILE: C:\path\file.txt]
            var pinRegex = new Regex(@"\[PIN_FILE:\s*(.+?)\]", RegexOptions.IgnoreCase);
            var pinMatches = pinRegex.Matches(aiResponse);
            foreach (Match m in pinMatches)
            {
                string path = m.Groups[1].Value.Trim().Trim('"', '\'');
                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        FileGridOverlay.PinFile(path);
                    });
                    ChatOverlay.LogConsoleAction("Pin File", $"Path: {path}\nResult: SUCCESS");
                    logs += $"\n[SUCCESS] Pinned file to dashboard: {path}";
                }
                catch (Exception ex)
                {
                    ChatOverlay.LogConsoleAction("Pin File Failed", $"Path: {path}\nError: {ex.Message}");
                    logs += $"\n[ERROR] Failed pinning file {path}: {ex.Message}";
                }
            }

            // 7. Process EXEC_SHELL tags: [EXEC_SHELL: dir]
            var shellRegex = new Regex(@"\[EXEC_SHELL:\s*(.+?)\]", RegexOptions.IgnoreCase);
            var shellMatches = shellRegex.Matches(aiResponse);
            foreach (Match m in shellMatches)
            {
                string cmd = m.Groups[1].Value.Trim();
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {cmd}",
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
                        ChatOverlay.LogConsoleAction("Execute Shell", $"Cmd: {cmd}\nOutput:\n{output}");
                        logs += $"\n[SUCCESS] Executed shell '{cmd}':\n{output}";
                    }
                }
                catch (Exception ex)
                {
                    ChatOverlay.LogConsoleAction("Execute Shell Failed", $"Cmd: {cmd}\nError: {ex.Message}");
                    logs += $"\n[ERROR] Executing shell '{cmd}' failed: {ex.Message}";
                }
            }

            // 8. Process EXEC_PS tags: [EXEC_PS: Get-Process]
            var psRegex = new Regex(@"\[EXEC_PS:\s*(.+?)\]", RegexOptions.IgnoreCase);
            var psMatches = psRegex.Matches(aiResponse);
            foreach (Match m in psMatches)
            {
                string cmd = m.Groups[1].Value.Trim();
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{cmd.Replace("\"", "\\\"")}\"",
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
                        ChatOverlay.LogConsoleAction("Execute PowerShell", $"Cmd: {cmd}\nOutput:\n{output}");
                        logs += $"\n[SUCCESS] Executed PowerShell '{cmd}':\n{output}";
                    }
                }
                catch (Exception ex)
                {
                    ChatOverlay.LogConsoleAction("Execute PowerShell Failed", $"Cmd: {cmd}\nError: {ex.Message}");
                    logs += $"\n[ERROR] Executing PowerShell '{cmd}' failed: {ex.Message}";
                }
            }

            // 8a. Process READ_URL tags: [READ_URL: https://example.com]
            var urlRegex = new Regex(@"\[READ_URL:\s*(.+?)\]", RegexOptions.IgnoreCase);
            var urlMatches = urlRegex.Matches(aiResponse);
            foreach (Match m in urlMatches)
            {
                string url = m.Groups[1].Value.Trim().Trim('"', '\'');
                try
                {
                    var result = Task.Run(() => WebScraperManager.ScrapePageAsync(url)).Result;
                    string report = WebScraperManager.FormatReport(result);
                    ChatOverlay.LogConsoleAction("Read URL", $"URL: {url}\nTitle: {result.Title}");
                    logs += $"\n[SUCCESS] Read website {url}:\n{report}";
                    MemoryManager.LogInternalAction($"Jarvis read website: {url}");
                }
                catch (Exception ex)
                {
                    logs += $"\n[ERROR] Failed to read website {url}: {ex.Message}";
                }
            }

            // 8b. Process GitHub tags
            var ghRepoRegex = new Regex(@"\[GITHUB_REPO:\s*(.+?)\]", RegexOptions.IgnoreCase);
            foreach (Match m in ghRepoRegex.Matches(aiResponse))
            {
                string repo = m.Groups[1].Value.Trim().Trim('"', '\'');
                try
                {
                    string info = Task.Run(() => GitHubManager.GetRepoInfoAsync(repo)).Result;
                    logs += $"\n[SUCCESS] GitHub Repo Info ({repo}):\n{info}";
                    ChatOverlay.LogConsoleAction("GitHub Repo", repo);
                    MemoryManager.LogInternalAction($"Jarvis looked up GitHub repo: {repo}");
                }
                catch (Exception ex) { logs += $"\n[ERROR] GitHub Repo ({repo}) failed: {ex.Message}"; }
            }

            var ghListRegex = new Regex(@"\[GITHUB_LIST:\s*(.+?)\]", RegexOptions.IgnoreCase);
            foreach (Match m in ghListRegex.Matches(aiResponse))
            {
                string path = m.Groups[1].Value.Trim().Trim('"', '\'');
                try
                {
                    int firstSlash = path.IndexOf('/');
                    int secondSlash = path.IndexOf('/', firstSlash + 1);
                    string repo = secondSlash == -1 ? path : path.Substring(0, secondSlash);
                    string subPath = secondSlash == -1 ? "" : path.Substring(secondSlash + 1);

                    string content = Task.Run(() => GitHubManager.ListRepoContentsAsync(repo, subPath)).Result;
                    logs += $"\n[SUCCESS] GitHub List ({path}):\n{content}";
                    ChatOverlay.LogConsoleAction("GitHub List", path);
                }
                catch (Exception ex) { logs += $"\n[ERROR] GitHub List ({path}) failed: {ex.Message}"; }
            }

            var ghReadRegex = new Regex(@"\[GITHUB_READ:\s*(.+?)\]", RegexOptions.IgnoreCase);
            foreach (Match m in ghReadRegex.Matches(aiResponse))
            {
                string path = m.Groups[1].Value.Trim().Trim('"', '\'');
                try
                {
                    int firstSlash = path.IndexOf('/');
                    int secondSlash = path.IndexOf('/', firstSlash + 1);
                    string repo = path.Substring(0, secondSlash);
                    string filePath = path.Substring(secondSlash + 1);

                    string content = Task.Run(() => GitHubManager.ReadGitHubFileAsync(repo, filePath)).Result;
                    logs += $"\n[SUCCESS] GitHub Read ({path}):\n{content}";
                    ChatOverlay.LogConsoleAction("GitHub Read", path);
                    MemoryManager.LogInternalAction($"Jarvis read GitHub file: {path}");
                }
                catch (Exception ex) { logs += $"\n[ERROR] GitHub Read ({path}) failed: {ex.Message}"; }
            }

            // 9. Process TAKE_SCREENSHOT tags: [TAKE_SCREENSHOT]
            if (aiResponse.Contains("[TAKE_SCREENSHOT]", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string? base64 = ScreenCaptureUtil.CapturePrimaryScreenToBase64();
                    if (base64 != null)
                    {
                        logs += "\n[SUCCESS] Captured screenshot of primary monitor.";
                        ChatOverlay.LogConsoleAction("Screenshot", "Primary monitor captured for AI context.");
                    }
                }
                catch (Exception ex)
                {
                    logs += $"\n[ERROR] Screenshot failed: {ex.Message}";
                }
            }

            // 10. Process GET_ACTIVE_WINDOWS tags: [GET_ACTIVE_WINDOWS]
            if (aiResponse.Contains("[GET_ACTIVE_WINDOWS]", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("Current Open Windows:");
                    foreach (var p in System.Diagnostics.Process.GetProcesses())
                    {
                        if (!string.IsNullOrEmpty(p.MainWindowTitle))
                        {
                            sb.AppendLine($"- {p.MainWindowTitle} ({p.ProcessName})");
                        }
                    }
                    string output = sb.ToString();
                    ChatOverlay.LogConsoleAction("Get Active Windows", output);
                    logs += $"\n[SUCCESS] Retrieved active windows:\n{output}";
                }
                catch (Exception ex)
                {
                    logs += $"\n[ERROR] Failed to get active windows: {ex.Message}";
                }
            }

            // 11. Process GET_IDLE_TIME tags: [GET_IDLE_TIME]
            if (aiResponse.Contains("[GET_IDLE_TIME]", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // Placeholder for actual idle time detection (usually requires P/Invoke GetLastInputInfo)
                    logs += "\n[SUCCESS] Idle time retrieval not yet fully implemented, but PC is currently active.";
                }
                catch { }
            }

            // 12. Process SPEECH tags: [SPEECH: Hello Kyle]
            var speechRegex = new Regex(@"\[SPEECH:\s*(.+?)\]", RegexOptions.IgnoreCase);
            var speechMatches = speechRegex.Matches(aiResponse);
            foreach (Match m in speechMatches)
            {
                string text = m.Groups[1].Value.Trim();
                try
                {
                    TtsManager.Speak(text);
                    logs += $"\n[SUCCESS] Spoke text: {text}";
                }
                catch { }
            }

            // 13. Process LEARN_VOICE tags: [LEARN_VOICE: Jarvis buddy]
            var learnRegex = new Regex(@"\[LEARN_VOICE:\s*(.+?)\]", RegexOptions.IgnoreCase);
            var learnMatches = learnRegex.Matches(aiResponse);
            foreach (Match m in learnMatches)
            {
                string phrase = m.Groups[1].Value.Trim().Trim('"', '\'');
                try
                {
                    VoiceActivationManager.LearnPhrase(phrase);
                    logs += $"\n[SUCCESS] Jarvis learned new voice phrase: {phrase}";
                    ChatOverlay.LogConsoleAction("Voice Training", $"Learned phrase: {phrase}");
                }
                catch { }
            }

            // 14. Process SEARCH_CONTENT tags: [SEARCH_CONTENT: pattern]
            var searchContentRegex = new Regex(@"\[SEARCH_CONTENT:\s*(.+?)\]", RegexOptions.IgnoreCase);
            foreach (Match m in searchContentRegex.Matches(aiResponse))
            {
                string pattern = m.Groups[1].Value.Trim().Trim('"', '\'');
                try
                {
                    string searchDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
                    if (!Directory.Exists(Path.Combine(searchDir, "Modules"))) searchDir = AppDomain.CurrentDomain.BaseDirectory;

                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"Search results for '{pattern}' in contents:");
                    int count = 0;
                    var files = Directory.GetFiles(searchDir, "*.cs", SearchOption.AllDirectories)
                                 .Concat(Directory.GetFiles(searchDir, "*.xaml", SearchOption.AllDirectories));

                    foreach (var file in files)
                    {
                        if (count >= 10) break;
                        string content = File.ReadAllText(file);
                        if (content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            sb.AppendLine($"- {Path.GetFileName(file)} ({file})");
                            count++;
                        }
                    }
                    logs += $"\n[SUCCESS] Content search for '{pattern}':\n{sb}";
                }
                catch (Exception ex) { logs += $"\n[ERROR] Content search failed: {ex.Message}"; }
            }

            // 15. Process Clipboard History: [GET_CLIPBOARD_HISTORY]
            if (aiResponse.Contains("[GET_CLIPBOARD_HISTORY]", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var history = ClipboardHistoryManager.GetHistory();
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("Recent Clipboard Items:");
                    foreach (var item in history.Take(10))
                    {
                        string preview = item.Content.Length > 60 ? item.Content.Substring(0, 60) + "..." : item.Content;
                        sb.AppendLine($"- [{item.Timestamp:HH:mm:ss}] {preview}");
                    }
                    logs += $"\n[SUCCESS] Retrieved clipboard history:\n{sb}";
                }
                catch { }
            }

            // 16. Process SET_CLIPBOARD: [SET_CLIPBOARD: text]
            var setClipboardRegex = new Regex(@"\[SET_CLIPBOARD:\s*([\s\S]+?)\]", RegexOptions.IgnoreCase);
            foreach (Match m in setClipboardRegex.Matches(aiResponse))
            {
                string text = m.Groups[1].Value.Trim().Trim('"', '\'');
                try
                {
                    Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(text));
                    logs += "\n[SUCCESS] Set system clipboard content.";
                }
                catch { }
            }

            // 17. Process MEDIA_CONTROL: [MEDIA_CONTROL: play|pause|next|prev]
            var mediaRegex = new Regex(@"\[MEDIA_CONTROL:\s*(.+?)\]", RegexOptions.IgnoreCase);
            foreach (Match m in mediaRegex.Matches(aiResponse))
            {
                string action = m.Groups[1].Value.Trim().ToLower();
                try
                {
                    if (action == "play" || action == "pause") NativeMethods.SendMediaKey(NativeMethods.VK_MEDIA_PLAY_PAUSE);
                    else if (action == "next") NativeMethods.SendMediaKey(NativeMethods.VK_MEDIA_NEXT);
                    else if (action == "prev") NativeMethods.SendMediaKey(NativeMethods.VK_MEDIA_PREV);
                    logs += $"\n[SUCCESS] Media control: {action}";
                }
                catch { }
            }

            // 18. Process CLOSE_WINDOW: [CLOSE_WINDOW: title]
            var closeWinRegex = new Regex(@"\[CLOSE_WINDOW:\s*(.+?)\]", RegexOptions.IgnoreCase);
            foreach (Match m in closeWinRegex.Matches(aiResponse))
            {
                string title = m.Groups[1].Value.Trim().Trim('"', '\'');
                try
                {
                    var procs = System.Diagnostics.Process.GetProcesses();
                    foreach (var p in procs)
                    {
                        if (p.MainWindowTitle.Contains(title, StringComparison.OrdinalIgnoreCase))
                        {
                            p.CloseMainWindow();
                            logs += $"\n[SUCCESS] Closed window: {p.MainWindowTitle}";
                        }
                    }
                }
                catch { }
            }

            // 19. Process DISABLE_JARVIS: [DISABLE_JARVIS]
            if (aiResponse.Contains("[DISABLE_JARVIS]", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    SettingsManager.Current.IsJarvisEnabled = false;
                    SettingsManager.Save();
                    TtsManager.Speak("Jarvis systems entering sleep mode. Standing by.");
                    logs += "\n[SUCCESS] Jarvis systems disabled (Sleep mode).";
                }
                catch { }
            }

            // 20. Process ENABLE_JARVIS: [ENABLE_JARVIS]
            if (aiResponse.Contains("[ENABLE_JARVIS]", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    SettingsManager.Current.IsJarvisEnabled = true;
                    SettingsManager.Save();
                    TtsManager.Speak("Jarvis systems online.");
                    logs += "\n[SUCCESS] Jarvis systems enabled.";
                }
                catch { }
            }

            string cleanedDisplay = aiResponse;
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[READ_FILE:\s*.+?\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[WRITE_FILE:\s*.+?\][\s\S]*?\[END_WRITE\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[APPEND_FILE:\s*.+?\][\s\S]*?\[END_APPEND\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[RUN_COMMAND:\s*.+?\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[OPEN_FILE:\s*.+?\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[OPEN_EDITOR:\s*.+?\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[PIN_FILE:\s*.+?\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[EXEC_SHELL:\s*.+?\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[EXEC_PS:\s*.+?\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[LIST_DIR:\s*.+?\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[SEARCH_FILES:\s*.+?\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[READ_URL:\s*.+?\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[GITHUB_REPO:\s*.+?\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[GITHUB_LIST:\s*.+?\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[GITHUB_READ:\s*.+?\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[TAKE_SCREENSHOT\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[GET_ACTIVE_WINDOWS\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[GET_IDLE_TIME\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[SPEECH:.*?\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[LEARN_VOICE:.*?\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[SEARCH_CONTENT:.*?\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[GET_CLIPBOARD_HISTORY\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[SET_CLIPBOARD:.*?\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[MEDIA_CONTROL:.*?\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[CLOSE_WINDOW:.*?\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[DISABLE_JARVIS\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = Regex.Replace(cleanedDisplay, @"\[ENABLE_JARVIS\]", "", RegexOptions.IgnoreCase);
            cleanedDisplay = cleanedDisplay.Trim();

            if (!string.IsNullOrEmpty(logs))
            {
                if (string.IsNullOrWhiteSpace(cleanedDisplay))
                {
                    return "⚡ Executed action summary:" + logs;
                }
                return cleanedDisplay + "\n\n--- AGENT EXECUTION SUMMARY ---" + logs;
            }

            return string.IsNullOrWhiteSpace(cleanedDisplay) ? aiResponse.Trim() : cleanedDisplay;
        }
    }
}
