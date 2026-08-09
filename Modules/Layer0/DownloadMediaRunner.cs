// Developer: heaplyn
// Date: 2026-08-09
// Summary: Spawns the DownloadMedia TypeScript CLI script, auto-installing Node dependencies and Playwright browsers on first run.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace JarvisLauncher
{
    public static class DownloadMediaRunner
    {
        public static string GetScriptDirectory()
        {
            // 1. Look in the compiled binary execution folder (Publish target)
            string binPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Modules", "Layer0", "DownloadMedia");
            if (Directory.Exists(binPath))
            {
                return binPath;
            }

            // 2. Fallback to the development source folder (3 levels up from bin/Debug/net8.0-windows)
            string devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\Modules\Layer0\DownloadMedia"));
            if (Directory.Exists(devPath))
            {
                return devPath;
            }

            return binPath; // Default fallback path
        }

        public static async Task<string> EnsureDependenciesAsync()
        {
            string scriptDir = GetScriptDirectory();
            if (!Directory.Exists(scriptDir))
            {
                return "Error: DownloadMedia script directory not found.";
            }

            string nodeModulesPath = Path.Combine(scriptDir, "node_modules");
            string flaresolverrPath = Path.Combine(scriptDir, "flaresolverr");

            bool needsNpm = !Directory.Exists(nodeModulesPath);
            bool needsFlare = !Directory.Exists(flaresolverrPath) || !Directory.Exists(flaresolverrPath) || Directory.GetFileSystemEntries(flaresolverrPath).Length == 0;

            if (needsNpm)
            {
                // Notify the user on the UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TextOverlay.Show("📦 Installing downloader dependencies (first run)...", 6000);
                });

                // Run npm install
                string npmResult = await RunSetupCommandAsync("npm.cmd", "install", scriptDir);
                if (npmResult.StartsWith("Error:"))
                {
                    return $"Error: Failed to install npm dependencies:\n{npmResult}\n\nMake sure Node.js is installed on your system.";
                }

                // Run playwright install chromium
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TextOverlay.Show("🌐 Configuring Playwright browser packages...", 5000);
                });

                string pwResult = await RunSetupCommandAsync("npx.cmd", "playwright install chromium", scriptDir);
                if (pwResult.StartsWith("Error:"))
                {
                    return $"Error: Failed to configure Playwright browser packages:\n{pwResult}";
                }
            }

            if (needsFlare)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TextOverlay.Show("⚡ Downloading FlareSolverr bypass proxy server...", 5000);
                });

                // Run node setup.js
                string fsResult = await RunSetupCommandAsync("node", "setup.js", scriptDir);
                if (fsResult.StartsWith("Error:"))
                {
                    return $"Error: Failed to setup FlareSolverr:\n{fsResult}";
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    TextOverlay.Show("✅ FlareSolverr successfully configured!", 3000);
                });
            }

            return "Success";
        }

        public static async Task<string> DownloadAsync(string url)
        {
            string scriptDir = GetScriptDirectory();

            // Verify the script directory exists before launching
            if (!Directory.Exists(scriptDir))
            {
                return $"Error: DownloadMedia script directory not found. Checked locations:\n" +
                       $"- {Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Modules", "Layer0", "DownloadMedia")}\n" +
                       $"- {Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\Modules\Layer0\DownloadMedia"))}";
            }

            // Ensure dependencies are resolved before starting
            string setupResult = await EnsureDependenciesAsync();
            if (setupResult.StartsWith("Error:"))
            {
                return setupResult;
            }

            var output = new StringBuilder();
            var errors = new StringBuilder();
            var tcs = new TaskCompletionSource<string>();

            // Escape double quotes inside URL parameters to prevent CLI argument injection
            string escapedUrl = url.Replace("\"", "\\\"");

            // Run "node" directly using Node 20.6+'s native ESM import hooks to bypass npx.cmd's pathing bugs on Windows
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "node",
                    Arguments              = $"--import tsx DownloadMedia.ts \"{escapedUrl}\"",
                    WorkingDirectory       = scriptDir,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                    EnvironmentVariables   = { }
                },
                EnableRaisingEvents = true
            };

            // Inherit the current user PATH into the spawned process to locate node
            string? userPath = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(userPath))
            {
                process.StartInfo.EnvironmentVariables["PATH"] = userPath;
            }

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) output.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) errors.AppendLine(e.Data);
            };

            process.Exited += (_, _) =>
            {
                int exitCode = -1;
                try { exitCode = process.ExitCode; } catch { }
                process.Dispose();

                string stdout = output.ToString().Trim();
                string stderr = errors.ToString().Trim();

                if (!string.IsNullOrEmpty(stdout))
                    tcs.TrySetResult(stdout);
                else if (!string.IsNullOrEmpty(stderr))
                    tcs.TrySetResult($"[Exit {exitCode}] Error output:\n{stderr}");
                else
                    tcs.TrySetResult($"[Exit {exitCode}] Process finished with no output.");
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                return $"Error: Failed to start Node process.\n{ex.Message}\n\nMake sure Node.js is installed and on your PATH.";
            }

            return await tcs.Task;
        }

        private static async Task<string> RunSetupCommandAsync(string fileName, string arguments, string workingDirectory)
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

            string? userPath = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(userPath))
            {
                process.StartInfo.EnvironmentVariables["PATH"] = userPath;
            }

            process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived  += (_, e) => { if (e.Data != null) errors.AppendLine(e.Data); };
            process.Exited += (_, _) =>
            {
                int exitCode = -1;
                try { exitCode = process.ExitCode; } catch { }
                process.Dispose();

                if (exitCode == 0)
                {
                    tcs.TrySetResult("Success");
                }
                else
                {
                    string stderr = errors.ToString().Trim();
                    tcs.TrySetResult($"Error: Exit code {exitCode}. Details:\n{stderr}");
                }
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                tcs.TrySetResult($"Error: {ex.Message}");
            }

            return await tcs.Task;
        }
    }
}
