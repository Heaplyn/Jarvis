// Developer: heaplyn
// Date: 2026-08-13
// Summary: Command handler for Voice AI Studio, Offline Pre-Caching Studio, GitHub Custom TTS Voice Library, & Vosk model downloader.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public class VoiceStudioCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.ToLower().Trim();
            return query == "voice" || query == "voicestudio" || query == "voicetrainer" ||
                   query == "record" || query == "audiorecorder" || query == "voicememo" ||
                   query == "speechcalibrate" || query == "voicetrain" || query == "speechtraining" ||
                   query == "downloadvosk" || query == "downloadmodel" || query == "voskmodel" ||
                   query == "ttsvoices" || query == "customvoice" || query == "ttssamples" || query == "ttsvoice" ||
                   query == "offline" || query == "offlinemode" || query == "precache" || query == "cacheoffline" ||
                   query == "disable voice" || query == "enable voice" || query == "voicemode off" || query == "voicemode on" || query == "toggle voice" ||
                   query == "voice dataset" || query == "voice classification" || query == "classify voice" || query == "teleprompter" ||
                   query.StartsWith("voice ") || query.StartsWith("record ");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var results = new List<CommandResult>();
            string lower = query.ToLower().Trim();

            // Voice Mode Toggle Commands
            if (lower == "disable voice" || lower == "voicemode off" || lower == "stop voice")
            {
                results.Add(new CommandResult
                {
                    Title = "🔇 Disable Master Voice Mode",
                    Description = "Stops all microphone speech recognition and background wake word listening",
                    Similarity = 6.0,
                    Execute = () =>
                    {
                        SettingsManager.Current.IsVoiceModeActive = false;
                        SettingsManager.Save();
                        LocalWakeWordDetector.Stop();
                        TextOverlay.Show("🔇 Master Voice Mode DISABLED", 3000);
                    }
                });
                return results;
            }

            if (lower == "enable voice" || lower == "voicemode on" || lower == "start voice")
            {
                results.Add(new CommandResult
                {
                    Title = "🎙️ Enable Master Voice Mode",
                    Description = "Starts microphone speech recognition and continuous wake word listening",
                    Similarity = 6.0,
                    Execute = () =>
                    {
                        SettingsManager.Current.IsVoiceModeActive = true;
                        SettingsManager.Save();
                        LocalWakeWordDetector.Initialize();
                        TextOverlay.Show("🎙️ Master Voice Mode ENABLED", 3000);
                    }
                });
                return results;
            }

            if (lower.Contains("dataset") || lower.Contains("classification") || lower.Contains("classify"))
            {
                results.Add(new CommandResult
                {
                    Title = "🏷️ Open Voice Dataset & Classification Studio",
                    Description = "View, play, tag (Command, AI Chat, Wake Word, Noise), & train acoustic voice dataset",
                    Similarity = 6.0,
                    Execute = () => VoiceStudioOverlay.ShowOverlay()
                });
                return results;
            }

            if (lower == "offline" || lower == "offlinemode" || lower == "precache" || lower == "cacheoffline")
            {
                results.Add(new CommandResult
                {
                    Title = "📶 Open Offline Mode & Wi-Fi Pre-Caching Studio",
                    Description = "Pre-download speech models, TTS voices, & local LLM models for 100% offline functionality",
                    Similarity = 6.0,
                    Execute = () => OfflineStudioOverlay.ShowOverlay()
                });
                return results;
            }

            if (lower == "ttsvoices" || lower == "customvoice" || lower == "ttssamples" || lower == "ttsvoice")
            {
                results.Add(new CommandResult
                {
                    Title = "🌐 Open GitHub Custom TTS Voice Library (yaph/tts-samples)",
                    Description = "Browse, preview, & set custom TTS voice MP3 samples directly from GitHub",
                    Similarity = 6.0,
                    Execute = () => TtsVoiceLibraryOverlay.ShowOverlay()
                });
                return results;
            }

            if (lower == "downloadvosk" || lower == "downloadmodel" || lower == "voskmodel")
            {
                results.Add(new CommandResult
                {
                    Title = "📥 Download Official Vosk Neural Speech Model (~40MB)",
                    Description = "Auto-downloads and installs full offline neural speech recognition model for 99%+ accuracy",
                    Similarity = 6.0,
                    Execute = () => Task.Run(async () => await VoskEngine.EnsureModelDownloadedAsync(showToast: true))
                });
                return results;
            }

            results.Add(new CommandResult
            {
                Title = "📶 Open Offline Mode & Pre-Caching Studio",
                Description = "Pre-cache speech, TTS, & local LLM features for 100% offline usage",
                Similarity = 5.6,
                Execute = () => OfflineStudioOverlay.ShowOverlay()
            });

            results.Add(new CommandResult
            {
                Title = "🎙️ Open Voice AI Studio & Audio Recorder",
                Description = "Train AI voice profiles, record audio memos, calibrate speech sensitivity, & map voice shortcuts",
                Similarity = 5.5,
                Execute = () => VoiceStudioOverlay.ShowOverlay()
            });

            return results;
        }
    }
}
