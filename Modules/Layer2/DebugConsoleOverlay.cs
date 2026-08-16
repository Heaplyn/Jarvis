// Developer: heaplyn
// Date: 2026-08-12
// Summary: Advanced real-time Debug Console overlay for monitoring internal Jarvis events, mobile bridge traffic, and system diagnostics.
// Upgraded with RichText support for color-coded status monitoring.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class DebugConsoleOverlay : BaseOverlay
    {
        private static DebugConsoleOverlay? _instance;
        private static readonly List<LogEntry> _history = new List<LogEntry>();
        private static readonly int _maxHistory = 500;

        private readonly RichTextBox _consoleBox;
        private readonly TextBlock _statusLabel;
        private readonly DispatcherTimer _refreshTimer;

        // Interactive Debug & Error Tools
        private readonly TextBox _searchBox;
        private readonly ComboBox _categoryFilterCombo;
        private readonly ComboBox _verbosityCombo;
        private readonly TextBox _commandInputBox;

        private class LogEntry
        {
            public string Category { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string Timestamp { get; set; } = string.Empty;
            public Brush Color { get; set; } = Brushes.White;

            public string FullLine => $"[{Timestamp}] [{Category.ToUpper()}] {Message}";
        }

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
            LogInternal(category, message, false);
        }

        public static void LogVerbose(string category, string message, bool isMinimal = false)
        {
            int level = SettingsManager.Current.DEBUG_VERBOSITY_LEVEL;

            // 0: None, 1: Minimal (Half), 2: Full
            if (level == 0) return;
            if (level == 1 && !isMinimal) return;

            LogInternal(category, message, true);
        }

        private static void LogInternal(string category, string message, bool isVerbose)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string upperCat = category.ToUpper();
            Brush color = Brushes.White;

            if (isVerbose)
                color = Brushes.Gray;
            else if (upperCat.Contains("ERROR") || upperCat.Contains("FATAL") || upperCat.Contains("FAIL"))
                color = Brushes.Tomato;
            else if (upperCat.Contains("WARN"))
                color = Brushes.Gold;
            else if (upperCat.Contains("AI"))
                color = Brushes.SpringGreen;
            else if (upperCat.Contains("BRIDGE"))
                color = Brushes.Plum;
            else if (upperCat.Contains("SYSTEM"))
                color = Brushes.DeepSkyBlue;
            else if (upperCat.Contains("ACTION") || message.StartsWith(">"))
                color = Brushes.Lime;

            var entry = new LogEntry
            {
                Category = isVerbose ? $"DEBUG-{category}" : category,
                Message = message,
                Timestamp = timestamp,
                Color = color
            };

            lock (_history)
            {
                _history.Add(entry);
                if (_history.Count > _maxHistory) _history.RemoveAt(0);
            }

            // Write to persistent debug log file
            try
            {
                string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jarvis_debug.log");
                string prefix = isVerbose ? "[VERBOSE] " : "";
                string fileLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {prefix}[{upperCat}] {message}{Environment.NewLine}";
                File.AppendAllText(logFile, fileLine);
            }
            catch { }

            if (Application.Current == null) return;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_instance != null)
                {
                    _instance.UpdateView();
                }
            }));
        }

        private DebugConsoleOverlay()
            : base("🛠️ JARVIS DEBUG & DIAGNOSTICS CONSOLE", width: 780, height: 600)
        {
            var mainGrid = new Grid { Margin = new Thickness(12) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Filters
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Console display
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bottom panel (REPL + Buttons)

            // --- 1. FILTER HEADER AREA (Styled like the request) ---
            var filterGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Search Label
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) }); // Search Box
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Cat Label
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) }); // Cat Combo
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Verbosity Label
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) }); // Verbosity Combo
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Stats

            // Search
            var searchLabel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            searchLabel.Children.Add(new TextBlock { Text = "🔍 Search:", FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(searchLabel, 0);
            filterGrid.Children.Add(searchLabel);

            _searchBox = new TextBox 
            { 
                Height = 24,
                FontSize = 11, 
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(5, 0, 5, 0),
                Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
                BorderThickness = new Thickness(1)
            };
            _searchBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            _searchBox.SetResourceReference(TextBox.BorderBrushProperty, "AccentBrush");
            _searchBox.TextChanged += (s, e) => UpdateView();
            Grid.SetColumn(_searchBox, 1);
            filterGrid.Children.Add(_searchBox);

            // Category
            var catLabel = new TextBlock { Text = "Category:", FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(15, 0, 8, 0) };
            Grid.SetColumn(catLabel, 2);
            filterGrid.Children.Add(catLabel);

            _categoryFilterCombo = new ComboBox { Height = 24, FontSize = 11, Padding = new Thickness(4, 0, 4, 0) };
            _categoryFilterCombo.Items.Add("ALL");
            _categoryFilterCombo.Items.Add("ERROR / FATAL");
            _categoryFilterCombo.Items.Add("SYSTEM");
            _categoryFilterCombo.Items.Add("BRIDGE");
            _categoryFilterCombo.Items.Add("AI");
            _categoryFilterCombo.SelectedIndex = 0;
            _categoryFilterCombo.SelectionChanged += (s, e) => UpdateView();
            Grid.SetColumn(_categoryFilterCombo, 3);
            filterGrid.Children.Add(_categoryFilterCombo);

            // Verbosity
            var verbLabel = new TextBlock { Text = "Verbosity:", FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(15, 0, 8, 0) };
            Grid.SetColumn(verbLabel, 4);
            filterGrid.Children.Add(verbLabel);

            _verbosityCombo = new ComboBox { Height = 24, FontSize = 11, Padding = new Thickness(4, 0, 4, 0) };
            _verbosityCombo.Items.Add("None (Silent)");
            _verbosityCombo.Items.Add("Minimal (Half)");
            _verbosityCombo.Items.Add("Full (Verbose)");
            _verbosityCombo.SelectedIndex = SettingsManager.Current.DEBUG_VERBOSITY_LEVEL;
            _verbosityCombo.SelectionChanged += (s, e) =>
            {
                SettingsManager.Current.DEBUG_VERBOSITY_LEVEL = _verbosityCombo.SelectedIndex;
                SettingsManager.Save();
                Log("System", $"Debug Verbosity set to: {_verbosityCombo.SelectedItem}");
            };
            Grid.SetColumn(_verbosityCombo, 5);
            filterGrid.Children.Add(_verbosityCombo);

            _statusLabel = new TextBlock 
            { 
                FontSize = 10,
                FontWeight = FontWeights.Medium, 
                VerticalAlignment = VerticalAlignment.Center, 
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10, 0, 0, 0)
            };
            _statusLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetColumn(_statusLabel, 6);
            filterGrid.Children.Add(_statusLabel);

            Grid.SetRow(filterGrid, 0);
            mainGrid.Children.Add(filterGrid);

            // --- 2. CONSOLE WINDOW (RichText) ---
            var consoleBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(230, 10, 8, 16)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6)
            };
            consoleBorder.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");

            _consoleBox = new RichTextBox
            {
                IsReadOnly = true,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Document = new FlowDocument()
            };
            _consoleBox.Document.PagePadding = new Thickness(0);

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
                TextRange range = new TextRange(_consoleBox.Document.ContentStart, _consoleBox.Document.ContentEnd);
                Clipboard.SetText(range.Text);
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
                    var matched = suggestions.OrderByDescending(r => r.SIMILARITY).First();
                    Log("Action", $"Executing matched command: '{matched.TITLE}'");
                    matched.EXECUTE?.Invoke();
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

            List<LogEntry> filtered;
            lock (_history)
            {
                filtered = _history.Where(entry =>
                {
                    // Filter by category
                    if (selectedCat != "ALL")
                    {
                        string upperCat = entry.Category.ToUpper();
                        if (selectedCat == "ERROR / FATAL")
                        {
                            if (!upperCat.Contains("ERROR") && !upperCat.Contains("FATAL") && !upperCat.Contains("FAIL"))
                                return false;
                        }
                        else
                        {
                            if (!upperCat.Contains(selectedCat.ToUpper()))
                                return false;
                        }
                    }

                    // Filter by search text
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        if (entry.Message.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0 &&
                            entry.Category.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                            return false;
                    }

                    return true;
                }).ToList();
            }

            _consoleBox.Document.Blocks.Clear();
            var paragraph = new Paragraph();

            foreach (var entry in filtered)
            {
                // Timestamp
                paragraph.Inlines.Add(new Run($"[{entry.Timestamp}] ") { Foreground = Brushes.DimGray });

                // Category
                paragraph.Inlines.Add(new Run($"[{entry.Category.ToUpper()}] ") { Foreground = entry.Color, FontWeight = FontWeights.Bold });

                // Message
                paragraph.Inlines.Add(new Run(entry.Message + Environment.NewLine) { Foreground = entry.Color });
            }

            _consoleBox.Document.Blocks.Add(paragraph);
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
