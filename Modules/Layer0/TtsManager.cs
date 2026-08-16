// Developer: heaplyn
// Date: 2026-08-13
// Summary: High-performance Text-to-Speech manager. Supports Windows voices and Custom Local Voice Packs.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using System.IO;

namespace JarvisLauncher
{
    public static class TtsManager
    {
        public static event Action? OnSpeechStopped;

        private static readonly SpeechSynthesizer _synthesizer = new SpeechSynthesizer();
        private static MediaPlayer? _customAudioPlayer;
        private static bool _isEnabled = true;
        public static bool IsSpeaking { get; private set; } = false;
        public static DateTime EchoCooldownUntil { get; private set; } = DateTime.MinValue;
        public static bool IsSpeakingOrEchoing => IsSpeaking || DateTime.Now < EchoCooldownUntil;

        static TtsManager()
        {
            ApplySettings();
            _synthesizer.SpeakStarted += (s, e) => IsSpeaking = true;
            _synthesizer.SpeakCompleted += (s, e) =>
            {
                IsSpeaking = false;
                EchoCooldownUntil = DateTime.Now.AddMilliseconds(25); // Faster resumption (50 -> 25)
                OnSpeechStopped?.Invoke(); // RESUME LISTENING SIGNAL
            };
        }

        public static void ApplySettings()
        {
            try
            {
                string voiceName = SettingsManager.Current.SELECTED_TTS_VOICE;
                if (!string.IsNullOrWhiteSpace(voiceName)) SetVoiceInternal(voiceName);
                else _synthesizer.SelectVoiceByHints(VoiceGender.Male, VoiceAge.Adult);
            }
            catch { }

            try
            {
                _synthesizer.Rate = Math.Clamp(SettingsManager.Current.TTS_SPEECH_RATE, -10, 10);
                _synthesizer.Volume = Math.Clamp(SettingsManager.Current.TTS_SPEECH_VOLUME, 0, 100);
            }
            catch { }
        }

        public static List<string> GetInstalledVoices()
        {
            var voices = new List<string>();
            try
            {
                foreach (InstalledVoice v in _synthesizer.GetInstalledVoices())
                {
                    if (v.Enabled && v.VoiceInfo != null) voices.Add(v.VoiceInfo.Name);
                }
            }
            catch { }
            if (voices.Count == 0)
            {
                voices.Add("Microsoft David Desktop");
                voices.Add("Microsoft Zira Desktop");
            }
            return voices;
        }

        public static bool SetVoice(string voiceName)
        {
            if (string.IsNullOrWhiteSpace(voiceName)) return false;
            bool success = SetVoiceInternal(voiceName);
            if (success)
            {
                SettingsManager.Current.SELECTED_TTS_VOICE = voiceName;
                SettingsManager.Save();
                TextOverlay.Show($"🔊 TTS Voice set to: {voiceName}", 2500);
            }
            return success;
        }

        public static void SetRate(int rate)
        {
            _synthesizer.Rate = Math.Clamp(rate, -10, 10);
            SettingsManager.Current.TTS_SPEECH_RATE = _synthesizer.Rate;
            SettingsManager.Save();
        }

        public static void SetVolume(int volume)
        {
            _synthesizer.Volume = Math.Clamp(volume, 0, 100);
            SettingsManager.Current.TTS_SPEECH_VOLUME = _synthesizer.Volume;
            SettingsManager.Save();
        }

        private static bool SetVoiceInternal(string voiceName)
        {
            try
            {
                var installed = GetInstalledVoices();
                string? match = installed.FirstOrDefault(v => v.Equals(voiceName, StringComparison.OrdinalIgnoreCase) || v.Contains(voiceName, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(match)) { _synthesizer.SelectVoice(match); return true; }
            }
            catch { }
            return false;
        }

        public static void Speak(string text, bool isShortSpeech = true)
        {
            if (!_isEnabled || string.IsNullOrWhiteSpace(text)) return;

            IsSpeaking = true;
            // Short initial buffer to prevent immediate feedback on start
            EchoCooldownUntil = DateTime.Now.AddMilliseconds(25); // 50 -> 25

            DebugConsoleOverlay.Log("TTS", $"Speaking: {text.Substring(0, Math.Min(text.Length, 60))}...");

            // CHARACTER VOICE PACK SUPPORT
            if (SettingsManager.Current.USE_CUSTOM_TTS_SOUND_FILE && !string.IsNullOrEmpty(SettingsManager.Current.CUSTOM_TTS_SAMPLE_PATH))
            {
                if (Directory.Exists(SettingsManager.Current.CUSTOM_TTS_SAMPLE_PATH))
                {
                    string match = FindLocalVoiceMatch(text, SettingsManager.Current.CUSTOM_TTS_SAMPLE_PATH);
                    if (!string.IsNullOrEmpty(match)) { PlayCustomAudio(match); return; }
                }
                else if (File.Exists(SettingsManager.Current.CUSTOM_TTS_SAMPLE_PATH))
                {
                    PlayCustomAudio(SettingsManager.Current.CUSTOM_TTS_SAMPLE_PATH);
                    if (SettingsManager.Current.CUSTOM_SOUND_ONLY) return;
                }
            }

            Task.Run(() =>
            {
                try
                {
                    _synthesizer.SpeakAsyncCancelAll();
                    string cleanText = PrepareSpeechText(text, isShortSpeech);
                    if (!string.IsNullOrWhiteSpace(cleanText))
                    {
                        ApplySettings();
                        _synthesizer.SpeakAsync(cleanText);
                    }
                }
                catch (Exception ex) { DebugConsoleOverlay.Log("TTS Error", ex.Message); }
            });
        }

        public static void SpeakFile(string filePath)
        {
            if (!File.Exists(filePath)) return;
            try
            {
                string text = File.ReadAllText(filePath);
                Speak(text, isShortSpeech: false);
            }
            catch { }
        }

        public static void PlayCustomAudio(string filePath)
        {
            if (!File.Exists(filePath)) return;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    _customAudioPlayer?.Stop();
                    _customAudioPlayer = new MediaPlayer();
                    _customAudioPlayer.Open(new Uri(filePath, UriKind.Absolute));
                    _customAudioPlayer.Volume = _synthesizer.Volume / 100.0;
                    _customAudioPlayer.Play();
                    IsSpeaking = true;
                    _customAudioPlayer.MediaEnded += (s, e) => {
                        IsSpeaking = false;
                        EchoCooldownUntil = DateTime.Now.AddMilliseconds(100); // 500 -> 100
                        OnSpeechStopped?.Invoke(); // RESUME LISTENING SIGNAL
                    };
                }
                catch { }
            });
        }

        public static void Stop()
        {
            Task.Run(() =>
            {
                try
                {
                    _synthesizer.SpeakAsyncCancelAll();
                    _customAudioPlayer?.Stop();
                    IsSpeaking = false;
                    OnSpeechStopped?.Invoke();
                }
                catch { }
            });
        }

        private static string PrepareSpeechText(string text, bool truncateLongText)
        {
            string cleaned = Regex.Replace(text, @"```[\s\S]*?```", "");
            cleaned = Regex.Replace(cleaned, @"\[[a-zA-Z0-9_]+:[\s\S]*?\]", "");
            cleaned = Regex.Replace(cleaned, @"\[[\s\S]*?\]", "");
            cleaned = Regex.Replace(cleaned, @"\[(.*?)\]\(.*?\)", "$1");
            cleaned = Regex.Replace(cleaned, @"[*_`#~]", "");
            cleaned = cleaned.Trim();

            if (truncateLongText && cleaned.Length > 220)
            {
                int periodIndex = cleaned.IndexOf('.', 140);
                if (periodIndex > 0 && periodIndex < 280) cleaned = cleaned.Substring(0, periodIndex + 1);
                else cleaned = cleaned.Substring(0, 220) + "...";
            }
            return cleaned;
        }

        private static string FindLocalVoiceMatch(string text, string datasetDir)
        {
            try
            {
                string lower = text.ToLowerInvariant().Trim();
                if (lower.Contains("yes") && lower.Length < 10) return GetRandomFile(datasetDir, "yes");
                if (lower.Contains("no") && lower.Length < 10) return GetRandomFile(datasetDir, "no");
                if (lower.Contains("ready") || lower.Contains("online")) return GetRandomFile(datasetDir, "online");
                if (lower.Contains("understand") || lower.Contains("got it")) return GetRandomFile(datasetDir, "confirm");
                if (lower.Contains("error") || lower.Contains("failed")) return GetRandomFile(datasetDir, "error");
                if (lower.Contains("hello") || lower.Contains("hi jarvis")) return GetRandomFile(datasetDir, "greeting");
            }
            catch { }
            return "";
        }

        private static string GetRandomFile(string dir, string prefix)
        {
            try
            {
                var files = Directory.GetFiles(dir, prefix + "*.*");
                if (files.Length > 0) return files[new Random().Next(files.Length)];
            }
            catch { }
            return "";
        }
    }
}
