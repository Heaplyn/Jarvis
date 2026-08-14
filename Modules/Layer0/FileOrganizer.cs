// Developer: copilot
// Date: 2026-08-13
// Summary: High-performance file organization algorithms, including MD5 hashing duplicate checks, extension grouping, date archiving, and size audits.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace JarvisLauncher
{
    public static class FileOrganizer
    {
        // 1. EXTENSION CLUSTERING ALGORITHM
        public static List<string> CategorizeByExtension(string targetDir, bool dryRun)
        {
            var log = new List<string>();
            if (!Directory.Exists(targetDir))
            {
                log.Add($"⚠️ Directory does not exist: {targetDir}");
                return log;
            }

            var categoryMapping = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Images", new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".ico", ".tiff" } },
                { "Documents", new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".rtf", ".md" } },
                { "Video", new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm" } },
                { "Audio", new[] { ".mp3", ".wav", ".wma", ".ogg", ".m4a", ".flac" } },
                { "Archives", new[] { ".zip", ".rar", ".7z", ".tar", ".gz" } },
                { "Code", new[] { ".cs", ".py", ".js", ".html", ".css", ".json", ".xml", ".cpp", ".h", ".lua", ".sh", ".bat", ".ps1" } },
                { "Executables", new[] { ".exe", ".msi" } }
            };

            var files = Directory.GetFiles(targetDir);
            foreach (var file in files)
            {
                string ext = Path.GetExtension(file);
                if (string.IsNullOrEmpty(ext)) continue;

                string folderName = "Other";
                foreach (var category in categoryMapping)
                {
                    if (category.Value.Contains(ext))
                    {
                        folderName = category.Key;
                        break;
                    }
                }

                string destFolder = Path.Combine(targetDir, folderName);
                string destFile = Path.Combine(destFolder, Path.GetFileName(file));

                log.Add($"[Clustering] Move \"{Path.GetFileName(file)}\" ➔ \\{folderName}\\");

                if (!dryRun)
                {
                    try
                    {
                        if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);
                        if (File.Exists(destFile))
                        {
                            string newName = Path.GetFileNameWithoutExtension(file) + "_" + Guid.NewGuid().ToString().Substring(0, 5) + ext;
                            destFile = Path.Combine(destFolder, newName);
                        }
                        File.Move(file, destFile);
                    }
                    catch (Exception ex)
                    {
                        log.Add($"  ❌ Error moving {Path.GetFileName(file)}: {ex.Message}");
                    }
                }
            }

            if (log.Count == 0) log.Add("No files to categorize.");
            return log;
        }

        // 2. DATE-BASED ARCHIVING ALGORITHM
        public static List<string> OrganizeByDate(string targetDir, bool dryRun)
        {
            var log = new List<string>();
            if (!Directory.Exists(targetDir))
            {
                log.Add($"⚠️ Directory does not exist: {targetDir}");
                return log;
            }

            var files = Directory.GetFiles(targetDir);
            foreach (var file in files)
            {
                var creationTime = File.GetLastWriteTime(file);
                string folderName = creationTime.ToString("yyyy-MM");
                string destFolder = Path.Combine(targetDir, folderName);
                string destFile = Path.Combine(destFolder, Path.GetFileName(file));

                log.Add($"[Date-Archive] Move \"{Path.GetFileName(file)}\" ➔ \\{folderName}\\");

                if (!dryRun)
                {
                    try
                    {
                        if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);
                        if (File.Exists(destFile))
                        {
                            string ext = Path.GetExtension(file);
                            string newName = Path.GetFileNameWithoutExtension(file) + "_" + Guid.NewGuid().ToString().Substring(0, 5) + ext;
                            destFile = Path.Combine(destFolder, newName);
                        }
                        File.Move(file, destFile);
                    }
                    catch (Exception ex)
                    {
                        log.Add($"  ❌ Error moving {Path.GetFileName(file)}: {ex.Message}");
                    }
                }
            }

            if (log.Count == 0) log.Add("No files to organize by date.");
            return log;
        }

        // 3. DUPLICATE FINDER ALGORITHM (MD5 Hash comparison)
        public static List<string> FindDuplicates(string targetDir, bool purge, out List<string> purgeLog)
        {
            purgeLog = new List<string>();
            var log = new List<string>();
            if (!Directory.Exists(targetDir))
            {
                log.Add($"⚠️ Directory does not exist: {targetDir}");
                return log;
            }

            var files = Directory.GetFiles(targetDir, "*.*", SearchOption.AllDirectories);
            
            // Fast grouping: group by file size first before hashing
            var fileGroupsBySize = files
                .Select(f => new FileInfo(f))
                .GroupBy(f => f.Length)
                .Where(g => g.Count() > 1);

            var hashDict = new Dictionary<string, List<string>>();

            using (var md5 = MD5.Create())
            {
                foreach (var sizeGroup in fileGroupsBySize)
                {
                    foreach (var fileInfo in sizeGroup)
                    {
                        try
                        {
                            using (var stream = File.OpenRead(fileInfo.FullName))
                            {
                                byte[] hashBytes = md5.ComputeHash(stream);
                                string hashStr = BitConverter.ToString(hashBytes).Replace("-", "");

                                if (!hashDict.ContainsKey(hashStr))
                                {
                                    hashDict[hashStr] = new List<string>();
                                }
                                hashDict[hashStr].Add(fileInfo.FullName);
                            }
                        }
                        catch { }
                    }
                }
            }

            var duplicateGroups = hashDict.Values.Where(g => g.Count > 1).ToList();

            if (duplicateGroups.Count == 0)
            {
                log.Add("No duplicate files detected.");
                return log;
            }

            int count = 1;
            foreach (var group in duplicateGroups)
            {
                log.Add($"Group #{count++} (Identical MD5 Hash):");
                log.Add($"  Keep: {group[0]}");
                
                for (int i = 1; i < group.Count; i++)
                {
                    log.Add($"  Duplicate: {group[i]}");
                    
                    if (purge)
                    {
                        try
                        {
                            File.Delete(group[i]);
                            purgeLog.Add($"🗑️ Deleted duplicate: {Path.GetFileName(group[i])} from {Path.GetDirectoryName(group[i])}");
                        }
                        catch (Exception ex)
                        {
                            purgeLog.Add($"  ❌ Failed to delete duplicate: {Path.GetFileName(group[i])} - {ex.Message}");
                        }
                    }
                }
                log.Add(string.Empty);
            }

            return log;
        }

        // 4. LARGE FILES AUDIT ALGORITHM
        public static List<string> AuditLargeFiles(string targetDir, long minSizeBytes)
        {
            var log = new List<string>();
            if (!Directory.Exists(targetDir))
            {
                log.Add($"⚠️ Directory does not exist: {targetDir}");
                return log;
            }

            var files = Directory.GetFiles(targetDir, "*.*", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f))
                .Where(f => f.Length >= minSizeBytes)
                .OrderByDescending(f => f.Length)
                .ToList();

            if (files.Count == 0)
            {
                log.Add($"No files found larger than {FormatSize(minSizeBytes)}.");
                return log;
            }

            log.Add($"Large files audit for '{targetDir}' (Threshold: {FormatSize(minSizeBytes)}):");
            log.Add(string.Empty);
            foreach (var f in files)
            {
                log.Add($"{FormatSize(f.Length),-10} - {f.Name} (Path: {f.FullName})");
            }

            return log;
        }

        // 5. PURGE EMPTY DIRECTORIES
        public static List<string> PurgeEmptyDirectories(string targetDir, bool dryRun)
        {
            var log = new List<string>();
            if (!Directory.Exists(targetDir))
            {
                log.Add($"⚠️ Directory does not exist: {targetDir}");
                return log;
            }

            PurgeEmptyDirsRecursive(targetDir, dryRun, log);

            if (log.Count == 0)
            {
                log.Add("No empty folders detected.");
            }
            return log;
        }

        private static void PurgeEmptyDirsRecursive(string currentDir, bool dryRun, List<string> log)
        {
            try
            {
                foreach (var subDir in Directory.GetDirectories(currentDir))
                {
                    PurgeEmptyDirsRecursive(subDir, dryRun, log);
                }

                // Reload after subdirectories are cleaned
                if (Directory.GetDirectories(currentDir).Length == 0 && Directory.GetFiles(currentDir).Length == 0)
                {
                    log.Add($"🗑️ Empty Folder: \\{Path.GetRelativePath(currentDir, currentDir)}\\ (Full Path: {currentDir})");
                    if (!dryRun)
                    {
                        Directory.Delete(currentDir);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Add($"  ❌ Error inspecting folder {currentDir}: {ex.Message}");
            }
        }

        private static string FormatSize(long bytes)
        {
            string[] Suffix = { "B", "KB", "MB", "GB", "TB" };
            int i;
            double dblS = bytes;
            for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024)
            {
                dblS = bytes / 1024.0;
            }
            return $"{dblS:0.##} {Suffix[i]}";
        }
    }
}
