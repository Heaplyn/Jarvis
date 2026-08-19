
# Architecture Overview

## Layered Ring Structure
- **Layer 0 (Infrastructure Core):** Native Win32 APIs, configuration management, instruction loader, search utilities, and core speech/audio processing components.
- **Layer 1 (Domain Core):** Data contracts, interfaces (`ICommandHandler.cs`), and communication bridges (`MobileBridgeServer.cs`).
- **Layer 2 (UI Overlays):** Glassmorphic WPF overlays, chat panels, and presentation widgets.
- **Layer 3 (Router & Handlers):** Query command parsing and individual feature handlers.
- **Layer 4 (Presentation):** Main application shell, launcher, and theme bindings.
