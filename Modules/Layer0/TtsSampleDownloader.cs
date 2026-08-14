// Developer: heaplyn
// Date: 2026-08-14
// Summary: Manages local custom TTS audio files. Removed GitHub cloud fetching for privacy and speed.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;

namespace JarvisLauncher
{
    public class TtsVoiceItem
    {
        public string name { get; set; } = string.Empty;
        public string path { get; set; } = string.Empty;
    }

    public static class TtsSampleDownloader
    {
        public static readonly string VoiceDirectory = Path.Combine(PathHandler.GetDataDirectory(), "TtsVoices");
        private static MediaPlayer? _previewPlayer;

        static TtsSampleDownloader()
        {
            if (!Directory.Exists(VoiceDirectory))
            {
                Directory.CreateDirectory(VoiceDirectory);
            }
        }

        /// <summary>
        /// Scans the local Data/TtsVoices directory for imported audio files.
        /// </summary>
        public static List<TtsVoiceItem> GetLocalVoiceFiles()
        {
            var voices = new List<TtsVoiceItem>();
            try
            {
                if (Directory.Exists(VoiceDirectory))
                {
                    var files = Directory.GetFiles(VoiceDirectory, "*.*");
                    foreach (var file in files)
                    {
                        string ext = Path.GetExtension(file).ToLower();
                        if (ext == ".mp3" || ext == ".wav" || ext == ".m4a" || ext == ".ogg")
                        {
                            voices.Add(new TtsVoiceItem { name = Path.GetFileName(file), path = file });
                        }
                    }
                }
            }
            catch { }
            return voices;
        }

        public static void PreviewLocalFile(string filePath)
        {
            if (!File.Exists(filePath)) return;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _previewPlayer?.Stop();
                _previewPlayer = new MediaPlayer();
                _previewPlayer.Open(new Uri(filePath, UriKind.Absolute));
                _previewPlayer.Play();
            });
        }

        public static void ImportUserCustomVoiceFile(string absolutePath)
        {
            if (!File.Exists(absolutePath))
            {
                TextOverlay.Show("⚠️ Selected file does not exist.", 2500);
                return;
            }

            try
            {
                string ext = Path.GetExtension(absolutePath).ToLower();
                if (ext != ".mp3" && ext != ".wav" && ext != ".m4a" && ext != ".ogg")
                {
                    TextOverlay.Show("⚠️ Unsupported format. Use MP3, WAV, M4A, or OGG.", 3500);
                    return;
                }

                string fileName = Path.GetFileName(absolutePath);
                string destPath = Path.Combine(VoiceDirectory, "User_" + fileName);

                File.Copy(absolutePath, destPath, true);

                SettingsManager.Current.CustomTtsSamplePath = destPath;
                SettingsManager.Current.CustomTtsVoiceName = "Custom: " + fileName;
                SettingsManager.Save();

                TextOverlay.Show($"✅ Custom user file imported:\n{fileName}", 3000);
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"❌ Import failed: {ex.Message}", 3000);
            }
        }
    }
}
