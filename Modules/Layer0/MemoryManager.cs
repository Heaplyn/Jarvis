using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace JarvisLauncher
{
    public static class MemoryManager
    {
        private static CancellationTokenSource? _cts;
        private static readonly string MemoryFilePath = Path.Combine(InstructionsManager.InstructionsDirectory, "Memories.md");
        private static readonly string ScreenshotsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Screenshots");
        private static string _lastWindowTitle = string.Empty;
        private static bool _isUserIdle = false;
        private static DateTime _lastActivityChange = DateTime.Now;
        private static readonly HashSet<string> _trackedProcesses = new HashSet<string>();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        public static void Start()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();

            // Ensure directories exist
            try
            {
                if (!Directory.Exists(ScreenshotsDirectory)) Directory.CreateDirectory(ScreenshotsDirectory);
            }
            catch { }

            // Start memory syncer for external AI coding apps
            MemorySyncer.Start();

            // Loop for window tracking and occasional screen analysis
            Task.Run(() => MainMemoryLoop(_cts.Token));

            LogInternalAction("Memory System Initialized - Enhanced Tracking Active");
        }

        public static void Stop()
        {
            MemorySyncer.Stop();
            _cts?.Cancel();
            _cts = null;
        }

        public static string GetCurrentWindowTitle()
        {
            return _lastWindowTitle;
        }

        public static void LogInternalAction(string action)
        {
            try
            {
                // Ensure daily header exists
                string dailyHeader = $"## Activity Log - {DateTime.Now:yyyy-MM-dd}";
                string content = "";
                if (File.Exists(MemoryFilePath)) content = File.ReadAllText(MemoryFilePath);

                if (!content.Contains(dailyHeader))
                {
                    File.AppendAllText(MemoryFilePath, $"\n{dailyHeader}\n===============================\n\n");
                }

                string entry = $"[{DateTime.Now:HH:mm:ss}] {action}\n";
                File.AppendAllText(MemoryFilePath, entry);
            }
            catch { }
        }

        private static async Task MainMemoryLoop(CancellationToken token)
        {
            int screenAnalysisCounter = 0;
            int processCheckCounter = 0;
            int screenshotCounter = 0;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!SettingsManager.Current.IS_JARVIS_ENABLED)
                    {
                        await Task.Delay(5000, token);
                        continue;
                    }

                    // 1. Track Active Window Changes
                    IntPtr handle = NativeMethods.GetForegroundWindow();
                    StringBuilder sb = new StringBuilder(256);
                    if (GetWindowText(handle, sb, 256) > 0)
                    {
                        string title = sb.ToString();
                        if (title != _lastWindowTitle && !string.IsNullOrEmpty(title))
                        {
                            _lastWindowTitle = title;
                            LogInternalAction($"Focus: \"{title}\"");
                        }
                    }

                    // 2. Track Idle State (User Away/Back)
                    uint idleTimeMs = NativeMethods.GetIdleTime();
                    bool nowIdle = idleTimeMs > 60000; // 1 minute idle
                    if (nowIdle != _isUserIdle)
                    {
                        _isUserIdle = nowIdle;
                        string state = nowIdle ? "User is Away (Idle)" : "User returned to PC";
                        LogInternalAction(state);
                    }

                    // 3. Track Process Changes (every 30 seconds - reduced frequency for speed)
                    if (processCheckCounter >= 30)
                    {
                        processCheckCounter = 0;
                        TrackProcessChanges();
                    }

                    // 4. Regular Screenshot (every 50 seconds)
                    if (screenshotCounter >= 50 && !_isUserIdle)
                    {
                        screenshotCounter = 0;
                        SaveMemoryScreenshot();
                    }

                    // 5. Perform AI Screen Analysis every 3 minutes (approx 180 * 1sec loop)
                    if (screenAnalysisCounter >= 180 && !_isUserIdle)
                    {
                        screenAnalysisCounter = 0;
                        await PerformScreenAnalysis();
                    }

                    screenAnalysisCounter++;
                    processCheckCounter++;
                    screenshotCounter++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Memory Loop Error: {ex.Message}");
                }

                await Task.Delay(1000, token);
            }
        }

        private static void SaveMemoryScreenshot()
        {
            try
            {
                var bytes = ScreenCaptureUtil.CapturePrimaryScreen();
                if (bytes != null)
                {
                    string filename = $"Screen_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    string path = Path.Combine(ScreenshotsDirectory, filename);
                    File.WriteAllBytes(path, bytes);

                    // Cleanup old screenshots (keep last 200)
                    var files = Directory.GetFiles(ScreenshotsDirectory, "*.jpg")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.CreationTime)
                        .ToList();

                    if (files.Count > 200)
                    {
                        foreach (var old in files.Skip(200))
                        {
                            try { old.Delete(); } catch { }
                        }
                    }
                }
            }
            catch { }
        }

        private static void TrackProcessChanges()
        {
            try
            {
                var currentProcesses = Process.GetProcesses()
                    .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
                    .Select(p => p.ProcessName)
                    .ToHashSet();

                // New processes
                foreach (var p in currentProcesses)
                {
                    if (!_trackedProcesses.Contains(p))
                    {
                        _trackedProcesses.Add(p);
                        // Only log interesting ones
                        if (!IsSystemProcess(p)) LogInternalAction($"App Started: {p}");
                    }
                }

                // Closed processes
                var closed = _trackedProcesses.Where(p => !currentProcesses.Contains(p)).ToList();
                foreach (var p in closed)
                {
                    _trackedProcesses.Remove(p);
                    if (!IsSystemProcess(p)) LogInternalAction($"App Closed: {p}");
                }
            }
            catch { }
        }

        private static bool IsSystemProcess(string name)
        {
            string[] sys = { "svchost", "conhost", "dllhost", "RuntimeBroker", "SearchHost", "StartMenuExperienceHost", "JarvisLauncher" };
            return sys.Contains(name, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task PerformScreenAnalysis()
        {
            try
            {
                string? base64 = ScreenCaptureUtil.CapturePrimaryScreenToBase64();
                if (!string.IsNullOrEmpty(base64))
                {
                    string prompt = "Describe the user's current project or activity based on this screen. Be specific about code content, browser tabs, or design work. One concise sentence.";
                    string description = await AiAPI.AnalyzeImageAsync(prompt, base64);

                    if (!description.StartsWith("Error") && !string.IsNullOrWhiteSpace(description))
                    {
                        LogInternalAction($"Visual Context: {description.Trim()}");
                    }
                }
            }
            catch { }
        }
    }
}
