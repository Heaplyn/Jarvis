// Developer: heaplyn
// Date: 2026-08-10
// Summary: Glassmorphic LLM Settings overlay. Lets user switch between Gemini, OpenAI, Ollama,
//          Custom endpoint, and P2P peer backends. Also manages P2P peer list and server toggle.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JarvisLauncher
{
    public class LlmSettingsOverlay : BaseOverlay
    {
        private static LlmSettingsOverlay? _instance;

        private ComboBox _backendCombo = null!;
        private StackPanel _geminiPanel = null!;
        private StackPanel _openAiPanel = null!;
        private StackPanel _ollamaPanel = null!;
        private StackPanel _customPanel = null!;
        private StackPanel _p2pPanel = null!;
        private StackPanel _peerListStack = null!;
        private TextBlock _statusText = null!;

        public LlmSettingsOverlay()
            : base("LLM ENGINE SETTINGS", width: 480, height: 600)
        {
            var workArea = SystemParameters.WorkArea;
            this.Left = (workArea.Width - this.Width) / 2;
            this.Top = (workArea.Height - this.Height) / 2;

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root = new StackPanel { Margin = new Thickness(4) };
            scroll.Content = root;

            // ── Backend Selector ──────────────────────────────────────────────────────
            root.Children.Add(MakeSectionHeader("🤖 LLM Backend"));

            _backendCombo = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13
            };
            foreach (var b in new[] { "Gemini", "OpenAI", "Ollama", "Custom", "P2P" })
                _backendCombo.Items.Add(b);
            _backendCombo.SelectedItem = SettingsManager.Current.LlmBackend;
            _backendCombo.SelectionChanged += (s, e) => UpdatePanelVisibility();
            root.Children.Add(_backendCombo);

            // ── Gemini Panel ──────────────────────────────────────────────────────────
            _geminiPanel = new StackPanel();
            _geminiPanel.Children.Add(MakeLabel("Google Gemini API Key:"));
            var geminiKey = MakeTextBox(SettingsManager.Current.GoogleAIKey, "AIza...");
            geminiKey.TextChanged += (s, e) => SettingsManager.Current.GoogleAIKey = geminiKey.Text.Trim();
            _geminiPanel.Children.Add(geminiKey);
            root.Children.Add(_geminiPanel);

            // ── OpenAI Panel ──────────────────────────────────────────────────────────
            _openAiPanel = new StackPanel();
            _openAiPanel.Children.Add(MakeLabel("OpenAI API Key (or LM Studio key):"));
            var oaiKey = MakeTextBox(SettingsManager.Current.OpenAIKey, "sk-...");
            oaiKey.TextChanged += (s, e) => SettingsManager.Current.OpenAIKey = oaiKey.Text.Trim();
            _openAiPanel.Children.Add(oaiKey);
            _openAiPanel.Children.Add(MakeLabel("Base URL (default: https://api.openai.com/v1):"));
            var oaiBase = MakeTextBox(SettingsManager.Current.OpenAIBaseUrl, "https://api.openai.com/v1");
            oaiBase.TextChanged += (s, e) => SettingsManager.Current.OpenAIBaseUrl = oaiBase.Text.Trim();
            _openAiPanel.Children.Add(oaiBase);
            _openAiPanel.Children.Add(MakeLabel("Model (e.g. gpt-4o-mini, gpt-4o):"));
            var oaiModel = MakeTextBox(SettingsManager.Current.OpenAIModel, "gpt-4o-mini");
            oaiModel.TextChanged += (s, e) => SettingsManager.Current.OpenAIModel = oaiModel.Text.Trim();
            _openAiPanel.Children.Add(oaiModel);
            root.Children.Add(_openAiPanel);

            // ── Ollama Panel ──────────────────────────────────────────────────────────
            _ollamaPanel = new StackPanel();
            _ollamaPanel.Children.Add(MakeLabel("Ollama Endpoint (default: http://localhost:11434):"));
            var ollamaEndpoint = MakeTextBox(SettingsManager.Current.OllamaEndpoint, "http://localhost:11434");
            ollamaEndpoint.TextChanged += (s, e) => SettingsManager.Current.OllamaEndpoint = ollamaEndpoint.Text.Trim();
            _ollamaPanel.Children.Add(ollamaEndpoint);
            _ollamaPanel.Children.Add(MakeLabel("Model (e.g. llama3, mistral, phi3):"));
            var ollamaModel = MakeTextBox(SettingsManager.Current.OllamaModel, "llama3");
            ollamaModel.TextChanged += (s, e) => SettingsManager.Current.OllamaModel = ollamaModel.Text.Trim();
            _ollamaPanel.Children.Add(ollamaModel);
            var detectBtn = MakeButton("🔍 Auto-Detect Installed Models");
            detectBtn.Click += async (s, e) =>
            {
                detectBtn.Content = "⏳ Detecting...";
                var models = await LlmRouter.GetOllamaModelsAsync();
                if (models.Count > 0)
                {
                    ollamaModel.Text = models[0];
                    detectBtn.Content = $"✅ Found: {string.Join(", ", models)}";
                }
                else
                    detectBtn.Content = "⚠️ Ollama not running or no models installed";
            };
            _ollamaPanel.Children.Add(detectBtn);
            root.Children.Add(_ollamaPanel);

            // ── Custom Panel ──────────────────────────────────────────────────────────
            _customPanel = new StackPanel();
            _customPanel.Children.Add(MakeLabel("Custom Endpoint URL (OpenAI-compatible /chat/completions):"));
            var customUrl = MakeTextBox(SettingsManager.Current.CustomLlmEndpoint, "http://...");
            customUrl.TextChanged += (s, e) => SettingsManager.Current.CustomLlmEndpoint = customUrl.Text.Trim();
            _customPanel.Children.Add(customUrl);
            _customPanel.Children.Add(MakeLabel("API Key (optional):"));
            var customKey = MakeTextBox(SettingsManager.Current.CustomLlmKey, "optional");
            customKey.TextChanged += (s, e) => SettingsManager.Current.CustomLlmKey = customKey.Text.Trim();
            _customPanel.Children.Add(customKey);
            _customPanel.Children.Add(MakeLabel("Model name:"));
            var customModel = MakeTextBox(SettingsManager.Current.CustomLlmModel, "model-name");
            customModel.TextChanged += (s, e) => SettingsManager.Current.CustomLlmModel = customModel.Text.Trim();
            _customPanel.Children.Add(customModel);
            root.Children.Add(_customPanel);

            // ── P2P Panel ─────────────────────────────────────────────────────────────
            _p2pPanel = new StackPanel();
            _p2pPanel.Children.Add(MakeSectionHeader("🌐 P2P Peer Compute Nodes"));

            // Server toggle
            var serverToggle = new CheckBox
            {
                Content = "📡 Enable P2P Server on This PC (let peers offload to me)",
                IsChecked = SettingsManager.Current.P2PServerEnabled,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };
            serverToggle.SetResourceReference(CheckBox.ForegroundProperty, "TextPrimaryBrush");
            serverToggle.Checked += (s, e) => { SettingsManager.Current.P2PServerEnabled = true; SettingsManager.Save(); };
            serverToggle.Unchecked += (s, e) => { SettingsManager.Current.P2PServerEnabled = false; SettingsManager.Save(); };
            _p2pPanel.Children.Add(serverToggle);

            _p2pPanel.Children.Add(MakeLabel("Shared Secret (optional, protects /p2p/ask from strangers):"));
            var secretBox = MakeTextBox(SettingsManager.Current.P2PServerSecret, "leave blank for LAN-open");
            secretBox.TextChanged += (s, e) => { SettingsManager.Current.P2PServerSecret = secretBox.Text.Trim(); SettingsManager.Save(); };
            _p2pPanel.Children.Add(secretBox);

            _p2pPanel.Children.Add(MakeSectionHeader("🖥️ Registered Peer PCs"));
            _peerListStack = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
            _p2pPanel.Children.Add(_peerListStack);
            RefreshPeerList();

            // Add peer row
            var addRow = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var peerUrlBox = MakeTextBox("", "http://192.168.1.x:8085 or Cloudflare URL");
            Grid.SetColumn(peerUrlBox, 0);
            addRow.Children.Add(peerUrlBox);
            var peerSecretBox = MakeTextBox("", "secret (opt.)");
            peerSecretBox.Width = 100;
            peerSecretBox.Margin = new Thickness(6, 0, 0, 0);
            Grid.SetColumn(peerSecretBox, 1);
            addRow.Children.Add(peerSecretBox);
            _p2pPanel.Children.Add(addRow);

            var addBtn = MakeButton("➕ Add Peer PC");
            addBtn.Click += (s, e) =>
            {
                string url = peerUrlBox.Text.Trim();
                if (string.IsNullOrEmpty(url)) return;
                JarvisP2PClient.AddPeer(url, peerSecretBox.Text.Trim());
                peerUrlBox.Text = "";
                peerSecretBox.Text = "";
                RefreshPeerList();
            };
            _p2pPanel.Children.Add(addBtn);

            var probeBtn = MakeButton("📡 Probe All Peers");
            probeBtn.Click += async (s, e) =>
            {
                probeBtn.Content = "⏳ Probing...";
                await JarvisP2PClient.ProbeAllPeersAsync();
                RefreshPeerList();
                probeBtn.Content = "📡 Probe All Peers";
            };
            _p2pPanel.Children.Add(probeBtn);
            root.Children.Add(_p2pPanel);

            // ── Test Button + Status ───────────────────────────────────────────────────
            root.Children.Add(MakeSectionHeader(""));
            var testBtn = MakeButton("⚡ Test Selected Backend");
            testBtn.Margin = new Thickness(0, 8, 0, 6);
            testBtn.Click += async (s, e) =>
            {
                string sel = (_backendCombo.SelectedItem as string) ?? "Gemini";
                SettingsManager.Current.LlmBackend = sel;
                testBtn.Content = "⏳ Testing...";
                _statusText.Text = "";
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    string resp = await LlmRouter.AskAsync("Reply with exactly: 'Jarvis online.'");
                    sw.Stop();
                    _statusText.Text = $"✅ {sel} responded in {sw.ElapsedMilliseconds}ms:\n{resp}";
                    _statusText.Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128));
                }
                catch (Exception ex)
                {
                    _statusText.Text = $"❌ {ex.Message}";
                    _statusText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                }
                testBtn.Content = "⚡ Test Selected Backend";
            };
            root.Children.Add(testBtn);

            _statusText = new TextBlock
            {
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };
            _statusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            root.Children.Add(_statusText);

            // Save & Close button
            var saveBtn = MakeButton("💾 Save & Close");
            saveBtn.Margin = new Thickness(0, 12, 0, 0);
            saveBtn.Click += (s, e) =>
            {
                SettingsManager.Current.LlmBackend = (_backendCombo.SelectedItem as string) ?? "Gemini";
                SettingsManager.Save();
                TextOverlay.Show($"✅ LLM Backend set to: {SettingsManager.Current.LlmBackend}", 2500);
                this.Close();
            };
            root.Children.Add(saveBtn);

            this.UserContent = scroll;
            UpdatePanelVisibility();
        }

        private void UpdatePanelVisibility()
        {
            string sel = (_backendCombo.SelectedItem as string) ?? "Gemini";
            _geminiPanel.Visibility = sel == "Gemini" ? Visibility.Visible : Visibility.Collapsed;
            _openAiPanel.Visibility = sel == "OpenAI" ? Visibility.Visible : Visibility.Collapsed;
            _ollamaPanel.Visibility = sel == "Ollama" ? Visibility.Visible : Visibility.Collapsed;
            _customPanel.Visibility = sel == "Custom" ? Visibility.Visible : Visibility.Collapsed;
            _p2pPanel.Visibility = sel == "P2P" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshPeerList()
        {
            _peerListStack.Children.Clear();
            var peers = JarvisP2PClient.Peers;
            if (peers.Count == 0)
            {
                var empty = new TextBlock
                {
                    Text = "No peers registered. Add a peer PC URL below.",
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 2)
                };
                empty.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                _peerListStack.Children.Add(empty);
                return;
            }

            foreach (var peer in peers)
            {
                var row = new Border
                {
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 6, 8, 6),
                    Margin = new Thickness(0, 2, 0, 2)
                };
                row.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");
                row.SetResourceReference(Border.BackgroundProperty, "HoverBackgroundBrush");

                var rowGrid = new Grid();
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                string status = peer.IsOnline
                    ? $"✅ {peer.PcName} | CPU {peer.CpuLoad:F0}% | {peer.RamFreeGb:F1}GB free | {peer.LatencyMs}ms"
                    : peer.LastChecked == DateTime.MinValue ? "⬜ Not yet probed" : "❌ Offline";

                var info = new TextBlock
                {
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = $"{peer.Nickname}\n{status}"
                };
                info.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                Grid.SetColumn(info, 0);
                rowGrid.Children.Add(info);

                var removeBtn = new Button
                {
                    Content = "🗑️",
                    FontSize = 12,
                    Padding = new Thickness(6, 2, 6, 2),
                    Cursor = Cursors.Hand,
                    ToolTip = "Remove peer"
                };
                string peerUrl = peer.Url;
                removeBtn.Click += (s, e) =>
                {
                    JarvisP2PClient.RemovePeer(peerUrl);
                    RefreshPeerList();
                };
                Grid.SetColumn(removeBtn, 1);
                rowGrid.Children.Add(removeBtn);

                row.Child = rowGrid;
                _peerListStack.Children.Add(row);
            }
        }

        // ── UI Helpers ────────────────────────────────────────────────────────────────

        private static TextBlock MakeSectionHeader(string text)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 10, 0, 6)
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "AccentCaretBrush");
            return tb;
        }

        private static TextBlock MakeLabel(string text)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 2),
                TextWrapping = TextWrapping.Wrap
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            return tb;
        }

        private static TextBox MakeTextBox(string value, string placeholder)
        {
            var tb = new TextBox
            {
                Text = value,
                FontSize = 12,
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 6),
                BorderThickness = new Thickness(1)
            };
            tb.SetResourceReference(TextBox.BackgroundProperty, "WindowBackgroundBrush");
            tb.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            tb.SetResourceReference(TextBox.BorderBrushProperty, "WindowBorderBrush");
            return tb;
        }

        private static Button MakeButton(string label)
        {
            var btn = new Button
            {
                Content = label,
                FontSize = 12,
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 2, 0, 4),
                Cursor = Cursors.Hand
            };
            btn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            btn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            return btn;
        }

        public static void ShowOverlay()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_instance == null || !_instance.IsLoaded)
                {
                    _instance = new LlmSettingsOverlay();
                    _instance.Show();
                }
                else
                {
                    _instance.Activate();
                    _instance.Focus();
                }
            });
        }
    }
}
