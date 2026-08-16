// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles CLI commands to stage, commit, and push the active project repository directly to GitHub.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;

namespace JarvisLauncher
{
    public class GitPushCommandHandler : ICommandHandler
    {
        public bool CanHandle(string Query)
        {
            if (string.IsNullOrWhiteSpace(Query)) return false;
            string lower = Query.Trim().ToLower();
            return lower.StartsWith("push") || lower.StartsWith("gitpush") || lower.StartsWith("git");
        }

        public List<CommandResult> GetSuggestions(string Query)
        {
            var suggestions = new List<CommandResult>();
            string trimmed = Query.Trim();
            string lower = trimmed.ToLower();
            var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

            if (lower == "git status" || lower == "git st")
            {
                suggestions.Add(new CommandResult { TITLE = "📋 Git Status", DESCRIPTION = "Show current working tree status", SIMILARITY = 3.0, EXECUTE = () => RunGitQuick("status") });
                return suggestions;
            }
            if (lower == "git log" || lower == "git l")
            {
                suggestions.Add(new CommandResult { TITLE = "📜 Git Log (last 15)", DESCRIPTION = "Show recent commit history", SIMILARITY = 3.0, EXECUTE = () => RunGitQuick("log --oneline -n 15") });
                return suggestions;
            }
            if (lower == "git diff")
            {
                suggestions.Add(new CommandResult { TITLE = "🔍 Git Diff", DESCRIPTION = "Show uncommitted file changes", SIMILARITY = 3.0, EXECUTE = () => RunGitQuick("diff --stat") });
                return suggestions;
            }

            string? commitMessage = null;
            if (lower.StartsWith("push ")) commitMessage = trimmed.Substring(5).Trim().Trim('"', '\'');
            else if (lower.StartsWith("gitpush ")) commitMessage = trimmed.Substring(8).Trim().Trim('"', '\'');
            else if (lower.StartsWith("git push ")) commitMessage = trimmed.Substring(9).Trim().Trim('"', '\'');

            double similarity = SearchUtil.GetSimilarity(parts[0].ToLower(), "push");

            if (!string.IsNullOrWhiteSpace(commitMessage))
            {
                suggestions.Add(new CommandResult
                {
                    TITLE = $"🚀 Push: \"{commitMessage}\" → GitHub",
                    DESCRIPTION = "Stage all, commit, and push to remote",
                    SIMILARITY = similarity + 5.0,
                    EXECUTE = () => ExecuteGitPush(commitMessage)
                });
            }
            else if (lower == "push" || lower == "git push")
            {
                suggestions.Add(new CommandResult
                {
                    TITLE = "🚀 AI-Generated Push",
                    DESCRIPTION = "Compare changes and use AI to write the commit message",
                    SIMILARITY = similarity + 5.5,
                    EXECUTE = () => ExecuteAiGitPush()
                });

                string autoMsg = $"Update: {DateTime.Now:yyyy-MM-dd HH:mm}";
                suggestions.Add(new CommandResult
                {
                    TITLE = "🚀 Quick Push (Auto-msg)",
                    DESCRIPTION = $"Message: \"{autoMsg}\"",
                    SIMILARITY = similarity + 5.0,
                    EXECUTE = () => ExecuteGitPush(autoMsg)
                });
            }
            else
            {
                suggestions.Add(new CommandResult { TITLE = "🚀 Push Project → GitHub...", DESCRIPTION = "Type a commit message: 'push <message>'", SIMILARITY = similarity, EXECUTE = null });
                suggestions.Add(new CommandResult { TITLE = "📋 Git Status", DESCRIPTION = "Show current working tree status", SIMILARITY = similarity - 0.1, EXECUTE = () => RunGitQuick("status") });
            }

            return suggestions;
        }

        private static void RunGitQuick(string gitArgs)
        {
            TextOverlay.Show($"⚡ Running: git {gitArgs}", 1500);
            Task.Run(async () =>
            {
                string projectRoot = GetProjectRoot();
                string result = await RunCommandAsync("git", gitArgs, projectRoot);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CliOutputOverlay.Show($"git {gitArgs}", result);
                });
            });
        }

        private static void ExecuteAiGitPush()
        {
            TextOverlay.Show("🧠 Jarvis is analyzing changes...", 3000);
            Task.Run(async () =>
            {
                string projectRoot = GetProjectRoot();

                // 1. Get high-level diff stats
                string diffStat = await RunCommandAsync("git", "diff HEAD --stat", projectRoot);

                // 2. Get content diff (truncated to avoid token limit)
                string diffContent = await RunCommandAsync("git", "diff HEAD", projectRoot);
                if (diffContent.Length > 8000) diffContent = diffContent.Substring(0, 8000) + "\n... (diff truncated)";

                if (string.IsNullOrWhiteSpace(diffStat) || diffStat.Contains("Error"))
                {
                    Application.Current.Dispatcher.Invoke(() => TextOverlay.Show("⚠️ No changes detected to push.", 3000));
                    return;
                }

                // 3. Ask AI to summarize
                string prompt = "Task: Write a concise, professional Git commit message based on these changes.\n" +
                               "Format: A single line summary (max 72 chars), followed by bullet points if complex.\n" +
                               "RULES:\n" +
                               "1. Be specific (e.g., 'Fix: Null check in UI' not 'Updated files').\n" +
                               "2. Do not use markdown backticks around the message.\n" +
                               "3. Output ONLY the commit message text.\n\n" +
                               $"## FILE STATS:\n{diffStat}\n\n" +
                               $"## DIFF CONTENT:\n{diffContent}";

                string aiMessage = await LlmRouter.AskAsync(prompt);
                aiMessage = aiMessage.Trim().Trim('"', '\'');

                if (string.IsNullOrWhiteSpace(aiMessage) || aiMessage.Contains("Error"))
                {
                    aiMessage = $"Update: {DateTime.Now:yyyy-MM-dd HH:mm} (AI generation failed)";
                }

                DebugConsoleOverlay.Log("Git-AI", $"Generated Message: {aiMessage}");

                // 4. Proceed with push
                string result = await RunGitPushAsync(aiMessage);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CliOutputOverlay.Show($"AI Push: {aiMessage}", result);
                });
            });
        }

        private static void ExecuteGitPush(string commitMessage)
        {
            TextOverlay.Show("⚡ Initiating GitHub push...", 2500);
            Task.Run(async () =>
            {
                string result = await RunGitPushAsync(commitMessage);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CliOutputOverlay.Show("GitHub Push Log", result);
                });
            });
        }

        private static string GetProjectRoot()
        {
            string devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            if (Directory.Exists(Path.Combine(devPath, ".git"))) return devPath;
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static async Task<string> RunGitPushAsync(string message)
        {
            var log = new StringBuilder();
            string projectRoot = GetProjectRoot();

            log.AppendLine($"Working directory: {projectRoot}\n");

            string branchName = await RunCommandAsync("git", "rev-parse --abbrev-ref HEAD", projectRoot);
            branchName = branchName.Trim();
            if (string.IsNullOrEmpty(branchName) || branchName.Contains("Error") || branchName.Contains("fatal")) branchName = "main";

            log.AppendLine($"Current branch: {branchName}\n");

            log.AppendLine("--- CLEANING CACHE ---");
            await RunCommandAsync("git", "rm -r --cached bin obj Data publish JarvisLauncher.exe", projectRoot);

            log.AppendLine("--- STAGING & COMMITTING ---");
            await RunCommandAsync("git", "add .", projectRoot);
            string commitResult = await RunCommandAsync("git", $"commit -m \"{message.Replace("\"", "\\\"")}\"", projectRoot);
            log.AppendLine(commitResult);

            string statusCheck = await RunCommandAsync("git", "status", projectRoot);
            bool isAhead = statusCheck.Contains("ahead of");
            bool isClean = commitResult.Contains("nothing to commit");

            if (isClean && !isAhead)
            {
                log.AppendLine("ℹ️ Branch up to date. Nothing to push.");
                return log.ToString();
            }

            log.AppendLine("--- PUSHING TO REMOTE ---");
            await RunCommandAsync("git", "config http.postBuffer 524288000", projectRoot);
            string pushResult = await RunCommandAsync("git", $"push origin {branchName}", projectRoot);
            log.AppendLine(pushResult);

            if (pushResult.Contains("rejected") || pushResult.Contains("error") || pushResult.Contains("fatal"))
            {
                log.AppendLine("\n⚠️ Push failed. Attempting self-healing (gc + reset)...");
                await RunCommandAsync("git", "gc --prune=now --aggressive", projectRoot);
                await RunCommandAsync("git", $"reset --soft origin/{branchName}", projectRoot);
                await RunCommandAsync("git", "add .", projectRoot);
                await RunCommandAsync("git", $"commit -m \"{message}\"", projectRoot);
                pushResult = await RunCommandAsync("git", $"push origin {branchName}", projectRoot);
                log.AppendLine(pushResult);
            }

            return log.ToString();
        }

        private static async Task<string> RunCommandAsync(string fileName, string arguments, string workingDirectory)
        {
            var output = new StringBuilder();
            var tcs = new TaskCompletionSource<string>();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.Exited += (s, e) => { tcs.SetResult(output.ToString()); process.Dispose(); };
            try { process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine(); }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
            return await tcs.Task;
        }

        public List<CommandDesc> GetCommandDescriptions() => new List<CommandDesc> { new CommandDesc("push <msg>", "Commit and push to GitHub", "push update") };
        public void OnStart() { }
    }
}
