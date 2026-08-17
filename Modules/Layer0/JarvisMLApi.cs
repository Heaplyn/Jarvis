// Developer: heaplyn
// Date: 2026-08-16
// Summary: Jarvis Native C# ML API.
//          Exposes high-level AI orchestration for Image, Audio, and Text processing to external plugins and scripts.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    /// <summary>
    /// The official C# API for interacting with Jarvis's Machine Learning and AI capabilities.
    /// Use this to process multimedia or run complex linguistic analysis.
    /// </summary>
    public static class JarvisMLApi
    {
        // ── TEXT & LLM ──────────────────────────────────────────────────────────

        /// <summary>
        /// Sends a text prompt to the currently active LLM backend (Gemini, Groq, Ollama, etc.).
        /// </summary>
        public static async Task<string> AskAiAsync(string prompt, List<ChatTurn>? history = null, CancellationToken ct = default)
        {
            return await CoreRegistry.Llm.AskAsync(prompt, history, ct);
        }

        /// <summary>
        /// Summarizes a large block of text into a concise response.
        /// </summary>
        public static async Task<string> SummarizeTextAsync(string longText, int maxSentences = 3)
        {
            string prompt = $"Summarize the following text in exactly {maxSentences} sentences:\n\n{longText}";
            return await CoreRegistry.Llm.AskAsync(prompt);
        }

        // ── VISION & IMAGE PROCESSING ───────────────────────────────────────────

        /// <summary>
        /// Analyzes a local image file using AI vision.
        /// </summary>
        public static async Task<string> AnalyzeImageFileAsync(string filePath, string question = "Describe this image.")
        {
            if (!System.IO.File.Exists(filePath)) return "Error: File not found.";
            byte[] bytes = System.IO.File.ReadAllBytes(filePath);
            string base64 = Convert.ToBase64String(bytes);
            return await AiAPI.AnalyzeImageBase64Async(question, base64);
        }

        /// <summary>
        /// Captures the primary screen and analyzes it immediately.
        /// </summary>
        public static async Task<string> AnalyzeCurrentScreenAsync(string question = "What is currently visible on the screen?")
        {
            string? base64 = ScreenCaptureUtil.CapturePrimaryScreenToBase64(saveToDisk: false);
            if (string.IsNullOrEmpty(base64)) return "Error: Failed to capture screen.";
            return await AiAPI.AnalyzeImageBase64Async(question, base64);
        }

        // ── AUDIO & SPEECH ──────────────────────────────────────────────────────

        /// <summary>
        /// Uses local Vosk engine to transcribe a short audio clip (WAV).
        /// </summary>
        public static string TranscribeLocalAudio(string wavFilePath)
        {
            return VoskEngine.RecognizeWavFile(wavFilePath);
        }

        /// <summary>
        /// Sends an audio clip to Gemini for deep semantic analysis (Multimodal).
        /// </summary>
        public static async Task<string> AnalyzeAudioClipAsync(string wavFilePath, string question = "What is being said or happening in this audio?")
        {
            if (!System.IO.File.Exists(wavFilePath)) return "Error: Audio file not found.";
            byte[] bytes = System.IO.File.ReadAllBytes(wavFilePath);
            string base64 = Convert.ToBase64String(bytes);
            return await AiAPI.AnalyzeAudioAsync(question, base64);
        }

        // ── OUTPUT & INTERACTION ────────────────────────────────────────────────

        /// <summary>
        /// Speaks the given text using the configured Jarvis TTS engine.
        /// </summary>
        public static void Speak(string text)
        {
            TtsManager.Speak(text);
        }

        /// <summary>
        /// Shows a HUD notification toast.
        /// </summary>
        public static void Notify(string title, string message, int durationMs = 3000)
        {
            TextOverlay.Show($"{title}: {message}", durationMs);
        }
    }
}
