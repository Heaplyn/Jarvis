// Developer: heaplyn
// Date: 2026-08-14
// Summary: Integrates Sideloadly tool chain for sideloading compiled IPA packages to connected iOS devices.
// Automatically indexes Sideloadly installation path or downloads it if not found.

using System;
using System.Diagnostics;
using System.IO;

namespace JarvisLauncher
{
    public static class SideloadlyIntegrator
    {
        private static readonly string[] SearchPaths = new[]
        {
            @"C:\Program Files\Sideloadly\Sideloadly.exe",
            @"C:\Program Files (x86)\Sideloadly\Sideloadly.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Sideloadly\Sideloadly.exe")
        };

        public static string? GetSideloadlyPath()
        {
            foreach (var path in SearchPaths)
            {
                if (File.Exists(path)) return path;
            }
            return null;
        }

        public static bool IsInstalled => GetSideloadlyPath() != null;

        public static void RunSideload(string ipaPath)
        {
            string? execPath = GetSideloadlyPath();
            if (string.IsNullOrEmpty(execPath) || !File.Exists(execPath))
            {
                TriggerDownload();
                return;
            }

            if (!File.Exists(ipaPath))
            {
                TextOverlay.Show("⚠️ No compiled IPA found to sideload.", 3000);
                return;
            }

            try
            {
                // Run Sideloadly directly passing the IPA as argument
                var psi = new ProcessStartInfo
                {
                    FileName = execPath,
                    Arguments = $"--ipa=\"{ipaPath}\"",
                    UseShellExecute = true
                };
                Process.Start(psi);
                TextOverlay.Show("📲 Launching Sideloadly...", 2500);
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Failed to start Sideloadly: {ex.Message}", 4000);
            }
        }

        public static void TriggerDownload()
        {
            try
            {
                // Open default browser to Sideloadly download page
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://sideloadly.io/index.html",
                    UseShellExecute = true
                });
                TextOverlay.Show("🌐 Opening Sideloadly Download Page...", 3000);
            }
            catch { }
        }
    }
}
