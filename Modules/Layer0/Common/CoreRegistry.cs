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
            // Settings MUST load synchronously — the theme, scheduler, and everything else read it.
            // Nothing else runs here: the heavy initializers are deferred to InitializeDeferred()
            // which the app calls AFTER the HUD is visible, so they don't contend with window
            // construction (running them during boot cost ~1.9s of blocking time).
            Data.Settings.Load();
        }

        private static int _deferredStarted;

        /// <summary>
        /// Heavy, non-UI-critical initializers. Call AFTER the main window is shown so they run in
        /// the background without slowing the HUD's first paint. Idempotent.
        /// </summary>
        public static void InitializeDeferred()
        {
            if (global::System.Threading.Interlocked.CompareExchange(ref _deferredStarted, 1, 0) != 0) return;

            Task.Run(() => {
                try { ContextNotesManager.Initialize(); } catch { }
                try { Data.Memory.Start(); } catch { }
                try { System.Apps.StartScan(); } catch { }
                // SECURITY: autonomous interjection is gated by the opt-in flag (it was not before).
                try { if (Data.Settings.Current.IS_AUTONOMOUS_MODE_ENABLED) Interaction.Autonomous.Start(); } catch { }
                try { Interaction.Voice.Start(); } catch { }
                try { BackupSyncManager.StartAutoSync(); } catch { }
                // Periodic screen perception (feeds the AI's [PERCEPTION CONTEXT]).
                try { if (Data.Settings.Current.ENABLE_SCREEN_PERCEPTION)
                        ScreenMonitorEngine.Start(Data.Settings.Current.SCREEN_PERCEPTION_INTERVAL_SEC); } catch { }
                // Slow background filesystem index for AI file reference.
                try { if (Data.Settings.Current.ENABLE_FILE_INDEXING) FileSystemIndexer.Start(); } catch { }
            });

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
