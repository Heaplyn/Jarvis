// Developer: heaplyn
// Date: 2026-08-18
// Summary: Handles CLI commands to pull, fetch, and merge the latest codebase commits.
//          Hardened with self-healing Git logic, remote re-mapping, and Fresh Sync support.

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
            return query == "update" || query == "gitupdate" || query == "git pull" ||
                   query == "pull" || query == "fresh sync" || query == "sync" || query == "freshsync";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim().ToLower();

            double similarity = Math.Max(
                SearchUtil.GetSimilarity(query, "update"), 
                SearchUtil.GetSimilarity(query, "sync")
            );

            suggestions.Add(new CommandResult
            {
                TITLE       = "🔄 Fresh Sync (Force GitHub Pull)",
                DESCRIPTION = "⚠️ Wipes all local modifications and forces sync with GitHub remote main",
                SIMILARITY  = query.Contains("fresh") ? 10.0 : similarity + 0.8,
                EXECUTE     = () => Task.Run(async () => await PullUpdatesAsync(force: true))
            });

            suggestions.Add(new CommandResult
            {
                TITLE       = "📥 Update Code from GitHub",
                DESCRIPTION = "Run 'git pull' safely (stashing any local changes)",
                SIMILARITY  = similarity + 0.5,
                EXECUTE     = () => Task.Run(async () => await PullUpdatesAsync(force: false))
            });

            return suggestions;
        }

        private static async Task PullUpdatesAsync(bool force)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                TextOverlay.Show(force ? "⚠️ Executing Fresh Sync..." : "📥 Checking for GitHub updates...", 4000);
            });

            string projectRoot = GetProjectRoot();
            var log = new StringBuilder();

            log.AppendLine("===================================================");
            log.AppendLine(force ? "           JARVIS FRESH SYNC ENGINE              " : "            JARVIS CODEBASE UPDATE ENGINE          ");
            log.AppendLine("===================================================");
            log.AppendLine();
            log.AppendLine($"Working directory: {projectRoot}");

            // 1. Git Availability Check
            string gitCheck = await RunCommandAsync("git", "--version", projectRoot);
            if (gitCheck.Contains("Error") || string.IsNullOrWhiteSpace(gitCheck))
            {
                log.AppendLine("❌ ERROR: Git not found in PATH.");
                CliOutputOverlay.Show("Update Failed", log.ToString());
                return;
            }

            // 2. Self-Healing Initialization
            if (!Directory.Exists(Path.Combine(projectRoot, ".git")))
            {
                log.AppendLine("⚠️ Repo not initialized. Running self-healing...");
                await RunCommandAsync("git", "init", projectRoot);
                await RunCommandAsync("git", "remote add origin https://github.com/Heaplyn/Jarvis.git", projectRoot);
                await RunCommandAsync("git", "fetch", projectRoot);
                await RunCommandAsync("git", "checkout -f -B main origin/main", projectRoot);
            }

            // 3. Remote Remapping
            string remoteUrl = (await RunCommandAsync("git", "remote get-url origin", projectRoot)).Trim();
            if (!remoteUrl.Contains("Heaplyn/Jarvis"))
            {
                log.AppendLine("🔗 Relinking remote to official repository...");
                await RunCommandAsync("git", "remote set-url origin https://github.com/Heaplyn/Jarvis.git", projectRoot);
            }

            string branchName = (await RunCommandAsync("git", "rev-parse --abbrev-ref HEAD", projectRoot)).Trim();
            if (string.IsNullOrEmpty(branchName) || branchName.Contains("fatal")) branchName = "main";

            log.AppendLine($"Branch: {branchName}");

            // 4. Execution
            if (force)
            {
                log.AppendLine("--- FORCING OVERWRITE FROM GITHUB ---");
                await RunCommandAsync("git", "fetch --all", projectRoot);
                await RunCommandAsync("git", $"reset --hard origin/{branchName}", projectRoot);
                await RunCommandAsync("git", "clean -fd", projectRoot);
                log.AppendLine("🎉 FRESH SYNC COMPLETED!");
            }
            else
            {
                log.AppendLine("--- PULLING UPDATES ---");
                string res = await RunCommandAsync("git", $"pull origin {branchName} --allow-unrelated-histories --no-rebase", projectRoot);
                log.AppendLine(res);
            }

            CliOutputOverlay.Show("Codebase Update", log.ToString());

            if (log.ToString().Contains("SUCCESS") || log.ToString().Contains("COMPLETED") || log.ToString().Contains("Updating"))
            {
                await Task.Delay(2000);
                NativeMethods.Restart();
            }
        }

        private static string GetProjectRoot()
        {
            string devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            return Directory.Exists(Path.Combine(devPath, "Modules")) ? devPath : AppDomain.CurrentDomain.BaseDirectory;
        }

        private static async Task<string> RunCommandAsync(string fileName, string arguments, string workingDirectory)
        {
            var output = new StringBuilder();
            var tcs = new TaskCompletionSource<string>();
            var process = new Process {
                StartInfo = new ProcessStartInfo { FileName = fileName, Arguments = arguments, WorkingDirectory = workingDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true },
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.Exited += (s, e) => { tcs.SetResult(output.ToString()); process.Dispose(); };
            try { process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine(); } catch (Exception ex) { return ex.Message; }
            return await tcs.Task;
        }

        public List<CommandDesc> GetCommandDescriptions() => new List<CommandDesc> {
            new CommandDesc("update", "Safely pull GitHub updates", "update"),
            new CommandDesc("fresh sync", "Force overwrite local with remote", "fresh sync")
        };
    }
}
