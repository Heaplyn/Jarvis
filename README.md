# ⚡ Jarvis HUD Launcher & AI Desktop Assistant

A lightweight, responsive, global dropdown Command HUD Launcher, System Utility Suite, and Agentic AI Companion for Windows built with **.NET 8** and **WPF**. 

Press the global backtick key (**`**) anywhere in Windows to slide down the command launcher, execute utilities, control your computer, search files or the web, and interact with an agentic AI companion that can modify local files and run system commands.

---

## 💻 System Requirements & Prerequisites

* **Operating System**: Windows 10 or Windows 11 (64-bit)
* **.NET 8.0 SDK**: Download from [.NET 8.0 SDK Official Site](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
* **Node.js**: Download from [Node.js Official Downloads](https://nodejs.org/en/download) (Required for optional media downloader utilities like yt-dlp/lucida)
* **Permissions**: Standard user privileges (Admin required only for system-level process terminations or system updates)

---

## 🛠️ Installation & Building

### 1. Clone the Repository
```powershell
git clone https://github.com/Heaplyn/Jarvis.git
cd Jarvis
```

### 2. Build the Application
Ensure the .NET 8 SDK is installed, then clean and build:
```powershell
dotnet clean
dotnet build
```

### 3. Run Jarvis
Launch the application background service directly from the shell:
```powershell
dotnet run
```

---

## 🎮 Global Keyboard Controls

Once launched, Jarvis runs quietly in your Windows system tray. Control it globally with these keyboard shortcuts:

| Shortcut | Description |
| :--- | :--- |
| **`** *(Backtick key)* | Toggle the drop-down HUD launcher on/off from anywhere in Windows |
| **`Ctrl` + `Shift` + `R`** | Silent hot-reload (rebuilds and restarts Jarvis live in 2 seconds) |
| **`Ctrl` + `Shift` + `C`** | Emergency exit (instantly closes Jarvis and frees system hotkeys) |
| **`Esc`** | Instantly hide HUD launcher or close active overlay window |
| **`Enter`** | Execute highlighted command suggestion |

---

## ⚙️ Quick Configuration & Setup

### 1. Visual Options & Settings GUI
Type **`settings`**, **`options`**, or **`config`** in the launcher and press **Enter** to open the visual Settings GUI:
* 🔑 **Google Gemini API Key**: Paste your key for AI Chat Companion and File Agent capabilities.
* 🐙 **GitHub Personal Access Token**: Auth token for repository self-pushing.
* 📁 **Media Downloads Directory**: Choose default download folders using an interactive Windows folder picker button.
* 🎨 **Active Theme Selector**: Live drop-down preview across all 11 visual themes!

### 2. CLI Key Setup
Alternatively, set keys via launcher commands:
```text
setkey google AIzaSyYourApiKeyHere
setkey github ghp_YourGithubTokenHere
```
*(Settings are saved securely inside `Data/SystemSettings.json`)*

---

## 🌟 Command Handbook & Feature Modules

### 🎨 11 Dynamic Color Themes (`theme <name>`)
Jarvis features dynamic glassmorphic themes applied live to all UI windows:
- `dracula`, `sunset`, `crimson`, `gold`, `nordic`, `purple`, `dark`, `blue`, `green`, `cyberpunk`, `glass`

### 📋 Clipboard History (`cb` / `clip`)
* Monitors system clipboard in background; stores up to 50 text items persistently.
* Type `cb` or `cb <filter>` to browse history and press **Enter** to copy back into your active clipboard!

### ⚙️ Interactive Process Manager GUI (`procs` / `taskmgr`)
* Launches a visual task manager listing your top 15 memory-heavy processes with live PIDs, RAM usage (MB), and a **`💀 End`** termination button.

### 🔍 Global Desktop & Web Search (`search <query>`)
* Type `search <query>` to search local files across `Desktop`, `Documents`, `Downloads`, and `Pictures`, or press **Enter** on **`Search Google`** / **`Search DuckDuckGo`** to launch your default web browser directly!

### ✂️ Quick Snippets & Text Expander (`snippet` / `snip`)
* `snippet add <name> <text>` — Saves text snippets to `Data/Snippets.json`.
* `snip` — Browse all snippets and press **Enter** to copy text to clipboard instantly.

### 📱 Application Launcher (`app <name>` / `apps`)
* Launch installed applications (`app chrome`, `app notepad`, `app calc`, `app explorer`).

### ⚡ Macro Action Chains (`macro <name>`)
* `macro add focus -> theme dark | vol 10 | remind 45m Take a break`
* Run multi-command chains in a single shortcut (`macro focus`).

### ⏰ Reminders & Quick Notes (`remind` / `note`)
* `remind 10m Take a break` — Desktop notification alert when timer expires.
* `note Meeting at 3pm` — Appends timestamped entries to `notes.txt`.

### 🌐 Web Scraper & Summarizer (`fetch <url>`)
* `fetch https://example.com` — Scrapes webpage text and summarizes it with Gemini AI inside the System Terminal.

### 🪟 Window Snap (`snap left` / `snap right`)
* Snaps the active foreground window to the left or right half of your monitor.

### 📊 Live System Monitor (`monitor` / `stats`)
* Floating glassmorphic desktop widget displaying live CPU %, RAM usage, and process count updated every second.

---

## 🤖 Agentic AI Chat Companion (`chat` / `ai`)

Type **`chat`** or **`ai`** to launch your draggable AI Chat Companion. Powered by Google Gemini with resilient dynamic model auto-discovery (`ModelService.ListModels`), the AI has execution powers:

* **File Reading**: Reads local files into context (`[READ_FILE: C:\path\file.cs]`).
* **File Writing**: Creates/overwrites files on your disk (`[WRITE_FILE: C:\path\file.txt]...[END_WRITE]`).
* **App & Native Launching**: Opens files natively (`[OPEN_FILE: path]`) or in the built-in Text Editor (`[OPEN_EDITOR: path]`).
* **Dashboard Pinning**: Pins file shortcuts to your launchpad grid (`[PIN_FILE: path]`).
* **Shell Command Execution**: Runs Command Prompt shell commands (`[EXEC_SHELL: dir]`).
* **HUD Execution**: Triggers launcher commands (`[RUN_COMMAND: theme dracula]`).
* **Dynamic Memory Persistence**: Saves guidelines to `Data/Instructions/` to remember across sessions!

---

## 📂 Project Architecture (5-Layer Ring Dependency)

Jarvis follows strict Layered Ring Dependency rules (**Layer N can only reference Layer M if M <= N**):

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
│         Layer 2: UI Overlays & Agent Exec    │  <-- BaseOverlay, TextEditor, ProcessManager, SystemMonitor
└──────────────────────┬───────────────────────┘
                       ▼
┌──────────────────────────────────────────────┐
│         Layer 1: Domain Core Interfaces      │  <-- ICommandHandler interface & CommandResult data
└──────────────────────┬───────────────────────┘
                       ▼
┌──────────────────────────────────────────────┐
│         Layer 0: Infrastructure Core         │  <-- NativeMethods, SettingsManager, AiAPI, SearchUtil
└──────────────────────────────────────────────┘
```

---

## 📜 License & Credits

Built by **heaplyn**. Open-source under the MIT License.
