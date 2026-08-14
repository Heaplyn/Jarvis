// Developer: heaplyn
// Date: 2026-08-13
// Summary: High-performance Text-to-Speech manager. Supports Windows installed voice selection (David, Zira, Mark, Hazel, etc.), speech rate, volume, and echo cancellation.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public static class TtsManager
    {
        private static readonly SpeechSynthesizer _synthesizer = new SpeechSynthesizer();
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
                EchoCooldownUntil = DateTime.Now.AddMilliseconds(400); // 400ms room echo suppression window
            };
        }

        /// <summary>
        /// Applies active voice, speed rate, and volume settings from SystemSettings.
        /// </summary>
        public static void ApplySettings()
        {
            try
            {
                string voiceName = SettingsManager.Current.SelectedTtsVoice;
                if (!string.IsNullOrWhiteSpace(voiceName))
                {
                    SetVoiceInternal(voiceName);
                }
                else
                {
                    _synthesizer.SelectVoiceByHints(VoiceGender.Male, VoiceAge.Adult);
                }
            }
            catch { }

            try
            {
                _synthesizer.Rate = Math.Clamp(SettingsManager.Current.TtsSpeechRate, -10, 10);
                _synthesizer.Volume = Math.Clamp(SettingsManager.Current.TtsSpeechVolume, 0, 100);
            }
            catch { }
        }

        /// <summary>
        /// Returns all Windows TTS voices installed on this PC.
        /// </summary>
        public static List<string> GetInstalledVoices()
        {
            var voices = new List<string>();
            try
            {
                foreach (InstalledVoice v in _synthesizer.GetInstalledVoices())
                {
                    if (v.Enabled && v.VoiceInfo != null)
                    {
                        voices.Add(v.VoiceInfo.Name);
                    }
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

        /// <summary>
        /// Changes the active TTS voice by name (e.g. "Microsoft Zira Desktop").
        /// </summary>
        public static bool SetVoice(string voiceName)
        {
            if (string.IsNullOrWhiteSpace(voiceName)) return false;

            bool success = SetVoiceInternal(voiceName);
            if (success)
            {
                SettingsManager.Current.SelectedTtsVoice = voiceName;
                SettingsManager.Save();
                TextOverlay.Show($"🔊 TTS Voice set to: {voiceName}", 2500);
            }
            return success;
        }

        private static bool SetVoiceInternal(string voiceName)
        {
            try
            {
                var installed = GetInstalledVoices();
                string? match = installed.FirstOrDefault(v => v.Equals(voiceName, StringComparison.OrdinalIgnoreCase) || v.Contains(voiceName, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(match))
                {
                    _synthesizer.SelectVoice(match);
                    return true;
                }
            }
            catch { }
            return false;
        }

        public static void SetRate(int rate)
        {
            _synthesizer.Rate = Math.Clamp(rate, -10, 10);
            SettingsManager.Current.TtsSpeechRate = _synthesizer.Rate;
            SettingsManager.Save();
        }

        public static void SetVolume(int volume)
        {
            _synthesizer.Volume = Math.Clamp(volume, 0, 100);
            SettingsManager.Current.TtsSpeechVolume = _synthesizer.Volume;
            SettingsManager.Save();
        }

        public static void Speak(string text, bool isShortSpeech = true)
        {
            if (!_isEnabled || string.IsNullOrWhiteSpace(text)) return;

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
                catch { }
            });
        }

        public static void Stop()
        {
            Task.Run(() =>
            {
                try { _synthesizer.SpeakAsyncCancelAll(); } catch { }
            });
        }

        public static void Toggle(bool enabled)
        {
            _isEnabled = enabled;
            if (!enabled) Stop();
        }

        private static string PrepareSpeechText(string text, bool truncateLongText)
        {
            // Remove code blocks
            string cleaned = Regex.Replace(text, @"```[\s\S]*?```", "");

            // Remove AI action tags like [WRITE_FILE: ...]
            cleaned = Regex.Replace(cleaned, @"\[[A-Z_]+:.*?\]", "");
            cleaned = Regex.Replace(cleaned, @"\[.*?\]", "");

            // Remove markdown links & formatting symbols
            cleaned = Regex.Replace(cleaned, @"\[(.*?)\]\(.*?\)", "$1");
            cleaned = Regex.Replace(cleaned, @"[*_`#~]", "");

            cleaned = cleaned.Trim();

            if (truncateLongText && cleaned.Length > 220)
            {
                // Truncate at sentence boundary to keep voice chat fast and responsive
                int periodIndex = cleaned.IndexOf('.', 140);
                if (periodIndex > 0 && periodIndex < 280)
                {
                    cleaned = cleaned.Substring(0, periodIndex + 1);
                }
                else
                {
                    cleaned = cleaned.Substring(0, 220) + "...";
                }
            }
            return cleaned;
        }
    }
}
