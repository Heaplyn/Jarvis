# Jarvis HUD Launcher

A lightweight, responsive, global dropdown Command HUD Launcher for Windows, built with **.NET 8** and **WPF**. Press the global backtick key (\`) to reveal the sliding dashboard launcher, execute commands, calculate equations, control volume, or launch processes, and watch it fade away seamlessly when done.

---

## Features

- 🎹 **Global Hotkey bindings**: Toggle the HUD instantly using backtick (\`) or force terminate using `Ctrl + Shift + C`.
- 🎨 **Sleek Aesthetics**: A dark purple / midnight violet glassmorphic HUD window with subtle lavender highlights and smooth slide/fade animations.
- 📐 **Dynamic Window Sizing**: The window dynamically shrinks to a single input field when empty and expands downward as suggestions appear.
- 🎚️ **System Control Handlers**:
  - **Math**: Calculate algebraic expressions instantly on-the-fly (`DataTable.Compute`).
  - **Volume**: Change system sound levels or toggle mute statuses using standard NAudio device handlers.
  - **Lock**: Instantly secure the active Windows session.
  - **Restart**: Safely restart the launcher thread.
  - **App Launcher**: A general command runner to trigger links, directories, or executable files.
- 🔍 **Fuzzy Similarity Sorting**: Inputs are ranked against command keywords using character-intersection edit closeness, sorting the most relevant suggestion to the top.
- 📥 **Tray Icon Integration**: Minimize the app to the system tray with context menus to launch or exit.

---

## Project Architecture (Strict Ring Dependency Layering)

The project codebase is strictly segregated according to Layered Ring Dependency rules: **Layer N can only reference Layer M if and only if M <= N**.

```
   ┌──────────────────────────────────────────────┐
   │         Layer 3: Presentation (Client)       │  <-- Main Window XAML & Code-behind
   └──────────────────────┬───────────────────────┘
                          ▼
   ┌──────────────────────────────────────────────┐
   │         Layer 2: Domain Implementation       │  <-- Command Parser & Handler classes
   └──────────────────────┬───────────────────────┘
                          ▼
   ┌──────────────────────────────────────────────┐
   │         Layer 1: Domain Core (Interfaces)    │  <-- ICommandHandler & CommandResult
   └──────────────────────┬───────────────────────┘
                          ▼
   ┌──────────────────────────────────────────────┐
   │         Layer 0: Infrastructure Core         │  <-- NativeMethods & SearchUtil
   └──────────────────────────────────────────────┘
```

- **Layer 0 (Infrastructure)**: Contains raw Win32 interop wrappers (`NativeMethods.cs`) and mathematical fuzzy distance helpers (`SearchUtil.cs`).
- **Layer 1 (Domain Core)**: Contains the base models (`CommandResult.cs`) and interfaces (`ICommandHandler.cs`).
- **Layer 2 (Domain Implementation)**: Coordinates evaluator dispatching registry (`CommandParser.cs`) and hosts the actual query modules (`Handlers/`).
- **Layer 3 (Presentation)**: The GUI window layout and input events (`MainWindow.xaml` & `MainWindow.xaml.cs`).
- **Root**: Startup tray hooks (`App.xaml` / `App.xaml.cs`) and configuration project specs (`JarvisLauncher.csproj`).

---

## How to Run

### Prerequisites
- Install **.NET 8.0 SDK** (or later) on Windows.

### Build and Run Command Line
Open a PowerShell or CMD terminal in the project directory (`C:\Users\Kyle\Downloads\Projects\Jarvis`) and run:

```powershell
# 1. Restore packages and clean old build caches
dotnet clean

# 2. Build the executable
dotnet build

# 3. Launch the Jarvis Launcher background service
dotnet run
```

---

## Developer Signature
- **Developer**: heaplyn
- **Date**: 2026-08-08
