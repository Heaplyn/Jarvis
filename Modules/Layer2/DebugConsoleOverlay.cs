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

namespace JarvisLauncher
{
    public class DebugConsoleOverlay : BaseOverlay
    {
        private static DebugConsoleOverlay? _instance;
        private static readonly List<string> _history = new List<string>();
        private static readonly int _maxHistory = 500;

        private readonly TextBox _consoleBox;
        private readonly TextBlock _statusLabel;
        private readonly DispatcherTimer _refreshTimer;

        // Interactive Debug & Error Tools
        private readonly TextBox _searchBox;
        private readonly ComboBox _categoryFilterCombo;
        private readonly TextBox _commandInputBox;

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

            // Write to persistent debug log file
            try
            {
                string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jarvis_debug.log");
                string fileLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{category.ToUpper()}] {message}{Environment.NewLine}";
                File.AppendAllText(logFile, fileLine);
            }
            catch { }

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_instance != null)
                {
                    _instance.UpdateView();
                }
            }));
        }

        private DebugConsoleOverlay()
            : base("🛠️ JARVIS DEBUG & DIAGNOSTICS CONSOLE", width: 680, height: 520)
        {
            var mainGrid = new Grid { Margin = new Thickness(10) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Filters
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Console display
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bottom panel (REPL + Buttons)

            // --- 1. FILTER HEADER AREA ---
            var filterGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Search Text
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                     // Category Dropdown
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                     // Stats label

            var searchStack = new StackPanel { Orientation = Orientation.Horizontal };
            searchStack.Children.Add(new TextBlock 
            { 
                Text = "🔍 Search: ", 
                FontSize = 11, 
                VerticalAlignment = VerticalAlignment.Center, 
                Margin = new Thickness(0, 0, 4, 0) 
            });
            _searchBox = new TextBox 
            { 
                Width = 160, 
                Height = 22, 
                FontSize = 11, 
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(3, 1, 3, 1)
            };
            _searchBox.SetResourceReference(TextBox.BackgroundProperty, "WindowBackgroundBrush");
            _searchBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            _searchBox.SetResourceReference(TextBox.BorderBrushProperty, "WindowBorderBrush");
            _searchBox.TextChanged += (s, e) => UpdateView();
            searchStack.Children.Add(_searchBox);
            Grid.SetColumn(searchStack, 0);
            filterGrid.Children.Add(searchStack);

            var catStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 0) };
            catStack.Children.Add(new TextBlock 
            { 
                Text = "Category: ", 
                FontSize = 11, 
                VerticalAlignment = VerticalAlignment.Center, 
                Margin = new Thickness(0, 0, 4, 0) 
            });
            _categoryFilterCombo = new ComboBox { Width = 110, Height = 22, FontSize = 11 };
            _categoryFilterCombo.Items.Add("ALL");
            _categoryFilterCombo.Items.Add("ERROR / FATAL");
            _categoryFilterCombo.Items.Add("SYSTEM");
            _categoryFilterCombo.Items.Add("BRIDGE");
            _categoryFilterCombo.Items.Add("AI");
            _categoryFilterCombo.SelectedIndex = 0;
            _categoryFilterCombo.SelectionChanged += (s, e) => UpdateView();
            catStack.Children.Add(_categoryFilterCombo);
            Grid.SetColumn(catStack, 1);
            filterGrid.Children.Add(catStack);

            _statusLabel = new TextBlock 
            { 
                FontSize = 11, 
                FontWeight = FontWeights.Medium, 
                VerticalAlignment = VerticalAlignment.Center, 
                Margin = new Thickness(15, 0, 0, 0) 
            };
            _statusLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetColumn(_statusLabel, 2);
            filterGrid.Children.Add(_statusLabel);

            Grid.SetRow(filterGrid, 0);
            mainGrid.Children.Add(filterGrid);

            // --- 2. CONSOLE WINDOW ---
            var consoleBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 10, 8, 16)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6)
            };
            consoleBorder.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");

            _consoleBox = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 235, 140)), // Lime green logs
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            consoleBorder.Child = _consoleBox;
            Grid.SetRow(consoleBorder, 1);
            mainGrid.Children.Add(consoleBorder);

            // --- 3. BOTTOM REPL INPUT & ACTIONS PANEL ---
            var bottomStack = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };

            // REPL Command Input Grid
            var replGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            replGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            replGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _commandInputBox = new TextBox
            {
                Height = 24,
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 2, 4, 2)
            };
            _commandInputBox.SetResourceReference(TextBox.BackgroundProperty, "WindowBackgroundBrush");
            _commandInputBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            _commandInputBox.SetResourceReference(TextBox.BorderBrushProperty, "WindowBorderBrush");
            _commandInputBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    ExecuteDebugCommand();
                    e.Handled = true;
                }
            };
            Grid.SetColumn(_commandInputBox, 0);
            replGrid.Children.Add(_commandInputBox);

            var runBtn = CreateToolButton("⚡ Run", (s, e) => ExecuteDebugCommand());
            runBtn.Height = 24;
            runBtn.Padding = new Thickness(14, 0, 14, 0);
            runBtn.Margin = new Thickness(6, 0, 0, 0);
            Grid.SetColumn(runBtn, 1);
            replGrid.Children.Add(runBtn);

            bottomStack.Children.Add(replGrid);

            // Action Toolbar
            var toolBar = new Grid();
            toolBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var statusUpdateLabel = new TextBlock 
            { 
                Text = "Type syslog commands or diagnostics to execute.", 
                FontSize = 10, 
                FontStyle = FontStyles.Italic, 
                VerticalAlignment = VerticalAlignment.Center 
            };
            statusUpdateLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetColumn(statusUpdateLabel, 0);
            toolBar.Children.Add(statusUpdateLabel);

            var buttonStack = new StackPanel { Orientation = Orientation.Horizontal };

            var viewLogBtn = CreateToolButton("📂 View Log File", (s, e) => {
                try
                {
                    string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jarvis_debug.log");
                    if (File.Exists(logFile))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = logFile,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        TextOverlay.Show("Log file does not exist yet.", 1500);
                    }
                }
                catch (Exception ex)
                {
                    Log("Error", $"Could not open log file: {ex.Message}");
                }
            });

            var copyBtn = CreateToolButton("📋 Copy All", (s, e) => {
                Clipboard.SetText(_consoleBox.Text);
                TextOverlay.Show("Logs copied to clipboard", 1500);
            });

            var clearBtn = CreateToolButton("🧹 Clear Display", (s, e) => {
                lock (_history) _history.Clear();
                UpdateView();
            });

            buttonStack.Children.Add(viewLogBtn);
            buttonStack.Children.Add(copyBtn);
            buttonStack.Children.Add(clearBtn);
            Grid.SetColumn(buttonStack, 1);
            toolBar.Children.Add(buttonStack);

            bottomStack.Children.Add(toolBar);

            Grid.SetRow(bottomStack, 2);
            mainGrid.Children.Add(bottomStack);

            this.UserContent = mainGrid;

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _refreshTimer.Tick += (s, e) => UpdateStatusLabel();
            _refreshTimer.Start();

            UpdateView();
            UpdateStatusLabel();
        }

        private void ExecuteDebugCommand()
        {
            string cmd = _commandInputBox.Text.Trim();
            if (string.IsNullOrEmpty(cmd)) return;

            _commandInputBox.Text = string.Empty;
            Log("Action", $"> {cmd}");

            try
            {
                var suggestions = CommandParser.GetSuggestions(cmd);
                if (suggestions.Count > 0)
                {
                    var matched = suggestions.OrderByDescending(r => r.Similarity).First();
                    Log("Action", $"Executing matched command: '{matched.Title}'");
                    matched.Execute?.Invoke();
                }
                else
                {
                    Log("Warning", $"No matching Jarvis command found for: '{cmd}'");
                }
            }
            catch (Exception ex)
            {
                Log("Error", $"Command execution error: {ex.Message}");
            }
        }

        private void UpdateView()
        {
            if (_consoleBox == null) return;

            string searchText = _searchBox?.Text.Trim() ?? "";
            string selectedCat = _categoryFilterCombo?.SelectedItem as string ?? "ALL";

            List<string> filtered;
            lock (_history)
            {
                filtered = _history.Where(line =>
                {
                    // Filter by category
                    if (selectedCat != "ALL")
                    {
                        if (selectedCat == "ERROR / FATAL")
                        {
                            if (!line.Contains("[ERROR]") && !line.Contains("[FATAL]"))
                                return false;
                        }
                        else
                        {
                            string expectedTag = $"[{selectedCat.ToUpper()}]";
                            if (!line.Contains(expectedTag))
                                return false;
                        }
                    }

                    // Filter by search text
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        if (line.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                            return false;
                    }

                    return true;
                }).ToList();
            }

            _consoleBox.Text = string.Join(Environment.NewLine, filtered);
            _consoleBox.ScrollToEnd();
        }

        private void UpdateStatusLabel()
        {
            int threadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count;
            string bridgeStatus = MobileBridgeServer.IsActive ? "ONLINE (9000)" : "OFFLINE";
            _statusLabel.Text = $"Bridge: {bridgeStatus} | Threads: {threadCount} | Memory: {GC.GetTotalMemory(false) / 1024 / 1024}MB";
        }

        private Button CreateToolButton(string text, RoutedEventHandler onClick)
        {
            var btn = new Button
            {
                Content = text,
                Margin = new Thickness(5, 0, 0, 0),
                Padding = new Thickness(10, 4, 10, 4),
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
