// Developer: copilot
// Date: 2026-08-12
// Summary: Small overlay to manage public tunnels (Cloudflare / ngrok)

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JarvisLauncher
{
    public class TunnelOverlay : BaseOverlay
    {
        private static TunnelOverlay? _instance;

        private TextBlock _cfUrl;
        private TextBlock _ngrokUrl;

        public TunnelOverlay() : base("TUNNELS", width: 420, height: 260)
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new TextBlock
            {
                Text = "Manage Public Tunnels",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(12)
            };
            header.SetResourceReference(TextBlock.ForegroundProperty, "AccentCaretBrush");
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            var stack = new StackPanel { Margin = new Thickness(12) };

            // Cloudflare row
            var cfGrid = new Grid();
            cfGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cfGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _cfUrl = new TextBlock { Text = CloudflareTunnelManager.PublicUrl ?? "(Inactive)", FontWeight = FontWeights.Medium };
            _cfUrl.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            cfGrid.Children.Add(_cfUrl);
            var cfBtn = new Button { Content = CloudflareTunnelManager.IsRunning ? "Stop" : "Start", Margin = new Thickness(8,0,0,0) };
            cfBtn.Click += async (s, e) =>
            {
                if (CloudflareTunnelManager.IsRunning)
                {
                    CloudflareTunnelManager.StopTunnel();
                    _cfUrl.Text = "(Inactive)";
                    cfBtn.Content = "Start";
                    TextOverlay.Show("Cloudflare tunnel stopped", 1500);
                }
                else
                {
                    cfBtn.Content = "Starting...";
                    try
                    {
                        string url = await CloudflareTunnelManager.StartTunnelAsync(9000);
                        _cfUrl.Text = url;
                        cfBtn.Content = "Stop";
                        TextOverlay.Show($"Cloudflare live: {url}", 3000);
                    }
                    catch (Exception ex)
                    {
                        cfBtn.Content = "Start";
                        TextOverlay.Show($"Cloudflare error: {ex.Message}", 3000);
                    }
                }
            };
            Grid.SetColumn(cfBtn, 1);
            cfGrid.Children.Add(cfBtn);
            stack.Children.Add(new TextBlock { Text = "Cloudflare", FontWeight = FontWeights.SemiBold });
            stack.Children.Add(cfGrid);

            // ngrok row
            var ngGrid = new Grid { Margin = new Thickness(0,10,0,0) };
            ngGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ngGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _ngrokUrl = new TextBlock { Text = NgrokTunnelManager.PublicUrl ?? "(Inactive)", FontWeight = FontWeights.Medium };
            _ngrokUrl.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            ngGrid.Children.Add(_ngrokUrl);
            var ngBtn = new Button { Content = NgrokTunnelManager.IsRunning ? "Stop" : "Start", Margin = new Thickness(8,0,0,0) };
            ngBtn.Click += async (s, e) =>
            {
                if (NgrokTunnelManager.IsRunning)
                {
                    NgrokTunnelManager.StopTunnel();
                    _ngrokUrl.Text = "(Inactive)";
                    ngBtn.Content = "Start";
                    TextOverlay.Show("ngrok tunnel stopped", 1500);
                }
                else
                {
                    ngBtn.Content = "Starting...";
                    try
                    {
                        string url = await NgrokTunnelManager.StartTunnelAsync(9000);
                        _ngrokUrl.Text = url;
                        ngBtn.Content = "Stop";
                        TextOverlay.Show($"ngrok live: {url}", 3000);
                    }
                    catch (Exception ex)
                    {
                        ngBtn.Content = "Start";
                        TextOverlay.Show($"ngrok error: {ex.Message}", 3000);
                    }
                }
            };
            Grid.SetColumn(ngBtn, 1);
            ngGrid.Children.Add(ngBtn);
            stack.Children.Add(new TextBlock { Text = "ngrok", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,8,0,0) });
            stack.Children.Add(ngGrid);

            var updateRow = new Grid { Margin = new Thickness(0,10,0,0) };
            updateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            updateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var updateLabel = new TextBlock { Text = "ngrok update", FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
            updateLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            updateRow.Children.Add(updateLabel);
            var updateBtn = new Button { Content = "Update ngrok", Margin = new Thickness(8,0,0,0) };
            updateBtn.Click += async (s, e) =>
            {
                updateBtn.Content = "Updating...";
                updateBtn.IsEnabled = false;
                try
                {
                    await NgrokTunnelManager.UpdateNgrokAsync();
                    updateBtn.Content = "Updated";
                    TextOverlay.Show("ngrok updated. Restart the tunnel to use new binary.", 4000);
                }
                catch (Exception ex)
                {
                    updateBtn.Content = "Update ngrok";
                    TextOverlay.Show($"ngrok update failed: {ex.Message}", 4000);
                }
                finally
                {
                    updateBtn.IsEnabled = true;
                    updateBtn.Content = "Update ngrok";
                }
            };
            Grid.SetColumn(updateBtn, 1);
            updateRow.Children.Add(updateBtn);
            stack.Children.Add(updateRow);

            // Token shortcuts
            var tokenStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,12,0,0), HorizontalAlignment = HorizontalAlignment.Right };
            var cfTokenBtn = new Button { Content = "Set Cloudflare Token", Margin = new Thickness(0,0,8,0) };
            cfTokenBtn.Click += (s, e) =>
            {
                MobileOverlay.ShowOverlay();
                MobileOverlay.PromptPermanentToken();
            };
            var ngTokenBtn = new Button { Content = "Set ngrok Token" };
            ngTokenBtn.Click += (s, e) =>
            {
                MobileOverlay.ShowOverlay();
                MobileOverlay.PromptNgrokToken();
            };
            tokenStack.Children.Add(cfTokenBtn);
            tokenStack.Children.Add(ngTokenBtn);
            stack.Children.Add(tokenStack);

            Grid.SetRow(stack, 1);
            grid.Children.Add(stack);

            this.UserContent = grid;
        }

        public static void ShowOverlay()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_instance == null || !_instance.IsLoaded)
                {
                    _instance = new TunnelOverlay();
                }

                _instance.Show();
                _instance.Activate();
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            _instance = null;
            base.OnClosed(e);
        }
    }
}
