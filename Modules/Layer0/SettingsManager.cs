// Developer: heaplyn
// Date: 2026-08-09
// Summary: Manages loading and saving configuration values (API keys) in SystemSettings.json.

using System;
using System.IO;
using System.Text.Json;

namespace JarvisLauncher
{
    public class SystemSettings
    {
        public string GoogleAIKey { get; set; } = string.Empty;
        public string GithubToken { get; set; } = string.Empty;
        public string DownloadDirectory { get; set; } = string.Empty;
        public string Theme { get; set; } = "purple";
        public bool StartWithWindows { get; set; } = false;
        public bool PlaySounds { get; set; } = true;
        public bool AutoHideOnExecute { get; set; } = true;
        public bool AlwaysOnTop { get; set; } = true;
        public double WindowOpacity { get; set; } = 1.0;
        public string DefaultSearchEngine { get; set; } = "Google";
        public System.Collections.Generic.Dictionary<string, string> Aliases { get; set; } = new System.Collections.Generic.Dictionary<string, string>();

        // LLM Backend Selection
        public string LlmBackend { get; set; } = "Gemini"; // Gemini | OpenAI | Ollama | Custom | P2P
        public string OpenAIKey { get; set; } = string.Empty;
        public string OpenAIBaseUrl { get; set; } = "https://api.openai.com/v1";
        public string OpenAIModel { get; set; } = "gpt-4o-mini";
        public string OllamaEndpoint { get; set; } = "http://localhost:11434";
        public string OllamaModel { get; set; } = "llama3";
        public string CustomLlmEndpoint { get; set; } = string.Empty;
        public string CustomLlmKey { get; set; } = string.Empty;
        public string CustomLlmModel { get; set; } = string.Empty;

        // P2P Compute Node Settings
        public bool P2PServerEnabled { get; set; } = false;
        public string P2PServerSecret { get; set; } = string.Empty;
    }

    public static class SettingsManager
    {
        private static string DataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        private static string SettingsPath = Path.Combine(DataDir, "SystemSettings.json");
        private static SystemSettings _currentSettings = new SystemSettings();

        static SettingsManager()
        {
            // Dynamically locate the source project folder 'Data' directory if developing
            string checkDir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 5; i++)
            {
                string dataFolder = Path.Combine(checkDir, "Data");
                if (Directory.Exists(dataFolder) && File.Exists(Path.Combine(dataFolder, "SystemSettings.json")))
                {
                    DataDir = dataFolder;
                    SettingsPath = Path.Combine(dataFolder, "SystemSettings.json");
                    break;
                }
                var parent = Directory.GetParent(checkDir);
                if (parent == null) break;
                checkDir = parent.FullName;
            }
            Load();
        }

        public static SystemSettings Current => _currentSettings;

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    _currentSettings = JsonSerializer.Deserialize<SystemSettings>(json) ?? new SystemSettings();
                }
                else
                {
                    _currentSettings = new SystemSettings();
                    Save(); // Initialize empty file
                }
            }
            catch
            {
                _currentSettings = new SystemSettings();
            }
        }

        public static void Save()
        {
            try
            {
                if (!Directory.Exists(DataDir))
                {
                    Directory.CreateDirectory(DataDir);
                }
                string json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }
    }
}
