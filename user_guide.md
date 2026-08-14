# 🤖 JARVIS SYSTEM COMPANION — MASTER USER & FEATURE GUIDE

Welcome to the **JARVIS System Companion Master User Guide**. This document provides an exhaustive reference for all advanced features, overlays, CLI search commands, OAuth2 authentication, continuous screen monitoring, Model Context Protocol (MCP) servers, and acoustic dataset classification tools.

---

## ⚡ 1. MODEL CONTEXT PROTOCOL (MCP) STUDIO & REGISTRY
Jarvis includes full client support for the **Model Context Protocol (MCP)** standard (`2024-11-05`), enabling seamless integration with local and remote MCP server instances over STDIO or HTTP/SSE streams.

### HUD Commands
- `mcp` / `mcp studio` / `mcp gui` / `mcp servers` ➔ Opens the **⚡ MCP Registry Studio Overlay**.
- `mcp add roblox` ➔ 1-click registration for Roblox Studio MCP Server.
- `mcp add filesystem` ➔ 1-click registration for Filesystem MCP Server.

### Features & Capabilities
1. **Live Connection Badges**: Displays `🟢 Connected`, `🔴 Idle`, or `⚠️ Error` status for all registered servers.
2. **1-Click Presets**:
   - **Roblox Studio MCP Server**: Executes `cmd.exe /c cd /d %LOCALAPPDATA%\Roblox && .\mcp.bat`.
   - **Filesystem MCP Server**: Executes `npx -y @modelcontextprotocol/server-filesystem`.
   - **Brave Search MCP Server**: Executes `npx -y @modelcontextprotocol/server-brave-search`.
3. **Raw JSON Config Pasting**: Paste raw JSON payloads directly into the text box (e.g. `mcpServers` format containing `voicebox`, `claude mcp`, etc.) and click `[ ⚡ Paste & Import Raw JSON Config ]`.
4. **Config Persistence Memory**: Automatically saves to `Data/mcp_config.json` and syncs with `%USERPROFILE%\.gemini\antigravity\mcp_config.json`.

---

## 📹 2. REAL-TIME AI SCREEN VISION & CONTINUOUS MONITORING
Jarvis features a background continuous screen monitoring engine paired with Google Gemini 1.5 Flash Vision AI analysis.

### HUD Commands
- `screen` / `screen vision` / `screen monitor` ➔ Opens **📹 AI Screen Vision & Monitoring Studio**.
- `analyze screen` / `what is on my screen` / `explain screen` ➔ Takes an instant desktop snapshot and runs Gemini Vision AI analysis.
- `start screen monitoring` / `stop screen monitoring` ➔ Toggles continuous background screen watcher.

### Features & Capabilities
1. **Continuous Background Tracking**: Automatically captures primary screen snapshots at your configured interval (e.g. every `5s`).
2. **Active Window & Process Detector**: Tracks active application titles (e.g. `Visual Studio Code`, `Roblox Studio`, `Chrome`).
3. **Gemini Vision AI Integration**: Queries Gemini Vision AI with base64 encoded screenshots to explain visible code, open windows, error popups, or active UI components.
4. **Sampling Interval Slider**: Custom interval slider from `1s` to `30s`.

---

## 🔑 3. GOOGLE GEMINI & GITHUB OAUTH2 AUTHENTICATION
Authenticate directly with Google Gemini AI and GitHub using standard OAuth2 authorization flows with local redirect servers.

### HUD Commands
- `oauth` / `auth` / `login` ➔ Opens **🔑 OAuth2 Account Authentication Studio**.
- `google login` ➔ Launches browser to authorize Google OAuth2 credentials.
- `github login` ➔ Launches browser to authorize GitHub OAuth2 credentials.

### Features & Capabilities
1. **Local Redirect Listener**: Listens on `http://localhost:8989/oauth/callback/` to capture OAuth authorization codes automatically.
2. **Google Gemini Account Auth**: Obtains access tokens and refresh tokens, saving your profile email (`🟢 Connected: user@example.com`).
3. **GitHub Account Auth**: Obtains GitHub API access tokens, saving your handle (`🟢 Connected: @GithubUser`).
4. **Custom Client Credentials**: Text inputs to provide custom Google Client ID / Secret and GitHub Client ID / Secret.

---

## 🎙️ 4. SPEECH RECOGNITION, 6-SECOND SILENCE GATE & DATASET STUDIO
Advanced voice control engine with noise filtering and acoustic dataset recording.

### Voice Directives & Settings
- **6-Second Silence Window**: Speech tokens accumulate in `FullSentenceAccumulator.cs` until **6 seconds (`6000ms`) of silence** are confirmed before processing voice queries.
- **Microphone Audio Energy Floor**: Adjust energy floor (`2%` to `30%`, default `12%`) and confidence threshold (`0.30` to `0.98`, default `0.75`).
- **Master Voice Mode Toggle**: Checkbox in Settings/Voice Studio and HUD commands (`disable voice`, `enable voice`, `voicemode off`, `voicemode on`).
- **Voice Classification Studio (Tab 6)**: View recorded audio clips in `Data/VoiceDataset/*.wav`, play clips, label (`Command`, `AI Chat`, `Wake Word`, `Noise`), and train acoustic dataset models.

---

## 🖼️ 5. UNIVERSAL MEDIA CONVERTER STUDIO
1-click FFmpeg media conversion suite for graphics and video assets.

### HUD Commands
- `convert media` / `media converter` / `webp to png` / `gif to mp4` ➔ Opens **🖼️ Universal Media Converter Studio**.

### Conversion Capabilities
- 🖼️ `WEBP` ➔ `PNG` (Image conversion with transparency)
- 🎬 `GIF` ➔ `MP4` (High-efficiency video conversion)
- 🖼️ `PNG` ➔ `WEBP` (Optimized web graphic conversion)
- 🎬 `MP4` ➔ `GIF` (Animated GIF creation)
- 🎵 `MP3` ➔ `WAV` (PCM audio extraction)

---

## 🧠 6. USER ACTIVITY HISTORY & LLM CONTEXT INJECTION
Every query sent to Gemini AI or local LLMs (Ollama) automatically receives real-time user environment context:

### Context Included
- **Active Window**: Active application title and process name.
- **Local Time**: Formatted system timestamp.
- **Recent Commands**: History of executed HUD queries and commands.
- **Clipboard Content**: Recent copied text snippets.
- **Online Gate**: Search suggestions rank Gemini AI #1 when connected online, automatically falling back to local Ollama LLMs when offline.

---

## 🪟 7. WINDOW PERSISTENCE MEMORY & GUI CLIPBOARD PASTING

### Window Geometry & Minimized State Memory
- Remembers `Left`, `Top`, `Width`, `Height`, and `IsMinimized` (Mini-Mode) for all overlay windows across app restarts.
- Saves automatically on move, resize, minimize, or close events via `WindowMemoryManager.cs`.

### GUI Clipboard Pasting & Context Menus
- Full `Ctrl+V` pasting across all text input boxes.
- Automatic glassmorphic right-click context menu on all TextBoxes with:
  - `[ 📋 Paste (Ctrl+V) ]`
  - `[ 📄 Copy (Ctrl+C) ]`
  - `[ ✂️ Cut (Ctrl+X) ]`
  - `[ 🔍 Select All (Ctrl+A) ]`

---

*JARVIS System Companion Architecture | Designed & Built for Maximum Productivity*
