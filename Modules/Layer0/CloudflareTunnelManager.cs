// Developer: heaplyn
// Date: 2026-08-10
// Summary: Self-healing Cloudflare Tunnel manager that downloads cloudflared.exe automatically, manages background HTTPS tunnels, and exposes Jarvis Mobile Web App to the public web with secure SSL encryption.

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public static class CloudflareTunnelManager
    {
        private static Process? _tunnelProcess;
        private static string? _publicUrl = null;
        public static string? PublicUrl => _publicUrl;
        public static bool IsRunning => _tunnelProcess != null && !_tunnelProcess.HasExited;

        public static async Task<string> StartTunnelAsync(int targetPort = 8080)
        {
            StopTunnel();

            // Ensure MobileBridgeServer is active before launching tunnel
            MobileBridgeServer.Start(targetPort);

            // Kill any old orphaned cloudflared background processes to avoid port/tunnel conflicts
            try
            {
                foreach (var oldProc in Process.GetProcessesByName("cloudflared"))
                {
                    try { oldProc.Kill(true); oldProc.Dispose(); } catch { }
                }
            }
            catch { }

            string toolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Tools");
            if (!Directory.Exists(toolsDir)) Directory.CreateDirectory(toolsDir);

            string exePath = Path.Combine(toolsDir, "cloudflared.exe");

            // 1. Download cloudflared.exe if missing
            if (!File.Exists(exePath))
            {
                TextOverlay.Show("⚡ Downloading Cloudflare Tunnel engine...", 3000);
                try
                {
                    string downloadUrl = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe";
                    using var client = new HttpClient();
                    byte[] data = await client.GetByteArrayAsync(downloadUrl);
                    await File.WriteAllBytesAsync(exePath, data);
                    TextOverlay.Show("✅ Cloudflare Tunnel engine ready!", 2000);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed downloading cloudflared.exe: {ex.Message}");
                }
            }

            string tokenPath = Path.Combine(toolsDir, "cloudflare_token.txt");
            string domainPath = Path.Combine(toolsDir, "cloudflare_domain.txt");

            string arguments = $"tunnel --url http://localhost:{targetPort}";

            if (File.Exists(tokenPath))
            {
                string savedToken = File.ReadAllText(tokenPath).Trim();
                if (!string.IsNullOrEmpty(savedToken))
                {
                    arguments = $"tunnel run --token {savedToken}";
                    if (File.Exists(domainPath))
                    {
                        _publicUrl = File.ReadAllText(domainPath).Trim();
                    }
                }
            }

            // 2. Start Cloudflare Tunnel process with persistent args
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _tunnelProcess = new Process { StartInfo = psi };
            var tcs = new TaskCompletionSource<string>();

            _tunnelProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    ChatOverlay.LogConsoleAction("Cloudflare Log", e.Data);

                    // Look for https://....trycloudflare.com
                    var match = Regex.Match(e.Data, @"https://[a-zA-Z0-9\-]+\.trycloudflare\.com");
                    if (match.Success)
                    {
                        string foundUrl = match.Value;
                        Task.Run(async () =>
                        {
                            // Verify Cloudflare DNS & HTTP endpoint is online
                            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                            for (int i = 0; i < 8; i++)
                            {
                                try
                                {
                                    var resp = await client.GetAsync(foundUrl);
                                    if (resp.IsSuccessStatusCode || (int)resp.StatusCode < 500)
                                    {
                                        _publicUrl = foundUrl;
                                        tcs.TrySetResult(_publicUrl);
                                        return;
                                    }
                                }
                                catch { }
                                await Task.Delay(1000);
                            }
                            _publicUrl = foundUrl;
                            tcs.TrySetResult(_publicUrl);
                        });
                    }
                }
            };

            _tunnelProcess.Start();
            _tunnelProcess.BeginErrorReadLine();

            // Wait up to 18 seconds for Cloudflare to assign and propagate public HTTPS URL
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(18000));
            if (completedTask == tcs.Task)
            {
                _publicUrl = await tcs.Task;
                TextOverlay.Show($"🌐 Cloudflare Web Host Live:\n{_publicUrl}", 5000);
                return _publicUrl;
            }
            else
            {
                if (!string.IsNullOrEmpty(_publicUrl)) return _publicUrl;
                throw new Exception("Timed out waiting for Cloudflare Tunnel public URL.");
            }
        }

        public static void SaveTunnelToken(string token, string? domainName = null)
        {
            try
            {
                string toolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Tools");
                if (!Directory.Exists(toolsDir)) Directory.CreateDirectory(toolsDir);

                File.WriteAllText(Path.Combine(toolsDir, "cloudflare_token.txt"), token.Trim());
                if (!string.IsNullOrEmpty(domainName))
                {
                    File.WriteAllText(Path.Combine(toolsDir, "cloudflare_domain.txt"), domainName.Trim());
                    _publicUrl = domainName.Trim();
                }
            }
            catch { }
        }

        public static void StopTunnel()
        {
            try
            {
                if (_tunnelProcess != null && !_tunnelProcess.HasExited)
                {
                    _tunnelProcess.Kill(true);
                    _tunnelProcess.Dispose();
                    _tunnelProcess = null;
                }
                _publicUrl = null;
            }
            catch { }
        }
    }
}
