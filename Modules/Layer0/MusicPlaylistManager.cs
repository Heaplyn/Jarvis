// Developer: heaplyn
// Date: 2026-08-09
// Summary: Data models and persistence manager for music playlists, custom folders, track metadata, and online stream links inside Data/MusicPlaylists.json.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace JarvisLauncher
{
    public class MusicTrack
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = "Unknown Artist";
        public string PathOrUrl { get; set; } = string.Empty; // Local .mp3/.wav/.flac path OR web stream URL
        public bool IsStreamUrl { get; set; } = false;
        public DateTime AddedAt { get; set; } = DateTime.Now;
    }

    public class MusicFolder
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FolderName { get; set; } = "Default Playlist";
        public List<MusicTrack> Tracks { get; set; } = new List<MusicTrack>();
    }

    public class MusicLibraryData
    {
        public List<MusicFolder> Folders { get; set; } = new List<MusicFolder>();
        public string LastActiveFolderId { get; set; } = string.Empty;
    }

    public static class MusicPlaylistManager
    {
        private static string GetFilePath()
        {
            string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            return Path.Combine(dataDir, "MusicPlaylists.json");
        }

        public static MusicLibraryData LoadLibrary()
        {
            try
            {
                string p = GetFilePath();
                if (File.Exists(p))
                {
                    string json = File.ReadAllText(p);
                    var data = JsonSerializer.Deserialize<MusicLibraryData>(json);
                    if (data != null && data.Folders.Count > 0) return data;
                }
            }
            catch { }

            // Default initial setup
            var defaultLibrary = new MusicLibraryData();
            var defaultFolder = new MusicFolder { FolderName = "Favorites" };
            defaultLibrary.Folders.Add(defaultFolder);
            defaultLibrary.LastActiveFolderId = defaultFolder.Id;
            SaveLibrary(defaultLibrary);
            return defaultLibrary;
        }

        public static void SaveLibrary(MusicLibraryData data)
        {
            try
            {
                string p = GetFilePath();
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(p, json);
            }
            catch { }
        }
    }
}
