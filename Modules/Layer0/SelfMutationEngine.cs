// Developer: heaplyn
// Date: 2026-08-14
// Summary: AI Auto-Evolution & Code Self-Mutation Engine.
// Allows Jarvis to mutate its own source code, verify compilation with MSBuild, self-heal build failures, and auto-restart to apply mutations.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public static class SelfMutationEngine
    {
        private static readonly string BackupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Backup");
        public static string MutationStatus { get; private set; } = "Idle. Waiting for self-mutation command.";
        public static string MutationLogs { get; private set; } = "";

        static SelfMutationEngine()
        {
            if (!Directory.Exists(BackupDir)) Directory.CreateDirectory(BackupDir);
        }

        public static async Task<MutationResult> MutateCodeAsync(string targetFilePath, string newCodeContent)
        {
            if (!File.Exists(targetFilePath))
            {
                MutationStatus = $"Error: Target file not found: {targetFilePath}";
                return new MutationResult(false, MutationStatus);
            }

            MutationStatus = "Evolving: Backing up file...";
            MutationLogs = $"[Mutation] Backing up: {Path.GetFileName(targetFilePath)}\n";

            string backupPath = Path.Combine(BackupDir, Path.GetFileName(targetFilePath) + ".bak");
            string originalContent = "";

            try
            {
                originalContent = File.ReadAllText(targetFilePath);
                File.WriteAllText(backupPath, originalContent);
                MutationLogs += $"[Mutation] Backup saved to: {backupPath}\n";
            }
            catch (Exception ex)
            {
                MutationStatus = $"Error writing backup: {ex.Message}";
                return new MutationResult(false, MutationStatus);
            }

            // Apply mutation
            MutationStatus = "Evolving: Applying code changes...";
            MutationLogs += "[Mutation] Applying new code content...\n";
            try
            {
                File.WriteAllText(targetFilePath, newCodeContent);
            }
            catch (Exception ex)
            {
                MutationStatus = $"Error applying mutation: {ex.Message}";
                // Restore immediately
                File.WriteAllText(targetFilePath, originalContent);
                return new MutationResult(false, MutationStatus);
            }

            // Compile validation
            MutationStatus = "Evolving: Running MSBuild validation...";
            MutationLogs += "[Mutation] Running 'dotnet build' to verify changes...\n";

            string projectDir = AppDomain.CurrentDomain.BaseDirectory;
            // Traverse up to find sln/csproj
            for (int i = 0; i < 5; i++)
            {
                if (File.Exists(Path.Combine(projectDir, "JarvisLauncher.csproj"))) break;
                var parent = Directory.GetParent(projectDir);
                if (parent == null) break;
                projectDir = parent.FullName;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "build",
                WorkingDirectory = projectDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            bool buildSuccess = await Task.Run(() =>
            {
                try
                {
                    using (var process = new Process { StartInfo = startInfo })
                    {
                        process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
                        process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        process.WaitForExit(45000);

                        return process.ExitCode == 0;
                    }
                }
                catch (Exception ex)
                {
                    errorBuilder.AppendLine($"Build Process Exception: {ex.Message}");
                    return false;
                }
            });

            if (buildSuccess)
            {
                MutationStatus = "Success! Hot-reloading...";
                MutationLogs += "\n🎉 BUILD SUCCESS! Code evolved cleanly.\nInitiating application auto-restart to apply mutations...\n";
                DebugConsoleOverlay.Log("AI Evolution", "Code mutated successfully. Restarting Jarvis.");

                // Wait 2 seconds for logs to write, then hot-restart
                _ = Task.Delay(2000).ContinueWith(_ =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        try { NativeMethods.Restart(); } catch { Environment.Exit(0); }
                    });
                });

                return new MutationResult(true, "Success! App is hot-restarting.");
            }
            else
            {
                MutationStatus = "Compilation Failed. Reverting changes.";
                MutationLogs += $"\n❌ BUILD FAILED!\nErrors:\n{outputBuilder}\n{errorBuilder}\nRestoring backup of: {Path.GetFileName(targetFilePath)}\n";

                // Revert changes
                try
                {
                    File.WriteAllText(targetFilePath, originalContent);
                    MutationLogs += "[Mutation] Original code restored successfully. Ready for AI self-healing correction.\n";
                }
                catch (Exception ex)
                {
                    MutationLogs += $"[Mutation] CRITICAL: Failed to revert backup: {ex.Message}\n";
                }

                return new MutationResult(false, $"Build failed. Errors:\n{outputBuilder}\n{errorBuilder}");
            }
        }
    }

    public class MutationResult
    {
        public bool Success { get; }
        public string Message { get; }
        public MutationResult(bool success, string msg)
        {
            Success = success;
            Message = msg;
        }
    }
}
