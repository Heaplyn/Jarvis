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
        private static bool _useVoskAsPrimary = true;
        private static readonly string TrainingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "VoiceTraining.json");
        private static List<string> _learnedPhrases = new List<string>();

        // Settings
        private static int ConversationTimeoutSeconds = 60;
        private static readonly List<byte> _circularBuffer = new List<byte>();
        private static readonly List<double> _ambientHistory = new List<double>();
        private const int BufferSeconds = 3;
        private static float SpeechThreshold => Math.Max(0.02f, SettingsManager.Current.MicAudioEnergyFloor);
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
                    if (VoskEngine.Initialize())
                    {
                        VoskEngine.OnFinalResult += (text) =>
                        {
                            if (!SettingsManager.Current.IsJarvisEnabled) return;
                            if (string.IsNullOrWhiteSpace(text)) return;

                            string lower = text.ToLower();
                            if (lower.Contains("jarvis") || lower.Contains("jar") || lower.Contains("vis"))
                            {
                                DebugConsoleOverlay.Log("Voice-ML (Vosk)", $"Neural Match: '{text}'");
                                TriggerJarvis("Vosk-ML");
                            }
                        };
                        DebugConsoleOverlay.Log("Voice", "Vosk Neural Engine Integrated into Pipeline.");
                    }
                    else
                    {
                        DebugConsoleOverlay.Log("Voice Warning", "Vosk model not found. Using SAPI fallback.");
                        _useVoskAsPrimary = false;
                    }

                    if (!_useVoskAsPrimary)
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
                            if (e.Result.Confidence > 0.86) {
                                DebugConsoleOverlay.Log("Voice-ML (SAPI)", $"High Confidence Match: '{e.Result.Text}' ({e.Result.Confidence:P0})");
                                TriggerJarvis("SAPI-ML");
                            }
                        };

                        _wakeWordEngine.SetInputToDefaultAudioDevice();
                        _wakeWordEngine.RecognizeAsync(RecognizeMode.Multiple);
                    }
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

            // Dynamic Voice Filter Autocalibration
            if (!_isRecordingCommand && !_isInConversation && !_isProcessingWakeWord)
            {
                UpdateAmbientNoiseFloor(rms);
            }

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

            // 1. Feature Extraction: Spectral Analysis (High-frequency content for 's')
            double highFreqEnergy = CalculateHighFrequencyEnergy(e.Buffer, e.BytesRecorded);
            double zcr = CalculateZeroCrossingRate(e.Buffer, e.BytesRecorded);

            // --- TIER 1: THE ALGORITHM (Instant, local energy pattern + Phonetic cues) ---
            _rmsHistory.Add(rms);
            if (_rmsHistory.Count > HistorySize) _rmsHistory.RemoveAt(0);

            // Look for a specific "Jar-vis" energy pattern
            // "vis" ends with a fricative (high ZCR and high-freq energy)
            if (DetectNeuralPhoneticPattern(rms, zcr, highFreqEnergy))
            {
                DebugConsoleOverlay.Log("Voice-Algo", "Neural Pulse Match! Syllable cadence and spectral profile match 'Jarvis'.");
                TriggerJarvis("Algorithm-V2");
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

        private static bool DetectNeuralPhoneticPattern(double rms, double zcr, double highEnergy)
        {
            if (_rmsHistory.Count < HistorySize) return false;

            // Simple state machine for "Jar" (loud, mid-freq) followed by "vis" (loud, high-freq/noisy)
            bool hasJarSyllable = false;
            bool hasVisSyllable = false;

            for (int i = 0; i < _rmsHistory.Count; i++)
            {
                // "Jar" part: Significant volume
                if (_rmsHistory[i] > 0.06) hasJarSyllable = true;

                // "vis" part: Significant volume + noise characteristics (High ZCR or High Freq Energy)
                if (hasJarSyllable && i > 5 && _rmsHistory[i] > 0.04 && (zcr > 0.15 || highEnergy > 0.02))
                {
                    hasVisSyllable = true;
                }
            }

            return hasJarSyllable && hasVisSyllable;
        }

        private static double CalculateZeroCrossingRate(byte[] buffer, int length)
        {
            int crossings = 0;
            for (int i = 2; i < length; i += 2)
            {
                short prev = (short)((buffer[i - 1] << 8) | buffer[i - 2]);
                short curr = (short)((buffer[i + 1] << 8) | buffer[i]);
                if ((prev > 0 && curr < 0) || (prev < 0 && curr > 0)) crossings++;
            }
            return (double)crossings / (length / 2);
        }

        private static double CalculateHighFrequencyEnergy(byte[] buffer, int length)
        {
            // Simple high-pass proxy: energy of differences between adjacent samples
            double diffSum = 0;
            for (int i = 2; i < length; i += 2)
            {
                short prev = (short)((buffer[i - 1] << 8) | buffer[i - 2]);
                short curr = (short)((buffer[i + 1] << 8) | buffer[i]);
                double diff = (curr - prev) / 32768.0;
                diffSum += diff * diff;
            }
            return Math.Sqrt(diffSum / (length / 2));
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
                string text = await AiAPI.AnalyzeAudioAsync("Transcribe the spoken human speech exactly as heard. Do not generate a response, only output the transcribed text. If there is no clear human speech (e.g., only breathing, keyboard typing clicks, background sigh/cough, or silence), return '...'.", base64);

                string cleanText = (text ?? "").Trim();
                string lowerText = cleanText.ToLower();
                
                // Smart Rejection Filter: Skip ambient noises, transcript markers, and single noise particles
                bool isNoiseText = lowerText == "..." || 
                                   lowerText.Contains("breathing") || 
                                   lowerText.Contains("sighing") || 
                                   lowerText.Contains("coughing") || 
                                   lowerText.Contains("typing") || 
                                   lowerText.Contains("keyboard") || 
                                   lowerText.Contains("click") || 
                                   lowerText.Contains("static");

                // If it's a single tiny word (length <= 3) and not a recognized command keyword, reject it as a false audio trigger
                bool isTinyNoiseWord = cleanText.Length <= 3 && 
                                       !lowerText.Contains("run") && 
                                       !lowerText.Contains("git") && 
                                       !lowerText.Contains("ipa") && 
                                       !lowerText.Contains("mcp") && 
                                       !lowerText.Contains("off") && 
                                       !lowerText.Contains("on") && 
                                       !lowerText.Contains("set");

                if (!string.IsNullOrWhiteSpace(cleanText) && !isNoiseText && !isTinyNoiseWord)
                {
                    _lastInteractionTime = DateTime.Now;
                    await ChatOverlay.SubmitVoiceCommand(cleanText, false);
                }
                else
                {
                    // If no valid speech was detected after capture, reset conversation state
                    _isInConversation = false;
                    DebugConsoleOverlay.Log("Voice", $"Discarded ambient/particle sound trigger: '{cleanText}'");
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

        private static void UpdateAmbientNoiseFloor(double rms)
        {
            if (rms <= 0) return;
            lock (_ambientHistory)
            {
                _ambientHistory.Add(rms);
                if (_ambientHistory.Count > 100) _ambientHistory.RemoveAt(0);

                double avg = _ambientHistory.Average();
                // Set threshold to 1.6x the ambient noise floor
                float newFloor = (float)Math.Clamp(avg * 1.6, 0.03, 0.25);
                SettingsManager.Current.MicAudioEnergyFloor = newFloor;
            }
        }

        public static void Stop()
        {
            _wakeWordEngine?.RecognizeAsyncCancel();
            _waveIn?.StopRecording();
        }
    }
}
