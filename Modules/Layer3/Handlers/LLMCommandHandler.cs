// Developer: heaplyn
// Date: 2026-08-13
// Summary: Command handler for LLM settings, Hugging Face Hub Model Grabber, local LLM installers, & model pulls.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace JarvisLauncher
{
    public class LLMCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "llm" || query == "ai" || query == "openai" || query == "gemini" ||
                   query == "ollama" || query == "lmstudio" || query == "deepseek" || query == "llama" ||
                   query == "huggingface" || query == "hf" || query == "grabmodel" || query == "downloadhf" ||
                   query == "installllm" || query == "installollama" || query == "installlmstudio" ||
                   query.StartsWith("install ") || query.StartsWith("pull ");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            string lower = query.Trim().ToLower();

            if (lower == "huggingface" || lower == "hf" || lower == "grabmodel" || lower == "downloadhf")
            {
                suggestions.Add(new CommandResult
                {
                    Title = "🤗 Open Hugging Face Model Hub & Internet Grabber",
                    Description = "Live search models on Hugging Face, 1-click grab GGUF models, & auto-install CLI",
                    Similarity = 6.0,
                    Execute = () => HuggingFaceOverlay.ShowOverlay()
                });
                return suggestions;
            }

            if (lower == "installollama" || lower == "ollama")
            {
                suggestions.Add(new CommandResult
                {
                    Title = "📥 Install Ollama Local LLM Engine",
                    Description = "Downloads and installs Ollama via Winget / web installer",
                    Similarity = 6.0,
                    Execute = () => Process.Start("cmd.exe", "/c start cmd /k \"winget install Ollama.Ollama || start https://ollama.com/download\"")
                });
            }

            if (lower == "deepseek" || lower == "pulldeepseek" || lower == "pull deepseek")
            {
                suggestions.Add(new CommandResult
                {
                    Title = "🧠 Pull DeepSeek R1 Local Model (Ollama)",
                    Description = "Downloads DeepSeek R1 reasoning model locally (ollama pull deepseek-r1:7b)",
                    Similarity = 6.0,
                    Execute = () => Process.Start("cmd.exe", "/c start cmd /k \"ollama pull deepseek-r1:7b\"")
                });
            }

            if (lower == "llama" || lower == "pullllama" || lower == "pull llama")
            {
                suggestions.Add(new CommandResult
                {
                    Title = "🦙 Pull Llama 3.2 Local Model (Ollama)",
                    Description = "Downloads Meta Llama 3.2 locally (ollama pull llama3.2)",
                    Similarity = 6.0,
                    Execute = () => Process.Start("cmd.exe", "/c start cmd /k \"ollama pull llama3.2\"")
                });
            }

            suggestions.Add(new CommandResult
            {
                Title = "🤗 Open Hugging Face Model Hub & Internet Grabber",
                Description = "Live search Hugging Face GGUF models, 1-click grab repos from the internet",
                Similarity = 5.6,
                Execute = () => HuggingFaceOverlay.ShowOverlay()
            });

            suggestions.Add(new CommandResult
            {
                Title = "🤖 Open LLM Engine & Installer Studio",
                Description = "Configure Gemini, OpenAI, Ollama, P2P nodes, & 1-click install local LLM models",
                Similarity = 5.5,
                Execute = () => LlmSettingsOverlay.ShowOverlay()
            });

            return suggestions;
        }
    }
}
