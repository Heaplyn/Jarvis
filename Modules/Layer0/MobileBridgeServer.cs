// Developer: heaplyn
// Date: 2026-08-10
// Summary: High-performance HTTP & REST API Mobile Bridge Server embedding a full-featured glassmorphic PWA mobile web app featuring smooth live PC screen streaming, expanded remote control deck, real-time command suggestions, CLI terminal, and system telemetry.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace JarvisLauncher
{
    public static class MobileBridgeServer
    {
        private static HttpListener? _listener;
        private static bool _isRunning;
        private static string _localIp = "127.0.0.1";
        private static int _port = 8080;

        public static string ServerUrl => $"http://{_localIp}:{_port}";
        public static string HostnameDomain => $"http://{Environment.MachineName.ToLower()}.local:{_port}";
        public static string JarvisDomain => $"http://jarvis.local:{_port}";

        public static void Start(int port = 8080)
        {
            if (_isRunning) return;

            _port = port;
            _localIp = GetLocalIPAddress();
            EnsureUrlAclPermission(_port);
            EnsureFirewallRule(_port);

            try
            {
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{_port}/");
                listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                try { listener.Prefixes.Add($"http://[::1]:{_port}/"); } catch { }
                if (!string.IsNullOrEmpty(_localIp) && _localIp != "127.0.0.1")
                {
                    try { listener.Prefixes.Add($"http://{_localIp}:{_port}/"); } catch { }
                }
                listener.Start();
                _listener = listener;
                _isRunning = true;
                ChatOverlay.LogConsoleAction("Mobile Server Active", $"Listening on localhost, 127.0.0.1, [::1], {_localIp}");
            }
            catch (Exception ex)
            {
                ChatOverlay.LogConsoleAction("Mobile Server Multi-Prefix Failed, Falling Back", ex.Message);
                try
                {
                    var listener = new HttpListener();
                    listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                    if (!string.IsNullOrEmpty(_localIp) && _localIp != "127.0.0.1")
                    {
                        listener.Prefixes.Add($"http://{_localIp}:{_port}/");
                    }
                    listener.Start();
                    _listener = listener;
                    _isRunning = true;
                }
                catch (Exception fallbackEx)
                {
                    ChatOverlay.LogConsoleAction("Mobile Server Critical Start Failure", fallbackEx.Message);
                    return;
                }
            }

            Task.Run(async () =>
            {
                while (_isRunning && _listener != null && _listener.IsListening)
                {
                    try
                    {
                        var ctx = await _listener.GetContextAsync();
                        _ = Task.Run(() => ProcessRequestAsync(ctx));
                    }
                    catch { }
                }
            });

            TextOverlay.Show($"📱 Mobile Server Active:\n{ServerUrl}", 4500);
        }

        private static void EnsureUrlAclPermission(int port)
        {
            Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = $"http add urlacl url=http://*:{port}/ user=Everyone",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(2000);
                }
                catch { }
            });
        }

        private static void EnsureFirewallRule(int port)
        {
            Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = $"advfirewall firewall add rule name=\"Jarvis Mobile Port {port}\" dir=in action=allow protocol=TCP localport={port}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(psi);
                }
                catch { }
            });
        }

        public static void Stop()
        {
            _isRunning = false;
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch { }
        }

        public static string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        private static async Task ProcessRequestAsync(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var resp = ctx.Response;

            resp.Headers.Add("Access-Control-Allow-Origin", "*");
            resp.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            resp.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (req.HttpMethod == "OPTIONS")
            {
                resp.StatusCode = 204;
                resp.Close();
                return;
            }

            string path = req.Url?.AbsolutePath ?? "/";

            try
            {
                if (path == "/" || path == "/index.html")
                {
                    string html = GetMobileAppHtml();
                    byte[] buf = Encoding.UTF8.GetBytes(html);
                    resp.ContentType = "text/html; charset=utf-8";
                    resp.ContentLength64 = buf.Length;
                    await resp.OutputStream.WriteAsync(buf);
                }
                else if (path == "/api/suggestions" && req.HttpMethod == "GET")
                {
                    string query = req.QueryString["q"] ?? "";
                    var list = new List<object>();

                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                var suggestions = CommandParser.GetSuggestions(query);
                                foreach (var s in suggestions.Take(6))
                                {
                                    list.Add(new { title = s.Title, desc = s.Description });
                                }
                            }
                            catch { }
                        });
                    }

                    string json = JsonSerializer.Serialize(list);
                    byte[] buf = Encoding.UTF8.GetBytes(json);
                    resp.ContentType = "application/json";
                    resp.ContentLength64 = buf.Length;
                    await resp.OutputStream.WriteAsync(buf);
                }
                else if (path == "/api/stats")
                {
                    if (!MobileOverlay.AllowTelemetry)
                    {
                        string jsonErr = JsonSerializer.Serialize(new { status = "disabled", message = "Telemetry disabled in Mobile Overlay Settings" });
                        byte[] errBuf = Encoding.UTF8.GetBytes(jsonErr);
                        resp.ContentType = "application/json";
                        await resp.OutputStream.WriteAsync(errBuf);
                        return;
                    }
                    var stats = GetSystemStats();
                    string json = JsonSerializer.Serialize(stats);
                    byte[] buf = Encoding.UTF8.GetBytes(json);
                    resp.ContentType = "application/json";
                    resp.ContentLength64 = buf.Length;
                    await resp.OutputStream.WriteAsync(buf);
                }
                else if (path == "/api/chat" && req.HttpMethod == "POST")
                {
                    if (!MobileOverlay.AllowAiChat)
                    {
                        string jsonErr = JsonSerializer.Serialize(new { response = "⚠️ Mobile AI Chat is currently disabled in desktop Mobile Overlay Settings." });
                        byte[] errBuf = Encoding.UTF8.GetBytes(jsonErr);
                        resp.ContentType = "application/json";
                        await resp.OutputStream.WriteAsync(errBuf);
                        return;
                    }

                    using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                    string body = await reader.ReadToEndAsync();
                    using var doc = JsonDocument.Parse(body);
                    string prompt = doc.RootElement.TryGetProperty("prompt", out var pProp) ? pProp.GetString() ?? "" : "";

                    string aiRaw = await AiAPI.AskGemini(prompt);
                    string finalResult = AgentExecutor.ProcessAIResponse(aiRaw);

                    var outObj = new { response = finalResult };
                    string json = JsonSerializer.Serialize(outObj);
                    byte[] buf = Encoding.UTF8.GetBytes(json);
                    resp.ContentType = "application/json";
                    resp.ContentLength64 = buf.Length;
                    await resp.OutputStream.WriteAsync(buf);
                }
                else if (path == "/api/command" && req.HttpMethod == "POST")
                {
                    using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                    string body = await reader.ReadToEndAsync();
                    using var doc = JsonDocument.Parse(body);
                    string command = doc.RootElement.TryGetProperty("command", out var cProp) ? cProp.GetString() ?? "" : "";

                    string cmdLower = command.ToLower();
                    if (cmdLower.StartsWith("app ") && !MobileOverlay.AllowAppLaunching)
                    {
                        string jsonErr = JsonSerializer.Serialize(new { status = "disabled", message = "App launching disabled in Mobile Overlay Settings" });
                        byte[] errBuf = Encoding.UTF8.GetBytes(jsonErr);
                        resp.ContentType = "application/json";
                        await resp.OutputStream.WriteAsync(errBuf);
                        return;
                    }
                    if (cmdLower.StartsWith("vol ") && !MobileOverlay.AllowVolumeControl)
                    {
                        string jsonErr = JsonSerializer.Serialize(new { status = "disabled", message = "Volume control disabled in Mobile Overlay Settings" });
                        byte[] errBuf = Encoding.UTF8.GetBytes(jsonErr);
                        resp.ContentType = "application/json";
                        await resp.OutputStream.WriteAsync(errBuf);
                        return;
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var suggestions = CommandParser.GetSuggestions(command);
                        if (suggestions != null && suggestions.Count > 0 && suggestions[0].Execute != null)
                        {
                            suggestions[0].Execute?.Invoke();
                        }
                    });

                    var outObj = new { status = "success", message = $"Executed: {command}" };
                    string json = JsonSerializer.Serialize(outObj);
                    byte[] buf = Encoding.UTF8.GetBytes(json);
                    resp.ContentType = "application/json";
                    resp.ContentLength64 = buf.Length;
                    await resp.OutputStream.WriteAsync(buf);
                }
                else if (path == "/api/terminal" && req.HttpMethod == "POST")
                {
                    if (!MobileOverlay.AllowTerminal)
                    {
                        string jsonErr = JsonSerializer.Serialize(new { command = "", output = "⚠️ Remote PowerShell Terminal is currently disabled in desktop Mobile Overlay settings." });
                        byte[] errBuf = Encoding.UTF8.GetBytes(jsonErr);
                        resp.ContentType = "application/json";
                        await resp.OutputStream.WriteAsync(errBuf);
                        return;
                    }

                    using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                    string body = await reader.ReadToEndAsync();
                    using var doc = JsonDocument.Parse(body);
                    string command = doc.RootElement.TryGetProperty("command", out var cProp) ? cProp.GetString() ?? "" : "";

                    string output = ExecutePowerShellCommand(command);
                    var outObj = new { command = command, output = output };
                    string json = JsonSerializer.Serialize(outObj);
                    byte[] buf = Encoding.UTF8.GetBytes(json);
                    resp.ContentType = "application/json";
                    resp.ContentLength64 = buf.Length;
                    await resp.OutputStream.WriteAsync(buf);
                }
                else if (path == "/api/screenshot" && req.HttpMethod == "GET")
                {
                    if (!MobileOverlay.AllowScreenMirroring)
                    {
                        byte[] errBytes = Encoding.UTF8.GetBytes("Desktop Screen Mirroring disabled in Mobile Overlay settings.");
                        resp.ContentType = "text/plain";
                        await resp.OutputStream.WriteAsync(errBytes);
                        return;
                    }

                    byte[] imageBytes = CaptureScreenJpeg();
                    resp.ContentType = "image/jpeg";
                    resp.ContentLength64 = imageBytes.Length;
                    await resp.OutputStream.WriteAsync(imageBytes);
                }
                else if (path == "/api/clipboard")
                {
                    if (!MobileOverlay.AllowClipboardSync)
                    {
                        string jsonErr = JsonSerializer.Serialize(new { status = "disabled", message = "Clipboard sync disabled in Mobile Overlay settings" });
                        byte[] errBuf = Encoding.UTF8.GetBytes(jsonErr);
                        resp.ContentType = "application/json";
                        await resp.OutputStream.WriteAsync(errBuf);
                        return;
                    }

                    if (req.HttpMethod == "GET")
                    {
                        string clipText = "";
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            try { clipText = Clipboard.GetText(); } catch { }
                        });
                        string json = JsonSerializer.Serialize(new { text = clipText });
                        byte[] buf = Encoding.UTF8.GetBytes(json);
                        resp.ContentType = "application/json";
                        await resp.OutputStream.WriteAsync(buf);
                    }
                    else if (req.HttpMethod == "POST")
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                        string body = await reader.ReadToEndAsync();
                        using var doc = JsonDocument.Parse(body);
                        string text = doc.RootElement.TryGetProperty("text", out var tProp) ? tProp.GetString() ?? "" : "";

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            try { Clipboard.SetText(text); TextOverlay.Show($"📋 Clipboard updated from Phone!", 2000); } catch { }
                        });

                        string json = JsonSerializer.Serialize(new { status = "success" });
                        byte[] buf = Encoding.UTF8.GetBytes(json);
                        resp.ContentType = "application/json";
                        await resp.OutputStream.WriteAsync(buf);
                    }
                }
                else
                {
                    resp.StatusCode = 404;
                    byte[] buf = Encoding.UTF8.GetBytes("Not Found");
                    await resp.OutputStream.WriteAsync(buf);
                }
            }
            catch (Exception ex)
            {
                resp.StatusCode = 500;
                byte[] buf = Encoding.UTF8.GetBytes($"Error: {ex.Message}");
                await resp.OutputStream.WriteAsync(buf);
            }
            finally
            {
                resp.Close();
            }
        }

        private static string ExecutePowerShellCommand(string command)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc == null) return "Failed starting PowerShell process.";

                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit(8000);

                string result = (output + "\n" + error).Trim();
                return string.IsNullOrWhiteSpace(result) ? "(Command executed cleanly with no output)" : result;
            }
            catch (Exception ex)
            {
                return $"Error executing command: {ex.Message}";
            }
        }

        private static byte[] CaptureScreenJpeg()
        {
            try
            {
                byte[] bytes = Array.Empty<byte>();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        int width = (int)SystemParameters.PrimaryScreenWidth;
                        int height = (int)SystemParameters.PrimaryScreenHeight;
                        if (width <= 0) width = 1920;
                        if (height <= 0) height = 1080;

                        using var bmp = new System.Drawing.Bitmap(width, height);
                        using var g = System.Drawing.Graphics.FromImage(bmp);
                        g.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(width, height), System.Drawing.CopyPixelOperation.SourceCopy);

                        int targetW = Math.Min(1280, width);
                        int targetH = (int)(height * ((double)targetW / width));

                        using var resized = new System.Drawing.Bitmap(bmp, new System.Drawing.Size(targetW, targetH));
                        using var ms = new MemoryStream();

                        var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                        if (jpegEncoder != null)
                        {
                            var ep = new System.Drawing.Imaging.EncoderParameters(1);
                            ep.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 75L);
                            resized.Save(ms, jpegEncoder, ep);
                        }
                        else
                        {
                            resized.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                        }
                        bytes = ms.ToArray();
                    }
                    catch { }
                });
                return bytes.Length > 0 ? bytes : Encoding.UTF8.GetBytes("Error capturing screen");
            }
            catch (Exception ex)
            {
                return Encoding.UTF8.GetBytes($"Error capturing screenshot: {ex.Message}");
            }
        }

        private static object GetSystemStats()
        {
            ulong totalRamMb = 0;
            ulong freeRamMb = 0;
            uint memoryLoad = 0;

            try
            {
                var memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    totalRamMb = memStatus.ullTotalPhys / (1024 * 1024);
                    freeRamMb = memStatus.ullAvailPhys / (1024 * 1024);
                    memoryLoad = memStatus.dwMemoryLoad;
                }
            }
            catch { }

            string activeWindow = "Desktop";
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                var sb = new StringBuilder(256);
                GetWindowText(hwnd, sb, 256);
                activeWindow = sb.ToString();
            }
            catch { }

            return new
            {
                computerName = Environment.MachineName,
                userName = Environment.UserName,
                memoryLoad = memoryLoad,
                totalRamMb = totalRamMb,
                freeRamMb = freeRamMb,
                usedRamMb = totalRamMb - freeRamMb,
                activeWindow = string.IsNullOrWhiteSpace(activeWindow) ? "Desktop" : activeWindow,
                serverUrl = ServerUrl,
                jarvisDomain = JarvisDomain,
                localIp = _localIp,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private static string GetMobileAppHtml()
        {
            return """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no">
    <title>JARVIS Mobile Companion</title>
    <meta name="theme-color" content="#0f172a">
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link href="https://fonts.googleapis.com/css2?family=Fira+Code:wght@400;600&family=Outfit:wght@300;400;600;700&display=swap" rel="stylesheet">
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; font-family: 'Outfit', sans-serif; -webkit-tap-highlight-color: transparent; }
        body { background-color: #0b0f19; color: #f8fafc; height: 100vh; display: flex; flex-direction: column; overflow: hidden; }
        header { background: rgba(15, 23, 42, 0.85); backdrop-filter: blur(12px); border-bottom: 1px solid rgba(255,255,255,0.08); padding: 12px 16px; display: flex; justify-content: space-between; align-items: center; }
        .logo { font-size: 1.1rem; font-weight: 700; background: linear-gradient(135deg, #60a5fa, #c084fc); -webkit-background-clip: text; -webkit-text-fill-color: transparent; display: flex; align-items: center; gap: 8px; }
        .badge { background: rgba(59, 130, 246, 0.2); border: 1px solid rgba(59, 130, 246, 0.4); color: #60a5fa; font-size: 0.7rem; padding: 3px 8px; border-radius: 20px; font-weight: 600; }
        .tabs { display: flex; background: rgba(15, 23, 42, 0.8); border-bottom: 1px solid rgba(255,255,255,0.08); overflow-x: auto; }
        .tab-btn { flex: 1; min-width: 70px; padding: 10px 6px; text-align: center; font-size: 0.75rem; font-weight: 600; color: #94a3b8; border: none; background: none; cursor: pointer; border-bottom: 2px solid transparent; transition: all 0.2s; white-space: nowrap; }
        .tab-btn.active { color: #38bdf8; border-bottom-color: #38bdf8; background: rgba(56, 189, 248, 0.08); }
        .content-area { flex: 1; overflow-y: auto; display: flex; flex-direction: column; position: relative; }
        .view { display: none; flex: 1; flex-direction: column; }
        .view.active { display: flex; }
        
        /* Popover Autocomplete */
        .popover { background: rgba(15, 23, 42, 0.95); border: 1px solid rgba(56, 189, 248, 0.35); border-radius: 12px; margin: 0 14px 6px 14px; max-height: 160px; overflow-y: auto; padding: 6px; display: flex; flex-direction: column; gap: 4px; backdrop-filter: blur(12px); box-shadow: 0 8px 24px rgba(0,0,0,0.5); }
        .pop-item { padding: 8px 12px; border-radius: 8px; font-size: 0.8rem; cursor: pointer; color: #f1f5f9; background: rgba(30, 41, 59, 0.6); transition: background 0.15s; }
        .pop-item:active { background: rgba(56, 189, 248, 0.25); color: #38bdf8; }
        .pop-title { font-weight: 600; color: #38bdf8; }
        .pop-desc { font-size: 0.7rem; color: #94a3b8; margin-top: 2px; }

        /* Chat View */
        #chat-history { flex: 1; overflow-y: auto; padding: 14px; display: flex; flex-direction: column; gap: 10px; }
        .msg { max-width: 88%; padding: 12px 14px; border-radius: 14px; font-size: 0.88rem; line-height: 1.45; word-break: break-word; white-space: pre-wrap; }
        .msg.user { align-self: flex-end; background: linear-gradient(135deg, #3b82f6, #1d4ed8); color: #fff; border-bottom-right-radius: 2px; }
        .msg.ai { align-self: flex-start; background: rgba(30, 41, 59, 0.85); border: 1px solid rgba(255,255,255,0.08); color: #e2e8f0; border-bottom-left-radius: 2px; }
        .input-bar { padding: 10px 14px; background: rgba(15, 23, 42, 0.95); border-top: 1px solid rgba(255,255,255,0.08); display: flex; gap: 8px; align-items: center; }
        .input-bar input { flex: 1; background: rgba(30, 41, 59, 0.8); border: 1px solid rgba(255,255,255,0.12); border-radius: 20px; padding: 10px 14px; color: #fff; font-size: 0.88rem; outline: none; }
        .input-bar button { background: linear-gradient(135deg, #38bdf8, #3b82f6); border: none; width: 38px; height: 38px; border-radius: 50%; color: #fff; font-size: 1rem; display: flex; align-items: center; justify-content: center; cursor: pointer; }
        
        /* Terminal View */
        .terminal-container { flex: 1; background: #050811; padding: 12px; font-family: 'Fira Code', monospace; display: flex; flex-direction: column; overflow: hidden; }
        #terminal-log { flex: 1; overflow-y: auto; color: #38bdf8; font-size: 0.78rem; line-height: 1.4; white-space: pre-wrap; word-break: break-all; margin-bottom: 10px; }
        .term-prompt-line { display: flex; gap: 6px; align-items: center; background: rgba(15, 23, 42, 0.9); padding: 8px 12px; border-radius: 8px; border: 1px solid rgba(56, 189, 248, 0.2); }
        .term-prompt { color: #c084fc; font-weight: 600; font-size: 0.8rem; }
        .term-input { flex: 1; background: none; border: none; color: #4ade80; font-family: 'Fira Code', monospace; font-size: 0.8rem; outline: none; }

        /* Screenshot View */
        .screenshot-box { padding: 14px; display: flex; flex-direction: column; align-items: center; gap: 10px; }
        .stream-bar { display: flex; gap: 8px; width: 100%; justify-content: center; }
        .stream-btn { background: rgba(30, 41, 59, 0.8); border: 1px solid rgba(255,255,255,0.12); color: #cbd5e1; padding: 6px 12px; border-radius: 8px; font-size: 0.75rem; cursor: pointer; font-weight: 600; }
        .stream-btn.active { background: #38bdf8; color: #0f172a; border-color: #38bdf8; }
        .screenshot-img { width: 100%; max-height: 380px; object-fit: contain; border-radius: 10px; border: 1px solid rgba(56, 189, 248, 0.3); background: #000; box-shadow: 0 4px 20px rgba(0,0,0,0.5); }
        
        /* Control Deck & Stats */
        .deck-section { padding: 12px 14px; }
        .section-label { font-size: 0.72rem; color: #94a3b8; text-transform: uppercase; font-weight: 700; margin-bottom: 8px; letter-spacing: 0.5px; }
        .deck-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px; margin-bottom: 12px; }
        .card { background: rgba(30, 41, 59, 0.65); border: 1px solid rgba(255,255,255,0.08); border-radius: 12px; padding: 12px; display: flex; flex-direction: column; gap: 4px; cursor: pointer; transition: transform 0.15s; }
        .card:active { transform: scale(0.97); background: rgba(56, 189, 248, 0.18); }
        .card-icon { font-size: 1.4rem; }
        .card-title { font-size: 0.85rem; font-weight: 600; color: #f1f5f9; }
        .card-desc { font-size: 0.7rem; color: #94a3b8; }
        .vol-slider-box { background: rgba(30, 41, 59, 0.65); border: 1px solid rgba(255,255,255,0.08); border-radius: 12px; padding: 12px; display: flex; flex-direction: column; gap: 8px; margin-bottom: 12px; }
        .vol-slider { width: 100%; accent-color: #38bdf8; cursor: pointer; height: 6px; }

        .btn-action { background: linear-gradient(135deg, #38bdf8, #3b82f6); color: #fff; border: none; padding: 10px 16px; border-radius: 10px; font-weight: 600; font-size: 0.85rem; cursor: pointer; width: 100%; text-align: center; }
        
        .stats-panel { padding: 14px; display: flex; flex-direction: column; gap: 10px; }
        .stat-box { background: rgba(30, 41, 59, 0.65); border: 1px solid rgba(255,255,255,0.08); border-radius: 12px; padding: 14px; }
        .stat-label { font-size: 0.72rem; color: #94a3b8; text-transform: uppercase; font-weight: 600; margin-bottom: 4px; }
        .stat-value { font-size: 1.25rem; font-weight: 700; color: #38bdf8; }
    </style>
</head>
<body>
    <header>
        <div class="logo">⚡ JARVIS Mobile</div>
        <div class="badge" id="status-badge">ONLINE</div>
    </header>

    <div class="tabs">
        <button class="tab-btn active" onclick="switchTab('chat')">💬 Chat</button>
        <button class="tab-btn" onclick="switchTab('terminal')">💻 Terminal</button>
        <button class="tab-btn" onclick="switchTab('commands')">⚡ Commands</button>
        <button class="tab-btn" onclick="switchTab('screen')">📸 Screen</button>
        <button class="tab-btn" onclick="switchTab('remote')">🎛️ Deck</button>
        <button class="tab-btn" onclick="switchTab('stats')">📊 Stats</button>
    </div>

    <div class="content-area">
        <!-- 1. AI Chat View -->
        <div id="view-chat" class="view active">
            <div id="chat-history">
                <div class="msg ai">Hello Kyle! Start typing any command or question below for instant mobile suggestions!</div>
            </div>
            <div id="autocomplete-popover" class="popover" style="display:none;"></div>
            <div class="input-bar">
                <input type="text" id="chat-input" placeholder="Type command (e.g. vol, app, theme, lock)..." onkeydown="if(event.key==='Enter') sendChat()">
                <button onclick="sendChat()">➔</button>
            </div>
        </div>

        <!-- 2. Interactive CLI Terminal View -->
        <div id="view-terminal" class="view">
            <div class="terminal-container">
                <div id="terminal-log">Windows PowerShell [Jarvis PC Terminal]
Type any command below (e.g. dir, git status, ping, ipconfig)
------------------------------------------------------------
</div>
                <div id="term-popover" class="popover" style="display:none;"></div>
                <div class="term-prompt-line">
                    <span class="term-prompt">jarvis@pc:~$</span>
                    <input type="text" id="term-input" class="term-input" placeholder="Type PowerShell command..." onkeydown="if(event.key==='Enter') runTerminalCmd()">
                </div>
            </div>
        </div>

        <!-- 3. Dedicated Interactive Commands Catalog View -->
        <div id="view-commands" class="view">
            <div class="deck-section">
                <div style="margin-bottom:12px;">
                    <input type="text" id="cmd-filter" placeholder="🔍 Search commands (wifi, ping, theme, app, vol)..." style="width:100%; background:rgba(30,41,59,0.8); border:1px solid rgba(56,189,248,0.3); border-radius:10px; padding:10px 14px; color:#fff; font-size:0.85rem; outline:none;" oninput="filterCommands(this.value)">
                </div>

                <div id="cmd-catalog-list">
                    <div class="section-label">📶 Network & Cloudflare</div>
                    <div class="deck-grid">
                        <div class="card cmd-card" onclick="sendCmd('wifi')">
                            <div class="card-title">📶 wifi</div>
                            <div class="card-desc">Show connected SSID & IP</div>
                        </div>
                        <div class="card cmd-card" onclick="sendCmd('wifi pass')">
                            <div class="card-title">🔑 wifi pass</div>
                            <div class="card-desc">Show Wi-Fi password</div>
                        </div>
                        <div class="card cmd-card" onclick="sendCmd('ping google.com')">
                            <div class="card-title">📡 ping google.com</div>
                            <div class="card-desc">Measure latency</div>
                        </div>
                        <div class="card cmd-card" onclick="sendCmd('flushdns')">
                            <div class="card-title">⚡ flushdns</div>
                            <div class="card-desc">Flush DNS resolver cache</div>
                        </div>
                        <div class="card cmd-card" onclick="sendCmd('tunnel')">
                            <div class="card-title">🌐 tunnel</div>
                            <div class="card-desc">Public Cloudflare Tunnel</div>
                        </div>
                        <div class="card cmd-card" onclick="sendCmd('mobile')">
                            <div class="card-title">📱 mobile</div>
                            <div class="card-desc">Show Mobile Companion HUD</div>
                        </div>
                    </div>

                    <div class="section-label">🔊 Volume & Audio</div>
                    <div class="deck-grid">
                        <div class="card cmd-card" onclick="sendCmd('vol 20')">
                            <div class="card-title">🌙 vol 20</div>
                            <div class="card-desc">Set Night sound 20%</div>
                        </div>
                        <div class="card cmd-card" onclick="sendCmd('vol 50')">
                            <div class="card-title">🔊 vol 50</div>
                            <div class="card-desc">Set master volume 50%</div>
                        </div>
                        <div class="card cmd-card" onclick="sendCmd('vol 100')">
                            <div class="card-title">🔊 vol 100</div>
                            <div class="card-desc">Set max volume 100%</div>
                        </div>
                        <div class="card cmd-card" onclick="sendCmd('vol 0')">
                            <div class="card-title">🔇 vol 0</div>
                            <div class="card-desc">Mute all PC audio</div>
                        </div>
                    </div>

                    <div class="section-label">🚀 Applications & Utility</div>
                    <div class="deck-grid">
                        <div class="card cmd-card" onclick="sendCmd('app studio')">
                            <div class="card-title">🎮 app studio</div>
                            <div class="card-desc">Launch Roblox Studio</div>
                        </div>
                        <div class="card cmd-card" onclick="sendCmd('app code')">
                            <div class="card-title">💻 app code</div>
                            <div class="card-desc">Open VS Code editor</div>
                        </div>
                        <div class="card cmd-card" onclick="sendCmd('app chrome')">
                            <div class="card-title">🌐 app chrome</div>
                            <div class="card-desc">Open Chrome Browser</div>
                        </div>
                        <div class="card cmd-card" onclick="sendCmd('speak Hello Kyle!')">
                            <div class="card-title">🗣️ speak [text]</div>
                            <div class="card-desc">TTS Speech voice</div>
                        </div>
                        <div class="card cmd-card" onclick="sendCmd('uptime')">
                            <div class="card-title">⏱️ uptime</div>
                            <div class="card-desc">Show system running time</div>
                        </div>
                        <div class="card cmd-card" onclick="sendCmd('lock')">
                            <div class="card-title">🔒 lock</div>
                            <div class="card-desc">Lock Windows PC</div>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- 3. High Quality PC Screen Mirror View -->
        <div id="view-screen" class="view">
            <div class="screenshot-box">
                <div class="stream-bar">
                    <button id="btn-stream-1s" class="stream-btn" onclick="setStreamInterval(1000)">🔴 Live 1s</button>
                    <button id="btn-stream-2s" class="stream-btn" onclick="setStreamInterval(2000)">⚡ 2s Refresh</button>
                    <button id="btn-stream-off" class="stream-btn active" onclick="setStreamInterval(0)">⏸️ Pause</button>
                </div>
                <img id="screen-img" class="screenshot-img" src="/api/screenshot" alt="PC Screen Capture">
                <button class="btn-action" onclick="refreshScreenshot()">📸 Snap Single Frame</button>
            </div>
        </div>

        <!-- 4. Expanded PC Control Deck View -->
        <div id="view-remote" class="view">
            <div class="deck-section">
                <div class="section-label">🔊 Volume & Sound Control</div>
                <div class="vol-slider-box">
                    <div style="display:flex; justify-content:space-between; font-size:0.8rem;">
                        <span>Master Volume</span>
                        <span id="vol-val-text" style="color:#38bdf8; font-weight:700;">50%</span>
                    </div>
                    <input type="range" min="0" max="100" value="50" class="vol-slider" id="vol-range" onchange="setVolume(this.value)">
                </div>
                <div class="deck-grid">
                    <div class="card" onclick="sendCmd('vol 20')">
                        <div class="card-icon">🌙</div>
                        <div class="card-title">Night Sound (20%)</div>
                        <div class="card-desc">Low background volume</div>
                    </div>
                    <div class="card" onclick="sendCmd('vol 0')">
                        <div class="card-icon">🔇</div>
                        <div class="card-title">Mute PC</div>
                        <div class="card-desc">Silence all PC audio</div>
                    </div>
                </div>

                <div class="section-label">🚀 Application Launch Deck</div>
                <div class="deck-grid">
                    <div class="card" onclick="sendCmd('app studio')">
                        <div class="card-icon">🎮</div>
                        <div class="card-title">Roblox Studio</div>
                        <div class="card-desc">Launch Studio IDE</div>
                    </div>
                    <div class="card" onclick="sendCmd('app code')">
                        <div class="card-icon">💻</div>
                        <div class="card-title">VS Code</div>
                        <div class="card-desc">Open code workspace</div>
                    </div>
                    <div class="card" onclick="sendCmd('app chrome')">
                        <div class="card-icon">🌐</div>
                        <div class="card-title">Chrome Browser</div>
                        <div class="card-desc">Open web browser</div>
                    </div>
                    <div class="card" onclick="sendCmd('open C:\\Users\\Kyle\\Downloads')">
                        <div class="card-icon">📁</div>
                        <div class="card-title">Downloads Folder</div>
                        <div class="card-desc">Open File Explorer</div>
                    </div>
                </div>

                <div class="section-label">🎨 Appearance & PC Security</div>
                <div class="deck-grid">
                    <div class="card" onclick="sendCmd('theme dracula')">
                        <div class="card-icon">🎨</div>
                        <div class="card-title">Dracula Theme</div>
                        <div class="card-desc">Dark purple theme</div>
                    </div>
                    <div class="card" onclick="sendCmd('theme nord')">
                        <div class="card-icon">❄️</div>
                        <div class="card-title">Nord Theme</div>
                        <div class="card-desc">Arctic blue theme</div>
                    </div>
                    <div class="card" onclick="sendCmd('lock')">
                        <div class="card-icon">🔒</div>
                        <div class="card-title">Lock PC</div>
                        <div class="card-desc">Lock Windows session</div>
                    </div>
                    <div class="card" onclick="sendCmd('stats')">
                        <div class="card-icon">📊</div>
                        <div class="card-title">System Stats</div>
                        <div class="card-desc">View active processes</div>
                    </div>
                </div>
            </div>
        </div>

        <!-- 5. Live Telemetry View -->
        <div id="view-stats" class="view">
            <div class="stats-panel">
                <div class="stat-box">
                    <div class="stat-label">Computer Name</div>
                    <div class="stat-value" id="stat-pc">--</div>
                </div>
                <div class="stat-box">
                    <div class="stat-label">RAM Memory Load</div>
                    <div class="stat-value" id="stat-ram">--%</div>
                </div>
                <div class="stat-box">
                    <div class="stat-label">Active Windows Foreground</div>
                    <div class="stat-value" id="stat-window" style="font-size: 0.9rem;">--</div>
                </div>
            </div>
        </div>
    </div>

    <script>
        let streamTimer = null;

        function switchTab(name) {
            document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
            document.querySelectorAll('.view').forEach(v => v.classList.remove('active'));
            event.target.classList.add('active');
            document.getElementById('view-' + name).classList.add('active');
            if (name === 'stats') fetchStats();
            if (name === 'screen') refreshScreenshot();
            if (name !== 'screen') setStreamInterval(0);
        }

        function setStreamInterval(ms) {
            if (streamTimer) clearInterval(streamTimer);
            document.querySelectorAll('.stream-btn').forEach(b => b.classList.remove('active'));

            if (ms === 1000) document.getElementById('btn-stream-1s').classList.add('active');
            else if (ms === 2000) document.getElementById('btn-stream-2s').classList.add('active');
            else document.getElementById('btn-stream-off').classList.add('active');

            if (ms > 0) {
                refreshScreenshot();
                streamTimer = setInterval(refreshScreenshot, ms);
            }
        }

        let debounceTimer;
        document.getElementById('chat-input').addEventListener('input', (e) => {
            clearTimeout(debounceTimer);
            debounceTimer = setTimeout(() => fetchSuggestions(e.target.value, 'chat-input'), 120);
        });

        async function fetchSuggestions(query, targetId) {
            const pop = document.getElementById('autocomplete-popover');
            if (!query || query.trim().length < 1) {
                pop.style.display = 'none';
                return;
            }

            try {
                const res = await fetch('/api/suggestions?q=' + encodeURIComponent(query));
                const items = await res.json();

                if (!items || items.length === 0) {
                    pop.style.display = 'none';
                    return;
                }

                pop.innerHTML = '';
                items.forEach(item => {
                    const div = document.createElement('div');
                    div.className = 'pop-item';
                    div.innerHTML = '<div class="pop-title">' + item.title + '</div><div class="pop-desc">' + item.desc + '</div>';
                    div.onclick = () => {
                        let cmdToRun = item.title.replace('⚡ Command: ', '').replace('📱 ', '').replace('🌐 ', '');
                        document.getElementById(targetId).value = cmdToRun;
                        pop.style.display = 'none';
                        sendChat();
                    };
                    pop.appendChild(div);
                });
                pop.style.display = 'flex';
            } catch (e) {
                pop.style.display = 'none';
            }
        }

        async function sendChat() {
            document.getElementById('autocomplete-popover').style.display = 'none';
            const input = document.getElementById('chat-input');
            const prompt = input.value.trim();
            if (!prompt) return;

            input.value = '';
            appendMsg(prompt, 'user');
            const aiBubble = appendMsg('🧠 Thinking...', 'ai');

            try {
                const res = await fetch('/api/chat', {
                    method: 'POST',
                    headers: {'Content-Type': 'application/json'},
                    body: JSON.stringify({ prompt: prompt })
                });
                const data = await res.json();
                aiBubble.innerText = data.response || 'Done.';
            } catch (err) {
                aiBubble.innerText = '⚠️ Connection Error: ' + err.message;
            }
        }

        async function runTerminalCmd() {
            const input = document.getElementById('term-input');
            const log = document.getElementById('terminal-log');
            const cmd = input.value.trim();
            if (!cmd) return;

            input.value = '';
            log.innerText += '\nPS C:\\> ' + cmd + '\nExecuting...';
            log.scrollTop = log.scrollHeight;

            try {
                const res = await fetch('/api/terminal', {
                    method: 'POST',
                    headers: {'Content-Type': 'application/json'},
                    body: JSON.stringify({ command: cmd })
                });
                const data = await res.json();
                log.innerText += '\n' + data.output + '\n------------------------------------------------------------';
                log.scrollTop = log.scrollHeight;
            } catch (err) {
                log.innerText += '\n⚠️ Error executing command: ' + err.message + '\n';
            }
        }

        function refreshScreenshot() {
            const img = document.getElementById('screen-img');
            img.src = '/api/screenshot?t=' + new Date().getTime();
        }

        function setVolume(level) {
            document.getElementById('vol-val-text').innerText = level + '%';
            sendCmd('vol ' + level);
        }

        async function sendCmd(cmd) {
            appendMsg('⚡ Triggered: ' + cmd, 'user');
            try {
                const res = await fetch('/api/command', {
                    method: 'POST',
                    headers: {'Content-Type': 'application/json'},
                    body: JSON.stringify({ command: cmd })
                });
                const data = await res.json();
                appendMsg('✅ ' + data.message, 'ai');
            } catch (err) {
                appendMsg('⚠️ Failed running command.', 'ai');
            }
        }

        function appendMsg(text, type) {
            const history = document.getElementById('chat-history');
            const div = document.createElement('div');
            div.className = 'msg ' + type;
            div.innerText = text;
            history.appendChild(div);
            history.scrollTop = history.scrollHeight;
            return div;
        }

        async function fetchStats() {
            try {
                const res = await fetch('/api/stats');
                const data = await res.json();
                document.getElementById('stat-pc').innerText = data.computerName;
                document.getElementById('stat-ram').innerText = data.memoryLoad + '% (' + data.usedRamMb + ' / ' + data.totalRamMb + ' MB)';
                document.getElementById('stat-window').innerText = data.activeWindow;
            } catch (e) {}
        }

        setInterval(fetchStats, 5000);
        fetchStats();
    </script>
</body>
</html>
""";
        }
    }
}
