// Developer: heaplyn
// Date: 2026-08-17
// Summary: Centralized service registry for modular components.
//          Enables transition from static helpers to injectable services.

using System;

namespace JarvisLauncher
{
    public static class CoreRegistry
    {
        private static ISettingsService? _settings;
        private static ITtsService? _tts;
        private static IMathEngine? _math;
        private static ILlmService? _llm;
        private static IMemoryService? _memory;
        private static IWebOperationService? _web;
        private static IAppScannerService? _apps;
        private static IAutonomousInterjectionService? _autonomous;
        private static IVoiceActivationService? _voice;
        private static IProjectContextService? _projectContext;

        public static ISettingsService Settings => _settings ??= new SettingsManager();
        public static ITtsService Tts => _tts ??= new TtsManager();
        public static IMathEngine Math => _math ??= new MathEngine();
        public static ILlmService Llm => _llm ??= new LlmService();
        public static IMemoryService Memory => _memory ??= new MemoryManager();
        public static IWebOperationService Web => _web ??= new WebOperationManager();
        public static IAppScannerService Apps => _apps ??= new WindowsAppScanner();
        public static IAutonomousInterjectionService Autonomous => _autonomous ??= new AutonomousInterjectionManager();
        public static IVoiceActivationService Voice => _voice ??= new VoiceActivationManager();
        public static IProjectContextService ProjectContext => _projectContext ??= new ProjectContextManager();

        public static void InitializeAll()
        {
            Settings.Load();
            Memory.Start();
            Apps.StartScan();
            Autonomous.Start();
            Voice.Start();
            ProjectContext.RefreshIndexAsync(AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}
