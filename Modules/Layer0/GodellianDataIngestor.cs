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
        private static FileSystemWatcher? _watcher;

        public static void InitializeAutoWatcher()
        {
            try
            {
                string downloads = PathHandler.GetDownloadsDirectory();
                if (!Directory.Exists(downloads)) Directory.CreateDirectory(downloads);

                _watcher = new FileSystemWatcher(downloads)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    Filter = "*.*",
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                _watcher.Created += (s, e) => Task.Run(async () => {
                    await Task.Delay(1000); // Wait for file to settle
                    await IngestFileAsync(e.FullPath);
                });

                DebugConsoleOverlay.Log("Neural-Ingest", "Autonomic Data Watcher active on Downloads.");
            }
            catch { }
        }

        public static async Task IngestFileAsync(string path)
        {
            if (!File.Exists(path)) return;
            try {
                string ext = Path.GetExtension(path).ToLower();

                // Text-based ingestion
                var textExts = new[] { ".txt", ".md", ".json", ".csv", ".py", ".cs", ".js", ".html", ".xml", ".yaml", ".yml", ".c", ".cpp", ".rs", ".go" };
                if (textExts.Contains(ext))
                {
                    string content = await File.ReadAllTextAsync(path);
                    await ProcessContentAsync(content, Path.GetFileName(path));
                }
                // Binary/Image/Other: Analyze via Vision if it's an image, or describe binary metadata
                else if (new[] { ".png", ".jpg", ".jpeg", ".bmp" }.Contains(ext))
                {
                    string res = await AiAPI.AnalyzeImageAsync("Sir, provide a technical mathematical summary of the patterns in this image for neural manifold ingestion.", path);
                    await ProcessContentAsync(res, "Visual_" + Path.GetFileName(path));
                }
                // Audio ingestion
                else if (new[] { ".wav", ".mp3", ".m4a", ".flac" }.Contains(ext))
                {
                    DebugConsoleOverlay.Log("Neural-Ingest", $"Transcribing audio for ingestion: {Path.GetFileName(path)}");
                    string transcription = VoskEngine.RecognizeWavFile(path);
                    if (!string.IsNullOrEmpty(transcription))
                    {
                        await ProcessContentAsync(transcription, "Audio_" + Path.GetFileName(path));
                    }
                }
                // Parquet Ingestion (Data Science standard)
                else if (ext == ".parquet")
                {
                    DebugConsoleOverlay.Log("Neural-Ingest", $"Analyzing Parquet structure: {Path.GetFileName(path)}");
                    string summary = await TryExtractParquetSummaryAsync(path);
                    await ProcessContentAsync(summary, "Parquet_" + Path.GetFileName(path));
                }
                else
                {
                    // For truly 'any' data, we can at least describe the binary structure or hex signature
                    byte[] bytes = await File.ReadAllBytesAsync(path);
                    string hex = BitConverter.ToString(bytes.Take(500).ToArray());
                    string prompt = $"Sir, this is a binary file named '{Path.GetFileName(path)}'. Hex signature: {hex}. Provide 5 training vectors representing its data entropy.";
                    string res = await LlmRouter.AskAsync(prompt);
                    var pairs = ParseSyntheticVectors(res, NeuralVectorizationKernels.CurrentDimension);
                    if (pairs.Count > 0)
                    {
                        CoreRegistry.Intelligence.MainBrain.BatchTrain(pairs.Select(p => p.Key).ToList(), pairs.Select(p => p.Value).ToList(), source: "Binary_Ingest");
                    }
                }
            } catch (Exception ex) {
                DebugConsoleOverlay.Log("Ingest-Error", $"File {path}: {ex.Message}");
            }
        }

        public static async Task IngestDirectoryAsync(string dirPath)
        {
            if (!Directory.Exists(dirPath)) return;

            var extensions = new[] { ".txt", ".md", ".json", ".csv", ".py", ".cs", ".js", ".html", ".yaml", ".yml", ".c", ".cpp" };
            var files = Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories)
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            DebugConsoleOverlay.Log("Neural-Ingest", $"Bulk ingestion started: {files.Count} files across multiple formats.");

            foreach (var f in files)
            {
                try {
                    // Limit file size for safety
                    if (new FileInfo(f).Length > 1024 * 1024 * 5) continue; // 1MB limit for auto-ingest

                    string content = await File.ReadAllTextAsync(f);
                    await ProcessContentAsync(content, Path.GetFileName(f));
                } catch { }
            }

            DebugConsoleOverlay.Log("Neural-Ingest", "Bulk ingestion finalized.");
        }

        public static async Task IngestRawContentAsync(string content, string source)
        {
            if (string.IsNullOrWhiteSpace(content)) return;
            await ProcessContentAsync(content, source);
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
                        epochs: SettingsManager.Current.GODELLIAN_TRAINING_EPOCHS,
                        source: "File_Import"
                    );
                }
            } catch { }
        }

        private static async Task<string> TryExtractParquetSummaryAsync(string path)
        {
            try
            {
                // We use a temporary Python script to extract the schema and head of the Parquet file
                // This leverages your existing Python environment
                string script = $"import pandas as pd; df = pd.read_parquet(r'{path}'); print('SCHEMA:'); print(df.dtypes); print('\\nPREVIEW:'); print(df.head(10).to_string())";
                string tempScriptPath = Path.Combine(Path.GetTempPath(), "parquet_reader.py");
                File.WriteAllText(tempScriptPath, script);

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "python.exe",
                    Arguments = $"\"{tempScriptPath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(psi);
                if (process == null) return "Parquet binary analysis failed (Python not found).";

                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                try { File.Delete(tempScriptPath); } catch { }
                return string.IsNullOrWhiteSpace(output) ? "Empty Parquet structure." : output;
            }
            catch (Exception ex)
            {
                return $"Error reading Parquet: {ex.Message}";
            }
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
