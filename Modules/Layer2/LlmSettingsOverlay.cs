// Developer: heaplyn
// Date: 2026-08-17
// Summary: Glassmorphic LLM Settings & Installer Overlay.
//          Hardened against NullReferenceExceptions.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace JarvisLauncher
{
    public class LlmSettingsOverlay : BaseOverlay
    {
        private static LlmSettingsOverlay? _instance;

        private ComboBox _backendCombo = null!;
        private StackPanel _geminiPanel = null!;
        private ComboBox _geminiModelCombo = null!;
        private StackPanel _openAiPanel = null!;
        private ComboBox _openAiModelCombo = null!;
        private StackPanel _anthropicPanel = null!;
        private ComboBox _anthropicModelCombo = null!;
        private StackPanel _deepSeekPanel = null!;
        private ComboBox _deepSeekModelCombo = null!;
        private StackPanel _ollamaPanel = null!;
        private TextBlock _statusText = null!;

        public LlmSettingsOverlay()
            : base("LLM ENGINE & INSTALLER STUDIO", width: 520, height: 750)
        {
            var workArea = SystemParameters.WorkArea;
            this.Left = (workArea.Width - this.Width) / 2;
            this.Top = (workArea.Height - this.Height) / 2;

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root = new StackPanel { Margin = new Thickness(12) };
            scroll.Content = root;

            // ── Backend Selector ──────────────────────────────────────────────────────
            root.Children.Add(CreateHeader("🤖 Active LLM Engine"));

            _backendCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(8, 6, 8, 6), FontSize = 13 };
            foreach (var b in new[] { "Auto", "Gemini", "OpenAI", "Anthropic", "Groq", "DeepSeek", "Perplexity", "Mistral", "OpenRouter", "Ollama", "Custom" })
                _backendCombo.Items.Add(b);

            root.Children.Add(_backendCombo);

            // ── Panels Initialization ──────────────────────────────────────────────────

            // 1. Gemini
            _geminiPanel = new StackPanel { Visibility = Visibility.Collapsed };
            _geminiPanel.Children.Add(CreateLabel("Google Gemini API Key:"));
            var geminiKey = CreateTextBox(CoreRegistry.Settings.Current?.GOOGLE_AI_KEY);
            geminiKey.TextChanged += (s, e) => { if (CoreRegistry.Settings.Current != null) CoreRegistry.Settings.Current.GOOGLE_AI_KEY = geminiKey.Text.Trim(); };
            _geminiPanel.Children.Add(geminiKey);
            _geminiPanel.Children.Add(CreateLabel("Gemini Model:"));
            _geminiModelCombo = CreateEditableComboBox(new[] { "gemini-2.0-flash", "gemini-1.5-flash", "gemini-1.5-pro" }, CoreRegistry.Settings.Current?.GEMINI_MODEL);
            _geminiModelCombo.SelectionChanged += (s, e) => {
                var selected = _geminiModelCombo.SelectedItem?.ToString() ?? _geminiModelCombo.Text;
                if (CoreRegistry.Settings.Current != null) CoreRegistry.Settings.Current.GEMINI_MODEL = selected;
            };
            _geminiPanel.Children.Add(_geminiModelCombo);
            root.Children.Add(_geminiPanel);

            // 2. OpenAI
            _openAiPanel = new StackPanel { Visibility = Visibility.Collapsed };
            _openAiPanel.Children.Add(CreateLabel("OpenAI API Key:"));
            var oaiKey = CreateTextBox(CoreRegistry.Settings.Current?.OPENAI_KEY);
            oaiKey.TextChanged += (s, e) => { if (CoreRegistry.Settings.Current != null) CoreRegistry.Settings.Current.OPENAI_KEY = oaiKey.Text.Trim(); };
            _openAiPanel.Children.Add(oaiKey);
            _openAiPanel.Children.Add(CreateLabel("OpenAI Model:"));
            _openAiModelCombo = CreateEditableComboBox(new[] { "gpt-4o", "gpt-4o-mini" }, CoreRegistry.Settings.Current?.OPENAI_MODEL);
            _openAiModelCombo.SelectionChanged += (s, e) => {
                var selected = _openAiModelCombo.SelectedItem?.ToString() ?? _openAiModelCombo.Text;
                if (CoreRegistry.Settings.Current != null) CoreRegistry.Settings.Current.OPENAI_MODEL = selected;
            };
            _openAiPanel.Children.Add(_openAiModelCombo);
            root.Children.Add(_openAiPanel);

            // 3. Anthropic
            _anthropicPanel = new StackPanel { Visibility = Visibility.Collapsed };
            _anthropicPanel.Children.Add(CreateLabel("Anthropic API Key:"));
            var antKey = CreateTextBox(CoreRegistry.Settings.Current?.ANTHROPIC_KEY);
            antKey.TextChanged += (s, e) => { if (CoreRegistry.Settings.Current != null) CoreRegistry.Settings.Current.ANTHROPIC_KEY = antKey.Text.Trim(); };
            _anthropicPanel.Children.Add(antKey);
            _anthropicPanel.Children.Add(CreateLabel("Anthropic Model:"));
            _anthropicModelCombo = CreateEditableComboBox(new[] { "claude-3-5-sonnet-20240620" }, CoreRegistry.Settings.Current?.ANTHROPIC_MODEL);
            _anthropicModelCombo.SelectionChanged += (s, e) => {
                var selected = _anthropicModelCombo.SelectedItem?.ToString() ?? _anthropicModelCombo.Text;
                if (CoreRegistry.Settings.Current != null) CoreRegistry.Settings.Current.ANTHROPIC_MODEL = selected;
            };
            _anthropicPanel.Children.Add(_anthropicModelCombo);
            root.Children.Add(_anthropicPanel);

            // 4. DeepSeek
            _deepSeekPanel = new StackPanel { Visibility = Visibility.Collapsed };
            _deepSeekPanel.Children.Add(CreateLabel("DeepSeek API Key:"));
            var dsKey = CreateTextBox(CoreRegistry.Settings.Current?.CUSTOM_LLM_KEY);
            dsKey.TextChanged += (s, e) => { if (CoreRegistry.Settings.Current != null) CoreRegistry.Settings.Current.CUSTOM_LLM_KEY = dsKey.Text.Trim(); };
            _deepSeekPanel.Children.Add(dsKey);
            _deepSeekPanel.Children.Add(CreateLabel("DeepSeek Model:"));
            _deepSeekModelCombo = CreateEditableComboBox(new[] { "deepseek-chat", "deepseek-reasoner" }, CoreRegistry.Settings.Current?.CUSTOM_LLM_MODEL);
            _deepSeekModelCombo.SelectionChanged += (s, e) => {
                var selected = _deepSeekModelCombo.SelectedItem?.ToString() ?? _deepSeekModelCombo.Text;
                if (CoreRegistry.Settings.Current != null) CoreRegistry.Settings.Current.CUSTOM_LLM_MODEL = selected;
            };
            _deepSeekPanel.Children.Add(_deepSeekModelCombo);
            root.Children.Add(_deepSeekPanel);

            // 5. Ollama / Auto
            _ollamaPanel = new StackPanel { Visibility = Visibility.Collapsed };
            _ollamaPanel.Children.Add(CreateLabel("Ollama Endpoint:"));
            var ollamaEndpoint = CreateTextBox(CoreRegistry.Settings.Current?.OLLAMA_ENDPOINT);
            ollamaEndpoint.TextChanged += (s, e) => { if (CoreRegistry.Settings.Current != null) CoreRegistry.Settings.Current.OLLAMA_ENDPOINT = ollamaEndpoint.Text.Trim(); };
            _ollamaPanel.Children.Add(ollamaEndpoint);
            _ollamaPanel.Children.Add(CreateLabel("Local Model:"));
            var ollamaModel = CreateTextBox(CoreRegistry.Settings.Current?.OLLAMA_MODEL);
            ollamaModel.TextChanged += (s, e) => { if (CoreRegistry.Settings.Current != null) CoreRegistry.Settings.Current.OLLAMA_MODEL = ollamaModel.Text.Trim(); };
            _ollamaPanel.Children.Add(ollamaModel);

            var discoverBtn = CreateStyledButton("🔍 Discover Servers", async (s, e) => {
                if (CoreRegistry.Llm != null) {
                    string res = await CoreRegistry.Llm.DiscoverAiServersAsync();
                    ContentPreviewOverlay.Show("Discovery Result", res, "markdown");
                }
            });
            _ollamaPanel.Children.Add(discoverBtn);
            root.Children.Add(_ollamaPanel);

            // ── Global Actions ───────────────────────────────────────────────────────
            root.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 10) });
            var testBtn = CreateStyledButton("⚡ Test Backend", async (s, e) => {
                try {
                    if (CoreRegistry.Llm != null) {
                        string res = await CoreRegistry.Llm.AskAsync("Reply: ONLINE.");
                        _statusText.Text = "✅ " + res;
                        _statusText.Foreground = Brushes.LightGreen;
                    }
                } catch (Exception ex) { 
                    _statusText.Text = "❌ " + ex.Message; 
                    _statusText.Foreground = Brushes.Tomato; 
                }
            });
            root.Children.Add(testBtn);

            _statusText = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 5), Foreground = Brushes.Gray };
            root.Children.Add(_statusText);

            var saveBtn = CreateStyledButton("💾 Save & Close", (s, e) => {
                if (_backendCombo?.SelectedItem != null && CoreRegistry.Settings.Current != null) 
                    CoreRegistry.Settings.Current.LLM_BACKEND = _backendCombo.SelectedItem.ToString()!;
                
                CoreRegistry.Settings.Save();
                this.FadeOutAndClose();
            }, isPrimary: true);
            root.Children.Add(saveBtn);

            this.UserContent = scroll;

            // Finalization: Attach event listeners AFTER panel setup is completely finished
            _backendCombo.SelectionChanged += (s, e) => UpdatePanelVisibility();
            
            if (CoreRegistry.Settings.Current?.LLM_BACKEND != null)
                _backendCombo.SelectedItem = CoreRegistry.Settings.Current.LLM_BACKEND;
            
            if (_backendCombo.SelectedItem == null) 
                _backendCombo.SelectedIndex = 0;

            UpdatePanelVisibility();
        }

        private void UpdatePanelVisibility()
        {
            if (_backendCombo?.SelectedItem == null) return;
            if (_geminiPanel == null || _openAiPanel == null || _anthropicPanel == null || _deepSeekPanel == null || _ollamaPanel == null) return;

            string sel = _backendCombo.SelectedItem.ToString()!;
            _geminiPanel.Visibility = sel == "Gemini" ? Visibility.Visible : Visibility.Collapsed;
            _openAiPanel.Visibility = sel == "OpenAI" ? Visibility.Visible : Visibility.Collapsed;
            _anthropicPanel.Visibility = sel == "Anthropic" ? Visibility.Visible : Visibility.Collapsed;
            _deepSeekPanel.Visibility = sel == "DeepSeek" ? Visibility.Visible : Visibility.Collapsed;
            _ollamaPanel.Visibility = (sel == "Ollama" || sel == "Auto") ? Visibility.Visible : Visibility.Collapsed;
        }

        private static TextBlock CreateHeader(string t) => new TextBlock { Text = t, FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 8, 0, 4), Foreground = Brushes.White };
        private static TextBlock CreateLabel(string t) => new TextBlock { Text = t, FontSize = 11, Margin = new Thickness(0, 4, 0, 2), Foreground = Brushes.LightGray };
        private static TextBox CreateTextBox(string? t) => new TextBox { Text = t ?? "", Margin = new Thickness(0, 0, 0, 6), Padding = new Thickness(6, 4, 6, 4), FontSize = 12, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Foreground = Brushes.White, BorderBrush = Brushes.DimGray };
        private static ComboBox CreateEditableComboBox(string[] items, string? current) { 
            var cb = new ComboBox { IsEditable = true, Margin = new Thickness(0, 0, 0, 8), FontSize = 12 }; 
            foreach (var x in items) cb.Items.Add(x); 
            if (!string.IsNullOrEmpty(current)) cb.Text = current; 
            return cb; 
        }

        public static void ShowOverlay() {
            Application.Current.Dispatcher.Invoke(() => {
                if (_instance == null || !_instance.IsLoaded) _instance = new LlmSettingsOverlay();
                _instance.Show(); 
                _instance.BringToFront();
            });
        }
    }
}