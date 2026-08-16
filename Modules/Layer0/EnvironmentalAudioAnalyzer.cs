// Developer: heaplyn
// Date: 2026-08-15
// Summary: Live Environmental Audio Analyzer.
//          Analyzes background sounds in real-time using vector categorization.
//          Fires events when significant non-voice sounds are detected.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public static class EnvironmentalAudioAnalyzer
    {
        public static event Action<string, double>? OnSoundDetected;

        private static DateTime _lastDetectionTime = DateTime.MinValue;
        private const int DetectionCooldownMs = 1500;

        public static void ProcessBuffer(byte[] buffer, int length)
        {
            if (TtsManager.IsSpeakingOrEchoing) return;

            // Extract samples
            int sampleCount = length / 2;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short sample16 = BitConverter.ToInt16(buffer, i * 2);
                samples[i] = sample16 / 32768.0f;
            }

            var features = AudioFeatureExtractor.ExtractFromPcmSamples(samples, 16000);

            // Gate: Only analyze sounds with significant energy
            if (features.RMS_ENERGY > 0.08)
            {
                if ((DateTime.Now - _lastDetectionTime).TotalMilliseconds < DetectionCooldownMs) return;

                var (category, confidence) = SoundVectorManager.ClassifyVector(features.MFCC_COEFFICIENTS);

                if (category != "Ambient" && category != "Unknown")
                {
                    _lastDetectionTime = DateTime.Now;
                    DebugConsoleOverlay.Log("Sound-Analyzer", $"Detected: {category} ({confidence:P0})");
                    OnSoundDetected?.Invoke(category, confidence);

                    // Ingest into predictive stream
                    PredictiveStreamManager.IngestEvent("SOUND", $"{category} ({confidence:P0})");
                }
            }
        }

        public static void LearnCurrentSound(string categoryName, byte[] buffer, int length)
        {
            int sampleCount = length / 2;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short sample16 = BitConverter.ToInt16(buffer, i * 2);
                samples[i] = sample16 / 32768.0f;
            }

            var features = AudioFeatureExtractor.ExtractFromPcmSamples(samples, 16000);
            SoundVectorManager.AddFingerprint(categoryName, features.MFCC_COEFFICIENTS);
            DebugConsoleOverlay.Log("Sound-Trainer", $"Learned new fingerprint for: {categoryName}");
        }
    }
}
