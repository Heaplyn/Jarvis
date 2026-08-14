using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace JarvisLauncher
{
    public static class PathHandler
    {
        private static string? _cachedDataDir;

        /// <summary>
        /// Returns a persistent Data directory. In development, points to the project root.
        /// In production/deployed, points to the AppDomain base or LocalAppData.
        /// </summary>
        public static string GetDataDirectory()
        {
            if (_cachedDataDir != null) return _cachedDataDir;

            // 1. Check if we are in a source code project directory
            string checkDir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 6; i++)
            {
                if (File.Exists(Path.Combine(checkDir, "JarvisLauncher.csproj")) ||
                    Directory.Exists(Path.Combine(checkDir, "Modules")))
                {
                    string sourceData = Path.Combine(checkDir, "Data");
                    if (!Directory.Exists(sourceData)) Directory.CreateDirectory(sourceData);
                    _cachedDataDir = sourceData;
                    return _cachedDataDir;
                }
                var parent = Directory.GetParent(checkDir);
                if (parent == null) break;
                checkDir = parent.FullName;
            }

            // 2. Fallback to LocalAppData for persistent storage outside of the installation folder
            string localApp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JarvisHUD", "Data");
            if (!Directory.Exists(localApp)) Directory.CreateDirectory(localApp);
            _cachedDataDir = localApp;

            return _cachedDataDir;
        }

        public static string GetCurrentSourceDirectory([CallerFilePath] string callerPath = "")
        {
            return Path.GetDirectoryName(callerPath) ?? string.Empty;
        }
    }
}
