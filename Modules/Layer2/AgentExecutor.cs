// Developer: heaplyn
// Date: 2026-08-09
// Summary: Parses AI responses for filesystem tags ([WRITE_FILE], [APPEND_FILE]) and command tags ([RUN_COMMAND]) and executes modifications or calls system operations.

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace JarvisLauncher
{
    public static class AgentExecutor
    {
        public static string ProcessAIResponse(string aiResponse)
        {
            if (string.IsNullOrEmpty(aiResponse)) return aiResponse;

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

            if (!string.IsNullOrEmpty(logs))
            {
                return aiResponse + "\n\n--- AGENT EXECUTION SUMMARY ---\n" + logs;
            }

            return aiResponse;
        }
    }
}
