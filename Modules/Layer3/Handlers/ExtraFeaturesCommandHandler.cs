// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles Desktop File Search (search), Snippets (snip / snippet), App Shortcuts (app / apps), Web Summarizer (fetch), and Sound Volume Presets.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace JarvisLauncher
{
    public class ExtraFeaturesCommandHandler : ICommandHandler
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query.StartsWith("search ") || query == "search" ||
                   query.StartsWith("snip") || query.StartsWith("snippet") ||
                   query.StartsWith("app ") || query == "app" || query == "apps" ||
                   query.StartsWith("fetch ") || query == "fetch" ||
                   query == "monitor" || query == "stats" ||
                   query.StartsWith("vol ") || query == "vol";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim();
            var parts = query.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLower();

            // --- 1. GLOBAL DESKTOP SEARCH ---
            if (cmd == "search")
            {
                if (parts.Length > 1)
                {
                    string target = parts[1].Trim();
                    var foundFiles = SearchDesktopFiles(target);
                    foreach (var file in foundFiles)
                    {
                        string fn = Path.GetFileName(file);
                        suggestions.Add(new CommandResult
                        {
                            Title       = $"📄 {fn}",
                            Description = file,
                            Similarity  = 2.0,
                            Execute     = () => OpenFileNatively(file)
                        });
                    }
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title       = "Search Desktop Files...",
                        Description = "Type a filename or keyword (e.g. 'search report')",
                        Similarity  = 1.5,
                        Execute     = () => InputPromptOverlay.Show("Enter file name to search:", (q) => ExecuteSearch(q))
                    });
                }
            }
            // --- 2. QUICK SNIPPETS ---
            else if (cmd == "snip" || cmd == "snippet")
            {
                var snippets = ExtraFeaturesManager.LoadSnippets();

                if (parts.Length > 1)
                {
                    string args = parts[1];
                    if (args.StartsWith("add "))
                    {
                        var snipParts = args.Substring(4).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                        if (snipParts.Length == 2)
                        {
                            string sName = snipParts[0];
                            string sContent = snipParts[1];
                            suggestions.Add(new CommandResult
                            {
                                Title       = $"Save Snippet: '{sName}'",
                                Description = $"Content: {sContent}",
                                Similarity  = 2.0,
                                Execute     = () => ExtraFeaturesManager.AddSnippet(sName, sContent)
                            });
                        }
                    }
                }

                foreach (var snip in snippets)
                {
                    suggestions.Add(new CommandResult
                    {
                        Title       = $"✂️ Snippet: {snip.Name}",
                        Description = $"Copy: \"{snip.Content}\"",
                        Similarity  = 1.0,
                        Execute     = () => CopySnippet(snip.Content)
                    });
                }

                suggestions.Add(new CommandResult
                {
                    Title       = "Add New Snippet...",
                    Description = "Type format: snippet add <name> <text>",
                    Similarity  = 0.5,
                    Execute     = () => InputPromptOverlay.Show("Enter format: <name> <text>", (str) => ParseAndAddSnippet(str))
                });
            }
            // --- 3. APPLICATION LAUNCHER SHORTCUTS ---
            else if (cmd == "app" || cmd == "apps")
            {
                var apps = ExtraFeaturesManager.LoadAppShortcuts();
                if (parts.Length > 1)
                {
                    string target = parts[1].ToLower();
                    foreach (var a in apps)
                    {
                        if (a.Name.ToLower().Contains(target))
                        {
                            suggestions.Add(new CommandResult
                            {
                                Title       = $"{a.IconEmoji} Launch {a.Name.ToUpper()}",
                                Description = a.TargetPath,
                                Similarity  = 2.0,
                                Execute     = () => LaunchApp(a.TargetPath)
                            });
                        }
                    }
                }
                else
                {
                    foreach (var a in apps)
                    {
                        suggestions.Add(new CommandResult
                        {
                            Title       = $"{a.IconEmoji} Launch {a.Name.ToUpper()}",
                            Description = a.TargetPath,
                            Similarity  = 1.0,
                            Execute     = () => LaunchApp(a.TargetPath)
                        });
                    }
                }
            }
            // --- 4. WEB SCRAPER & SUMMARIZER ---
            else if (cmd == "fetch")
            {
                if (parts.Length > 1)
                {
                    string url = parts[1].Trim();
                    suggestions.Add(new CommandResult
                    {
                        Title       = $"🌐 Fetch & Summarize URL: {url}",
                        Description = "Scrape webpage text and summarize with Gemini AI",
                        Similarity  = 2.0,
                        Execute     = () => FetchAndSummarize(url)
                    });
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title       = "Fetch & Summarize Webpage...",
                        Description = "Prompt for a URL to scrape and summarize",
                        Similarity  = 1.5,
                        Execute     = () => InputPromptOverlay.Show("Enter URL to fetch:", (url) => FetchAndSummarize(url))
                    });
                }
            }
            // --- 5. LIVE SYSTEM MONITOR ---
            else if (cmd == "monitor" || cmd == "stats")
            {
                suggestions.Add(new CommandResult
                {
                    Title       = "⚡ Toggle Live Floating System Monitor",
                    Description = "Display real-time CPU %, RAM, and active processes overlay",
                    Similarity  = 2.0,
                    Execute     = () => SystemMonitorOverlay.ToggleMonitor()
                });
            }
            // --- 6. VOLUME PRESETS ---
            else if (cmd == "vol")
            {
                if (parts.Length > 1)
                {
                    string preset = parts[1].ToLower();
                    if (preset == "night" || preset == "quiet")
                    {
                        suggestions.Add(new CommandResult
                        {
                            Title       = "🌙 Preset: Night Mode (10% Volume)",
                            Description = "Set master volume to 10%",
                            Similarity  = 2.0,
                            Execute     = () => CommandParser.GetSuggestions("volume 10")[0].Execute?.Invoke()
                        });
                    }
                    else if (preset == "gaming" || preset == "music" || preset == "loud")
                    {
                        suggestions.Add(new CommandResult
                        {
                            Title       = "🎵 Preset: Gaming/Music (75% Volume)",
                            Description = "Set master volume to 75%",
                            Similarity  = 2.0,
                            Execute     = () => CommandParser.GetSuggestions("volume 75")[0].Execute?.Invoke()
                        });
                    }
                }
            }

            return suggestions;
        }

        private static List<string> SearchDesktopFiles(string keyword)
        {
            var results = new List<string>();
            try
            {
                string userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string[] searchFolders = new string[]
                {
                    Path.Combine(userDir, "Desktop"),
                    Path.Combine(userDir, "Documents"),
                    Path.Combine(userDir, "Downloads"),
                    Path.Combine(userDir, "Pictures")
                };

                foreach (var folder in searchFolders)
                {
                    if (Directory.Exists(folder))
                    {
                        var files = Directory.GetFiles(folder, $"*{keyword}*", SearchOption.TopDirectoryOnly);
                        foreach (var f in files)
                        {
                            results.Add(f);
                            if (results.Count >= 10) break;
                        }
                    }
                    if (results.Count >= 10) break;
                }
            }
            catch { }
            return results;
        }

        private static void ExecuteSearch(string query)
        {
            var files = SearchDesktopFiles(query);
            if (files.Count > 0)
            {
                OpenFileNatively(files[0]);
            }
            else
            {
                TextOverlay.Show($"⚠️ No files found matching '{query}'", 3000);
            }
        }

        private static void CopySnippet(string content)
        {
            try
            {
                Clipboard.SetText(content);
                TextOverlay.Show("✂️ Snippet copied to clipboard!", 2500);
            }
            catch { }
        }

        private static void ParseAndAddSnippet(string input)
        {
            var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                ExtraFeaturesManager.AddSnippet(parts[0], parts[1]);
            }
            else
            {
                TextOverlay.Show("⚠️ Use format: <name> <text>", 3000);
            }
        }

        private static void LaunchApp(string targetPath)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = targetPath, UseShellExecute = true });
                TextOverlay.Show($"🚀 Launching: {targetPath}", 2500);
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ App launch failed: {ex.Message}", 3000);
            }
        }

        private static void FetchAndSummarize(string url)
        {
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }

            TextOverlay.Show("🌐 Fetching webpage...", 2500);

            Task.Run(async () =>
            {
                try
                {
                    string html = await _httpClient.GetStringAsync(url);
                    // Basic text extraction stripping tags
                    string textOnly = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ");
                    textOnly = System.Text.RegularExpressions.Regex.Replace(textOnly, @"\s+", " ").Trim();
                    if (textOnly.Length > 3000) textOnly = textOnly.Substring(0, 3000);

                    string prompt = $"Please provide a concise summary of the following webpage content extracted from {url}:\n\n{textOnly}";
                    string summary = await AiAPI.AskGemini(prompt);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CliOutputOverlay.Show($"Web Summary: {url}", summary);
                    });
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TextOverlay.Show($"⚠️ Fetch failed: {ex.Message}", 3500);
                    });
                }
            });
        }

        private static void OpenFileNatively(string filePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
                TextOverlay.Show($"🚀 Opening: {Path.GetFileName(filePath)}", 2500);
            }
            catch { }
        }
    }
}
