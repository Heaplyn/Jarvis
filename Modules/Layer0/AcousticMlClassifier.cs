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

            // 1. Load official samples from VoiceTrainerManager (Golden Set)
            var profileSamples = VoiceTrainerManager.Profile.Samples;
            foreach (var sample in profileSamples)
            {
                if (File.Exists(sample.AudioFilePath))
                {
                    var features = AudioFeatureExtractor.ExtractFromFile(sample.AudioFilePath);
                    if (features != null && features.MfccCoefficients != null)
                    {
                        string key = $"TRAINER:{sample.Id}:{sample.Phrase}";
                        _cachedProfileMfccs[key] = features.MfccCoefficients;
                    }
                }
            }

            // 2. Load historical logs from VoiceDatasetManager (Self-Learning Set)
            var datasetRecords = VoiceDatasetManager.DatasetRecords;
            foreach (var rec in datasetRecords)
            {
                if (File.Exists(rec.FilePath) && !string.IsNullOrWhiteSpace(rec.Transcript) && rec.Transcript != "...")
                {
                    // Only index successfully captured audio
                    var features = AudioFeatureExtractor.ExtractFromFile(rec.FilePath);
                    if (features != null && features.MfccCoefficients != null)
                    {
                        // Use filename hash as a pseudo-id for uniqueness in the index
                        string pseudoId = rec.FileName.GetHashCode().ToString("X");
                        string key = $"DATASET:{pseudoId}:{rec.Transcript}";
                        _cachedProfileMfccs[key] = features.MfccCoefficients;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"🧠 Rebuilt Acoustic ML Index with {_cachedProfileMfccs.Count} feature vectors (Golden + Historical).");
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
                    // Key format: SOURCE:ID:PHRASE
                    bestPhrase = parts.Length > 2 ? parts[2] : (parts.Length > 1 ? parts[1] : string.Empty);

                    if (parts[0] == "TRAINER")
                    {
                        string sampleId = parts[1];
                        bestSample = VoiceTrainerManager.Profile.Samples.Find(s => s.Id == sampleId);
                    }
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
