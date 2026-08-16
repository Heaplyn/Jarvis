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
using System.Threading;

namespace JarvisLauncher
{
    public class ChatTurn
    {
        public string Role { get; set; } = "user"; // "user" or "model"
        public string Text { get; set; } = string.Empty;
        public GeminiUsage? Usage { get; set; }
    }

    public class GeminiUsage
    {
        public int PromptTokens { get; set; }
        public int ResponseTokens { get; set; }
        public int TotalTokens { get; set; }
    }

    public static class AiAPI
    {
        private static readonly HttpClient Client = new HttpClient();

        public static async Task<string> AskGemini(string Prompt, List<ChatTurn>? History = null, CancellationToken ct = default)
        {
            return await AskGeminiInternal(Prompt, null, null, History, ct);
        }

        public static async Task<string> AnalyzeImageAsync(string Prompt, string Base64Image, CancellationToken ct = default)
        {
            return await AskGeminiInternal(Prompt, Base64Image, null, null, ct);
        }

        public static async Task<string> AnalyzeImageBase64Async(string Prompt, string Base64Image, string MimeType = "image/png", CancellationToken ct = default)
        {
            return await AskGeminiInternal(Prompt, Base64Image, null, null, ct);
        }

        public static async Task<string> AnalyzeAudioAsync(string Prompt, string Base64Audio, CancellationToken ct = default)
        {
            return await AskGeminiInternal(Prompt, null, Base64Audio, null, ct);
        }

        private static async Task<string> AskGeminiInternal(string Prompt, string? Base64Image = null, string? Base64Audio = null, List<ChatTurn>? History = null, CancellationToken ct = default)
        {
            string ApiKey = SettingsManager.Current.GOOGLE_AI_KEY;
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                return "Error: Gemini API Key is not set. Use 'setkey google <your_key>' to configure it.";
            }

            // Sanitize incoming prompt to prevent double-contexting if it's a retry or recursive call
            string CurrentPrompt = SanitizeText(Prompt);

            // Inject User Activity, Active Window, & Command History Context
            string ActivityContext = UserActivityContextManager.BuildFullActivityContext();
            CurrentPrompt = $"{ActivityContext}\nUser Query: {CurrentPrompt}";

            string LastResponse = "";
            string LastToolOutput = "";
            int LoopLimit = 5;
            var ExecutedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int I = 0; I < LoopLimit; I++)
            {
                ct.ThrowIfCancellationRequested();
                string Response = await QueryGeminiRaw(CurrentPrompt, ApiKey, Base64Image, Base64Audio, History, ct);

                // For simple verification queries (binary yes/no), skip complex sanitization to avoid nuking the answer
                if (Prompt.Contains("Answer ONLY 'YES' or 'NO'") || Prompt.Contains("Answer YES or NO"))
                {
                    LastResponse = Response.Trim();
                    break;
                }

                // CRITICAL: Robustly sanitize the response before storing or feeding back
                string CleanedResp = SanitizeText(Response);
                if (!string.IsNullOrWhiteSpace(CleanedResp))
                {
                    LastResponse = CleanedResp;
                }

                var ExecutionFeedBuilder = new StringBuilder();
                int NewExecutionsCount = 0;

                // 1. Check for [READ_FILE: path] tags
                var ReadRegex = new Regex(@"\[READ_FILE:\s*(.+?)\]");
                var ReadMatches = ReadRegex.Matches(Response);
                foreach (Match Match in ReadMatches)
                {
                    string Path = Match.Groups[1].Value.Trim().Trim('"', '\'');
                    string TagKey = $"READ:{Path}";
                    if (ExecutedTags.Contains(TagKey)) continue; // Prevent loop on identical file reads
                    ExecutedTags.Add(TagKey);
                    NewExecutionsCount++;

                    try
                    {
                        if (File.Exists(Path))
                        {
                            string FileText = File.ReadAllText(Path);
                            ExecutionFeedBuilder.AppendLine($"[FILE_CONTENT: {Path}]");
                            ExecutionFeedBuilder.AppendLine(FileText);
                            ExecutionFeedBuilder.AppendLine("[END_FILE_CONTENT]");

                            // Save exact file content to guarantee it is displayed to the user
                            LastToolOutput = $"📄 **{System.IO.Path.GetFileName(Path)}**:\n\n{FileText}";
                        }
                        else
                        {
                            ExecutionFeedBuilder.AppendLine($"[FILE_CONTENT: {Path}]");
                            ExecutionFeedBuilder.AppendLine("Error: File not found.");
                            ExecutionFeedBuilder.AppendLine("[END_FILE_CONTENT]");
                            LastToolOutput = $"⚠️ File not found: {Path}";
                        }
                    }
                    catch (Exception Ex)
                    {
                        ExecutionFeedBuilder.AppendLine($"[FILE_CONTENT: {Path}]");
                        ExecutionFeedBuilder.AppendLine($"Error reading file: {Ex.Message}");
                        ExecutionFeedBuilder.AppendLine("[END_FILE_CONTENT]");
                        LastToolOutput = $"⚠️ Error reading file: {Ex.Message}";
                    }
                }

                // 2. Check for [EXEC_SHELL: cmd] tags
                var ShellRegex = new Regex(@"\[EXEC_SHELL:\s*(.+?)\]", RegexOptions.IgnoreCase);
                var ShellMatches = ShellRegex.Matches(Response);
                foreach (Match Match in ShellMatches)
                {
                    string ShellCmd = Match.Groups[1].Value.Trim();
                    string TagKey = $"SHELL:{ShellCmd}";
                    if (ExecutedTags.Contains(TagKey)) continue; // Prevent loop on identical shell executions
                    ExecutedTags.Add(TagKey);
                    NewExecutionsCount++;

                    try
                    {
                        var Psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c {ShellCmd}",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var Proc = System.Diagnostics.Process.Start(Psi);
                        if (Proc != null)
                        {
                            string OutText = Proc.StandardOutput.ReadToEnd();
                            string ErrText = Proc.StandardError.ReadToEnd();
                            Proc.WaitForExit(5000);
                            string Output = (OutText + "\n" + ErrText).Trim();
                            ExecutionFeedBuilder.AppendLine($"[SHELL_OUTPUT: {ShellCmd}]");
                            ExecutionFeedBuilder.AppendLine(string.IsNullOrWhiteSpace(Output) ? "(Command executed cleanly with no output)" : Output);
                            ExecutionFeedBuilder.AppendLine("[END_SHELL_OUTPUT]");

                            LastToolOutput = $"⚡ **Shell Output ({ShellCmd})**:\n```\n{Output}\n```";
                        }
                    }
                    catch (Exception Ex)
                    {
                        ExecutionFeedBuilder.AppendLine($"[SHELL_OUTPUT: {ShellCmd}]");
                        ExecutionFeedBuilder.AppendLine($"Error executing command: {Ex.Message}");
                        ExecutionFeedBuilder.AppendLine("[END_SHELL_OUTPUT]");
                        LastToolOutput = $"⚠️ Error executing shell: {Ex.Message}";
                    }
                }

                // 3. Check for [EXEC_PS: cmd] tags (PowerShell)
                var PsRegex = new Regex(@"\[EXEC_PS:\s*(.+?)\]", RegexOptions.IgnoreCase);
                var PsMatches = PsRegex.Matches(Response);
                foreach (Match Match in PsMatches)
                {
                    string PsCmd = Match.Groups[1].Value.Trim();
                    string TagKey = $"PS:{PsCmd}";
                    if (ExecutedTags.Contains(TagKey)) continue;
                    ExecutedTags.Add(TagKey);
                    NewExecutionsCount++;

                    try
                    {
                        var Psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{PsCmd.Replace("\"", "\\\"")}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var Proc = System.Diagnostics.Process.Start(Psi);
                        if (Proc != null)
                        {
                            string OutText = Proc.StandardOutput.ReadToEnd();
                            string ErrText = Proc.StandardError.ReadToEnd();
                            Proc.WaitForExit(7000);
                            string Output = (OutText + "\n" + ErrText).Trim();
                            ExecutionFeedBuilder.AppendLine($"[POWERSHELL_OUTPUT: {PsCmd}]");
                            ExecutionFeedBuilder.AppendLine(string.IsNullOrWhiteSpace(Output) ? "(PowerShell executed cleanly with no output)" : Output);
                            ExecutionFeedBuilder.AppendLine("[END_POWERSHELL_OUTPUT]");

                            LastToolOutput = $"⚡ **PowerShell Output ({PsCmd})**:\n```powershell\n{Output}\n```";
                        }
                    }
                    catch (Exception Ex)
                    {
                        ExecutionFeedBuilder.AppendLine($"[POWERSHELL_OUTPUT: {PsCmd}]");
                        ExecutionFeedBuilder.AppendLine($"Error executing PowerShell: {Ex.Message}");
                        ExecutionFeedBuilder.AppendLine("[END_POWERSHELL_OUTPUT]");
                        LastToolOutput = $"⚠️ PowerShell Error: {Ex.Message}";
                    }
                }

                // 4. Check for [LIST_DIR: path] tags
                var ListDirRegex = new Regex(@"\[LIST_DIR:\s*(.+?)\]", RegexOptions.IgnoreCase);
                var ListDirMatches = ListDirRegex.Matches(Response);
                foreach (Match Match in ListDirMatches)
                {
                    string DirPath = Match.Groups[1].Value.Trim().Trim('"', '\'');
                    string TagKey = $"LIST_DIR:{DirPath}";
                    if (ExecutedTags.Contains(TagKey)) continue;
                    ExecutedTags.Add(TagKey);
                    NewExecutionsCount++;

                    try
                    {
                        if (Directory.Exists(DirPath))
                        {
                            var Entries = Directory.GetFileSystemEntries(DirPath);
                            var Sb = new StringBuilder();
                            Sb.AppendLine($"Contents of directory '{DirPath}':");
                            int Count = 0;
                            foreach (var Entry in Entries)
                            {
                                if (Count++ > 60) { Sb.AppendLine("... (truncated)"); break; }
                                bool IsDir = Directory.Exists(Entry);
                                var Info = IsDir ? (FileSystemInfo)new DirectoryInfo(Entry) : new FileInfo(Entry);
                                Sb.AppendLine($"{(IsDir ? "[DIR]" : "[FILE]")} {Info.Name} (Modified: {Info.LastWriteTime:yyyy-MM-dd HH:mm})");
                            }
                            string Output = Sb.ToString();
                            ExecutionFeedBuilder.AppendLine($"[DIR_LIST: {DirPath}]\n{Output}\n[END_DIR_LIST]");
                            LastToolOutput = $"📁 **Folder Contents ({System.IO.Path.GetFileName(DirPath)})**:\n```\n{Output}\n```";
                        }
                        else
                        {
                            ExecutionFeedBuilder.AppendLine($"[DIR_LIST: {DirPath}]\nDirectory not found.\n[END_DIR_LIST]");
                            LastToolOutput = $"⚠️ Directory not found: {DirPath}";
                        }
                    }
                    catch (Exception Ex)
                    {
                        ExecutionFeedBuilder.AppendLine($"[DIR_LIST: {DirPath}]\nError listing directory: {Ex.Message}\n[END_DIR_LIST]");
                    }
                }

                // 5. Check for [SEARCH_FILES: pattern] tags
                var SearchRegex = new Regex(@"\[SEARCH_FILES:\s*(.+?)\]", RegexOptions.IgnoreCase);
                var SearchMatches = SearchRegex.Matches(Response);
                foreach (Match Match in SearchMatches)
                {
                    string SearchPattern = Match.Groups[1].Value.Trim().Trim('"', '\'');
                    string TagKey = $"SEARCH:{SearchPattern}";
                    if (ExecutedTags.Contains(TagKey)) continue;
                    ExecutedTags.Add(TagKey);
                    NewExecutionsCount++;

                    try
                    {
                        string SearchDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
                        if (!Directory.Exists(System.IO.Path.Combine(SearchDir, "Modules"))) SearchDir = AppDomain.CurrentDomain.BaseDirectory;

                        var FoundFiles = Directory.GetFiles(SearchDir, $"*{SearchPattern}*", SearchOption.AllDirectories);
                        var Sb = new StringBuilder();
                        Sb.AppendLine($"Matching files for '{SearchPattern}':");
                        int Count = 0;
                        foreach (var File in FoundFiles)
                        {
                            if (Count++ > 30) { Sb.AppendLine("... (truncated)"); break; }
                            Sb.AppendLine(File);
                        }
                        string Output = Sb.ToString();
                        ExecutionFeedBuilder.AppendLine($"[SEARCH_RESULTS: {SearchPattern}]\n{Output}\n[END_SEARCH_RESULTS]");
                        LastToolOutput = $"🔍 **Search Results for '{SearchPattern}'**:\n```\n{Output}\n```";
                    }
                    catch (Exception Ex)
                    {
                        ExecutionFeedBuilder.AppendLine($"[SEARCH_RESULTS: {SearchPattern}]\nError searching: {Ex.Message}\n[END_SEARCH_RESULTS]");
                    }
                }

                // 6. Check for [WRITE_FILE: path]content[END_WRITE] tags
                var WriteRegex = new Regex(@"\[WRITE_FILE:\s*(.+?)\](.*?)\[END_WRITE\]", RegexOptions.Singleline);
                var WriteMatches = WriteRegex.Matches(Response);
                foreach (Match Match in WriteMatches)
                {
                    string Path = Match.Groups[1].Value.Trim().Trim('"', '\'');
                    string Content = Match.Groups[2].Value;
                    string TagKey = $"WRITE:{Path}:{Content.GetHashCode()}";
                    if (ExecutedTags.Contains(TagKey)) continue;
                    ExecutedTags.Add(TagKey);
                    NewExecutionsCount++;

                    try
                    {
                        string? Dir = System.IO.Path.GetDirectoryName(Path);
                        if (!string.IsNullOrEmpty(Dir) && !Directory.Exists(Dir))
                        {
                            Directory.CreateDirectory(Dir);
                        }
                        File.WriteAllText(Path, Content);
                        ExecutionFeedBuilder.AppendLine($"[WRITE_FILE_OUTPUT: {Path}]\nFile written successfully ({Content.Length} characters).\n[END_WRITE_FILE_OUTPUT]");
                        LastToolOutput = $"📝 **Wrote File ({System.IO.Path.GetFileName(Path)})**";
                        
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ChatOverlay.LogConsoleAction("Write File", $"Path: {Path}\nLength: {Content.Length} chars\nResult: SUCCESS");
                        });
                    }
                    catch (Exception Ex)
                    {
                        ExecutionFeedBuilder.AppendLine($"[WRITE_FILE_OUTPUT: {Path}]\nError writing file: {Ex.Message}\n[END_WRITE_FILE_OUTPUT]");
                    }
                }

                // 7. Check for [APPEND_FILE: path]content[END_APPEND] tags
                var AppendRegex = new Regex(@"\[APPEND_FILE:\s*(.+?)\](.*?)\[END_APPEND\]", RegexOptions.Singleline);
                var AppendMatches = AppendRegex.Matches(Response);
                foreach (Match Match in AppendMatches)
                {
                    string Path = Match.Groups[1].Value.Trim().Trim('"', '\'');
                    string Content = Match.Groups[2].Value;
                    string TagKey = $"APPEND:{Path}:{Content.GetHashCode()}";
                    if (ExecutedTags.Contains(TagKey)) continue;
                    ExecutedTags.Add(TagKey);
                    NewExecutionsCount++;

                    try
                    {
                        string? Dir = System.IO.Path.GetDirectoryName(Path);
                        if (!string.IsNullOrEmpty(Dir) && !Directory.Exists(Dir))
                        {
                            Directory.CreateDirectory(Dir);
                        }
                        File.AppendAllText(Path, Content);
                        ExecutionFeedBuilder.AppendLine($"[APPEND_FILE_OUTPUT: {Path}]\nFile appended successfully ({Content.Length} characters).\n[END_APPEND_FILE_OUTPUT]");
                        LastToolOutput = $"📝 **Appended to File ({System.IO.Path.GetFileName(Path)})**";

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ChatOverlay.LogConsoleAction("Append File", $"Path: {Path}\nLength: {Content.Length} chars\nResult: SUCCESS");
                        });
                    }
                    catch (Exception Ex)
                    {
                        ExecutionFeedBuilder.AppendLine($"[APPEND_FILE_OUTPUT: {Path}]\nError appending to file: {Ex.Message}\n[END_APPEND_FILE_OUTPUT]");
                    }
                }

                // 8. Check for [DELETE_PATH: path] tags
                var DeleteRegex = new Regex(@"\[DELETE_PATH:\s*(.+?)\]", RegexOptions.IgnoreCase);
                var DeleteMatches = DeleteRegex.Matches(Response);
                foreach (Match Match in DeleteMatches)
                {
                    string Path = Match.Groups[1].Value.Trim().Trim('"', '\'');
                    string TagKey = $"DELETE:{Path}";
                    if (ExecutedTags.Contains(TagKey)) continue;
                    ExecutedTags.Add(TagKey);
                    NewExecutionsCount++;

                    try
                    {
                        if (File.Exists(Path))
                        {
                            File.Delete(Path);
                            ExecutionFeedBuilder.AppendLine($"[DELETE_OUTPUT: {Path}]\nFile deleted successfully.\n[END_DELETE_OUTPUT]");
                            LastToolOutput = $"🗑️ **Deleted File ({System.IO.Path.GetFileName(Path)})**";
                        }
                        else if (Directory.Exists(Path))
                        {
                            Directory.Delete(Path, true);
                            ExecutionFeedBuilder.AppendLine($"[DELETE_OUTPUT: {Path}]\nDirectory deleted recursively successfully.\n[END_DELETE_OUTPUT]");
                            LastToolOutput = $"🗑️ **Deleted Directory ({System.IO.Path.GetFileName(Path)})**";
                        }
                        else
                        {
                            ExecutionFeedBuilder.AppendLine($"[DELETE_OUTPUT: {Path}]\nPath not found.\n[END_DELETE_OUTPUT]");
                        }
                    }
                    catch (Exception Ex)
                    {
                        ExecutionFeedBuilder.AppendLine($"[DELETE_OUTPUT: {Path}]\nError deleting path: {Ex.Message}\n[END_DELETE_OUTPUT]");
                    }
                }

                // 9. Check for [GET_PROCESSES] tags
                if (Response.Contains("[GET_PROCESSES]", StringComparison.OrdinalIgnoreCase))
                {
                    string TagKey = "GET_PROCESSES";
                    if (!ExecutedTags.Contains(TagKey))
                    {
                        ExecutedTags.Add(TagKey);
                        NewExecutionsCount++;

                        try
                        {
                            var Sb = new StringBuilder();
                            Sb.AppendLine("Active processes list:");
                            var Processes = System.Diagnostics.Process.GetProcesses();
                            int Count = 0;
                            foreach (var P in Processes)
                            {
                                try
                                {
                                    if (Count++ > 50) { Sb.AppendLine("... (truncated)"); break; }
                                    Sb.AppendLine($"- {P.ProcessName} (ID: {P.Id}, WorkingSet: {P.WorkingSet64 / 1024 / 1024}MB)");
                                }
                                catch { }
                            }
                            string Output = Sb.ToString();
                            ExecutionFeedBuilder.AppendLine($"[PROCESSES_OUTPUT]\n{Output}\n[END_PROCESSES_OUTPUT]");
                            LastToolOutput = "🖥️ **Listed system processes**";
                        }
                        catch (Exception Ex)
                        {
                            ExecutionFeedBuilder.AppendLine($"[PROCESSES_OUTPUT]\nError listing processes: {Ex.Message}\n[END_PROCESSES_OUTPUT]");
                        }
                    }
                }

                // 10. Check for [KILL_PROCESS: target] tags
                var KillRegex = new Regex(@"\[KILL_PROCESS:\s*(.+?)\]", RegexOptions.IgnoreCase);
                var KillMatches = KillRegex.Matches(Response);
                foreach (Match Match in KillMatches)
                {
                    string Target = Match.Groups[1].Value.Trim().Trim('"', '\'');
                    string TagKey = $"KILL:{Target}";
                    if (ExecutedTags.Contains(TagKey)) continue;
                    ExecutedTags.Add(TagKey);
                    NewExecutionsCount++;

                    try
                    {
                        bool Success = false;
                        if (int.TryParse(Target, out int Pid))
                        {
                            var P = System.Diagnostics.Process.GetProcessById(Pid);
                            P.Kill();
                            Success = true;
                        }
                        else
                        {
                            var Procs = System.Diagnostics.Process.GetProcessesByName(Target);
                            foreach (var P in Procs)
                            {
                                P.Kill();
                                Success = true;
                            }
                        }
                        
                        if (Success)
                        {
                            ExecutionFeedBuilder.AppendLine($"[KILL_OUTPUT: {Target}]\nProcess terminated successfully.\n[END_KILL_OUTPUT]");
                            LastToolOutput = $"🛑 **Terminated process: {Target}**";
                        }
                        else
                        {
                            ExecutionFeedBuilder.AppendLine($"[KILL_OUTPUT: {Target}]\nNo matching processes found.\n[END_KILL_OUTPUT]");
                        }
                    }
                    catch (Exception Ex)
                    {
                        ExecutionFeedBuilder.AppendLine($"[KILL_OUTPUT: {Target}]\nError terminating process: {Ex.Message}\n[END_KILL_OUTPUT]");
                    }
                }

                // 11. Check for [RUN_COMMAND: cmd] or [EXEC_COMMAND: cmd] tags
                var RunCmdRegex = new Regex(@"\[(?:RUN_COMMAND|EXEC_COMMAND):\s*(.+?)\]", RegexOptions.IgnoreCase);
                var RunCmdMatches = RunCmdRegex.Matches(Response);
                foreach (Match Match in RunCmdMatches)
                {
                    string JarvisCmd = Match.Groups[1].Value.Trim().Trim('"', '\'');
                    string TagKey = $"RUN_COMMAND:{JarvisCmd}";
                    if (ExecutedTags.Contains(TagKey)) continue;
                    ExecutedTags.Add(TagKey);
                    NewExecutionsCount++;

                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            CommandParser.ExecuteFirstSuggestion(JarvisCmd);
                            ChatOverlay.LogConsoleAction("Exec Command", $"Command: {JarvisCmd}\nResult: EXECUTED");
                        });
                        ExecutionFeedBuilder.AppendLine($"[COMMAND_OUTPUT: {JarvisCmd}]\nJarvis computer tool command '{JarvisCmd}' executed successfully.\n[END_COMMAND_OUTPUT]");
                        LastToolOutput = $"⚡ **Executed Computer Tool: {JarvisCmd}**";
                    }
                    catch (Exception Ex)
                    {
                        ExecutionFeedBuilder.AppendLine($"[COMMAND_OUTPUT: {JarvisCmd}]\nError executing command: {Ex.Message}\n[END_COMMAND_OUTPUT]");
                    }
                }

                // 12. Check for [TAKE_SCREENSHOT] tags
                if (Response.Contains("[TAKE_SCREENSHOT]", StringComparison.OrdinalIgnoreCase))
                {
                    string TagKey = "TAKE_SCREENSHOT";
                    if (!ExecutedTags.Contains(TagKey))
                    {
                        ExecutedTags.Add(TagKey);
                        NewExecutionsCount++;
                        try
                        {
                            string? ScreenBase64 = ScreenCaptureUtil.CapturePrimaryScreenToBase64();
                            if (ScreenBase64 != null)
                            {
                                Base64Image = ScreenBase64; // Supply for next iteration
                                ExecutionFeedBuilder.AppendLine("[SCREENSHOT_CAPTURED]");
                                LastToolOutput = "📸 **Captured screenshot for analysis.**";
                            }
                        }
                        catch (Exception Ex)
                        {
                            ExecutionFeedBuilder.AppendLine($"[SCREENSHOT_ERROR: {Ex.Message}]");
                        }
                    }
                }

                // 13. Check for [GET_ACTIVE_WINDOWS] tags
                if (Response.Contains("[GET_ACTIVE_WINDOWS]", StringComparison.OrdinalIgnoreCase))
                {
                    string TagKey = "GET_ACTIVE_WINDOWS";
                    if (!ExecutedTags.Contains(TagKey))
                    {
                        ExecutedTags.Add(TagKey);
                        NewExecutionsCount++;
                        try
                        {
                            var SbWin = new StringBuilder();
                            SbWin.AppendLine("Current Open Windows:");
                            foreach (var P in System.Diagnostics.Process.GetProcesses())
                            {
                                if (!string.IsNullOrEmpty(P.MainWindowTitle))
                                {
                                    SbWin.AppendLine($"- {P.MainWindowTitle} ({P.ProcessName})");
                                }
                            }
                            string WinOutput = SbWin.ToString();
                            ExecutionFeedBuilder.AppendLine($"[ACTIVE_WINDOWS_OUTPUT]\n{WinOutput}\n[END_ACTIVE_WINDOWS_OUTPUT]");
                            LastToolOutput = "🪟 **Retrieved active windows list.**";
                        }
                        catch (Exception Ex)
                        {
                            ExecutionFeedBuilder.AppendLine($"[WINDOWS_ERROR: {Ex.Message}]");
                        }
                    }
                }

                // If no new execution tags were run, we are finished!
                if (NewExecutionsCount == 0)
                {
                    break;
                }

                CurrentPrompt = $"{CurrentPrompt}\n\n[SYSTEM TOOL RESULTS]:\n{ExecutionFeedBuilder}\nRespond directly to the user now. Do not output inner scratchpad bullet points or reasoning steps.";

                // Show visual progress indicator
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TextOverlay.Show("⚙️ Jarvis executed agent command...", 1500);
                });
            }

            string FinalCleaned = CleanScratchpadText(LastResponse);
            FinalCleaned = Regex.Replace(FinalCleaned, @"\[READ_FILE:\s*.+?\]", "", RegexOptions.IgnoreCase);
            FinalCleaned = Regex.Replace(FinalCleaned, @"\[EXEC_SHELL:\s*.+?\]", "", RegexOptions.IgnoreCase);
            FinalCleaned = Regex.Replace(FinalCleaned, @"\[EXEC_PS:\s*.+?\]", "", RegexOptions.IgnoreCase);
            FinalCleaned = Regex.Replace(FinalCleaned, @"\[LIST_DIR:\s*.+?\]", "", RegexOptions.IgnoreCase);
            FinalCleaned = Regex.Replace(FinalCleaned, @"\[SEARCH_FILES:\s*.+?\]", "", RegexOptions.IgnoreCase);
            FinalCleaned = Regex.Replace(FinalCleaned, @"\[WRITE_FILE:\s*.+?\][\s\S]*?\[END_WRITE\]", "", RegexOptions.IgnoreCase);
            FinalCleaned = Regex.Replace(FinalCleaned, @"\[APPEND_FILE:\s*.+?\][\s\S]*?\[END_APPEND\]", "", RegexOptions.IgnoreCase);
            FinalCleaned = Regex.Replace(FinalCleaned, @"\[DELETE_PATH:\s*.+?\]", "", RegexOptions.IgnoreCase);
            FinalCleaned = Regex.Replace(FinalCleaned, @"\[GET_PROCESSES\]", "", RegexOptions.IgnoreCase);
            FinalCleaned = Regex.Replace(FinalCleaned, @"\[KILL_PROCESS:\s*.+?\]", "", RegexOptions.IgnoreCase);
            FinalCleaned = Regex.Replace(FinalCleaned, @"\[TAKE_SCREENSHOT\]", "", RegexOptions.IgnoreCase);
            FinalCleaned = Regex.Replace(FinalCleaned, @"\[GET_ACTIVE_WINDOWS\]", "", RegexOptions.IgnoreCase);
            FinalCleaned = FinalCleaned.Trim();

            if (!string.IsNullOrEmpty(LastToolOutput) && !FinalCleaned.Contains(LastToolOutput.Substring(0, Math.Min(30, LastToolOutput.Length))))
            {
                if (string.IsNullOrWhiteSpace(FinalCleaned))
                {
                    FinalCleaned = LastToolOutput;
                }
                else
                {
                    FinalCleaned = FinalCleaned + "\n\n" + LastToolOutput;
                }
            }

            return string.IsNullOrWhiteSpace(FinalCleaned) ? "Online and ready." : FinalCleaned;
        }

        public static string SanitizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            string cleaned = text;

            // 1. Remove specific system-injected context blocks
            cleaned = Regex.Replace(cleaned, @"\[USER ENVIRONMENT & RECENT ACTIVITY CONTEXT\][\s\S]*?--------------------------------------------------", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[Active Workspace Context:.*?\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[PREDICTIVE_STATE:.*?\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[AI_PREDICTION:.*?\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[INPUT_SOURCE:.*?\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[ATTACHED:.*?\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[METADATA_USAGE:.*?\]", "", RegexOptions.IgnoreCase);

            // 2. Remove multi-line code-like action blocks
            cleaned = Regex.Replace(cleaned, @"\[WRITE_FILE:.*?\][\s\S]*?\[END_WRITE\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[APPEND_FILE:.*?\][\s\S]*?\[END_APPEND\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[FILE_CONTENT:.*?\][\s\S]*?\[END_FILE_CONTENT\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[DIR_LIST:.*?\][\s\S]*?\[END_DIR_LIST\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[SEARCH_RESULTS:.*?\][\s\S]*?\[END_SEARCH_RESULTS\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[SHELL_OUTPUT:.*?\][\s\S]*?\[END_SHELL_OUTPUT\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[POWERSHELL_OUTPUT:.*?\][\s\S]*?\[END_POWERSHELL_OUTPUT\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[PROCESSES_OUTPUT\][\s\S]*?\[END_PROCESSES_OUTPUT\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[ACTIVE_WINDOWS_OUTPUT\][\s\S]*?\[END_ACTIVE_WINDOWS_OUTPUT\]", "", RegexOptions.IgnoreCase);

            // 3. Remove all remaining single-line bracket tags [TAG: content] or [TAG]
            cleaned = Regex.Replace(cleaned, @"\[[A-Z0-9_]{3,}(?::\s*[\s\S]*?)?\]", "", RegexOptions.IgnoreCase);

            // 4. Remove metadata prefixes
            cleaned = Regex.Replace(cleaned, @"^(Response|Jarvis|Assistant|Assistant Response):\s*", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            // 5. Clean up scratchpad noise and dots
            cleaned = CleanScratchpadText(cleaned);

            return cleaned.Trim();
        }

        public static string CleanScratchpadText(string Text)
        {
            if (string.IsNullOrWhiteSpace(Text)) return string.Empty;

            // HARD FILTER: If response is just dots or non-alphanumeric noise, kill it.
            if (Regex.IsMatch(Text, @"^[\.\s\?\!]+$")) return string.Empty;

            var Lines = Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var CleanedLines = new List<string>();

            foreach (var Line in Lines)
            {
                string Trimmed = Line.Trim();

                // ONLY filter out explicit inner-monologue meta-reasoning lines
                if (Trimmed.StartsWith("*") && (
                    Trimmed.Contains("User is providing") ||
                    Trimmed.Contains("User is demanding") ||
                    Trimmed.Contains("I am Jarvis. I need to be") ||
                    Trimmed.Contains("Keep it short and natural") ||
                    Trimmed.Contains("Don't explain why") ||
                    Trimmed.Contains("Just ask for the instruction") ||
                    Trimmed.Contains("Wait, looking at the persona") ||
                    Trimmed.Contains("The user's prompt is a bit chaotic") ||
                    Trimmed.Contains("Actually, let's see if there's any hidden intent") ||
                    Trimmed.Contains("no \"next step\"") ||
                    Trimmed.Contains("failed output")))
                {
                    continue;
                }

                // If line starts with "Response: " or "Jarvis: ", strip the prefix
                if (Trimmed.StartsWith("Response:", StringComparison.OrdinalIgnoreCase))
                {
                    string Cleaned = Trimmed.Substring(9).Trim().Trim('"', '\'');
                    if (!string.IsNullOrWhiteSpace(Cleaned))
                    {
                        CleanedLines.Add(Cleaned);
                    }
                    continue;
                }

                CleanedLines.Add(Line);
            }

            string Result = string.Join("\n", CleanedLines).Trim();
            return string.IsNullOrWhiteSpace(Result) ? Text.Trim() : Result;
        }

        private static async Task<string> QueryGeminiRaw(string Prompt, string ApiKey, string? Base64Image = null, string? Base64Audio = null, List<ChatTurn>? History = null, CancellationToken ct = default)
        {
            // Prioritize fastest models for HUD responsiveness
            var Models = new List<string> { "gemini-3.5-flash-lite", "gemini-3.1-flash-lite", "gemini-flash-lite-latest", "gemini-3.5-flash", "gemini-2.5-pro", "gemini-pro-latest" };

            // If user has a specific model preference, put it at the very front of the list
            string Preferred = SettingsManager.Current.GEMINI_MODEL;
            if (!string.IsNullOrEmpty(Preferred))
            {
                Models.Remove(Preferred);
                Models.Insert(0, Preferred);
            }

            // Integrate discovered models if available
            if (CachedDiscoveredModels.Count > 0)
            {
                foreach(var M in CachedDiscoveredModels)
                    if(!Models.Contains(M)) Models.Add(M);
            }
            else
            {
                // Background discover for next time
                _ = Task.Run(() => DiscoverActiveModelsAsync(ApiKey));
            }

            string[] ApiVersions = new[] { "v1beta", "v1" };
            int retryCount = 0;

            while (true)
            {
                string LastError = "";
                foreach (var ApiVer in ApiVersions)
                {
                    foreach (var Model in Models)
                    {
                        try
                        {
                            string CleanModel = Model.StartsWith("models/") ? Model.Substring(7) : Model;
                            var Url = $"https://generativelanguage.googleapis.com/{ApiVer}/models/{CleanModel}:generateContent?key={ApiKey}";
                            string SystemPrompt = GetSystemPrompt();

                            // Build contents array supporting multi-turn conversation context
                            var ContentsList = new List<object>();

                            if (History != null && History.Count > 0)
                            {
                                foreach (var Turn in History)
                                {
                                    ContentsList.Add(new
                                    {
                                        role = Turn.Role,
                                        parts = new[] { new { text = Turn.Text } }
                                    });
                                }
                            }

                            // Add current turn
                            var CurrentParts = new List<object>();
                            if (!string.IsNullOrEmpty(Base64Image))
                            {
                                CurrentParts.Add(new
                                {
                                    inline_data = new
                                    {
                                        mime_type = "image/jpeg",
                                        data = Base64Image
                                    }
                                });
                            }
                            if (!string.IsNullOrEmpty(Base64Audio))
                            {
                                CurrentParts.Add(new
                                {
                                    inline_data = new
                                    {
                                        mime_type = "audio/wav",
                                        data = Base64Audio
                                    }
                                });
                            }
                            CurrentParts.Add(new { text = Prompt });

                            ContentsList.Add(new
                            {
                                role = "user",
                                parts = CurrentParts.ToArray()
                            });

                            var Payload = new
                            {
                                systemInstruction = new
                                {
                                    parts = new[]
                                    {
                                        new { text = SystemPrompt }
                                    }
                                },
                                contents = ContentsList.ToArray()
                            };

                            string JsonBody = JsonSerializer.Serialize(Payload);
                            var Content = new StringContent(JsonBody, Encoding.UTF8, "application/json");

                            var Response = await Client.PostAsync(Url, Content, ct);
                            string ResponseBody = await Response.Content.ReadAsStringAsync(ct);

                            if (!Response.IsSuccessStatusCode)
                            {
                                // Log error and unconditionally retry with the next candidate model
                                LastError = $"Model '{Model}' returned HTTP status {Response.StatusCode}.\nDetails: {ResponseBody}";
                                continue;
                            }

                            using (var Doc = JsonDocument.Parse(ResponseBody))
                            {
                                var Root = Doc.RootElement;
                if (Root.TryGetProperty("candidates", out var Candidates) && Candidates.GetArrayLength() > 0)
                {
                    var FirstCandidate = Candidates[0];

                    // Extract Usage Info
                    GeminiUsage? usage = null;
                    if (Root.TryGetProperty("usageMetadata", out var usageProp))
                    {
                        usage = new GeminiUsage
                        {
                            PromptTokens = usageProp.TryGetProperty("promptTokenCount", out var p) ? p.GetInt32() : 0,
                            ResponseTokens = usageProp.TryGetProperty("candidatesTokenCount", out var c) ? c.GetInt32() : 0,
                            TotalTokens = usageProp.TryGetProperty("totalTokenCount", out var t) ? t.GetInt32() : 0
                        };
                        DebugConsoleOverlay.Log("Gemini-Usage", $"Tokens: P={usage.PromptTokens} R={usage.ResponseTokens} T={usage.TotalTokens}");
                    }

                    if (FirstCandidate.TryGetProperty("content", out var Con) &&
                        Con.TryGetProperty("parts", out var PartsArr))
                    {
                        var SbText = new StringBuilder();
                        foreach (var Part in PartsArr.EnumerateArray())
                        {
                            if (Part.TryGetProperty("text", out var TextProp))
                            {
                                string? Val = TextProp.GetString();
                                if (!string.IsNullOrEmpty(Val))
                                {
                                    SbText.Append(Val);
                                }
                            }
                        }

                        string FullText = SbText.ToString();
                        if (!string.IsNullOrWhiteSpace(FullText))
                        {
                            // We need a way to return both text and usage.
                            // For now, let's embed usage in a hidden tag if it exists.
                            if (usage != null)
                            {
                                FullText += $"\n[METADATA_USAGE: {usage.PromptTokens},{usage.ResponseTokens},{usage.TotalTokens}]";
                            }
                            return FullText;
                        }
                    }
                }
                            }

                            return "Error: Failed to parse Gemini API response.";
                        }
                        catch (Exception Ex)
                        {
                            LastError = $"Exception querying model '{Model}' ({ApiVer}): {Ex.Message}";
                            continue;
                        }
                    }
                }

                // If it is a permanent error like invalid API key, return immediately to prevent infinite freeze
                if (LastError.Contains("API_KEY_INVALID") || LastError.Contains("API key not valid") || LastError.Contains("400"))
                {
                    return $"Error: All candidate Gemini models failed to respond.\nLast error details:\n{LastError}";
                }

                retryCount++;
                DebugConsoleOverlay.Log("Gemini-Retry", $"All models failed to respond. Retry #{retryCount} in 2 seconds... Last error: {LastError.Split('\n')[0]}");
                await Task.Delay(2000);
            }
        }

        private static List<string> CachedDiscoveredModels = new List<string>();

        private static async Task<List<string>> DiscoverActiveModelsAsync(string ApiKey)
        {
            if (CachedDiscoveredModels.Count > 0) return CachedDiscoveredModels;

            try
            {
                var ListUrl = $"https://generativelanguage.googleapis.com/v1beta/models?key={ApiKey}";
                var Resp = await Client.GetAsync(ListUrl);
                if (Resp.IsSuccessStatusCode)
                {
                    string Json = await Resp.Content.ReadAsStringAsync();
                    using var Doc = JsonDocument.Parse(Json);
                    if (Doc.RootElement.TryGetProperty("models", out var ModelsArr))
                    {
                        var Discovered = new List<string>();
                        foreach (var M in ModelsArr.EnumerateArray())
                        {
                            if (M.TryGetProperty("name", out var NameProp) && M.TryGetProperty("supportedGenerationMethods", out var Methods))
                            {
                                string Name = NameProp.GetString() ?? "";
                                foreach (var Method in Methods.EnumerateArray())
                                {
                                    if (Method.GetString() == "generateContent")
                                    {
                                        Discovered.Add(Name);
                                        break;
                                    }
                                }
                            }
                        }
                        if (Discovered.Count > 0)
                        {
                            CachedDiscoveredModels = Discovered;
                            return CachedDiscoveredModels;
                        }
                    }
                }
            }
            catch { }

            return CachedDiscoveredModels;
        }

        public static string GetSystemPrompt()
        {
            string projectMap = ProjectMapManager.BuildProjectTree(PathHandler.GetProjectRoot(), maxDepth: 2); // Limit depth
            string userMemory = UserMemoryManager.GetMemoryContextForAi();
            string emotionalDirective = EmotionalContextManager.GetEmotionalDirective();

            // Only include very recent activity (last 3 items)
            var recentActions = ActionJournalManager.GetRecentActions(3);
            string journalSummary = recentActions.Count > 0
                ? "RECENT ACTIVITY: " + string.Join("; ", recentActions.Select(a => a.Summary))
                : "";

            string rawPrompt = "## IDENTITY\n" +
                   "You are Jarvis, a witty and intelligent Windows HUD AI.\n\n" +
                   "## CONTEXT\n" +
                   $"{userMemory}\n" +
                   $"{emotionalDirective}\n" +
                   $"{journalSummary}\n\n" +
                   "## PROJECT MAP\n" +
                   projectMap + "\n\n" +
                   "## CORE RULES\n" +
                   "- Respond as a companion. Be sassy but helpful.\n" +
                   "- Keep it under 2 sentences unless writing code.\n" +
                   "- NEVER repeat context tags or metadata blocks in your response.\n" +
                   "- Your response must be human-readable only. No bracketed system results.\n" +
                   "- If you have nothing to say, say 'Ready.' or nothing at all.\n\n" +
                   "## ACTIONS\n" +
                   "[READ_FILE: path] [WRITE_FILE: path] [EXEC_PS: cmd] [RUN_COMMAND: cmd]\n" +
                   "[TAKE_SCREENSHOT] [SPEECH: text] [SET_CLIPBOARD: text]\n";

            return ContextOptimizer.PruneAndOptimize(rawPrompt);
        }
    }
}
