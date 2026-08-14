// Developer: heaplyn
// Date: 2026-08-12
// Summary: High-performance, permission-independent HTTP server using TcpListener.
// Enhanced with Dual-Stack support, self-healing firewall tools, and advanced file/stats API.
// Updated screen capture to support high-DPI full-desktop snapshots across all monitors.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// Resolve ambiguity between System.Drawing.Size and System.Windows.Size
using Size = System.Drawing.Size;

namespace JarvisLauncher
{
    public static class MobileBridgeServer
    {
        private static TcpListener? _listener;
        private static bool _isRunning;
        private static int _port = 9000;

        public static bool IsActive => _isRunning && _listener != null;
        public static int Port => _port;
        public static string ServerUrl => $"http://{GetLocalIPAddress()}:{_port}/";
        public static string HostnameDomain => $"http://{Environment.MachineName.ToLower()}.local:{_port}/";
        public static string JarvisDomain => $"http://jarvis.local:{_port}/";

        public static void Start(int port = 9000)
        {
            if (_isRunning) return;
            _port = port;
            _isRunning = true;

            Task.Run(async () =>
            {
                try
                {
                    LogToFile($"Starting Dual-Stack TCP Server on port {_port}...");

                    _listener = TcpListener.Create(_port);
                    _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    _listener.Start();

                    LogToFile($"Server LIVE on all interfaces at port {_port}.");

                    Application.Current.Dispatcher.Invoke(() =>
                        ChatOverlay.LogConsoleAction("Mobile Server Active", $"Listening on port {_port} (Dual-Stack TCP)"));

                    while (_isRunning && _listener != null)
                    {
                        try
                        {
                            var client = await _listener.AcceptTcpClientAsync();
                            _ = Task.Run(() => HandleClientAsync(client));
                        }
                        catch (Exception ex)
                        {
                            if (_isRunning) LogToFile($"Accept error: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _isRunning = false;
                    LogToFile($"FATAL server crash: {ex}");
                }
            });
        }

        private static async Task HandleClientAsync(TcpClient client)
        {
            var remoteEp = client.Client.RemoteEndPoint;
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 15000;

                    byte[] buffer = new byte[8192];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) return;

                    string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    string[] lines = request.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    if (lines.Length == 0) return;

                    string[] requestLine = lines[0].Split(' ');
                    if (requestLine.Length < 2) return;

                    string method = requestLine[0].ToUpper();
                    string fullPath = requestLine[1];

                    string path = fullPath;
                    string queryStr = string.Empty;
                    int qIndex = fullPath.IndexOf('?');
                    if (qIndex != -1)
                    {
                        path = fullPath.Substring(0, qIndex);
                        queryStr = fullPath.Substring(qIndex + 1);
                    }

                    var query = new Dictionary<string, string>();
                    foreach (var pair in queryStr.Split('&', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts2 = pair.Split('=');
                        if (parts2.Length == 2) query[parts2[0]] = Uri.UnescapeDataString(parts2[1]);
                        else if (parts2.Length == 1) query[parts2[0]] = string.Empty;
                    }

                    // Read headers
                    int contentLength = 0;
                    foreach (var header in lines.Skip(1))
                    {
                        if (string.IsNullOrWhiteSpace(header)) break;
                        if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            int.TryParse(header.Substring(15).Trim(), out contentLength);
                        }
                    }

                    if (!path.Contains("screenshot"))
                        LogToFile($"Request: {method} {path} from {remoteEp}");

                    if (method == "OPTIONS")
                    {
                        await SendResponseAsync(stream, 204, "No Content", null, "text/plain");
                        return;
                    }

                    if (path == "/" || path.Equals("/index.html", StringComparison.OrdinalIgnoreCase))
                    {
                        string html = GetMobileAppHtml();
                        await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes(html), "text/html; charset=utf-8");
                    }
                    else if (path.Equals("/cmd", StringComparison.OrdinalIgnoreCase) || path.Equals("/bar", StringComparison.OrdinalIgnoreCase))
                    {
                        string html = GetMobileCommandBarHtml();
                        await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes(html), "text/html; charset=utf-8");
                    }
                    else if (path.Contains("health", StringComparison.OrdinalIgnoreCase))
                    {
                        string json = JsonSerializer.Serialize(new { status = "ok", pc = Environment.MachineName, time = DateTime.Now.ToString("HH:mm:ss") });
                        await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes(json), "application/json");
                    }
                    else if (path.Equals("/api/suggestions", StringComparison.OrdinalIgnoreCase))
                    {
                        query.TryGetValue("q", out string? q);
                        var list = new List<object>();
                        if (!string.IsNullOrWhiteSpace(q))
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    foreach (var s in CommandParser.GetSuggestions(q).Take(6))
                                        list.Add(new { title = s.Title, desc = s.Description });
                                }
                                catch { }
                            });
                        }
                        string json = JsonSerializer.Serialize(list);
                        await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes(json), "application/json");
                    }
                    else if (path.Equals("/api/chat", StringComparison.OrdinalIgnoreCase) && method == "POST")
                    {
                        string body = await GetRequestBodyAsync(request, stream, contentLength);
                        string reply;
                        try
                        {
                            using var doc = JsonDocument.Parse(body);
                            string prompt = doc.RootElement.TryGetProperty("prompt", out var pProp) ? pProp.GetString() ?? "" : "";
                            reply = await AiAPI.AskGemini(prompt);
                        }
                        catch (Exception ex) { reply = $"Error: {ex.Message}"; }
                        string json = JsonSerializer.Serialize(new { response = reply });
                        await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes(json), "application/json");
                    }
                    else if (path.Equals("/api/terminal", StringComparison.OrdinalIgnoreCase) && method == "POST")
                    {
                        if (!SettingsManager.Current.MobileAllowTerminal)
                        {
                            await SendResponseAsync(stream, 403, "Forbidden", Encoding.UTF8.GetBytes("{\"error\":\"Remote terminal is disabled in Mobile Hub settings.\"}"), "application/json");
                            return;
                        }
                        string body = await GetRequestBodyAsync(request, stream, contentLength);
                        string output;
                        try
                        {
                            using var doc = JsonDocument.Parse(body);
                            string command = doc.RootElement.TryGetProperty("command", out var cProp2) ? cProp2.GetString() ?? "" : "";
                            output = RunPowerShellCommand(command);
                        }
                        catch (Exception ex) { output = $"Error: {ex.Message}"; }
                        string json = JsonSerializer.Serialize(new { output });
                        await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes(json), "application/json");
                    }
                    else if (path.Equals("/api/stats", StringComparison.OrdinalIgnoreCase))
                    {
                        var stats = GetSystemStats();
                        string json = JsonSerializer.Serialize(stats);
                        await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes(json), "application/json");
                    }
                    else if (path.Contains("screenshot", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!SettingsManager.Current.MobileAllowScreenMirror)
                        {
                            await SendResponseAsync(stream, 403, "Forbidden", Encoding.UTF8.GetBytes("Screen mirroring is disabled in Mobile Hub settings."), "text/plain");
                            return;
                        }
                        byte[]? img = CaptureScreenJpeg();
                        if (img != null && img.Length > 0)
                            await SendResponseAsync(stream, 200, "OK", img, "image/jpeg");
                        else
                            await SendResponseAsync(stream, 500, "Error", Encoding.UTF8.GetBytes("Capture failed"), "text/plain");
                    }
                    else if (path.Equals("/api/clipboard", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!SettingsManager.Current.MobileAllowClipboard)
                        {
                            await SendResponseAsync(stream, 403, "Forbidden", Encoding.UTF8.GetBytes("{\"error\":\"Clipboard sync is disabled in Mobile Hub settings.\"}"), "application/json");
                            return;
                        }
                        if (method == "GET")
                        {
                            string text = string.Empty;
                            Application.Current.Dispatcher.Invoke(() => { try { text = Clipboard.GetText(); } catch { } });
                            string json = JsonSerializer.Serialize(new { text });
                            await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes(json), "application/json");
                        }
                        else if (method == "POST")
                        {
                            string body = await GetRequestBodyAsync(request, stream, contentLength);
                            try
                            {
                                using var doc = JsonDocument.Parse(body);
                                string text = doc.RootElement.GetProperty("text").GetString() ?? "";
                                Application.Current.Dispatcher.Invoke(() => { try { Clipboard.SetText(text); } catch { } });
                            }
                            catch { }
                            await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes("{\"status\":\"success\"}"), "application/json");
                        }
                    }
                    else if (path.Equals("/api/command", StringComparison.OrdinalIgnoreCase) && method == "POST")
                    {
                        string body = await GetRequestBodyAsync(request, stream, contentLength);
                        string command = "";
                        try
                        {
                            using var doc = JsonDocument.Parse(body);
                            command = doc.RootElement.TryGetProperty("command", out var cProp) ? cProp.GetString() ?? "" : "";
                            Application.Current.Dispatcher.Invoke(() => CommandParser.ExecuteFirstSuggestion(command));
                        }
                        catch { }
                        string json = JsonSerializer.Serialize(new { status = "success", message = $"Executed: {command}" });
                        await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes(json), "application/json");
                    }
                    else if (path.Equals("/api/notes", StringComparison.OrdinalIgnoreCase))
                    {
                        string dir = GetNotesDir();
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                        if (method == "GET")
                        {
                            var list = new List<object>();
                            foreach (var file in Directory.GetFiles(dir, "*.txt"))
                            {
                                try
                                {
                                    string name = Path.GetFileNameWithoutExtension(file);
                                    string content = File.ReadAllText(file);
                                    list.Add(new { name, content });
                                }
                                catch { }
                            }
                            string json = JsonSerializer.Serialize(list);
                            await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes(json), "application/json");
                        }
                        else if (method == "POST")
                        {
                            string body = await GetRequestBodyAsync(request, stream, contentLength);
                            try
                            {
                                using var doc = JsonDocument.Parse(body);
                                string name = doc.RootElement.GetProperty("name").GetString() ?? "";
                                string content = doc.RootElement.GetProperty("content").GetString() ?? "";

                                foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');

                                if (!string.IsNullOrEmpty(name))
                                {
                                    string file = Path.Combine(dir, $"{name}.txt");
                                    File.WriteAllText(file, content);
                                    await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes("{\"status\":\"saved\"}"), "application/json");
                                }
                                else
                                {
                                    await SendResponseAsync(stream, 400, "Bad Request", Encoding.UTF8.GetBytes("{\"error\":\"Invalid note name\"}"), "application/json");
                                }
                            }
                            catch (Exception ex)
                            {
                                await SendResponseAsync(stream, 500, "Error", Encoding.UTF8.GetBytes($"{{\"error\":\"{ex.Message}\"}}"), "application/json");
                            }
                        }
                        else if (method == "DELETE")
                        {
                            string body = await GetRequestBodyAsync(request, stream, contentLength);
                            try
                            {
                                using var doc = JsonDocument.Parse(body);
                                string name = doc.RootElement.GetProperty("name").GetString() ?? "";
                                string file = Path.Combine(dir, $"{name}.txt");
                                if (File.Exists(file))
                                {
                                    File.Delete(file);
                                    await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes("{\"status\":\"deleted\"}"), "application/json");
                                }
                                else
                                {
                                    await SendResponseAsync(stream, 404, "Not Found", Encoding.UTF8.GetBytes("{\"error\":\"Note not found\"}"), "application/json");
                                }
                            }
                            catch (Exception ex)
                            {
                                await SendResponseAsync(stream, 500, "Error", Encoding.UTF8.GetBytes($"{{\"error\":\"{ex.Message}\"}}"), "application/json");
                            }
                        }
                    }
                    else if (path.Equals("/api/calendar", StringComparison.OrdinalIgnoreCase))
                    {
                        if (method == "GET")
                        {
                            var events = CalendarOverlay.LoadEvents();
                            string json = JsonSerializer.Serialize(events);
                            await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes(json), "application/json");
                        }
                        else if (method == "POST")
                        {
                            string body = await GetRequestBodyAsync(request, stream, contentLength);
                            try
                            {
                                var ev = JsonSerializer.Deserialize<CalendarEvent>(body);
                                if (ev != null)
                                {
                                    if (ev.Id == Guid.Empty) ev.Id = Guid.NewGuid();
                                    CalendarOverlay.LogEvent(ev.Title, ev.DateString, ev.Time, ev.Category);
                                    await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes("{\"status\":\"success\"}"), "application/json");
                                }
                                else
                                {
                                    await SendResponseAsync(stream, 400, "Bad Request", Encoding.UTF8.GetBytes("{\"error\":\"Invalid event format\"}"), "application/json");
                                }
                            }
                            catch (Exception ex)
                            {
                                await SendResponseAsync(stream, 500, "Error", Encoding.UTF8.GetBytes($"{{\"error\":\"{ex.Message}\"}}"), "application/json");
                            }
                        }
                        else if (method == "DELETE")
                        {
                            string body = await GetRequestBodyAsync(request, stream, contentLength);
                            try
                            {
                                using var doc = JsonDocument.Parse(body);
                                Guid id = doc.RootElement.GetProperty("id").GetGuid();
                                var events = CalendarOverlay.LoadEvents();
                                var match = events.FirstOrDefault(e => e.Id == id);
                                if (match != null)
                                {
                                    events.Remove(match);
                                    CalendarOverlay.SaveEvents();
                                    await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes("{\"status\":\"deleted\"}"), "application/json");
                                }
                                else
                                {
                                    await SendResponseAsync(stream, 404, "Not Found", Encoding.UTF8.GetBytes("{\"error\":\"Event not found\"}"), "application/json");
                                }
                            }
                            catch (Exception ex)
                            {
                                await SendResponseAsync(stream, 500, "Error", Encoding.UTF8.GetBytes($"{{\"error\":\"{ex.Message}\"}}"), "application/json");
                            }
                        }
                    }
                    else if (path.Equals("/api/files/organize", StringComparison.OrdinalIgnoreCase) && method == "POST")
                    {
                        string body = await GetRequestBodyAsync(request, stream, contentLength);
                        var results = new List<string>();
                        try
                        {
                            using var doc = JsonDocument.Parse(body);
                            string targetDir = doc.RootElement.GetProperty("path").GetString() ?? "";
                            string task = doc.RootElement.GetProperty("task").GetString() ?? "";
                            bool execute = doc.RootElement.GetProperty("execute").GetBoolean();

                            if (Directory.Exists(targetDir))
                            {
                                if (task == "extension") results = FileOrganizer.CategorizeByExtension(targetDir, !execute);
                                else if (task == "date") results = FileOrganizer.OrganizeByDate(targetDir, !execute);
                                else if (task == "duplicate")
                                {
                                    List<string> purgeLogs;
                                    var logs = FileOrganizer.FindDuplicates(targetDir, execute, out purgeLogs);
                                    results = execute ? purgeLogs : logs;
                                }
                                else if (task == "large") results = FileOrganizer.AuditLargeFiles(targetDir, 100 * 1024 * 1024);
                                else if (task == "empty") results = FileOrganizer.PurgeEmptyDirectories(targetDir, !execute);
                            }
                            else
                            {
                                results.Add("⚠️ Target directory does not exist.");
                            }
                        }
                        catch (Exception ex)
                        {
                            results.Add($"❌ Error: {ex.Message}");
                        }
                        string json = JsonSerializer.Serialize(results);
                        await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes(json), "application/json");
                    }
                    else if (path.Equals("/api/screen/analyze", StringComparison.OrdinalIgnoreCase))
                    {
                        var windows = ScreenAnalyzer.GetActiveWindows();
                        double coverage, overlap;
                        string feedback;
                        ScreenAnalyzer.CalculateClutterIndex(windows, out coverage, out overlap, out feedback);

                        System.Windows.Media.Color dominant, accent;
                        ScreenAnalyzer.ExtractScreenPalette(out dominant, out accent);

                        string domHex = $"#{dominant.R:X2}{dominant.G:X2}{dominant.B:X2}";
                        string accHex = $"#{accent.R:X2}{accent.G:X2}{accent.B:X2}";

                        var payload = new
                        {
                            coverage = Math.Round(coverage, 1),
                            overlap = Math.Round(overlap, 1),
                            feedback = feedback,
                            dominantHex = domHex,
                            accentHex = accHex,
                            windowsCount = windows.Count
                        };

                        string json = JsonSerializer.Serialize(payload);
                        await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes(json), "application/json");
                    }
                    else if (path.Equals("/api/screen/tile", StringComparison.OrdinalIgnoreCase) && method == "POST")
                    {
                        Application.Current.Dispatcher.Invoke(() => ScreenAnalyzer.TileActiveWindows());
                        string json = JsonSerializer.Serialize(new { status = "success", message = "Windows tiled." });
                        await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes(json), "application/json");
                    }
                    else if (path.Equals("/api/screen/synctheme", StringComparison.OrdinalIgnoreCase) && method == "POST")
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            System.Windows.Media.Color dominant, accent;
                            ScreenAnalyzer.ExtractScreenPalette(out dominant, out accent);

                            double factor = 0.12;
                            byte bgR = (byte)(dominant.R * factor);
                            byte bgG = (byte)(dominant.G * factor);
                            byte bgB = (byte)(dominant.B * factor);

                            string bgHex = $"#F2{bgR:X2}{bgG:X2}{bgB:X2}";
                            string borderHex = $"#FF{accent.R:X2}{accent.G:X2}{accent.B:X2}";
                            string caretHex = $"#FF{accent.R:X2}{accent.G:X2}{accent.B:X2}";
                            string hoverHex = $"#1C{accent.R:X2}{accent.G:X2}{accent.B:X2}";
                            string selectedHex = $"#33{accent.R:X2}{accent.G:X2}{accent.B:X2}";
                            string selectedBorderHex = $"#80{accent.R:X2}{accent.G:X2}{accent.B:X2}";

                            byte gsR = (byte)Math.Min(255, bgR + 15);
                            byte gsG = (byte)Math.Min(255, bgG + 15);
                            byte gsB = (byte)Math.Min(255, bgB + 15);
                            string gradientStartHex = $"#F2{gsR:X2}{gsG:X2}{gsB:X2}";
                            string gradientEndHex = $"#F2{Math.Max(0, bgR - 10):X2}{Math.Max(0, bgG - 10):X2}{Math.Max(0, bgB - 10):X2}";

                            ThemeManager.SetBackgroundResource("WindowBackgroundBrush", bgHex, gradientStartHex, gradientEndHex);
                            ThemeManager.SetColorResource("WindowBorderBrush", borderHex);
                            ThemeManager.SetColorResource("AccentCaretBrush", caretHex);
                            ThemeManager.SetColorResource("HoverBackgroundBrush", hoverHex);
                            ThemeManager.SetColorResource("SelectedBackgroundBrush", selectedHex);
                            ThemeManager.SetColorResource("SelectedBorderBrush", selectedBorderHex);

                            ThemeManager.SetColorResource("TextPrimaryBrush", "#FFFFFF");
                            ThemeManager.SetColorResource("TextPlaceholderBrush", "#5AFFFFFF");
                            ThemeManager.SetColorResource("TextSecondaryBrush", "#8CFFFFFF");
                        });

                        string json = JsonSerializer.Serialize(new { status = "success", message = "Theme synced." });
                        await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes(json), "application/json");
                    }
                    else if (path.Equals("/api/ipa/download", StringComparison.OrdinalIgnoreCase))
                    {
                        string ipaPath = IpaCompilerService.LastCompiledIpaPath;
                        if (!string.IsNullOrEmpty(ipaPath) && File.Exists(ipaPath))
                        {
                            try
                            {
                                byte[] fileBytes = File.ReadAllBytes(ipaPath);
                                await SendResponseAsync(stream, 200, "OK", fileBytes, "application/octet-stream");
                            }
                            catch (Exception ex)
                            {
                                await SendResponseAsync(stream, 500, "Error", Encoding.UTF8.GetBytes($"{{\"error\":\"{ex.Message}\"}}"), "application/json");
                            }
                        }
                        else
                        {
                            await SendResponseAsync(stream, 404, "Not Found", Encoding.UTF8.GetBytes("{\"error\":\"No compiled IPA file found.\"}"), "application/json");
                        }
                    }
                    else if (path.Equals("/api/ipa/status", StringComparison.OrdinalIgnoreCase))
                    {
                        var statusPayload = new
                        {
                            status = IpaCompilerService.CompileStatus,
                            lastCompiledPath = IpaCompilerService.LastCompiledIpaPath,
                            fileName = string.IsNullOrEmpty(IpaCompilerService.LastCompiledIpaPath) ? "" : Path.GetFileName(IpaCompilerService.LastCompiledIpaPath)
                        };
                        string json = JsonSerializer.Serialize(statusPayload);
                        await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes(json), "application/json");
                    }
                    else if (path.Equals("/api/memories", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            string memoryFile = Path.Combine(InstructionsManager.InstructionsDirectory, "Memories.md");
                            if (File.Exists(memoryFile))
                            {
                                string content = File.ReadAllText(memoryFile);
                                // Return the last 2000 characters or so to avoid huge payloads
                                if (content.Length > 5000) content = content.Substring(content.Length - 5000);
                                await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes(content), "text/plain");
                            }
                            else
                            {
                                await SendResponseAsync(stream, 404, "Not Found", Encoding.UTF8.GetBytes("Memories file not found."), "text/plain");
                            }
                        }
                        catch (Exception ex)
                        {
                            await SendResponseAsync(stream, 500, "Error", Encoding.UTF8.GetBytes(ex.Message), "text/plain");
                        }
                    }
                    else if (path.Equals("/api/files/root", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!SettingsManager.Current.MobileAllowFiles)
                        {
                            await SendResponseAsync(stream, 403, "Forbidden", Encoding.UTF8.GetBytes("{\"error\":\"File access is disabled in Mobile Hub settings.\"}"), "application/json");
                            return;
                        }
                        string root = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        string json = JsonSerializer.Serialize(new { root });
                        await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes(json), "application/json");
                    }
                    else if (path.Equals("/api/files/list", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!SettingsManager.Current.MobileAllowFiles)
                        {
                            await SendResponseAsync(stream, 403, "Forbidden", Encoding.UTF8.GetBytes("[]"), "application/json");
                            return;
                        }
                        query.TryGetValue("path", out string? targetPath);
                        if (string.IsNullOrEmpty(targetPath)) targetPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                        var entries = new List<object>();
                        try
                        {
                            if (Directory.Exists(targetPath))
                            {
                                foreach (var d in Directory.GetDirectories(targetPath))
                                {
                                    var info = new DirectoryInfo(d);
                                    entries.Add(new { name = info.Name, path = info.FullName, isDirectory = true, size = 0L, modifiedUtc = info.LastWriteTimeUtc });
                                }
                                foreach (var f in Directory.GetFiles(targetPath))
                                {
                                    var info = new FileInfo(f);
                                    entries.Add(new { name = info.Name, path = info.FullName, isDirectory = false, size = info.Length, modifiedUtc = info.LastWriteTimeUtc });
                                }
                            }
                        }
                        catch { }
                        string json = JsonSerializer.Serialize(entries);
                        await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes(json), "application/json");
                    }
                    else if (path.Equals("/api/files/delete", StringComparison.OrdinalIgnoreCase) && method == "POST")
                    {
                        string body = await GetRequestBodyAsync(request, stream, contentLength);
                        try
                        {
                            using var doc = JsonDocument.Parse(body);
                            string target = doc.RootElement.GetProperty("path").GetString() ?? "";
                            if (File.Exists(target)) File.Delete(target);
                            else if (Directory.Exists(target)) Directory.Delete(target, true);
                        }
                        catch { }
                        await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes("{\"status\":\"deleted\"}"), "application/json");
                    }
                    else if (path.Equals("/api/files/upload", StringComparison.OrdinalIgnoreCase) && method == "POST")
                    {
                        query.TryGetValue("path", out string? targetDir);
                        query.TryGetValue("name", out string? fileName);
                        if (string.IsNullOrEmpty(fileName)) fileName = "uploaded_file.bin";

                        if (!string.IsNullOrEmpty(targetDir) && Directory.Exists(targetDir))
                        {
                            string fullFile = Path.Combine(targetDir, fileName);
                            int headerEnd = request.IndexOf("\r\n\r\n") + 4;
                            using (var fs = new FileStream(fullFile, FileMode.Create))
                            {
                                int initialBodyBytes = bytesRead - headerEnd;
                                if (initialBodyBytes > 0) await fs.WriteAsync(buffer, headerEnd, initialBodyBytes);

                                int totalBodyRead = initialBodyBytes;
                                while (totalBodyRead < contentLength)
                                {
                                    int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                                    if (read == 0) break;
                                    await fs.WriteAsync(buffer, 0, read);
                                    totalBodyRead += read;
                                }
                            }
                            await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes("{\"status\":\"uploaded\"}"), "application/json");
                        }
                        else
                        {
                            await SendResponseAsync(stream, 400, "Bad Request", Encoding.UTF8.GetBytes("Invalid path"), "text/plain");
                        }
                    }
                    else
                    {
                        await SendResponseAsync(stream, 200, "OK", Encoding.UTF8.GetBytes("Jarvis Bridge Active"), "text/plain");
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Handle error: {ex.Message}");
            }
        }

        private static async Task<string> GetRequestBodyAsync(string request, Stream stream, int contentLength)
        {
            int bodyStartIndex = request.IndexOf("\r\n\r\n") + 4;
            string body = request.Substring(bodyStartIndex);

            if (body.Length < contentLength)
            {
                byte[] remainingBody = new byte[contentLength - body.Length];
                int totalRead = 0;
                while (totalRead < remainingBody.Length)
                {
                    int read = await stream.ReadAsync(remainingBody, totalRead, remainingBody.Length - totalRead);
                    if (read == 0) break;
                    totalRead += read;
                }
                body += Encoding.UTF8.GetString(remainingBody, 0, totalRead);
            }
            return body;
        }

        private static async Task SendResponseAsync(Stream stream, int code, string status, byte[]? body, string contentType)
        {
            try
            {
                var headerBuilder = new StringBuilder();
                headerBuilder.Append($"HTTP/1.1 {code} {status}\r\n");
                headerBuilder.Append("Access-Control-Allow-Origin: *\r\n");
                headerBuilder.Append("Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n");
                headerBuilder.Append("Access-Control-Allow-Headers: Content-Type, X-Jarvis-Secret\r\n");
                headerBuilder.Append($"Content-Type: {contentType}\r\n");
                headerBuilder.Append($"Content-Length: {(body?.Length ?? 0)}\r\n");
                headerBuilder.Append("Connection: close\r\n");
                headerBuilder.Append("\r\n");

                byte[] headerBytes = Encoding.UTF8.GetBytes(headerBuilder.ToString());
                await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
                if (body != null) await stream.WriteAsync(body, 0, body.Length);
                await stream.FlushAsync();
            }
            catch { }
        }

        private static string? _cachedHtml;
        private static string? _cachedCmdHtml;

        private static string GetMobileCommandBarHtml()
        {
            if (_cachedCmdHtml != null) return _cachedCmdHtml;

            string[] candidates =
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HTML", "MobileCommandBar.html"),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "HTML", "MobileCommandBar.html"))
            };

            foreach (var path in candidates)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        _cachedCmdHtml = File.ReadAllText(path);
                        return _cachedCmdHtml;
                    }
                }
                catch { }
            }

            _cachedCmdHtml = "<html><body style='background:#030712;color:#fff;font-family:sans-serif;padding:20px;'>Jarvis Bridge Active, but MobileCommandBar.html was not found.</body></html>";
            return _cachedCmdHtml;
        }

        private static string GetMobileAppHtml()
        {
            if (_cachedHtml != null) return _cachedHtml;

            // Look for the source HTML alongside the running exe first, then fall back to the project folder during debugging.
            string[] candidates =
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HTML", "MobileBridgeServer.html"),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "HTML", "MobileBridgeServer.html"))
            };

            foreach (var path in candidates)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        _cachedHtml = File.ReadAllText(path);
                        return _cachedHtml;
                    }
                }
                catch { }
            }

            _cachedHtml = "<html><body style='background:#0b0f19;color:#fff;font-family:sans-serif;padding:20px;'>Jarvis Bridge Active, but MobileBridgeServer.html was not found.</body></html>";
            return _cachedHtml;
        }

        private static string RunPowerShellCommand(string command)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -Command \"{command.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return "Failed to start PowerShell.";
                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit(10000);
                return string.IsNullOrWhiteSpace(output) ? error : output;
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        private static object GetSystemStats()
        {
            try
            {
                var mem = new NativeMethods.MEMORYSTATUSEX();
                mem.dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MEMORYSTATUSEX));
                NativeMethods.GlobalMemoryStatusEx(ref mem);

                string activeWin = "Unknown";
                try {
                    var handle = NativeMethods.GetForegroundWindow();
                    NativeMethods.GetWindowThreadProcessId(handle, out uint pid);
                    var proc = Process.GetProcessById((int)pid);
                    activeWin = proc.MainWindowTitle;
                    if (string.IsNullOrEmpty(activeWin)) activeWin = proc.ProcessName;
                } catch { }

                return new
                {
                    computerName = Environment.MachineName,
                    userName = Environment.UserName,
                    memoryLoad = mem.dwMemoryLoad,
                    totalRamMb = mem.ullTotalPhys / 1024 / 1024,
                    freeRamMb = mem.ullAvailPhys / 1024 / 1024,
                    usedRamMb = (mem.ullTotalPhys - mem.ullAvailPhys) / 1024 / 1024,
                    activeWindow = activeWin,
                    serverUrl = ServerUrl,
                    localIp = GetLocalIPAddress(),
                    timestamp = DateTime.Now.ToString("HH:mm:ss")
                };
            }
            catch { return new { computerName = Environment.MachineName }; }
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        private static byte[]? CaptureScreenJpeg()
        {
            try
            {
                byte[]? result = null;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // Use low-level SystemMetrics to get raw pixel values, bypassing WPF scaling logic
                        int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
                        int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
                        int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
                        int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

                        // If virtual screen returns 0 (single monitor or error), fallback to primary monitor
                        if (width <= 0 || height <= 0)
                        {
                            width = (int)System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
                            height = (int)System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;
                            left = 0;
                            top = 0;
                        }

                        using var bmp = new Bitmap(width, height);
                        using var g = Graphics.FromImage(bmp);

                        // Captures the entire desktop spanning all monitors
                        g.CopyFromScreen(left, top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);

                        using var ms = new MemoryStream();
                        // Send as medium-quality Jpeg to balance clarity and speed
                        var encoderParams = new EncoderParameters(1);
                        encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 65L);
                        var jpegCodec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);

                        bmp.Save(ms, jpegCodec, encoderParams);
                        result = ms.ToArray();
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"Capture failure: {ex.Message}");
                    }
                });
                return result;
            }
            catch { return null; }
        }

        private static void LogToFile(string message)
        {
            try
            {
                string path = GetLogPath();
                File.AppendAllText(path, $"{DateTime.Now}: {message}\n");
                DebugConsoleOverlay.Log("Bridge", message);
            }
            catch { }
        }

        public static string GetLogPath() => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mobile_server_log.txt");

        public static string GetRecentLogs(int lines = 10)
        {
            try
            {
                string path = GetLogPath();
                if (!File.Exists(path)) return "No logs found.";
                var allLines = File.ReadAllLines(path);
                return string.Join("\n", allLines.TakeLast(lines));
            }
            catch { return "Error reading logs."; }
        }

        public static async Task FixFirewallPermissionsAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = $"advfirewall firewall add rule name=\"Jarvis Mobile Hub\" dir=in action=allow protocol=TCP localport={_port} profile=any",
                        Verb = "runas",
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };
                    var proc = Process.Start(psi);
                    proc?.WaitForExit(5000);
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
                        Arguments = $"advfirewall firewall add rule name=\"Jarvis Bridge\" dir=in action=allow protocol=TCP localport={port} profile=any",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(psi);
                }
                catch { }
            });
        }

        public static string GetLocalIPAddress()
        {
            try
            {
                var ips = GetAllLocalIPv4Addresses();
                return ips.FirstOrDefault(i => i.StartsWith("100.")) ??
                       ips.FirstOrDefault(i => i.StartsWith("192.168.")) ??
                       ips.FirstOrDefault(i => !i.StartsWith("127.")) ?? "127.0.0.1";
            }
            catch { return "127.0.0.1"; }
        }

        public static List<string> GetAllLocalIPv4Addresses()
        {
            var ips = new List<string>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    var props = ni.GetIPProperties();
                    foreach (var addr in props.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                            ips.Add(addr.Address.ToString());
                    }
                }
            }
            catch { }
            return ips.Distinct().ToList();
        }

        private static string GetNotesDir()
        {
            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Notes");
            if (!Directory.Exists(baseDir))
            {
                string devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\Data\Notes"));
                if (Directory.Exists(Path.GetDirectoryName(devPath)!))
                {
                    baseDir = devPath;
                }
                else
                {
                    Directory.CreateDirectory(baseDir);
                }
            }
            return baseDir;
        }

        public static void Stop()
        {
            _isRunning = false;
            try { _listener?.Stop(); _listener = null; } catch { }
        }
    }
}
