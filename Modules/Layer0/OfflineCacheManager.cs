// Developer: heaplyn
// Date: 2026-08-13
// Summary: Manages offline caching, Wi-Fi pre-downloading, and offline fallback routing for speech, LLM, and TTS features.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public static class OfflineCacheManager
    {
        public static readonly string OfflineDataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "OfflineCache");

        static OfflineCacheManager()
        {
            if (!Directory.Exists(OfflineDataDirectory))
            {
                Directory.CreateDirectory(OfflineDataDirectory);
            }
        }

        /// <summary>
        /// Checks whether active internet / Wi-Fi connection is available.
        /// </summary>
        public static bool IsInternetAvailable()
        {
            try
            {
                using var ping = new Ping();
                var reply = ping.Send("8.8.8.8", 1500);
                return reply != null && reply.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks whether Gemini AI can be queried (Internet available + API key configured).
        /// </summary>
        public static bool CanUseGemini()
        {
            string key = SettingsManager.Current.GoogleAIKey;
            if (string.IsNullOrWhiteSpace(key)) return false;
            return IsInternetAvailable();
        }

        /// <summary>
        /// Pre-caches all online resources (Vosk speech model, GitHub TTS voice samples) for 100% offline usage.
        /// </summary>
        public static async Task PreCacheAllForOfflineAsync(Action<string>? statusCallback = null)
        {
            statusCallback?.Invoke("📡 Checking internet connection...");
            if (!IsInternetAvailable())
            {
                statusCallback?.Invoke("⚠️ Internet disconnected. Pre-caching requires Wi-Fi / active internet connection.");
                return;
            }

            // 1. Download Offline Vosk Speech Model
            statusCallback?.Invoke("🎙️ Pre-caching Vosk Offline Neural Speech Model (~40MB)...");
            await VoskEngine.EnsureModelDownloadedAsync(showToast: false);

            // 2. Pre-cache all GitHub Custom TTS Voice Samples
            statusCallback?.Invoke("🎵 Pre-caching GitHub Custom TTS Voice Samples...");
            var voices = await TtsSampleDownloader.FetchVoiceSamplesAsync();
            int downloadedVoices = 0;
            foreach (var voice in voices)
            {
                string path = await TtsSampleDownloader.DownloadVoiceSampleAsync(voice);
                if (!string.IsNullOrEmpty(path)) downloadedVoices++;
            }

            statusCallback?.Invoke($"✅ Pre-cache complete! Cached Vosk speech model & {downloadedVoices} TTS voices for 100% offline usage.");
            TextOverlay.Show("📶 Jarvis is now 100% Ready For Offline Use!", 3500);
        }

        /// <summary>
        /// Parses spoken queries locally when internet is offline.
        /// </summary>
        public static string OfflineIntentFallback(string query)
        {
            string lower = query.ToLower().Trim();

            if (lower.Contains("time") || lower.Contains("clock"))
                return $"The current time is {DateTime.Now:h:mm tt}.";
            if (lower.Contains("date") || lower.Contains("day"))
                return $"Today is {DateTime.Now:dddd, MMMM d, yyyy}.";
            if (lower.Contains("battery") || lower.Contains("power"))
                return "Checking system battery status locally...";
            if (lower.Contains("restart") || lower.Contains("reboot"))
                return "Restarting system requested. Awaiting user confirmation.";
            if (lower.Contains("shutdown") || lower.Contains("power off"))
                return "Shutdown requested. Awaiting user confirmation.";

            return $"Offline Mode Active: Standard desktop system handler ready for '{query}'.";
        }
    }
}
