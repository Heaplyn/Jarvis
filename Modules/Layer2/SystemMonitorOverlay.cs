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
using System.Net.NetworkInformation;

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

        // Expanded resources
        private readonly TextBlock _diskTextBlock;
        private readonly ProgressBar _diskProgressBar;
        private readonly TextBlock _netTextBlock;
        private readonly TextBlock _uptimeTextBlock;

        private long _lastBytesReceived = 0;
        private long _lastBytesSent = 0;
        private DateTime _lastNetworkTime = DateTime.MinValue;

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
            : base("JARVIS LIVE SYSTEM MONITOR", width: 340, height: 320)
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

            var grid = new Grid { Margin = new Thickness(8) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // CPU
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // RAM
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Disk
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Network
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Processes & Uptime

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

            // Disk Row
            var diskStack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            _diskTextBlock = new TextBlock
            {
                Text = "💾 Disk C: Space: Freeing details...",
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI Semibold")
            };
            _diskTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            diskStack.Children.Add(_diskTextBlock);

            _diskProgressBar = new ProgressBar
            {
                Height = 8,
                Maximum = 100,
                Margin = new Thickness(0, 4, 0, 0)
            };
            diskStack.Children.Add(_diskProgressBar);
            Grid.SetRow(diskStack, 2);
            grid.Children.Add(diskStack);

            // Network Row
            var netStack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            _netTextBlock = new TextBlock
            {
                Text = "🌐 Net: Down 0.0 KB/s | Up 0.0 KB/s",
                FontSize = 11,
                FontFamily = new FontFamily("Segoe UI Semibold")
            };
            _netTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            netStack.Children.Add(_netTextBlock);
            Grid.SetRow(netStack, 3);
            grid.Children.Add(netStack);

            // Processes & Uptime Row
            var infoStack = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
            _threadsTextBlock = new TextBlock
            {
                Text = "⚙️ Running Processes: 0",
                FontSize = 11,
                FontFamily = new FontFamily("Segoe UI"),
                Margin = new Thickness(0, 0, 0, 2)
            };
            _threadsTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            infoStack.Children.Add(_threadsTextBlock);

            _uptimeTextBlock = new TextBlock
            {
                Text = "🕒 System Uptime: 0h 0m 0s",
                FontSize = 11,
                FontFamily = new FontFamily("Segoe UI")
            };
            _uptimeTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            infoStack.Children.Add(_uptimeTextBlock);

            Grid.SetRow(infoStack, 4);
            grid.Children.Add(infoStack);

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
                // 1. CPU %
                float cpuVal = 0;
                try { cpuVal = _cpuCounter.NextValue(); } catch { }
                _cpuTextBlock.Text = $"⚡ CPU Usage: {cpuVal:F1}%";
                _cpuProgressBar.Value = Math.Min(100, Math.Max(0, cpuVal));

                // 2. Memory Info
                var memStatus = new NativeMethods.MEMORYSTATUSEX();
                memStatus.dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MEMORYSTATUSEX));
                if (NativeMethods.GlobalMemoryStatusEx(ref memStatus))
                {
                    double totalGB = memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                    double availGB = memStatus.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
                    double usedGB = totalGB - availGB;
                    double ramPct = memStatus.dwMemoryLoad;

                    _ramTextBlock.Text = $"🧠 RAM Usage: {usedGB:F1} GB / {totalGB:F1} GB ({ramPct}%)";
                    _ramProgressBar.Value = ramPct;
                }

                // 3. Disk Info (Drive C:)
                try
                {
                    var driveC = new DriveInfo("C");
                    if (driveC.IsReady)
                    {
                        double totalGB = driveC.TotalSize / (1024.0 * 1024.0 * 1024.0);
                        double freeGB = driveC.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                        double usedGB = totalGB - freeGB;
                        double usePct = (usedGB / totalGB) * 100.0;

                        _diskTextBlock.Text = $"💾 Disk C: {usedGB:F1} GB / {totalGB:F1} GB ({usePct:F0}% Used)";
                        _diskProgressBar.Value = usePct;
                    }
                }
                catch { }

                // 4. Real-time Network Speeds
                try
                {
                    long currentRecv = 0;
                    long currentSent = 0;
                    var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                    foreach (var ni in interfaces)
                    {
                        if (ni.OperationalStatus == OperationalStatus.Up && 
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                        {
                            if (ni.Supports(NetworkInterfaceComponent.IPv4))
                            {
                                var ipv4Stats = ni.GetIPv4Statistics();
                                currentRecv += ipv4Stats.BytesReceived;
                                currentSent += ipv4Stats.BytesSent;
                            }
                        }
                    }

                    DateTime now = DateTime.Now;
                    if (_lastNetworkTime != DateTime.MinValue)
                    {
                        double seconds = (now - _lastNetworkTime).TotalSeconds;
                        if (seconds > 0)
                        {
                            double downloadSpeed = (currentRecv - _lastBytesReceived) / seconds; // Bytes/sec
                            double uploadSpeed = (currentSent - _lastBytesSent) / seconds;

                            // Convert to appropriate unit (KB/s or MB/s)
                            string downStr = FormatSpeed(downloadSpeed);
                            string upStr = FormatSpeed(uploadSpeed);

                            _netTextBlock.Text = $"🌐 Network: ⬇️ {downStr} | ⬆️ {upStr}";
                        }
                    }

                    _lastBytesReceived = currentRecv;
                    _lastBytesSent = currentSent;
                    _lastNetworkTime = now;
                }
                catch { }

                // 5. Process Count
                int procCount = Process.GetProcesses().Length;
                _threadsTextBlock.Text = $"⚙️ Active System Processes: {procCount}";

                // 6. System Uptime
                try
                {
                    long uptimeMs = Environment.TickCount64;
                    var uptime = TimeSpan.FromMilliseconds(uptimeMs);
                    _uptimeTextBlock.Text = $"🕒 System Uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
                }
                catch { }
            }
            catch { }
        }

        private string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond >= 1024 * 1024)
            {
                return $"{(bytesPerSecond / (1024.0 * 1024.0)):F1} MB/s";
            }
            else
            {
                return $"{(bytesPerSecond / 1024.0):F1} KB/s";
            }
        }
    }
}
