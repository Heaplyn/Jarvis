using System;
using System.Collections.Generic;
using System.IO;

namespace JarvisLauncher.Modules.Layer3.Handlers
{
    public class BackgroundCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            return query.StartsWith("background", StringComparison.OrdinalIgnoreCase) ||
                   query.StartsWith("bg", StringComparison.OrdinalIgnoreCase) ||
                   query.StartsWith("gif", StringComparison.OrdinalIgnoreCase);
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var results = new List<CommandResult>();
            string[] parts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
            {
                results.Add(new CommandResult
                {
                    Title = "🖼️ Background Mode: Gradient",
                    Description = "Switch to animated liquid gradient background",
                    Execute = () => SetBackgroundMode("Gradient")
                });
                results.Add(new CommandResult
                {
                    Title = "🖼️ Background Mode: Solid",
                    Description = "Switch to solid theme color background",
                    Execute = () => SetBackgroundMode("Solid")
                });
                results.Add(new CommandResult
                {
                    Title = "🖼️ Background Mode: Media (GIF)",
                    Description = "Switch to GIF/Media background mode",
                    Execute = () => SetBackgroundMode("Media")
                });
            }
            else if (parts.Length >= 2)
            {
                string sub = parts[1].ToLower();
                if (sub == "set" && parts.Length >= 3)
                {
                    string path = query.Substring(query.IndexOf(parts[2])).Trim();
                    results.Add(new CommandResult
                    {
                        Title = $"🖼️ Set Background GIF: {Path.GetFileName(path)}",
                        Description = $"Use this file as your media background: {path}",
                        Execute = () => SetBackgroundMedia(path)
                    });
                }
            }

            return results;
        }

        private void SetBackgroundMode(string mode)
        {
            SettingsManager.Current.BackgroundMode = mode;
            SettingsManager.Save();
            ThemeManager.ApplyTheme(SettingsManager.Current.Theme);
            TextOverlay.Show($"🖼️ Background Mode: {mode}", 2000);
        }

        private void SetBackgroundMedia(string path)
        {
            if (File.Exists(path))
            {
                SettingsManager.Current.BackgroundMode = "Media";
                SettingsManager.Current.BackgroundMediaSource = path;
                SettingsManager.Save();
                ThemeManager.ApplyTheme(SettingsManager.Current.Theme);
                TextOverlay.Show($"🖼️ Background GIF Set!", 2000);
            }
            else
            {
                TextOverlay.Show("⚠️ File not found!", 2000);
            }
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("background [mode]", "Switch background between Solid, Gradient, or Media", "bg gradient"),
                new CommandDesc("bg set [path]", "Set a specific GIF file as background", "bg set C:\\path\\to\\my.gif")
            };
        }

        public void OnStart() { }
    }
}
