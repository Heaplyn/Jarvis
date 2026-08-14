using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NAudio.Wave;
using System.Windows;
using System.Collections.Generic;
using System.Speech.Recognition;
using System.Threading;

namespace JarvisLauncher
{
    public static class VoiceActivationManager
    {
        private static WaveInEvent? _waveIn;
        private static MemoryStream _commandAudioStream = new MemoryStream();
        private static bool _isRecordingCommand = false;
        private static bool _isProcessingWakeWord = false;
        private static SpeechRecognitionEngine? _wakeWordEngine;
        private static readonly string TrainingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "VoiceTraining.json");
        private static List<string> _learnedPhrases = new List<string>();

        // Settings
        private static int ConversationTimeoutSeconds = 60;
        private static readonly List<byte> _circularBuffer = new List<byte>();
        private const int BufferSeconds = 3;
        private static float SpeechThreshold = 0.065f; // Raised to ignore ambient background noise
        private static int SilenceTimeoutMs = 800; // Reduced from 1500 for snappier response

        private static DateTime _lastInteractionTime = DateTime.MinValue;
        private static bool _isInConversation = false;
        private static DateTime _lastSpeechTime = DateTime.MinValue;
        private static DateTime _cooldownUntil = DateTime.MinValue;
        private static DateTime _lastFallbackTime = DateTime.MinValue;

        // Syllable/Algorithm tracking
        private static List<double> _rmsHistory = new List<double>();
        private const int HistorySize = 20; // ~200ms of audio history

        public static void Start()
        {
            try
            {
                // 1. Setup ML Engine
                var culture = System.Globalization.CultureInfo.CurrentCulture;
                if (culture.TwoLetterISOLanguageName != "en") culture = new System.Globalization.CultureInfo("en-US");

                try
                {
                    _wakeWordEngine = new SpeechRecognitionEngine(culture);
                    LoadLearnedPhrases();

                    Choices commands = new Choices();
                    commands.Add(new string[] { "Jarvis", "Hey Jarvis", "Wake up Jarvis", "Hello Jarvis", "Computer" });
                    if (_learnedPhrases.Count > 0) commands.Add(_learnedPhrases.ToArray());

                    GrammarBuilder gb = new GrammarBuilder();
                    gb.Append(commands);
                    _wakeWordEngine.LoadGrammar(new Grammar(gb));

                    _wakeWordEngine.SpeechRecognized += (s, e) => {
                        if (e.Result.Confidence > 0.7) {
                            DebugConsoleOverlay.Log("Voice-ML", $"High Confidence Match: '{e.Result.Text}' ({e.Result.Confidence:P0})");
                            TriggerJarvis("ML-High");
                        }
                    };

                    _wakeWordEngine.SetInputToDefaultAudioDevice();
                    _wakeWordEngine.RecognizeAsync(RecognizeMode.Multiple);
                }
                catch { _wakeWordEngine = null; }

                // 2. Setup NAudio for Algorithm & Capture
                _waveIn = new WaveInEvent { DeviceNumber = 0, WaveFormat = new WaveFormat(16000, 1) };
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.StartRecording();

                DebugConsoleOverlay.Log("Voice", "Tiered Activation Pipeline Started (Algorithm -> ML -> Gemini)");

                Task.Run(async () => {
                    while (true) {
                        if (_isInConversation && (DateTime.Now - _lastInteractionTime).TotalSeconds > ConversationTimeoutSeconds) {
                            _isInConversation = false;
                            // Reset state silently
                            DebugConsoleOverlay.Log("Voice", "Conversation timed out (Standby).");
                        }
                        await Task.Delay(5000);
                    }
                });
            }
            catch (Exception ex) { DebugConsoleOverlay.Log("Voice Error", ex.Message); }
        }

        private static void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (!SettingsManager.Current.IsJarvisEnabled) return;

            // Acoustic Echo Suppression: Ignore mic input while TTS speaker output is playing or decaying
            if (TtsManager.IsSpeakingOrEchoing) return;

            double rms = CalculateRMS(e.Buffer, e.BytesRecorded);

            if (DateTime.Now < _cooldownUntil) return;

            // Pass real-time microphone PCM audio buffer directly to Vosk Neural Speech Engine
            if (VoskEngine.IsInitialized)
            {
                VoskEngine.ProcessAudioBuffer(e.Buffer, e.BytesRecorded);
            }

            if (_isRecordingCommand)
            {
                _commandAudioStream.Write(e.Buffer, 0, e.BytesRecorded);
                if (rms > SpeechThreshold) _lastSpeechTime = DateTime.Now;
                return;
            }

            // Maintain circular buffer for Gemini Fallback
            lock (_circularBuffer)
            {
                _circularBuffer.AddRange(e.Buffer.Take(e.BytesRecorded));
                int maxSize = 16000 * 2 * BufferSeconds;
                if (_circularBuffer.Count > maxSize) _circularBuffer.RemoveRange(0, _circularBuffer.Count - maxSize);
            }

            if (_isInConversation || _isProcessingWakeWord) return;

            // --- TIER 1: THE ALGORITHM (Instant, local energy pattern) ---
            _rmsHistory.Add(rms);
            if (_rmsHistory.Count > HistorySize) _rmsHistory.RemoveAt(0);

            // Look for a specific "Jar-vis" energy pattern (two spikes separated by a small dip)
            if (DetectSyllablePattern())
            {
                DebugConsoleOverlay.Log("Voice-Algo", "Pattern Match Detected! Syllable cadence looks like 'Jarvis'.");
                TriggerJarvis("Algorithm");
                return;
            }

            // --- TIER 2: ML ENGINE ---
            // (Handled by SpeechRecognized event above, which runs in parallel)

            // --- TIER 3: GEMINI FALLBACK (Only if Tier 1/2 missed it and sound is significant) ---
            if (rms > 0.08 && (DateTime.Now - _lastFallbackTime).TotalSeconds > 5)
            {
                _lastFallbackTime = DateTime.Now;
                _isProcessingWakeWord = true;
                DebugConsoleOverlay.Log("Voice-Triage", $"Significant sustained sound (RMS: {rms:F3}). Asking Gemini...");
                Task.Run(async () => await VerifyWithAi());
            }
        }

        private static bool DetectSyllablePattern()
        {
            if (_rmsHistory.Count < HistorySize) return false;

            int peaks = 0;
            bool inPeak = false;
            foreach(var vol in _rmsHistory)
            {
                if (vol > 0.05) { if (!inPeak) { peaks++; inPeak = true; } } // Lowered from 0.08
                else if (vol < 0.02) { inPeak = false; } // Lowered from 0.03
            }

            return peaks >= 2;
        }

        private static async Task VerifyWithAi()
        {
            try
            {
                byte[] clip;
                lock (_circularBuffer) clip = _circularBuffer.ToArray();

                string base64 = ConvertToBase64Wav(clip);
                string prompt = "Is the name 'Jarvis' clearly spoken in this audio? Answer ONLY 'YES' or 'NO'.";
                string response = await AiAPI.AnalyzeAudioAsync(prompt, base64);

                if (response.Trim().ToUpper().Contains("YES"))
                {
                    DebugConsoleOverlay.Log("Voice-Match", "Gemini Fallback Confirmed wake word!");
                    TriggerJarvis("Gemini-Fallback");
                }
            }
            catch { }
            finally { _isProcessingWakeWord = false; }
        }

        private static void TriggerJarvis(string source)
        {
            if (_isInConversation || _isRecordingCommand) return;

            _isInConversation = true;
            _lastInteractionTime = DateTime.Now;
            DebugConsoleOverlay.Log("Voice", $"Wake word TRIGGERED via {source}");

            // Shorter confirmation or skip if already talking
            if (!TtsManager.IsSpeaking)
            {
                TtsManager.Speak("Yes?");
            }

            Task.Run(async () => await TriggerCommandCapture(isFollowUp: false));
        }

        private static double CalculateRMS(byte[] buffer, int length)
        {
            double sum = 0;
            for (int i = 0; i < length; i += 2)
            {
                short sample = (short)((buffer[i + 1] << 8) | buffer[i]);
                double sample32 = sample / 32768.0;
                sum += sample32 * sample32;
            }
            return Math.Sqrt(sum / (length / 2));
        }

        private static async Task TriggerCommandCapture(bool isFollowUp)
        {
            if (_isRecordingCommand) return;

            _isRecordingCommand = true;
            _commandAudioStream = new MemoryStream();
            _lastSpeechTime = DateTime.Now;

            // Wait for silence
            int elapsed = 0;
            while (elapsed < 60000) // Max 60s
            {
                await Task.Delay(100);
                elapsed += 100;
                // Reduced minimum wait from 2000 to 500ms for faster interaction
                if ((DateTime.Now - _lastSpeechTime).TotalMilliseconds > SilenceTimeoutMs && elapsed > 500) break;
            }

            _isRecordingCommand = false;
            _cooldownUntil = DateTime.Now.AddSeconds(2.0);

            byte[] data = _commandAudioStream.ToArray();
            if (data.Length > 8000)
            {
                string base64 = ConvertToBase64Wav(data);
                string text = await AiAPI.AnalyzeAudioAsync("Transcribe the spoken audio exactly as heard. Do not generate a response, only output the transcribed text. If no speech is detected, return '...'.", base64);

                if (!string.IsNullOrWhiteSpace(text) && text != "...")
                {
                    _lastInteractionTime = DateTime.Now;
                    await ChatOverlay.SubmitVoiceCommand(text, false);
                }
                else
                {
                    // If no valid speech was detected after capture, reset conversation state
                    _isInConversation = false;
                    DebugConsoleOverlay.Log("Voice", "No valid command captured. Resetting conversation mode.");
                }
            }
        }

        public static void LearnPhrase(string phrase)
        {
            if (string.IsNullOrEmpty(phrase)) return;
            if (!_learnedPhrases.Contains(phrase, StringComparer.OrdinalIgnoreCase))
            {
                _learnedPhrases.Add(phrase);
                SaveLearnedPhrases();
            }
        }

        private static void LoadLearnedPhrases()
        {
            if (File.Exists(TrainingPath))
            {
                string json = File.ReadAllText(TrainingPath);
                _learnedPhrases = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
        }

        private static void SaveLearnedPhrases()
        {
            string? dir = Path.GetDirectoryName(TrainingPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(TrainingPath, System.Text.Json.JsonSerializer.Serialize(_learnedPhrases));
        }

        private static string ConvertToBase64Wav(byte[] pcmData)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new WaveFileWriter(ms, new WaveFormat(16000, 1))) writer.Write(pcmData, 0, pcmData.Length);
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        public static void Stop()
        {
            _wakeWordEngine?.RecognizeAsyncCancel();
            _waveIn?.StopRecording();
        }
    }
}
