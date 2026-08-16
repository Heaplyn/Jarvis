# Jarvis C# Plugin & ML API Guide

Jarvis now supports native C# plugins, allowing you to extend the HUD with custom logic and high-level AI orchestration.

## 🚀 Getting Started

1. Create a new **C# Class Library (.NET 8.0)** project.
2. Reference the `JarvisLauncher.dll` (found in the Jarvis root folder).
3. Implement the `IJarvisPlugin` interface.
4. Drop your compiled `.dll` into the `/Plugins` folder in your Jarvis directory.

## 🧠 The Jarvis ML API

The `JarvisMLApi` static class provides high-level methods for AI processing.

### Text & LLM
```csharp
// Ask a generic question to the active LLM
string result = await JarvisMLApi.AskAiAsync("Explain quantum physics.");

// Summarize long text
string summary = await JarvisMLApi.AskAiAsync(hugeContent, maxSentences: 2);
```

### Vision (Image Processing)
```csharp
// Analyze a local image
string description = await JarvisMLApi.AnalyzeImageFileAsync("C:\\temp\\data.png", "What's in this image?");

// Analyze what the user is looking at right now
string screenInfo = await JarvisMLApi.AnalyzeCurrentScreenAsync("Summarize this workspace.");
```

### Audio Processing
```csharp
// Multi-modal audio analysis via Gemini
string audioIntent = await JarvisMLApi.AnalyzeAudioClipAsync("recording.wav", "Extract the emotional tone.");
```

## 🛠️ Example Plugin Implementation

```csharp
using System;
using System.Collections.Generic;
using JarvisLauncher;

namespace MyCustomPlugin
{
    public class WorkspaceAnalyzerPlugin : IJarvisPlugin
    {
        public string PluginName => "Workspace Analyzer";
        public string Description => "Uses AI to suggest workspace optimizations based on screen captures.";
        public string Author => "Dev";
        public Version Version => new Version(1, 0, 0);

        public void OnInitialize() 
        {
            Console.WriteLine("Workspace Analyzer Initialized.");
        }

        public void OnShutdown() { }

        public IEnumerable<CommandDesc> GetPluginCommands()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("analyze workspace", "Run AI vision audit on your current screen", "analyze workspace")
            };
        }
        
        // You can then hook this command into a custom handler or logic
    }
}
```

## 📂 Folder Structure
```text
Jarvis/
├── JarvisLauncher.exe
├── JarvisLauncher.dll (Core Library)
├── Plugins/
│   └── MyCustomPlugin.dll (Your Plugin)
└── Data/
    └── ...
```
