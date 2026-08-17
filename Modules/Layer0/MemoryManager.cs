// Developer: heaplyn
// Date: 2026-08-17
// Summary: User System Activity & History Context Service implementation.

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
    public class MemoryManager : IMemoryService
    {
        private CancellationTokenSource? _cts;
        private static readonly string MemoryFilePath = Path.Combine(InstructionsManager.InstructionsDirectory, "Memories.md");
        private static readonly string ScreenshotsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Screenshots");
        private string _lastWindowTitle = string.Empty;
        private bool _isUserIdle = false;
        private readonly HashSet<string> _trackedProcesses = new HashSet<string>();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        void IMemoryService.Start()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            try { if (!Directory.Exists(ScreenshotsDirectory)) Directory.CreateDirectory(ScreenshotsDirectory); } catch { }
            MemorySyncer.Start();
            Task.Run(() => MainMemoryLoop(_cts.Token));
        }

        void IMemoryService.Stop()
        {
            MemorySyncer.Stop();
            _cts?.Cancel();
            _cts = null;
        }

        string IMemoryService.GetCurrentWindowTitle() => _lastWindowTitle;

        private async Task MainMemoryLoop(CancellationToken token)
        {
            int counter = 0;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!CoreRegistry.Settings.Current.IS_JARVIS_ENABLED) { await Task.Delay(5000, token); continue; }
                    IntPtr handle = NativeMethods.GetForegroundWindow();
                    StringBuilder sb = new StringBuilder(256);
                    if (GetWindowText(handle, sb, 256) > 0)
                    {
                        string title = sb.ToString();
                        if (title != _lastWindowTitle && !string.IsNullOrEmpty(title)) _lastWindowTitle = title;
                    }
                    if (counter % 30 == 0) TrackProcessChanges();
                    counter++;
                } catch { }
                await Task.Delay(1000, token);
            }
        }

        private void TrackProcessChanges() { /* logic */ }

        public static void Start() => CoreRegistry.Memory.Start();
        public static void Stop() => CoreRegistry.Memory.Stop();
        public static string GetCurrentWindowTitle() => CoreRegistry.Memory.GetCurrentWindowTitle();
    }
}
