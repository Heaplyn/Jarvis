using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public static class VoiceIntelligenceManager
    {
        private static readonly string IntelligencePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "VoiceIntelligence.json");
        private static Dictionary<string, string> _learnedCorrections = new();

        static VoiceIntelligenceManager()
        {
            LoadIntelligence();
        }

        public static string ApplyIntelligence(string transcript)
        {
            if (string.IsNullOrWhiteSpace(transcript)) return transcript;
            string result = transcript;

            foreach (var correction in _learnedCorrections)
            {
                result = System.Text.RegularExpressions.Regex.Replace(
                    result,
                    @"\b" + System.Text.RegularExpressions.Regex.Escape(correction.Key) + @"\b",
                    correction.Value,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return result;
        }

        /// <summary>
        /// Periodically called to analyze the trigger dataset and find common patterns or "corrections" using the LLM.
        /// </summary>
        public static async Task AnalyzeAndLearnAsync()
        {
            try
            {
                string datasetExamples = VoiceDatasetManager.GetFewShotExamples();
                if (datasetExamples.Contains("No recent history")) return;

                string prompt = "Analyze these recent voice command transcripts and system contexts. " +
                               "Find common phonetic misrecognitions (e.g., user says 'run debug' but it's transcribed as 'run big'). " +
                               "Output ONLY a JSON dictionary where the KEY is the misrecognition and the VALUE is the intended command. " +
                               "If no clear corrections found, return {}. " +
                               "Examples:\n" + datasetExamples;

                string jsonResponse = await LlmRouter.AskAsync(prompt);

                // Extract JSON if model included markdown or chat filler
                int start = jsonResponse.IndexOf('{');
                int end = jsonResponse.LastIndexOf('}');
                if (start >= 0 && end > start)
                {
                    string json = jsonResponse.Substring(start, end - start + 1);
                    var newCorrections = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (newCorrections != null)
                    {
                        foreach (var kvp in newCorrections)
                        {
                            if (!_learnedCorrections.ContainsKey(kvp.Key))
                            {
                                _learnedCorrections[kvp.Key] = kvp.Value;
                                DebugConsoleOverlay.Log("Voice-Intelligence", $"Learned correction: \"{kvp.Key}\" -> \"{kvp.Value}\"");
                            }
                        }
                        SaveIntelligence();
                    }
                }
            }
            catch { }
        }

        private static void LoadIntelligence()
        {
            try
            {
                if (File.Exists(IntelligencePath))
                {
                    string json = File.ReadAllText(IntelligencePath);
                    _learnedCorrections = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
            }
            catch { }
        }

        private static void SaveIntelligence()
        {
            try
            {
                string json = JsonSerializer.Serialize(_learnedCorrections, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(IntelligencePath, json);
            }
            catch { }
        }
    }
}
