// Developer: heaplyn
// Date: 2026-08-09
// Summary: Interactive Glassmorphic Task Manager overlay displaying top CPU/RAM consuming system processes with one-click termination.

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
            : base("JARVIS PROCESS MANAGER", width: 560, height: 440)
        {
            this.Closed += (s, e) =>
            {
                _timer?.Stop();
                _instance = null;
            };

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header bar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List

            // Header labels container
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 6),
                CornerRadius = new CornerRadius(4)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            AddHeaderLabel(headerGrid, "Process Name", 0);
            AddHeaderLabel(headerGrid, "PID", 1);
            AddHeaderLabel(headerGrid, "Memory Usage", 2);
            AddHeaderLabel(headerGrid, "Action", 3);

            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            // ScrollViewer for Process Cards
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            _processListPanel = new StackPanel();
            scrollViewer.Content = _processListPanel;
            Grid.SetRow(scrollViewer, 1);
            rootGrid.Children.Add(scrollViewer);

            this.UserContent = rootGrid;

            // Timer to auto refresh process list every 3 seconds
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _timer.Tick += (s, e) => RefreshProcessList();
            _timer.Start();
        }

        public void RefreshProcessList()
        {
            _processListPanel.Children.Clear();

            try
            {
                var processes = Process.GetProcesses()
                    .Where(p => {
                        try { return !string.IsNullOrEmpty(p.ProcessName); } catch { return false; }
                    })
                    .OrderByDescending(p => {
                        try { return p.WorkingSet64; } catch { return 0; }
                    })
                    .Take(15)
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
                Margin = new Thickness(0, 2, 0, 4),
                Padding = new Thickness(10, 8, 10, 8),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(10, 255, 255, 255))
            };

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Name
            var nameBlock = new TextBlock
            {
                Text = proc.ProcessName,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            nameBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            Grid.SetColumn(nameBlock, 0);
            rowGrid.Children.Add(nameBlock);

            // PID
            var pidBlock = new TextBlock
            {
                Text = proc.Id.ToString(),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            pidBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetColumn(pidBlock, 1);
            rowGrid.Children.Add(pidBlock);

            // RAM Memory
            long bytes = 0;
            try { bytes = proc.WorkingSet64; } catch { }
            double mb = bytes / (1024.0 * 1024.0);

            var memBlock = new TextBlock
            {
                Text = $"{mb:F1} MB",
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            memBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            Grid.SetColumn(memBlock, 2);
            rowGrid.Children.Add(memBlock);

            // Kill Button
            var killBtn = new Button
            {
                Content = "💀 End",
                Padding = new Thickness(8, 2, 8, 2),
                Cursor = Cursors.Hand,
                FontSize = 10,
                FontFamily = new FontFamily("Segoe UI")
            };
            killBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            killBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            killBtn.Click += (s, e) =>
            {
                try
                {
                    proc.Kill();
                    TextOverlay.Show($"💀 Terminated {proc.ProcessName} (PID: {proc.Id})", 2500);
                    RefreshProcessList();
                }
                catch (Exception ex)
                {
                    TextOverlay.Show($"⚠️ Kill failed: {ex.Message}", 3000);
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
