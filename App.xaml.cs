// Developer: heaplyn
// Date: 2026-08-14
// Summary: Main application entry point.

using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using System.Threading.Tasks;
using System.IO;

namespace JarvisLauncher
{
    public partial class App : Application
    {
        private NotifyIcon? _notifyIcon;
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            KillPreviousJarvisInstances();
            base.OnStartup(e);

            this.DispatcherUnhandledException += (s, ev) =>
            {
                ev.Handled = true;
            };

            RunBootSequence();
        }

        private async void RunBootSequence()
        {
            try
            {
                // 1. Core Config (Fast)
                System.Diagnostics.Debug.WriteLine("BOOT: Loading settings...");
                SettingsManager.Load();

                System.Diagnostics.Debug.WriteLine("BOOT: Applying theme...");
                ThemeManager.ApplyTheme(SettingsManager.Current.THEME);

                // 2. Build Window
                System.Diagnostics.Debug.WriteLine("BOOT: Creating MainWindow...");
                _mainWindow = new MainWindow();
                this.MainWindow = _mainWindow;

                // 3. Show Immediately
                System.Diagnostics.Debug.WriteLine("BOOT: Showing Window...");
                _mainWindow.Show();
                _mainWindow.ShowHUD();

                // 4. Background Initialization
                _ = Task.Run(async () => {
                    // Primitive log to see if this task even runs
                    System.IO.File.AppendAllText("boot_debug.log", "Task started at " + DateTime.Now + "\n");

                    DebugConsoleOverlay.Log("System-Boot", "Background initialization started...");
                    try { VoiceActivationManager.Start(); } catch (Exception ex) { DebugConsoleOverlay.Log("Error", "Voice engine failed: " + ex.Message); }
                    try { PredictiveStreamManager.Start(); } catch { }
                    try { SystemKnowledgeManager.Start(); } catch { }

                    try { SelfHealingManager.Initialize(); } catch { }
                    try { CommandParser.Initialize(); } catch { }
                    try { await VoiceDatasetManager.InitializeAsync(); } catch { }
                    try { await VoiceTrainerManager.InitializeAsync(); } catch { }
                    try { MemoryManager.Start(); } catch { }
                    try { MobileBridgeServer.Start(SettingsManager.Current.MOBILE_PORT); } catch { }
                    try { ReminderManager.Initialize(); } catch { }
                    try { NotesCuratorManager.Initialize(); } catch { }
                });

                // 5. System Tray (Non-blocking)
                try {
                    _notifyIcon = new NotifyIcon { Icon = SystemIcons.Application, Visible = true, Text = "Jarvis Launcher" };
                    var contextMenu = new ContextMenuStrip();
                    contextMenu.Items.Add("Show Launcher", null, (s, ev) => _mainWindow?.Dispatcher.Invoke(() => _mainWindow.ShowHUD()));
                    contextMenu.Items.Add(new ToolStripSeparator());
                    contextMenu.Items.Add("Exit", null, (s, ev) => Application.Current.Shutdown());
                    _notifyIcon.ContextMenuStrip = contextMenu;
                    _notifyIcon.DoubleClick += (s, ev) => _mainWindow?.Dispatcher.Invoke(() => _mainWindow.ShowHUD());
                } catch { }

                // 6. Restore Overlays (Delayed)
                _ = Task.Delay(1500).ContinueWith(_ => {
                    Application.Current.Dispatcher.Invoke(() => {
                        try { WindowPositionManager.RestoreOpenOverlays(); } catch { }
                    });
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Jarvis failed to start: " + ex.Message);
            }
        }

        private static void KillPreviousJarvisInstances()
        {
            try
            {
                int currentId = System.Diagnostics.Process.GetCurrentProcess().Id;
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("JarvisLauncher"))
                {
                    if (proc.Id != currentId) { try { proc.Kill(); } catch { } }
                }
            }
            catch { }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
