// Developer: heaplyn
// Date: 2026-08-13
// Summary: Full-Sentence Speech Accumulator & Silence Detector Engine.
// Buffers streaming voice tokens until the user completely finishes speaking, then executes the statement.

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace JarvisLauncher
{
    public static class FullSentenceAccumulator
    {
        public static event Action<string>? OnFullSentenceCompleted;

        private static readonly StringBuilder _sentenceBuffer = new();
        private static System.Threading.Timer? _silenceTimer;
        private static readonly object _lock = new();
        private static DateTime _lastSpeechTime = DateTime.MinValue;

        // 700ms of complete audio silence required before processing user's full sentence
        private static int SilencePauseMs => Math.Max(400, SettingsManager.Current.VoiceChunkingSilenceMs);

        static FullSentenceAccumulator()
        {
            _silenceTimer = new System.Threading.Timer(OnSilenceTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Appends a new voice token into the full-sentence buffer and resets silence timer.
        /// </summary>
        public static void AppendSpeechToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return;

            // Acoustic Echo Suppression: Ignore mic input while TTS is active
            if (TtsManager.IsSpeakingOrEchoing) return;

            lock (_lock)
            {
                string cleanToken = token.Trim();

                // Avoid duplicate consecutive tokens
                string currentStr = _sentenceBuffer.ToString().Trim();
                if (!currentStr.EndsWith(cleanToken, StringComparison.OrdinalIgnoreCase))
                {
                    if (_sentenceBuffer.Length > 0) _sentenceBuffer.Append(" ");
                    _sentenceBuffer.Append(cleanToken);
                }

                _lastSpeechTime = DateTime.Now;

                string fullTextSoFar = _sentenceBuffer.ToString().Trim();
                DebugConsoleOverlay.Log("Sentence Accumulator", $"Listening... \"{fullTextSoFar}\"");

                // Reset silence countdown timer (waits until user finishes speaking completely)
                _silenceTimer?.Change(SilencePauseMs, Timeout.Infinite);
            }
        }

        private static void OnSilenceTimerElapsed(object? state)
        {
            string completedSentence = string.Empty;

            lock (_lock)
            {
                if (_sentenceBuffer.Length == 0) return;

                completedSentence = _sentenceBuffer.ToString().Trim();
                _sentenceBuffer.Clear();
            }

            if (!string.IsNullOrWhiteSpace(completedSentence))
            {
                System.Diagnostics.Debug.WriteLine($"✅ User Finished Speaking ({completedSentence.Length} chars): \"{completedSentence}\"");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    OnFullSentenceCompleted?.Invoke(completedSentence);
                });
            }
        }

        /// <summary>
        /// Forces immediate submission of the accumulated sentence without waiting for silence timer.
        /// </summary>
        public static void FlushNow()
        {
            OnSilenceTimerElapsed(null);
        }

        /// <summary>
        /// Clears current buffered speech.
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _sentenceBuffer.Clear();
                _silenceTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }
    }
}
