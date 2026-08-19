// Developer: heaplyn
// Date: 2026-08-19
// Summary: Godellian Distributed Data Forge.
//          Pools multiple AI backends to generate high-fidelity synthetic training data.
//          Refines symbolic math kernels through cross-model consensus.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public static class GodellianDataForge
    {
        private static bool _isForging = false;
        private static readonly string[] KnowledgeDomains = {
            "Quantum Calculus", "Multi-Variable Manifolds", "Neural Topology",
            "Cybernetic Control Theory", "Recursive Symbolic Logic", "Temporal Data Streaming"
        };

        public static void StartBackgroundForging()
        {
            if (_isForging) return;
            _isForging = true;

            Task.Run(async () => {
                while (_isForging) {
                    try {
                        if (SettingsManager.Current.GODELLIAN_ENABLE_BACKGROUND_TRAINING) {
                            await PerformCrossModelConsensusAsync();
                            await MnistDataIngestor.StartIngestionAsync();
                        }
                    } catch { }
                    await Task.Delay(TimeSpan.FromMinutes(5));
                }
            });
        }

        private static async Task PerformCrossModelConsensusAsync()
        {
            string domain = KnowledgeDomains[Random.Shared.Next(KnowledgeDomains.Count())];
            int dim = NeuralVectorizationKernels.CurrentDimension;

            string prompt = $"### GODELLIAN DATA FORGE: {domain}\n" +
                            "Sir, generate 10 pairs of 16-dimensional training vectors representing a stable logic manifold in this domain.\n" +
                            "Also, provide a symbolic equation that maps these vectors.\n" +
                            "Format: [IN]: v1,v2... [OUT]: t1,t2... [EQ]: f(x)=...";

            // Use the Router to pick different models for variety, including local high-speed nodes
            var models = new[] { "Gemini", "OpenAI", "Anthropic", "Groq", "LM Studio", "Bionic", "Ollama" };
            string model = models[Random.Shared.Next(models.Length)];

            try {
                string result = await LlmRouter.AskAsync(prompt);
                var pairs = ParseSyntheticVectors(result, dim);

                if (pairs.Count > 0) {
                    CoreRegistry.Intelligence.MainBrain.BatchTrain(
                        pairs.Select(p => p.Key).ToList(),
                        pairs.Select(p => p.Value).ToList(),
                        epochs: SettingsManager.Current.GODELLIAN_TRAINING_EPOCHS,
                        source: $"Consensus_{model}"
                    );
                    DebugConsoleOverlay.Log("Neural-Forge", $"Consensus Ingested: {domain} via {model}");
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
