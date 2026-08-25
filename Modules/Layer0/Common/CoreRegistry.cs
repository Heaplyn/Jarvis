// Developer: heaplyn
// Date: 2026-08-18
// Summary: Centralized service registry for modular components.
//          Organized into a logical hierarchy for better maintainability.

using System;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public static class CoreRegistry
    {
        // --- DATA & CONFIGURATION ---
        public static class Data
        {
            public static ISettingsService Settings => _settings ??= new SettingsManager();
            public static IMemoryService Memory => _memory ??= new MemoryManager();
            public static IStorageCleanupService StorageCleanup => _storageCleanup ??= new StorageCleanupManager();
        }

        // --- ARTIFICIAL INTELLIGENCE ---
        public static class Intelligence
        {
            public static ILlmService Llm => _llm ??= new LlmService();
            public static IMathEngine Math => _math ??= new MathEngine();
            public static IProjectContextService ProjectContext => _projectContext ??= new ProjectContextManager();
        }

        // --- USER INTERACTION ---
        public static class Interaction
        {
            public static ITtsService Tts => _tts ??= new TtsManager();
            public static IVoiceActivationService Voice => _voice ??= new VoiceActivationManager();
            public static IAutonomousInterjectionService Autonomous => _autonomous ??= new AutonomousInterjectionManager();
        }

        // --- INFRASTRUCTURE ---
        public static class System
        {
            public static IAppScannerService Apps => _apps ??= new WindowsAppScanner();
            public static IWebOperationService Web => _web ??= new WebOperationManager();
        }

        // --- PRIVATE BACKING FIELDS ---
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
        private static IStorageCleanupService? _storageCleanup;

        public static void InitializeAll()
        {
            Data.Settings.Load();
            ContextNotesManager.Initialize();
            Data.Memory.Start();
            System.Apps.StartScan();
            Interaction.Autonomous.Start();
            Interaction.Voice.Start();
            BackupSyncManager.StartAutoSync();

            Task.Run(async () => {
                try {
                    await Intelligence.Llm.DiscoverAiServersAsync();
                    await Intelligence.ProjectContext.RefreshIndexAsync(AppDomain.CurrentDomain.BaseDirectory);
                } catch { }
            });
        }

        // --- LEGACY REDIRECTS (To prevent immediate breakage) ---
        [Obsolete("Use Data.Settings")] public static ISettingsService Settings => Data.Settings;
        [Obsolete("Use Data.Memory")] public static IMemoryService Memory => Data.Memory;
        [Obsolete("Use Intelligence.Llm")] public static ILlmService Llm => Intelligence.Llm;
        [Obsolete("Use Intelligence.Math")] public static IMathEngine Math => Intelligence.Math;
        [Obsolete("Use Interaction.Tts")] public static ITtsService Tts => Interaction.Tts;
        [Obsolete("Use Interaction.Voice")] public static IVoiceActivationService Voice => Interaction.Voice;
        [Obsolete("Use Interaction.Autonomous")] public static IAutonomousInterjectionService Autonomous => Interaction.Autonomous;
        [Obsolete("Use System.Apps")] public static IAppScannerService Apps => System.Apps;
        [Obsolete("Use System.Web")] public static IWebOperationService Web => System.Web;
        [Obsolete("Use Intelligence.ProjectContext")] public static IProjectContextService ProjectContext => Intelligence.ProjectContext;
        [Obsolete("Use Data.StorageCleanup")] public static IStorageCleanupService StorageCleanup => Data.StorageCleanup;
    }
}
