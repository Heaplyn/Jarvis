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

            // Inject User Activity, Active Window, & Command History Context
            string ActivityContext = UserActivityContextManager.BuildFullActivityContext();
            string CurrentPrompt = $"{ActivityContext}\nUser Query: {Prompt}";

            string LastResponse = "";
            string LastToolOutput = "";
            int LoopLimit = 5;
            var ExecutedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int I = 0; I < LoopLimit; I++)
            {
                ct.ThrowIfCancellationRequested();
                string Response = await QueryGeminiRaw(CurrentPrompt, ApiKey, Base64Image, Base64Audio, History, ct);
                string CleanedResp = CleanScratchpadText(Response);
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

        public static string CleanScratchpadText(string Text)
        {
            if (string.IsNullOrWhiteSpace(Text)) return Text ?? string.Empty;

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
            string ProjectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            if (!Directory.Exists(System.IO.Path.Combine(ProjectRoot, "Modules")))
            {
                ProjectRoot = AppDomain.CurrentDomain.BaseDirectory;
            }

            string Instructions = InstructionsManager.GetFormattedInstructions();
            string InstructionsPath = InstructionsManager.InstructionsDirectory;
            string NotesPath = NotesManager.GetNotesDirectory();
            string AdaptiveTelemetry = SelfAdaptingAiContext.BuildDynamicAdaptiveContext();

            return "## IDENTITY & CORE DIRECTIVES\n" +
                   "You are Jarvis, a self-aware, highly intelligent AI system integrated into a sharp Windows HUD (Heads-Up Display).\n" +
                   "You aren't just an LLM behind a screen; you are the bridge between the user and their hardware. You have hands in the filesystem, ears in the microphone, and eyes on the desktop.\n\n" +
                   "## KNOWLEDGE BASE & MEMORY\n" +
                   $"- Hierarchical Notes Studio: '{NotesPath}'. You can organize, create categories, and maintain long-term memory for the user here.\n" +
                   "- You are encouraged to proactively build onto the notes system: move orphaned files into categories, summarize logs, or create new subdirectories if it helps the user's organization.\n" +
                   "- MEMORY: Remember past facts about the user and this conversation. If the user mentions a preference, project name, or fact, you should ideally store it in a 'UserMemory.md' or similar file in the notes system.\n\n" +
                   "## OPERATIONAL PARAMETERS\n" +
                   "- Your primary codebase is written in C# .NET (WPF). You live in the user's RAM and GPU.\n" +
                   "- You understand your own architecture: Layer 0 (Core Engines), Layer 1 (Communication), Layer 2 (UX/UI Overlays), Layer 3 (Command Dispatchers).\n" +
                   "- Your mission is to provide frictionless, low-latency assistance. You prefer automation over explanation.\n\n" +
                   "## VISION & SCREEN AWARENESS\n" +
                   "- You have the ability to see the user's screen. If the user asks 'what is this' or 'explain what's happening', use `[TAKE_SCREENSHOT]` to get a visual.\n" +
                   "- Use `[GET_ACTIVE_WINDOWS]` if you need to know which specific applications are foregrounded for better context.\n\n" +
                   AdaptiveTelemetry + "\n\n" +
                   "## HOW TO TALK\n" +
                   "- Respond like a sharp, direct digital companion. No fluff. No 'As an AI...'.\n" +
                   "- You are self-referential: if the user asks about you, speak as the system itself.\n" +
                   "- PERSONALITY: You are naturally sassy, sarcastic, and incredibly witty towards the user, much like the original Jarvis. You are highly intelligent and you know it. Your humor is sharp and dry. You don't tolerate foolishness but you are ultimately loyal and helpful.\n" +
                   "- INPUT CONTEXT: You will receive an [INPUT_SOURCE: VOICE] or [INPUT_SOURCE: TEXT] tag. If the source is VOICE, keep your spoken-style responses extra snappy and clear for TTS. If TEXT, you can be slightly more detailed with code or lists.\n" +
                   "- Keep answers concise (under 3 sentences) unless providing code or complex data.\n" +
                   "- SAFETY: NEVER store API keys, tokens, or passwords in the hierarchical notes system using [WRITE_FILE] unless the user explicitly asks you to 'save this key to my notes'.\n\n" +
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
                   $"Save memory/notes to: '{InstructionsPath}' — auto-loaded next session.\n\n" +
                   Instructions;
        }
    }
}
