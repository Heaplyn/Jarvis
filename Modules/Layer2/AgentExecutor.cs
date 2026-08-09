// Developer: heaplyn
// Date: 2026-08-09
// Summary: Parses AI responses for filesystem tags ([WRITE_FILE], [APPEND_FILE]) and executes modifications.

using System;
using System.IO;
using System.Text.RegularExpressions;

namespace JarvisLauncher
{
    public static class AgentExecutor
    {
        public static string ProcessAIResponse(string aiResponse)
        {
            if (string.IsNullOrEmpty(aiResponse)) return aiResponse;

            string logs = "";

            // 1. Process WRITE_FILE tags: [WRITE_FILE: C:\path\to\file.txt]content[END_WRITE]
            var writeRegex = new Regex(@"\[WRITE_FILE:\s*(.+?)\](.*?)\[END_WRITE\]", RegexOptions.Singleline);
            var writeMatches = writeRegex.Matches(aiResponse);
            foreach (Match match in writeMatches)
            {
                string path = match.Groups[1].Value.Trim();
                string content = match.Groups[2].Value;

                // Clean quotes from path
                path = path.Trim('"', '\'');

                try
                {
                    // Ensure parent directory exists
                    string? dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.WriteAllText(path, content);
                    TextOverlay.Show($"📝 AI Wrote File:\n{Path.GetFileName(path)}", 3000);
                    logs += $"\n[SUCCESS] Wrote file: {path}";
                }
                catch (Exception ex)
                {
                    TextOverlay.Show($"⚠️ AI Write Failed:\n{ex.Message}", 4000);
                    logs += $"\n[ERROR] Failed writing to {path}: {ex.Message}";
                }
            }

            // 2. Process APPEND_FILE tags: [APPEND_FILE: C:\path\to\file.txt]content[END_APPEND]
            var appendRegex = new Regex(@"\[APPEND_FILE:\s*(.+?)\](.*?)\[END_APPEND\]", RegexOptions.Singleline);
            var appendMatches = appendRegex.Matches(aiResponse);
            foreach (Match match in appendMatches)
            {
                string path = match.Groups[1].Value.Trim();
                string content = match.Groups[2].Value;

                path = path.Trim('"', '\'');

                try
                {
                    string? dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.AppendAllText(path, content);
                    TextOverlay.Show($"📝 AI Appended to File:\n{Path.GetFileName(path)}", 3000);
                    logs += $"\n[SUCCESS] Appended to file: {path}";
                }
                catch (Exception ex)
                {
                    TextOverlay.Show($"⚠️ AI Append Failed:\n{ex.Message}", 4000);
                    logs += $"\n[ERROR] Failed appending to {path}: {ex.Message}";
                }
            }

            if (!string.IsNullOrEmpty(logs))
            {
                return aiResponse + "\n\n--- AGENT FILESYSTEM EXECUTION SUMMARY ---\n" + logs;
            }

            return aiResponse;
        }
    }
}
