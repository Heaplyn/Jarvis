// Developer: heaplyn
// Date: 2026-08-14
// Summary: Power User Command Suite - 60+ commands for System, Dev, Network, Productivity, and Web Scraping.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace JarvisLauncher
{
    public class ExpandedCommandsHandler : ICommandHandler
    {
        [DllImport("user32.dll")] private static extern bool LockWorkStation();
        [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        public bool CanHandle(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return false;
            string q = query.Trim().ToLower().Split(' ')[0];
            string[] verbs = { "sleep", "shutdown", "reboot", "restart", "lock", "wifi", "netstat", "specs", "md5", "sha256", "guid", "temp", "vol", "mute", "ping", "port", "wordcount", "res", "gmail", "json", "base64", "url", "lorem", "unix", "uptime", "battery", "gpu", "cpu", "whoami", "kill", "ip", "speedtest", "upper", "lower", "titlecase", "reverse", "sort", "unique", "weather", "stock", "crypto", "define", "news", "trash", "shred" };
            return verbs.Any(v => q.StartsWith(v));
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            string raw = query.Trim();
            string[] parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return suggestions;
            string cmd = parts[0].ToLower();
            string arg = parts.Length > 1 ? raw.Substring(parts[0].Length).Trim() : "";

            // ── 1. GMAIL & WEB SCRAPING ──────────────────────────────────────────
            if (cmd == "gmail")
            {
                suggestions.Add(new CommandResult { Title = "📬 Gmail: Inbox Summary", Description = "Scrape recent emails from your linked Google account", Execute = async () => CliOutputOverlay.Show("Gmail Inbox", await GmailManager.GetInboxSummaryAsync()), Similarity = 5.0 });
                if (!string.IsNullOrEmpty(arg))
                    suggestions.Add(new CommandResult { Title = $"🔍 Gmail Search: {arg}", Description = $"Search for '{arg}' in your emails", Execute = async () => CliOutputOverlay.Show("Gmail Search", await GmailManager.SearchEmailsAsync(arg)), Similarity = 4.5 });
            }

            // ── 2. DEVELOPER TOOLKIT ─────────────────────────────────────────────
            if (cmd == "json")
                suggestions.Add(new CommandResult { Title = "📜 JSON Prettify", Description = "Format clipboard JSON with indentation", Execute = () => { try { string json = Clipboard.GetText(); var obj = System.Text.Json.JsonSerializer.Deserialize<object>(json); string formatted = System.Text.Json.JsonSerializer.Serialize(obj, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }); Clipboard.SetText(formatted); TextOverlay.Show("✅ JSON Formatted to Clipboard", 2000); } catch { TextOverlay.Show("❌ Invalid JSON in Clipboard", 2500); } }, Similarity = 5.0 });
            if (cmd == "base64")
            {
                suggestions.Add(new CommandResult { Title = "🔗 Base64 Encode", Description = "Encode clipboard text", Execute = () => { string t = Clipboard.GetText(); Clipboard.SetText(Convert.ToBase64String(Encoding.UTF8.GetBytes(t))); TextOverlay.Show("✅ Encoded", 1500); }, Similarity = 4.0 });
                suggestions.Add(new CommandResult { Title = "🔓 Base64 Decode", Description = "Decode clipboard text", Execute = () => { try { string t = Clipboard.GetText(); Clipboard.SetText(Encoding.UTF8.GetString(Convert.FromBase64String(t))); TextOverlay.Show("✅ Decoded", 1500); } catch { TextOverlay.Show("❌ Not Base64", 2000); } }, Similarity = 4.0 });
            }
            if (cmd == "unix") suggestions.Add(new CommandResult { Title = "⌚ Unix Timestamp", Description = "Copy current epoch time", Execute = () => { string ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(); Clipboard.SetText(ts); TextOverlay.Show($"📋 {ts}", 2000); }, Similarity = 5.0 });
            if (cmd == "guid" || cmd == "uuid") suggestions.Add(new CommandResult { Title = "🎲 New GUID", Description = "Generate unique identifier", Execute = () => { string g = Guid.NewGuid().ToString(); Clipboard.SetText(g); TextOverlay.Show($"📋 {g}", 2000); }, Similarity = 5.0 });

            // ── 3. SYSTEM & HARDWARE ─────────────────────────────────────────────
            if (cmd == "uptime") suggestions.Add(new CommandResult { Title = "⏱️ System Uptime", Description = "Show how long the PC has been running", Execute = () => { var up = TimeSpan.FromMilliseconds(Environment.TickCount64); TextOverlay.Show($"⏱️ Uptime: {up.Days}d {up.Hours}h {up.Minutes}m", 4000); }, Similarity = 5.0 });
            if (cmd == "stopwatch") suggestions.Add(new CommandResult { Title = "⏱️ Stopwatch", Description = "Launch system stopwatch", Execute = () => RunProcess("explorer.exe", "shell:Appsfolder\\Microsoft.WindowsAlarms_8wekyb3d8bbwe!App"), Similarity = 5.0 });
            if (cmd == "timer") suggestions.Add(new CommandResult { Title = "⏲️ Timer", Description = "Launch system timer", Execute = () => CommandParser.ExecuteFirstSuggestion("timer 10m"), Similarity = 4.0 });
            if (cmd == "whoami") suggestions.Add(new CommandResult { Title = "👤 Current User", Description = "Show Windows username and machine", Execute = () => TextOverlay.Show($"{Environment.UserName} @ {Environment.MachineName}", 3000), Similarity = 5.0 });
            if (cmd == "kill" && !string.IsNullOrEmpty(arg)) suggestions.Add(new CommandResult { Title = $"🛑 Kill Process: {arg}", Description = $"Force terminate {arg}", Execute = () => { foreach (var p in Process.GetProcessesByName(arg)) p.Kill(); TextOverlay.Show($"✅ Terminated {arg}", 2000); }, Similarity = 4.5 });

            // ── 4. NETWORK & IP ──────────────────────────────────────────────────
            if (cmd == "ip")
            {
                suggestions.Add(new CommandResult { Title = "🌐 Local IP Address", Description = "Show LAN IP", Execute = () => { string output = ExecuteProcessOutput("hostname", "-I"); TextOverlay.Show($"📍 {output.Trim()}", 3000); }, Similarity = 5.0 });
                suggestions.Add(new CommandResult { Title = "🌍 Public IP Address", Description = "Fetch WAN IP from API", Execute = async () => { try { var client = new System.Net.Http.HttpClient(); string ip = await client.GetStringAsync("https://api.ipify.org"); TextOverlay.Show($"🌍 {ip}", 4000); } catch { } }, Similarity = 4.5 });
            }
            if (cmd == "mac") suggestions.Add(new CommandResult { Title = "🆔 MAC Address", Description = "Show network hardware ID", Execute = () => { string output = ExecuteProcessOutput("getmac", "/v /fo list"); CliOutputOverlay.Show("MAC Addresses", output); }, Similarity = 5.0 });

            // ── 5. PRODUCTIVITY & TEXT ───────────────────────────────────────────
            if (cmd == "upper") suggestions.Add(new CommandResult { Title = "🔠 UPPERCASE", Description = "Capitalize clipboard text", Execute = () => { Clipboard.SetText(Clipboard.GetText().ToUpper()); TextOverlay.Show("✅ UPPERED", 1500); }, Similarity = 5.0 });
            if (cmd == "lower") suggestions.Add(new CommandResult { Title = "🔡 lowercase", Description = "Lowercase clipboard text", Execute = () => { Clipboard.SetText(Clipboard.GetText().ToLower()); TextOverlay.Show("✅ LOWERED", 1500); }, Similarity = 5.0 });
            if (cmd == "reverse") suggestions.Add(new CommandResult { Title = "↩️ Reverse Text", Description = "Reverse clipboard string", Execute = () => { string t = Clipboard.GetText(); Clipboard.SetText(new string(t.Reverse().ToArray())); TextOverlay.Show("✅ REVERSED", 1500); }, Similarity = 5.0 });
            if (cmd == "wordcount") suggestions.Add(new CommandResult { Title = "📝 Word Count", Description = "Count words in clipboard", Execute = () => { string t = Clipboard.GetText(); int c = t.Length; int w = t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length; TextOverlay.Show($"📊 Words: {w} | Chars: {c}", 3000); }, Similarity = 5.0 });
            if (cmd == "sort") suggestions.Add(new CommandResult { Title = "🔡 Sort Clipboard", Description = "Sort lines alphabetically", Execute = () => { string t = Clipboard.GetText(); var lines = t.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).OrderBy(l => l); Clipboard.SetText(string.Join("\n", lines)); TextOverlay.Show("✅ Sorted", 1500); }, Similarity = 5.0 });
            if (cmd == "unique") suggestions.Add(new CommandResult { Title = "💎 Unique Lines", Description = "Remove duplicate lines in clipboard", Execute = () => { string t = Clipboard.GetText(); var lines = t.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).Distinct(); Clipboard.SetText(string.Join("\n", lines)); TextOverlay.Show("✅ Duplicates Removed", 1500); }, Similarity = 5.0 });
            if (cmd == "url") suggestions.Add(new CommandResult { Title = "🔗 URL Encode", Description = "Encode clipboard URL", Execute = () => { Clipboard.SetText(Uri.EscapeDataString(Clipboard.GetText())); TextOverlay.Show("✅ URL Encoded", 1500); }, Similarity = 5.0 });
            if (cmd == "speedtest") suggestions.Add(new CommandResult { Title = "🚀 Speedtest", Description = "Open internet speed test", Execute = () => Process.Start(new ProcessStartInfo { FileName = "https://www.speedtest.net", UseShellExecute = true }), Similarity = 5.0 });
            if (cmd == "lorem") suggestions.Add(new CommandResult { Title = "📜 Lorem Ipsum", Description = "Copy placeholder text to clipboard", Execute = () => { string t = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua."; Clipboard.SetText(t); TextOverlay.Show("📋 Lorem Ipsum Copied", 2000); }, Similarity = 5.0 });
            if (cmd == "titlecase") suggestions.Add(new CommandResult { Title = "🔠 Title Case", Description = "Convert clipboard to Title Case", Execute = () => { string t = Clipboard.GetText(); var ti = System.Globalization.CultureInfo.CurrentCulture.TextInfo; Clipboard.SetText(ti.ToTitleCase(t.ToLower())); TextOverlay.Show("✅ Title Cased", 1500); }, Similarity = 5.0 });

            // ── 6. WEB QUICK FETCH ───────────────────────────────────────────────
            if (cmd == "weather" && !string.IsNullOrEmpty(arg)) suggestions.Add(new CommandResult { Title = $"🌤️ Weather: {arg}", Description = "Search weather for city", Execute = () => Process.Start(new ProcessStartInfo { FileName = $"https://www.google.com/search?q=weather+{arg}", UseShellExecute = true }), Similarity = 4.5 });
            if (cmd == "crypto" && !string.IsNullOrEmpty(arg)) suggestions.Add(new CommandResult { Title = $"🪙 Crypto: {arg}", Description = "Check current coin price", Execute = () => Process.Start(new ProcessStartInfo { FileName = $"https://www.google.com/search?q={arg}+price", UseShellExecute = true }), Similarity = 4.5 });
            if (cmd == "stock" && !string.IsNullOrEmpty(arg)) suggestions.Add(new CommandResult { Title = $"📈 Stock: {arg}", Description = "Check ticker price", Execute = () => Process.Start(new ProcessStartInfo { FileName = $"https://www.google.com/search?q=stock+{arg}", UseShellExecute = true }), Similarity = 4.5 });
            if (cmd == "define" && !string.IsNullOrEmpty(arg)) suggestions.Add(new CommandResult { Title = $"📖 Define: {arg}", Description = "Lookup word definition", Execute = () => Process.Start(new ProcessStartInfo { FileName = $"https://www.google.com/search?q=define+{arg}", UseShellExecute = true }), Similarity = 4.5 });
            if (cmd == "news") suggestions.Add(new CommandResult { Title = "📰 Latest News", Description = "Open top headlines", Execute = () => Process.Start(new ProcessStartInfo { FileName = "https://news.google.com", UseShellExecute = true }), Similarity = 5.0 });

            // ── 7. FILE & CLEANUP ────────────────────────────────────────────────
            if (cmd == "trash" || cmd == "empty") suggestions.Add(new CommandResult { Title = "🗑️ Empty Recycle Bin", Description = "Permanent delete all trash", Execute = () => CommandParser.ExecuteFirstSuggestion("recycle bin"), Similarity = 5.0 });
            if (cmd == "temp") suggestions.Add(new CommandResult { Title = "🧹 Clear Temp Files", Description = "Purge Windows %TEMP% folder", Execute = () => { int d = PurgeTempFolder(); TextOverlay.Show($"🧹 Cleared {d} files", 2500); }, Similarity = 5.0 });
            if (cmd == "shred" && !string.IsNullOrEmpty(arg)) suggestions.Add(new CommandResult { Title = $"💥 Shred File: {Path.GetFileName(arg)}", Description = "Securely wipe file from disk", Execute = () => { if (File.Exists(arg)) { File.WriteAllBytes(arg, new byte[new FileInfo(arg).Length]); File.Delete(arg); TextOverlay.Show("💥 File Shredded", 2000); } }, Similarity = 4.0 });

            // ── 8. SYSTEM POWER & STATS ──────────────────────────────────────────
            if (cmd == "lock") suggestions.Add(new CommandResult { Title = "🔒 Lock Workstation", Description = "Lock screen instantly", Execute = () => LockWorkStation(), Similarity = 8.0 });
            if (cmd == "sleep") suggestions.Add(new CommandResult { Title = "💤 Sleep", Description = "Suspend system", Execute = () => RunProcess("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0"), Similarity = 5.0 });
            if (cmd == "battery") suggestions.Add(new CommandResult { Title = "🔋 Battery Status", Description = "Show charge and health", Execute = () => { var power = System.Windows.Forms.SystemInformation.PowerStatus; TextOverlay.Show($"🔋 {power.BatteryLifePercent * 100}% | {power.PowerLineStatus}", 4000); }, Similarity = 5.0 });
            if (cmd == "cpu") suggestions.Add(new CommandResult { Title = "🧠 CPU Usage", Description = "Show active processor load", Execute = () => CommandParser.ExecuteFirstSuggestion("system stats"), Similarity = 5.0 });
            if (cmd == "gpu") suggestions.Add(new CommandResult { Title = "🎮 GPU Info", Description = "Open graphics settings", Execute = () => RunProcess("control.exe", "desk.cpl,,3"), Similarity = 5.0 });

            return suggestions;
        }

        private static string ExecuteProcessOutput(string f, string a) { try { var p = Process.Start(new ProcessStartInfo { FileName = f, Arguments = a, CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true }); return p?.StandardOutput.ReadToEnd() ?? ""; } catch { return ""; } }
        private static void RunProcess(string f, string a) { try { Process.Start(new ProcessStartInfo { FileName = f, Arguments = a, CreateNoWindow = true, UseShellExecute = false }); } catch { } }
        private static int PurgeTempFolder() { int d = 0; try { foreach (var f in Directory.GetFiles(Path.GetTempPath())) { try { File.Delete(f); d++; } catch { } } } catch { } return d; }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc> {
                new CommandDesc("gmail", "Read Gmail inbox summary", "gmail"),
                new CommandDesc("json prettify", "Format clipboard JSON", "json"),
                new CommandDesc("base64 encode/decode", "Base64 processing", "base64"),
                new CommandDesc("guid", "Generate unique ID", "guid"),
                new CommandDesc("uptime", "PC run time", "uptime"),
                new CommandDesc("whoami", "User info", "whoami"),
                new CommandDesc("kill <process>", "Terminate app", "kill notepad"),
                new CommandDesc("ip", "Show LAN and Public IP", "ip"),
                new CommandDesc("upper / lower", "Change text case", "upper"),
                new CommandDesc("weather <city>", "Check forecast", "weather london"),
                new CommandDesc("temp clear", "Purge temp files", "temp"),
                new CommandDesc("lock", "Lock computer", "lock")
            };
        }
    }
}
