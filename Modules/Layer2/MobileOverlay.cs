// Developer: heaplyn
// Date: 2026-08-12
// Summary: Mobile Companion Control Hub overlay with advanced diagnostics and self-repair tools.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JarvisLauncher
{
    public class MobileOverlay : BaseOverlay
    {
        private static MobileOverlay? _instance;

        private TextBlock _statusText;
        private TextBlock _localIpText;
        private TextBlock _publicUrlText;
        private TextBox _logView;
        private Button _startTunnelBtn;

        public MobileOverlay()
            : base("MOBILE COMPANION HUB", width: 460, height: 580)
        {
            var contentGrid = new Grid { Margin = new Thickness(12) };
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Controls
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Logs

            // --- Status Card ---
            var statusBorder = new Border { Padding = new Thickness(12), Margin = new Thickness(0,0,0,10), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1) };
            statusBorder.SetResourceReference(Border.BackgroundProperty, "WindowBackgroundBrush");
            statusBorder.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");

            var statusStack = new StackPanel();
            _statusText = new TextBlock { Text = MobileBridgeServer.IsActive ? "🟢 Server Active" : "🔴 Server Offline", FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,8) };
            _statusText.Foreground = MobileBridgeServer.IsActive ? Brushes.LimeGreen : Brushes.Red;
            statusStack.Children.Add(_statusText);

            _localIpText = new TextBlock { Text = $"Tailscale/Wi-Fi: {MobileBridgeServer.ServerUrl}", FontSize = 12 };
            _localIpText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            statusStack.Children.Add(_localIpText);

            statusBorder.Child = statusStack;
            Grid.SetRow(statusBorder, 0);
            contentGrid.Children.Add(statusBorder);

            // --- Controls ---
            var controlStack = new StackPanel();

            var repairBtn = new Button { Content = "🛡️ Fix Firewall & Permissions", Padding = new Thickness(10), Margin = new Thickness(0,0,0,10), Background = new SolidColorBrush(Color.FromRgb(40, 60, 100)), Foreground = Brushes.White };
            repairBtn.Click += async (s, e) => {
                repairBtn.Content = "Repairing...";
                await MobileBridgeServer.FixFirewallPermissionsAsync();
                repairBtn.Content = "🛡️ Fix Firewall & Permissions";
                UpdateStatus();
            };
            controlStack.Children.Add(repairBtn);

            var restartBtn = new Button { Content = "🔄 Restart Connectivity Engine", Padding = new Thickness(10), Margin = new Thickness(0,0,0,10) };
            restartBtn.Click += (s, e) => {
                MobileBridgeServer.Stop();
                MobileBridgeServer.Start(9000);
                UpdateStatus();
            };
            controlStack.Children.Add(restartBtn);

            var debugBtn = new Button { Content = "🛠️ Open Debug Console", Padding = new Thickness(10), Margin = new Thickness(0,0,0,10) };
            debugBtn.Click += (s, e) => {
                DebugConsoleOverlay.ShowConsole();
            };
            controlStack.Children.Add(debugBtn);

            _startTunnelBtn = new Button { Content = CloudflareTunnelManager.IsRunning ? "🌐 Stop Public Tunnel" : "🌐 Start Public Tunnel", Padding = new Thickness(10), Margin = new Thickness(0,0,0,10) };
            _startTunnelBtn.Click += async (s, e) => {
                if (CloudflareTunnelManager.IsRunning) CloudflareTunnelManager.StopTunnel();
                else await CloudflareTunnelManager.StartTunnelAsync(9000);
                UpdateStatus();
            };
            controlStack.Children.Add(_startTunnelBtn);

            _publicUrlText = new TextBlock { Text = CloudflareTunnelManager.PublicUrl ?? "No public tunnel active.", TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, FontSize = 11, Margin = new Thickness(0,0,0,10) };
            _publicUrlText.SetResourceReference(TextBlock.ForegroundProperty, "AccentCaretBrush");
            controlStack.Children.Add(_publicUrlText);

            Grid.SetRow(controlStack, 1);
            contentGrid.Children.Add(controlStack);

            // --- Log Viewer ---
            var logBorder = new Border { CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1), Margin = new Thickness(0,5,0,0) };
            logBorder.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");

            _logView = new TextBox {
                IsReadOnly = true,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Background = Brushes.Black,
                Foreground = Brushes.LightGray,
                Padding = new Thickness(5),
                Text = "Initializing diagnostics..."
            };
            logBorder.Child = _logView;
            Grid.SetRow(logBorder, 2);
            contentGrid.Children.Add(logBorder);

            this.UserContent = contentGrid;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            _statusText.Text = MobileBridgeServer.IsActive ? "🟢 Server Active" : "🔴 Server Offline";
            _statusText.Foreground = MobileBridgeServer.IsActive ? Brushes.LimeGreen : Brushes.Red;
            _localIpText.Text = $"Tailscale/Wi-Fi: {MobileBridgeServer.ServerUrl}";
            _publicUrlText.Text = CloudflareTunnelManager.PublicUrl ?? "No public tunnel active.";
            _startTunnelBtn.Content = CloudflareTunnelManager.IsRunning ? "🌐 Stop Public Tunnel" : "🌐 Start Public Tunnel";
            _logView.Text = MobileBridgeServer.GetRecentLogs(20);
            _logView.ScrollToEnd();
        }

        public static void ShowOverlay()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_instance == null || !_instance.IsLoaded) _instance = new MobileOverlay();
                _instance.Show();
                _instance.Activate();
                _instance.UpdateStatus();
            });
        }

        public static void PromptPermanentToken() => TextOverlay.Show("Manual Token Set via Settings.", 2000);
        public static void PromptNgrokToken() => TextOverlay.Show("Manual Token Set via Settings.", 2000);

        public static void ShowQrPairingWindow(string? url = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                string target = url ?? CloudflareTunnelManager.PublicUrl ?? MobileBridgeServer.ServerUrl;
                string qrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=250x250&data={Uri.EscapeDataString(target)}";

                var win = new Window
                {
                    Title = "Connect Phone",
                    Width = 320,
                    Height = 380,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Background = Brushes.Black,
                    Topmost = true,
                    ResizeMode = ResizeMode.NoResize
                };

                var sp = new StackPanel { Margin = new Thickness(20) };
                var img = new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(qrUrl)), Width = 250, Height = 250 };
                sp.Children.Add(img);
                sp.Children.Add(new TextBlock { Text = target, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,10,0,0), TextWrapping = TextWrapping.Wrap });

                win.Content = sp;
                win.Show();
            });
        }
    }
}
