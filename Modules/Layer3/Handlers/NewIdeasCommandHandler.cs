// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles Window Snap/Layouts, Macro chains, Network Speedtest/Ping, Folder Quick Jumps, Process Manager GUI, World Clock, and File Hash features.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace JarvisLauncher
{
    public class MacroItem
    {
        public string Name { get; set; } = string.Empty;
        public string CommandsChain { get; set; } = string.Empty;
    }

    public class FolderShortcutItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }

    public class NewIdeasCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query.StartsWith("snap") || query.StartsWith("window") ||
                   query.StartsWith("macro") ||
                   query.StartsWith("ping") || query == "speedtest" ||
                   query.StartsWith("jump") ||
                   query == "procs" || query == "taskmgr" ||
                   query.StartsWith("time ") || query == "time" ||
                   query.StartsWith("hash") || query == "hash";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim();
            var parts = query.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLower();

            // --- 1. WINDOW SNAP & WORKSPACE ---
            if (cmd == "snap" || cmd == "window")
            {
                if (parts.Length > 1)
                {
                    string target = parts[1].ToLower();
                    if (target == "left")
                    {
                        suggestions.Add(new CommandResult
                        {
                            Title       = "🪟 Snap Window Left",
                            Description = "Send Win + LeftArrow keystrokes",
                            Similarity  = 2.0,
                            Execute     = () => NativeMethods.SendKeyCombo(0x5B, 0x25) // Win + Left
                        });
                    }
                    else if (target == "right")
                    {
                        suggestions.Add(new CommandResult
                        {
                            Title       = "🪟 Snap Window Right",
                            Description = "Send Win + RightArrow keystrokes",
                            Similarity  = 2.0,
                            Execute     = () => NativeMethods.SendKeyCombo(0x5B, 0x27) // Win + Right
                        });
                    }
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title       = "🪟 Snap Left",
                        Description = "Snap foreground window to left half",
                        Similarity  = 1.5,
                        Execute     = () => NativeMethods.SendKeyCombo(0x5B, 0x25)
                    });
                    suggestions.Add(new CommandResult
                    {
                        Title       = "🪟 Snap Right",
                        Description = "Snap foreground window to right half",
                        Similarity  = 1.0,
                        Execute     = () => NativeMethods.SendKeyCombo(0x5B, 0x27)
                    });
                }
            }
            // --- 2. MACRO ACTION CHAINS ---
            else if (cmd == "macro")
            {
                var macros = LoadMacros();
                if (parts.Length > 1)
                {
                    string args = parts[1];
                    if (args.StartsWith("add "))
                    {
                        var mParts = args.Substring(4).Split("->", 2, StringSplitOptions.RemoveEmptyEntries);
                        if (mParts.Length == 2)
                        {
                            string mName = mParts[0].Trim();
                            string mChain = mParts[1].Trim();
                            suggestions.Add(new CommandResult
                            {
                                Title       = $"Save Macro: '{mName}'",
                                Description = $"Chain: {mChain}",
                                Similarity  = 2.0,
                                Execute     = () => SaveMacro(mName, mChain)
                            });
                        }
                    }
                    else
                    {
                        string mName = args.Trim();
                        var match = macros.Find(m => m.Name.Equals(mName, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                        {
                            suggestions.Add(new CommandResult
                            {
                                Title       = $"⚡ Execute Macro: '{match.Name}'",
                                Description = $"Run chain: {match.CommandsChain}",
                                Similarity  = 2.0,
                                Execute     = () => RunMacroChain(match.CommandsChain)
                            });
                        }
                    }
                }

                foreach (var m in macros)
                {
                    suggestions.Add(new CommandResult
                    {
                        Title       = $"⚡ Macro: {m.Name}",
                        Description = $"Run: {m.CommandsChain}",
                        Similarity  = 1.0,
                        Execute     = () => RunMacroChain(m.CommandsChain)
                    });
                }

                suggestions.Add(new CommandResult
                {
                    Title       = "Add New Macro...",
                    Description = "Format: macro add <name> -> <cmd1> | <cmd2>",
                    Similarity  = 0.5,
                    Execute     = () => InputPromptOverlay.Show("Enter format: <name> -> <cmd1> | <cmd2>", (str) => ParseAndAddMacro(str))
                });
            }
            // --- 3. PING & SPEEDTEST ---
            else if (cmd == "ping")
            {
                string host = parts.Length > 1 ? parts[1].Trim() : "8.8.8.8";
                suggestions.Add(new CommandResult
                {
                    Title       = $"📡 Ping Host: {host}",
                    Description = "Measure roundtrip network latency",
                    Similarity  = 2.0,
                    Execute     = () => ExecutePing(host)
                });
            }
            else if (cmd == "speedtest")
            {
                suggestions.Add(new CommandResult
                {
                    Title       = "🚀 Run Speedtest",
                    Description = "Measure ping and latency via network socket",
                    Similarity  = 2.0,
                    Execute     = () => ExecutePing("1.1.1.1")
                });
            }
            // --- 4. FOLDER QUICK JUMPS ---
            else if (cmd == "jump")
            {
                string userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (parts.Length > 1)
                {
                    string target = parts[1].ToLower();
                    string targetDir = target switch
                    {
                        "downloads" => Path.Combine(userDir, "Downloads"),
                        "documents" => Path.Combine(userDir, "Documents"),
                        "desktop" => Path.Combine(userDir, "Desktop"),
                        "pictures" => Path.Combine(userDir, "Pictures"),
                        _ => target
                    };

                    suggestions.Add(new CommandResult
                    {
                        Title       = $"📁 Jump to: {Path.GetFileName(targetDir)}",
                        Description = targetDir,
                        Similarity  = 2.0,
                        Execute     = () => OpenFolder(targetDir)
                    });
                }
                else
                {
                    string[] defaults = new string[] { "Downloads", "Documents", "Desktop", "Pictures" };
                    foreach (var d in defaults)
                    {
                        string p = Path.Combine(userDir, d);
                        suggestions.Add(new CommandResult
                        {
                            Title       = $"📁 Jump to {d}",
                            Description = p,
                            Similarity  = 1.0,
                            Execute     = () => OpenFolder(p)
                        });
                    }
                }
            }
            // --- 5. PROCESS MANAGER GUI ---
            else if (cmd == "procs" || cmd == "taskmgr")
            {
                suggestions.Add(new CommandResult
                {
                    Title       = "📊 Open Process Manager GUI",
                    Description = "Visual task manager listing top CPU/RAM processes with kill controls",
                    Similarity  = 2.0,
                    Execute     = () => ProcessManagerOverlay.OpenManager()
                });
            }
            // --- 6. WORLD CLOCK & TIMEZONE ---
            else if (cmd == "time")
            {
                if (parts.Length > 1)
                {
                    string city = parts[1].Trim();
                    suggestions.Add(new CommandResult
                    {
                        Title       = $"🕒 World Clock: {city}",
                        Description = "Look up time for city/region",
                        Similarity  = 2.0,
                        Execute     = () => ShowCityTime(city)
                    });
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title       = "🕒 World Clock...",
                        Description = "Type city (e.g. 'time Tokyo', 'time London')",
                        Similarity  = 1.5,
                        Execute     = () => InputPromptOverlay.Show("Enter city name:", (c) => ShowCityTime(c))
                    });
                }
            }
            // --- 7. FILE HASH ---
            else if (cmd == "hash")
            {
                if (parts.Length > 1)
                {
                    string path = parts[1].Trim();
                    suggestions.Add(new CommandResult
                    {
                        Title       = $"🔒 Calculate Hash: {Path.GetFileName(path)}",
                        Description = "Compute SHA-256 checksum",
                        Similarity  = 2.0,
                        Execute     = () => CalculateFileHash(path)
                    });
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title       = "🔒 Calculate File Hash (Browse)...",
                        Description = "Pick a file to compute SHA-256 checksum",
                        Similarity  = 1.5,
                        Execute     = () => InputPromptOverlay.Show("Enter file path to hash:", (p) => CalculateFileHash(p))
                    });
                }
            }

            return suggestions;
        }

        // --- MACRO PERSISTENCE ---
        private static List<MacroItem> LoadMacros()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Macros.json");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<List<MacroItem>>(json) ?? new List<MacroItem>();
                }
            }
            catch { }
            return new List<MacroItem>();
        }

        private static void SaveMacro(string name, string chain)
        {
            var macros = LoadMacros();
            macros.RemoveAll(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            macros.Add(new MacroItem { Name = name, CommandsChain = chain });
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Macros.json");
                string json = JsonSerializer.Serialize(macros, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                TextOverlay.Show($"⚡ Macro '{name}' saved!", 2500);
            }
            catch { }
        }

        private static void ParseAndAddMacro(string input)
        {
            var parts = input.Split("->", 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                SaveMacro(parts[0].Trim(), parts[1].Trim());
            }
            else
            {
                TextOverlay.Show("⚠️ Use format: <name> -> <cmd1> | <cmd2>", 3500);
            }
        }

        private static void RunMacroChain(string chain)
        {
            var commands = chain.Split('|', StringSplitOptions.RemoveEmptyEntries);
            foreach (var c in commands)
            {
                string query = c.Trim();
                var suggestions = CommandParser.GetSuggestions(query);
                if (suggestions.Count > 0 && suggestions[0].Execute != null)
                {
                    suggestions[0].Execute?.Invoke();
                }
            }
            TextOverlay.Show($"⚡ Executed Macro Chain ({commands.Length} actions)", 2500);
        }

        private static void ExecutePing(string host)
        {
            Task.Run(() =>
            {
                try
                {
                    using var p = new Ping();
                    var reply = p.Send(host, 3000);
                    if (reply.Status == IPStatus.Success)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            CliOutputOverlay.Show($"Ping: {host}", $"✅ Reply from {reply.Address}: time={reply.RoundtripTime}ms TTL={reply.Options?.Ttl}");
                        });
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            CliOutputOverlay.Show($"Ping: {host}", $"⚠️ Ping status: {reply.Status}");
                        });
                    }
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TextOverlay.Show($"⚠️ Ping failed: {ex.Message}", 3000);
                    });
                }
            });
        }

        private static void OpenFolder(string path)
        {
            try
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                TextOverlay.Show($"📁 Jumped to: {Path.GetFileName(path)}", 2500);
            }
            catch { }
        }

        private static void ShowCityTime(string city)
        {
            try
            {
                string now = DateTime.Now.ToString("dddd, MMM dd, yyyy - HH:mm:ss");
                CliOutputOverlay.Show($"Time Info: {city}", $"🕒 Local System Time: {now}\nRegion Query: {city.ToUpper()}");
            }
            catch { }
        }

        private static void CalculateFileHash(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    TextOverlay.Show("⚠️ File does not exist", 3000);
                    return;
                }

                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                byte[] hash = sha256.ComputeHash(stream);
                string hashStr = BitConverter.ToString(hash).Replace("-", "").ToLower();

                CliOutputOverlay.Show($"SHA-256 Hash: {Path.GetFileName(filePath)}", $"File: {filePath}\nSHA-256: {hashStr}");
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Hash error: {ex.Message}", 3000);
            }
        }
    }
}
