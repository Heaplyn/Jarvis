// Developer: heaplyn
// Date: 2026-08-12
// Summary: Advanced real-time Debug Console overlay for monitoring internal Jarvis events, mobile bridge traffic, and system diagnostics.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Collections.Generic;

using System.Windows.Input;

namespace JarvisLauncher
{
    public class DebugConsoleOverlay : BaseOverlay
    {
        private static DebugConsoleOverlay? _instance;
        private static readonly List<string> _history = new List<string>();
        private static readonly int _maxHistory = 200;

        private readonly TextBox _consoleBox;
        private readonly TextBlock _statusLabel;
        private readonly DispatcherTimer _refreshTimer;

        public static void ShowConsole()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_instance == null)
                {
                    _instance = new DebugConsoleOverlay();
                    _instance.Closed += (s, e) => _instance = null;
                }
                _instance.Show();
                _instance.Activate();
            });
        }

        public static void Log(string category, string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] [{category.ToUpper()}] {message}";
            lock (_history)
            {
                _history.Add(line);
                if (_history.Count > _maxHistory) _history.RemoveAt(0);
            }

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_instance != null)
                {
                    _instance.UpdateView();
                }
            }));
        }

        private DebugConsoleOverlay()
            : base("🛠️ JARVIS DEBUG CONSOLE", width: 600, height: 450)
        {
            var mainGrid = new Grid { Margin = new Thickness(10) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header/Stats
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Console
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Toolbar

            // 1. Header Area
            var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            _statusLabel = new TextBlock { FontSize = 11, FontWeight = FontWeights.Medium };
            _statusLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            headerStack.Children.Add(_statusLabel);
            Grid.SetRow(headerStack, 0);
            mainGrid.Children.Add(headerStack);

            // 2. Main Console Box
            var consoleBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5)
            };
            consoleBorder.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");

            _consoleBox = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                Background = Brushes.Transparent,
                Foreground = Brushes.LimeGreen,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            consoleBorder.Child = _consoleBox;
            Grid.SetRow(consoleBorder, 1);
            mainGrid.Children.Add(consoleBorder);

            // 3. Bottom Toolbar
            var toolStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };

            var clearBtn = CreateToolButton("🧹 Clear", (s, e) => {
                lock (_history) _history.Clear();
                UpdateView();
            });

            var netDiagBtn = CreateToolButton("🌐 Net Diag", (s, e) => {
                ExecuteDiag("netdiag");
            });

            var fixFwBtn = CreateToolButton("🛡️ Fix Firewall", async (s, e) => {
                await MobileBridgeServer.FixFirewallPermissionsAsync();
                Log("System", "Firewall repair command dispatched.");
            });

            var copyBtn = CreateToolButton("📋 Copy All", (s, e) => {
                Clipboard.SetText(_consoleBox.Text);
                TextOverlay.Show("Logs copied to clipboard", 1500);
            });

            toolStack.Children.Add(netDiagBtn);
            toolStack.Children.Add(fixFwBtn);
            toolStack.Children.Add(copyBtn);
            toolStack.Children.Add(clearBtn);

            Grid.SetRow(toolStack, 2);
            mainGrid.Children.Add(toolStack);

            this.UserContent = mainGrid;

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _refreshTimer.Tick += (s, e) => UpdateStatusLabel();
            _refreshTimer.Start();

            UpdateView();
            UpdateStatusLabel();
        }

        private void ExecuteDiag(string cmd)
        {
            Log("Action", $"Running {cmd}...");
            CommandParser.ExecuteFirstSuggestion(cmd);
        }

        private void UpdateView()
        {
            lock (_history)
            {
                _consoleBox.Text = string.Join(Environment.NewLine, _history);
            }
            _consoleBox.ScrollToEnd();
        }

        private void UpdateStatusLabel()
        {
            int threadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count;
            string bridgeStatus = MobileBridgeServer.IsActive ? "ONLINE (9000)" : "OFFLINE";
            _statusLabel.Text = $"Status: {bridgeStatus} | Threads: {threadCount} | Memory: {GC.GetTotalMemory(false) / 1024 / 1024}MB";
        }

        private Button CreateToolButton(string text, RoutedEventHandler onClick)
        {
            var btn = new Button {
                Content = text,
                Margin = new Thickness(5, 0, 0, 0),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 10,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            btn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            btn.Click += onClick;
            return btn;
        }
    }
}
