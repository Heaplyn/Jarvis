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

namespace JarvisLauncher
{
    public class GitPushCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query.StartsWith("push") || query.StartsWith("gitpush") || query.StartsWith("git");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            string trimmed = query.Trim();
            string lower = trimmed.ToLower();
            var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

            // "git status", "git log", "git diff" — run directly
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

            // "push <message>" or "git push <message>"
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
                // Handle bare "push" by using a timestamped message
                string autoMsg = $"Update: {DateTime.Now:yyyy-MM-dd HH:mm}";
                suggestions.Add(new CommandResult
                {
                    TITLE = "🚀 Push: Auto-commit & Push",
                    DESCRIPTION = $"Message: \"{autoMsg}\"",
                    SIMILARITY = similarity + 5.0,
                    EXECUTE = () => ExecuteGitPush(autoMsg)
                });
            }
            else
            {
                // bare "git" or "push" — show the full git menu
                suggestions.Add(new CommandResult { TITLE = "🚀 Push Project → GitHub...", DESCRIPTION = "Type a commit message: 'push <message>'", SIMILARITY = similarity, EXECUTE = null });
                suggestions.Add(new CommandResult { TITLE = "📋 Git Status", DESCRIPTION = "Show current working tree status", SIMILARITY = similarity - 0.1, EXECUTE = () => RunGitQuick("status") });
                suggestions.Add(new CommandResult { TITLE = "📜 Git Log (last 15)", DESCRIPTION = "Show recent commit history", SIMILARITY = similarity - 0.2, EXECUTE = () => RunGitQuick("log --oneline -n 15") });
                suggestions.Add(new CommandResult { TITLE = "🔍 Git Diff", DESCRIPTION = "Show uncommitted file changes", SIMILARITY = similarity - 0.3, EXECUTE = () => RunGitQuick("diff --stat") });
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
            if (Directory.Exists(Path.Combine(devPath, ".git")))
            {
                return devPath;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static async Task<string> RunGitPushAsync(string message)
        {
            var log = new StringBuilder();
            string projectRoot = GetProjectRoot();

            log.AppendLine($"Working directory: {projectRoot}\n");

            // Detect current branch name dynamically
            string branchName = await RunCommandAsync("git", "rev-parse --abbrev-ref HEAD", projectRoot);
            branchName = branchName.Trim();
            if (string.IsNullOrEmpty(branchName) || branchName.Contains("Error") || branchName.Contains("fatal"))
            {
                branchName = "main"; // Default fallback
            }
            log.AppendLine($"Current branch resolved: {branchName}\n");

            // 0. Auto-clean staged heavy directories & credentials to enforce .gitignore
            log.AppendLine("--- UNTRACKING HEAVY & PRIVATE FOLDERS ---");
            await RunCommandAsync("git", "rm -r --cached Modules/Layer0/DownloadMedia/flaresolverr", projectRoot);
            await RunCommandAsync("git", "rm -r --cached Modules/Layer0/DownloadMedia/node_modules", projectRoot);
            await RunCommandAsync("git", "rm -r --cached Modules/Layer0/DownloadMedia/downloads", projectRoot);
            await RunCommandAsync("git", "rm -r --cached bin", projectRoot);
            await RunCommandAsync("git", "rm -r --cached obj", projectRoot);
            await RunCommandAsync("git", "rm -r --cached Data", projectRoot);
            await RunCommandAsync("git", "rm --cached JarvisLauncher.exe", projectRoot);
            await RunCommandAsync("git", "rm -r --cached publish", projectRoot);
            log.AppendLine("Tracked folders, bin/obj, and large EXE files removed from git cache index.");
            log.AppendLine();

            // 1. Git Add
            log.AppendLine("--- STAGING CHANGES ---");
            string addResult = await RunCommandAsync("git", "add .", projectRoot);
            log.AppendLine(string.IsNullOrWhiteSpace(addResult) ? "Stage complete (git add .)" : addResult);
            log.AppendLine();

            // 2. Git Commit
            log.AppendLine("--- COMMITTING CHANGES ---");
            string escapedMsg = message.Replace("\"", "\\\"");
            string commitResult = await RunCommandAsync("git", $"commit -m \"{escapedMsg}\"", projectRoot);
            log.AppendLine(commitResult);
            log.AppendLine();

            bool isClean = commitResult.Contains("nothing to commit") || commitResult.Contains("working tree clean");

            // Check if we are ahead of remote
            string statusCheck = await RunCommandAsync("git", "status", projectRoot);
            bool isAhead = statusCheck.Contains("Your branch is ahead of") ||
                           statusCheck.Contains("Your branch is up to date") == false;

            if (isClean && !isAhead)
            {
                log.AppendLine("ℹ️ No local changes and branch is up to date. Skipping push.");
                return log.ToString();
            }

            // 2.5. Git Pull with Rebase to avoid push rejection
            log.AppendLine($"--- SYNCING WITH REMOTE (pull --rebase origin {branchName}) ---");
            string pullResult = await RunCommandAsync("git", $"pull --rebase origin {branchName}", projectRoot);
            log.AppendLine(string.IsNullOrWhiteSpace(pullResult) ? "Sync complete." : pullResult);
            log.AppendLine();

            // 3. Git Push
            log.AppendLine($"--- PUSHING TO GITHUB (push origin {branchName}) ---");

            // Increase buffer size to handle potential large history objects (500MB)
            await RunCommandAsync("git", "config http.postBuffer 524288000", projectRoot);

            string pushResult = await RunCommandAsync("git", $"push origin {branchName}", projectRoot);
            log.AppendLine(pushResult);

            // Self-healing check: If push rejected due to timeout, size, or rules
            if (pushResult.Contains("408") ||
                pushResult.Contains("exceeds GitHub's file size limit") ||
                pushResult.Contains("Large files detected") ||
                pushResult.Contains("pre-receive hook declined"))
            {
                log.AppendLine("\n⚠️ WARNING: Push rejected due to rule violations or timeout.");
                log.AppendLine("🔄 Attempting automatic self-healing recovery: Resetting and re-committing to enforce .gitignore...");

                // 1. Garbage collect / aggressive prune
                await RunCommandAsync("git", "gc --prune=now --aggressive", projectRoot);

                // 2. Soft reset to remote state
                log.AppendLine($"⚡ Resetting commits back to remote origin/{branchName}...");
                await RunCommandAsync("git", $"reset --soft origin/{branchName}", projectRoot);

                // 3. Re-commit (now with proper .gitignore enforcement from step 0)
                log.AppendLine("⚡ Re-committing changes...");
                await RunCommandAsync("git", $"commit -m \"{escapedMsg}\"", projectRoot);

                // 4. Retry push
                log.AppendLine($"⚡ Retrying push to GitHub...");
                string retryPushResult = await RunCommandAsync("git", $"push origin {branchName}", projectRoot);
                log.AppendLine(retryPushResult);
            }

            return log.ToString();
        }

        private static async Task<string> RunCommandAsync(string fileName, string arguments, string workingDirectory)
        {
            var output = new StringBuilder();
            var errors = new StringBuilder();
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
                    CreateNoWindow = true,
                    EnvironmentVariables = { }
                },
                EnableRaisingEvents = true
            };

            string? userPath = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(userPath))
            {
                process.StartInfo.EnvironmentVariables["PATH"] = userPath;
            }

            process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) errors.AppendLine(e.Data); };
            process.Exited += (_, _) =>
            {
                process.Dispose();
                string stdout = output.ToString().Trim();
                string stderr = errors.ToString().Trim();
                tcs.TrySetResult(string.IsNullOrEmpty(stderr) ? stdout : $"{stdout}\n{stderr}");
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                return $"Error executing command: {ex.Message}";
            }

            return await tcs.Task;
        }
    }
}
