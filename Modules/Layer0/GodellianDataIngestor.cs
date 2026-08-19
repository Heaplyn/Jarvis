// Developer: heaplyn
// Date: 2026-08-18
// Summary: High-throughput Data Ingestor for Godellian Intelligence.
//          Handles single files, bulk directories, and raw text blobs.
//          Converts unstructured data into normalized training tensors.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public static class GodellianDataIngestor
    {
        public static async Task IngestFileAsync(string path)
        {
            if (!File.Exists(path)) return;
            try {
                string content = await File.ReadAllTextAsync(path);
                await ProcessContentAsync(content, Path.GetFileName(path));
            } catch (Exception ex) {
                DebugConsoleOverlay.Log("Ingest-Error", $"File {path}: {ex.Message}");
            }
        }

        public static async Task IngestDirectoryAsync(string dirPath)
        {
            if (!Directory.Exists(dirPath)) return;
            var files = Directory.GetFiles(dirPath, "*.txt", SearchOption.AllDirectories);

            DebugConsoleOverlay.Log("Neural-Ingest", $"Bulk ingestion started: {files.Length} files.");

            foreach (var f in files)
            {
                try {
                    string content = await File.ReadAllTextAsync(f);
                    await ProcessContentAsync(content, Path.GetFileName(f));
                } catch { }
            }

            DebugConsoleOverlay.Log("Neural-Ingest", "Bulk ingestion finalized.");
        }

        private static async Task ProcessContentAsync(string content, string source)
        {
            // 1. Concept Extraction
            var words = content.Split(new[] { ' ', ',', '.', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(w => w.Trim().ToLower())
                               .Where(w => w.Length > 4 && w.Any(char.IsLetter))
                               .Distinct()
                               .Take(60)
                               .ToList();

            if (words.Count > 0)
                CoreRegistry.Intelligence.MainBrain.IngestVocabulary(words, "Bulk_Import");

            // 2. Vectorization & Training Data Generation
            string prompt = $"### TECHNICAL KNOWLEDGE HARVEST: {source}\n" +
                            "Sir, extract the mathematical essence of this data.\n" +
                            "Generate 6 training pairs (16-dim vectors) representing the technical patterns in this text.\n" +
                            $"TEXT SNIPPET: {new string(content.Take(3000).ToArray())}\n" +
                            "Format: [IN]: v1,v2... [OUT]: t1,t2...";

            try {
                string result = await LlmRouter.AskAsync(prompt);
                var pairs = ParseSyntheticVectors(result, NeuralVectorizationKernels.CurrentDimension);

                if (pairs.Count > 0)
                {
                    CoreRegistry.Intelligence.MainBrain.BatchTrain(
                        pairs.Select(p => p.Key).ToList(),
                        pairs.Select(p => p.Value).ToList(),
                        epochs: 35,
                        source: "File_Import"
                    );
                }
            } catch { }
        }

        private static List<KeyValuePair<double[], double[]>> ParseSyntheticVectors(string raw, int dim)
        {
            var pairs = new List<KeyValuePair<double[], double[]>>();
            try {
                var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                double[]? currentIn = null;
                foreach (var line in lines) {
                    var clean = line.Trim();
                    if (clean.Contains("[IN]:")) {
                        var valStr = clean.Split("[IN]:")[1];
                        currentIn = valStr.Split(',').Select(s => double.TryParse(s.Trim(), out double d) ? d : 0.0).Take(dim).ToArray();
                    } else if (clean.Contains("[OUT]:") && currentIn != null) {
                        var valStr = clean.Split("[OUT]:")[1];
                        var target = valStr.Split(',').Select(s => double.TryParse(s.Trim(), out double d) ? d : 0.0).ToArray();
                        pairs.Add(new KeyValuePair<double[], double[]>(currentIn, target));
                        currentIn = null;
                    }
                }
            } catch { }
            return pairs;
        }
    }
}
