// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles CLI commands to get, set, or reset the custom download directory path for downloaded music media.

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace JarvisLauncher
{
    public class DownloadPathCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "getdlpath" || query == "resetdlpath" || query.StartsWith("setdlpath");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim();
            var parts = query.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

            string cmd = parts[0].ToLower();
            double similarity = 2.0; // High priority match

            if (cmd == "getdlpath")
            {
                string currentPath = SettingsManager.Current.DownloadDirectory;
                string displayPath = string.IsNullOrWhiteSpace(currentPath) 
                    ? GetDefaultDownloadPath() + " [Default]"
                    : currentPath;

                suggestions.Add(new CommandResult
                {
                    Title       = $"Downloads Folder: {displayPath}",
                    Description = "Display the target directory path where music files are saved",
                    Similarity  = similarity,
                    Execute     = null
                });
            }
            else if (cmd == "resetdlpath")
            {
                suggestions.Add(new CommandResult
                {
                    Title       = "Reset Downloads Folder",
                    Description = $"Restore default target path to project folder: {GetDefaultDownloadPath()}",
                    Similarity  = similarity,
                    Execute     = () => ResetDownloadPath()
                });
            }
            else if (cmd == "setdlpath")
            {
                if (parts.Length > 1)
                {
                    string targetPath = parts[1].Trim().Trim('"', '\'');
                    suggestions.Add(new CommandResult
                    {
                        Title       = $"Set Downloads Folder to: {targetPath}",
                        Description = "Update the download destination folder for Lucida/YT-DLP",
                        Similarity  = similarity,
                        Execute     = () => SetDownloadPath(targetPath)
                    });
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title       = "Set Downloads Folder...",
                        Description = "Type the destination folder path (e.g. setdlpath C:\\Users\\Name\\Music)",
                        Similarity  = similarity,
                        Execute     = null
                    });
                }
            }

            return suggestions;
        }

        private static string GetDefaultDownloadPath()
        {
            string devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            if (Directory.Exists(Path.Combine(devPath, "Modules")))
            {
                return Path.Combine(devPath, "Downloads");
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
        }

        private static void SetDownloadPath(string path)
        {
            try
            {
                // Basic path validation
                if (path.IndexOfAny(Path.GetInvalidPathChars()) != -1)
                {
                    TextOverlay.Show("⚠️ Invalid folder path characters detected!", 3000);
                    return;
                }

                // If path doesn't exist, try to create it to verify write permissions
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                SettingsManager.Current.DownloadDirectory = path;
                SettingsManager.Save();
                TextOverlay.Show($"📁 Downloads directory configured successfully!", 2500);
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Failed to save directory: {ex.Message}", 3000);
            }
        }

        private static void ResetDownloadPath()
        {
            try
            {
                SettingsManager.Current.DownloadDirectory = string.Empty;
                SettingsManager.Save();
                TextOverlay.Show("📁 Downloads path reset to project default.", 2500);
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Reset failed: {ex.Message}", 3000);
            }
        }
    }
}
