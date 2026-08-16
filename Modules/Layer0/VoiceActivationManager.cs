
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NAudio.Wave;
using System.Windows;
using System.Collections.Generic;
using System.Speech.Recognition;
using System.Threading;
using System.Text.RegularExpressions;

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
        private static float SpeechThreshold => Math.Max(0.020f, SettingsManager.Current.MIC_AUDIO_ENERGY_FLOOR);
        private static int SilenceTimeoutMs = 1200; // Increased timeout for more natural pauses (800 -> 1200)

        public static string LastAiSpokenText { get; set; } = string.Empty;
        private static DateTime _lastInteractionTime = DateTime.MinValue;
        private static bool _isInConversation = false;
        private static DateTime _lastSpeechTime = DateTime.MinValue;
        private static DateTime _cooldownUntil = DateTime.MinValue;
        private static DateTime _lastFallbackTime = DateTime.MinValue;
        private static DateTime _lastWakeVerificationTime = DateTime.MinValue;
        private static bool _lastWakeVerificationResult = false;

        // Syllable/Algorithm tracking
        private static List<double> _rmsHistory = new List<double>();
        private const int HistorySize = 20;

        public static void Start()
        {
            System.Diagnostics.Debug.WriteLine("VoiceActivationManager: Start() called");
            DebugConsoleOverlay.Log("Voice-System", "Starting Voice Activation Engine...");

            // SUBSCRIBE TO TTS STOPPED: Resume listening for follow-ups automatically
            TtsManager.OnSpeechStopped += () => {
                if (_isInConversation && !_isRecordingCommand)
                {
                    // Delay slightly to let echo settle and avoid the IsSpeakingOrEchoing block
                    Task.Run(async () => {
                        await Task.Delay(300);
                        DebugConsoleOverlay.Log("Voice", "Jarvis finished speaking. Resuming follow-up listening...");
                        await TriggerCommandCapture(isFollowUp: true);
                    });
                }
            };

            try
            {
                DebugConsoleOverlay.Log("Voice-System", $"Detected {WaveIn.DeviceCount} audio input devices.");
                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    var capabilities = WaveIn.GetCapabilities(i);
                    DebugConsoleOverlay.Log("Audio-HW", $"Device {i}: {capabilities.ProductName}");
                }

                var culture = System.Globalization.CultureInfo.CurrentCulture;
                if (culture.TwoLetterISOLanguageName != "en") culture = new System.Globalization.CultureInfo("en-US");

                Task.Run(() => AcousticMlClassifier.RebuildAcousticIndex());

                DebugConsoleOverlay.Log("Voice-System", "Initializing Vosk...");
                try
                {
                    if (VoskEngine.Initialize())
                    {
                        VoskEngine.OnFinalResult += (text) =>
                        {
                            if (!SettingsManager.Current.IS_JARVIS_ENABLED) return;
                            if (_isInConversation || _isRecordingCommand || _isProcessingWakeWord) return;
                            if (string.IsNullOrWhiteSpace(text)) return;

                            string lower = text.ToLower();
                            if (lower.Contains("jarvis") || lower.Contains("jar") || lower.Contains("vis") ||
                                lower.Contains("hey") || lower.Contains("hi") || lower.Contains("hello"))
                            {
                                DebugConsoleOverlay.Log("Voice-ML (Vosk)", $"Trigger Match: '{text}'");
                                TriggerJarvis("Vosk-ML", text);
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
                        commands.Add(new string[] { "Jarvis", "Hey Jarvis", "OK Jarvis", "Computer" });
                        if (_learnedPhrases.Count > 0) commands.Add(_learnedPhrases.ToArray());

                        GrammarBuilder gb = new GrammarBuilder();
                        gb.Append(commands);
                        _wakeWordEngine.LoadGrammar(new Grammar(gb));

                        _wakeWordEngine.SpeechRecognized += (s, e) => {
                            if (!SettingsManager.Current.IS_JARVIS_ENABLED) return;
                            if (_isInConversation || _isRecordingCommand || _isProcessingWakeWord) return;

                            double confidence = e.Result.Confidence;
                            double score = 1.0;
                            if (SettingsManager.Current.IS_SPEAKER_VERIFICATION_ENABLED && SpeakerBiometricsManager.IsEnrolled)
                            {
                                score = GetWakeWordSpeakerScore();
                                double threshold = SettingsManager.Current.SPEAKER_VERIFICATION_THRESHOLD;
                                double ratio = threshold > 0 ? score / threshold : 1.0;
                                double factor = Math.Clamp(ratio, 0.3, 1.25);
                                confidence = Math.Clamp(confidence * factor, 0.0, 1.0);
                                DebugConsoleOverlay.Log("Biometrics Fusion", $"SAPI Conf: {e.Result.Confidence:F2} -> Adjusted: {confidence:F2} (Cluster Similarity: {score:F3})");
                            }

                            if (confidence >= Math.Max(0.70, SettingsManager.Current.MIN_VOICE_CONFIDENCE)) {
                                DebugConsoleOverlay.Log("Voice-ML (SAPI)", $"High Confidence Match: '{e.Result.Text}' ({confidence:P0})");
                                TriggerJarvis("SAPI-ML", e.Result.Text);
                            }
                        };

                        _wakeWordEngine.SetInputToDefaultAudioDevice();
                        _wakeWordEngine.RecognizeAsync(RecognizeMode.Multiple);
                    }
                }
                catch { _wakeWordEngine = null; }

                try
                {
                    _waveIn = new WaveInEvent { DeviceNumber = 0, WaveFormat = new WaveFormat(16000, 1) };
                    _waveIn.DataAvailable += OnDataAvailable;
                    _waveIn.RecordingStopped += (s, e) => {
                        if (e.Exception != null)
                            DebugConsoleOverlay.Log("Audio-Error", $"Mic recording stopped unexpectedly: {e.Exception.Message}");
                    };
                    _waveIn.StartRecording();
                    DebugConsoleOverlay.Log("Voice-Init", "Successfully started NAudio WaveIn capture on Device 0.");
                }
                catch (Exception ex)
                {
                    DebugConsoleOverlay.Log("Audio-Error", $"Failed to start NAudio capture: {ex.Message}");
                }

                DebugConsoleOverlay.Log("Voice", "Tiered Activation Pipeline Started (Algorithm -> ML -> Gemini)");

                Task.Run(async () => {
                    while (true) {
                        await Task.Delay(TimeSpan.FromHours(1));
                        await VoiceIntelligenceManager.AnalyzeAndLearnAsync();
                    }
                });

                Task.Run(async () => {
                    while (true) {
                        if (_isInConversation && (DateTime.Now - _lastInteractionTime).TotalSeconds > ConversationTimeoutSeconds) {
                            _isInConversation = false;
                            DebugConsoleOverlay.Log("Voice", "Conversation timed out (Standby).");
                        }
                        await Task.Delay(5000);
                    }
                });
            }
            catch (Exception ex) { DebugConsoleOverlay.Log("Voice Error", ex.Message); }
        }

        private static DateTime _speechStartedTime = DateTime.MinValue;

        private static void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (!SettingsManager.Current.IS_JARVIS_ENABLED) return;

            double rms = CalculateRMS(e.Buffer, e.BytesRecorded);

            // 1. COMMAND RECORDING
            if (_isRecordingCommand)
            {
                _commandAudioStream.Write(e.Buffer, 0, e.BytesRecorded);
                if (rms > SpeechThreshold)
                {
                    if ((DateTime.Now - _lastSpeechTime).TotalMilliseconds > 500)
                        DebugConsoleOverlay.Log("Voice-Capture", "Actively recording speech...");
                    _lastSpeechTime = DateTime.Now;
                }
                return;
            }

            if (TtsManager.IsSpeaking && (DateTime.Now - _speechStartedTime).TotalMilliseconds > 800)
            {
                if (rms > 0.35)
                {
                    DebugConsoleOverlay.Log("Interruption", $"User detected while Jarvis was speaking (RMS: {rms:F2}). Silencing...");
                    TtsManager.Stop();
                }
            }

            if (TtsManager.IsSpeakingOrEchoing) return;

            if (rms > 0.015 && !_isInConversation && !_isProcessingWakeWord)
            {
                // Silence periodic mic peak logs unless verbose
                DebugConsoleOverlay.LogVerbose("Audio-Metrics", $"Peak: {rms:F3} | ZCR: {CalculateZeroCrossingRate(e.Buffer, e.BytesRecorded):F2} | HFE: {CalculateHighFrequencyEnergy(e.Buffer, e.BytesRecorded):F3}");
            }

            if (!_isRecordingCommand && !_isInConversation && !_isProcessingWakeWord)
            {
                UpdateAmbientNoiseFloor(rms);
            }

            // ONLY fill circular buffer if we aren't already recording a command
            lock (_circularBuffer)
            {
                _circularBuffer.AddRange(e.Buffer.Take(e.BytesRecorded));
                int maxSize = 16000 * 2 * BufferSeconds;
                if (_circularBuffer.Count > maxSize) _circularBuffer.RemoveRange(0, _circularBuffer.Count - maxSize);
            }

            // FEED ENVIRONMENTAL ANALYZER (Vector Sound Classification)
            EnvironmentalAudioAnalyzer.ProcessBuffer(e.Buffer, e.BytesRecorded);

            // FEED VOSK ENGINE FOR LOCAL STT (Fix: Was not being called)
            if (_useVoskAsPrimary && VoskEngine.IsInitialized)
            {
                VoskEngine.ProcessAudioBuffer(e.Buffer, e.BytesRecorded);
            }

            if (_isInConversation || _isProcessingWakeWord) return;

            double highFreqEnergy = CalculateHighFrequencyEnergy(e.Buffer, e.BytesRecorded);
            double zcr = CalculateZeroCrossingRate(e.Buffer, e.BytesRecorded);

            _rmsHistory.Add(rms);
            if (_rmsHistory.Count > HistorySize) _rmsHistory.RemoveAt(0);

            if (DetectNeuralPhoneticPattern(rms, zcr, highFreqEnergy))
            {
                // Only trigger if energy is actually significant
                if (rms > 0.08)
                {
                    DebugConsoleOverlay.Log("Voice-Algo", "Neural Pulse Match! Syllable cadence and spectral profile match 'Jarvis'.");
                    TriggerJarvis("Algorithm-V2");
                    return;
                }
            }

            if (rms > 0.15 && (DateTime.Now - _lastFallbackTime).TotalSeconds > 6)
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

            bool hasJarSyllable = false;
            bool hasVisSyllable = false;

            for (int i = 0; i < _rmsHistory.Count; i++)
            {
                // Raised threshold for 'Jar' syllable (0.09 -> 0.15)
                if (_rmsHistory[i] > 0.15) hasJarSyllable = true;

                // Raised threshold and stricter ZCR/Spectral requirements for 'vis' (sibilant)
                if (hasJarSyllable && i > 5 && _rmsHistory[i] > 0.08 && (zcr > 0.25 || highEnergy > 0.04))
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

        private static async Task VerifyWithAi()
        {
            try
            {
                byte[] clip;
                lock (_circularBuffer) clip = _circularBuffer.ToArray();

                string activeWin = MemoryManager.GetCurrentWindowTitle();
                string base64 = ConvertToBase64Wav(clip);
                string prompt = "Task: Decide if 'Jarvis' is clearly being addressed in this clip.\n" +
                               "1. Listen for 'Jarvis', 'Jar', or 'Vis'.\n" +
                               "2. IGNORE background noise, music, or non-English chatter.\n" +
                               "3. If it sounds like a human intentionally calling for an AI, answer 'YES'.\n" +
                               "4. If it's just ambient sound or random words like 'Mala' or 'Alo', answer 'NO'.\n\n" +
                               "Answer ONLY 'YES' or 'NO'.";

                string response = await AiAPI.AnalyzeAudioAsync(prompt, base64);

                if (response.Trim().ToUpper().Contains("YES"))
                {
                    DebugConsoleOverlay.Log("Voice-Match", "Gemini Fallback Confirmed wake word!");
                    VoiceDatasetManager.LogTrigger("Gemini-Fallback", "Jarvis (Wake Word)", activeWin, clip);
                    TriggerJarvis("Gemini-Fallback");
                }
                else
                {
                    VoiceDatasetManager.LogTrigger("Gemini-Rejection", "No Match", activeWin, clip);
                }
            }
            catch { }
            finally { _isProcessingWakeWord = false; }
        }

        private static DateTime _lastTriggerTime = DateTime.MinValue;

        private static void TriggerJarvis(string source, string detectedText = "")
        {
            if (_isRecordingCommand) return;

            // Block new wake-word triggers if we are already in the middle of a conversation turn
            if (_isInConversation && source != "Gemini-Fallback") return;

            // DEBOUNCE: Prevent multiple triggers
            if ((DateTime.Now - _lastTriggerTime).TotalMilliseconds < 2000) return;
            _lastTriggerTime = DateTime.Now;

            // Ensure we are in conversation mode
            _isInConversation = true;
            _lastInteractionTime = DateTime.Now;
            _speechStartedTime = DateTime.Now;
            DebugConsoleOverlay.Log("Voice", $"Wake word TRIGGERED via {source}.");

            // One-Shot Check: If the user provided a command with the wake word (e.g., "Jarvis what time is it")
            string cleanOneShot = StripWakeWordLocal(detectedText);
            if (!string.IsNullOrWhiteSpace(cleanOneShot) && cleanOneShot.Length > 3)
            {
                DebugConsoleOverlay.Log("Voice", "One-Shot Command Detected. Executing immediately.");
                _isInConversation = true; // Ensure state is correct for response handling
                _lastInteractionTime = DateTime.Now;
                _ = Task.Run(async () => {
                    await ChatOverlay.SubmitVoiceCommand(cleanOneShot, false);
                });
                return;
            }

            // Visual feedback
            Application.Current.Dispatcher.Invoke(() => {
                ChatOverlay.ShowChat();
                TextOverlay.Show("🎙️ Listening...", 2500);
            });

            // If Jarvis is silent, say "Yes?".
            if (!TtsManager.IsSpeakingOrEchoing)
            {
                TtsManager.Speak("Yes?");
            }

            // CRITICAL: Trigger capture immediately to catch fast speech/one-shots
            _ = Task.Run(async () => await TriggerCommandCapture(isFollowUp: false));
        }

        private static async Task VerifyWakeWordAcousticallyAsync()
        {
            try
            {
                byte[] clip;
                lock (_circularBuffer) clip = _circularBuffer.ToArray();
                if (clip.Length < 16000) return;

                string tempWav = Path.Combine(Path.GetTempPath(), $"wake_verify_{Guid.NewGuid():N}.wav");
                using (var fs = File.Create(tempWav))
                using (var writer = new WaveFileWriter(fs, new WaveFormat(16000, 1)))
                {
                    writer.Write(clip, 0, clip.Length);
                }

                var mlResult = AcousticMlClassifier.MatchWavFile(tempWav, 0.60);
                if (mlResult.IS_MATCHED)
                {
                    DebugConsoleOverlay.Log("Voice-ML", $"Acoustic ML verified wake word: '{mlResult.MATCHED_PHRASE}' ({mlResult.CONFIDENCE:P0})");
                }
                else
                {
                    _ = Task.Run(async () => {
                        string base64 = ConvertToBase64Wav(clip);
                        string response = await AiAPI.AnalyzeAudioAsync("Is the name 'Jarvis' spoken in this clip? Answer YES or NO.", base64);
                        DebugConsoleOverlay.Log("Voice-AI", $"Gemini verification: {response}");
                    });
                }

                try { File.Delete(tempWav); } catch { }
            }
            catch { }
        }

        private static double GetWakeWordSpeakerScore()
        {
            if (!SpeakerBiometricsManager.IsEnrolled) return 1.0;

            byte[] clipData;
            lock (_circularBuffer)
            {
                int bytesToTake = Math.Min(_circularBuffer.Count, 80000);
                if (bytesToTake < 16000) return 0.0;
                
                clipData = _circularBuffer.Skip(_circularBuffer.Count - bytesToTake).Take(bytesToTake).ToArray();
            }

            string tempWav = Path.Combine(Path.GetTempPath(), "jarvis_wake_score_temp.wav");
            try
            {
                using (var fs = File.Create(tempWav))
                using (var writer = new WaveFileWriter(fs, new WaveFormat(16000, 1)))
                {
                    writer.Write(clipData, 0, clipData.Length);
                }

                var (isVerified, score) = SpeakerBiometricsManager.VerifySpeakerFromWav(tempWav);
                try { File.Delete(tempWav); } catch { }

                _lastWakeVerificationTime = DateTime.Now;
                _lastWakeVerificationResult = isVerified;

                return score;
            }
            catch (Exception ex)
            {
                DebugConsoleOverlay.Log("Biometrics Error", $"Wake score match failed: {ex.Message}");
                return 0.5;
            }
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
            // Hard block if already recording
            if (_isRecordingCommand) return;

            // Wait for existing speech to clear before starting capture
            int echoWait = 0;
            while (TtsManager.IsSpeakingOrEchoing && echoWait < 1500)
            {
                await Task.Delay(100);
                echoWait += 100;
            }

            _isRecordingCommand = true;
            _commandAudioStream = new MemoryStream();

            // CRITICAL: Reset last speech time to far in the past so the timeout doesn't trigger immediately
            _lastSpeechTime = DateTime.MinValue;
            bool userStartedSpeaking = false;
            int elapsed = 0;
            const int MAX_WAIT_TO_START = 5000;

            DebugConsoleOverlay.Log("Voice", isFollowUp ? "Listening for follow-up..." : "Listening for command...");

            while (elapsed < 20000)
            {
                await Task.Delay(50); // Faster polling for snappier response
                elapsed += 50;

                double timeSinceLastSpeech = (DateTime.Now - _lastSpeechTime).TotalMilliseconds;

                // Detect if user IS currently speaking
                if (userStartedSpeaking)
                {
                    if (timeSinceLastSpeech > SilenceTimeoutMs) break;
                }
                else
                {
                    // If we haven't seen speech yet, but just started recording, check if user is already talking
                    if (_lastSpeechTime != DateTime.MinValue && timeSinceLastSpeech < 200) userStartedSpeaking = true;

                    if (elapsed > MAX_WAIT_TO_START)
                    {
                        DebugConsoleOverlay.Log("Voice", "Capture timed out: user did not speak.");
                        break;
                    }
                }
            }

            _isRecordingCommand = false;
            _cooldownUntil = DateTime.Now.AddSeconds(0.5); // Faster cooldown

            byte[] data = _commandAudioStream.ToArray();
            if (data.Length > 4000) // Lowered to catch short words like "yes/no/hi" (8000 -> 4000)
            {
                string text = "";
                if (VoskEngine.IsInitialized)
                {
                    DebugConsoleOverlay.Log("Voice-STT", "Transcribing command locally via Vosk...");
                    text = VoskEngine.RecognizePcmData(data); // USE DIRECT PCM RECOGNITION
                }
                else
                {
                    DebugConsoleOverlay.Log("Voice-STT", "Vosk not ready. Falling back to Gemini Cloud STT...");
                    string base64 = ConvertToBase64Wav(data);
                    string transcribePrompt = "Task: Transcribe the spoken human speech in this audio clip EXACTLY as heard.\n" +
                                             "RULES:\n" +
                                             "1. Only output the transcribed text. Do not add explanations or notes.\n" +
                                             "2. If no clear human speech is present, output exactly '...'.\n" +
                                             "3. Do NOT use past conversation history to 'guess' what was said. Be literal.";
                    text = (await AiAPI.AnalyzeAudioAsync(transcribePrompt, base64) ?? "").Trim();
                }

                string activeWin = MemoryManager.GetCurrentWindowTitle();

                // --- ECHO REJECTION ---
                // If the transcription is too similar to Jarvis's last spoken sentence, it's likely an echo.
                if (!string.IsNullOrEmpty(LastAiSpokenText))
                {
                    double similarity = SearchUtil.GetSimilarity(text.ToLower(), LastAiSpokenText.ToLower());
                    if (similarity > 0.80)
                    {
                        DebugConsoleOverlay.Log("Voice-Echo", $"Rejected capture: transcription too similar to last AI speech ({similarity:P0}).");
                        _isInConversation = false;
                        return;
                    }
                }

                // ANTI-DOT SPAM: Filter out silence/noise results from AI
                if (string.IsNullOrWhiteSpace(text) || Regex.IsMatch(text, @"^[\.\s]+$"))
                {
                    _isInConversation = false;
                    return;
                }

                // STRIP WAKE WORD & ECHO: Local pass to remove redundant triggers
                string cleanText = StripWakeWordLocal(text.Trim());

                if (string.IsNullOrWhiteSpace(cleanText))
                {
                    if (isFollowUp)
                    {
                        DebugConsoleOverlay.Log("Voice", "Transcription empty during follow-up. Waiting one more cycle...");
                        // Don't kill conversation yet, might be a long pause
                        return;
                    }
                    _isInConversation = false;
                    return;
                }

                string lowText = cleanText.ToLower();
                if (lowText.Contains("turn off voice mode") || lowText.Contains("disable voice mode"))
                {
                    SettingsManager.Current.IS_VOICE_MODE_ACTIVE = false;
                    SettingsManager.Save();
                    TtsManager.Speak("Voice mode disabled.");
                    TextOverlay.Show("🔇 Voice Mode: OFF", 3000);
                    _isInConversation = false;
                    return;
                }
                if (lowText.Contains("turn on voice mode") || lowText.Contains("enable voice mode"))
                {
                    SettingsManager.Current.IS_VOICE_MODE_ACTIVE = true;
                    SettingsManager.Save();
                    TtsManager.Speak("Voice mode enabled.");
                    TextOverlay.Show("🎙️ Voice Mode: ON", 3000);
                    _isInConversation = false;
                    return;
                }

                if (!SettingsManager.Current.IS_VOICE_MODE_ACTIVE)
                {
                    DebugConsoleOverlay.Log("Voice", "Ignored capture because Voice Mode is OFF.");
                    _isInConversation = false;
                    return;
                }

                cleanText = VoiceIntelligenceManager.ApplyIntelligence(cleanText);
                VoiceDatasetManager.LogTrigger("Command-Capture", cleanText, activeWin, data);

                string lowerText = cleanText.ToLower();
                
                bool isNoiseText = Regex.IsMatch(lowerText, @"^[\.\s\?\!]+$") ||
                                   lowerText.Contains("breathing") || 
                                   lowerText.Contains("sighing") || 
                                   lowerText.Contains("coughing") || 
                                   lowerText.Contains("typing") || 
                                   lowerText.Contains("keyboard") || 
                                   lowerText.Contains("click") || 
                                   lowerText.Contains("static");

                if (!string.IsNullOrWhiteSpace(cleanText) && !isNoiseText)
                {
                    _lastInteractionTime = DateTime.Now;
                    _ = SaveAudioForTrainingAsync(data, cleanText);
                    _ = ChatOverlay.SubmitVoiceCommand(cleanText, false);

                    // DO NOT set _isInConversation = false here.
                    // Let the TtsManager.OnSpeechStopped event trigger the next capture.
                }
                else
                {
                    // If they didn't say anything meaningful, end the conversation mode
                    _isInConversation = false;
                    DebugConsoleOverlay.Log("Voice", $"Discarded ambient/particle sound trigger: '{cleanText}'");
                }
            }
            else
            {
                _isInConversation = false;
                DebugConsoleOverlay.Log("Voice", "Discarded audio capture (duration too short).");
            }
        }

        private static string StripWakeWordLocal(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string lower = text.ToLowerInvariant();

            // If the AI just transcribed Jarvis's own name or acknowledgement, ignore it
            string[] noiseOnly = new[] { "jarvis", "hey jarvis", "yes", "yes?", "yeah", "yep", "...", "ready" };
            if (noiseOnly.Contains(lower)) return string.Empty;

            string[] prefixes = new[] { "hey jarvis", "ok jarvis", "hi jarvis", "hello jarvis", "jarvis", "computer", "yes", "yeah", "yep" };
            foreach (var p in prefixes)
            {
                if (lower.StartsWith(p))
                {
                    string stripped = text.Substring(p.Length).Trim();
                    // Remove leading punctuation
                    if (stripped.StartsWith(",") || stripped.StartsWith(".")) stripped = stripped.Substring(1).Trim();
                    return stripped;
                }
            }
            return text;
        }

        private static async Task SaveAudioForTrainingAsync(byte[] data, string transcription)
        {
            try
            {
                string trainDir = Path.Combine(PathHandler.GetDataDirectory(), "Training", "VoiceCaptures");
                if (!Directory.Exists(trainDir)) Directory.CreateDirectory(trainDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string id = Guid.NewGuid().ToString("N").Substring(0, 6);
                string wavPath = Path.Combine(trainDir, $"{timestamp}_{id}.wav");
                string txtPath = Path.Combine(trainDir, $"{timestamp}_{id}.txt");

                using (var fs = File.Create(wavPath))
                using (var writer = new NAudio.Wave.WaveFileWriter(fs, new NAudio.Wave.WaveFormat(16000, 1)))
                {
                    writer.Write(data, 0, data.Length);
                }

                await File.WriteAllTextAsync(txtPath, transcription);
                DebugConsoleOverlay.Log("Voice-Training", $"Saved clip to training dataset: {id}.wav");
            }
            catch { }
        }

        public static async Task SaveBackgroundAudioTokenAsync(string transcription)
        {
            try
            {
                string logFile = Path.Combine(PathHandler.GetDataDirectory(), "Training", "BackgroundTranscription.log");
                string dir = Path.GetDirectoryName(logFile)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {transcription}{Environment.NewLine}";
                await File.AppendAllTextAsync(logFile, line);
            }
            catch { }
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
                using (var writer = new NAudio.Wave.WaveFileWriter(ms, new NAudio.Wave.WaveFormat(16000, 1))) writer.Write(pcmData, 0, pcmData.Length);
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
                // More aggressive noise floor: 2.5x the average ambient noise
                float newFloor = (float)Math.Clamp(avg * 2.5, 0.025, 0.40);
                SettingsManager.Current.MIC_AUDIO_ENERGY_FLOOR = newFloor;
            }
        }

        public static async Task EnrollVoiceAsync(string name)
        {
            if (_isRecordingCommand || _isProcessingWakeWord)
            {
                TextOverlay.Show("⚠️ Voice engine busy, try again.", 3000);
                return;
            }

            _isInConversation = true;

            TtsManager.Speak($"Recording your voiceprint profile for {name}. Please speak your wake phrase after the chime.");
            await Task.Delay(2500);

            _isRecordingCommand = true;
            _commandAudioStream = new MemoryStream();
            _lastSpeechTime = DateTime.Now;

            DebugConsoleOverlay.Log("Biometrics", "Voice enrollment recording started...");
            TextOverlay.Show("🎙️ Recording Voiceprint (Speak now)...", 4000);

            await Task.Delay(4000);

            _isRecordingCommand = false;
            _isInConversation = false;

            byte[] data = _commandAudioStream.ToArray();
            if (data.Length > 8000)
            {
                string tempWav = Path.Combine(Path.GetTempPath(), "jarvis_enroll_temp.wav");
                try
                {
                    using (var fs = File.Create(tempWav))
                    using (var writer = new NAudio.Wave.WaveFileWriter(fs, new NAudio.Wave.WaveFormat(16000, 1)))
                    {
                        writer.Write(data, 0, data.Length);
                    }

                    bool success = SpeakerBiometricsManager.EnrollFromWav(name, tempWav);
                    if (success)
                    {
                        TtsManager.Speak($"Voice profile saved successfully for {name}. Owner verification is now ready.");
                        TextOverlay.Show($"✅ Speaker verification enrolled for {name}!", 4000);
                    }
                    else
                    {
                        TtsManager.Speak("Failed to extract voiceprint features. Please speak louder and retry.");
                        TextOverlay.Show("❌ Voiceprint extraction failed.", 4000);
                    }

                    try { File.Delete(tempWav); } catch { }
                }
                catch (Exception ex)
                {
                    DebugConsoleOverlay.Log("Biometrics Error", $"Enrollment failed: {ex.Message}");
                }
            }
            else
            {
                TtsManager.Speak("No audio captured. Voice enrollment aborted.");
            }
        }

        public static async Task LearnEnvironmentalSoundAsync(string categoryName)
        {
            if (_isRecordingCommand) return;

            TtsManager.Speak($"Ready to learn sound: {categoryName}. Please make the sound after the chime.");
            await Task.Delay(2500);

            _isRecordingCommand = true;
            _commandAudioStream = new MemoryStream();
            _lastSpeechTime = DateTime.Now;

            TextOverlay.Show($"🎙️ Learning sound: {categoryName}...", 3000);
            await Task.Delay(2000); // Record for 2 seconds

            _isRecordingCommand = false;
            byte[] data = _commandAudioStream.ToArray();

            if (data.Length > 1000)
            {
                EnvironmentalAudioAnalyzer.LearnCurrentSound(categoryName, data, data.Length);
                TtsManager.Speak($"Got it. Fingerprint for {categoryName} has been stored in my sound library.");
                TextOverlay.Show($"✅ Learned sound: {categoryName}", 3000);
            }
            else
            {
                TtsManager.Speak("I didn't catch any significant sound. Please try again.");
            }
        }

        public static void Stop()
        {
            _wakeWordEngine?.RecognizeAsyncCancel();
            _waveIn?.StopRecording();
        }
    }
}
