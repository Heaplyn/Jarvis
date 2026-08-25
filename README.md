# 🤖 Jarvis Launcher & HUD Environment

Welcome to **Jarvis** — a high-fidelity, frosted-glass desktop AI companion, developer automation deck, and offline workspace orchestration suite designed to run locally on Windows. 

Jarvis is designed to act as an extension of the developer's thought process — providing a low-latency keyboard-driven HUD, dynamic media downloading, multi-engine AI chat routing, automated web scraping, and integrated reverse engineering decompilers.

---

## 🚀 Quick Start (Fast Launch)

The build and launch pipeline has been optimized to leverage .NET incremental compilation, reducing startup checks from **5+ minutes down to under 2 seconds**.

1. **Launch the HUD**:
   Double-click the **`JarvisLauncher.bat`** (or **`run.bat`**) in the root directory. 
   - *This automatically checks for file modifications, compiles incrementally (typically taking less than 1 second), and launches the environment.*
2. **Development Compile**:
   To manually build the project check from your terminal:
   ```powershell
   dotnet build
   ```
3. **Hotkeys**:
   - Press **`Ctrl + Space`** (or your configured trigger) to show the Jarvis HUD launcher.
   - Press **`Escape`** to dismiss any active glassmorphic overlay window instantly.

---

## 🏗️ Architecture & Layer Hierarchy

Jarvis is structured as a strictly decoupled **4-Layer Dependency Hierarchy**. This prevents lateral coupling and eliminates circular dependency loops in C#.

```
[Layer 3: Routing/Handlers] 
          ↓ (Downward reference only)
[Layer 2: WPF Glassmorphic Overlays] 
          ↓ 
[Layer 1: Messaging Contracts & Bridges] 
          ↓ 
[Layer 0: Core Utilities & Engines]
```

### Layer Dependency Rules
- **Rule**: A module in **Layer K** can reference modules in **Layer J** if and only if **J < K**.
- **Isolation Constraint**: A class in Layer K **MUST NOT** reference or depend on another class in the same layer (J = K) or higher. This isolates modules cleanly.

### Folder Mapping
- 📂 **[`Modules/Layer0/`](file:///c:/Users/Kyle/Downloads/Projects/Jarvis/Modules/Layer0/) (Core Utilities & Engines)**: Contains settings managers, audio processing pipelines, web scrapers, security engines, and local data files.
- 📂 **[`Modules/Layer1/`](file:///c:/Users/Kyle/Downloads/Projects/Jarvis/Modules/Layer1/) (Bridges & Interfaces)**: Contains system interfaces, data transfer objects, and local SSE/TCP server bridges.
- 📂 **[`Modules/Layer2/`](file:///c:/Users/Kyle/Downloads/Projects/Jarvis/Modules/Layer2/) (WPF UI Overlays)**: Glassmorphic user interface overlay windows constructed programmatically in C# (no XAML files).
- 📂 **[`Modules/Layer3/`](file:///c:/Users/Kyle/Downloads/Projects/Jarvis/Modules/Layer3/) (Command Parsing & Handlers)**: Keyboard routing commands and suggestion providers.

---

## ⚡ Core Systems & Features

### 1. Multi-Engine AI Prober & Router (`LlmRouter.cs`)
Jarvis maintains a parallel diagnostic prober that benchmark-tests **17 concurrent AI inference endpoints** (Ollama, LM Studio, Gemini, Groq, Mistral, OpenRouter, Perplexity, DeepSeek, SambaNova, Cerebras, etc.) using `Task.WhenAll`. 
- **Auto-Failover**: If your preferred model fails or goes offline, the router instantly switches to the next lowest-latency active cloud or local model in the chain without interrupting your workflow.
- **Token Saver**: Automatically trims conversational history to fit prompt context windows efficiently.

### 2. Reflection-Based Dynamic Command Registry
- **Decoupled Commands**: All command handlers implement the `ICommandHandler` interface.
- **Zero-Config Extensions**: Instead of mapping commands to hardcoded enums, the `CommandParser` dynamically scans assembly types using reflection at boot time. Developers can drop in a new handler file under `Layer3/Handlers/` and it will be registered automatically without modifying a single core file.

### 3. Integrated Reverse Engineering Suite (`DisassemblerSuiteOverlay.cs`)
An overlay dedicated to reversing and decompilation. On first use, it automatically downloads and builds workspace tools under the `Data/ReversedTools` folder:
- **Ghidra**: Java-based headless PE/ELF analysis.
- **pycdc & pork**: Locally compiled C++ decompiler for Python `.pyc` bytecode.
- **Krakatau & javabytes**: Bytecode interpreters and decompilers for Java `.class` / `.jar` files.
- **jadx & APK Decompiler**: Android APK decompilers.
- **Features**: Includes hex viewing with patch diffing, symbol variable grouping, and assembly editing mode.

### 4. Advanced Web Scraping & Crawling (`WebScraperManager.cs`)
An automated web crawler and parser that extracts data from online resources:
- **Recursive Scraper**: Follows page structures recursively to extract relevant developer documentation.
- **Readability Parser**: Uses heuristics to extract main text from articles, stripping navigation headers, sidebars, and footer clutter.
- **API and Table Extractor**: Translates raw HTML table rows into clean, parseable JSON arrays.

### 5. Media Downloader (`DownloadMediaRunner.cs`)
Handles streaming downloads of audio and videos:
- **Emoji-safe pathing**: Process streams are configured with UTF-8 encoding to prevent Windows ANSI conversion from garbling folders containing emojis (e.g. `🎵 All Songs`).
- **Direct Save**: Supports saving files directly to custom destination directories without forced nested folder restructuring.

---

## ⚙️ Performance & Optimization Settings

WPF's frosted glass `BlurEffect` and background processes can be heavy on some systems. Jarvis includes optimization settings to run efficiently:

- **Low-VFX Performance Mode**: Can be enabled in **Settings -> Visual Options**. This disables GPU-heavy window blurs and replaces them with solid, semi-transparent overlays for ultra-low GPU consumption.
- **Parallel Boot Sequences**: The bootloader inside `App.xaml.cs` runs heavy engine initializations concurrently in separate threads. The HUD becomes active in milliseconds while larger indexes load gracefully in the background.
- **Vosk STT Deferral**: The heavy 40MB offline Vosk speech model is loaded **lazily** only when a speech recognition file command is run, freeing up ~100MB of startup memory.
