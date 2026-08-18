// Developer: heaplyn
// Date: 2026-08-18
// Summary: Custom Dark Glassmorphic Multi-Tab Master System Settings & Configuration Studio.
//          Fixed Brush cast error and restored missing namespaces.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace JarvisLauncher
{
    public class SettingsOverlay : BaseOverlay
    {
        private static SettingsOverlay? _instance;
        private readonly List<Button> _tabButtons = new List<Button>();
        private readonly List<UIElement> _tabPanels = new List<UIElement>();
        private Grid _contentGrid = null!;

        // AI Controls
        private ComboBox _llmBackendCombo = null!;
        private StackPanel _geminiPanel = null!;
        private StackPanel _openAiPanel = null!;
        private StackPanel _groqPanel = null!;
        private StackPanel _anthropicPanel = null!;
        private StackPanel _deepSeekPanel = null!;
        private StackPanel _ollamaPanel = null!;

        private TextBox _googleKeyBox = null!;
        private TextBox _geminiModelBox = null!;
        private TextBox _openAiKeyBox = null!;
        private TextBox _groqKeyBox = null!;
        private TextBox _anthropicKeyBox = null!;
        private TextBox _deepSeekKeyBox = null!;
        private TextBox _ollamaUrlBox = null!;
        private TextBlock _testStatusLabel = null!;

        public static void OpenSettings() => ShowSettings();
        public static void ShowOverlay() => ShowSettings();
        public static void ShowSettings() {
            Application.Current.Dispatcher.Invoke(() => {
                if (_instance == null || !_instance.IsLoaded) { _instance = new SettingsOverlay(); _instance.Show(); }
                else { _instance.Activate(); _instance.BringToFront(); }
            });
        }

        public SettingsOverlay() : base("⚙️ MASTER SYSTEM SETTINGS", 880, 780)
        {
            _instance = this;
            this.Closed += (s, e) => _instance = null;
            var workArea = SystemParameters.WorkArea;
            this.Left = (workArea.Width - this.Width) / 2;
            this.Top = (workArea.Height - this.Height) / 2;

            var mainGrid = new Grid { Margin = new Thickness(15) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var tabBarGrid = new UniformGrid { Columns = 8, Margin = new Thickness(0, 0, 0, 15) };
            string[] tabs = { "⚙️ Gen", "🤖 LLM", "🗣️ TTS", "🎙️ Vox", "🧹 Data", "📶 Off", "🏷️ Map", "💬 Chat" };
            for (int i = 0; i < tabs.Length; i++) {
                int idx = i;
                var btn = new Button { Content = tabs[i], Padding = new Thickness(5, 12, 5, 12), FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(2, 0, 2, 0), Cursor = System.Windows.Input.Cursors.Hand };
                btn.Click += (s, e) => SelectTab(idx);
                _tabButtons.Add(btn); tabBarGrid.Children.Add(btn);
            }
            Grid.SetRow(tabBarGrid, 0); mainGrid.Children.Add(tabBarGrid);

            _contentGrid = new Grid(); Grid.SetRow(_contentGrid, 1);
            _tabPanels.Add(BuildGeneralTab()); _tabPanels.Add(BuildLlmTab());
            _tabPanels.Add(BuildTtsTab()); _tabPanels.Add(BuildVoiceAiTab());
            _tabPanels.Add(BuildCleanupTab()); _tabPanels.Add(BuildOfflineTab());
            _tabPanels.Add(BuildAliasesTab()); _tabPanels.Add(BuildChatTab());
            foreach (var p in _tabPanels) _contentGrid.Children.Add(p);
            mainGrid.Children.Add(_contentGrid);

            var saveBtn = CreateStyledButton("💾 Save Configuration", (s, e) => SaveAllSettings(), isPrimary: true, fontSize: 13);
            Grid.SetRow(saveBtn, 2); mainGrid.Children.Add(saveBtn);

            this.UserContent = mainGrid;
            SelectTab(1);
        }

        private void SelectTab(int index) {
            for (int i = 0; i < _tabPanels.Count; i++) {
                bool sel = (i == index);
                _tabPanels[i].Visibility = sel ? Visibility.Visible : Visibility.Collapsed;

                var accent = TryFindResource("AccentBrush") as Brush ?? Brushes.DeepSkyBlue;
                var cardBg = TryFindResource("CardBackgroundBrush") as Brush ?? Brushes.Transparent;
                var textCol = TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.White;

                _tabButtons[i].Background = sel ? accent : cardBg;
                _tabButtons[i].Foreground = sel ? Brushes.White : textCol;
            }
        }

        private UIElement BuildLlmTab()
        {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root = new StackPanel { Margin = new Thickness(10) }; scroll.Content = root;
            var s = SettingsManager.Current;

            _llmBackendCombo = CreateSettingsComboBox(new[] { "Gemini", "Groq", "OpenAI", "Anthropic", "DeepSeek", "Ollama", "Godellian" }, s.LLM_BACKEND);
            _llmBackendCombo.SelectionChanged += (obj, e) => UpdateLlmPanels();
            root.Children.Add(CreateLabel("Primary Intelligence:")); root.Children.Add(_llmBackendCombo);

            _geminiPanel = new StackPanel();
            _googleKeyBox = CreateLabeledTextBox(_geminiPanel, "API Key:", s.GOOGLE_AI_KEY);
            _geminiModelBox = CreateLabeledTextBox(_geminiPanel, "Model:", s.GEMINI_MODEL);
            root.Children.Add(_geminiPanel);

            _groqPanel = new StackPanel(); _groqKeyBox = CreateLabeledTextBox(_groqPanel, "Groq Key:", s.GROQ_KEY); root.Children.Add(_groqPanel);
            _openAiPanel = new StackPanel(); _openAiKeyBox = CreateLabeledTextBox(_openAiPanel, "OpenAI Key:", s.OPENAI_KEY); root.Children.Add(_openAiPanel);
            _anthropicPanel = new StackPanel(); _anthropicKeyBox = CreateLabeledTextBox(_anthropicPanel, "Anthropic Key:", s.ANTHROPIC_KEY); root.Children.Add(_anthropicPanel);
            _deepSeekPanel = new StackPanel(); _deepSeekKeyBox = CreateLabeledTextBox(_deepSeekPanel, "DeepSeek Key:", s.CUSTOM_LLM_KEY); root.Children.Add(_deepSeekPanel);
            _ollamaPanel = new StackPanel(); _ollamaUrlBox = CreateLabeledTextBox(_ollamaPanel, "Ollama URL:", s.OLLAMA_ENDPOINT); root.Children.Add(_ollamaPanel);

            _testStatusLabel = new TextBlock { Text = "Pending Test", FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(0, 5, 0, 5) };
            root.Children.Add(_testStatusLabel);
            root.Children.Add(CreateStyledButton("⚡ Test Connection", async (s, e) => await RunBackendTestAsync(), false));

            UpdateLlmPanels();
            return scroll;
        }

        private void UpdateLlmPanels() {
            if (_llmBackendCombo == null) return;
            string sel = (_llmBackendCombo.SelectedItem as string) ?? "Gemini";
            _geminiPanel.Visibility = sel == "Gemini" ? Visibility.Visible : Visibility.Collapsed;
            _groqPanel.Visibility = sel == "Groq" ? Visibility.Visible : Visibility.Collapsed;
            _openAiPanel.Visibility = sel == "OpenAI" ? Visibility.Visible : Visibility.Collapsed;
            _anthropicPanel.Visibility = sel == "Anthropic" ? Visibility.Visible : Visibility.Collapsed;
            _deepSeekPanel.Visibility = sel == "DeepSeek" ? Visibility.Visible : Visibility.Collapsed;
            _ollamaPanel.Visibility = sel == "Ollama" ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task RunBackendTestAsync() {
            _testStatusLabel.Text = "Testing..."; SaveAllSettings();
            try {
                string res = await CoreRegistry.Intelligence.Llm.AskAsync("ONLINE_TEST");
                _testStatusLabel.Text = "✅ Connected."; _testStatusLabel.Foreground = Brushes.SpringGreen;
            } catch { _testStatusLabel.Text = "❌ Failed."; _testStatusLabel.Foreground = Brushes.Tomato; }
        }

        private void SaveAllSettings() {
            var s = SettingsManager.Current;
            if (_themeComboBox?.SelectedItem is string th) s.THEME = th;
            if (_llmBackendCombo?.SelectedItem is string llm) s.LLM_BACKEND = llm;
            s.GOOGLE_AI_KEY = _googleKeyBox.Text.Trim();
            s.GEMINI_MODEL = _geminiModelBox.Text.Trim();
            s.GROQ_KEY = _groqKeyBox.Text.Trim();
            s.OPENAI_KEY = _openAiKeyBox.Text.Trim();
            s.ANTHROPIC_KEY = _anthropicKeyBox.Text.Trim();
            s.CUSTOM_LLM_KEY = _deepSeekKeyBox.Text.Trim();
            s.OLLAMA_ENDPOINT = _ollamaUrlBox.Text.Trim();
            SettingsManager.Save();
            TextOverlay.Show("✅ Saved", 1000);
        }

        private ComboBox _themeComboBox = null!;
        private UIElement BuildGeneralTab() {
            var s = new StackPanel { Margin = new Thickness(10) };
            _themeComboBox = CreateSettingsComboBox(new[] { "dracula", "dark", "purple" }, SettingsManager.Current.THEME);
            s.Children.Add(CreateLabel("Theme:")); s.Children.Add(_themeComboBox); return s;
        }

        private UIElement BuildTtsTab() => new StackPanel { Children = { new TextBlock { Text = "TTS Settings", Foreground = Brushes.White } } };
        private UIElement BuildVoiceAiTab() => new StackPanel { Children = { new TextBlock { Text = "Voice AI", Foreground = Brushes.White } } };
        private UIElement BuildCleanupTab() => new StackPanel { Children = { new TextBlock { Text = "Cleanup", Foreground = Brushes.White } } };
        private UIElement BuildOfflineTab() => new StackPanel { Children = { new TextBlock { Text = "Offline", Foreground = Brushes.White } } };
        private UIElement BuildAliasesTab() => new StackPanel { Children = { new TextBlock { Text = "Aliases", Foreground = Brushes.White } } };
        private UIElement BuildChatTab() => new StackPanel { Children = { new TextBlock { Text = "Chat", Foreground = Brushes.White } } };

        private ComboBox CreateSettingsComboBox(IEnumerable<string> items, string selected) {
            var cb = new ComboBox { Margin = new Thickness(0, 5, 0, 15), Height = 32, Background = new SolidColorBrush(Color.FromArgb(50, 0,0,0)), Foreground = Brushes.White };
            foreach (var item in items) cb.Items.Add(item);
            cb.SelectedItem = cb.Items.Cast<object>().FirstOrDefault(i => i.ToString() == selected) ?? cb.Items[0];
            return cb;
        }

        private TextBox CreateLabeledTextBox(StackPanel p, string label, string value) {
            p.Children.Add(CreateLabel(label));
            var tb = new TextBox { Text = value, Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(5), Background = new SolidColorBrush(Color.FromArgb(40, 255,255,255)), Foreground = Brushes.White };
            p.Children.Add(tb); return tb;
        }

        private static TextBlock CreateLabel(string t) => new TextBlock { Text = t, FontSize = 11, Foreground = Brushes.LightGray };
    }
}
