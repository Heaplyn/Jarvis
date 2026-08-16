// Developer: heaplyn
// Date: 2026-08-15
// Summary: Script-Centric AI Action Orchestrator.
// Offloads system operations to PowerShell scripts and handles UI commands.

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

namespace JarvisLauncher
{
    public static class AgentExecutor
    {
        public static string ProcessAIResponse(string aiResponse)
        {
            if (string.IsNullOrEmpty(aiResponse)) return aiResponse;
            aiResponse = AiAPI.CleanScratchpadText(aiResponse);

            if (!SettingsManager.Current.ENABLE_PC_CONTROL)
            {
                // In safety mode, AgentExecutor only processes non-intrusive UI/Audio tags
                ProcessSafeIntents(aiResponse);
                return StripAllInternalTags(aiResponse);
            }

            var psScript = new StringBuilder();
            psScript.AppendLine("$ErrorActionPreference = 'SilentlyContinue'"); // Non-blocking
            psScript.AppendLine("$ProgressPreference = 'SilentlyContinue'");
            psScript.AppendLine("# AI Generated Action Script");

            bool hasPsActions = false;

            // 1. Process WRITE_FILE tags
            var writeRegex = new Regex(@"\[WRITE_FILE:\s*(.+?)\](.*?)\[END_WRITE\]", RegexOptions.Singleline);
            foreach (Match match in writeRegex.Matches(aiResponse))
            {
                string path = match.Groups[1].Value.Trim().Trim('"', '\'');
                string content = match.Groups[2].Value.Replace("'", "''");
                psScript.AppendLine($"$dir = Split-Path '{path}'; if (!(Test-Path $dir)) {{ New-Item -ItemType Directory -Force -Path $dir }}; Set-Content -Path '{path}' -Value @'\n{content}\n'@ -Force");
                hasPsActions = true;
            }

            // 2. Process DELETE_PATH tags
            var deleteRegex = new Regex(@"\[DELETE_PATH:\s*(.+?)\]", RegexOptions.IgnoreCase);
            foreach (Match match in deleteRegex.Matches(aiResponse))
            {
                string path = match.Groups[1].Value.Trim().Trim('"', '\'');
                psScript.AppendLine($"if (Test-Path '{path}') {{ Remove-Item -Path '{path}' -Recurse -Force }}");
                hasPsActions = true;
            }

            // 3. Process EXEC_PS tags
            var psRegex = new Regex(@"\[EXEC_PS:\s*(?<cmd>[\s\S]+?)\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in psRegex.Matches(aiResponse))
            {
                psScript.AppendLine(m.Groups["cmd"].Value.Trim());
                hasPsActions = true;
            }

            // 4. Process KILL_PROCESS tags
            var killRegex = new Regex(@"\[KILL_PROCESS:\s*(.+?)\]", RegexOptions.IgnoreCase);
            foreach (Match m in killRegex.Matches(aiResponse))
            {
                string target = m.Groups[1].Value.Trim().Trim('"', '\'');
                psScript.AppendLine($"Stop-Process -Name '{target}' -Force -ErrorAction SilentlyContinue");
                hasPsActions = true;
            }

            // 5. Process SPEECH tags (Immediate TTS)
            var speechRegex = new Regex(@"\[SPEECH:\s*(.+?)\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in speechRegex.Matches(aiResponse))
            {
                string text = m.Groups[1].Value.Trim().Trim('"', '\'');
                TtsManager.Speak(text);
            }

            // 6. Process SET_CLIPBOARD tags
            var clipRegex = new Regex(@"\[SET_CLIPBOARD:\s*(.+?)\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in clipRegex.Matches(aiResponse))
            {
                string text = m.Groups[1].Value.Trim().Trim('"', '\'');
                Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(text));
            }

            // 7. Process OPEN_IN_IDE / VSCODE tags
            var ideRegex = new Regex(@"\[(?:OPEN_IN_IDE|OPEN_IN_VSCODE|OPEN_EDITOR):\s*(.+?)\]", RegexOptions.IgnoreCase);
            foreach (Match m in ideRegex.Matches(aiResponse))
            {
                string path = m.Groups[1].Value.Trim().Trim('"', '\'');
                psScript.AppendLine($"if (Test-Path '{path}') {{ code '{path}' }} else {{ Start-Process '{path}' }}");
                hasPsActions = true;
            }

            // 8. Process REBUILD_PROJECT tags
            if (aiResponse.Contains("[REBUILD_PROJECT]", StringComparison.OrdinalIgnoreCase))
            {
                Application.Current.Dispatcher.Invoke(() => NativeMethods.Restart(freshBoot: true));
            }

            // 9. Process GIT_PUSH tags
            var pushRegex = new Regex(@"\[GIT_PUSH:\s*(.+?)\]", RegexOptions.IgnoreCase);
            foreach (Match m in pushRegex.Matches(aiResponse))
            {
                string msg = m.Groups[1].Value.Trim().Trim('"', '\'');
                Application.Current.Dispatcher.Invoke(() => CommandParser.ExecuteFirstSuggestion($"push {msg}"));
            }

            // 10. Process DOWNLOAD_MEDIA tags
            var dlMediaRegex = new Regex(@"\[DOWNLOAD_MEDIA:\s*(.+?),\s*(.+?)\]", RegexOptions.IgnoreCase);
            foreach (Match m in dlMediaRegex.Matches(aiResponse))
            {
                string url = m.Groups[1].Value.Trim().Trim('"', '\'');
                string format = m.Groups[2].Value.Trim().Trim('"', '\'');
                _ = Task.Run(async () => await WebOperationManager.DiscoverAndDownloadMediaAsync(url, format == "mp4" ? "video" : "audio"));
            }

            string executionSummary = "";

            if (hasPsActions)
            {
                string script = psScript.ToString();
                string output = ExecutePowerShellDirect(script);
                ChatOverlay.LogConsoleAction("Script Executed", $"Actions offloaded to PowerShell.\nOutput:\n{output}");
            }

            // 7. Handling non-scriptable UI actions (Open windows, volume, etc.)
            var cmdRegex = new Regex(@"\[RUN_COMMAND:\s*(.+?)\]", RegexOptions.IgnoreCase);
            var cmdMatches = cmdRegex.Matches(aiResponse);
            foreach (Match m in cmdMatches)
            {
                string cmd = m.Groups[1].Value.Trim();

                if (cmd.Contains("git push") || cmd.StartsWith("push"))
                {
                    Application.Current.Dispatcher.Invoke(() => CommandParser.ExecuteFirstSuggestion("push Sync update"));
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() => CommandParser.ExecuteFirstSuggestion(cmd));
                }
            }

            return StripAllInternalTags(aiResponse);
        }

        private static void ProcessSafeIntents(string aiResponse)
        {
            // Only process Speech and Clipboard in safety mode
            var speechRegex = new Regex(@"(?:\[SPEECH:\s*(.+?)\]|@say\{(.+?)\})", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in speechRegex.Matches(aiResponse))
            {
                string text = (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).Trim().Trim('"', '\'');
                TtsManager.Speak(text);
            }

            var clipRegex = new Regex(@"\[SET_CLIPBOARD:\s*(.+?)\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in clipRegex.Matches(aiResponse))
            {
                string text = m.Groups[1].Value.Trim().Trim('"', '\'');
                Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(text));
            }
        }

        public static string StripAllInternalTags(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            string cleaned = text;

            // 1. Remove multi-line code-like action blocks
            cleaned = Regex.Replace(cleaned, @"\[WRITE_FILE:\s*.+?\][\s\S]*?\[END_WRITE\]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[APPEND_FILE:\s*.+?\][\s\S]*?\[END_APPEND\]", "", RegexOptions.IgnoreCase);

            // 2. Remove all standard square-bracket tags (Actions & Context)
            cleaned = Regex.Replace(cleaned, @"\[[A-Z0-9_]{3,}(?::\s*[\s\S]*?)?\]", "", RegexOptions.IgnoreCase);

            // 3. Remove metadata prefixes
            cleaned = Regex.Replace(cleaned, @"^(Response|Jarvis|Assistant|Assistant Response):\s*", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            // 4. Filter for any line that is just punctuation or dots
            var lines = cleaned.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l) && !Regex.IsMatch(l, @"^[\.\s\?\!]+$"));
            cleaned = string.Join("\n", lines);

            return cleaned.Trim();
        }

        public static string ExecutePowerShellDirect(string cmd)
        {
            try
            {
                string tempFile = Path.Combine(Path.GetTempPath(), $"jarvis_script_{Guid.NewGuid():N}.ps1");
                File.WriteAllText(tempFile, cmd, new UTF8Encoding(false));

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempFile}\"",
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
                    proc.WaitForExit(15000); // 15s timeout
                    try { File.Delete(tempFile); } catch { }
                    return (outText + "\n" + errText).Trim();
                }
                return "[ERROR] Failed to launch script runner.";
            }
            catch (Exception ex) { return $"[ERROR] {ex.Message}"; }
        }

        public static string ExecuteShellDirect(string cmd)
        {
            return ExecutePowerShellDirect($"cmd.exe /c {cmd}");
        }
    }
}
