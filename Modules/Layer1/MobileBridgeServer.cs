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
using PathHandler = JarvisLauncher.PathHandler;

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

        public static void Start(int port = 8085)
        {
            
            if (_isRunning) return;

            _port = port;
            _localIp = GetLocalIPAddress();
            EnsureUrlAclPermission(_port);
            EnsureFirewallRule(_port);

            try
            {
                // Use http://+:{port}/ to bind all interfaces (http.sys wildcard).
                // This is required so cloudflared's proxy to 127.0.0.1 is accepted.
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://+:{_port}/");
                listener.Start();
                _listener = listener;
                _isRunning = true;
                ChatOverlay.LogConsoleAction("Mobile Server Active", $"Listening on http://+:{_port}/ (all interfaces)");
            }
            catch (Exception ex)
            {
                // Wildcard failed (no netsh ACL), fall back to specific IPs
                ChatOverlay.LogConsoleAction("Mobile Server Wildcard Failed, Falling Back", ex.Message);
                try
                {
                    var listener = new HttpListener();
                    listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                    listener.Prefixes.Add($"http://localhost:{_port}/");
                    if (!string.IsNullOrEmpty(_localIp) && _localIp != "127.0.0.1")
                    {
                        try { listener.Prefixes.Add($"http://{_localIp}:{_port}/"); } catch { }
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

        /// <summary>
        /// Validates the P2P shared secret if one is configured on this node.
        /// If no secret is set, access is allowed (LAN-open mode).
        /// </summary>
        private static bool CheckP2PSecret(HttpListenerRequest req, HttpListenerResponse resp)
        {
            string configSecret = SettingsManager.Current.P2PServerSecret;
            if (string.IsNullOrEmpty(configSecret)) return true; // open mode

            string? provided = req.Headers["X-Jarvis-Secret"];
            if (provided == configSecret) return true;

            resp.StatusCode = 401;
            byte[] buf = Encoding.UTF8.GetBytes("{\"error\":\"Invalid or missing X-Jarvis-Secret header.\"}");
            resp.ContentType = "application/json";
            resp.OutputStream.Write(buf);
            resp.Close();
            return false;
        }

        private static async Task ProcessRequestAsync(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var resp = ctx.Response;

            resp.Headers.Add("Access-Control-Allow-Origin", "*");
            resp.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS, DELETE");
            resp.Headers.Add("Access-Control-Allow-Headers", "Content-Type, X-Jarvis-Secret");

            if (req.HttpMethod == "OPTIONS")
            {
                resp.StatusCode = 204;
                resp.Close();
                return;
            }

            string path = req.Url?.AbsolutePath ?? "/";

            try
            {
                TextOverlay.Show(path);
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
                // ── P2P Compute Node Routes ─────────────────────────────────────────────
                else if (path == "/api/files/root" && req.HttpMethod == "GET")
                {
                    if (!CheckP2PSecret(req, resp)) return;

                    string json = JsonSerializer.Serialize(new
                    {
                        root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    });

                    byte[] buf = Encoding.UTF8.GetBytes(json);
                    resp.ContentType = "application/json";
                    resp.ContentLength64 = buf.Length;
                    await resp.OutputStream.WriteAsync(buf);
                }
                else if (path == "/api/files/list" && req.HttpMethod == "GET")
                {
                    if (!CheckP2PSecret(req, resp)) return;

                    string requestedPath = req.QueryString["path"] ?? string.Empty;
                    string folderPath = ResolveRequestedPath(requestedPath);
                    var items = ReadDirectoryEntries(folderPath);

                    string json = JsonSerializer.Serialize(items);
                    byte[] buf = Encoding.UTF8.GetBytes(json);
                    resp.ContentType = "application/json";
                    resp.ContentLength64 = buf.Length;
                    await resp.OutputStream.WriteAsync(buf);
                }
                else if (path == "/api/files/upload" && req.HttpMethod == "POST")
                {
                    if (!CheckP2PSecret(req, resp)) return;

                    string targetFolder = ResolveRequestedPath(req.QueryString["path"] ?? string.Empty);
                    string fileName = SanitizeFileName(req.QueryString["name"] ?? "upload.bin");
                    Directory.CreateDirectory(targetFolder);

                    string destinationPath = Path.Combine(targetFolder, fileName);
                    using (var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await req.InputStream.CopyToAsync(output);
                    }

                    string json = JsonSerializer.Serialize(new
                    {
                        status = "success",
                        path = destinationPath
                    });

                    byte[] buf = Encoding.UTF8.GetBytes(json);
                    resp.ContentType = "application/json";
                    resp.ContentLength64 = buf.Length;
                    await resp.OutputStream.WriteAsync(buf);
                }
                else if (path == "/api/files/delete" && req.HttpMethod == "POST")
                {
                    if (!CheckP2PSecret(req, resp)) return;

                    using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                    string body = await reader.ReadToEndAsync();
                    using var doc = JsonDocument.Parse(body);
                    string filePath = doc.RootElement.TryGetProperty("path", out var pProp) ? pProp.GetString() ?? string.Empty : string.Empty;
                    string resolvedPath = ResolveRequestedPath(filePath);

                    if (Directory.Exists(resolvedPath))
                    {
                        Directory.Delete(resolvedPath, true);
                    }
                    else if (File.Exists(resolvedPath))
                    {
                        File.Delete(resolvedPath);
                    }

                    string json = JsonSerializer.Serialize(new { status = "success" });
                    byte[] buf = Encoding.UTF8.GetBytes(json);
                    resp.ContentType = "application/json";
                    resp.ContentLength64 = buf.Length;
                    await resp.OutputStream.WriteAsync(buf);
                }
                else if (path == "/p2p/health")
                {
                    // Lightweight liveness ping — no auth required
                    string json = JsonSerializer.Serialize(new { status = "ok", pc = Environment.MachineName });
                    byte[] buf = Encoding.UTF8.GetBytes(json);
                    resp.ContentType = "application/json";
                    await resp.OutputStream.WriteAsync(buf);
                }
                else if (path == "/p2p/info")
                {
                    if (!CheckP2PSecret(req, resp)) return;
                    if (!SettingsManager.Current.P2PServerEnabled)
                    {
                        resp.StatusCode = 403;
                        byte[] buf403 = Encoding.UTF8.GetBytes("{\"error\":\"P2P server not enabled on this node.\"}");
                        resp.ContentType = "application/json";
                        await resp.OutputStream.WriteAsync(buf403);
                        return;
                    }

                    var stats = GetSystemStats();
                    var statsDict = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, JsonElement>>(
                        JsonSerializer.Serialize(stats)) ?? new();

                    double cpuLoad = statsDict.TryGetValue("cpuPercent", out var cp) ? cp.GetDouble() : 0;
                    double ramFree = 0;
                    if (statsDict.TryGetValue("freeRamMb", out var fr))
                        ramFree = Math.Round(fr.GetDouble() / 1024.0, 1);

                    // Detect available backends
                    var backends = new System.Collections.Generic.List<string>();
                    if (!string.IsNullOrEmpty(SettingsManager.Current.GoogleAIKey)) backends.Add("Gemini");
                    if (!string.IsNullOrEmpty(SettingsManager.Current.OpenAIKey)) backends.Add("OpenAI");
                    backends.Add("Ollama"); // Always advertise Ollama; client will fail gracefully if not installed

                    var ollamaModels = await LlmRouter.GetOllamaModelsAsync();

                    string infoJson = JsonSerializer.Serialize(new
                    {
                        pc_name = Environment.MachineName,
                        backends,
                        models = ollamaModels,
                        cpu_load = cpuLoad,
                        ram_free_gb = ramFree,
                        p2p_version = "1.0"
                    });
                    byte[] infoBuf = Encoding.UTF8.GetBytes(infoJson);
                    resp.ContentType = "application/json";
                    await resp.OutputStream.WriteAsync(infoBuf);
                }
                else if (path == "/p2p/ask" && req.HttpMethod == "POST")
                {
                    if (!CheckP2PSecret(req, resp)) return;
                    if (!SettingsManager.Current.P2PServerEnabled)
                    {
                        resp.StatusCode = 403;
                        byte[] buf403 = Encoding.UTF8.GetBytes("{\"error\":\"P2P server not enabled on this node.\"}");
                        resp.ContentType = "application/json";
                        await resp.OutputStream.WriteAsync(buf403);
                        return;
                    }

                    using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                    string body = await reader.ReadToEndAsync();
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;

                    string prompt = root.TryGetProperty("prompt", out var pr) ? pr.GetString() ?? "" : "";
                    string model = root.TryGetProperty("model", out var mo) ? mo.GetString() ?? "auto" : "auto";

                    // Reconstruct chat history if provided
                    List<ChatTurn>? history = null;
                    if (root.TryGetProperty("history", out var hist) && hist.ValueKind == JsonValueKind.Array)
                    {
                        history = new List<ChatTurn>();
                        foreach (var item in hist.EnumerateArray())
                        {
                            history.Add(new ChatTurn
                            {
                                Role = item.TryGetProperty("role", out var ro) ? ro.GetString() ?? "user" : "user",
                                Text = item.TryGetProperty("text", out var tx) ? tx.GetString() ?? "" : ""
                            });
                        }
                    }

                    if (string.IsNullOrEmpty(prompt))
                    {
                        resp.StatusCode = 400;
                        byte[] buf400 = Encoding.UTF8.GetBytes("{\"error\":\"prompt is required\"}");
                        resp.ContentType = "application/json";
                        await resp.OutputStream.WriteAsync(buf400);
                        return;
                    }

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    // Use the local LlmRouter (but avoid P2P to prevent loops)
                    string savedBackend = SettingsManager.Current.LlmBackend;
                    if (savedBackend == "P2P") SettingsManager.Current.LlmBackend = "Gemini"; // prevent P2P loop
                    string aiResponse = await LlmRouter.AskAsync(prompt, history);
                    SettingsManager.Current.LlmBackend = savedBackend;
                    sw.Stop();

                    string askJson = JsonSerializer.Serialize(new
                    {
                        response = aiResponse,
                        model_used = SettingsManager.Current.LlmBackend,
                        latency_ms = sw.ElapsedMilliseconds,
                        pc_name = Environment.MachineName
                    });
                    byte[] askBuf = Encoding.UTF8.GetBytes(askJson);
                    resp.ContentType = "application/json";
                    await resp.OutputStream.WriteAsync(askBuf);
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

        private static string ResolveRequestedPath(string requestedPath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(requestedPath))
                {
                    return Path.GetFullPath(requestedPath);
                }
            }
            catch { }

            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        private static List<object> ReadDirectoryEntries(string folderPath)
        {
            var items = new List<object>();
            try
            {
                foreach (var directory in Directory.GetDirectories(folderPath).OrderBy(path => path))
                {
                    var info = new DirectoryInfo(directory);
                    items.Add(new
                    {
                        name = info.Name,
                        path = info.FullName,
                        isDirectory = true,
                        size = 0L,
                        modifiedUtc = info.LastWriteTimeUtc
                    });
                }

                foreach (var file in Directory.GetFiles(folderPath).OrderBy(path => path))
                {
                    var info = new FileInfo(file);
                    items.Add(new
                    {
                        name = info.Name,
                        path = info.FullName,
                        isDirectory = false,
                        size = info.Length,
                        modifiedUtc = info.LastWriteTimeUtc
                    });
                }
            }
            catch (Exception ex)
            {
                items.Add(new
                {
                    name = "Error",
                    path = folderPath,
                    isDirectory = false,
                    size = 0L,
                    modifiedUtc = DateTime.UtcNow,
                    error = ex.Message
                });
            }

            return items;
        }

        private static string SanitizeFileName(string fileName)
        {
            try
            {
                foreach (var invalid in Path.GetInvalidFileNameChars())
                {
                    fileName = fileName.Replace(invalid, '_');
                }
            }
            catch { }

            return string.IsNullOrWhiteSpace(fileName) ? "upload.bin" : fileName;
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
            //HTML
            PathHandler NewPath = new PathHandler();
                return File.ReadAllText(NewPath.GetCurrentSourceDirectory());
        }
    } 
        }

