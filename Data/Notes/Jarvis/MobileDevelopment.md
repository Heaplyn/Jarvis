
# Jarvis Mobile Development & C# Architecture
- Target Platform: .NET MAUI / C# Mobile
- Current Focus: Integrating local wake-word detection (`LocalWakeWordDetector.cs`) and audio accumulator pipelines (`FullSentenceAccumulator.cs`) to achieve robust interruptibility.
- Build Warnings: Addressing Android 16 KB memory page size requirements across NuGet packages (`xamarin.androidx.camera.core`).
- Cross-Platform Utilities: Managing shared service bridges, theme definitions, and UI converters in `Modules\Layer0\Models` and `Modules\Layer4\Pages`.
