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
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;
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
            LoadingWindow? loadingWindow = null;
            try
            {
                // 1. Show Loading Screen Immediately
                loadingWindow = new LoadingWindow();
                loadingWindow.Show();

                // 2. Fast Initialization
                loadingWindow.UpdateStatus("Initializing Core Services...", 10);
                CoreRegistry.InitializeAll();

                loadingWindow.UpdateStatus("Applying visual interface...", 25);
                ThemeManager.ApplyTheme(CoreRegistry.Data.Settings.Current.THEME);

                // 3. Background Services (Sequential for progress tracking)
                await Task.Run(async () => {
                    void Update(string msg, double p) => loadingWindow?.UpdateStatus(msg, p);

                    Update("Connecting predictive data streams...", 45);
                    try { PredictiveStreamManager.Start(); } catch { }

                    Update("Harvesting system knowledge...", 55);
                    try { SystemKnowledgeManager.Start(); } catch { }

                    Update("Calibrating command handlers...", 65);
                    try { CommandParser.Initialize(); } catch { }

                    Update("Establishing mobile bridge...", 85);
                    try { MobileBridgeServer.Start(CoreRegistry.Data.Settings.Current.MOBILE_PORT); } catch { }
                    try { PersonalityEvolver.Start(); } catch { }
                    try { EmotionalContextManager.Start(); } catch { }
                    try { ChronoLogManager.StartAutoTracker(); } catch { }
                    try { ScreenMonitorEngine.Start(15); } catch { }

                    Update("Finalizing HUD environment...", 95);
                    try { SelfHealingManager.Initialize(); } catch { }
                    try { ReminderManager.Initialize(); } catch { }
                    try { JarvisPluginManager.Initialize(); } catch { }
                    try { EvolutionManager.StartContinuousEvolution(); } catch { }

                    await Task.Delay(500);
                });

                // 4. Build and Show Main Window
                _mainWindow = new MainWindow();
                this.MainWindow = _mainWindow;

                loadingWindow.UpdateStatus("System Online.", 100);
                await Task.Delay(300);

                _mainWindow.Show();
                _mainWindow.ShowHUD();

                loadingWindow.Close();

                // 5. System Tray
                try {
                    _notifyIcon = new NotifyIcon { Icon = SystemIcons.Application, Visible = true, Text = "Jarvis HUD" };
                    var contextMenu = new ContextMenuStrip();
                    contextMenu.Items.Add("Show Launcher", null, (s, ev) => _mainWindow?.Dispatcher.Invoke(() => _mainWindow.ShowHUD()));
                    contextMenu.Items.Add(new ToolStripSeparator());
                    contextMenu.Items.Add("Exit", null, (s, ev) => Application.Current.Shutdown());
                    _notifyIcon.ContextMenuStrip = contextMenu;
                    _notifyIcon.DoubleClick += (s, ev) => _mainWindow?.Dispatcher.Invoke(() => _mainWindow.ShowHUD());
                } catch { }

                // 6. Restore Overlays
                _ = Task.Delay(1000).ContinueWith(_ => {
                    Application.Current.Dispatcher.Invoke(() => {
                        try { WindowPositionManager.RestoreOpenOverlays(); } catch { }
                    });
                });
            }
            catch (Exception ex)
            {
                loadingWindow?.Close();
                MessageBox.Show("Jarvis failed to boot: " + ex.Message);
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
            try
            {
                // Graceful shutdown of active background services
                VoiceActivationManager.Stop();
                MobileBridgeServer.Stop();
                MemoryManager.Stop();

                _notifyIcon?.Dispose();
            }
            catch { }
            base.OnExit(e);
        }
    }
}
