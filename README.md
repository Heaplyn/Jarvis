# Jarvis HUD Documentation

This directory contains the core documentation for the Jarvis Windows HUD.

## Documentation Index
- [System Architecture](Docs/ARCHITECTURE.md): Deep dive into Layers 0-4.
- [AI & LLM Logic](Docs/AI_AND_LLM.md): How Jarvis thinks and acts.
- [Voice & Activation](Docs/VOICE_SYSTEM.md): Vosk, wake words, and echo rejection.
- [Developer Guidelines](Docs/DEV_GUIDE.md): Styling, debugging, and best practices.

## Project Structure
- `Modules/Layer0`: Core engines (AI, Voice, OAuth, Settings).
- `Modules/Layer1`: Background services (Bridge, Predict).
- `Modules/Layer2`: HUD UI Overlays.
- `Modules/Layer3`: Command Parser and Handlers.
- `Modules/Layer4`: Main Application Shell.
