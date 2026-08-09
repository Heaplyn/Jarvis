// Developer: heaplyn
// Date: 2026-08-08
// Summary: Main application entry point that initializes the tray icon NotifyIcon menu and controls window instances.

using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace JarvisLauncher
{
    public partial class App : Application
    {
        private NotifyIcon? _notifyIcon;
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize main window
            _mainWindow = new MainWindow();

            // Run command handler startup initializations
            CommandParser.Initialize();

            // Setup Tray Icon using WinForms NotifyIcon
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "Jarvis HUD Launcher"
            };

            // Setup Context Menu for Tray Icon
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Show Launcher", null, (s, ev) => ShowLauncher());
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, (s, ev) => ExitApp());
            _notifyIcon.ContextMenuStrip = contextMenu;

            // Double click tray icon to show window
            _notifyIcon.DoubleClick += (s, ev) => ShowLauncher();

            // Show window initially (or run in background)
            // For convenience, we show it on startup
            ShowLauncher();
        }

        private void ShowLauncher()
        {
            if (_mainWindow != null)
            {
                _mainWindow.ShowHUD();
            }
        }

        private void ExitApp()
        {
            _notifyIcon?.Dispose();
            _notifyIcon = null;

            // Shut down WPF application
            Application.Current.Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
