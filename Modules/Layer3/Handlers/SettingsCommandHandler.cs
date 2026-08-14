// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles CLI commands to get or set Google and GitHub API keys, saving changes to SystemSettings.json.

using System;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class SettingsCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            string q = query.Trim().ToLower();
            return q.Contains("setting") || q.Contains("option") || q.Contains("config") || q.Contains("pref")
                || q.StartsWith("setkey") || q.StartsWith("getkey")
                || q.StartsWith("ontop") || q.StartsWith("topmost") || q.StartsWith("alwaysontop")
                || q.StartsWith("disable") || q.StartsWith("enable") || q == "sleep jarvis" || q == "wake jarvis";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            string lowerQuery = query.Trim().ToLower();

            var parts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return suggestions;

            string cmd = parts[0].ToLower();
            double similarity = 2.0; // High priority match

            if (lowerQuery.Contains("setting") || lowerQuery.Contains("option") || lowerQuery.Contains("config") || lowerQuery.Contains("pref"))
            {
                suggestions.Add(new CommandResult
                {
                    Title       = "⚙️ Open Settings & Options GUI",
                    Description = "Visually configure API keys, download folders, and color themes",
                    Similarity  = similarity + 3.0,
                    Execute     = () => SettingsOverlay.OpenSettings()
                });
            }
            else if (cmd == "setkey")
            {
                if (parts.Length > 2)
                {
                    string service = parts[1].ToLower();
                    string value = query.Substring(query.IndexOf(parts[2])).Trim();

                    if (service == "google" || service == "googleai" || service == "gemini")
                    {
                        suggestions.Add(new CommandResult
                        {
                            Title = $"Set Google AI Key",
                            Description = $"Configure the key to: {MaskKey(value)}",
                            Execute = () => SetKey("google", value),
                            Similarity = similarity
                        });
                    }
                    else if (service == "github" || service == "git")
                    {
                        suggestions.Add(new CommandResult
                        {
                            Title = $"Set GitHub Token",
                            Description = $"Configure the token to: {MaskKey(value)}",
                            Execute = () => SetKey("github", value),
                            Similarity = similarity
                        });
                    }
                }
                else if (parts.Length > 1)
                {
                    string service = parts[1].ToLower();
                    suggestions.Add(new CommandResult
                    {
                        Title = $"Set Key for {service}...",
                        Description = $"Type the key (e.g. 'setkey {service} <your_key>')",
                        Execute = null,
                        Similarity = similarity
                    });
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title = "Set Key...",
                        Description = "Type service name (e.g. 'setkey google' or 'setkey github')",
                        Execute = null,
                        Similarity = similarity
                    });
                }
            }
            else if (cmd == "getkey")
            {
                if (parts.Length > 1)
                {
                    string service = parts[1].ToLower();
                    if (service == "google" || service == "googleai" || service == "gemini")
                    {
                        string current = SettingsManager.Current.GoogleAIKey;
                        suggestions.Add(new CommandResult
                        {
                            Title = $"Google AI Key: {MaskKey(current)}",
                            Description = "Displays configured Google API Key",
                            Execute = null,
                            Similarity = similarity
                        });
                    }
                    else if (service == "github" || service == "git")
                    {
                        string current = SettingsManager.Current.GithubToken;
                        suggestions.Add(new CommandResult
                        {
                            Title = $"GitHub Token: {MaskKey(current)}",
                            Description = "Displays configured GitHub Token",
                            Execute = null,
                            Similarity = similarity
                        });
                    }
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title = "Get Key...",
                        Description = "Type service name (e.g. 'getkey google' or 'getkey github')",
                        Execute = null,
                        Similarity = similarity
                    });
                }
            }

            if (cmd == "ontop" || cmd == "topmost" || cmd == "alwaysontop")
            {
                bool current = SettingsManager.Current.AlwaysOnTop;
                if (parts.Length > 1)
                {
                    string arg = parts[1].ToLower();
                    if (arg == "on" || arg == "true" || arg == "1")
                    {
                        suggestions.Add(new CommandResult
                        {
                            Title = "📌 Enable Always On Top",
                            Description = "Keep all Jarvis HUD windows persistently on top of other windows",
                            Execute = () => SetAlwaysOnTop(true),
                            Similarity = similarity + 1.0
                        });
                    }
                    else if (arg == "off" || arg == "false" || arg == "0")
                    {
                        suggestions.Add(new CommandResult
                        {
                            Title = "📌 Disable Always On Top",
                            Description = "Allow Jarvis HUD windows to be placed behind other windows",
                            Execute = () => SetAlwaysOnTop(false),
                            Similarity = similarity + 1.0
                        });
                    }
                    else if (arg == "toggle")
                    {
                        suggestions.Add(new CommandResult
                        {
                            Title = $"📌 Toggle Always On Top (Currently: {(current ? "On" : "Off")})",
                            Description = $"Switch Always On Top to {!current}",
                            Execute = () => SetAlwaysOnTop(!current),
                            Similarity = similarity + 1.0
                        });
                    }
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title = $"📌 Always On Top is {(current ? "Enabled" : "Disabled")}",
                        Description = "Type 'ontop on', 'ontop off', or 'ontop toggle' to configure",
                        Execute = null,
                        Similarity = similarity + 0.5
                    });
                    suggestions.Add(new CommandResult
                    {
                        Title = "📌 Toggle Always On Top",
                        Description = $"Switch Always On Top to {!current}",
                        Execute = () => SetAlwaysOnTop(!current),
                        Similarity = similarity
                    });
                }
            }

            if (cmd == "disable" || cmd == "sleep")
            {
                if (query.Contains("jarvis") || parts.Length == 1)
                {
                    suggestions.Add(new CommandResult
                    {
                        Title = "🔇 Disable Jarvis (Sleep Mode)",
                        Description = "Pause voice activation, tracking, and background analysis",
                        Execute = () => SetJarvisEnabled(false),
                        Similarity = similarity + 1.0
                    });
                }
            }
            else if (cmd == "enable" || cmd == "wake")
            {
                if (query.Contains("jarvis") || parts.Length == 1)
                {
                    suggestions.Add(new CommandResult
                    {
                        Title = "🔋 Enable Jarvis (Wake Up)",
                        Description = "Resume all Jarvis background services and listeners",
                        Execute = () => SetJarvisEnabled(true),
                        Similarity = similarity + 1.0
                    });
                }
            }

            return suggestions;
        }

        private static void SetJarvisEnabled(bool value)
        {
            try
            {
                SettingsManager.Current.IsJarvisEnabled = value;
                SettingsManager.Save();

                if (value)
                {
                    TtsManager.Speak("Jarvis systems online. How can I help you, Kyle?");
                    TextOverlay.Show("🔋 Jarvis Services Enabled", 3000);
                }
                else
                {
                    TtsManager.Speak("Jarvis systems entering sleep mode. Standing by.");
                    TextOverlay.Show("🔇 Jarvis Services Disabled (Sleep)", 3000);
                }
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Error: {ex.Message}", 3000);
            }
        }

        private static void SetAlwaysOnTop(bool value)
        {
            try
            {
                SettingsManager.Current.AlwaysOnTop = value;
                SettingsManager.Save();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (System.Windows.Window win in System.Windows.Application.Current.Windows)
                    {
                        if (win is BaseOverlay baseOverlay)
                        {
                            baseOverlay.Topmost = value;
                        }
                    }
                });

                TextOverlay.Show($"📌 Always On Top {(value ? "Enabled" : "Disabled")}", 2500);
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Failed to set Always On Top: {ex.Message}", 3000);
            }
        }

        private static string MaskKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "[Not Configured]";
            if (key.Length <= 8) return "****";
            return key.Substring(0, 4) + "..." + key.Substring(key.Length - 4);
        }

        private static void SetKey(string service, string key)
        {
            try
            {
                if (service == "google")
                {
                    SettingsManager.Current.GoogleAIKey = key;
                }
                else if (service == "github")
                {
                    SettingsManager.Current.GithubToken = key;
                }

                SettingsManager.Save();
                TextOverlay.Show($"🔑 Configured {service.ToUpper()} key successfully!", 2500);
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Failed to save key: {ex.Message}", 3000);
            }
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("settings / options", "Open visual Options & Settings GUI", "settings"),
                new CommandDesc("apikey <key>", "Configure Gemini API key", "apikey AIzaSy...")
            };
        }
    }
}
