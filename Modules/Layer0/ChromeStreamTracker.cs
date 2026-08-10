// Developer: heaplyn
// Date: 2026-08-09
// Summary: Static tracker for the currently spawned Chrome/Edge web stream process.
//          Any part of the app can call ChromeStreamTracker.Set(process) to register,
//          ChromeStreamTracker.KillIfRunning() to terminate, or ChromeStreamTracker.IsRunning to check status.

using System;
using System.Diagnostics;

namespace JarvisLauncher
{
    public static class ChromeStreamTracker
    {
        private static Process? _process = null;
        private static int _pid = -1;

        /// <summary>True if the tracked Chrome stream process is alive.</summary>
        public static bool IsRunning
        {
            get
            {
                if (_process == null) return false;
                try { return !_process.HasExited; }
                catch { return false; }
            }
        }

        /// <summary>The PID of the tracked Chrome stream process, or -1 if none.</summary>
        public static int Pid => _pid;

        /// <summary>
        /// Register a newly spawned Chrome/Edge stream process.
        /// Automatically kills any previously tracked process first.
        /// </summary>
        public static void Set(Process? process)
        {
            KillIfRunning(); // always kill old one first

            _process = process;
            _pid = process != null ? process.Id : -1;
        }

        /// <summary>
        /// Kill the tracked stream process and all chrome children spawned from it.
        /// </summary>
        public static void KillIfRunning()
        {
            int pidToKill = _pid;

            // Clear state immediately before attempting kill
            _process = null;
            _pid = -1;

            if (pidToKill <= 0) return;

            // Kill(entireProcessTree:true) walks the entire child tree from the root PID.
            // This handles Chrome even if the initial launcher process is still alive.
            try
            {
                var proc = Process.GetProcessById(pidToKill);
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(2000);
            }
            catch { }

            // If Chrome's launcher already exited (common), it spawned children under a new parent.
            // Re-check: kill any remaining chrome process whose PID we can still reach via stored PID.
            // Since we can't use WMI without a package, scan chrome processes by creation time proximity.
            KillRecentChromeAppProcesses();
        }

        // Timestamp recorded just before launching the stream - used to find newly spawned Chrome procs
        private static DateTime _launchTime = DateTime.MinValue;

        /// <summary>Call this just before Process.Start so we can find newly spawned Chrome windows.</summary>
        public static void MarkLaunchTime() => _launchTime = DateTime.Now;

        private static void KillRecentChromeAppProcesses()
        {
            if (_launchTime == DateTime.MinValue) return;

            string[] browserNames = { "chrome", "msedge" };
            foreach (var name in browserNames)
            {
                foreach (var proc in Process.GetProcessesByName(name))
                {
                    try
                    {
                        // Only target Chrome processes that started within 5 seconds of our stream launch
                        // AND have a visible window (app window mode always has a main window)
                        if (proc.StartTime >= _launchTime.AddSeconds(-1) &&
                            proc.StartTime <= _launchTime.AddSeconds(5) &&
                            proc.MainWindowHandle != IntPtr.Zero)
                        {
                            proc.Kill(entireProcessTree: true);
                        }
                    }
                    catch { }
                }
            }

            _launchTime = DateTime.MinValue;
        }
    }
}
