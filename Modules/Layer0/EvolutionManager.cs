// Developer: heaplyn
// Date: 2026-08-18
// Summary: Godellian Intelligence Evolution Orchestrator v19 (Ultra-Turbo).
//          Turbo Mode: Supports sub-second training cycles (500ms default).
//          Parallel Knowledge Forge: Scrapes, Vectorizes, and Trains in parallel.
//          Symbolic Hardening: Constantly refines the symbolic calculus bridge.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;

namespace JarvisLauncher
{
    public static class EvolutionManager
    {
        private static bool _isActive = false;
        private static readonly List<string> _scrapeSources = new List<string> {
            "https://en.wikipedia.org/wiki/Calculus",
            "https://en.wikipedia.org/wiki/Tensor_field",
            "https://en.wikipedia.org/wiki/Manifold",
            "https://en.wikipedia.org/wiki/Cybernetics",
            "https://en.wikipedia.org/wiki/Recursion"
        };

        public static void StartContinuousEvolution()
        {
            if (_isActive || !SettingsManager.Current.ENABLE_GODELLIAN_ENGINE) return;
            _isActive = true;

            // Start Data Forge
            GodellianDataForge.StartBackgroundForging();

            // 1. TURBO TRAINING LOOP (Millisecond Aware)
            Task.Run(async () => {
                DebugConsoleOverlay.Log("Evolution-Turbo", "Godellian Ultra-Turbo Training: ACTIVE");
                while (_isActive) {
                    try {
                        var settings = SettingsManager.Current;
                        int interval = settings.GODELLIAN_TURBO_MODE ? Math.Max(100, settings.GODELLIAN_TURBO_INTERVAL_MS) : 30000;

                        await PerformHighFidelityExpansionAsync();
                        await Task.Delay(interval);
                    } catch { }
                }
            });

            // 2. CONSTANT WEB SCOUR (Every 2 mins in turbo)
            Task.Run(async () => {
                while (_isActive) {
                    try { await ScrapeAndIngestTechnicalKnowledgeAsync(); } catch { }
                    int delay = SettingsManager.Current.GODELLIAN_TURBO_MODE ? 2 : 10;
                    await Task.Delay(TimeSpan.FromMinutes(delay));
                }
            });

            // 3. KERNEL REFINEMENT (Every 5 mins)
            Task.Run(async () => {
                while (_isActive) {
                    try { await PerformDeepKernelRefinementAsync(); } catch { }
                    await Task.Delay(TimeSpan.FromMinutes(5));
                }
            });

            // 4. DATASET HARVESTING & GODELLIAN HF GRAB (Every 30 mins)
            Task.Run(async () => {
                while (_isActive) {
                    try {
                        await DatasetHarvester.RunAutomaticHarvestAsync();
                        await GodellianHuggingFaceEngine.RunAutoGrabCycleAsync();
                    } catch { }
                    await Task.Delay(TimeSpan.FromMinutes(30));
                }
            });

            // 5. AUTONOMIC LLM LOGIC EXCHANGE (User Configurable)
            Task.Run(async () => {
                while (_isActive) {
                    try {
                        var settings = SettingsManager.Current;
                        if (settings.GODELLIAN_AUTO_LLM_EXCHANGE) {
                            await CoreRegistry.Intelligence.MainBrain.ExchangeLogicWithLlmAsync();
                        }
                        int interval = Math.Max(10, settings.GODELLIAN_EXCHANGE_INTERVAL_SEC);
                        await Task.Delay(TimeSpan.FromSeconds(interval));
                    } catch {
                        await Task.Delay(TimeSpan.FromSeconds(30)); // Safety delay on error
                    }
                }
            });

            // 6. NORMATIVE LAW EVOLUTION (Every 60 mins)
            Task.Run(async () => {
                while (_isActive) {
                    try {
                        if (SettingsManager.Current.ENABLE_GODELLIAN_ENGINE) {
                            await LawEvolutionEngine.RunLawEvolutionCycleAsync();
                        }
                    } catch { }
                    await Task.Delay(TimeSpan.FromMinutes(60));
                }
            });
        }

        private static async Task ScrapeAndIngestTechnicalKnowledgeAsync()
        {
            if (!SettingsManager.Current.DATA_ENABLE_AUTO_SCRAPE) return;

            string url = _scrapeSources[Random.Shared.Next(_scrapeSources.Count)];
            try {
                string prompt = $"### KNOWLEDGE HARVEST: {url}\n" +
                                "Sir, extract 15 high-level technical concepts and provide 8 synthetic 16-dim training vectors.\n" +
                                "Format: [CONCEPTS]: c1,c2... [VECTORS]: [IN]:v...[OUT]:v...";

                string harvest = await LlmRouter.AskAsync(prompt);

                if (harvest.Contains("[CONCEPTS]:")) {
                    var list = harvest.Split("[CONCEPTS]:")[1].Split('\n')[0].Split(',').Select(s => s.Trim()).ToList();
                    CoreRegistry.Intelligence.MainBrain.IngestVocabulary(list, "Scraped_Growth");
                }

                var pairs = ParseSyntheticVectors(harvest, NeuralVectorizationKernels.CurrentDimension);
                if (pairs.Count > 0)
                    CoreRegistry.Intelligence.MainBrain.BatchTrain(pairs.Select(p => p.Key).ToList(), pairs.Select(p => p.Value).ToList(), epochs: SettingsManager.Current.GODELLIAN_TRAINING_EPOCHS, source: "Scraped_Growth");
            } catch { }
        }

        private static async Task PerformHighFidelityExpansionAsync()
        {
            var brain = CoreRegistry.Intelligence.MainBrain;
            var settings = SettingsManager.Current;
            int dim = NeuralVectorizationKernels.CurrentDimension;

            // Collect Environment Logic
            string screen = await HarvestScreenContextAsync();
            string chat = await SemanticMemoryManager.GetRecentChatContextAsync();

            string prompt = $"### TURBO TRAINING (DIM: {dim})\n" +
                            $"CONTEXT: {screen}\n" +
                            "### TASK\n" +
                            "Generate 10 complex symbolic-to-logic training pairs.\n" +
                            "Format: [IN]: v1,v2... [OUT]: t1,t2...";

            try {
                string result = await LlmRouter.AskAsync(prompt);
                var pairs = ParseSyntheticVectors(result, dim);
                if (pairs.Count > 0)
                    brain.BatchTrain(pairs.Select(p => p.Key).ToList(), pairs.Select(p => p.Value).ToList(), epochs: settings.GODELLIAN_TRAINING_EPOCHS);

                brain.MutateTopology();
            } catch { }
        }

        private static async Task PerformDeepKernelRefinementAsync()
        {
            try {
                string advice = await LlmRouter.AskAsync("### OPTIMIZE KERNEL\nReview 'NeuralVectorizationKernels.cs'. Provide @mod_code to improve manifold projection density.");
                if (advice.Contains("@mod_code")) await AiAPI.ExecuteAgentLoopAsync(advice);
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
                        var parts = clean.Split("[IN]:")[1].Split(',');
                        currentIn = parts.Select(s => double.TryParse(s.Trim(), out double d) ? d : 0.0).Take(dim).ToArray();
                    } else if (clean.Contains("[OUT]:") && currentIn != null) {
                        var parts = clean.Split("[OUT]:")[1].Split(',');
                        var target = parts.Select(s => double.TryParse(s.Trim(), out double d) ? d : 0.0).ToArray();
                        pairs.Add(new KeyValuePair<double[], double[]>(currentIn, target));
                        currentIn = null;
                    }
                }
            } catch { }
            return pairs;
        }

        private static async Task<string> HarvestScreenContextAsync()
        {
            try {
                string? b64 = ScreenCaptureUtil.CapturePrimaryScreenToBase64(false);
                if (string.IsNullOrEmpty(b64)) return "Idle.";
                return await AiAPI.AnalyzeImageBase64Async("Summary.", b64, "image/png");
            } catch { return "Static."; }
        }
    }
}
