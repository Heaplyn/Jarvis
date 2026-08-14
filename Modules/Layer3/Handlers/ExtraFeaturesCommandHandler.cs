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

        private static bool IsMatch(string input, string target)
        {
            if (string.IsNullOrEmpty(input)) return false;
            return target.StartsWith(input) || input.StartsWith(target);
        }

        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            string firstWord = query.Split(' ')[0];

            return IsMatch(firstWord, "search") ||
                   IsMatch(firstWord, "snippet") || IsMatch(firstWord, "snip") ||
                   IsMatch(firstWord, "app") || IsMatch(firstWord, "apps") ||
                   IsMatch(firstWord, "fetch") ||
                   IsMatch(firstWord, "monitor") || IsMatch(firstWord, "stats") ||
                   IsMatch(firstWord, "tabs") || IsMatch(firstWord, "tab") || IsMatch(firstWord, "browser") ||
                   IsMatch(firstWord, "vol") || IsMatch(firstWord, "volume") ||
                   IsMatch(firstWord, "open") ||
                   IsMatch(firstWord, "edit") ||
                   IsMatch(firstWord, "view") ||
                   IsMatch(firstWord, "mobile") || IsMatch(firstWord, "phone") ||
                   IsMatch(firstWord, "tunnel") || IsMatch(firstWord, "cloudflare") || IsMatch(firstWord, "cloudflared") ||
                   IsMatch(firstWord, "ngrok") ||
                   IsMatch(firstWord, "wifi") || IsMatch(firstWord, "net") ||
                   IsMatch(firstWord, "ping") ||
                   IsMatch(firstWord, "uptime") ||
                   IsMatch(firstWord, "flushdns") || IsMatch(firstWord, "dns") ||
                   IsMatch(firstWord, "speak") || IsMatch(firstWord, "tts") ||
                   IsMatch(firstWord, "copy");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim();
            var parts = query.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLower();

            if (IsMatch(cmd, "wifi") || IsMatch(cmd, "net"))
            {
                bool wantPass = query.ToLower().Contains("pass");
                suggestions.Add(new CommandResult
                {
                    Title = wantPass ? "🔑 View Wi-Fi Password" : "📶 View Wi-Fi Network Info",
                    Description = wantPass ? "Display saved Wi-Fi password for current network" : "Display connected Wi-Fi SSID, signal, & local IP",
                    Similarity = 4.0,
                    Execute = () => ShowWifiInfo(wantPass)
                });
                return suggestions;
            }

            if (IsMatch(cmd, "tunnel") && (parts.Length == 1 || parts[1].Trim().ToLower() == "ui"))
            {
                suggestions.Add(new CommandResult
                {
                    Title = "🔧 Open Tunnel Manager UI",
                    Description = "Open the compact Tunnel Manager overlay to start/stop tunnels",
                    Similarity = 4.0,
                    Execute = () => Application.Current.Dispatcher.Invoke(() => TunnelOverlay.ShowOverlay())
                });
                return suggestions;
            }

            if (IsMatch(cmd, "uptime"))
            {
                suggestions.Add(new CommandResult
                {
                    Title = "⏱️ System Uptime",
                    Description = "Display host PC uptime in days, hours, and minutes",
                    Similarity = 4.0,
                    Execute = ShowUptime
                });
                return suggestions;
            }

            if (IsMatch(cmd, "flushdns") || IsMatch(cmd, "dns"))
            {
                suggestions.Add(new CommandResult
                {
                    Title = "⚡ Flush DNS Resolver Cache",
                    Description = "Purge Windows DNS cache to fix network issues",
                    Similarity = 4.0,
                    Execute = FlushDns
                });
                return suggestions;
            }

            if (IsMatch(cmd, "ping"))
            {
                string host = parts.Length > 1 ? parts[1].Trim() : "google.com";
                suggestions.Add(new CommandResult
                {
                    Title = $"📡 Ping {host}",
                    Description = $"Measure network roundtrip latency to {host}",
                    Similarity = 4.0,
                    Execute = () => PingHost(host)
                });
                return suggestions;
            }

            if (IsMatch(cmd, "speak") || IsMatch(cmd, "tts"))
            {
                string textToSpeak = parts.Length > 1 ? parts[1].Trim() : "Hello Kyle!";
                suggestions.Add(new CommandResult
                {
                    Title = $"🗣️ Speak: \"{textToSpeak}\"",
                    Description = "Synthesize and read text out loud via Windows Speech engine",
                    Similarity = 4.0,
                    Execute = () => SpeakText(textToSpeak)
                });
                return suggestions;
            }

            if (IsMatch(cmd, "copy"))
            {
                string copyText = parts.Length > 1 ? parts[1].Trim() : "";
                suggestions.Add(new CommandResult
                {
                    Title = $"📋 Copy to Clipboard: \"{copyText}\"",
                    Description = "Set text directly to system clipboard",
                    Similarity = 4.0,
                    Execute = () => { try { Clipboard.SetText(copyText); TextOverlay.Show("📋 Copied to Clipboard!", 2000); } catch {} }
                });
                return suggestions;
            }

            if (IsMatch(cmd, "tunnel") || IsMatch(cmd, "cloudflare") || IsMatch(cmd, "cloudflared"))
            {
                if (parts.Length > 1 && parts[1].Trim().Length > 5)
                {
                    string tokenStr = parts[1].Replace("token", "").Trim();
                    suggestions.Add(new CommandResult
                    {
                        Title = $"🔑 Save Permanent Cloudflare Token",
                        Description = $"Save token & bind static custom domain on every restart",
                        Similarity = 4.5,
                        Execute = () =>
                        {
                            CloudflareTunnelManager.SaveTunnelToken(tokenStr);
                            TextOverlay.Show("🔑 Permanent Cloudflare Token Saved!\nRestart tunnel to bind.", 4000);
                        }
                    });
                }

                bool isRunning = CloudflareTunnelManager.IsRunning;
                string activeUrl = CloudflareTunnelManager.PublicUrl ?? "";

                suggestions.Add(new CommandResult
                {
                    Title = isRunning ? $"🌐 Cloudflare Tunnel Active: {activeUrl}" : "🌐 Start Cloudflare Public Web Tunnel",
                    Description = isRunning ? "Click to open public HTTPS URL in browser" : "Auto-downloads cloudflared.exe & hosts Jarvis Mobile App to the public web",
                    Similarity = 3.5,
                    Execute = () =>
                    {
                        System.Threading.Tasks.Task.Run(async () =>
                        {
                            try
                            {
                                string url = await CloudflareTunnelManager.StartTunnelAsync(MobileBridgeServer.Port);
                                MobileOverlay.ShowQrPairingWindow(url);
                                OpenWebBrowser(url);
                            }
                            catch (Exception ex)
                            {
                                TextOverlay.Show($"⚠️ Cloudflare Error: {ex.Message}", 3000);
                            }
                        });
                    }
                });

                // ngrok alternatives
                if (parts.Length > 1 && parts[1].Trim().Length > 5)
                {
                    string tokenStr = parts[1].Replace("token", "").Trim();
                    suggestions.Add(new CommandResult
                    {
                        Title = $"🔑 Save Permanent ngrok Token",
                        Description = $"Save ngrok authtoken for higher-rate/public tunnels",
                        Similarity = 4.5,
                        Execute = () =>
                        {
                            NgrokTunnelManager.SaveAuthToken(tokenStr);
                            TextOverlay.Show("🔑 ngrok Token Saved!\nRestart tunnel to apply.", 4000);
                        }
                    });
                }

                bool ngrokRunning = NgrokTunnelManager.IsRunning;
                string ngrokUrl = NgrokTunnelManager.PublicUrl ?? "";
                suggestions.Add(new CommandResult
                {
                    Title = ngrokRunning ? $"🌐 ngrok Tunnel Active: {ngrokUrl}" : "🌐 Start ngrok Public Web Tunnel",
                    Description = ngrokRunning ? "Open public ngrok URL in browser" : "Auto-downloads ngrok.exe & hosts Jarvis Mobile App to the public web",
                    Similarity = 3.2,
                    Execute = () =>
                    {
                        System.Threading.Tasks.Task.Run(async () =>
                        {
                            try
                            {
                                string url = await NgrokTunnelManager.StartTunnelAsync(MobileBridgeServer.Port);
                                MobileOverlay.ShowQrPairingWindow(url);
                                OpenWebBrowser(url);
                            }
                            catch (Exception ex)
                            {
                                TextOverlay.Show($"⚠️ ngrok Error: {ex.Message}", 3000);
                            }
                        });
                    }
                });
                return suggestions;
            }

            if (IsMatch(cmd, "vercel") || IsMatch(cmd, "deploy vercel"))
            {
                suggestions.Add(new CommandResult
                {
                    Title = "🚀 Deploy Mobile Companion to Vercel (1-Click Free Hosting)",
                    Description = "Get a permanent HTTPS URL (https://jarvis.vercel.app) that never expires or gets 502 errors",
                    Similarity = 4.5,
                    Execute = () => OpenWebBrowser("https://vercel.com/new")
                });
                return suggestions;
            }

            if (IsMatch(cmd, "mobile") || IsMatch(cmd, "phone"))
            {
                string dnsUrl = MobileBridgeServer.JarvisDomain;
                string ipUrl = MobileBridgeServer.ServerUrl;
                string arg = parts.Length > 1 ? parts[1].Trim().ToLower() : "";

                if (arg == "lockdown" || arg == "lock")
                {
                    suggestions.Add(new CommandResult
                    {
                        Title = "🔒 Mobile Privacy Lockdown",
                        Description = "Instantly disable remote terminal, files, screen mirror & clipboard sync",
                        Similarity = 5.0,
                        Execute = () =>
                        {
                            SettingsManager.Current.MobileAllowTerminal = false;
                            SettingsManager.Current.MobileAllowFiles = false;
                            SettingsManager.Current.MobileAllowScreenMirror = false;
                            SettingsManager.Current.MobileAllowClipboard = false;
                            SettingsManager.Save();
                            TextOverlay.Show("🔒 All remote phone capabilities disabled.", 2500);
                        }
                    });
                    return suggestions;
                }

                if (arg == "cloudflare" || arg == "ngrok" || arg == "none")
                {
                    string provider = char.ToUpper(arg[0]) + arg.Substring(1);
                    suggestions.Add(new CommandResult
                    {
                        Title = $"🌍 Set Preferred Tunnel: {provider}",
                        Description = "Auto-start this provider next time the Mobile Hub opens (if auto-start is enabled)",
                        Similarity = 5.0,
                        Execute = () =>
                        {
                            SettingsManager.Current.MobilePreferredTunnel = provider;
                            SettingsManager.Save();
                            TextOverlay.Show($"🌍 Preferred tunnel set to {provider}", 2000);
                        }
                    });
                    return suggestions;
                }

                suggestions.Add(new CommandResult
                {
                    Title = "📷 Scan QR Code to Pair Phone Instantly",
                    Description = "Display QR Code on PC monitor — scan with phone camera to connect in 1 second",
                    Similarity = 4.2,
                    Execute = () => MobileOverlay.ShowQrPairingWindow()
                });

                suggestions.Add(new CommandResult
                {
                    Title = "📱 Open Mobile & Tunnel Hub Overlay",
                    Description = "Manage connection links, Cloudflare/ngrok tunnels, and phone capability customization",
                    Similarity = 4.0,
                    Execute = () => MobileOverlay.ShowOverlay()
                });

                suggestions.Add(new CommandResult
                {
                    Title = "🛠️ Run Mobile Connectivity Diagnostics",
                    Description = "Analyze network interfaces, port status, and firewall configuration",
                    Similarity = 3.8,
                    Execute = () => {
                        var log = MobileBridgeServer.GetRecentLogs(50);
                        ChatOverlay.LogConsoleAction("Connectivity Diagnostics", log);
                        MobileOverlay.ShowOverlay();
                    }
                });

                suggestions.Add(new CommandResult
                {
                    Title = $"🌐 Connect Mobile via DNS: {dnsUrl}",
                    Description = $"Open {dnsUrl} or {ipUrl} on phone browser to connect AI Chat & PC Deck",
                    Similarity = 3.5,
                    Execute = () => OpenWebBrowser(dnsUrl)
                });
                suggestions.Add(new CommandResult
                {
                    Title = $"📱 Connect Mobile via IP: {ipUrl}",
                    Description = "Direct local network IP connection",
                    Similarity = 3.0,
                    Execute = () => OpenWebBrowser(ipUrl)
                });
                return suggestions;
            }

            if (IsMatch(cmd, "open") || IsMatch(cmd, "edit") || IsMatch(cmd, "view"))
            {
                if (parts.Length > 1)
                {
                    string path = parts[1].Trim().Trim('"', '\'');
                    if (File.Exists(path) || Directory.Exists(path))
                    {
                        suggestions.Add(new CommandResult
                        {
                            Title = $"📂 Open: {Path.GetFileName(path)}",
                            Description = $"Open '{path}' in default Windows application",
                            Similarity = 3.0,
                            Execute = () => OpenFileNatively(path)
                        });

                        suggestions.Add(new CommandResult
                        {
                            Title = $"📝 Edit: {Path.GetFileName(path)}",
                            Description = $"Open '{path}' in default text editor",
                            Similarity = 2.8,
                            Execute = () => OpenFileInEditor(path)
                        });
                    }
                    else
                    {
                        suggestions.Add(new CommandResult
                        {
                            Title = $"📂 Open Path: {path}",
                            Description = "Attempt to open specified file or directory path",
                            Similarity = 2.5,
                            Execute = () => OpenFileNatively(path)
                        });
                    }
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title = "📂 Open File...",
                        Description = "Pick a file from dialog to open in default application",
                        Similarity = 2.0,
                        Execute = InteractiveOpenFile
                    });
                }
                return suggestions;
            }

            // --- 1. GLOBAL DESKTOP & WEB SEARCH ---
            if (cmd == "search")
            {
                if (parts.Length > 1)
                {
                    string target = parts[1].Trim();

                    // Option A: Search Google Web Browser
                    suggestions.Add(new CommandResult
                    {
                        Title       = $"🌐 Search Google for \"{target}\"",
                        Description = $"Open browser to search Google for '{target}'",
                        Similarity  = 2.5,
                        Execute     = () => OpenWebBrowser($"https://www.google.com/search?q={Uri.EscapeDataString(target)}")
                    });

                    // Option B: Search DuckDuckGo Web Browser
                    suggestions.Add(new CommandResult
                    {
                        Title       = $"🦆 Search DuckDuckGo for \"{target}\"",
                        Description = $"Open browser to search DuckDuckGo for '{target}'",
                        Similarity  = 2.4,
                        Execute     = () => OpenWebBrowser($"https://duckduckgo.com/?q={Uri.EscapeDataString(target)}")
                    });

                    // Option C: Local Files Search
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
                        Title       = "Search Web in Browser...",
                        Description = "Type query (e.g. 'search how to build a PC')",
                        Similarity  = 1.5,
                        Execute     = () => InputPromptOverlay.Show("Enter web query to search:", (q) => OpenWebBrowser($"https://www.google.com/search?q={Uri.EscapeDataString(q)}"))
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

            // --- 7. BROWSER TABS INSPECTION ---
            if (cmd == "tabs" || cmd == "tab" || cmd == "browsers" || cmd == "browser")
            {
                suggestions.Add(new CommandResult
                {
                    Title = "🌐 Inspect Open Browser Tabs & Windows",
                    Description = "Scans Chrome, Edge, Firefox, Brave, and Opera active window/tab titles",
                    Similarity = 3.0,
                    Execute = () => InspectBrowserTabs()
                });
            }

            return suggestions;
        }

        private static void InspectBrowserTabs()
        {
            var browserNames = new[] { "chrome", "msedge", "firefox", "brave", "opera", "vivaldi" };
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🌐 ACTIVE BROWSER TABS & WINDOWS INSPECTION REPORT");
            sb.AppendLine("-----------------------------------------------------------------------");

            int totalCount = 0;
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    string pName = proc.ProcessName.ToLower();
                    if (Array.Exists(browserNames, b => b == pName))
                    {
                        string title = proc.MainWindowTitle;
                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            totalCount++;
                            sb.AppendLine($"• [{proc.ProcessName.ToUpper()}] {title}");
                        }
                    }
                }
                catch { }
            }

            if (totalCount == 0)
            {
                sb.AppendLine("No active browser windows with visible tab titles were found.");
            }
            else
            {
                sb.AppendLine("-----------------------------------------------------------------------");
                sb.AppendLine($"Total Visible Browser Windows/Tabs Detected: {totalCount}");
            }

            CliOutputOverlay.Show("🌐 Browser Tabs Inspector", sb.ToString());
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
                    string summary = await LlmRouter.AskAsync(prompt);

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

        private static void OpenWebBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                TextOverlay.Show("🌐 Opening default browser...", 2500);
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Browser launch failed: {ex.Message}", 3000);
            }
        }

        private static void InteractiveOpenFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select File to Open",
                Filter = "All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                OpenFileNatively(dlg.FileName);
            }
        }

        private static void OpenFileInEditor(string filePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{filePath}\"",
                    UseShellExecute = true
                });
                TextOverlay.Show($"📝 Editing: {Path.GetFileName(filePath)}", 1500);
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Failed to edit: {ex.Message}", 2500);
            }
        }

        private static void ShowWifiInfo(bool showPassword)
        {
            Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = "wlan show interfaces",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    string output = proc?.StandardOutput.ReadToEnd() ?? "";
                    proc?.WaitForExit(2000);

                    string ssid = "Connected Network";
                    var match = System.Text.RegularExpressions.Regex.Match(output, @"\bSSID\s*:\s*(.+)");
                    if (match.Success) ssid = match.Groups[1].Value.Trim();

                    if (showPassword)
                    {
                        var passPsi = new ProcessStartInfo
                        {
                            FileName = "netsh",
                            Arguments = $"wlan show profile name=\"{ssid}\" key=clear",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var passProc = Process.Start(passPsi);
                        string passOutput = passProc?.StandardOutput.ReadToEnd() ?? "";
                        passProc?.WaitForExit(2000);

                        string keyContent = "Not found";
                        var keyMatch = System.Text.RegularExpressions.Regex.Match(passOutput, @"Key Content\s*:\s*(.+)");
                        if (keyMatch.Success) keyContent = keyMatch.Groups[1].Value.Trim();

                        TextOverlay.Show($"📶 Wi-Fi: {ssid}\n🔑 Password: {keyContent}", 6000);
                    }
                    else
                    {
                        string ip = MobileBridgeServer.GetLocalIPAddress();
                        TextOverlay.Show($"📶 Wi-Fi: {ssid}\n🌐 Local IP: {ip}", 5000);
                    }
                }
                catch (Exception ex)
                {
                    TextOverlay.Show($"📶 Wi-Fi Info: {ex.Message}", 4000);
                }
            });
        }

        private static void ShowUptime()
        {
            TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            TextOverlay.Show($"⏱️ PC Uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s", 4500);
        }

        private static void FlushDns()
        {
            Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "ipconfig",
                        Arguments = "/flushdns",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(3000);
                    TextOverlay.Show("⚡ Windows DNS Resolver Cache Flushed Successfully!", 3500);
                }
                catch (Exception ex)
                {
                    TextOverlay.Show($"⚠️ Flush DNS Failed: {ex.Message}", 3500);
                }
            });
        }

        private static void PingHost(string host)
        {
            Task.Run(async () =>
            {
                try
                {
                    using var p = new System.Net.NetworkInformation.Ping();
                    var reply = await p.SendPingAsync(host, 3000);
                    if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                    {
                        TextOverlay.Show($"📡 Ping to {host}: {reply.RoundtripTime} ms ({reply.Address})", 4000);
                    }
                    else
                    {
                        TextOverlay.Show($"⚠️ Ping to {host} failed: {reply.Status}", 4000);
                    }
                }
                catch (Exception ex)
                {
                    TextOverlay.Show($"⚠️ Ping error: {ex.Message}", 4000);
                }
            });
        }

        private static void SpeakText(string text)
        {
            Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"Add-Type -AssemblyName System.Speech; $synth = New-Object System.Speech.Synthesis.SpeechSynthesizer; $synth.Speak('{text.Replace("'", "''")}')\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(psi);
                }
                catch { }
            });
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("wifi / net", "Show connected Wi-Fi network SSID & local IP", "wifi"),
                new CommandDesc("wifi pass", "Show saved Wi-Fi password for current network", "wifi pass"),
                new CommandDesc("ping <host>", "Measure network latency / roundtrip response time", "ping google.com"),
                new CommandDesc("uptime", "Display host PC running time in days, hours, & mins", "uptime"),
                new CommandDesc("flushdns / dns", "Flush Windows DNS resolver cache", "flushdns"),
                new CommandDesc("speak / tts <text>", "Synthesize and read text out loud via TTS", "speak Jarvis online"),
                new CommandDesc("copy <text>", "Copy text directly to system clipboard", "copy hello world"),
                new CommandDesc("tunnel / cloudflare", "Host Jarvis Mobile Web App to public HTTPS web via Cloudflare Tunnel", "tunnel"),
                new CommandDesc("mobile / phone", "Open the unified Mobile & Tunnel Hub for phone pairing & tunnel control", "mobile"),
                new CommandDesc("mobile lockdown", "Instantly disable all remote phone capabilities (privacy panic button)", "mobile lockdown"),
                new CommandDesc("mobile cloudflare/ngrok/none", "Set the preferred auto-start tunnel provider", "mobile ngrok"),
                new CommandDesc("open [file]", "Open file or folder in default Windows application", "open C:\\doc.pdf"),
                new CommandDesc("edit <file>", "Open file in default text editor", "edit main.cs"),
                new CommandDesc("google <query>", "Search Google in default browser", "google WPF layouts"),
                new CommandDesc("search <query>", "Search files across Desktop & Documents", "search report"),
                new CommandDesc("snippet / snip", "Manage and copy saved text snippets", "snip"),
                new CommandDesc("app <name>", "Launch software application shortcut", "app chrome"),
                new CommandDesc("fetch <url>", "Scrape & summarize webpage with AI", "fetch https://..."),
                new CommandDesc("vol night/gaming", "Quick volume preset profiles", "vol night"),
                new CommandDesc("tabs / browser", "Inspect active browser tab titles", "tabs")
            };
        }
    }
}
