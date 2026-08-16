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
                string prompt = "## TASK\n" +
                               "Write a concise, professional Git commit message based on the code changes provided below.\n\n" +
                               "## FORMAT\n" +
                               "1. Start with a high-level summary line (max 72 chars).\n" +
                               "2. If multiple modules changed, add a brief bulleted list of key technical improvements.\n" +
                               "3. Output ONLY the raw commit message text. No markdown backticks.\n\n" +
                               "## CONTEXT\n" +
                               $"FILE STATS:\n{diffStat}\n\n" +
                               $"CODE CHANGES:\n{diffContent}";

                // Use Gemini Flash specifically for speed on large text processing
                string aiMessage = await LlmRouter.AskAsync(prompt);
                aiMessage = AiAPI.SanitizeText(aiMessage).Trim().Trim('"', '\'');

                if (string.IsNullOrWhiteSpace(aiMessage) || aiMessage.Contains("Error") || aiMessage.Length < 5)
                {
                    aiMessage = $"System Update: {DateTime.Now:yyyy-MM-dd HH:mm}";
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

            // 1. AI .gitignore Analysis (Pre-Push check)
            if (OfflineCacheManager.IsInternetAvailable() || await LlmRouter.IsOllamaAvailableAsync())
            {
                log.AppendLine("--- ANALYZING .GITIGNORE ---");
                string gitignoreResult = await AnalyzeAndFixGitIgnoreAsync(projectRoot);
                log.AppendLine(gitignoreResult);
            }

            string branchName = await RunCommandAsync("git", "rev-parse --abbrev-ref HEAD", projectRoot);
            branchName = branchName.Trim();
            if (string.IsNullOrEmpty(branchName) || branchName.Contains("Error") || branchName.Contains("fatal")) branchName = "main";

            log.AppendLine($"Current branch: {branchName}\n");

            log.AppendLine("--- CLEANING CACHE ---");
            // Ensure we don't accidentally push large build artifacts now that they are in the root
            await RunCommandAsync("git", "rm -r --cached bin obj Data publish *.exe *.dll *.pdb *.deps.json *.runtimeconfig.json", projectRoot);

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

        private static async Task<string> AnalyzeAndFixGitIgnoreAsync(string projectRoot)
        {
            try
            {
                string gitignorePath = Path.Combine(projectRoot, ".gitignore");
                string currentContent = File.Exists(gitignorePath) ? File.ReadAllText(gitignorePath) : "";
                string filesInRoot = string.Join("\n", Directory.GetFiles(projectRoot).Select(Path.GetFileName));

                string prompt = "## TASK\n" +
                               "Analyze the current .gitignore and the list of files in the project root. " +
                               "Identify if any build artifacts, sensitive data, or temporary files are NOT being ignored.\n\n" +
                               "## CURRENT .GITIGNORE:\n" + currentContent + "\n\n" +
                               "## FILES IN ROOT:\n" + filesInRoot + "\n\n" +
                               "## INSTRUCTIONS:\n" +
                               "1. If the .gitignore is already perfect, respond with 'PERFECT'.\n" +
                               "2. If improvements are needed, output ONLY the full, corrected content for the .gitignore file.\n" +
                               "3. Ensure large binaries (.exe, .dll), logs (*.log), and private settings (.json) are ignored.\n" +
                               "4. Output only the file content. No conversation.";

                string aiResult = await LlmRouter.AskAsync(prompt);
                aiResult = AiAPI.SanitizeText(aiResult).Trim();

                if (aiResult.Contains("PERFECT") || string.IsNullOrWhiteSpace(aiResult) || aiResult.Length < 10)
                {
                    return "✅ .gitignore is up to date.";
                }

                // Apply the update
                File.WriteAllText(gitignorePath, aiResult);

                // Clean cache for newly ignored files
                await RunCommandAsync("git", "rm -r --cached .", projectRoot);

                return "✨ AI updated .gitignore and synchronized the index.";
            }
            catch (Exception ex)
            {
                return $"⚠️ .gitignore analysis failed: {ex.Message}";
            }
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
