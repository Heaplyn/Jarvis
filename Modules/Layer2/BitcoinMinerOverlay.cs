// Developer: heaplyn
// Date: 2026-08-09
// Summary: Interactive Bitcoin Mining Simulation overlay window complete with live hash rate, shares accepted, block rewards, and matrix animation.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace JarvisLauncher
{
    public class MinerData
    {
        public double TotalBtc { get; set; } = 0.00000000;
        public long TotalShares { get; set; } = 0;
        public int BlocksFound { get; set; } = 0;
    }

    public class BitcoinMinerOverlay : BaseOverlay
    {
        private static BitcoinMinerOverlay? _instance;

        private readonly DispatcherTimer _mineTimer;
        private readonly Random _rand = new Random();
        private readonly TextBox _hashLogBox;
        private readonly TextBlock _hashRateBlock;
        private readonly TextBlock _btcBalanceBlock;
        private readonly TextBlock _sharesBlock;
        private readonly TextBlock _blocksBlock;

        private double _currentHashRateMHs = 450.0;
        private MinerData _data = new MinerData();

        public static void ToggleMiner()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_instance == null)
                {
                    _instance = new BitcoinMinerOverlay();
                    _instance.Show();
                }
                else
                {
                    _instance.FadeOutAndClose();
                    _instance = null;
                }
            });
        }

        private BitcoinMinerOverlay()
            : base("⛏️ JARVIS BITCOIN MINER SIMULATOR", width: 620, height: 420)
        {
            LoadMinerData();

            this.Closed += (s, e) =>
            {
                _mineTimer?.Stop();
                SaveMinerData();
                _instance = null;
            };

            var rootGrid = new Grid { Margin = new Thickness(8) };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Top Stats bar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Hash log

            // 1. Stats Bar Border
            var statsBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
                Padding = new Thickness(12, 8, 12, 8),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var statsGrid = new Grid();
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _hashRateBlock = CreateStatBlock("⚡ Hashrate", "0.00 MH/s", 0, statsGrid);
            _btcBalanceBlock = CreateStatBlock("💰 BTC Earned", $"{_data.TotalBtc:F8} BTC", 1, statsGrid);
            _sharesBlock = CreateStatBlock("✅ Shares", _data.TotalShares.ToString(), 2, statsGrid);
            _blocksBlock = CreateStatBlock("🧱 Blocks", _data.BlocksFound.ToString(), 3, statsGrid);

            statsBorder.Child = statsGrid;
            Grid.SetRow(statsBorder, 0);
            rootGrid.Children.Add(statsBorder);

            // 2. Hash Mining Terminal Log
            _hashLogBox = new TextBox
            {
                Text = "Starting SHA-256 Mining Rig...\nConnecting to Stratum Pool stratum+tcp://btc.jarvis.pool:3333...\n[SUCCESS] Connected! Target difficulty: 0x00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff\n\n",
                IsReadOnly = true,
                AcceptsReturn = true,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 120)), // Matrix Green
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 12,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(6)
            };
            _hashLogBox.SetResourceReference(TextBox.BorderBrushProperty, "SelectedBorderBrush");

            Grid.SetRow(_hashLogBox, 1);
            rootGrid.Children.Add(_hashLogBox);

            this.UserContent = rootGrid;

            // 3. Fast Mining Loop Timer (Ticks every 150ms)
            _mineTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _mineTimer.Tick += MineStep;
            _mineTimer.Start();
        }

        private void MineStep(object? sender, EventArgs e)
        {
            // Fluctuate hash rate slightly
            _currentHashRateMHs += (_rand.NextDouble() - 0.48) * 15.0;
            if (_currentHashRateMHs < 300) _currentHashRateMHs = 300;
            _hashRateBlock.Text = $"{_currentHashRateMHs:F1} MH/s";

            // Generate fake block hash
            byte[] nonceBytes = new byte[16];
            _rand.NextBytes(nonceBytes);
            string hexHash;
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(nonceBytes);
                hexHash = BitConverter.ToString(hash).Replace("-", "").ToLower();
            }

            // Simulate rare Share accepted (1 in 5 ticks)
            bool isShareAccepted = _rand.Next(0, 5) == 0;
            if (isShareAccepted)
            {
                _data.TotalShares++;
                _data.TotalBtc += 0.00000012 + (_rand.NextDouble() * 0.00000005);
                _sharesBlock.Text = _data.TotalShares.ToString("N0");
                _btcBalanceBlock.Text = $"{_data.TotalBtc:F8} BTC";

                _hashLogBox.AppendText($"[SHARE ACCEPTED] Nonce: 0x{hexHash.Substring(0, 8)} | Hash: 0000{hexHash.Substring(4, 32)} (Diff: 4.8k)\n");
            }
            else
            {
                _hashLogBox.AppendText($"[HASH] 0x{hexHash}\n");
            }

            // Simulate ultra rare Block found (1 in 150 ticks)
            if (_rand.Next(0, 150) == 77)
            {
                _data.BlocksFound++;
                _data.TotalBtc += 3.125; // Current BTC block reward
                _blocksBlock.Text = _data.BlocksFound.ToString();
                _btcBalanceBlock.Text = $"{_data.TotalBtc:F8} BTC";
                _hashLogBox.AppendText($"\n🎉🎉🎉 BLOCK FOUND! Block #{850000 + _data.BlocksFound} Solved! Reward: +3.125 BTC 🎉🎉🎉\n\n");
            }

            // Keep log scrolled to bottom
            if (_hashLogBox.Text.Length > 8000)
            {
                _hashLogBox.Text = _hashLogBox.Text.Substring(4000);
            }
            _hashLogBox.ScrollToEnd();
        }

        private TextBlock CreateStatBlock(string title, string initialValue, int col, Grid parentGrid)
        {
            var stack = new StackPanel();
            var titleLabel = new TextBlock
            {
                Text = title,
                FontSize = 10,
                FontFamily = new FontFamily("Segoe UI")
            };
            titleLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            stack.Children.Add(titleLabel);

            var valueLabel = new TextBlock
            {
                Text = initialValue,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas, Segoe UI")
            };
            valueLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            stack.Children.Add(valueLabel);

            Grid.SetColumn(stack, col);
            parentGrid.Children.Add(stack);
            return valueLabel;
        }

        private string GetFilePath()
        {
            string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            return Path.Combine(dataDir, "BitcoinMinerData.json");
        }

        private void LoadMinerData()
        {
            try
            {
                string p = GetFilePath();
                if (File.Exists(p))
                {
                    string json = File.ReadAllText(p);
                    _data = JsonSerializer.Deserialize<MinerData>(json) ?? new MinerData();
                }
            }
            catch { }
        }

        private void SaveMinerData()
        {
            try
            {
                string p = GetFilePath();
                string json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(p, json);
            }
            catch { }
        }
    }
}
