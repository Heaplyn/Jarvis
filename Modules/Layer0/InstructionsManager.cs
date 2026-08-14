// Developer: heaplyn
// Date: 2026-08-09
// Summary: Manages reading and formatting files in the Data/Instructions folder to supply to the AI's system prompt.

using System;
using System.IO;
using System.Text;

namespace JarvisLauncher
{
    public static class InstructionsManager
    {
        private static string InstructionsDir => Path.Combine(PathHandler.GetDataDirectory(), "Instructions");

        public static string InstructionsDirectory => InstructionsDir;

        static InstructionsManager()
        {
        }

        public static string GetFormattedInstructions()
        {
            if (!Directory.Exists(InstructionsDir))
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            try
            {
                var files = Directory.GetFiles(InstructionsDir, "*.*", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    string ext = Path.GetExtension(file).ToLower();
                    // Read text-based formats (.txt, .md, .json, .xml, .yaml, .yml)
                    if (ext == ".txt" || ext == ".md" || ext == ".json" || ext == ".xml" || ext == ".yaml" || ext == ".yml")
                    {
                        string fileName = Path.GetFileName(file);
                        string content = File.ReadAllText(file);
                        
                        builder.AppendLine($"[INSTRUCTION FILE: {fileName}]");
                        builder.AppendLine(content);
                        builder.AppendLine("[END INSTRUCTION FILE]");
                        builder.AppendLine();
                    }
                }
            }
            catch (Exception ex)
            {
                builder.AppendLine($"[ERROR READING INSTRUCTIONS: {ex.Message}]");
            }

            return builder.ToString();
        }
    }
}
