// Developer: heaplyn
// Date: 2026-08-09
// Summary: Self-Healing System Manager that automatically repairs missing directories, corrupted JSON configuration files, missing default data files, and catches unhandled exceptions gracefully to prevent application crashes.

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace JarvisLauncher
{
    public static class SelfHealingManager
    {
        public static void Initialize()
        {
            // 1. Hook Global Unhandled Exception Handlers to prevent crashes
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledDomainException;
            Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            // 2. Perform Self-Healing Directory and Data Files Audit
            AuditAndHealDirectories();
            AuditAndHealSettingsFile();
            AuditAndHealDataFiles();
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true; // Mark handled to prevent app crash
            LogException("UI Dispatcher Exception", e.Exception);
            TextOverlay.Show("⚡ Jarvis Self-Healing: Handled background error gracefully", 3000);
        }

        private static void OnUnhandledDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogException("Domain Exception", ex);
            }
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved(); // Mark observed to prevent Task crash
            LogException("Task Exception", e.Exception);
        }

        public static void AuditAndHealDirectories()
        {
            try
            {
                string dataDir = PathHandler.GetDataDirectory();
                string[] requiredDirs = new string[]
                {
                    dataDir,
                    Path.Combine(dataDir, "Instructions"),
                    Path.Combine(dataDir, "Notes"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Macros")
                };

                foreach (var dir in requiredDirs)
                {
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                }
            }
            catch { }
        }

        public static void AuditAndHealSettingsFile()
        {
            try
            {
                string dataDir = PathHandler.GetDataDirectory();
                string settingsFile = Path.Combine(dataDir, "SystemSettings.json");

                if (!File.Exists(settingsFile))
                {
                    SettingsManager.Save();
                }
                else
                {
                    // Verify JSON validity
                    try
                    {
                        string content = File.ReadAllText(settingsFile);
                        using var doc = JsonDocument.Parse(content);
                    }
                    catch (JsonException)
                    {
                        // File corrupted: Restore backup or regenerate default
                        File.Copy(settingsFile, settingsFile + ".corrupted_bak", overwrite: true);
                        SettingsManager.Save();
                        TextOverlay.Show("⚡ Self-Healing: Restored corrupted SystemSettings.json", 3000);
                    }
                }
            }
            catch { }
        }

        public static void AuditAndHealDataFiles()
        {
            try
            {
                string dataDir = PathHandler.GetDataDirectory();

                // PinnedFiles.json
                AuditJsonFile(Path.Combine(dataDir, "PinnedFiles.json"), "[]");

                // ClipboardHistory.json
                AuditJsonFile(Path.Combine(dataDir, "ClipboardHistory.json"), "[]");

                // Snippets.json
                AuditJsonFile(Path.Combine(dataDir, "Snippets.json"), "[]");

                // AppShortcuts.json
                AuditJsonFile(Path.Combine(dataDir, "AppShortcuts.json"), "[]");

                // Create default focus.txt macro if Macros folder is empty
                string macrosDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Macros");
                if (Directory.Exists(macrosDir) && Directory.GetFiles(macrosDir, "*.txt").Length == 0)
                {
                    string focusTxt = Path.Combine(macrosDir, "focus.txt");
                    File.WriteAllText(focusTxt, "# Self-Healed Default Focus Macro\ntheme dark\nvol 10\nremind 45m Take a break\n");
                }
            }
            catch { }
        }

        private static void AuditJsonFile(string filePath, string defaultContent)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    File.WriteAllText(filePath, defaultContent);
                }
                else
                {
                    string json = File.ReadAllText(filePath);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        File.WriteAllText(filePath, defaultContent);
                    }
                    else
                    {
                        using var doc = JsonDocument.Parse(json);
                    }
                }
            }
            catch
            {
                try
                {
                    File.WriteAllText(filePath, defaultContent);
                }
                catch { }
            }
        }

        private static void LogException(string source, Exception ex)
        {
            try
            {
                string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
                string logFile = Path.Combine(dataDir, "SelfHealingLog.txt");

                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {ex.Message}\nStack: {ex.StackTrace}\n\n";
                File.AppendAllText(logFile, entry);
            }
            catch { }
        }
    }
}
