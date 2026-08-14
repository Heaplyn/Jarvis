// Developer: heaplyn
// Date: 2026-08-08
// Summary: Main application entry point.

using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public partial class App : Application
    {
        private NotifyIcon? _notifyIcon;
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 🔪 Terminate any previous running instances of Jarvis first
            KillPreviousJarvisInstances();

            base.OnStartup(e);

            // Register global exception handlers
            this.DispatcherUnhandledException += (s, ev) =>
            {
                LogFatalException(ev.Exception, "UI Dispatcher Thread");
                ev.Handled = true; // Prevent app crash on UI thread exceptions
            };

            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                if (ev.ExceptionObject is Exception ex)
                {
                    LogFatalException(ex, "AppDomain Background Thread");
                }
            };

            TaskScheduler.UnobservedTaskException += (s, ev) =>
            {
                LogFatalException(ev.Exception, "TaskScheduler Background Task");
                ev.SetObserved(); // Prevent background tasks from crashing the app
            };

            // 🚀 Start Boot sequence
            RunBootSequence();
        }

        private async void RunBootSequence()
        {
            var loader = new LoadingWindow();
            loader.Show();

            bool isFreshBoot = Environment.CommandLine.Contains("--fresh");

            try
            {
                if (isFreshBoot)
                {
                    loader.UpdateStatus("Executing Cold-Start Fresh Boot...", 5);
                    await Task.Delay(500);
                }

                loader.UpdateStatus("Self-Healing check...", 10);
                SelfHealingManager.Initialize();
                await Task.Delay(200);

                loader.UpdateStatus("Building HUD Interface...", 30);
                _mainWindow = new MainWindow();
                await Task.Delay(200);

                loader.UpdateStatus("Parsing Command Handlers...", 50);
                CommandParser.Initialize();
                await Task.Delay(200);

                loader.UpdateStatus("Initializing Reminders...", 70);
                ReminderManager.Initialize();
                await Task.Delay(200);

                loader.UpdateStatus("Initializing AI Curator...", 75);
                NotesCuratorManager.Initialize();
                await Task.Delay(200);

                loader.UpdateStatus("Configuring System Tray...", 85);
                _notifyIcon = new NotifyIcon { Icon = SystemIcons.Application, Visible = true, Text = "Jarvis HUD Launcher" };
                var contextMenu = new ContextMenuStrip();
                contextMenu.Items.Add("Show Launcher", null, (s, ev) => ShowLauncher());
                contextMenu.Items.Add(new ToolStripSeparator());
                contextMenu.Items.Add("Exit", null, (s, ev) => ExitApp());
                _notifyIcon.ContextMenuStrip = contextMenu;
                _notifyIcon.DoubleClick += (s, ev) => ShowLauncher();

                loader.UpdateStatus("Waking up AI...", 95);
                MemoryManager.Start();
                VoiceActivationManager.Start();
                AutonomousAgentEngine.Start();
                BackgroundContextManager.Start();
                VoiceAutoImprover.Start();

                MobileBridgeServer.Start(SettingsManager.Current.MobilePort);
                await Task.Delay(500);

                loader.UpdateStatus("Ready.", 100);
                await Task.Delay(300);

                ShowLauncher();
                WindowPositionManager.RestoreOpenOverlays();
                loader.Close();
            }
            catch (Exception ex)
            {
                loader.Close();
                LogFatalException(ex, "Startup Boot Sequence");
                System.Windows.MessageBox.Show($"Startup Error: {ex.Message}\n\nJarvis could not initialize fully.", "Boot Failure", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LogFatalException(Exception ex, string context)
        {
            string logMsg = $"CRITICAL ERROR in {context}: {ex.Message}{Environment.NewLine}Stack Trace:{Environment.NewLine}{ex.StackTrace}";
            if (ex.InnerException != null)
            {
                logMsg += $"{Environment.NewLine}Inner Exception: {ex.InnerException.Message}{Environment.NewLine}{ex.InnerException.StackTrace}";
            }
            
            // Log to console overlay
            DebugConsoleOverlay.Log("Fatal", logMsg);

            // Save to persistent file
            try
            {
                string logFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jarvis_debug.log");
                string fileContent = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [FATAL] {logMsg}{Environment.NewLine}{new string('-', 50)}{Environment.NewLine}";
                System.IO.File.AppendAllText(logFile, fileContent);
            }
            catch { }

            // Show a visual overlay warning if possible
            try
            {
                TextOverlay.Show($"⚠️ Critical Error: {ex.Message}", 4000);
            }
            catch { }
        }

        private void ShowLauncher()
        {
            if (_mainWindow != null) _mainWindow.ShowHUD();
        }

        private void ExitApp()
        {
            TtsManager.Stop();
            MemoryManager.Stop();
            VoiceActivationManager.Stop();
            AutonomousAgentEngine.Stop();
            BackgroundContextManager.Stop();
            VoiceAutoImprover.Stop();
            _notifyIcon?.Dispose();
            _notifyIcon = null;
            Application.Current.Shutdown();
        }

        private static void KillPreviousJarvisInstances()
        {
            try
            {
                int currentId = System.Diagnostics.Process.GetCurrentProcess().Id;
                string currentProcessName = "JarvisLauncher";

                // 1. Kill any other processes named JarvisLauncher
                var processes = System.Diagnostics.Process.GetProcessesByName(currentProcessName);
                foreach (var proc in processes)
                {
                    if (proc.Id != currentId)
                    {
                        try
                        {
                            proc.Kill();
                            proc.WaitForExit(1000);
                        }
                        catch { }
                    }
                }

                // 2. Kill any stray dotnet processes that might be holding file locks on this project
                var dotnetProcs = System.Diagnostics.Process.GetProcessesByName("dotnet");
                foreach (var p in dotnetProcs)
                {
                    try
                    {
                        // Only kill if it's likely related to this project (optional heuristic)
                        // p.Kill();
                    }
                    catch { }
                }
            }
            catch { }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            TtsManager.Stop();
            MemoryManager.Stop();
            VoiceActivationManager.Stop();
            AutonomousAgentEngine.Stop();
            BackgroundContextManager.Stop();
            VoiceAutoImprover.Stop();
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
