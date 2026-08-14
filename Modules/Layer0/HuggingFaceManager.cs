// Developer: heaplyn
// Date: 2026-08-13
// Summary: Hugging Face Hub Integration Manager.
// Features huggingface-cli auto-installer, live model search API, 1-click GGUF/model downloader, and Ollama GGUF importer.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public class HuggingFaceModelItem
    {
        public string id { get; set; } = string.Empty;
        public string modelId { get; set; } = string.Empty;
        public int downloads { get; set; } = 0;
        public int likes { get; set; } = 0;
        public string pipeline_tag { get; set; } = string.Empty;
    }

    public static class HuggingFaceManager
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        public static readonly string HfModelDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Models", "huggingface");

        static HuggingFaceManager()
        {
            if (!Directory.Exists(HfModelDirectory))
            {
                Directory.CreateDirectory(HfModelDirectory);
            }
        }

        /// <summary>
        /// Auto-installs huggingface_hub[cli] via Python pip.
        /// </summary>
        public static void AutoInstallHfCli()
        {
            try
            {
                TextOverlay.Show("📥 Auto-Installing Hugging Face CLI via pip...", 4000);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/k \"echo Installing Hugging Face Hub CLI... && pip install -U huggingface_hub[cli] && echo Finished! && pause\"",
                    CreateNoWindow = false,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Hugging Face CLI Install error: {ex.Message}", 3000);
            }
        }

        /// <summary>
        /// Searches Hugging Face Hub live models API by query keyword or pipeline tag.
        /// </summary>
        public static async Task<List<HuggingFaceModelItem>> SearchModelsAsync(string query = "gguf", int limit = 15)
        {
            var results = new List<HuggingFaceModelItem>();
            try
            {
                string url = $"https://huggingface.co/api/models?search={Uri.EscapeDataString(query)}&limit={limit}&sort=downloads&direction=-1";
                _http.DefaultRequestHeaders.UserAgent.ParseAdd("JarvisLauncher/1.0");

                string json = await _http.GetStringAsync(url);
                var items = JsonSerializer.Deserialize<List<HuggingFaceModelItem>>(json);
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        if (string.IsNullOrEmpty(item.modelId)) item.modelId = item.id;
                        results.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HF Search Error: {ex.Message}");
            }
            return results;
        }

        /// <summary>
        /// Downloads a specific GGUF or model repo from Hugging Face using huggingface-cli.
        /// </summary>
        public static void DownloadModelRepo(string repoId, string filename = "")
        {
            try
            {
                TextOverlay.Show($"📥 Downloading Hugging Face Model: {repoId}...", 4000);

                string cmdArgs = string.IsNullOrWhiteSpace(filename)
                    ? $"huggingface-cli download {repoId} --local-dir \"{HfModelDirectory}\""
                    : $"huggingface-cli download {repoId} {filename} --local-dir \"{HfModelDirectory}\"";

                Process.Start("cmd.exe", $"/c start cmd /k \"echo Downloading from Hugging Face: {repoId}... & {cmdArgs} & echo Download Complete! File saved to {HfModelDirectory} & pause\"");
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Download error: {ex.Message}", 3000);
            }
        }

        /// <summary>
        /// Auto-imports a downloaded GGUF file directly into Ollama local engine.
        /// </summary>
        public static void ImportGgufToOllama(string ggufFilePath, string modelName)
        {
            if (!File.Exists(ggufFilePath))
            {
                TextOverlay.Show($"⚠️ GGUF file not found: {ggufFilePath}", 3000);
                return;
            }

            try
            {
                string modelfilePath = Path.Combine(Path.GetDirectoryName(ggufFilePath)!, "Modelfile");
                File.WriteAllText(modelfilePath, $"FROM \"{ggufFilePath.Replace("\\", "/")}\"\n");

                TextOverlay.Show($"⚙️ Importing GGUF to Ollama as '{modelName}'...", 4000);
                Process.Start("cmd.exe", $"/c start cmd /k \"echo Importing GGUF to Ollama... & ollama create {modelName} -f \"{modelfilePath}\" & echo Import Complete! & pause\"");
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Ollama Import error: {ex.Message}", 3000);
            }
        }
    }
}
