// Developer: heaplyn
// Date: 2026-08-09
// Summary: Live floating glassmorphic system resource monitor overlay displaying real-time CPU %, RAM usage, and thread stats.

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace JarvisLauncher
{
    public class SystemMonitorOverlay : BaseOverlay
    {
        private static SystemMonitorOverlay? _instance;

        private readonly DispatcherTimer _timer;
        private readonly PerformanceCounter _cpuCounter;
        private readonly TextBlock _cpuTextBlock;
        private readonly TextBlock _ramTextBlock;
        private readonly TextBlock _threadsTextBlock;
        private readonly ProgressBar _cpuProgressBar;
        private readonly ProgressBar _ramProgressBar;

        public static void ToggleMonitor()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_instance == null)
                {
                    _instance = new SystemMonitorOverlay();
                    _instance.Show();
                }
                else
                {
                    _instance.FadeOutAndClose();
                    _instance = null;
                }
            });
        }

        private SystemMonitorOverlay()
            : base("JARVIS LIVE SYSTEM MONITOR", width: 340, height: 180)
        {
            this.Closed += (s, e) => 
            { 
                _timer?.Stop();
                _cpuCounter?.Dispose();
                _instance = null; 
            };

            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue(); // First value is always 0
            }
            catch
            {
                _cpuCounter = new PerformanceCounter();
            }

            var grid = new Grid { Margin = new Thickness(6) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // CPU Row
            var cpuStack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            _cpuTextBlock = new TextBlock
            {
                Text = "⚡ CPU Usage: 0%",
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI Semibold")
            };
            _cpuTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            cpuStack.Children.Add(_cpuTextBlock);

            _cpuProgressBar = new ProgressBar
            {
                Height = 8,
                Maximum = 100,
                Margin = new Thickness(0, 4, 0, 0)
            };
            cpuStack.Children.Add(_cpuProgressBar);
            Grid.SetRow(cpuStack, 0);
            grid.Children.Add(cpuStack);

            // RAM Row
            var ramStack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            _ramTextBlock = new TextBlock
            {
                Text = "🧠 RAM Usage: 0 MB / 0 MB",
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI Semibold")
            };
            _ramTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            ramStack.Children.Add(_ramTextBlock);

            _ramProgressBar = new ProgressBar
            {
                Height = 8,
                Maximum = 100,
                Margin = new Thickness(0, 4, 0, 0)
            };
            ramStack.Children.Add(_ramProgressBar);
            Grid.SetRow(ramStack, 1);
            grid.Children.Add(ramStack);

            // System Threads / Process Info
            _threadsTextBlock = new TextBlock
            {
                Text = "⚙️ Running Processes: 0",
                FontSize = 11,
                FontFamily = new FontFamily("Segoe UI")
            };
            _threadsTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetRow(_threadsTextBlock, 2);
            grid.Children.Add(_threadsTextBlock);

            this.UserContent = grid;

            // Timer to update live stats every 1 second
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += UpdateStats;
            _timer.Start();

            UpdateStats(null, EventArgs.Empty);
        }

        private void UpdateStats(object? sender, EventArgs e)
        {
            try
            {
                // CPU %
                float cpuVal = 0;
                try { cpuVal = _cpuCounter.NextValue(); } catch { }
                _cpuTextBlock.Text = $"⚡ CPU Usage: {cpuVal:F1}%";
                _cpuProgressBar.Value = Math.Min(100, Math.Max(0, cpuVal));

                // Memory Info
                var memStatus = new NativeMethods.MEMORYSTATUSEX();
                if (NativeMethods.GlobalMemoryStatusEx(ref memStatus))
                {
                    double totalGB = memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                    double availGB = memStatus.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
                    double usedGB = totalGB - availGB;
                    double ramPct = memStatus.dwMemoryLoad;

                    _ramTextBlock.Text = $"🧠 RAM Usage: {usedGB:F1} GB / {totalGB:F1} GB ({ramPct}%)";
                    _ramProgressBar.Value = ramPct;
                }

                // Process Count
                int procCount = Process.GetProcesses().Length;
                _threadsTextBlock.Text = $"⚙️ Active System Processes: {procCount}";
            }
            catch { }
        }
    }
}
