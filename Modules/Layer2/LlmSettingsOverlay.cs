// Developer: heaplyn
// Date: 2026-08-13
// Summary: Glassmorphic LLM Settings & Installer Overlay.
// Provides backend switching (Gemini, OpenAI, Ollama, Custom, P2P) & 1-Click Local LLM Installers (Ollama, LM Studio, Jan.ai, GPT4All, DeepSeek R1, Llama 3.2, Mistral).

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
        private StackPanel _groqPanel = null!;
        private ComboBox _groqModelCombo = null!;
        private StackPanel _perplexityPanel = null!;
        private ComboBox _perplexityModelCombo = null!;
        private StackPanel _mistralPanel = null!;
        private ComboBox _mistralModelCombo = null!;
        private StackPanel _openRouterPanel = null!;
        private ComboBox _openRouterModelCombo = null!;
        private StackPanel _ollamaPanel = null!;
        private StackPanel _customPanel = null!;
        private StackPanel _p2pPanel = null!;
        private StackPanel _peerListStack = null!;
        private TextBlock _statusText = null!;

        public LlmSettingsOverlay()
            : base("LLM ENGINE & INSTALLER STUDIO", width: 520, height: 680)
        {
            var workArea = SystemParameters.WorkArea;
            this.Left = (workArea.Width - this.Width) / 2;
            this.Top = (workArea.Height - this.Height) / 2;

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root = new StackPanel { Margin = new Thickness(4) };
            scroll.Content = root;

            // ── Backend Selector ──────────────────────────────────────────────────────
            root.Children.Add(CreateHeader("🤖 Active LLM Engine"));

            _backendCombo = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13
            };
            foreach (var b in new[] { "Gemini", "OpenAI", "Anthropic", "Groq", "Perplexity", "Mistral", "OpenRouter", "Ollama", "Custom", "P2P" })
                _backendCombo.Items.Add(b);

            // NOTE: Don't set SelectedItem here yet, as it fires SelectionChanged before all panels are created.
            // _backendCombo.SelectedItem = SettingsManager.Current.LLM_BACKEND;

            _backendCombo.SelectionChanged += (s, e) => UpdatePanelVisibility();
            root.Children.Add(_backendCombo);

            // ── Gemini Panel ──────────────────────────────────────────────────────────
            _geminiPanel = new StackPanel();
            _geminiPanel.Children.Add(CreateLabel("Google Gemini API Key (use semicolon ; for multiple):"));
            _geminiPanel.Children.Add(CreateLinkButton("🔗 Get Gemini API Key", "https://aistudio.google.com/app/apikey"));
            var geminiKey = CreateTextBox(SettingsManager.Current.GOOGLE_AI_KEY);
            geminiKey.TextChanged += (s, e) => SettingsManager.Current.GOOGLE_AI_KEY = geminiKey.Text.Trim();
            _geminiPanel.Children.Add(geminiKey);

            _geminiPanel.Children.Add(CreateLabel("Gemini Model:"));
            _geminiModelCombo = CreateEditableComboBox(new[] { "gemini-1.5-flash", "gemini-1.5-flash-8b", "gemini-1.5-pro", "gemini-2.0-flash-exp" }, SettingsManager.Current.GEMINI_MODEL);
            _geminiModelCombo.SelectionChanged += (s, e) => SettingsManager.Current.GEMINI_MODEL = _geminiModelCombo.Text;
            _geminiModelCombo.LostFocus += (s, e) => SettingsManager.Current.GEMINI_MODEL = _geminiModelCombo.Text;
            _geminiPanel.Children.Add(_geminiModelCombo);

            root.Children.Add(_geminiPanel);

            // ── OpenAI Panel ──────────────────────────────────────────────────────────
            _openAiPanel = new StackPanel();
            _openAiPanel.Children.Add(CreateLabel("OpenAI API Key:"));
            _openAiPanel.Children.Add(CreateLinkButton("🔗 Get OpenAI API Key", "https://platform.openai.com/api-keys"));
            var oaiKey = CreateTextBox(SettingsManager.Current.OPENAI_KEY);
            oaiKey.TextChanged += (s, e) => SettingsManager.Current.OPENAI_KEY = oaiKey.Text.Trim();
            _openAiPanel.Children.Add(oaiKey);

            _openAiPanel.Children.Add(CreateLabel("OpenAI Model:"));
            _openAiModelCombo = CreateEditableComboBox(new[] { "gpt-4o-mini", "gpt-4o", "gpt-4-turbo", "o1-preview" }, SettingsManager.Current.OPENAI_MODEL);
            _openAiModelCombo.SelectionChanged += (s, e) => SettingsManager.Current.OPENAI_MODEL = _openAiModelCombo.Text;
            _openAiModelCombo.LostFocus += (s, e) => SettingsManager.Current.OPENAI_MODEL = _openAiModelCombo.Text;
            _openAiPanel.Children.Add(_openAiModelCombo);

            _openAiPanel.Children.Add(CreateLabel("Base URL (default: https://api.openai.com/v1):"));
            var oaiBase = CreateTextBox(SettingsManager.Current.OPENAI_BASE_URL);
            oaiBase.TextChanged += (s, e) => SettingsManager.Current.OPENAI_BASE_URL = oaiBase.Text.Trim();
            _openAiPanel.Children.Add(oaiBase);
            root.Children.Add(_openAiPanel);

            // ── Anthropic Panel ───────────────────────────────────────────────────────
            _anthropicPanel = new StackPanel();
            _anthropicPanel.Children.Add(CreateLabel("Anthropic (Claude) API Key:"));
            _anthropicPanel.Children.Add(CreateLinkButton("🔗 Get Anthropic API Key", "https://console.anthropic.com/settings/keys"));
            var antKey = CreateTextBox(SettingsManager.Current.ANTHROPIC_KEY);
            antKey.TextChanged += (s, e) => SettingsManager.Current.ANTHROPIC_KEY = antKey.Text.Trim();
            _anthropicPanel.Children.Add(antKey);

            _anthropicPanel.Children.Add(CreateLabel("Anthropic Model:"));
            _anthropicModelCombo = CreateEditableComboBox(new[] { "claude-3-5-sonnet-20240620", "claude-3-opus-20240229", "claude-3-haiku-20240307" }, SettingsManager.Current.ANTHROPIC_MODEL);
            _anthropicModelCombo.SelectionChanged += (s, e) => SettingsManager.Current.ANTHROPIC_MODEL = _anthropicModelCombo.Text;
            _anthropicModelCombo.LostFocus += (s, e) => SettingsManager.Current.ANTHROPIC_MODEL = _anthropicModelCombo.Text;
            _anthropicPanel.Children.Add(_anthropicModelCombo);

            root.Children.Add(_anthropicPanel);

            // ── Groq Panel ────────────────────────────────────────────────────────────
            _groqPanel = new StackPanel();
            _groqPanel.Children.Add(CreateLabel("Groq API Key (Ultra-Fast):"));
            _groqPanel.Children.Add(CreateLinkButton("🔗 Get Groq API Key", "https://console.groq.com/keys"));
            var groqKey = CreateTextBox(SettingsManager.Current.GROQ_KEY);
            groqKey.TextChanged += (s, e) => SettingsManager.Current.GROQ_KEY = groqKey.Text.Trim();
            _groqPanel.Children.Add(groqKey);

            _groqPanel.Children.Add(CreateLabel("Groq Model:"));
            _groqModelCombo = CreateEditableComboBox(new[] { "llama-3.1-70b-versatile", "llama-3.1-8b-instant", "mixtral-8x7b-32768", "gemma2-9b-it" }, SettingsManager.Current.GROQ_MODEL);
            _groqModelCombo.SelectionChanged += (s, e) => SettingsManager.Current.GROQ_MODEL = _groqModelCombo.Text;
            _groqModelCombo.LostFocus += (s, e) => SettingsManager.Current.GROQ_MODEL = _groqModelCombo.Text;
            _groqPanel.Children.Add(_groqModelCombo);

            root.Children.Add(_groqPanel);

            // ── Perplexity Panel ──────────────────────────────────────────────────────
            _perplexityPanel = new StackPanel();
            _perplexityPanel.Children.Add(CreateLabel("Perplexity API Key (Online Search):"));
            _perplexityPanel.Children.Add(CreateLinkButton("🔗 Get Perplexity API Key", "https://www.perplexity.ai/settings/api"));
            var perpKey = CreateTextBox(SettingsManager.Current.PERPLEXITY_KEY);
            perpKey.TextChanged += (s, e) => SettingsManager.Current.PERPLEXITY_KEY = perpKey.Text.Trim();
            _perplexityPanel.Children.Add(perpKey);

            _perplexityPanel.Children.Add(CreateLabel("Perplexity Model:"));
            _perplexityModelCombo = CreateEditableComboBox(new[] { "llama-3-sonar-large-32k-online", "llama-3-sonar-small-32k-online", "mistral-7b-instruct" }, SettingsManager.Current.PERPLEXITY_MODEL);
            _perplexityModelCombo.SelectionChanged += (s, e) => SettingsManager.Current.PERPLEXITY_MODEL = _perplexityModelCombo.Text;
            _perplexityModelCombo.LostFocus += (s, e) => SettingsManager.Current.PERPLEXITY_MODEL = _perplexityModelCombo.Text;
            _perplexityPanel.Children.Add(_perplexityModelCombo);

            root.Children.Add(_perplexityPanel);

            // ── Mistral Panel ─────────────────────────────────────────────────────────
            _mistralPanel = new StackPanel();
            _mistralPanel.Children.Add(CreateLabel("Mistral AI API Key:"));
            _mistralPanel.Children.Add(CreateLinkButton("🔗 Get Mistral API Key", "https://console.mistral.ai/api-keys/"));
            var misKey = CreateTextBox(SettingsManager.Current.MISTRAL_KEY);
            misKey.TextChanged += (s, e) => SettingsManager.Current.MISTRAL_KEY = misKey.Text.Trim();
            _mistralPanel.Children.Add(misKey);

            _mistralPanel.Children.Add(CreateLabel("Mistral Model:"));
            _mistralModelCombo = CreateEditableComboBox(new[] { "mistral-large-latest", "mistral-medium-latest", "mistral-small-latest", "open-mixtral-8x22b" }, SettingsManager.Current.MISTRAL_MODEL);
            _mistralModelCombo.SelectionChanged += (s, e) => SettingsManager.Current.MISTRAL_MODEL = _mistralModelCombo.Text;
            _mistralModelCombo.LostFocus += (s, e) => SettingsManager.Current.MISTRAL_MODEL = _mistralModelCombo.Text;
            _mistralPanel.Children.Add(_mistralModelCombo);

            root.Children.Add(_mistralPanel);

            // ── OpenRouter Panel ──────────────────────────────────────────────────────
            _openRouterPanel = new StackPanel();
            _openRouterPanel.Children.Add(CreateLabel("OpenRouter API Key (Unified):"));
            _openRouterPanel.Children.Add(CreateLinkButton("🔗 Get OpenRouter API Key", "https://openrouter.ai/settings/keys"));
            var orKey = CreateTextBox(SettingsManager.Current.OPENROUTER_KEY);
            orKey.TextChanged += (s, e) => SettingsManager.Current.OPENROUTER_KEY = orKey.Text.Trim();
            _openRouterPanel.Children.Add(orKey);

            _openRouterPanel.Children.Add(CreateLabel("OpenRouter Model:"));
            _openRouterModelCombo = CreateEditableComboBox(new[] { "anthropic/claude-3.5-sonnet", "google/gemini-pro-1.5", "meta-llama/llama-3.1-405b", "mistralai/mistral-large-2407", "nousresearch/hermes-3-llama-3.1-405b", "openchat/openchat-7b" }, SettingsManager.Current.OPENROUTER_MODEL);
            _openRouterModelCombo.SelectionChanged += (s, e) => SettingsManager.Current.OPENROUTER_MODEL = _openRouterModelCombo.Text;
            _openRouterModelCombo.LostFocus += (s, e) => SettingsManager.Current.OPENROUTER_MODEL = _openRouterModelCombo.Text;
            _openRouterPanel.Children.Add(_openRouterModelCombo);

            root.Children.Add(_openRouterPanel);

            // ── Custom Processor Panel ────────────────────────────────────────────────
            root.Children.Add(CreateHeader("⚙️ Custom Data Processor (Experimental)"));

            var processorToggle = new CheckBox {
                Content = "Enable External Data Processor (@proc)",
                IsChecked = SettingsManager.Current.ENABLE_CUSTOM_PROCESSOR,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 6)
            };
            processorToggle.SetResourceReference(CheckBox.ForegroundProperty, "TextPrimaryBrush");
            processorToggle.Checked += (s, e) => { SettingsManager.Current.ENABLE_CUSTOM_PROCESSOR = true; SettingsManager.Save(); };
            processorToggle.Unchecked += (s, e) => { SettingsManager.Current.ENABLE_CUSTOM_PROCESSOR = false; SettingsManager.Save(); };
            root.Children.Add(processorToggle);

            root.Children.Add(CreateLabel("Processor Path (.exe / .bat / .ps1):"));
            var procPathBox = CreateTextBox(SettingsManager.Current.CUSTOM_DATA_PROCESSOR_PATH);
            procPathBox.TextChanged += (s, e) => SettingsManager.Current.CUSTOM_DATA_PROCESSOR_PATH = procPathBox.Text.Trim();
            root.Children.Add(procPathBox);

            var browseProcBtn = CreateButton("📂 Browse for Processor...");
            browseProcBtn.Click += (s, e) => {
                var dlg = new Microsoft.Win32.OpenFileDialog();
                if (dlg.ShowDialog() == true) procPathBox.Text = dlg.FileName;
            };
            root.Children.Add(browseProcBtn);

            // ── Ollama & Local LLM Panel ──────────────────────────────────────────────
            _ollamaPanel = new StackPanel();
            _ollamaPanel.Children.Add(CreateLabel("Ollama Endpoint (default: http://localhost:11434):"));
            var ollamaEndpoint = CreateTextBox(SettingsManager.Current.OLLAMA_ENDPOINT);
            ollamaEndpoint.TextChanged += (s, e) => SettingsManager.Current.OLLAMA_ENDPOINT = ollamaEndpoint.Text.Trim();
            _ollamaPanel.Children.Add(ollamaEndpoint);
            _ollamaPanel.Children.Add(CreateLabel("Model (e.g. llama3, mistral, deepseek-r1):"));
            var ollamaModel = CreateTextBox(SettingsManager.Current.OLLAMA_MODEL);
            ollamaModel.TextChanged += (s, e) => SettingsManager.Current.OLLAMA_MODEL = ollamaModel.Text.Trim();
            _ollamaPanel.Children.Add(ollamaModel);

            var detectBtn = CreateButton("🔍 Auto-Detect Installed Local Models");
            detectBtn.Click += async (s, e) =>
            {
                detectBtn.Content = "⏳ Detecting...";
                var models = await LlmRouter.GetOllamaModelsAsync();
                if (models.Count > 0)
                {
                    ollamaModel.Text = models[0];
                    detectBtn.Content = $"✅ Found ({models.Count}): {string.Join(", ", models)}";
                }
                else
                {
                    detectBtn.Content = "⚠️ No active local models found. Use 1-Click Installers below!";
                }
            };
            _ollamaPanel.Children.Add(detectBtn);

            // ── 1-Click LLM Installers ────────────────────────────────────────────────
            _ollamaPanel.Children.Add(CreateHeader("🛠️ 1-Click Local LLM & Engine Installers"));

            var installOllamaBtn = CreateButton("📥 Install Ollama Engine (winget / web)");
            installOllamaBtn.Click += (s, e) => InstallTool("winget install Ollama.Ollama", "https://ollama.com/download");
            _ollamaPanel.Children.Add(installOllamaBtn);

            var modelGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 4, 0, 4) };

            var pullLlamaBtn = CreateButton("🦙 Pull Llama 3.2 (3B)");
            pullLlamaBtn.Click += (s, e) => PullOllamaModel("llama3.2");
            modelGrid.Children.Add(pullLlamaBtn);

            var pullDeepseekBtn = CreateButton("🧠 Pull DeepSeek R1");
            pullDeepseekBtn.Click += (s, e) => PullOllamaModel("deepseek-r1:7b");
            modelGrid.Children.Add(pullDeepseekBtn);

            var pullMistralBtn = CreateButton("⚡ Pull Mistral (7B)");
            pullMistralBtn.Click += (s, e) => PullOllamaModel("mistral");
            modelGrid.Children.Add(pullMistralBtn);

            var pullQwenBtn = CreateButton("💻 Pull Qwen 2.5 Coder");
            pullQwenBtn.Click += (s, e) => PullOllamaModel("qwen2.5-coder");
            modelGrid.Children.Add(pullQwenBtn);

            var pullGemmaBtn = CreateButton("🔬 Pull Gemma 2 (2B)");
            pullGemmaBtn.Click += (s, e) => PullOllamaModel("gemma2:2b");
            modelGrid.Children.Add(pullGemmaBtn);

            var pullPhiBtn = CreateButton("📐 Pull Phi-3 Mini");
            pullPhiBtn.Click += (s, e) => PullOllamaModel("phi3");
            modelGrid.Children.Add(pullPhiBtn);

            _ollamaPanel.Children.Add(modelGrid);

            // ── Other Local LLM Tools ──────────────────────────────────────────
            _ollamaPanel.Children.Add(CreateHeader("💻 Alternative Local LLM Apps"));

            var appsGrid = new UniformGrid { Columns = 3, Margin = new Thickness(0, 4, 0, 4) };

            var installLmStudioBtn = CreateButton("💻 LM Studio");
            installLmStudioBtn.Click += (s, e) => InstallTool("winget install ElementLabs.LMStudio", "https://lmstudio.ai");
            appsGrid.Children.Add(installLmStudioBtn);

            var installJanBtn = CreateButton("🤖 Jan.ai");
            installJanBtn.Click += (s, e) => InstallTool("winget install Jan.Jan", "https://jan.ai");
            appsGrid.Children.Add(installJanBtn);

            var installGpt4AllBtn = CreateButton("🔮 GPT4All");
            installGpt4AllBtn.Click += (s, e) => InstallTool("winget install Nomic.GPT4All", "https://gpt4all.io");
            appsGrid.Children.Add(installGpt4AllBtn);

            _ollamaPanel.Children.Add(appsGrid);
            root.Children.Add(_ollamaPanel);

            // ── Custom Panel ──────────────────────────────────────────────────────────
            _customPanel = new StackPanel();
            _customPanel.Children.Add(CreateLabel("Custom Endpoint URL (OpenAI-compatible /chat/completions):"));
            var customUrl = CreateTextBox(SettingsManager.Current.CUSTOM_LLM_ENDPOINT);
            customUrl.TextChanged += (s, e) => SettingsManager.Current.CUSTOM_LLM_ENDPOINT = customUrl.Text.Trim();
            _customPanel.Children.Add(customUrl);
            _customPanel.Children.Add(CreateLabel("API Key (optional):"));
            var customKey = CreateTextBox(SettingsManager.Current.CUSTOM_LLM_KEY);
            customKey.TextChanged += (s, e) => SettingsManager.Current.CUSTOM_LLM_KEY = customKey.Text.Trim();
            _customPanel.Children.Add(customKey);
            _customPanel.Children.Add(CreateLabel("Model name:"));
            var customModel = CreateTextBox(SettingsManager.Current.CUSTOM_LLM_MODEL);
            customModel.TextChanged += (s, e) => SettingsManager.Current.CUSTOM_LLM_MODEL = customModel.Text.Trim();
            _customPanel.Children.Add(customModel);
            root.Children.Add(_customPanel);

            // ── P2P Panel ─────────────────────────────────────────────────────────────
            _p2pPanel = new StackPanel();
            _p2pPanel.Children.Add(CreateHeader("🌐 P2P Peer Compute Nodes"));

            var serverToggle = new CheckBox
            {
                Content = "📡 Enable P2P Server on This PC (let peers offload to me)",
                IsChecked = SettingsManager.Current.P2P_SERVER_ENABLED,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };
            serverToggle.SetResourceReference(CheckBox.ForegroundProperty, "TextPrimaryBrush");
            serverToggle.Checked += (s, e) => { SettingsManager.Current.P2P_SERVER_ENABLED = true; SettingsManager.Save(); };
            serverToggle.Unchecked += (s, e) => { SettingsManager.Current.P2P_SERVER_ENABLED = false; SettingsManager.Save(); };
            _p2pPanel.Children.Add(serverToggle);

            _p2pPanel.Children.Add(CreateLabel("Shared Secret (optional, protects /p2p/ask from strangers):"));
            var secretBox = CreateTextBox(SettingsManager.Current.P2P_SERVER_SECRET);
            secretBox.TextChanged += (s, e) => { SettingsManager.Current.P2P_SERVER_SECRET = secretBox.Text.Trim(); SettingsManager.Save(); };
            _p2pPanel.Children.Add(secretBox);

            _p2pPanel.Children.Add(CreateHeader("🖥️ Registered Peer PCs"));
            _peerListStack = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
            _p2pPanel.Children.Add(_peerListStack);
            RefreshPeerList();

            var addRow = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var peerUrlBox = CreateTextBox("");
            Grid.SetColumn(peerUrlBox, 0);
            addRow.Children.Add(peerUrlBox);
            var peerSecretBox = CreateTextBox("");
            peerSecretBox.Width = 100;
            peerSecretBox.Margin = new Thickness(6, 0, 0, 0);
            Grid.SetColumn(peerSecretBox, 1);
            addRow.Children.Add(peerSecretBox);
            _p2pPanel.Children.Add(addRow);

            var addBtn = CreateButton("➕ Add Peer PC");
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

            var probeBtn = CreateButton("📡 Probe All Peers");
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
            root.Children.Add(CreateHeader(""));
            var testBtn = CreateButton("⚡ Test Selected Backend");
            testBtn.Margin = new Thickness(0, 8, 0, 6);
            testBtn.Click += async (s, e) =>
            {
                string sel = (_backendCombo.SelectedItem as string) ?? "Gemini";
                SettingsManager.Current.LLM_BACKEND = sel;
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

            var saveBtn = CreateButton("💾 Save & Close");
            saveBtn.Margin = new Thickness(0, 12, 0, 0);
            saveBtn.Click += (s, e) =>
            {
                SettingsManager.Current.LLM_BACKEND = (_backendCombo.SelectedItem as string) ?? "Gemini";
                SettingsManager.Save();
                TextOverlay.Show($"✅ LLM Backend set to: {SettingsManager.Current.LLM_BACKEND}", 2500);
                this.FadeOutAndClose();
            };
            root.Children.Add(saveBtn);

            this.UserContent = scroll;

            // Initialize selection and panels AFTER everything is created
            _backendCombo.SelectedItem = SettingsManager.Current.LLM_BACKEND;
            UpdatePanelVisibility();
        }

        private static TextBlock CreateHeader(string title)
        {
            var header = new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 8, 0, 4)
            };
            header.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            return header;
        }

        private static TextBlock CreateLabel(string text)
        {
            var lbl = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 2)
            };
            lbl.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            return lbl;
        }

        private static TextBox CreateTextBox(string initialText)
        {
            var tb = new TextBox
            {
                Text = initialText ?? "",
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 12
            };
            return tb;
        }

        private static Button CreateButton(string content)
        {
            var btn = new Button
            {
                Content = content,
                Margin = new Thickness(0, 2, 0, 2),
                Padding = new Thickness(8, 5, 8, 5),
                FontSize = 12,
                Cursor = Cursors.Hand
            };
            return btn;
        }

        private static Button CreateLinkButton(string content, string url)
        {
            var btn = new Button
            {
                Content = content,
                Margin = new Thickness(0, 2, 0, 8),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = Brushes.DimGray
            };
            btn.SetResourceReference(Button.ForegroundProperty, "AccentBrush");
            btn.Click += (s, e) => {
                try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); } catch { }
            };
            return btn;
        }

        private static ComboBox CreateEditableComboBox(string[] items, string current)
        {
            var combo = new ComboBox
            {
                IsEditable = true,
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 12
            };
            foreach (var item in items) combo.Items.Add(item);
            combo.Text = current;
            return combo;
        }

        private static void InstallTool(string wingetCommand, string fallbackUrl)
        {
            try
            {
                TextOverlay.Show($"📥 Launching Installer: {wingetCommand}", 3500);
                Process.Start("cmd.exe", $"/c start cmd /k \"echo Installing LLM Tool via Winget... & {wingetCommand} || start {fallbackUrl}\"");
            }
            catch
            {
                Process.Start(new ProcessStartInfo { FileName = fallbackUrl, UseShellExecute = true });
            }
        }

        private static void PullOllamaModel(string modelName)
        {
            try
            {
                TextOverlay.Show($"📥 Pulling Ollama Model '{modelName}'...", 4000);
                Process.Start("cmd.exe", $"/c start cmd /k \"echo Pulling Ollama Model: {modelName}... & ollama pull {modelName}\"");
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Failed to pull model: {ex.Message}", 3000);
            }
        }

        private void UpdatePanelVisibility()
        {
            if (_backendCombo == null || _geminiPanel == null) return;

            string sel = (_backendCombo.SelectedItem as string) ?? "Gemini";
            if (_geminiPanel != null) _geminiPanel.Visibility = sel == "Gemini" ? Visibility.Visible : Visibility.Collapsed;
            if (_openAiPanel != null) _openAiPanel.Visibility = sel == "OpenAI" ? Visibility.Visible : Visibility.Collapsed;
            if (_anthropicPanel != null) _anthropicPanel.Visibility = sel == "Anthropic" ? Visibility.Visible : Visibility.Collapsed;
            if (_groqPanel != null) _groqPanel.Visibility = sel == "Groq" ? Visibility.Visible : Visibility.Collapsed;
            if (_perplexityPanel != null) _perplexityPanel.Visibility = sel == "Perplexity" ? Visibility.Visible : Visibility.Collapsed;
            if (_mistralPanel != null) _mistralPanel.Visibility = sel == "Mistral" ? Visibility.Visible : Visibility.Collapsed;
            if (_openRouterPanel != null) _openRouterPanel.Visibility = sel == "OpenRouter" ? Visibility.Visible : Visibility.Collapsed;
            if (_ollamaPanel != null) _ollamaPanel.Visibility = sel == "Ollama" ? Visibility.Visible : Visibility.Collapsed;
            if (_customPanel != null) _customPanel.Visibility = sel == "Custom" ? Visibility.Visible : Visibility.Collapsed;
            if (_p2pPanel != null) _p2pPanel.Visibility = sel == "P2P" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshPeerList()
        {
            if (_peerListStack == null) return;
            _peerListStack.Children.Clear();
            var peers = JarvisP2PClient.Peers;
            if (peers.Count == 0)
            {
                var empty = new TextBlock { Text = "No peer PCs added yet. Add a peer URL above.", FontSize = 11, FontStyle = FontStyles.Italic };
                empty.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                _peerListStack.Children.Add(empty);
                return;
            }

            foreach (var p in peers)
            {
                var card = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 6, 8, 6),
                    Margin = new Thickness(0, 0, 0, 4)
                };
                card.SetResourceReference(Border.BackgroundProperty, "CardBackgroundBrush");

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                string statusDot = p.IsOnline ? "🟢" : "🔴";
                var txt = new TextBlock
                {
                    Text = $"{statusDot} {p.Url}{(string.IsNullOrEmpty(p.PcName) ? "" : " (" + p.PcName + ")")} - {(p.IsOnline ? p.LatencyMs + "ms" : "Offline")}",
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                };
                txt.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                Grid.SetColumn(txt, 0);
                grid.Children.Add(txt);

                var delBtn = new Button
                {
                    Content = "❌",
                    Width = 24,
                    Height = 24,
                    Padding = new Thickness(0),
                    FontSize = 10,
                    Cursor = Cursors.Hand
                };
                delBtn.Click += (s, e) =>
                {
                    JarvisP2PClient.RemovePeer(p.Url);
                    RefreshPeerList();
                };
                Grid.SetColumn(delBtn, 1);
                grid.Children.Add(delBtn);

                card.Child = grid;
                _peerListStack.Children.Add(card);
            }
        }

        public static void ShowOverlay()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_instance == null || !_instance.IsLoaded)
                {
                    _instance = new LlmSettingsOverlay();
                }

                _instance.Show();
                _instance.Activate();
                _instance.BringToFront();
            });
        }
    }
}
