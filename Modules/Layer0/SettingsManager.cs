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
        public int WindowMargin { get; set; } = 10;
        public bool UseGradientBackground { get; set; } = true;
        public string BackgroundMode { get; set; } = "Gradient"; // Solid | Gradient | Media
        public string BackgroundMediaSource { get; set; } = string.Empty; // Path to GIF or Video
        public bool EnableAnimations { get; set; } = true;
        public bool UseRoundedCorners { get; set; } = true;
        public string CustomFontFamily { get; set; } = "Segoe UI";
        public bool IsJarvisEnabled { get; set; } = true;
        public bool IsVoiceModeActive { get; set; } = true;
        public bool EnableWindowsAppIndexing { get; set; } = true;
        public int MaxSearchSuggestions { get; set; } = 10;
        public bool AutoFocusSearchOnLaunch { get; set; } = true;

        // Voice & Assistant Fine-Tuning Controls
        public bool EnableVoiceCommandChunking { get; set; } = true;
        public int VoiceChunkingSilenceMs { get; set; } = 6000; // 6 seconds silence pause before processing voice
        public double MinVoiceConfidence { get; set; } = 0.75; // 75% strict confidence threshold (range: 0.30 - 0.98)
        public float MicAudioEnergyFloor { get; set; } = 0.12f; // 12% audio volume energy floor required
        public double MicNoiseGateDb { get; set; } = -35.0; // -35 dB noise gate floor threshold
        public int TtsSpeechRate { get; set; } = 0;
        public int TtsSpeechVolume { get; set; } = 100;
        public string SelectedTtsVoice { get; set; } = string.Empty;
        public string CustomTtsSamplePath { get; set; } = string.Empty;
        public string CustomTtsVoiceName { get; set; } = string.Empty;
        public string GeminiVoiceDetailLevel { get; set; } = "Concise"; // Concise | Detailed | Bullet Points
        public bool PhoneticFuzzyMatching { get; set; } = true;

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

        // Dual LLM Co-Pilot Query Processor (Optional - Default Disabled)
        public bool EnableDualLlmCopilot { get; set; } = false;
        public string DualLlmBackend { get; set; } = "Ollama";
        public string DualLlmModel { get; set; } = "deepseek-r1:7b";

        // OAuth2 Credentials & Account Tokens
        public string GoogleOAuthClientId { get; set; } = string.Empty;
        public string GoogleOAuthClientSecret { get; set; } = string.Empty;
        public string GoogleOAuthAccessToken { get; set; } = string.Empty;
        public string GoogleOAuthRefreshToken { get; set; } = string.Empty;
        public string GoogleOAuthUserEmail { get; set; } = string.Empty;

        public string GithubOAuthClientId { get; set; } = string.Empty;
        public string GithubOAuthClientSecret { get; set; } = string.Empty;
        public string GithubOAuthUserLogin { get; set; } = string.Empty;

        // P2P Compute Node Settings
        public bool P2PServerEnabled { get; set; } = false;
        public string P2PServerSecret { get; set; } = string.Empty;

        // Mobile Companion & Tunnel Settings
        public int MobilePort { get; set; } = 9000;
        public string MobilePreferredTunnel { get; set; } = "None"; // None | Cloudflare | Ngrok
        public bool MobileAutoStartTunnel { get; set; } = false;
        public bool MobileAllowTerminal { get; set; } = true;
        public bool MobileAllowFiles { get; set; } = true;
        public bool MobileAllowScreenMirror { get; set; } = true;
        public bool MobileAllowClipboard { get; set; } = true;

        // Discord Bot API (official Bot token — used for legitimate, ToS-compliant server reading)
        public string DiscordBotToken { get; set; } = string.Empty;
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
