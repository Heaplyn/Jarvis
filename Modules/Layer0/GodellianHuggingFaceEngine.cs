// Developer: heaplyn
// Date: 2026-08-19
// Summary: Godellian Autonomic Hugging Face Crawler.
//          Specifically designed to feed the Godellian manifold with technical data and READMEs from HF.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public static class GodellianHuggingFaceEngine
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private static bool _isRunning = false;

        static GodellianHuggingFaceEngine()
        {
            _http.DefaultRequestHeaders.Add("User-Agent", "JarvisLauncher/1.0");
        }

        public static async Task RunAutoGrabCycleAsync()
        {
            if (_isRunning) return;
            _isRunning = true;

            try
            {
                DebugConsoleOverlay.Log("Godellian-HF", "Starting autonomic Hugging Face knowledge grab...");

                // 1. Ask Brain for current "Knowledge Gaps" or trends to search
                string prompt = "### GODELLIAN KNOWLEDGE MANAGER\n" +
                                "Sir, identify 3 advanced technical or scientific domains where our manifold logic could be expanded.\n" +
                                "Return 3 specific search keywords for Hugging Face datasets.\n" +
                                "Format: [DOMAINS]: d1, d2, d3 [KEYWORDS]: k1, k2, k3";

                string response = await LlmRouter.AskAsync(prompt);
                var keywords = ParseSection(response, "[KEYWORDS]:");

                foreach (var k in keywords.Take(3))
                {
                    await GrabKnowledgeFromKeywordAsync(k);
                }
            }
            catch (Exception ex)
            {
                DebugConsoleOverlay.Log("Godellian-HF-Error", ex.Message);
            }
            finally
            {
                _isRunning = false;
            }
        }

        private static async Task GrabKnowledgeFromKeywordAsync(string keyword)
        {
            try
            {
                DebugConsoleOverlay.Log("Godellian-HF", $"Searching HF for knowledge in: '{keyword}'");
                string url = $"https://huggingface.co/api/datasets?search={Uri.EscapeDataString(keyword)}&limit=3&sort=downloads&direction=-1";

                string json = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    string id = item.GetProperty("id").GetString() ?? "";
                    if (!string.IsNullOrEmpty(id))
                    {
                        await IngestDatasetMetadataAsync(id);
                    }
                }
            }
            catch { }
        }

        private static async Task IngestDatasetMetadataAsync(string repoId)
        {
            try
            {
                // Try to fetch README.md directly from HF raw CDN
                // Format: https://huggingface.co/datasets/REPOD/raw/main/README.md
                string readmeUrl = $"https://huggingface.co/datasets/{repoId}/raw/main/README.md";
                string content = await _http.GetStringAsync(readmeUrl);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    DebugConsoleOverlay.Log("Godellian-HF", $"Ingesting knowledge from README: {repoId}");
                    await GodellianDataIngestor.IngestRawContentAsync(content, $"HF_README_{repoId}");
                }
            }
            catch { }
        }

        private static List<string> ParseSection(string text, string marker)
        {
            if (!text.Contains(marker)) return new List<string>();
            var part = text.Split(marker)[1].Split('\n')[0];
            return part.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
        }
    }
}
