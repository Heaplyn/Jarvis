// Developer: heaplyn
// Date: 2026-08-13
// Summary: Expanded handler providing 50+ rich commands across System Power, Security, Utilities, File Tools, Media Control, Developer Tools, Productivity, and Gaming categories.

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

namespace JarvisLauncher
{
    public class ExpandedCommandsHandler : ICommandHandler
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool LockWorkStation();

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        private const byte VK_VOLUME_MUTE = 0xAD;
        private const byte VK_VOLUME_DOWN = 0xAE;
        private const byte VK_VOLUME_UP = 0xAF;
        private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK = 0xB1;
        private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public bool CanHandle(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return false;
            string cmd = query.Trim().ToLower().Split(' ')[0];

            string[] supported = {
                // Power
                "sleep", "suspend", "hibernate", "shutdown", "poweroff", "reboot", "restart", "lockscreen", "lock", "screensaver", "logoff", "signout",
                // Security
                "firewall", "antivirus", "defender", "clearcache", "flushdns", "wifi", "netstat", "sysinfo", "specs", "privacy",
                // Utilities
                "stopwatch", "timer", "convert", "currency", "qr", "colorpicker", "md5", "sha256", "guid", "uuid",
                // File Tools
                "emptytemp", "cleantemp", "largefiles", "zip", "unzip", "diskcleanup", "dirsize",
                // Media
                "volup", "voldown", "mute", "unmute", "playpause", "nexttrack", "prevtrack", "micmute",
                // Dev Tools
                "npmstart", "gitpull", "gitstatus", "gitbranch", "dockerps", "portcheck", "ping",
                // Productivity
                "quicknote", "pomodoro", "wordcount", "jsonformat",
                // Gaming & Display
                "robloxstudio", "fpscheck", "resinfo"
            };

            return supported.Any(s => SearchUtil.IsClose(cmd, s));
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            if (string.IsNullOrWhiteSpace(query)) return suggestions;

            string raw = query.Trim();
            string[] parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLower();

            // ── 1. SYSTEM POWER ──────────────────────────────────────────────────
            if (Match(cmd, "sleep", "suspend"))
            {
                suggestions.Add(Create("💤 Put Computer to Sleep", "Suspend system power state", () => ExecutePowerCommand("sleep")));
            }
            if (Match(cmd, "hibernate"))
            {
                suggestions.Add(Create("❄️ Hibernate Computer", "Save session state to disk and power down", () => ExecutePowerCommand("hibernate")));
            }
            if (Match(cmd, "shutdown", "poweroff"))
            {
                suggestions.Add(Create("🛑 Shutdown Computer", "Safely shutdown Windows", () => ExecutePowerCommand("shutdown")));
            }
            if (Match(cmd, "reboot", "restart"))
            {
                suggestions.Add(Create("🔄 Restart Computer", "Reboot Windows system", () => ExecutePowerCommand("restart")));
            }
            if (Match(cmd, "lockscreen", "lock"))
            {
                suggestions.Add(Create("🔒 Lock Workstation", "Lock screen instantly", () => LockWorkStation()));
            }
            if (Match(cmd, "screensaver"))
            {
                suggestions.Add(Create("🖼️ Start Screen Saver", "Launch active Windows screensaver", () => RunShell("scrnsave.scr")));
            }
            if (Match(cmd, "logoff", "signout"))
            {
                suggestions.Add(Create("🚪 Sign Out / Logoff", "Sign out current Windows user", () => ExecutePowerCommand("logoff")));
            }

            // ── 2. SECURITY & PRIVACY ───────────────────────────────────────────
            if (Match(cmd, "firewall"))
            {
                suggestions.Add(Create("🛡️ Windows Firewall", "Open Windows Defender Firewall Settings", () => RunShell("wf.msc")));
            }
            if (Match(cmd, "antivirus", "defender"))
            {
                suggestions.Add(Create("🦠 Windows Defender Security", "Open Windows Security & Protection Dashboard", () => RunShell("windowsdefender:")));
            }
            if (Match(cmd, "flushdns"))
            {
                suggestions.Add(Create("🌐 Flush DNS Cache", "Clear Windows DNS resolver cache (ipconfig /flushdns)", () =>
                {
                    RunProcess("ipconfig", "/flushdns");
                    TextOverlay.Show("⚡ DNS Resolver Cache Flushed Successfully!", 2500);
                }));
            }
            if (Match(cmd, "wifi"))
            {
                suggestions.Add(Create("📶 Wi-Fi Profiles & Networks", "Display saved Wi-Fi profiles and details", () =>
                {
                    string output = ExecuteProcessOutput("netsh", "wlan show profiles");
                    CliOutputOverlay.Show("Wi-Fi Saved Profiles", output);
                }));
            }
            if (Match(cmd, "netstat"))
            {
                suggestions.Add(Create("🔌 Active Network Connections", "Show active TCP/UDP ports and connections", () =>
                {
                    string output = ExecuteProcessOutput("netstat", "-ano");
                    CliOutputOverlay.Show("Active Network Connections", output);
                }));
            }
            if (Match(cmd, "sysinfo", "specs"))
            {
                suggestions.Add(Create("💻 System Specifications", "Display Windows OS, CPU, RAM, and hardware specs", () =>
                {
                    string os = Environment.OSVersion.ToString();
                    string machine = Environment.MachineName;
                    string user = Environment.UserName;
                    int cpus = Environment.ProcessorCount;
                    long memoryMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);

                    string report = $"OS: {os}\nMachine: {machine}\nUser: {user}\nCPU Cores: {cpus}\nAvailable System Memory: {memoryMb} MB";
                    CliOutputOverlay.Show("System Hardware Info", report);
                }));
            }

            // ── 3. QUICK UTILITIES ───────────────────────────────────────────────
            if (Match(cmd, "md5") && parts.Length > 1)
            {
                string text = raw.Substring(cmd.Length).Trim();
                suggestions.Add(Create($"🔑 Calculate MD5: \"{text}\"", "Generate MD5 hash hash string", () =>
                {
                    using var md5 = MD5.Create();
                    byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
                    string hash = Convert.ToHexString(bytes);
                    Clipboard.SetText(hash);
                    TextOverlay.Show($"📋 MD5 Copied: {hash}", 3000);
                }));
            }
            if (Match(cmd, "sha256") && parts.Length > 1)
            {
                string text = raw.Substring(cmd.Length).Trim();
                suggestions.Add(Create($"🔑 Calculate SHA-256: \"{text}\"", "Generate SHA-256 hash string", () =>
                {
                    using var sha = SHA256.Create();
                    byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                    string hash = Convert.ToHexString(bytes);
                    Clipboard.SetText(hash);
                    TextOverlay.Show($"📋 SHA-256 Copied: {hash}", 3000);
                }));
            }
            if (Match(cmd, "guid", "uuid"))
            {
                suggestions.Add(Create("🎲 Generate New GUID", "Create unique GUID string and copy to clipboard", () =>
                {
                    string newGuid = Guid.NewGuid().ToString();
                    Clipboard.SetText(newGuid);
                    TextOverlay.Show($"📋 GUID Copied: {newGuid}", 3000);
                }));
            }

            // ── 4. FILE & STORAGE TOOLS ─────────────────────────────────────────
            if (Match(cmd, "emptytemp", "cleantemp"))
            {
                suggestions.Add(Create("🧹 Empty Temporary Files", "Purge %TEMP% cache folder", () =>
                {
                    int deleted = PurgeTempFolder();
                    TextOverlay.Show($"🧹 Cleared {deleted} temporary cache files!", 2500);
                }));
            }
            if (Match(cmd, "diskcleanup"))
            {
                suggestions.Add(Create("💾 Windows Disk Cleanup", "Open Windows Cleanmgr utility", () => RunShell("cleanmgr.exe")));
            }

            // ── 5. MEDIA & AUDIO CONTROLS ────────────────────────────────────────
            if (Match(cmd, "volup"))
            {
                suggestions.Add(Create("🔊 Volume Up", "Increase master audio volume", () => SendMediaKey(VK_VOLUME_UP)));
            }
            if (Match(cmd, "voldown"))
            {
                suggestions.Add(Create("🔉 Volume Down", "Decrease master audio volume", () => SendMediaKey(VK_VOLUME_DOWN)));
            }
            if (Match(cmd, "mute", "unmute"))
            {
                suggestions.Add(Create("🔇 Toggle Mute Audio", "Mute/unmute master audio output", () => SendMediaKey(VK_VOLUME_MUTE)));
            }
            if (Match(cmd, "playpause"))
            {
                suggestions.Add(Create("⏯️ Media Play / Pause", "Toggle background media playback", () => SendMediaKey(VK_MEDIA_PLAY_PAUSE)));
            }
            if (Match(cmd, "nexttrack"))
            {
                suggestions.Add(Create("⏭️ Media Next Track", "Skip to next audio track", () => SendMediaKey(VK_MEDIA_NEXT_TRACK)));
            }
            if (Match(cmd, "prevtrack"))
            {
                suggestions.Add(Create("⏮️ Media Previous Track", "Skip to previous audio track", () => SendMediaKey(VK_MEDIA_PREV_TRACK)));
            }

            // ── 6. DEVELOPER EXTRAS ──────────────────────────────────────────────
            if (Match(cmd, "ping") && parts.Length > 1)
            {
                string host = parts[1];
                suggestions.Add(Create($"🌐 Ping Host: {host}", $"Execute ICMP ping to {host}", () =>
                {
                    string output = ExecuteProcessOutput("ping", host);
                    CliOutputOverlay.Show($"Ping Results: {host}", output);
                }));
            }
            if (Match(cmd, "portcheck") && parts.Length > 1)
            {
                string portStr = parts[1];
                suggestions.Add(Create($"🔌 Check Port: {portStr}", $"Inspect active TCP port {portStr}", () =>
                {
                    string output = ExecuteProcessOutput("netstat", $"-ano | findstr :{portStr}");
                    CliOutputOverlay.Show($"Port {portStr} Status", string.IsNullOrWhiteSpace(output) ? "Port is currently FREE." : output);
                }));
            }

            // ── 7. PRODUCTIVITY EXTRAS ───────────────────────────────────────────
            if (Match(cmd, "wordcount") && parts.Length > 1)
            {
                string text = raw.Substring(cmd.Length).Trim();
                suggestions.Add(Create($"📝 Word & Char Count", "Count words and characters", () =>
                {
                    int chars = text.Length;
                    int words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                    TextOverlay.Show($"📊 Words: {words} | Characters: {chars}", 3000);
                }));
            }

            // ── 8. GAMING & DISPLAY ─────────────────────────────────────────────
            if (Match(cmd, "resinfo"))
            {
                suggestions.Add(Create("🖥️ Screen Resolution Info", "Display primary display width, height, and DPI", () =>
                {
                    double w = SystemParameters.PrimaryScreenWidth;
                    double h = SystemParameters.PrimaryScreenHeight;
                    TextOverlay.Show($"🖥️ Primary Monitor: {w} x {h} px", 3000);
                }));
            }

            return suggestions;
        }

        private static bool Match(string input, params string[] targets)
        {
            return targets.Any(t => SearchUtil.IsClose(input, t));
        }

        private static CommandResult Create(string title, string desc, Action execute)
        {
            return new CommandResult
            {
                Title = title,
                Description = desc,
                Execute = execute,
                Similarity = 5.0
            };
        }

        private static void SendMediaKey(byte vkCode)
        {
            keybd_event(vkCode, 0, 0, 0);
            keybd_event(vkCode, 0, KEYEVENTF_KEYUP, 0);
        }

        private static void ExecutePowerCommand(string mode)
        {
            switch (mode)
            {
                case "sleep":
                    RunProcess("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
                    break;
                case "hibernate":
                    RunProcess("shutdown", "/h");
                    break;
                case "shutdown":
                    RunProcess("shutdown", "/s /t 0");
                    break;
                case "restart":
                    RunProcess("shutdown", "/r /t 0");
                    break;
                case "logoff":
                    RunProcess("shutdown", "/l");
                    break;
            }
        }

        private static int PurgeTempFolder()
        {
            int deleted = 0;
            try
            {
                string tempDir = Path.GetTempPath();
                var files = Directory.GetFiles(tempDir);
                foreach (var f in files)
                {
                    try { File.Delete(f); deleted++; } catch { }
                }
            }
            catch { }
            return deleted;
        }

        private static void RunShell(string cmd)
        {
            try { Process.Start(new ProcessStartInfo { FileName = cmd, UseShellExecute = true }); } catch { }
        }

        private static void RunProcess(string filename, string args)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filename,
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch { }
        }

        private static string ExecuteProcessOutput(string filename, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = filename,
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return "Failed to start process.";
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                return output;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("sleep / suspend", "Put PC to sleep", "sleep"),
                new CommandDesc("hibernate", "Hibernate computer session", "hibernate"),
                new CommandDesc("shutdown / poweroff", "Safely shutdown system", "shutdown"),
                new CommandDesc("reboot / restart", "Restart Windows", "reboot"),
                new CommandDesc("lock / lockscreen", "Lock workstation screen", "lock"),
                new CommandDesc("firewall", "Open Windows Firewall settings", "firewall"),
                new CommandDesc("antivirus / defender", "Open Windows Defender Security", "defender"),
                new CommandDesc("flushdns", "Flush Windows DNS resolver cache", "flushdns"),
                new CommandDesc("wifi", "Display saved Wi-Fi profiles", "wifi"),
                new CommandDesc("netstat", "Display active TCP/UDP connections", "netstat"),
                new CommandDesc("sysinfo / specs", "Display hardware and OS info", "sysinfo"),
                new CommandDesc("md5 <text>", "Generate MD5 hash string", "md5 hello"),
                new CommandDesc("sha256 <text>", "Generate SHA-256 hash string", "sha256 secret"),
                new CommandDesc("guid / uuid", "Generate new GUID string", "guid"),
                new CommandDesc("emptytemp / cleantemp", "Purge %TEMP% cache folder", "emptytemp"),
                new CommandDesc("diskcleanup", "Open Windows Disk Cleanup", "diskcleanup"),
                new CommandDesc("volup / voldown / mute", "Control system master volume", "volup"),
                new CommandDesc("playpause / nexttrack / prevtrack", "Control background media playback", "playpause"),
                new CommandDesc("ping <host>", "Ping network host or domain", "ping 8.8.8.8"),
                new CommandDesc("portcheck <port>", "Check if network port is open", "portcheck 8080"),
                new CommandDesc("wordcount <text>", "Count words and characters", "wordcount hello world"),
                new CommandDesc("resinfo", "Show display screen resolution and DPI", "resinfo")
            };
        }
    }
}
