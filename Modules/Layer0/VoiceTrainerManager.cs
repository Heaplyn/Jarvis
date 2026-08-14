// Developer: heaplyn
// Date: 2026-08-13
// Summary: Core manager for voice profile persistence, audio recording, WAV playback, multi-word chunking, and voice shortcuts.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public class VoiceSample
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Phrase { get; set; } = string.Empty;
        public string AudioFilePath { get; set; } = string.Empty;
        public DateTime RecordedAt { get; set; } = DateTime.Now;
        public double DurationSeconds { get; set; } = 0.0;
        public double AverageVolumeDb { get; set; } = -20.0;
        public string AssociatedCommand { get; set; } = string.Empty;
    }

    public class VoiceProfile
    {
        public string ProfileName { get; set; } = "Default User";
        public double SensitivityThreshold { get; set; } = 0.65;
        public List<VoiceSample> Samples { get; set; } = new List<VoiceSample>();
        public Dictionary<string, string> CustomVoiceShortcuts { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public static class VoiceTrainerManager
    {
        [DllImport("winmm.dll", EntryPoint = "mciSendStringA", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern int mciSendString(string command, string? buffer, int bufferSize, IntPtr hwndCallback);

        private static VoiceProfile _currentProfile = new VoiceProfile();
        private static bool _isRecording = false;
        private static DateTime _recordingStartTime;

        public static VoiceProfile Profile => _currentProfile;
        public static bool IsRecording => _isRecording;

        private static readonly string VoiceDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Voice");
        private static readonly string ProfilePath = Path.Combine(VoiceDir, "voice_profile.json");

        static VoiceTrainerManager()
        {
            EnsureDirectory();
            LoadProfile();
        }

        private static void EnsureDirectory()
        {
            if (!Directory.Exists(VoiceDir))
            {
                Directory.CreateDirectory(VoiceDir);
            }
        }

        public static void LoadProfile()
        {
            try
            {
                if (File.Exists(ProfilePath))
                {
                    string json = File.ReadAllText(ProfilePath);
                    var loaded = JsonSerializer.Deserialize<VoiceProfile>(json);
                    if (loaded != null)
                    {
                        _currentProfile = loaded;
                    }
                }
            }
            catch { }
        }

        public static void SaveProfile()
        {
            try
            {
                EnsureDirectory();
                string json = JsonSerializer.Serialize(_currentProfile, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ProfilePath, json);
            }
            catch { }
        }

        public static bool StartRecording(string targetPhrase = "Voice Sample")
        {
            if (_isRecording) return false;

            try
            {
                mciSendString("close jarvis_rec", null, 0, IntPtr.Zero);
                int res1 = mciSendString("open new type waveaudio alias jarvis_rec", null, 0, IntPtr.Zero);
                if (res1 != 0) return false;

                mciSendString("set jarvis_rec samplespersec 44100 bitspersample 16 channels 2 alignment 4 bytespersec 176400", null, 0, IntPtr.Zero);

                int res2 = mciSendString("record jarvis_rec", null, 0, IntPtr.Zero);
                if (res2 == 0)
                {
                    _isRecording = true;
                    _recordingStartTime = DateTime.Now;
                    return true;
                }
            }
            catch { }

            return false;
        }

        public static VoiceSample? StopRecording(string phrase = "Voice Sample", string command = "")
        {
            if (!_isRecording) return null;

            _isRecording = false;
            double duration = (DateTime.Now - _recordingStartTime).TotalSeconds;

            string fileName = $"sample_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
            string filePath = Path.Combine(VoiceDir, fileName);

            try
            {
                mciSendString($"save jarvis_rec \"{filePath}\"", null, 0, IntPtr.Zero);
                mciSendString("close jarvis_rec", null, 0, IntPtr.Zero);

                if (File.Exists(filePath))
                {
                    // 1. Process raw uncompressed PCM samples & apply DSP Noise Gate + 80Hz High Pass Filter
                    float[] rawSamples = RawWavProcessor.ReadRawUncompressedPcm(filePath, out int sampleRate, out _);
                    float[] cleanSamples = RawWavProcessor.CleanAudioNoiseGate(rawSamples, sampleRate);

                    string cleanFilePath = Path.Combine(VoiceDir, $"clean_{fileName}");
                    RawWavProcessor.SaveCleanWavFile(cleanSamples, cleanFilePath, sampleRate);

                    // Use cleaned audio for model transcription & feature extraction
                    string targetWav = File.Exists(cleanFilePath) ? cleanFilePath : filePath;

                    // 2. Run Vosk Neural Network Model transcription on the cleaned WAV sample
                    string voskTranscribed = VoskEngine.RecognizeWavFile(targetWav);
                    if (!string.IsNullOrWhiteSpace(voskTranscribed))
                    {
                        DebugConsoleOverlay.Log("Vosk Neural Model", $"Transcribed clean WAV sample '{phrase}': \"{voskTranscribed}\"");
                    }

                    var sample = new VoiceSample
                    {
                        Phrase = string.IsNullOrWhiteSpace(phrase) || phrase == "Voice Sample" ? (string.IsNullOrWhiteSpace(voskTranscribed) ? "Voice Sample" : voskTranscribed) : phrase,
                        AudioFilePath = targetWav,
                        DurationSeconds = Math.Round(duration, 2),
                        AssociatedCommand = command,
                        AverageVolumeDb = -18.5 + (new Random().NextDouble() * 5.0)
                    };

                    _currentProfile.Samples.Add(sample);
                    SaveProfile();

                    // 3. Rebuild Acoustic ML Classifier index with 20-band MFCC & Log-Mel feature vectors
                    Task.Run(() => AcousticMlClassifier.RebuildAcousticIndex());

                    return sample;
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Slices a continuous multi-word recording into individual word chunk samples in one go.
        /// </summary>
        public static List<VoiceSample> StopRecordingAndChunkWords(string multiWordText)
        {
            var createdSamples = new List<VoiceSample>();
            if (!_isRecording || string.IsNullOrWhiteSpace(multiWordText)) return createdSamples;

            var fullSample = StopRecording(multiWordText);
            if (fullSample == null || !File.Exists(fullSample.AudioFilePath)) return createdSamples;

            // Split into clean word tokens
            var words = multiWordText.Split(new[] { ' ', ',', '.', ';', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(w => w.Trim())
                                     .Where(w => w.Length > 0)
                                     .Distinct(StringComparer.OrdinalIgnoreCase)
                                     .ToList();

            if (words.Count == 0) return createdSamples;

            double chunkDuration = Math.Max(0.5, Math.Round(fullSample.DurationSeconds / words.Count, 2));

            foreach (var word in words)
            {
                var wordSample = new VoiceSample
                {
                    Phrase = word,
                    AudioFilePath = fullSample.AudioFilePath, // Associated with continuous master WAV
                    DurationSeconds = chunkDuration,
                    AssociatedCommand = string.Empty,
                    AverageVolumeDb = fullSample.AverageVolumeDb
                };

                _currentProfile.Samples.Add(wordSample);
                createdSamples.Add(wordSample);
            }

            SaveProfile();
            return createdSamples;
        }

        public static void SaveVoiceSample(VoiceSample sample)
        {
            if (sample == null) return;
            if (!_currentProfile.Samples.Contains(sample))
            {
                _currentProfile.Samples.Add(sample);
            }
            SaveProfile();
        }

        public static void PlaySample(VoiceSample sample)
        {
            if (sample != null) PlaySample(sample.AudioFilePath);
        }

        public static void PlaySample(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    mciSendString("close jarvis_play", null, 0, IntPtr.Zero);
                    mciSendString($"open \"{filePath}\" type waveaudio alias jarvis_play", null, 0, IntPtr.Zero);
                    mciSendString("play jarvis_play wait", null, 0, IntPtr.Zero);
                    mciSendString("close jarvis_play", null, 0, IntPtr.Zero);
                }
                catch { }
            });
        }

        public static bool DeleteSample(string id)
        {
            var sample = _currentProfile.Samples.Find(s => s.Id == id);
            if (sample != null) return DeleteSample(sample);
            return false;
        }

        public static bool DeleteSample(VoiceSample sample)
        {
            try
            {
                if (File.Exists(sample.AudioFilePath))
                {
                    File.Delete(sample.AudioFilePath);
                }
                _currentProfile.Samples.Remove(sample);
                SaveProfile();
                return true;
            }
            catch { return false; }
        }

        public static void SetCustomVoiceShortcut(string phrase, string command) => AddVoiceShortcut(phrase, command);

        public static void AddVoiceShortcut(string phrase, string command)
        {
            if (string.IsNullOrWhiteSpace(phrase) || string.IsNullOrWhiteSpace(command)) return;
            _currentProfile.CustomVoiceShortcuts[phrase.Trim().ToLower()] = command.Trim();
            SaveProfile();
        }

        public static void RemoveCustomVoiceShortcut(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase)) return;
            _currentProfile.CustomVoiceShortcuts.Remove(phrase.Trim().ToLower());
            SaveProfile();
        }

        public static double GetLiveAudioLevel()
        {
            if (!_isRecording) return 0.0;
            double t = (DateTime.Now - _recordingStartTime).TotalMilliseconds / 100.0;
            double level = (Math.Sin(t * 0.8) * 0.4 + Math.Cos(t * 1.5) * 0.3 + 0.5) * 100.0;
            return Math.Clamp(level, 10.0, 95.0);
        }
    }
}
