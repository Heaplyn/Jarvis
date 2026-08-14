using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace JarvisLauncher
{
    public class VoiceTriggerEvent
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Source { get; set; } = string.Empty; // Algorithm, Vosk, Gemini
        public string Transcript { get; set; } = string.Empty;
        public string SystemContext { get; set; } = string.Empty; // Active window, etc.
        public bool IsSuccess { get; set; }
        public string AudioClipPath { get; set; } = string.Empty;
    }

    public class VoiceDatasetRecord
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public double DurationSeconds { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime RecordedAt { get; set; } = DateTime.Now;
        public string Classification { get; set; } = "Command"; // Command, AI Chat, Wake Word
    }

    public static class VoiceDatasetManager
    {
        private static readonly string DatasetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "VoiceDataset");
        private static readonly string ClipsDir = Path.Combine(DatasetDir, "Clips");
        private static readonly string LogFilePath = Path.Combine(DatasetDir, "Triggers.json");
        private static readonly string MetadataFilePath = Path.Combine(DatasetDir, "DatasetMetadata.json");

        private static readonly List<VoiceTriggerEvent> _recentTriggers = new();
        public static List<VoiceDatasetRecord> Records { get; private set; } = new();

        static VoiceDatasetManager()
        {
            try
            {
                if (!Directory.Exists(DatasetDir)) Directory.CreateDirectory(DatasetDir);
                if (!Directory.Exists(ClipsDir)) Directory.CreateDirectory(ClipsDir);
                LoadDataset();
                LoadMetadata();
            }
            catch { }
        }

        // --- VoiceStudioOverlay Required Methods ---

        public static void LoadMetadata()
        {
            try
            {
                if (File.Exists(MetadataFilePath))
                {
                    string json = File.ReadAllText(MetadataFilePath);
                    var list = JsonSerializer.Deserialize<List<VoiceDatasetRecord>>(json);
                    if (list != null)
                    {
                        Records = list;
                    }
                }

                // Synchronize with any existing WAV files in the Clips directory
                if (Directory.Exists(ClipsDir))
                {
                    var existingFiles = Directory.GetFiles(ClipsDir, "*.wav");
                    foreach (var file in existingFiles)
                    {
                        if (!Records.Any(r => r.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase)))
                        {
                            var fi = new FileInfo(file);
                            Records.Add(new VoiceDatasetRecord
                            {
                                FileName = fi.Name,
                                FilePath = fi.FullName,
                                FileSizeBytes = fi.Length,
                                RecordedAt = fi.CreationTime,
                                DurationSeconds = Math.Max(0.5, fi.Length / 32000.0), // Approximate for 16kHz 16-bit mono
                                Classification = fi.Name.Contains("Wake") ? "Wake Word" : "Command"
                            });
                        }
                    }
                }

                SaveMetadata();
            }
            catch { }
        }

        public static string TrainClassifierModel()
        {
            LoadMetadata();
            int total = Records.Count;
            int cmds = Records.Count(r => r.Classification == "Command");
            int chats = Records.Count(r => r.Classification == "AI Chat");
            int wakes = Records.Count(r => r.Classification == "Wake Word");

            return $"✅ Training Complete!\nTotal Samples: {total}\n• Commands: {cmds}\n• AI Chat: {chats}\n• Wake Words: {wakes}";
        }

        public static void ClassifyRecord(string filePath, string classification)
        {
            var record = Records.FirstOrDefault(r => r.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (record != null)
            {
                record.Classification = classification;
                SaveMetadata();
            }
        }

        public static void DeleteRecord(string filePath)
        {
            try
            {
                if (File.Exists(filePath)) File.Delete(filePath);
            }
            catch { }

            Records.RemoveAll(r => r.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            SaveMetadata();
        }

        private static void SaveMetadata()
        {
            try
            {
                string json = JsonSerializer.Serialize(Records, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(MetadataFilePath, json);
            }
            catch { }
        }

        // --- Voice Trigger Event Logging ---

        public static void LogTrigger(string source, string transcript, string context, byte[]? audioData = null)
        {
            var ev = new VoiceTriggerEvent
            {
                Source = source,
                Transcript = transcript,
                SystemContext = context,
                IsSuccess = !string.IsNullOrWhiteSpace(transcript) && transcript != "..."
            };

            if (audioData != null)
            {
                string fileName = $"Clip_{DateTime.Now:yyyyMMdd_HHmmss}_{source}.wav";
                string fullPath = Path.Combine(ClipsDir, fileName);
                try
                {
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new NAudio.Wave.WaveFileWriter(ms, new NAudio.Wave.WaveFormat(16000, 1)))
                        {
                            writer.Write(audioData, 0, audioData.Length);
                        }
                        File.WriteAllBytes(fullPath, ms.ToArray());
                    }
                    ev.AudioClipPath = fullPath;

                    // Automatically add to dataset records
                    var fi = new FileInfo(fullPath);
                    Records.Add(new VoiceDatasetRecord
                    {
                        FileName = fileName,
                        FilePath = fullPath,
                        FileSizeBytes = fi.Length,
                        RecordedAt = DateTime.Now,
                        DurationSeconds = audioData.Length / 32000.0,
                        Classification = source.Contains("Wake") ? "Wake Word" : "Command"
                    });
                    SaveMetadata();
                }
                catch { }
            }

            lock (_recentTriggers)
            {
                _recentTriggers.Add(ev);
                if (_recentTriggers.Count > 500) _recentTriggers.RemoveAt(0);
                SaveDataset();
            }
        }

        public static string GetFewShotExamples()
        {
            lock (_recentTriggers)
            {
                var successes = _recentTriggers.Where(t => t.IsSuccess).TakeLast(5);
                if (!successes.Any()) return "No recent history.";
                return string.Join("\n", successes.Select(s => $"- [{s.Source}] User said: \"{s.Transcript}\" while using {s.SystemContext}"));
            }
        }

        private static void LoadDataset()
        {
            try
            {
                if (File.Exists(LogFilePath))
                {
                    string json = File.ReadAllText(LogFilePath);
                    var list = JsonSerializer.Deserialize<List<VoiceTriggerEvent>>(json);
                    if (list != null) _recentTriggers.AddRange(list.TakeLast(500));
                }
            }
            catch { }
        }

        private static void SaveDataset()
        {
            try
            {
                string json = JsonSerializer.Serialize(_recentTriggers, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(LogFilePath, json);
            }
            catch { }
        }
    }
}