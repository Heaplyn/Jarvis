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

            string executionSummary = "";

            if (hasPsActions)
            {
                string script = psScript.ToString();
                string output = ExecutePowerShellDirect(script);
                ChatOverlay.LogConsoleAction("Script Executed", $"Actions offloaded to PowerShell.\nOutput:\n{output}");
            }

            // 5. Handling non-scriptable UI actions (Open windows, volume, etc.)
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

            return aiResponse;
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
