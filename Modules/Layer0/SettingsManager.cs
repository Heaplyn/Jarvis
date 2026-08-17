// Developer: heaplyn
// Date: 2026-08-17
// Summary: Manages loading and saving configuration values in SystemSettings.json.

using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class SystemSettings
    {
        public string GOOGLE_AI_KEY { get; set; } = string.Empty;
        public string GITHUB_TOKEN { get; set; } = string.Empty;
        public string DOWNLOAD_DIRECTORY { get; set; } = string.Empty;
        public string THEME { get; set; } = "purple";
        public bool START_WITH_WINDOWS { get; set; } = false;
        public bool PLAY_SOUNDS { get; set; } = true;
        public bool AUTO_HIDE_ON_EXECUTE { get; set; } = true;
        public bool ALWAYS_ON_TOP { get; set; } = true;
        public double WINDOW_OPACITY { get; set; } = 1.0;
        public string DEFAULT_SEARCH_ENGINE { get; set; } = "Google";
        public int WINDOW_MARGIN { get; set; } = 10;
        public bool USE_GRADIENT_BACKGROUND { get; set; } = true;
        public string BACKGROUND_MODE { get; set; } = "Gradient";
        public string BACKGROUND_MEDIA_SOURCE { get; set; } = string.Empty;
        public bool ENABLE_ANIMATIONS { get; set; } = true;
        public bool USE_ROUNDED_CORNERS { get; set; } = true;
        public string CUSTOM_FONT_FAMILY { get; set; } = "Segoe UI";
        public bool IS_JARVIS_ENABLED { get; set; } = true;
        public bool IS_VOICE_MODE_ACTIVE { get; set; } = true;
        public bool ENABLE_WINDOWS_APP_INDEXING { get; set; } = true;
        public int MAX_SEARCH_SUGGESTIONS { get; set; } = 10;
        public bool AUTO_FOCUS_SEARCH_ON_LAUNCH { get; set; } = true;
        public bool IS_SPEAKER_VERIFICATION_ENABLED { get; set; } = false;
        public string ENROLLED_SPEAKER_NAME { get; set; } = "Kyle";
        public double SPEAKER_VERIFICATION_THRESHOLD { get; set; } = 0.50;
        public bool IS_TEACHER_MODE_ENABLED { get; set; } = true;
        public bool IS_AUTONOMOUS_MODE_ENABLED { get; set; } = true;
        public int AUTONOMOUS_INTERVAL_MINUTES { get; set; } = 2;

        public bool ENABLE_VOICE_COMMAND_CHUNKING { get; set; } = true;
        public int VOICE_CHUNKING_SILENCE_MS { get; set; } = 1200;
        public double MIN_VOICE_CONFIDENCE { get; set; } = 0.65;
        public float MIC_AUDIO_ENERGY_FLOOR { get; set; } = 0.12f;
        public double MIC_NOISE_GATE_DB { get; set; } = -35.0;
        public int TTS_SPEECH_RATE { get; set; } = 0;
        public int TTS_SPEECH_VOLUME { get; set; } = 100;
        public string SELECTED_TTS_VOICE { get; set; } = string.Empty;
        public string CUSTOM_TTS_SAMPLE_PATH { get; set; } = string.Empty;
        public string CUSTOM_TTS_VOICE_NAME { get; set; } = string.Empty;
        public bool USE_CUSTOM_TTS_SOUND_FILE { get; set; } = false;
        public bool CUSTOM_SOUND_ONLY { get; set; } = false;
        public string GEMINI_VOICE_DETAIL_LEVEL { get; set; } = "Concise";
        public bool PHONETIC_FUZZY_MATCHING { get; set; } = true;

        public Dictionary<string, string> ALIASES { get; set; } = new Dictionary<string, string>();

        public string LLM_BACKEND { get; set; } = "Gemini";
        public string GEMINI_MODEL { get; set; } = "gemini-2.0-flash";
        public string OPENAI_KEY { get; set; } = string.Empty;
        public string OPENAI_BASE_URL { get; set; } = "https://api.openai.com/v1";
        public string OPENAI_MODEL { get; set; } = "gpt-4o-mini";
        public string OLLAMA_ENDPOINT { get; set; } = "http://localhost:11434";
        public string OLLAMA_MODEL { get; set; } = "llama3";
        public string CUSTOM_LLM_ENDPOINT { get; set; } = string.Empty;
        public string CUSTOM_LLM_KEY { get; set; } = string.Empty;
        public string CUSTOM_LLM_MODEL { get; set; } = string.Empty;

        public string ANTHROPIC_KEY { get; set; } = string.Empty;
        public string ANTHROPIC_MODEL { get; set; } = "claude-3-5-sonnet-20240620";
        public string GROQ_KEY { get; set; } = string.Empty;
        public string GROQ_MODEL { get; set; } = "llama-3.3-70b-versatile";
        public string PERPLEXITY_KEY { get; set; } = string.Empty;
        public string PERPLEXITY_MODEL { get; set; } = "llama-3-sonar-large-32k-online";
        public string MISTRAL_KEY { get; set; } = string.Empty;
        public string MISTRAL_MODEL { get; set; } = "mistral-large-latest";
        public string OPENROUTER_KEY { get; set; } = string.Empty;
        public string OPENROUTER_MODEL { get; set; } = "anthropic/claude-3.5-sonnet";
        public string CUSTOM_DATA_PROCESSOR_PATH { get; set; } = string.Empty;
        public bool ENABLE_CUSTOM_PROCESSOR { get; set; } = false;

        public bool ENABLE_DUAL_LLM_COPILOT { get; set; } = false;
        public string DUAL_LLM_BACKEND { get; set; } = "Ollama";
        public string DUAL_LLM_MODEL { get; set; } = "deepseek-r1:7b";

        // Editor Settings
        public bool EDITOR_SHOW_LINE_NUMBERS { get; set; } = true;
        public bool EDITOR_ENABLE_AI_AUTOCOMPLETE { get; set; } = true;
        public bool EDITOR_AUTO_SAVE_ON_CLOSE { get; set; } = false;
        public string EDITOR_FONT_FAMILY { get; set; } = "Consolas";
        public double EDITOR_FONT_SIZE { get; set; } = 13.0;

        public string GOOGLE_OAUTH_CLIENT_ID { get; set; } = string.Empty;
        public string GOOGLE_OAUTH_CLIENT_SECRET { get; set; } = string.Empty;
        public string GOOGLE_OAUTH_ACCESS_TOKEN { get; set; } = string.Empty;
        public string GOOGLE_OAUTH_REFRESH_TOKEN { get; set; } = string.Empty;
        public string GOOGLE_OAUTH_USER_EMAIL { get; set; } = string.Empty;

        public string GITHUB_OAUTH_CLIENT_ID { get; set; } = string.Empty;
        public string GITHUB_OAUTH_CLIENT_SECRET { get; set; } = string.Empty;
        public string GITHUB_OAUTH_USER_LOGIN { get; set; } = string.Empty;

        public string DISCORD_OAUTH_CLIENT_ID { get; set; } = string.Empty;
        public string DISCORD_OAUTH_CLIENT_SECRET { get; set; } = string.Empty;
        public string DISCORD_OAUTH_ACCESS_TOKEN { get; set; } = string.Empty;
        public string DISCORD_OAUTH_REFRESH_TOKEN { get; set; } = string.Empty;
        public string DISCORD_USER_TAG { get; set; } = string.Empty;

        public string SPOTIFY_OAUTH_CLIENT_ID { get; set; } = string.Empty;
        public string SPOTIFY_OAUTH_CLIENT_SECRET { get; set; } = string.Empty;
        public string SPOTIFY_OAUTH_ACCESS_TOKEN { get; set; } = string.Empty;
        public string SPOTIFY_OAUTH_REFRESH_TOKEN { get; set; } = string.Empty;
        public string SPOTIFY_USER_NAME { get; set; } = string.Empty;

        public string TWITCH_OAUTH_CLIENT_ID { get; set; } = string.Empty;
        public string TWITCH_OAUTH_CLIENT_SECRET { get; set; } = string.Empty;
        public string TWITCH_OAUTH_ACCESS_TOKEN { get; set; } = string.Empty;
        public string TWITCH_USER_NAME { get; set; } = string.Empty;

        public bool P2P_SERVER_ENABLED { get; set; } = false;
        public string P2P_SERVER_SECRET { get; set; } = string.Empty;

        public int MOBILE_PORT { get; set; } = 9000;
        public string MOBILE_PREFERRED_TUNNEL { get; set; } = "None";
        public bool MOBILE_AUTO_START_TUNNEL { get; set; } = false;
        public bool MOBILE_ALLOW_TERMINAL { get; set; } = true;
        public bool MOBILE_ALLOW_FILES { get; set; } = true;
        public bool MOBILE_ALLOW_SCREEN_MIRROR { get; set; } = true;
        public bool MOBILE_ALLOW_CLIPBOARD { get; set; } = true;

        public string DISCORD_BOT_TOKEN { get; set; } = string.Empty;
        public string ROBLOX_COOKIE { get; set; } = string.Empty;
        public string HUGGINGFACE_API_KEY { get; set; } = string.Empty;
        public bool ENABLE_HF_AUTO_TRAINING { get; set; } = false;
        public string HF_TRAINING_DATASET_ID { get; set; } = string.Empty;
        public bool VERBOSE_LOGGING { get; set; } = false;
        public int DEBUG_VERBOSITY_LEVEL { get; set; } = 1;
        public bool ENABLE_PC_CONTROL { get; set; } = true;
        public string OBSIDIAN_VAULT_PATH { get; set; } = string.Empty;
        public bool MINIMIZE_TO_WIDGET { get; set; } = true;
    }

    public class SettingsManager : ISettingsService
    {
        private static string DataDir => PathHandler.GetDataDirectory();
        private static string SettingsPath => Path.Combine(DataDir, "SystemSettings.json");
        private SystemSettings? _cached;

        public static SystemSettings Current => CoreRegistry.Settings.Current;

        SystemSettings ISettingsService.Current => _cached ??= LoadInternal();

        void ISettingsService.Load() => _cached = LoadInternal();

        private SystemSettings LoadInternal()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<SystemSettings>(json) ?? new SystemSettings();
                }
            }
            catch { }
            return new SystemSettings();
        }

        void ISettingsService.Save()
        {
            try
            {
                if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
                string json = JsonSerializer.Serialize(_cached ?? new SystemSettings(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }

        public static void Load() => CoreRegistry.Settings.Load();
        public static void Save() => CoreRegistry.Settings.Save();
    }
}
