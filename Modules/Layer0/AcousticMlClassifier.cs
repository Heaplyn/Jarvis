// Developer: heaplyn
// Date: 2026-08-13
// Summary: Machine Learning Acoustic Sound Classifier matching live mic audio against trained Voice Profile MFCC vectors.

using System;
using System.Collections.Generic;
using System.IO;

namespace JarvisLauncher
{
    public class AcousticMatchResult
    {
        public bool IsMatched { get; set; } = false;
        public string MatchedPhrase { get; set; } = string.Empty;
        public double Confidence { get; set; } = 0.0; // 0.0 to 1.0 (0% to 100%)
        public VoiceSample? BestSample { get; set; }
    }

    public static class AcousticMlClassifier
    {
        private static readonly Dictionary<string, double[]> _cachedProfileMfccs = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Re-builds in-memory MFCC acoustic feature vectors from all recorded samples in Voice Profile.
        /// </summary>
        public static void RebuildAcousticIndex()
        {
            _cachedProfileMfccs.Clear();
            var samples = VoiceTrainerManager.Profile.Samples;

            foreach (var sample in samples)
            {
                if (File.Exists(sample.AudioFilePath))
                {
                    var features = AudioFeatureExtractor.ExtractFromFile(sample.AudioFilePath);
                    if (features != null && features.MfccCoefficients != null)
                    {
                        string key = $"{sample.Id}:{sample.Phrase}";
                        _cachedProfileMfccs[key] = features.MfccCoefficients;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"🧠 Rebuilt Acoustic ML Index with {_cachedProfileMfccs.Count} sample feature vectors.");
        }

        /// <summary>
        /// Classifies an incoming WAV audio file against the trained acoustic voice profile using MFCC Cosine Distance.
        /// </summary>
        public static AcousticMatchResult MatchWavFile(string wavFilePath, double threshold = 0.70)
        {
            var result = new AcousticMatchResult();
            if (!File.Exists(wavFilePath)) return result;

            if (_cachedProfileMfccs.Count == 0)
            {
                RebuildAcousticIndex();
            }

            if (_cachedProfileMfccs.Count == 0) return result;

            var inputFeatures = AudioFeatureExtractor.ExtractFromFile(wavFilePath);
            if (inputFeatures == null || inputFeatures.MfccCoefficients == null) return result;

            double maxSimilarity = 0.0;
            string bestPhrase = string.Empty;
            VoiceSample? bestSample = null;

            foreach (var kvp in _cachedProfileMfccs)
            {
                double similarity = AudioFeatureExtractor.CosineSimilarity(inputFeatures.MfccCoefficients, kvp.Value);
                if (similarity > maxSimilarity)
                {
                    maxSimilarity = similarity;
                    string[] parts = kvp.Key.Split(':');
                    string sampleId = parts[0];
                    bestPhrase = parts.Length > 1 ? parts[1] : string.Empty;
                    bestSample = VoiceTrainerManager.Profile.Samples.Find(s => s.Id == sampleId);
                }
            }

            result.Confidence = Math.Round(maxSimilarity, 3);
            result.MatchedPhrase = bestPhrase;
            result.BestSample = bestSample;
            result.IsMatched = maxSimilarity >= threshold;

            DebugConsoleOverlay.Log("Acoustic ML Match", $"Match: \"{bestPhrase}\" ({result.Confidence * 100:F1}% similarity)");
            return result;
        }
    }
}
