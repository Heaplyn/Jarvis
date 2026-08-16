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
                || q.StartsWith("disable") || q.StartsWith("enable") || q == "sleep jarvis" || q == "wake jarvis"
                || q.StartsWith("debug") || q.StartsWith("verbose")
                || q.StartsWith("obsidian");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            string lowerQuery = query.Trim().ToLower();

            var parts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return suggestions;

            string cmd = parts[0].ToLower();
            double similarity = 2.0; // High priority match

            if (cmd == "obsidian")
            {
                if (parts.Length > 1 && parts[1].ToLower() == "path")
                {
                    if (parts.Length > 2)
                    {
                        string path = query.Substring(query.IndexOf(parts[2])).Trim();
                        suggestions.Add(new CommandResult
                        {
                            TITLE = "Set Obsidian Vault Path",
                            DESCRIPTION = $"Target: {path}",
                            SIMILARITY = 7.0,
                            EXECUTE = () => { SettingsManager.Current.OBSIDIAN_VAULT_PATH = path; SettingsManager.Save(); TextOverlay.Show("✅ Obsidian Vault path updated.", 2500); }
                        });
                    }
                    else
                    {
                        suggestions.Add(new CommandResult
                        {
                            TITLE = "Set Obsidian Vault Path...",
                            DESCRIPTION = "Type path or use 'obsidian path browse'",
                            SIMILARITY = 6.0,
                            EXECUTE = null
                        });
                    }
                }
            }

            if (lowerQuery.Contains("setting") || lowerQuery.Contains("option") || lowerQuery.Contains("config") || lowerQuery.Contains("pref"))
            {
                suggestions.Add(new CommandResult
                {
                    TITLE       = "⚙️ Open Settings & Options GUI",
                    DESCRIPTION = "Visually configure API keys, download folders, and color themes",
                    SIMILARITY  = similarity + 3.0,
                    EXECUTE     = () => SettingsOverlay.OpenSettings()
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
                            TITLE = $"Set Google AI Key",
                            DESCRIPTION = $"Configure the key to: {MaskKey(value)}",
                            EXECUTE = () => SetKey("google", value),
                            SIMILARITY = similarity
                        });
                    }
                    else if (service == "github" || service == "git")
                    {
                        suggestions.Add(new CommandResult
                        {
                            TITLE = $"Set GitHub Token",
                            DESCRIPTION = $"Configure the token to: {MaskKey(value)}",
                            EXECUTE = () => SetKey("github", value),
                            SIMILARITY = similarity
                        });
                    }
                }
                else if (parts.Length > 1)
                {
                    string service = parts[1].ToLower();
                    suggestions.Add(new CommandResult
                    {
                        TITLE = $"Set Key for {service}...",
                        DESCRIPTION = $"Type the key (e.g. 'setkey {service} <your_key>')",
                        EXECUTE = null,
                        SIMILARITY = similarity
                    });
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        TITLE = "Set Key...",
                        DESCRIPTION = "Type service name (e.g. 'setkey google' or 'setkey github')",
                        EXECUTE = null,
                        SIMILARITY = similarity
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
                        string current = SettingsManager.Current.GOOGLE_AI_KEY;
                        suggestions.Add(new CommandResult
                        {
                            TITLE = $"Google AI Key: {MaskKey(current)}",
                            DESCRIPTION = "Displays configured Google API Key",
                            EXECUTE = null,
                            SIMILARITY = similarity
                        });
                    }
                    else if (service == "github" || service == "git")
                    {
                        string current = SettingsManager.Current.GITHUB_TOKEN;
                        suggestions.Add(new CommandResult
                        {
                            TITLE = $"GitHub Token: {MaskKey(current)}",
                            DESCRIPTION = "Displays configured GitHub Token",
                            EXECUTE = null,
                            SIMILARITY = similarity
                        });
                    }
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        TITLE = "Get Key...",
                        DESCRIPTION = "Type service name (e.g. 'getkey google' or 'getkey github')",
                        EXECUTE = null,
                        SIMILARITY = similarity
                    });
                }
            }

            if (cmd == "ontop" || cmd == "topmost" || cmd == "alwaysontop")
            {
                bool current = SettingsManager.Current.ALWAYS_ON_TOP;
                if (parts.Length > 1)
                {
                    string arg = parts[1].ToLower();
                    if (arg == "on" || arg == "true" || arg == "1")
                    {
                        suggestions.Add(new CommandResult
                        {
                            TITLE = "📌 Enable Always On Top",
                            DESCRIPTION = "Keep all Jarvis HUD windows persistently on top of other windows",
                            EXECUTE = () => SetAlwaysOnTop(true),
                            SIMILARITY = similarity + 1.0
                        });
                    }
                    else if (arg == "off" || arg == "false" || arg == "0")
                    {
                        suggestions.Add(new CommandResult
                        {
                            TITLE = "📌 Disable Always On Top",
                            DESCRIPTION = "Allow Jarvis HUD windows to be placed behind other windows",
                            EXECUTE = () => SetAlwaysOnTop(false),
                            SIMILARITY = similarity + 1.0
                        });
                    }
                    else if (arg == "toggle")
                    {
                        suggestions.Add(new CommandResult
                        {
                            TITLE = $"📌 Toggle Always On Top (Currently: {(current ? "On" : "Off")})",
                            DESCRIPTION = $"Switch Always On Top to {!current}",
                            EXECUTE = () => SetAlwaysOnTop(!current),
                            SIMILARITY = similarity + 1.0
                        });
                    }
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        TITLE = $"📌 Always On Top is {(current ? "Enabled" : "Disabled")}",
                        DESCRIPTION = "Type 'ontop on', 'ontop off', or 'ontop toggle' to configure",
                        EXECUTE = null,
                        SIMILARITY = similarity + 0.5
                    });
                    suggestions.Add(new CommandResult
                    {
                        TITLE = "📌 Toggle Always On Top",
                        DESCRIPTION = $"Switch Always On Top to {!current}",
                        EXECUTE = () => SetAlwaysOnTop(!current),
                        SIMILARITY = similarity
                    });
                }
            }

            if (cmd == "disable" || cmd == "sleep")
            {
                if (query.Contains("jarvis") || parts.Length == 1)
                {
                    suggestions.Add(new CommandResult
                    {
                        TITLE = "🔇 Disable Jarvis (Sleep Mode)",
                        DESCRIPTION = "Pause voice activation, tracking, and background analysis",
                        EXECUTE = () => SetJarvisEnabled(false),
                        SIMILARITY = similarity + 1.0
                    });
                }
            }
            else if (cmd == "enable" || cmd == "wake")
            {
                if (query.Contains("jarvis") || parts.Length == 1)
                {
                    suggestions.Add(new CommandResult
                    {
                        TITLE = "🔋 Enable Jarvis (Wake Up)",
                        DESCRIPTION = "Resume all Jarvis background services and listeners",
                        EXECUTE = () => SetJarvisEnabled(true),
                        SIMILARITY = similarity + 1.0
                    });
                }
            }

            if (cmd == "debug" || cmd == "verbose")
            {
                bool current = SettingsManager.Current.VERBOSE_LOGGING;
                if (parts.Length > 1)
                {
                    string arg = parts[1].ToLower();
                    if (arg == "on" || arg == "true" || arg == "1")
                    {
                        suggestions.Add(new CommandResult
                        {
                            TITLE = "🛠️ Enable Verbose Debugging",
                            DESCRIPTION = "Log detailed internal states, network packets, and AI payloads",
                            EXECUTE = () => SetVerboseLogging(true),
                            SIMILARITY = similarity + 1.0
                        });
                    }
                    else if (arg == "off" || arg == "false" || arg == "0")
                    {
                        suggestions.Add(new CommandResult
                        {
                            TITLE = "🛠️ Disable Verbose Debugging",
                            DESCRIPTION = "Return to standard system event logging",
                            EXECUTE = () => SetVerboseLogging(false),
                            SIMILARITY = similarity + 1.0
                        });
                    }
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        TITLE = $"🛠️ Verbose Logging: {(current ? "Enabled" : "Disabled")}",
                        DESCRIPTION = "Type 'debug on' or 'debug off' to toggle internal diagnostics",
                        EXECUTE = () => SetVerboseLogging(!current),
                        SIMILARITY = similarity + 0.5
                    });
                }
            }

            return suggestions;
        }

        private static void SetVerboseLogging(bool value)
        {
            try
            {
                SettingsManager.Current.VERBOSE_LOGGING = value;
                SettingsManager.Save();
                TextOverlay.Show($"🛠️ Verbose Logging {(value ? "ON" : "OFF")}", 2500);
                DebugConsoleOverlay.Log("System", $"Verbose logging mode changed to: {value}");
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Error: {ex.Message}", 3000);
            }
        }

        private static void SetJarvisEnabled(bool value)
        {
            try
            {
                SettingsManager.Current.IS_JARVIS_ENABLED = value;
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
                SettingsManager.Current.ALWAYS_ON_TOP = value;
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
                    SettingsManager.Current.GOOGLE_AI_KEY = key;
                }
                else if (service == "github")
                {
                    SettingsManager.Current.GITHUB_TOKEN = key;
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
                new CommandDesc("apikey <key>", "Configure Gemini API key (use semicolon for multiples)", "apikey AIzaSy1;AIzaSy2")
            };
        }
    }
}
