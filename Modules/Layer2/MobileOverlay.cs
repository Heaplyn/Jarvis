// Developer: heaplyn
// Date: 2026-08-10
// Summary: Draggable, glassmorphic Mobile Companion Control Hub overlay GUI providing connection management, Cloudflare tunnel controls, permission toggles, and direct phone link preview.

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

        private TextBlock _localIpText;
        private TextBlock _dnsText;
        private TextBlock _publicUrlText;
        private TextBlock _ngrokUrlText;
        private Button _startTunnelBtn;
        private Button _startNgrokBtn;

        // Capability Settings
        public static bool AllowAppLaunching { get; set; } = true;
        public static bool AllowVolumeControl { get; set; } = true;
        public static bool AllowAiChat { get; set; } = true;
        public static bool AllowTelemetry { get; set; } = true;
        public static bool AllowFileUploads { get; set; } = true;
        public static bool AllowTerminal { get; set; } = true;
        public static bool AllowScreenMirroring { get; set; } = true;
        public static bool AllowClipboardSync { get; set; } = true;

        public MobileOverlay()
            : base("MOBILE COMPANION HUB", width: 440, height: 530)
        {
            var workArea = SystemParameters.WorkArea;
            this.Left = workArea.Width - this.Width - 30;
            this.Top = workArea.Top + 40;

            var contentGrid = new Grid();
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Links Header
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Permissions Checklist
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Tunnel & Actions

            // 1. Connection Links Card (Row 0)
            var linksBorder = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            linksBorder.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");
            linksBorder.SetResourceReference(Border.BackgroundProperty, "WindowBackgroundBrush");

            var linksStack = new StackPanel();

            var titleBlock = new TextBlock
            {
                Text = "📡 Phone Connection Links",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "AccentCaretBrush");
            linksStack.Children.Add(titleBlock);

            _dnsText = AddLinkRow(linksStack, "🌐 Local DNS Domain:", MobileBridgeServer.JarvisDomain);
            _localIpText = AddLinkRow(linksStack, "📱 Local Wi-Fi IP:", MobileBridgeServer.ServerUrl);
            _publicUrlText = AddLinkRow(linksStack, "🔒 Cloudflare Public:", CloudflareTunnelManager.PublicUrl ?? "(Tunnel Inactive)");
            _ngrokUrlText = AddLinkRow(linksStack, "🔓 Ngrok Public:", NgrokTunnelManager.PublicUrl ?? "(Tunnel Inactive)");

            linksBorder.Child = linksStack;
            Grid.SetRow(linksBorder, 0);
            contentGrid.Children.Add(linksBorder);

            // 2. Permissions Checklist (Row 1)
            var permBorder = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            permBorder.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");
            permBorder.SetResourceReference(Border.BackgroundProperty, "WindowBackgroundBrush");

            var permStack = new StackPanel();
            var permTitle = new TextBlock
            {
                Text = "⚙️ Remote Phone Capabilities & Security",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            permTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            permStack.Children.Add(permTitle);

            AddCheckbox(permStack, "Allow AI Companion Chat & Voice Commands", AllowAiChat, (val) => AllowAiChat = val);
            AddCheckbox(permStack, "Allow Remote PC Volume & Audio Control", AllowVolumeControl, (val) => AllowVolumeControl = val);
            AddCheckbox(permStack, "Allow Remote App Launching (Roblox, VS Code)", AllowAppLaunching, (val) => AllowAppLaunching = val);
            AddCheckbox(permStack, "Allow Remote PowerShell CLI Terminal", AllowTerminal, (val) => AllowTerminal = val);
            AddCheckbox(permStack, "Allow Remote PC Desktop Screen Mirroring", AllowScreenMirroring, (val) => AllowScreenMirroring = val);
            AddCheckbox(permStack, "Allow Remote Clipboard Sync (Read/Write)", AllowClipboardSync, (val) => AllowClipboardSync = val);
            AddCheckbox(permStack, "Allow Remote System Telemetry (CPU/RAM)", AllowTelemetry, (val) => AllowTelemetry = val);
            AddCheckbox(permStack, "Allow Remote File Uploads & Execution", AllowFileUploads, (val) => AllowFileUploads = val);

            permBorder.Child = permStack;
            Grid.SetRow(permBorder, 1);
            contentGrid.Children.Add(permBorder);

            // 3. Action Buttons (Row 2)
            var actionStack = new StackPanel();

            _startTunnelBtn = new Button
            {
                Content = CloudflareTunnelManager.IsRunning ? "🌐 Public Cloudflare Tunnel Active" : "🌐 Launch Public Cloudflare Tunnel",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };
            _startTunnelBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            _startTunnelBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            _startTunnelBtn.Click += async (s, e) => await ToggleCloudflareTunnelAsync();
            actionStack.Children.Add(_startTunnelBtn);

            var tokenBtn = new Button
            {
                Content = "🔑 Optional: Set Permanent Cloudflare Token",
                FontSize = 11,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };
            tokenBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            tokenBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            tokenBtn.Click += (s, e) => PromptPermanentToken();
            actionStack.Children.Add(tokenBtn);

            var ngrokTokenBtn = new Button
            {
                Content = "🔑 Optional: Set Permanent ngrok Token",
                FontSize = 11,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };
            ngrokTokenBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            ngrokTokenBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            ngrokTokenBtn.Click += (s, e) => PromptNgrokToken();
            actionStack.Children.Add(ngrokTokenBtn);

            _startNgrokBtn = new Button
            {
                Content = NgrokTunnelManager.IsRunning ? "🌐 Public ngrok Tunnel Active" : "🌐 Launch Public ngrok Tunnel",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };
            _startNgrokBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            _startNgrokBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            _startNgrokBtn.Click += async (s, e) => await ToggleNgrokTunnelAsync();
            actionStack.Children.Add(_startNgrokBtn);

            var qrBtn = new Button
            {
                Content = "📷 Scan QR Code to Connect Phone Instantly",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };
            qrBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            qrBtn.SetResourceReference(Button.ForegroundProperty, "AccentCaretBrush");
            qrBtn.Click += (s, e) => ShowQrPairingWindow();
            actionStack.Children.Add(qrBtn);

            var previewBtn = new Button
            {
                Content = "🚀 Open Mobile App Preview in Browser",
                FontSize = 11,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 4),
                Cursor = Cursors.Hand
            };
            previewBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            previewBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            previewBtn.Click += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = MobileBridgeServer.ServerUrl,
                        UseShellExecute = true
                    });
                }
                catch { }
            };
            actionStack.Children.Add(previewBtn);

            var manageBtn = new Button
            {
                Content = "🔧 Manage Tunnels",
                FontSize = 11,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand
            };
            manageBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            manageBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            manageBtn.Click += (s, e) => TunnelOverlay.ShowOverlay();
            actionStack.Children.Add(manageBtn);

            Grid.SetRow(actionStack, 2);
            contentGrid.Children.Add(actionStack);

            this.UserContent = contentGrid;
        }

        private TextBlock AddLinkRow(StackPanel parent, string label, string initialValue)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lbl = new TextBlock { Text = label, FontSize = 11, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
            lbl.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            var valBlock = new TextBlock { Text = initialValue, FontSize = 11, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            valBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            Grid.SetColumn(valBlock, 1);
            grid.Children.Add(valBlock);

            var copyBtn = new Button
            {
                Content = "📋",
                ToolTip = "Copy link to clipboard",
                FontSize = 10,
                Padding = new Thickness(4, 1, 4, 1),
                Margin = new Thickness(4, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            copyBtn.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(valBlock.Text) && !valBlock.Text.StartsWith("("))
                {
                    Clipboard.SetText(valBlock.Text);
                    TextOverlay.Show($"📋 Copied: {valBlock.Text}", 1500);
                }
            };
            Grid.SetColumn(copyBtn, 2);
            grid.Children.Add(copyBtn);

            parent.Children.Add(grid);
            return valBlock;
        }

        private void AddCheckbox(StackPanel parent, string title, bool initialVal, Action<bool> onChange)
        {
            var cb = new CheckBox
            {
                Content = title,
                IsChecked = initialVal,
                FontSize = 11,
                Margin = new Thickness(0, 3, 0, 3),
                Cursor = Cursors.Hand
            };
            cb.SetResourceReference(CheckBox.ForegroundProperty, "TextPrimaryBrush");
            cb.Checked += (s, e) => onChange(true);
            cb.Unchecked += (s, e) => onChange(false);
            parent.Children.Add(cb);
        }

        private async Task ToggleCloudflareTunnelAsync()
        {
            if (CloudflareTunnelManager.IsRunning)
            {
                CloudflareTunnelManager.StopTunnel();
                _publicUrlText.Text = "(Tunnel Inactive)";
                _startTunnelBtn.Content = "🌐 Launch Public Cloudflare Tunnel";
                TextOverlay.Show("🌐 Cloudflare Tunnel Stopped", 1500);
            }
            else
            {
                _startTunnelBtn.Content = "⏳ Connecting Cloudflare Tunnel...";
                try
                {
                    string url = await CloudflareTunnelManager.StartTunnelAsync(8085);
                    _publicUrlText.Text = url;
                    _startTunnelBtn.Content = "🌐 Public Cloudflare Tunnel Active";
                    TextOverlay.Show($"🌐 Tunnel Live:\n{url}", 4000);
                }
                catch (Exception ex)
                {
                    _startTunnelBtn.Content = "🌐 Launch Public Cloudflare Tunnel";
                    TextOverlay.Show($"⚠️ Tunnel Error: {ex.Message}", 3000);
                }
            }
        }

        public static void PromptPermanentToken()
        {
            string tokenFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Tools", "cloudflare_token.txt");
            string existingToken = File.Exists(tokenFile) ? File.ReadAllText(tokenFile).Trim() : "";

            var win = new Window
            {
                Title = "🔑 Permanent Cloudflare Tunnel Token (Optional)",
                Width = 460,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = Brushes.White
            };

            var stack = new StackPanel { Margin = new Thickness(16) };
            var lbl = new TextBlock
            {
                Text = "Optional: Paste your Cloudflare Tunnel token (eyJ...) to bind your permanent domain on every restart:",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
            stack.Children.Add(lbl);

            var txt = new TextBox
            {
                Text = existingToken,
                FontSize = 12,
                Padding = new Thickness(8),
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                Margin = new Thickness(0, 0, 0, 14)
            };
            stack.Children.Add(txt);

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var saveBtn = new Button { Content = "💾 Save Token", Width = 100, Height = 32, Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand };
            saveBtn.Click += (s, e) =>
            {
                CloudflareTunnelManager.SaveTunnelToken(txt.Text.Trim());
                TextOverlay.Show("🔑 Permanent Cloudflare Token Saved!\nRestart tunnel to bind.", 4000);
                win.Close();
            };
            var cancelBtn = new Button { Content = "Cancel", Width = 80, Height = 32, Cursor = Cursors.Hand };
            cancelBtn.Click += (s, e) => win.Close();

            btnStack.Children.Add(saveBtn);
            btnStack.Children.Add(cancelBtn);
            stack.Children.Add(btnStack);

            win.Content = stack;
            win.ShowDialog();
        }

        private async Task ToggleNgrokTunnelAsync()
        {
            if (NgrokTunnelManager.IsRunning)
            {
                NgrokTunnelManager.StopTunnel();
                _ngrokUrlText.Text = "(Tunnel Inactive)";
                _startNgrokBtn.Content = "🌐 Launch Public ngrok Tunnel";
                TextOverlay.Show("🌐 ngrok Tunnel Stopped", 1500);
            }
            else
            {
                _startNgrokBtn.Content = "⏳ Connecting ngrok Tunnel...";
                try
                {
                    string url = await NgrokTunnelManager.StartTunnelAsync(8085);
                    _ngrokUrlText.Text = url;
                    _startNgrokBtn.Content = "🌐 Public ngrok Tunnel Active";
                    TextOverlay.Show($"🌐 Tunnel Live:\n{url}", 4000);
                }
                catch (Exception ex)
                {
                    _startNgrokBtn.Content = "🌐 Launch Public ngrok Tunnel";
                    TextOverlay.Show($"⚠️ ngrok Error: {ex.Message}", 3000);
                }
            }
        }

        public static void PromptNgrokToken()
        {
            string tokenFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Tools", "ngrok_token.txt");
            string existingToken = File.Exists(tokenFile) ? File.ReadAllText(tokenFile).Trim() : "";

            var win = new Window
            {
                Title = "🔑 Permanent ngrok Token (Optional)",
                Width = 460,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = Brushes.White
            };

            var stack = new StackPanel { Margin = new Thickness(16) };
            var lbl = new TextBlock
            {
                Text = "Optional: Paste your ngrok authtoken to enable higher-rate/public tunnels:",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
            stack.Children.Add(lbl);

            var txt = new TextBox
            {
                Text = existingToken,
                FontSize = 12,
                Padding = new Thickness(8),
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                Margin = new Thickness(0, 0, 0, 14)
            };
            stack.Children.Add(txt);

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var saveBtn = new Button { Content = "💾 Save Token", Width = 100, Height = 32, Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand };
            saveBtn.Click += (s, e) =>
            {
                NgrokTunnelManager.SaveAuthToken(txt.Text.Trim());
                TextOverlay.Show("🔑 ngrok Token Saved!\nRestart tunnel to apply.", 4000);
                win.Close();
            };
            var cancelBtn = new Button { Content = "Cancel", Width = 80, Height = 32, Cursor = Cursors.Hand };
            cancelBtn.Click += (s, e) => win.Close();

            btnStack.Children.Add(saveBtn);
            btnStack.Children.Add(cancelBtn);
            stack.Children.Add(btnStack);

            win.Content = stack;
            win.ShowDialog();
        }

        public static void ShowQrPairingWindow(string? targetUrl = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                string url = targetUrl ?? CloudflareTunnelManager.PublicUrl ?? MobileBridgeServer.JarvisDomain;
                string qrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=260x260&data={Uri.EscapeDataString(url)}";

                var win = new Window
                {
                    Title = "📷 Scan QR Code to Pair Phone Instantly",
                    Width = 360,
                    Height = 440,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize,
                    Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                    Foreground = Brushes.White
                };

                var stack = new StackPanel { Margin = new Thickness(18), HorizontalAlignment = HorizontalAlignment.Center };
                var title = new TextBlock
                {
                    Text = "📱 Connect Phone to PC",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 6),
                    Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248))
                };
                stack.Children.Add(title);

                var subtitle = new TextBlock
                {
                    Text = "Point your phone camera at this QR code to connect instantly!",
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 14),
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
                };
                stack.Children.Add(subtitle);

                var imgBorder = new Border
                {
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 14),
                    Width = 240,
                    Height = 240
                };
                var img = new Image();
                try
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage(new Uri(qrUrl));
                    img.Source = bmp;
                }
                catch { }
                imgBorder.Child = img;
                stack.Children.Add(imgBorder);

                var urlBlock = new TextBlock
                {
                    Text = url,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(192, 132, 252))
                };
                stack.Children.Add(urlBlock);

                win.Content = stack;
                win.Show();
            });
        }

        public static void ShowOverlay()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_instance == null || !_instance.IsLoaded)
                {
                    _instance = new MobileOverlay();
                    _instance.Show();
                }
                else
                {
                    _instance.Activate();
                }
            });
        }
    }
}
