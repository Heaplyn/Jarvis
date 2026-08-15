// Developer: heaplyn
// Date: 2026-08-13
// Summary: Continuous offline wake-word detector + acoustic phonetic alias normalization engine.
// Buffers continuous speech into FullSentenceAccumulator until user completely finishes speaking.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Recognition;
using System.Threading.Tasks;
using System.Windows;

namespace JarvisLauncher
{
    public static class LocalWakeWordDetector
    {
        // Suppress unused warnings as these are intended for future extensibility or external subscribers
        public static event Action<string>? OnWakeWordDetected;
        public static event Action<string>? OnVoiceCommandRecognized;

        private static SpeechRecognitionEngine? _engine;
        private static bool _isListening = false;
        private static readonly object _lock = new();

        public static bool IsListening => _isListening;

        public static void Initialize()
        {
            lock (_lock)
            {
                if (_isListening) return;

                try
                {
                    _engine = new SpeechRecognitionEngine();
                    _engine.SetInputToDefaultAudioDevice();

                    // 1. High-Priority Custom Choice Grammar (Reliable Wake Trigger)
                    var choices = new Choices();
                    choices.Add(new string[] { "Jarvis", "Hey Jarvis", "OK Jarvis", "Hi Jarvis", "Computer" });

                    var gb = new GrammarBuilder();
                    gb.Append(choices);
                    var wakeGrammar = new Grammar(gb) { Name = "JarvisWakeTrigger", Priority = 1 };
                    _engine.LoadGrammar(wakeGrammar);

                    // 2. Free-Form Dictation Grammar for continuous sentences
                    var dictationGrammar = new DictationGrammar { Name = "JarvisDictation", Priority = 0 };
                    _engine.LoadGrammar(dictationGrammar);

                    _engine.SpeechRecognized += Engine_SpeechRecognized;
                    _engine.SpeechHypothesized += Engine_SpeechHypothesized;
                    _engine.RecognizeAsync(RecognizeMode.Multiple);

                    // Subscribe Full-Sentence Accumulator to process statement ONLY when user finishes speaking
                    FullSentenceAccumulator.OnFullSentenceCompleted += (fullSentence) =>
                    {
                        if (string.IsNullOrWhiteSpace(fullSentence)) return;
                        string normalized = NormalizeAcousticPhrases(fullSentence);
                        DebugConsoleOverlay.Log("Full Sentence Completed", $"Processing statement after silence: \"{normalized}\"");
                        ProcessSpokenQuery(normalized);
                    };

                    // Initialize Vosk API Neural Speech-to-Text Engine
                    if (VoskEngine.Initialize())
                    {
                        VoskEngine.OnFinalResult += (text) =>
                        {
                            if (string.IsNullOrWhiteSpace(text)) return;
                            // Acoustic Echo Cancellation: Suppress speech recognition triggered by Jarvis's own TTS output
                            if (TtsManager.IsSpeakingOrEchoing) return;

                            string normalized = NormalizeAcousticPhrases(text);
                            DebugConsoleOverlay.Log("Background-Capture", $"Recorded token: \"{normalized}\"");

                            // FEED PREDICTIVE STREAM
                            PredictiveStreamManager.IngestEvent("VOICE", normalized);

                            // BACKGROUND LOGGING: Save even non-commands to training folder for future learning
                            _ = VoiceActivationManager.SaveBackgroundAudioTokenAsync(normalized);

                            // Append token to sentence buffer (waits until user finishes speaking completely)
                            FullSentenceAccumulator.AppendSpeechToken(normalized);
                        };
                    }

                    _isListening = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Local wake word engine init note: {ex.Message}");
                }
            }
        }

        private static void Engine_SpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
        {
            if (e.Result == null || string.IsNullOrWhiteSpace(e.Result.Text)) return;

            // INTERRUPTION LOGIC: If Jarvis is speaking and we detect a strong speech hypothesis, stop him.
            if (TtsManager.IsSpeakingOrEchoing && e.Result.Confidence > 0.65) // Increased from 0.6
            {
                CheckForUserInterruption(e.Result.Text);
            }

            if (TtsManager.IsSpeakingOrEchoing) return;
    
            string rawText = e.Result.Text.Trim();
            string normalizedText = NormalizeAcousticPhrases(rawText);
            float conf = e.Result.Confidence;

            // Only log thoughts if they are reasonably confident to reduce UI noise
            if (conf > 0.40)
            {
                DebugConsoleOverlay.Log("Voice Thought", $"\"{normalizedText}\" ({conf * 100:F0}% confidence)");
            }
        }

        public static void Stop()
        {
            lock (_lock)
            {
                if (!_isListening || _engine == null) return;
                try
                {
                    _engine.RecognizeAsyncCancel();
                    _engine.Dispose();
                    _engine = null;
                }
                catch { }
                _isListening = false;
            }
        }

        private static void Engine_SpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result == null || string.IsNullOrWhiteSpace(e.Result.Text)) return;

            // INTERRUPTION LOGIC: Recognized speech while Jarvis is talking instantly stops him.
            if (TtsManager.IsSpeakingOrEchoing && e.Result.Confidence > 0.6) // Increased from 0.5
            {
                CheckForUserInterruption(e.Result.Text);
                return;
            }

            if (TtsManager.IsSpeakingOrEchoing) return;

            // Strict confidence gate (default 75%, up to 98%) to make voice recognition less sensitive to background room noise
            double minConf = Math.Max(0.50, SettingsManager.Current.MIN_VOICE_CONFIDENCE); // Increased floor from 0.30
            if (e.Result.Confidence < minConf)
            {
                DebugConsoleOverlay.Log("Voice Ignored (Low Confidence)", $"\"{e.Result.Text}\" ({e.Result.Confidence * 100:F0}% < {minConf * 100:F0}%)");
                return;
            }

            string rawText = e.Result.Text.Trim();
            string recognizedText = NormalizeAcousticPhrases(rawText);

            if (string.IsNullOrWhiteSpace(recognizedText)) return;

            DebugConsoleOverlay.Log("Voice Recognized", $"\"{recognizedText}\" ({e.Result.Confidence * 100:F0}% confidence)");

            // Buffer token into FullSentenceAccumulator (never execute mid-sentence)
            FullSentenceAccumulator.AppendSpeechToken(recognizedText);
        }

        private static void CheckForUserInterruption(string text)
        {
            string lower = text.ToLowerInvariant();

            // If the user says a wake word or common stop words, stop the AI immediately
            bool isInterruptionPhrase = lower.Contains("jarvis") ||
                                        lower.Contains("stop") ||
                                        lower.Contains("wait") ||
                                        lower.Contains("listen") ||
                                        lower.Contains("hey");

            // Or if it's just a long enough sentence, assume the user is talking to us
            if (isInterruptionPhrase || lower.Split(' ').Length > 1)
            {
                DebugConsoleOverlay.Log("Interruption", $"User interrupted Jarvis with: \"{text}\"");
                TtsManager.Stop();

                // Also reset the accumulator so we start fresh with the new speech
                FullSentenceAccumulator.Reset();
            }
        }

        private static void ProcessSpokenQuery(string query)
        {
            query = query.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            // FLEXIBLE TRIGGER: Accept any command if Jarvis was mentioned recently
            string cleanQuery = query;
            bool hasWakeWord = false;
            string[] wakeWords = new[] { "hey jarvis", "ok jarvis", "hi jarvis", "hello jarvis", "jarvis", "computer", "hey", "hi", "hello" };

            foreach (var w in wakeWords)
            {
                if (query.StartsWith(w, StringComparison.OrdinalIgnoreCase))
                {
                    cleanQuery = query.Substring(w.Length).Trim();
                    // Remove leading punctuation
                    if (cleanQuery.StartsWith(",") || cleanQuery.StartsWith(".")) cleanQuery = cleanQuery.Substring(1).Trim();
                    hasWakeWord = true;
                    break;
                }
            }

            // If no explicit wake word, but query contains "jarvis", consider it a match
            if (!hasWakeWord && query.ToLowerInvariant().Contains("jarvis"))
            {
                hasWakeWord = true;
            }

            if (!hasWakeWord)
            {
                DebugConsoleOverlay.Log("Voice-Filter", $"Ignored speech: \"{query}\" (No wake trigger detected)");
                return;
            }

            // Remove optional leading punctuation or filler words
            if (cleanQuery.StartsWith(",") || cleanQuery.StartsWith(".")) cleanQuery = cleanQuery.Substring(1).Trim();
            if (cleanQuery.StartsWith("please ", StringComparison.OrdinalIgnoreCase)) cleanQuery = cleanQuery.Substring(7).Trim();
            if (cleanQuery.StartsWith("can you ", StringComparison.OrdinalIgnoreCase)) cleanQuery = cleanQuery.Substring(8).Trim();

            // If user ONLY said "Jarvis" or "Hey Jarvis" without extra words, speak a brief prompt
            if (string.IsNullOrWhiteSpace(cleanQuery))
            {
                TextOverlay.Show("🎙️ Yes? Listening...", 2000);
                TtsManager.Speak("Yes?");
                return;
            }

            // ⚡ Voice Recognition Word/Phrase Chunking Engine
            if (SettingsManager.Current.ENABLE_VOICE_COMMAND_CHUNKING && (cleanQuery.Contains(" then ") || cleanQuery.Contains(" and then ") || cleanQuery.Contains(" and ") || cleanQuery.Contains(" next ")))
            {
                string[] delims = new[] { " and then ", " then ", " next ", " and ", " also " };
                var chunks = cleanQuery.Split(delims, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(c => c.Trim())
                                       .Where(c => !string.IsNullOrWhiteSpace(c))
                                       .ToList();

                if (chunks.Count > 1)
                {
                    DebugConsoleOverlay.Log("Voice Chunking", $"Sliced statement into {chunks.Count} chunked commands: [{string.Join(" | ", chunks)}]");
                    foreach (var chunk in chunks)
                    {
                        ExecuteSingleVoiceQuery(chunk);
                    }
                    return;
                }
            }

            ExecuteSingleVoiceQuery(cleanQuery);
        }

        private static string StripWakeWordPrefix(string statement)
        {
            string clean = statement.Trim();
            string[] wakeWords = new[] { "hey jarvis", "ok jarvis", "hi jarvis", "hello jarvis", "jarvis", "computer" };
            foreach (var w in wakeWords)
            {
                if (clean.StartsWith(w, StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean.Substring(w.Length).Trim();
                    // Remove leading punctuation or filler words
                    if (clean.StartsWith(",") || clean.StartsWith(".")) clean = clean.Substring(1).Trim();
                    if (clean.StartsWith("please ", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(7).Trim();
                    if (clean.StartsWith("can you ", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(8).Trim();
                    break;
                }
            }
            return clean;
        }

        private static bool IsStandaloneWakeWord(string text)
        {
            string lower = text.Trim().ToLowerInvariant();
            string[] wakeWords = new[] { "jarvis", "hey jarvis", "ok jarvis", "hi jarvis", "hello jarvis", "computer" };
            return wakeWords.Contains(lower);
        }

        private static void ExecuteSingleVoiceQuery(string query)
        {
            query = query.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            string lower = query.ToLowerInvariant();

            // 1. MASTER VOICE MODE TOGGLE (Always active if engine is running)
            if (lower.Contains("turn off voice mode") || lower.Contains("disable voice mode"))
            {
                SettingsManager.Current.IS_VOICE_MODE_ACTIVE = false;
                SettingsManager.Save();
                TtsManager.Speak("Voice mode disabled. I'll still listen for your command to turn it back on.");
                TextOverlay.Show("🔇 Voice Mode: OFF", 3000);
                return;
            }
            if (lower.Contains("turn on voice mode") || lower.Contains("enable voice mode"))
            {
                SettingsManager.Current.IS_VOICE_MODE_ACTIVE = true;
                SettingsManager.Save();
                TtsManager.Speak("Voice mode enabled. I am listening.");
                TextOverlay.Show("🎙️ Voice Mode: ON", 3000);
                return;
            }

            // 2. CHECK IF VOICE MODE IS ACTIVE
            if (!SettingsManager.Current.IS_VOICE_MODE_ACTIVE)
            {
                DebugConsoleOverlay.Log("Voice-Filter", "Ignored speech because Voice Mode is OFF.");
                return;
            }

            // Explicit command trigger verbs requiring action
            string[] explicitCommandVerbs = new[] {
                "open", "run", "launch", "start", "close", "stop", "kill", "toggle",
                "set", "turn", "lock", "show", "hide", "organize", "search", "play", "pause", "download", "do", "execute"
            };

            bool isExplicitCommand = explicitCommandVerbs.Any(v => lower.StartsWith(v + " ") || lower == v);

            // If NOT explicitly told to run a command, route to Gemini AI to parse intent!
            if (!isExplicitCommand || !CommandParser.IsKnownLocalCommand(query))
            {
                // Only show toast if chat isn't already active to reduce UI noise
                if (!ChatOverlay.IsVisible)
                {
                    TextOverlay.Show($"🧠 AI Assistant: \"{query}\"...", 2500);
                }

                DebugConsoleOverlay.Log("Voice AI Intent", $"Routing statement to Gemini AI parser: \"{query}\"");

                Task.Run(async () =>
                {
                    try
                    {
                        await ChatOverlay.SubmitVoiceCommand(query, showUi: true);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Voice Gemini query error: {ex.Message}");
                    }
                });
                return;
            }

            // Explicit command requested -> execute local command
            DebugConsoleOverlay.Log("Voice Execution", $"Executing local PC command: \"{query}\"");
            TextOverlay.Show($"⚡ Local Command: \"{query}\"", 2000);
            CommandParser.ExecuteFirstSuggestion(query);
        }

        /// <summary>
        /// Acoustic Phonetic Normalizer: Maps Windows Speech Engine misrecognitions to true intended words.
        /// </summary>
        public static string NormalizeAcousticPhrases(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            string normalized = text.Trim();

            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "chart is", "jarvis" },
                { "targets", "jarvis" },
                { "target", "jarvis" },
                { "chargers", "jarvis" },
                { "harvest", "jarvis" },
                { "chavis", "jarvis" },
                { "garvis", "jarvis" },
                { "jervis", "jarvis" },
                { "huge", "jarvis" },
                { "jawvis", "jarvis" },
                { "color eu", "how are you" },
                { "color you", "how are you" }
            };

            foreach (var kvp in replacements)
            {
                if (normalized.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;

                normalized = System.Text.RegularExpressions.Regex.Replace(
                    normalized,
                    @"\b" + System.Text.RegularExpressions.Regex.Escape(kvp.Key) + @"\b",
                    kvp.Value,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return normalized;
        }

        private static bool IsWakeWord(string text, out string remainingCommand)
        {
            remainingCommand = string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string lower = text.Trim().ToLowerInvariant();
            string[] wakeWords = new[] { "jarvis", "hey jarvis", "ok jarvis", "hi jarvis", "hello jarvis" };

            foreach (var wake in wakeWords)
            {
                if (lower.StartsWith(wake))
                {
                    remainingCommand = text.Substring(wake.Length).Trim();
                    return true;
                }
            }
            return false;
        }
    }
}
