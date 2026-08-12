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
            base.OnStartup(e);

            // 📢 BOOT TEST
            System.Windows.MessageBox.Show("JARVIS CONNECT V2.5 STARTING", "System Check");

            try
            {
                SelfHealingManager.Initialize();
                _mainWindow = new MainWindow();
                CommandParser.Initialize();

                _notifyIcon = new NotifyIcon { Icon = SystemIcons.Application, Visible = true, Text = "Jarvis HUD Launcher" };
                var contextMenu = new ContextMenuStrip();
                contextMenu.Items.Add("Show Launcher", null, (s, ev) => ShowLauncher());
                contextMenu.Items.Add(new ToolStripSeparator());
                contextMenu.Items.Add("Exit", null, (s, ev) => ExitApp());
                _notifyIcon.ContextMenuStrip = contextMenu;
                _notifyIcon.DoubleClick += (s, ev) => ShowLauncher();

                ShowLauncher();

                // Ensure we start on 9000
                MobileBridgeServer.Start(9000);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Startup Error: {ex.Message}");
            }
        }

        private void ShowLauncher()
        {
            if (_mainWindow != null) _mainWindow.ShowHUD();
        }

        private void ExitApp()
        {
            _notifyIcon?.Dispose();
            _notifyIcon = null;
            Application.Current.Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
