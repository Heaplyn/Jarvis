// Developer: heaplyn
// Date: 2026-08-15
// Summary: Autonomous System Knowledge Harvester.
//          Periodically crawls the codebase and system directories to index class structures,
//          handler logic, and file relationships. This creates a "Self-Aware" knowledge base
//          that is injected into the AI's context.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public static class SystemKnowledgeManager
    {
        private static bool IsRunning = false;
        private static string _cachedKnowledgeSummary = "Indexing system structure...";
        private static readonly object _lock = new object();

        public static string GetSystemKnowledge()
        {
            lock (_lock) return _cachedKnowledgeSummary;
        }

        public static void Start()
        {
            if (IsRunning) return;
            IsRunning = true;

            Task.Run(async () =>
            {
                // Delay first scan to allow boot to finish
                await Task.Delay(15000);

                while (IsRunning)
                {
                    try
                    {
                        await RebuildKnowledgeBaseAsync();
                    }
                    catch (Exception ex)
                    {
                        DebugConsoleOverlay.Log("Knowledge-Error", ex.Message);
                    }

                    // Re-scan every 10 minutes to stay updated with code changes
                    await Task.Delay(TimeSpan.FromMinutes(10));
                }
            });

            DebugConsoleOverlay.Log("Knowledge-System", "Autonomous System Harvester active.");
        }

        private static async Task RebuildKnowledgeBaseAsync()
        {
            string root = PathHandler.GetProjectRoot();
            var sb = new StringBuilder();
            sb.AppendLine("## INTERNAL SYSTEM ARCHITECTURE KNOWLEDGE");

            // 1. Map Modules and Layers
            var modulesDir = Path.Combine(root, "Modules");
            if (Directory.Exists(modulesDir))
            {
                var layers = Directory.GetDirectories(modulesDir, "Layer*");
                foreach (var layer in layers)
                {
                    string layerName = Path.GetFileName(layer);
                    sb.AppendLine($"### {layerName}");

                    var files = Directory.GetFiles(layer, "*.cs", SearchOption.AllDirectories);
                    foreach (var file in files.Take(15)) // Sample top files to avoid token bloat
                    {
                        string fileName = Path.GetFileName(file);
                        // Extract brief summary from file header if available
                        string summary = ExtractSummary(file);
                        sb.AppendLine($"- {fileName}: {summary}");
                    }
                }
            }

            // 2. Map Handlers (Command capabilities)
            var handlersDir = Path.Combine(root, "Modules", "Layer3", "Handlers");
            if (Directory.Exists(handlersDir))
            {
                sb.AppendLine("### Command Handlers (Action Logic)");
                var handlerFiles = Directory.GetFiles(handlersDir, "*Handler.cs");
                foreach (var h in handlerFiles)
                {
                    sb.AppendLine($"- {Path.GetFileNameWithoutExtension(h)}");
                }
            }

            // 3. Extract Knowledge from Notes system (User's shared info)
            string notesDir = Path.Combine(root, "Data", "Notes");
            if (Directory.Exists(notesDir))
            {
                sb.AppendLine("### User/Session Metadata");
                var noteFiles = Directory.GetFiles(notesDir, "*.md", SearchOption.AllDirectories);
                foreach (var n in noteFiles.Take(5))
                {
                    sb.AppendLine($"- Context from {Path.GetFileName(n)} indexed.");
                }
            }

            lock (_lock)
            {
                _cachedKnowledgeSummary = sb.ToString();
            }

            DebugConsoleOverlay.Log("Knowledge-System", $"Self-teaching pass complete. Indexing {sb.Length} bytes of architecture data.");

            // Log to a persistent file for the AI to read if needed
            string knowledgeFile = Path.Combine(PathHandler.GetDataDirectory(), "SystemKnowledge.md");
            await File.WriteAllTextAsync(knowledgeFile, _cachedKnowledgeSummary);
        }

        private static string ExtractSummary(string filePath)
        {
            try
            {
                var lines = File.ReadLines(filePath).Take(10);
                foreach (var line in lines)
                {
                    if (line.Contains("Summary:"))
                    {
                        return line.Substring(line.indexOf("Summary:") + 8).Trim();
                    }
                }
            }
            catch { }
            return "Core logic module.";
        }

        // Extension helper for old .NET versions if needed
        private static int indexOf(this string source, string value) => source.IndexOf(value);
    }
}
