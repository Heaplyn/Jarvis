// Developer: heaplyn
// Date: 2026-08-13
// Summary: Downloads & manages custom TTS voice samples from GitHub repository (yaph/tts-samples/mp3).

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;

namespace JarvisLauncher
{
    public class TtsVoiceItem
    {
        public string name { get; set; } = string.Empty;
        public string path { get; set; } = string.Empty;
        public string download_url { get; set; } = string.Empty;
        public int size { get; set; } = 0;
    }

    public static class TtsSampleDownloader
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        public static readonly string VoiceDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "TtsVoices");
        private static MediaPlayer? _previewPlayer;

        static TtsSampleDownloader()
        {
            if (!Directory.Exists(VoiceDirectory))
            {
                Directory.CreateDirectory(VoiceDirectory);
            }
        }

        /// <summary>
        /// Fetches the list of all TTS voice MP3 samples from yaph/tts-samples repo via GitHub API.
        /// </summary>
        public static async Task<List<TtsVoiceItem>> FetchVoiceSamplesAsync()
        {
            var voices = new List<TtsVoiceItem>();
            try
            {
                string url = "https://api.github.com/repos/yaph/tts-samples/contents/mp3";
                _http.DefaultRequestHeaders.UserAgent.Clear();
                _http.DefaultRequestHeaders.UserAgent.ParseAdd("JarvisLauncher/1.0");

                string json = await _http.GetStringAsync(url);
                var items = JsonSerializer.Deserialize<List<TtsVoiceItem>>(json);
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        if (item.name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                        {
                            voices.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching TTS voice samples: {ex.Message}");
            }
            return voices;
        }

        /// <summary>
        /// Downloads a specific MP3 voice sample to local Data/TtsVoices directory.
        /// </summary>
        public static async Task<string> DownloadVoiceSampleAsync(TtsVoiceItem voice)
        {
            string localPath = Path.Combine(VoiceDirectory, voice.name);
            if (File.Exists(localPath)) return localPath;

            try
            {
                byte[] data = await _http.GetByteArrayAsync(voice.download_url);
                await File.WriteAllBytesAsync(localPath, data);
                return localPath;
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Download failed: {ex.Message}", 3000);
                return string.Empty;
            }
        }

        /// <summary>
        /// Previews / plays the MP3 voice sample directly in WPF.
        /// </summary>
        public static async Task PreviewVoiceSampleAsync(TtsVoiceItem voice)
        {
            try
            {
                string localPath = await DownloadVoiceSampleAsync(voice);
                if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath)) return;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _previewPlayer?.Stop();
                    _previewPlayer = new MediaPlayer();
                    _previewPlayer.Open(new Uri(localPath, UriKind.Absolute));
                    _previewPlayer.Play();
                });
                TextOverlay.Show($"🔊 Playing Voice Sample: {voice.name}", 2500);
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Playback error: {ex.Message}", 3000);
            }
        }

        /// <summary>
        /// Sets the downloaded sample as the active custom voice sample for Jarvis TTS notifications.
        /// </summary>
        public static async Task SetCustomVoiceSampleAsync(TtsVoiceItem voice)
        {
            string localPath = await DownloadVoiceSampleAsync(voice);
            if (File.Exists(localPath))
            {
                SettingsManager.Current.CustomTtsSamplePath = localPath;
                SettingsManager.Current.CustomTtsVoiceName = voice.name;
                SettingsManager.Save();
                TextOverlay.Show($"✅ Custom Voice set to: {voice.name}", 3000);
            }
        }
    }
}
