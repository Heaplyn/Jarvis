# Jarvis HUD Launcher

A lightweight, responsive, global dropdown Command HUD Launcher and AI Companion for Windows, built with **.NET 8** and **WPF**. Press the global backtick key (\`) to reveal the sliding dashboard launcher, execute commands, calculate equations, control volume, or engage with an agentic AI companion that can modify your local files.

---

## 🎨 Visual Preview & Features

- 🎹 **Global Hotkeys**:
  - Toggle the search HUD instantly using the backtick (\`) key.
  - Rebuild and restart silently using `Ctrl + Shift + R`.
  - Force terminate/exit the background service instantly using `Ctrl + Shift + C`.
- 🎨 **Sleek Aesthetics**: A dark purple / midnight violet glassmorphic HUD window with smooth slide animations (defaulting to 100% opacity).
- 📐 **Dynamic Sizing**: The HUD window dynamically shrinks to a single input field when empty and expands downward as suggestions appear.
- 🎚️ **System Control Handlers**:
  - **Math**: Calculate algebraic expressions instantly on-the-fly (`DataTable.Compute`).
  - **Volume**: Change system sound levels or toggle mute statuses using standard NAudio device handlers.
  - **Lock**: Instantly secure the active Windows session.
  - **Brightness**: Adjust monitor backlight percentages using background powershell CIM queries.
  - **System Stats**: Poll system memory loads and CPU delta ticks on a low-priority polling thread.
  - **Local IP**: List active IPv4 interfaces and IPs (click to copy to clipboard).
  - **Recycle Bin**: Empty Recycle Bin via Shell32 P/Invokes (`SHEmptyRecycleBin`).
  - **Process Killer**: Terminate frozen processes by name (`kill chrome`).
  - **PC Power**: Power operations (`sleep`, `shutdown`, `rebootpc`).
- 🧠 **AI Chat Companion (`chat` / `ai`)**: Draggable, selectable text chat overlay connecting to Gemini.
- 📂 **Agent Filesystem Loop**: The AI can read, write, and append files on your computer using structured tag parsers.
- 🏷️ **Alias System**: Map custom shortcuts to long commands persistently (e.g. `alias clean empty` maps `clean` to empty recycle bin).

---

## 📂 Project Architecture (5-Layer Ring Dependency)

The project codebase is strictly segregated according to Layered Ring Dependency rules: **Layer N can only reference Layer M if and only if M <= N**. This prevents circular dependencies and isolates core logic from UI layers.

```
   ┌──────────────────────────────────────────────┐
   │         Layer 4: Main Presentation Client    │  <-- MainWindow XAML & Code-behind, Themes
   └──────────────────────┬───────────────────────┘
                          ▼
   ┌──────────────────────────────────────────────┐
   │         Layer 3: Router & Domain Handlers    │  <-- CommandParser & ICommandHandler classes
   └──────────────────────┬───────────────────────┘
                          ▼
   ┌──────────────────────────────────────────────┐
   │         Layer 2: UI Overlays & Agent Exec    │  <-- BaseOverlay, TextOverlay, CliOutputOverlay, AgentExecutor
   └──────────────────────┬───────────────────────┘
                          ▼
   ┌──────────────────────────────────────────────┐
   │         Layer 1: Domain Core Interfaces      │  <-- ICommandHandler interface & CommandResult data
   └──────────────────────┬───────────────────────┘
                          ▼
   ┌──────────────────────────────────────────────┐
   │         Layer 0: Infrastructure Core         │  <-- NativeMethods, SettingsManager, InstructionsManager, AiAPI
   └──────────────────────────────────────────────┘
```

### 1. Codebase Directory Map

* **`Modules/Layer0/` (Infrastructure)**:
  - [NativeMethods.cs](file:///C:/Users/Kyle/Downloads/Projects/Jarvis/Modules/Layer0/NativeMethods.cs): Raw Win32 user32/kernel32/shell32 interop imports (hotkeys, CPU polling ticks, Recycle Bin APIs).
  - [SearchUtil.cs](file:///C:/Users/Kyle/Downloads/Projects/Jarvis/Modules/Layer0/SearchUtil.cs): Character-intersection edit closeness fuzzy sorting.
  - [SettingsManager.cs](file:///C:/Users/Kyle/Downloads/Projects/Jarvis/Modules/Layer0/SettingsManager.cs): Reads, writes, and serializes API keys and aliases inside `Data/SystemSettings.json`.
  - [InstructionsManager.cs](file:///C:/Users/Kyle/Downloads/Projects/Jarvis/Modules/Layer0/InstructionsManager.cs): Scans files inside the `Data/Instructions/` folder and formats them to supply context to the AI.
  - [AiAPI.cs](file:///C:/Users/Kyle/Downloads/Projects/Jarvis/Modules/Layer0/AiAPI.cs): Performs HTTP requests to Gemini with resilient model failovers (`gemini-1.5-flash-latest`, `gemini-2.0-flash-exp`, etc.).
* **`Modules/Layer1/` (Core Interfaces)**:
  - [ICommandHandler.cs](file:///C:/Users/Kyle/Downloads/Projects/Jarvis/Modules/Layer1/ICommandHandler.cs): Standard query contract.
  - [CommandResult.cs](file:///C:/Users/Kyle/Downloads/Projects/Jarvis/Modules/Layer1/CommandResult.cs): Suggestions structure data.
* **`Modules/Layer2/` (UI Overlays & Agent Executives)**:
  - [BaseOverlay.cs](file:///C:/Users/Kyle/Downloads/Projects/Jarvis/Modules/Layer2/BaseOverlay.cs): Draggable glassmorphic window template.
  - [TextOverlay.cs](file:///C:/Users/Kyle/Downloads/Projects/Jarvis/Modules/Layer2/TextOverlay.cs): Notification alerts.
  - [CliOutputOverlay.cs](file:///C:/Users/Kyle/Downloads/Projects/Jarvis/Modules/Layer2/CliOutputOverlay.cs): Retro monospaced terminal reader.
  - [ChatOverlay.cs](file:///C:/Users/Kyle/Downloads/Projects/Jarvis/Modules/Layer2/ChatOverlay.cs): AI Chat bubble list with selectable copy-paste message textboxes.
  - [AgentExecutor.cs](file:///C:/Users/Kyle/Downloads/Projects/Jarvis/Modules/Layer2/AgentExecutor.cs): File system write/append executor.
* **`Modules/Layer3/` (Router & Command Handlers)**:
  - [CommandParser.cs](file:///C:/Users/Kyle/Downloads/Projects/Jarvis/Modules/Layer3/CommandParser.cs): Resolves query aliases and dispatches query tokens to registered handlers.
  - **`Handlers/`**: Individual implementations of command logic (Math, Volume, Brightness, IP, Power, Process Killer, etc.).
* **`Modules/Layer4/` (Presentation)**:
  - [MainWindow.xaml](file:///C:/Users/Kyle/Downloads/Projects/Jarvis/Modules/Layer4/MainWindow.xaml) & [MainWindow.xaml.cs](file:///C:/Users/Kyle/Downloads/Projects/Jarvis/Modules/Layer4/MainWindow.xaml.cs): Input textboxes and global hooks.
  - **`Themes/`**: Merged Resource Dictionaries for custom styles and slide animations.
* **App Root**:
  - `App.xaml` & `App.xaml.cs`: System tray tray icon and startup initialization hooks.
  - `JarvisLauncher.csproj`: Build targets and NuGet package dependencies (WPF enabled, Windows Forms enabled, implicit usings disabled to avoid namespace crashes).

---

## 🚀 Setup & Execution

### 1. Build and Run
Open a PowerShell terminal in the project directory and run:

```powershell
# Rebuild the executable
dotnet clean
dotnet build

# Launch the Jarvis Launcher background service
dotnet run
```

### 2. Configure your AI API Key
Once the HUD runs, activate it with (\`), type this command, and press Enter to save your API Key:
```
setkey google AIzaSyYourApiKeyHere
```
*(Your key is saved securely inside `Data/SystemSettings.json`)*

---

## 🧠 Using the AI File Agent & Memory Loop

When chatting with the AI Companion (`chat` command):
- **Reading Files**: Jarvis can read files on your computer. If you ask it about a file, it outputs `[READ_FILE: <path>]`, the C# app silently reads the file in the background, pops up a notification, and queries the model again with the content automatically.
- **Writing Files**: Jarvis can create or modify files. If you ask it to write code or text, it writes it using `[WRITE_FILE: <path>]` blocks, sliding in a popup confirmation notice.
- **Persistent Memory**: Jarvis can write context/guideline files to `Data/Instructions/` to update its own instructions. Anything written there is loaded into its system prompt on all subsequent turns.
