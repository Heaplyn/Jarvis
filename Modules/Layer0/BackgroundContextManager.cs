// Developer: heaplyn
// Date: 2026-08-14
// Summary: Background Context Manager and Prefetch Optimizer.
//          Periodically gathers environment metrics, active files, and screen context
//          and pre-analyzes them using AI to maintain a compact, pre-fetched context summary.
//          This drastically reduces final LLM prompt tokens and speeds up user query responses.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace JarvisLauncher
{
    public static class BackgroundContextManager
    {
        private static bool _isRunning = false;
        private static string _cachedContextSummary = "User is coding in their workspace.";
        private static DateTime _lastUpdateTime = DateTime.MinValue;
        private static readonly object _lock = new object();

        public static void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            Task.Run(async () =>
            {
                // Give the system some time to fully initialize on boot
                await Task.Delay(10000);

                while (_isRunning)
                {
                    try
                    {
                        if (SettingsManager.Current.IsJarvisEnabled)
                        {
                            await RefreshContextSnapshotAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugConsoleOverlay.Log("Prefetch Error", ex.Message);
                    }

                    // Run prefetch analysis every 45 seconds
                    await Task.Delay(45000);
                }
            });

            DebugConsoleOverlay.Log("ContextPrefetch", "Background context prefetch manager active.");
        }

        public static void Stop()
        {
            _isRunning = false;
        }

        public static string GetActiveContextSummary()
        {
            lock (_lock)
            {
                // If summary is older than 5 minutes, return fallback to prevent stale data usage
                if ((DateTime.Now - _lastUpdateTime).TotalMinutes > 5.0)
                {
                    return string.Empty;
                }
                return _cachedContextSummary;
            }
        }

        private static async Task RefreshContextSnapshotAsync()
        {
            // Gather telemetry components
            string activeWin = MemoryManager.GetCurrentWindowTitle();
            string clipboardText = string.Empty;
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Clipboard.ContainsText()) clipboardText = Clipboard.GetText();
                });
            }
            catch { }

            var wsMemory = WorkspaceMemoryManager.GetCurrent();
            string activeFile = wsMemory.ActiveFileName;
            string activeLang = wsMemory.ActiveProgrammingLanguage;
            string codeSnippet = wsMemory.ActiveCodeSnippet;

            // Combine telemetry data into a structured prompt
            string telemetryData = $"[TELEMETRY SNAPSHOT]\n" +
                                   $"Focused Window: {activeWin}\n" +
                                   $"Workspace Active File: {activeFile} ({activeLang})\n" +
                                   $"Recent Code snippet:\n{codeSnippet}\n" +
                                   $"Clipboard text:\n{clipboardText}\n";

            string prefetchPrompt = $"You are a telemetry pre-analyzer. Summarize this user environment snapshot in 2-3 concise sentences. " +
                                    $"Identify the active programming language, active files, visible developer topics, and user focus. " +
                                    $"Be extremely compact. Here is the telemetry:\n\n{telemetryData}";

            // Query LLM in background (use the fast route)
            try
            {
                string summary = await LlmRouter.AskAsync(prefetchPrompt, null);
                if (!string.IsNullOrWhiteSpace(summary) && !summary.StartsWith("⚠️"))
                {
                    lock (_lock)
                    {
                        _cachedContextSummary = summary.Trim();
                        _lastUpdateTime = DateTime.Now;
                    }
                    DebugConsoleOverlay.Log("ContextPrefetch", $"Pre-analyzed context updated (Length: {_cachedContextSummary.Length} chars).");
                }
            }
            catch (Exception ex)
            {
                DebugConsoleOverlay.Log("ContextPrefetch Note", $"Prefetch pass skipped: {ex.Message}");
            }
        }
    }
}
