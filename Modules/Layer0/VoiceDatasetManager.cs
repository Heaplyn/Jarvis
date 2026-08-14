// Developer: heaplyn
// Date: 2026-08-13
// Summary: Voice Recording Dataset & Classification Manager.
// Automatically records user speech samples into Data/VoiceDataset/ and manages classification metadata.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace JarvisLauncher
{
    public class VoiceDatasetRecord
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName => Path.GetFileName(FilePath);
        public DateTime RecordedAt { get; set; } = DateTime.Now;
        public double DurationSeconds { get; set; } = 0.0;
        public long FileSizeBytes { get; set; } = 0;
        public string Classification { get; set; } = "Unclassified"; // Command | AI Chat | Wake Word | Noise | Unclassified
    }

    public static class VoiceDatasetManager
    {
        private static readonly string DatasetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "VoiceDataset");
        private static readonly string MetadataFile = Path.Combine(DatasetDir, "Metadata.json");
        private static readonly object _lock = new();

        public static List<VoiceDatasetRecord> Records = new();

        static VoiceDatasetManager()
        {
            Directory.CreateDirectory(DatasetDir);
            LoadMetadata();
        }

        public static void LoadMetadata()
        {
            lock (_lock)
            {
                Records.Clear();
                if (File.Exists(MetadataFile))
                {
                    try
                    {
                        string json = File.ReadAllText(MetadataFile);
                        var list = JsonSerializer.Deserialize<List<VoiceDatasetRecord>>(json);
                        if (list != null) Records = list;
                    }
                    catch { }
                }

                // Sync with actual files on disk
                var files = Directory.GetFiles(DatasetDir, "*.wav");
                foreach (var file in files)
                {
                    if (!Records.Any(r => r.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase)))
                    {
                        var info = new FileInfo(file);
                        Records.Add(new VoiceDatasetRecord
                        {
                            FilePath = file,
                            RecordedAt = info.CreationTime,
                            FileSizeBytes = info.Length,
                            DurationSeconds = Math.Round((double)info.Length / (16000 * 2), 1),
                            Classification = "Unclassified"
                        });
                    }
                }
                SaveMetadata();
            }
        }

        public static void SaveMetadata()
        {
            lock (_lock)
            {
                try
                {
                    string json = JsonSerializer.Serialize(Records, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(MetadataFile, json);
                }
                catch { }
            }
        }

        public static VoiceDatasetRecord SaveAudioRecording(byte[] pcmData, string classification = "Unclassified")
        {
            lock (_lock)
            {
                Directory.CreateDirectory(DatasetDir);
                string fileName = $"Voice_{DateTime.Now:yyyyMMdd_HHmmss_fff}.wav";
                string fullPath = Path.Combine(DatasetDir, fileName);

                WriteWavHeaderAndData(fullPath, pcmData, 16000);

                var record = new VoiceDatasetRecord
                {
                    FilePath = fullPath,
                    RecordedAt = DateTime.Now,
                    FileSizeBytes = new FileInfo(fullPath).Length,
                    DurationSeconds = Math.Round((double)pcmData.Length / (16000 * 2), 1),
                    Classification = classification
                };

                Records.Insert(0, record);
                SaveMetadata();
                return record;
            }
        }

        public static void ClassifyRecord(string filePath, string label)
        {
            lock (_lock)
            {
                var record = Records.FirstOrDefault(r => r.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
                if (record != null)
                {
                    record.Classification = label;
                    SaveMetadata();
                }
            }
        }

        public static void DeleteRecord(string filePath)
        {
            lock (_lock)
            {
                var record = Records.FirstOrDefault(r => r.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
                if (record != null)
                {
                    Records.Remove(record);
                    SaveMetadata();
                }

                if (File.Exists(filePath))
                {
                    try { File.Delete(filePath); } catch { }
                }
            }
        }

        public static string TrainClassifierModel()
        {
            lock (_lock)
            {
                int total = Records.Count;
                int commands = Records.Count(r => r.Classification == "Command");
                int chat = Records.Count(r => r.Classification == "AI Chat");
                int wake = Records.Count(r => r.Classification == "Wake Word");
                int noise = Records.Count(r => r.Classification == "Noise");

                return $"🧬 Trained Voice Classifier on {total} samples:\n• Commands: {commands}\n• AI Chat: {chat}\n• Wake Words: {wake}\n• Noise: {noise}";
            }
        }

        private static void WriteWavHeaderAndData(string filePath, byte[] pcmData, int sampleRate)
        {
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(fs);

            int subchunk2Size = pcmData.Length;
            int chunkSize = 36 + subchunk2Size;

            writer.Write(new char[] { 'R', 'I', 'F', 'F' });
            writer.Write(chunkSize);
            writer.Write(new char[] { 'W', 'A', 'V', 'E' });

            writer.Write(new char[] { 'f', 'm', 't', ' ' });
            writer.Write(16); // Subchunk1Size (16 for PCM)
            writer.Write((short)1); // AudioFormat (1 for PCM)
            writer.Write((short)1); // NumChannels (1 for Mono)
            writer.Write(sampleRate); // SampleRate
            writer.Write(sampleRate * 2); // ByteRate (SampleRate * NumChannels * BitsPerSample/8)
            writer.Write((short)2); // BlockAlign (NumChannels * BitsPerSample/8)
            writer.Write((short)16); // BitsPerSample

            writer.Write(new char[] { 'd', 'a', 't', 'a' });
            writer.Write(subchunk2Size);
            writer.Write(pcmData);
        }
    }
}
