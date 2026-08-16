// Developer: heaplyn
// Date: 2026-08-13
// Summary: Indexes Windows installed desktop applications and Start Menu shortcuts for fast HUD search bar autocomplete.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace JarvisLauncher
{
    public class InstalledApp
    {
        public string Name { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
    }

    public static class WindowsAppScanner
    {
        private static readonly List<InstalledApp> _cachedApps = new();
        private static bool _isIndexed = false;
        private static bool _isIndexingInProgress = false;
        private static readonly object _lock = new();

        static WindowsAppScanner()
        {
            // Start indexing immediately on a background thread
            Task.Run(() => IndexApplications());
        }

        public static void IndexApplications(bool force = false)
        {
            lock (_lock)
            {
                if (!force && _isIndexed && _cachedApps.Count > 0) return;
                if (_isIndexingInProgress) return;
                _isIndexingInProgress = true;
            }

            Task.Run(() => {
                try {
                    DebugConsoleOverlay.Log("App-Scanner", force ? "Forcing full application re-index..." : "Starting application indexing...");
                    var appMap = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);

                    // 1. Common Windows Apps (Built-in)
                    AddBuiltInApps(appMap);

                    // 2. Scan Start Menu Directories
                    ScanStartMenuDirectory(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), appMap);
                    ScanStartMenuDirectory(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), appMap);

                    // 3. Scan LocalAppData Programs
                    string localProg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
                    if (Directory.Exists(localProg))
                    {
                        ScanDirectoryForExes(localProg, appMap);
                    }

                    // 4. Scan Registry Uninstall keys
                    ScanRegistryApps(appMap);

                    lock (_lock) {
                        _cachedApps.Clear();
                        _cachedApps.AddRange(appMap.Values.OrderBy(a => a.Name));
                        _isIndexed = true;
                        _isIndexingInProgress = false;
                    }
                    DebugConsoleOverlay.Log("App-Scanner", $"Indexing complete. Found {_cachedApps.Count} applications.");
                }
                catch (Exception ex) {
                    lock(_lock) { _isIndexingInProgress = false; }
                    DebugConsoleOverlay.Log("App-Scanner", $"Indexing failed: {ex.Message}");
                }
            });
        }

        public static List<CommandResult> GetMatchingApps(string query)
        {
            var results = new List<CommandResult>();
            if (!SettingsManager.Current.ENABLE_WINDOWS_APP_INDEXING || string.IsNullOrWhiteSpace(query)) return results;

            // If not indexed and not currently indexing, trigger it.
            // But don't wait for it here to avoid blocking HUD responsiveness.
            if (!_isIndexed && !_isIndexingInProgress) IndexApplications();

            // If we have NO apps yet, just return empty instead of blocking.
            if (_cachedApps.Count == 0) return results;

            string q = query.ToLower().Trim();

            lock (_lock)
            {
                foreach (var app in _cachedApps)
                {
                    if (string.IsNullOrEmpty(app.Name)) continue;

                    string name = app.Name.ToLower();

                    bool isMatch = name.StartsWith(q) ||
                                   name.Contains(q) ||
                                   SearchUtil.IsAcronymMatch(q, name) ||
                                   SearchUtil.IsClose(q, name);

                    if (isMatch)
                    {
                        double sim = SearchUtil.GetSimilarity(q, name);
                        if (sim < 1.0) sim = name.StartsWith(q) ? 4.8 : 3.5;

                        string path = app.TargetPath;
                        results.Add(new CommandResult
                        {
                            TITLE = $"📱 App: {app.Name}",
                            DESCRIPTION = $"Launch {Path.GetFileName(app.TargetPath)}",
                            SIMILARITY = sim,
                            EXECUTE = () => LaunchApp(path)
                        });
                    }
                }
            }

            return results;
        }

        private static void AddBuiltInApps(Dictionary<string, InstalledApp> map)
        {
            AddApp(map, "Calculator", "calc.exe");
            AddApp(map, "Notepad", "notepad.exe");
            AddApp(map, "Paint", "mspaint.exe");
            AddApp(map, "Task Manager", "taskmgr.exe");
            AddApp(map, "Command Prompt", "cmd.exe");
            AddApp(map, "PowerShell", "powershell.exe");
            AddApp(map, "Windows Terminal", "wt.exe");
            AddApp(map, "File Explorer", "explorer.exe");
            AddApp(map, "Control Panel", "control.exe");
            AddApp(map, "Snipping Tool", "snippingtool.exe");
            AddApp(map, "Device Manager", "devmgmt.msc");
        }

        private static void AddApp(Dictionary<string, InstalledApp> map, string name, string path)
        {
            if (!map.ContainsKey(name))
            {
                map[name] = new InstalledApp { Name = name, TargetPath = path };
            }
        }

        private static void ScanStartMenuDirectory(string baseDir, Dictionary<string, InstalledApp> map)
        {
            try
            {
                if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(baseDir)) return;

                var files = Directory.GetFiles(baseDir, "*.lnk", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file);

                    // Exclude uninstaller / help links
                    if (name.StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("Help", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("Website", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Documentation", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!map.ContainsKey(name))
                    {
                        map[name] = new InstalledApp { Name = name, TargetPath = file };
                    }
                }
            }
            catch { }
        }

        private static void ScanDirectoryForExes(string dir, Dictionary<string, InstalledApp> map)
        {
            try
            {
                var exes = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories);
                foreach (var exe in exes)
                {
                    string name = Path.GetFileNameWithoutExtension(exe);
                    if (name.StartsWith("unins", StringComparison.OrdinalIgnoreCase) || name.Contains("Update")) continue;

                    if (!map.ContainsKey(name))
                    {
                        map[name] = new InstalledApp { Name = name, TargetPath = exe };
                    }
                }
            }
            catch { }
        }

        private static void ScanRegistryApps(Dictionary<string, InstalledApp> map)
        {
            string[] registryPaths = {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var regPath in registryPaths)
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(regPath);
                    if (key != null)
                    {
                        foreach (var subkeyName in key.GetSubKeyNames())
                        {
                            using var subkey = key.OpenSubKey(subkeyName);
                            if (subkey != null)
                            {
                                string displayName = subkey.GetValue("DisplayName") as string ?? "";
                                string displayIcon = subkey.GetValue("DisplayIcon") as string ?? "";
                                string installLocation = subkey.GetValue("InstallLocation") as string ?? "";

                                if (!string.IsNullOrWhiteSpace(displayName) && !map.ContainsKey(displayName))
                                {
                                    string exePath = displayIcon.Split(',')[0].Trim('"');
                                    if (File.Exists(exePath))
                                    {
                                        map[displayName] = new InstalledApp { Name = displayName, TargetPath = exePath };
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private static void LaunchApp(string targetPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = targetPath,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                try
                {
                    Process.Start("cmd.exe", $"/c start \"\" \"{targetPath}\"");
                }
                catch { }
            }
        }
    }
}
