// Developer: heaplyn
// Date: 2026-08-13
// Summary: Model Context Protocol (MCP) Client & Server Registry Manager.
// Handles JSON-RPC STDIO & SSE transports, mcp_config.json persistence, and tool enumeration.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public class McpServerConfig
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("transport")]
        public string Transport { get; set; } = "STDIO"; // STDIO | SSE

        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;

        [JsonPropertyName("args")]
        public List<string> Args { get; set; } = new();

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("env")]
        public Dictionary<string, string> Env { get; set; } = new();

        [JsonPropertyName("enabled")]
        public bool IsEnabled { get; set; } = true;

        [JsonIgnore]
        public string Status { get; set; } = "Idle"; // Idle | Connected | Error
    }

    public class McpToolInfo
    {
        public string ServerName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public static class McpManager
    {
        private static readonly string LocalConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "mcp_config.json");
        private static readonly string UserConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini", "antigravity", "mcp_config.json");
        private static readonly object _lock = new();

        public static List<McpServerConfig> Servers { get; set; } = new();

        static McpManager()
        {
            Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data"));
            LoadConfig();
        }

        public static void LoadConfig()
        {
            lock (_lock)
            {
                Servers.Clear();
                string pathToRead = File.Exists(UserConfigPath) ? UserConfigPath : LocalConfigPath;

                if (File.Exists(pathToRead))
                {
                    try
                    {
                        string json = File.ReadAllText(pathToRead);
                        using var doc = JsonDocument.Parse(json);

                        if (doc.RootElement.TryGetProperty("mcpServers", out var mcpProp))
                        {
                            foreach (var prop in mcpProp.EnumerateObject())
                            {
                                var s = new McpServerConfig
                                {
                                    Name = prop.Name
                                };

                                if (prop.Value.TryGetProperty("command", out var cmd)) s.Command = cmd.GetString() ?? "";
                                if (prop.Value.TryGetProperty("url", out var url)) s.Url = url.GetString() ?? "";
                                if (prop.Value.TryGetProperty("transport", out var tr)) s.Transport = tr.GetString() ?? "STDIO";

                                if (prop.Value.TryGetProperty("args", out var args))
                                {
                                    foreach (var a in args.EnumerateArray()) s.Args.Add(a.GetString() ?? "");
                                }

                                if (prop.Value.TryGetProperty("env", out var envs))
                                {
                                    foreach (var e in envs.EnumerateObject()) s.Env[e.Name] = e.Value.GetString() ?? "";
                                }

                                Servers.Add(s);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugConsoleOverlay.Log("MCP Load Error", ex.Message);
                    }
                }

                // Add default Roblox & Filesystem MCP presets if empty
                if (Servers.Count == 0)
                {
                    Servers.Add(new McpServerConfig
                    {
                        Name = "Roblox_Studio",
                        Transport = "STDIO",
                        Command = "cmd.exe",
                        Args = new List<string> { "/c", "cd /d %LOCALAPPDATA%\\Roblox && .\\mcp.bat" },
                        IsEnabled = true
                    });

                    Servers.Add(new McpServerConfig
                    {
                        Name = "Filesystem",
                        Transport = "STDIO",
                        Command = "npx",
                        Args = new List<string> { "-y", "@modelcontextprotocol/server-filesystem", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) },
                        IsEnabled = true
                    });
                    SaveConfig();
                }
            }
        }

        public static void SaveConfig()
        {
            lock (_lock)
            {
                try
                {
                    var dict = new Dictionary<string, object>();
                    var serverDict = new Dictionary<string, object>();

                    foreach (var s in Servers)
                    {
                        var entry = new Dictionary<string, object>
                        {
                            ["command"] = s.Command,
                            ["args"] = s.Args,
                            ["env"] = s.Env
                        };
                        if (!string.IsNullOrEmpty(s.Url)) entry["url"] = s.Url;
                        if (!string.IsNullOrEmpty(s.Transport)) entry["transport"] = s.Transport;

                        serverDict[s.Name] = entry;
                    }

                    dict["mcpServers"] = serverDict;

                    string json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(LocalConfigPath, json);

                    // Also sync to User antigravity folder if directory exists
                    string userDir = Path.GetDirectoryName(UserConfigPath)!;
                    if (Directory.Exists(userDir))
                    {
                        File.WriteAllText(UserConfigPath, json);
                    }
                }
                catch (Exception ex)
                {
                    DebugConsoleOverlay.Log("MCP Save Error", ex.Message);
                }
            }
        }

        public static int ImportRawJsonConfig(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson)) return 0;

            lock (_lock)
            {
                int importedCount = 0;
                try
                {
                    using var doc = JsonDocument.Parse(rawJson);
                    JsonElement targetElement = doc.RootElement;

                    if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("mcpServers", out var mcpProp))
                    {
                        targetElement = mcpProp;
                    }

                    if (targetElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in targetElement.EnumerateObject())
                        {
                            string serverName = prop.Name;
                            var s = new McpServerConfig { Name = serverName };

                            if (prop.Value.TryGetProperty("command", out var cmd)) s.Command = cmd.GetString() ?? "";
                            if (prop.Value.TryGetProperty("url", out var url)) s.Url = url.GetString() ?? "";
                            if (prop.Value.TryGetProperty("transport", out var tr)) s.Transport = tr.GetString() ?? "STDIO";

                            if (prop.Value.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var a in args.EnumerateArray()) s.Args.Add(a.GetString() ?? "");
                            }

                            if (prop.Value.TryGetProperty("env", out var envs) && envs.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var e in envs.EnumerateObject()) s.Env[e.Name] = e.Value.GetString() ?? "";
                            }

                            Servers.RemoveAll(existing => existing.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase));
                            Servers.Add(s);
                            importedCount++;
                        }

                        if (importedCount > 0) SaveConfig();
                    }
                }
                catch (Exception ex)
                {
                    DebugConsoleOverlay.Log("MCP JSON Import Error", ex.Message);
                }
                return importedCount;
            }
        }

        public static async Task<bool> TestServerConnectionAsync(McpServerConfig server)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(server.Command)) return false;

                    var psi = new ProcessStartInfo
                    {
                        FileName = server.Command,
                        Arguments = string.Join(" ", server.Args),
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    foreach (var kvp in server.Env) psi.Environment[kvp.Key] = kvp.Value;

                    using var proc = Process.Start(psi);
                    if (proc == null) return false;

                    // Send MCP initialize JSON-RPC request
                    string initJson = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"JarvisLauncher\",\"version\":\"1.0.0\"}}}\n";
                    proc.StandardInput.WriteLine(initJson);
                    proc.StandardInput.Flush();

                    Task.Delay(1200).Wait();
                    if (!proc.HasExited)
                    {
                        try { proc.Kill(); } catch { }
                        server.Status = "Connected";
                        return true;
                    }

                    server.Status = proc.ExitCode == 0 ? "Connected" : "Error";
                    return proc.ExitCode == 0;
                }
                catch (Exception ex)
                {
                    server.Status = "Error";
                    DebugConsoleOverlay.Log("MCP Test Error", ex.Message);
                    return false;
                }
            });
        }

        public static void AddServer(McpServerConfig server)
        {
            lock (_lock)
            {
                Servers.RemoveAll(s => s.Name.Equals(server.Name, StringComparison.OrdinalIgnoreCase));
                Servers.Add(server);
                SaveConfig();
            }
        }

        public static void RemoveServer(string serverName)
        {
            lock (_lock)
            {
                Servers.RemoveAll(s => s.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase));
                SaveConfig();
            }
        }
    }
}
