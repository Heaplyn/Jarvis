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
using System.Diagnostics;
using System.Reflection;
using System.Linq;

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
                InitializeTelerik();
                ThemeManager.ApplyTheme(CoreRegistry.Data.Settings.Current.THEME);

                // 3. Fire background service initializations in parallel
                loadingWindow.UpdateStatus("Starting engines...", 50);

                var depTask = Task.Run(() => { try { EnsureDependenciesAsync().GetAwaiter().GetResult(); } catch { } });
                var predTask = Task.Run(() => { try { PredictiveStreamManager.Start(); } catch { } });
                var sysTask = Task.Run(() => { try { SystemKnowledgeManager.Start(); } catch { } });
                var cmdTask = Task.Run(() => { try { CommandParser.Initialize(); } catch { } });
                var mobTask = Task.Run(() => { try { MobileBridgeServer.Start(CoreRegistry.Data.Settings.Current.MOBILE_PORT); } catch { } });
                var persTask = Task.Run(() => { try { PersonalityEvolver.Start(); } catch { } });
                var emoTask = Task.Run(() => { try { EmotionalContextManager.Start(); } catch { } });
                var chronoTask = Task.Run(() => { try { ChronoLogManager.StartAutoTracker(); } catch { } });
                var screenTask = Task.Run(() => { try { ScreenMonitorEngine.Start(15); } catch { } });
                var selfTask = Task.Run(() => { try { SelfHealingManager.Initialize(); } catch { } });
                var remTask = Task.Run(() => { try { ReminderManager.Initialize(); } catch { } });
                var plugTask = Task.Run(() => { try { JarvisPluginManager.Initialize(); } catch { } });
                var evoTask = Task.Run(() => { try { EvolutionManager.StartContinuousEvolution(); } catch { } });

                // Await only the essential core setup (dependencies & command calibration)
                await Task.WhenAll(depTask, cmdTask);

                // 4. Build and Show Main Window
                loadingWindow.UpdateStatus("System Online.", 100);
                
                _mainWindow = new MainWindow();
                this.MainWindow = _mainWindow;

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
                _ = Task.Delay(500).ContinueWith(_ => {
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

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime dependency health-check
        //  SharpCompress is a NuGet reference so it's bundled into the EXE on
        //  every build. This is a safety net for corrupted or stripped deploys.
        // ─────────────────────────────────────────────────────────────────────
        private static async Task EnsureDependenciesAsync()
        {
            // Required assemblies: name → NuGet package id
            var required = new[] { ("SharpCompress", "SharpCompress") };

            bool allPresent = true;
            foreach (var (asmName, _) in required)
            {
                try { Assembly.Load(new AssemblyName(asmName)); }
                catch { allPresent = false; break; }
            }

            if (allPresent) return; // Fast-path: everything is fine

            // Slow-path: try to restore via dotnet CLI
            try
            {
                var exeDir  = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName) ?? "";
                var csproj  = Directory.GetFiles(exeDir, "*.csproj", SearchOption.TopDirectoryOnly)
                               .FirstOrDefault() ?? "";

                if (string.IsNullOrEmpty(csproj)) return; // Can't find project – give up silently

                var psi = new ProcessStartInfo("dotnet", $"restore \"{csproj}\"")
                {
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                    await proc.WaitForExitAsync(new System.Threading.CancellationTokenSource(60_000).Token)
                              .ConfigureAwait(false);
            }
            catch { /* Swallow: app continues even without the restore */ }
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

        private void InitializeTelerik()
        {
            try
            {
                // Set Global Telerik Theme to Fluent with DARK variation
                Telerik.Windows.Controls.StyleManager.ApplicationTheme = new Telerik.Windows.Controls.FluentTheme();
                Telerik.Windows.Controls.FluentPalette.LoadPreset(Telerik.Windows.Controls.FluentPalette.ColorVariation.Dark);

                // Calibrate Palette for Jarvis HUD
                Telerik.Windows.Controls.FluentPalette.Palette.AccentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00FFFF");
                Telerik.Windows.Controls.FluentPalette.Palette.BasicColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#333333");
                Telerik.Windows.Controls.FluentPalette.Palette.MainColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1A1A1A");
                Telerik.Windows.Controls.FluentPalette.Palette.MarkerColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF");
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
