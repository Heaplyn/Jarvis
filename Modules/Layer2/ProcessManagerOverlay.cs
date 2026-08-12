// Developer: heaplyn
// Date: 2026-08-12
// Summary: Advanced Glassmorphic Task Manager overlay with real-time process filtering, sorting, and termination.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace JarvisLauncher
{
    public class ProcessManagerOverlay : BaseOverlay
    {
        private static ProcessManagerOverlay? _instance;
        private readonly StackPanel _processListPanel;
        private readonly DispatcherTimer _timer;
        private readonly TextBox _searchBox;
        private string _searchFilter = string.Empty;

        public static void OpenManager()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_instance == null)
                {
                    _instance = new ProcessManagerOverlay();
                }

                _instance.RefreshProcessList();
                _instance.Show();

                if (_instance.WindowState == WindowState.Minimized)
                {
                    _instance.WindowState = WindowState.Normal;
                }

                _instance.Activate();
                _instance.Focus();
            });
        }

        private ProcessManagerOverlay()
            : base("JARVIS ADVANCED PROCESS MANAGER", width: 600, height: 500)
        {
            this.Closed += (s, e) =>
            {
                _timer?.Stop();
                _instance = null;
            };

            var rootGrid = new Grid { Margin = new Thickness(10) };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Toolbar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header bar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

            // 1. Toolbar (Search)
            var toolbarGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var searchLabel = new TextBlock { Text = "🔍 Filter: ", VerticalAlignment = VerticalAlignment.Center, FontSize = 12, FontWeight = FontWeights.Bold };
            searchLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetColumn(searchLabel, 0);
            toolbarGrid.Children.Add(searchLabel);

            _searchBox = new TextBox {
                Padding = new Thickness(5, 2, 5, 2),
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
                Foreground = Brushes.White,
                BorderBrush = Brushes.Gray
            };
            _searchBox.TextChanged += (s, e) => {
                _searchFilter = _searchBox.Text.ToLower().Trim();
                RefreshProcessList();
            };
            Grid.SetColumn(_searchBox, 1);
            toolbarGrid.Children.Add(_searchBox);

            var refreshBtn = new Button { Content = "🔄 Refresh", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(10, 2, 10, 2) };
            refreshBtn.Click += (s, e) => RefreshProcessList();
            Grid.SetColumn(refreshBtn, 2);
            toolbarGrid.Children.Add(refreshBtn);

            Grid.SetRow(toolbarGrid, 0);
            rootGrid.Children.Add(toolbarGrid);

            // 2. Header labels
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 6),
                CornerRadius = new CornerRadius(4)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            AddHeaderLabel(headerGrid, "Process Name", 0);
            AddHeaderLabel(headerGrid, "PID", 1);
            AddHeaderLabel(headerGrid, "Memory (Private)", 2);
            AddHeaderLabel(headerGrid, "Action", 3);

            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, 1);
            rootGrid.Children.Add(headerBorder);

            // 3. ScrollViewer for Process Cards
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            _processListPanel = new StackPanel();
            scrollViewer.Content = _processListPanel;
            Grid.SetRow(scrollViewer, 2);
            rootGrid.Children.Add(scrollViewer);

            // 4. Footer
            var footer = new TextBlock { FontSize = 10, Margin = new Thickness(0,5,0,0), Opacity = 0.6, HorizontalAlignment = HorizontalAlignment.Center };
            footer.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            footer.Text = "Auto-refreshes every 5 seconds. Sorted by Memory usage.";
            Grid.SetRow(footer, 3);
            rootGrid.Children.Add(footer);

            this.UserContent = rootGrid;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _timer.Tick += (s, e) => RefreshProcessList();
            _timer.Start();
        }

        public void RefreshProcessList()
        {
            if (!this.IsVisible) return;

            _processListPanel.Children.Clear();

            try
            {
                var processes = Process.GetProcesses()
                    .Where(p => {
                        try {
                            if (string.IsNullOrEmpty(p.ProcessName)) return false;
                            if (!string.IsNullOrEmpty(_searchFilter) && !p.ProcessName.ToLower().Contains(_searchFilter)) return false;
                            return true;
                        } catch { return false; }
                    })
                    .OrderByDescending(p => {
                        try { return p.PrivateMemorySize64; } catch { return 0; }
                    })
                    .Take(30)
                    .ToList();

                foreach (var proc in processes)
                {
                    _processListPanel.Children.Add(CreateProcessRow(proc));
                }
            }
            catch { }
        }

        private Border CreateProcessRow(Process proc)
        {
            var rowBorder = new Border
            {
                Margin = new Thickness(0, 0, 0, 4),
                Padding = new Thickness(10, 6, 10, 6),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255))
            };

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Name
            var nameBlock = new TextBlock
            {
                Text = proc.ProcessName,
                FontWeight = FontWeights.Medium,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            nameBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            Grid.SetColumn(nameBlock, 0);
            rowGrid.Children.Add(nameBlock);

            // PID
            var pidBlock = new TextBlock
            {
                Text = proc.Id.ToString(),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.8
            };
            pidBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetColumn(pidBlock, 1);
            rowGrid.Children.Add(pidBlock);

            // Memory
            long memBytes = 0;
            try { memBytes = proc.PrivateMemorySize64; } catch { }
            string memStr = (memBytes / 1024 / 1024.0).ToString("F1") + " MB";

            var memBlock = new TextBlock
            {
                Text = memStr,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            memBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            Grid.SetColumn(memBlock, 2);
            rowGrid.Children.Add(memBlock);

            // Action
            var killBtn = new Button
            {
                Content = "End Task",
                Padding = new Thickness(6, 1, 6, 1),
                FontSize = 10,
                Background = new SolidColorBrush(Color.FromArgb(40, 200, 0, 0)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            killBtn.Click += (s, e) =>
            {
                try
                {
                    proc.Kill();
                    DebugConsoleOverlay.Log("System", $"Killed process {proc.ProcessName} ({proc.Id})");
                    RefreshProcessList();
                }
                catch (Exception ex)
                {
                    TextOverlay.Show($"⚠️ Failed: {ex.Message}", 2000);
                }
            };
            Grid.SetColumn(killBtn, 3);
            rowGrid.Children.Add(killBtn);

            rowBorder.Child = rowGrid;
            return rowBorder;
        }

        private void AddHeaderLabel(Grid grid, string text, int col)
        {
            var label = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetColumn(label, col);
            grid.Children.Add(label);
        }
    }
}
