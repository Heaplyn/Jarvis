// Developer: heaplyn
// Date: 2026-08-10
// Summary: Spawns the Discord Music Downloader TypeScript CLI script to download audio links via Lucida or YT-DLP.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public static class DownloadMediaRunner
    {
        public static Task<string> EnsureDependenciesAsync()
        {
            return Task.FromResult("Success");
        }

        public static async Task<string> DownloadAsync(string url, string? customDestinationDir = null)
        {
            string projectDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Modules", "Layer0", "DownloadMedia");

            if (!Directory.Exists(projectDir))
            {
                // Fallback check
                projectDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DownloadMedia");
            }

            var output = new StringBuilder();
            var errors = new StringBuilder();
            var tcs = new TaskCompletionSource<string>();

            // Escape quotes in URL to prevent CLI parser breakages
            string escapedUrl = url.Replace("\"", "\\\"");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "node",
                    Arguments              = $"--import tsx src/downloadmedia.ts \"{escapedUrl}\"",
                    WorkingDirectory       = projectDir,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true
                },
                EnableRaisingEvents = true
            };

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
                return $"Error: Failed to start npx process.\n{ex.Message}\n\nMake sure Node.js is installed and on your PATH.";
            }

            string resultStr = await tcs.Task;

            // Search for "Path: " in the console output to extract the downloaded file location
            string searchKey = "Path: ";
            int pathIndex = resultStr.IndexOf(searchKey);
            if (pathIndex >= 0)
            {
                int start = pathIndex + searchKey.Length;
                int end = resultStr.IndexOf('\n', start);
                string filePath = (end >= 0 ? resultStr.Substring(start, end - start) : resultStr.Substring(start)).Trim();
                filePath = filePath.Replace("\r", "").Trim(); // Strip carriage returns

                if (File.Exists(filePath))
                {
                    if (!string.IsNullOrEmpty(customDestinationDir))
                    {
                        try
                        {
                            if (!Directory.Exists(customDestinationDir))
                            {
                                Directory.CreateDirectory(customDestinationDir);
                            }
                            string destPath = Path.Combine(customDestinationDir, Path.GetFileName(filePath));
                            if (File.Exists(destPath))
                            {
                                File.Delete(destPath);
                            }
                            File.Move(filePath, destPath);
                            return $"Success:{destPath}";
                        }
                        catch (Exception ex)
                        {
                            return $"Error moving downloaded file: {ex.Message}. File remains at: {filePath}";
                        }
                    }
                    return $"Success:{filePath}";
                }
            }

            // Fallback sweep if "Path: " was not explicitly logged but script reported success
            if (resultStr.Contains("DOWNLOAD SUCCESSFUL"))
            {
                try
                {
                    string dlDir = Path.Combine(projectDir, "downloads");
                    if (Directory.Exists(dlDir))
                    {
                        var files = Directory.GetFiles(dlDir);
                        var newestFile = files.OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
                        if (newestFile != null && File.Exists(newestFile) && (DateTime.Now - File.GetLastWriteTime(newestFile)).TotalSeconds < 60)
                        {
                            if (!string.IsNullOrEmpty(customDestinationDir))
                            {
                                string destPath = Path.Combine(customDestinationDir, Path.GetFileName(newestFile));
                                if (File.Exists(destPath)) File.Delete(destPath);
                                File.Move(newestFile, destPath);
                                return $"Success:{destPath}";
                            }
                            return $"Success:{newestFile}";
                        }
                    }
                }
                catch { }
            }

            return $"Error: Downloader script completed but file wasn't resolved. Console output:\n{resultStr}";
        }
    }
}
