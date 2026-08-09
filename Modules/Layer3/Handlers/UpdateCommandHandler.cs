// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles CLI commands to pull, fetch, and merge the latest codebase commits from the remote GitHub repository.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace JarvisLauncher
{
    public class UpdateCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "update" || query == "gitupdate" || query == "git pull" || query == "pull" || query == "git pull origin";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim().ToLower();

            double similarity = Math.Max(
                SearchUtil.GetSimilarity(query, "update"), 
                SearchUtil.GetSimilarity(query, "pull")
            );

            suggestions.Add(new CommandResult
            {
                Title       = "Update Code from GitHub",
                Description = "Run 'git pull' to merge the latest changes from your remote GitHub origin",
                Similarity  = similarity + 0.5, // Priority boost for direct matches
                Execute     = () => Task.Run(async () => await PullUpdatesAsync())
            });

            return suggestions;
        }

        private static async Task PullUpdatesAsync()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                TextOverlay.Show("📥 Checking for GitHub updates...", 3000);
            });

            string projectRoot = GetProjectRoot();
            var log = new StringBuilder();

            log.AppendLine("===================================================");
            log.AppendLine("            JARVIS CODEBASE UPDATE ENGINE          ");
            log.AppendLine("===================================================");
            log.AppendLine();
            log.AppendLine($"Working directory: {projectRoot}");
            log.AppendLine();

            log.AppendLine("--- CONFIGURING REMOTE ORIGIN ---");
            string remoteUrl = await RunCommandAsync("git", "remote get-url origin", projectRoot);
            remoteUrl = remoteUrl.Trim();
            
            if (remoteUrl.Contains("fatal:") || remoteUrl.Contains("error:") || string.IsNullOrWhiteSpace(remoteUrl))
            {
                log.AppendLine("ℹ️ Remote 'origin' is missing. Adding origin: https://github.com/Heaplyn/Jarvis.git");
                await RunCommandAsync("git", "remote add origin https://github.com/Heaplyn/Jarvis.git", projectRoot);
            }
            else if (!remoteUrl.Contains("Heaplyn/Jarvis"))
            {
                log.AppendLine($"ℹ️ Relinking remote 'origin' from '{remoteUrl}' to 'https://github.com/Heaplyn/Jarvis.git'...");
                await RunCommandAsync("git", "remote set-url origin https://github.com/Heaplyn/Jarvis.git", projectRoot);
            }
            else
            {
                log.AppendLine($"✅ Remote 'origin' correctly mapped to: {remoteUrl}");
            }
            log.AppendLine();

            log.AppendLine("--- PULLING FROM GITHUB ---");
            string pullResult = await RunCommandAsync("git", "pull", projectRoot);
            log.AppendLine(pullResult);
            log.AppendLine();

            if (pullResult.Contains("Already up to date") || pullResult.Contains("Already up-to-date"))
            {
                log.AppendLine("✅ System is already up to date. No updates found.");
            }
            else if (pullResult.Contains("Updating") || pullResult.Contains("Fast-forward") || pullResult.Contains("files changed"))
            {
                log.AppendLine("🎉 UPDATES PULLED SUCCESSFULLY!");
                log.AppendLine("Run the 'restart' command or press Ctrl+Shift+R to rebuild and apply updates.");
            }
            else if (pullResult.Contains("conflict") || pullResult.Contains("Merge conflict"))
            {
                log.AppendLine("⚠️ [CONFLICT] Merge conflicts detected during pull!");
                log.AppendLine("Please resolve conflicts manually in your editor.");
            }

            // Show logs inside system terminal
            CliOutputOverlay.Show("Codebase Update", log.ToString());
        }

        private static string GetProjectRoot()
        {
            string devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            if (Directory.Exists(Path.Combine(devPath, "Modules")))
            {
                return devPath;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
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
                    FileName               = fileName,
                    Arguments              = arguments,
                    WorkingDirectory       = workingDirectory,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                    EnvironmentVariables   = { }
                },
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived  += (s, e) => { if (e.Data != null) errors.AppendLine(e.Data); };

            process.Exited += (s, e) =>
            {
                tcs.SetResult(output.ToString() + "\n" + errors.ToString());
                process.Dispose();
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                return await tcs.Task;
            }
            catch (Exception ex)
            {
                return $"Error executing command: {ex.Message}";
            }
        }
    }
}
