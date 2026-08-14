// Developer: heaplyn
// Date: 2026-08-14
// Summary: Hierarchical Notes Manager. Handles loading/saving notes organized into folders (categories).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JarvisLauncher
{
    public class NoteItem
    {
        public string Name { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
        public List<NoteItem> Children { get; set; } = new List<NoteItem>();
    }

    public static class NotesManager
    {
        private static string NotesDir => Path.Combine(PathHandler.GetDataDirectory(), "Notes");

        static NotesManager()
        {
            if (!Directory.Exists(NotesDir))
            {
                Directory.CreateDirectory(NotesDir);
            }
        }

        public static string GetNotesDirectory() => NotesDir;

        public static List<NoteItem> GetHierarchy()
        {
            return GetItemsRecursive(NotesDir, "");
        }

        private static List<NoteItem> GetItemsRecursive(string fullPath, string relative)
        {
            var items = new List<NoteItem>();
            try
            {
                // Add directories first
                foreach (var dir in Directory.GetDirectories(fullPath))
                {
                    string name = Path.GetFileName(dir);
                    string rel = Path.Combine(relative, name);
                    items.Add(new NoteItem
                    {
                        Name = name,
                        RelativePath = rel,
                        IsFolder = true,
                        Children = GetItemsRecursive(dir, rel)
                    });
                }

                // Add .txt and .md files
                foreach (var file in Directory.GetFiles(fullPath, "*.*")
                    .Where(f => f.EndsWith(".txt") || f.EndsWith(".md")))
                {
                    string name = Path.GetFileName(file);
                    items.Add(new NoteItem
                    {
                        Name = name,
                        RelativePath = Path.Combine(relative, name),
                        IsFolder = false
                    });
                }
            }
            catch { }
            return items.OrderBy(i => !i.IsFolder).ThenBy(i => i.Name).ToList();
        }

        public static void CreateCategory(string parentRelativePath, string categoryName)
        {
            string targetDir = Path.Combine(NotesDir, parentRelativePath, categoryName);
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
        }

        public static string CreateNote(string categoryRelativePath, string noteName)
        {
            if (!noteName.EndsWith(".txt") && !noteName.EndsWith(".md"))
            {
                noteName += ".txt";
            }

            string path = Path.Combine(NotesDir, categoryRelativePath, noteName);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, $"# {Path.GetFileNameWithoutExtension(noteName)}\n\nCreated: {DateTime.Now:F}");
            }
            return path;
        }

        public static void SaveNote(string relativePath, string content)
        {
            string fullPath = Path.Combine(NotesDir, relativePath);
            File.WriteAllText(fullPath, content);
        }

        public static string LoadNote(string relativePath)
        {
            string fullPath = Path.Combine(NotesDir, relativePath);
            return File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
        }

        public static void DeleteItem(string relativePath)
        {
            string fullPath = Path.Combine(NotesDir, relativePath);
            if (File.Exists(fullPath)) File.Delete(fullPath);
            else if (Directory.Exists(fullPath)) Directory.Delete(fullPath, true);
        }
    }
}
