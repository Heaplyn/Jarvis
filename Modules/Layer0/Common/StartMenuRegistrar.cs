// Developer: heaplyn
// Date: 2026-08-13
// Summary: Automatically creates and registers Start Menu shortcuts for Windows Search Bar indexing.

using System;
using System.IO;

namespace JarvisLauncher
{
    public static class StartMenuRegistrar
    {
        /// <summary>
        /// Registers Jarvis in the Windows Start Menu & Search Bar by creating shortcuts in %APPDATA%\Microsoft\Windows\Start Menu\Programs.
        /// </summary>
        public static void EnsureStartMenuShortcut()
        {
            try
            {
                string programsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    "Programs"
                );

                if (!Directory.Exists(programsDir))
                {
                    Directory.CreateDirectory(programsDir);
                }

                string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JarvisLauncher.exe");
                }

                if (!File.Exists(exePath)) return;

                // 1. Create "Jarvis.lnk" for "Jarvis" search
                CreateShortcut(Path.Combine(programsDir, "Jarvis.lnk"), exePath, "Jarvis HUD Launcher & AI Assistant");

                // 2. Create "Jarvis AI.lnk" for "Jarvis AI" search
                CreateShortcut(Path.Combine(programsDir, "Jarvis AI.lnk"), exePath, "Jarvis AI Assistant");
            }
            catch { }
        }

        private static void CreateShortcut(string shortcutPath, string exePath, string description)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell != null)
                    {
                        dynamic shortcut = shell.CreateShortcut(shortcutPath);
                        shortcut.TargetPath = exePath;
                        shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                        shortcut.Description = description;
                        shortcut.IconLocation = $"{exePath}, 0";
                        shortcut.Save();
                    }
                }
            }
            catch { }
        }
    }
}
