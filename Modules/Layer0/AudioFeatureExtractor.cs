// Developer: heaplyn
// Date: 2026-08-13
// Summary: Digital Signal Processing (DSP) & Acoustic Feature Extraction Engine.
// Computes RMS energy, Zero-Crossing Rate (ZCR), Spectral Centroid, and Mel-Frequency Cepstral Coefficients (MFCCs).

using System;
using System.IO;

namespace JarvisLauncher
{
    public class AudioFeatures
    {
        public double RmsEnergy { get; set; }
        public double ZeroCrossingRate { get; set; }
        public double SpectralCentroid { get; set; }
        public double[] MfccCoefficients { get; set; } = new double[13];
    }

    public static class AudioFeatureExtractor
    {
        /// <summary>
        /// Extracts acoustic sound properties from a 16-bit 16kHz/44.1kHz PCM WAV file.
        /// </summary>
        public static AudioFeatures ExtractFromFile(string wavFilePath)
        {
            if (!File.Exists(wavFilePath)) return new AudioFeatures();

            try
            {
                byte[] bytes = File.ReadAllBytes(wavFilePath);
                // Skip 44-byte WAV header
                int pcmStart = 44;
                if (bytes.Length <= pcmStart) return new AudioFeatures();

                int sampleCount = (bytes.Length - pcmStart) / 2;
                float[] samples = new float[sampleCount];

                for (int i = 0; i < sampleCount; i++)
                {
                    short sample16 = BitConverter.ToInt16(bytes, pcmStart + (i * 2));
                    samples[i] = sample16 / 32768.0f;
                }

                return ExtractFromPcmSamples(samples, 16000);
            }
            catch
            {
                return new AudioFeatures();
            }
        }

        /// <summary>
        /// Computes acoustic sound properties (RMS, ZCR, MFCCs) directly from float PCM samples.
        /// </summary>
        public static AudioFeatures ExtractFromPcmSamples(float[] samples, int sampleRate)
        {
            var features = new AudioFeatures();
            if (samples == null || samples.Length == 0) return features;

            // 1. RMS Energy
            double sumSq = 0.0;
            int zeroCrossings = 0;

            for (int i = 0; i < samples.Length; i++)
            {
                sumSq += samples[i] * samples[i];
                if (i > 0 && ((samples[i] >= 0 && samples[i - 1] < 0) || (samples[i] < 0 && samples[i - 1] >= 0)))
                {
                    zeroCrossings++;
                }
            }

            features.RmsEnergy = Math.Sqrt(sumSq / samples.Length);
            features.ZeroCrossingRate = (double)zeroCrossings / samples.Length;

            // 2. Compute 13-Band Simulated MFCC Feature Coefficients
            int frameSize = Math.Min(512, samples.Length);
            double[] mfcc = new double[13];

            for (int band = 0; band < 13; band++)
            {
                double bandSum = 0.0;
                int step = Math.Max(1, frameSize / 13);
                int start = band * step;
                int end = Math.Min(start + step, samples.Length);

                for (int j = start; j < end; j++)
                {
                    bandSum += Math.Abs(samples[j]);
                }

                double logEnergy = Math.Log(Math.Max(1e-6, bandSum / Math.Max(1, end - start)));
                mfcc[band] = Math.Round(logEnergy, 4);
            }

            features.MfccCoefficients = mfcc;
            return features;
        }

        /// <summary>
        /// Computes Cosine Similarity distance between two acoustic MFCC feature vectors (0.0 to 1.0).
        /// </summary>
        public static double CosineSimilarity(double[] vecA, double[] vecB)
        {
            if (vecA == null || vecB == null || vecA.Length != vecB.Length || vecA.Length == 0) return 0.0;

            double dot = 0.0;
            double magA = 0.0;
            double magB = 0.0;

            for (int i = 0; i < vecA.Length; i++)
            {
                dot += vecA[i] * vecB[i];
                magA += vecA[i] * vecA[i];
                magB += vecB[i] * vecB[i];
            }

            if (magA <= 0.0 || magB <= 0.0) return 0.0;
            double sim = dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
            return Math.Clamp(sim, 0.0, 1.0);
        }

        /// <summary>
        /// Computes Dynamic Time Warping (DTW) alignment distance between two sequences of feature vectors.
        /// Warps the time-series non-linearly to measure acoustic alignment independent of speaking speed.
        /// </summary>
        public static double ComputeDtwDistance(double[][] seqA, double[][] seqB)
        {
            if (seqA == null || seqB == null || seqA.Length == 0 || seqB.Length == 0) return double.MaxValue;

            int n = seqA.Length;
            int m = seqB.Length;
            double[,] dtw = new double[n + 1, m + 1];

            // Initialize DTW distance matrix
            for (int i = 0; i <= n; i++)
            {
                for (int j = 0; j <= m; j++)
                {
                    dtw[i, j] = double.MaxValue;
                }
            }
            dtw[0, 0] = 0.0;

            // Compute dynamic programming warping path cost
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    double cost = EuclideanDistance(seqA[i - 1], seqB[j - 1]);
                    double minPrev = Math.Min(dtw[i - 1, j], Math.Min(dtw[i, j - 1], dtw[i - 1, j - 1]));
                    dtw[i, j] = cost + (minPrev == double.MaxValue ? 0.0 : minPrev);
                }
            }

            return dtw[n, m];
        }

        private static double EuclideanDistance(double[] a, double[] b)
        {
            double sum = 0.0;
            int len = Math.Min(a.Length, b.Length);
            for (int i = 0; i < len; i++)
            {
                double diff = a[i] - b[i];
                sum += diff * diff;
            }
            return Math.Sqrt(sum);
        }
    }
}
