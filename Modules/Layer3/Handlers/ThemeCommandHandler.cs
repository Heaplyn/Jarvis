// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles CLI commands to preview, select, and persistently load Jarvis launcher window visual themes.

using System;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class ThemeCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "theme" || query.StartsWith("theme ");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim().ToLower();
            var parts = query.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

            string currentTheme = SettingsManager.Current.Theme;

            if (parts.Length > 1)
            {
                string targetTheme = parts[1].Trim().ToLower();
                suggestions.Add(new CommandResult
                {
                    Title       = $"Apply Theme: {targetTheme}",
                    Description = $"Switch visual layout style to {targetTheme} accents",
                    Similarity  = 2.0, // High priority match
                    Execute     = () => ChangeTheme(targetTheme)
                });
            }
            else
            {
                // List all available themes
                var themes = new string[] { "purple", "dark", "blue", "green", "cyberpunk", "glass" };
                foreach (var th in themes)
                {
                    bool isCurrent = th.Equals(currentTheme, StringComparison.OrdinalIgnoreCase);
                    suggestions.Add(new CommandResult
                    {
                        Title       = $"Theme: {th}" + (isCurrent ? " (active)" : ""),
                        Description = $"Switch HUD appearance to {th} theme style",
                        Similarity  = 1.0,
                        Execute     = () => ChangeTheme(th)
                    });
                }
            }

            return suggestions;
        }

        private static void ChangeTheme(string themeName)
        {
            try
            {
                ThemeManager.ApplyTheme(themeName);
                
                // Save persistently in Settings JSON
                SettingsManager.Current.Theme = themeName;
                SettingsManager.Save();

                TextOverlay.Show($"🎨 Theme Switched to {themeName.ToUpper()}", 2500);
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Failed to apply theme: {ex.Message}", 3000);
            }
        }
    }
}
