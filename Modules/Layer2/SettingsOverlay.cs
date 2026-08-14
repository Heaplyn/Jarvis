// Developer: heaplyn
// Date: 2026-08-13
// Summary: Custom Dark Glassmorphic Multi-Tab Master System Settings & Configuration Studio.
// Integrates General & Appearance, LLM Engine Studio, TTS & Custom Voice Studio, Voice AI & Speech Training, Offline Pre-Caching, and Custom Aliases without any WPF default white boxes.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        // Custom Glass Tab Switcher Controls
        private readonly List<Button> _tabButtons = new List<Button>();
        private readonly List<UIElement> _tabPanels = new List<UIElement>();
        private Grid _contentGrid = null!;

        // General Tab Controls
        private ComboBox _themeComboBox = null!;
        private ComboBox _searchEngineComboBox = null!;
        private CheckBox _startWithWinCheckBox = null!;
        private CheckBox _playSoundsCheckBox = null!;
        private CheckBox _autoHideCheckBox = null!;
        private CheckBox _alwaysOnTopCheckBox = null!;
        private Slider _opacitySlider = null!;
        private TextBox _googleKeyBox = null!;
        private TextBox _githubTokenBox = null!;
        private TextBox _downloadDirBox = null!;
        private TextBox _mobilePortBox = null!;

        // LLM Tab Controls
        private ComboBox _llmBackendCombo = null!;
        private StackPanel _geminiPanel = null!;
        private StackPanel _openAiPanel = null!;
        private StackPanel _ollamaPanel = null!;
        private StackPanel _customPanel = null!;
        private CheckBox _enableDualLlmCheckBox = null!;
        private ComboBox _dualLlmBackendCombo = null!;
        private ComboBox _dualLlmModelCombo = null!;

        // TTS & Custom Voice Controls
        private ComboBox _ttsVoiceCombo = null!;
        private Slider _ttsSpeedSlider = null!;
        private Slider _ttsVolumeSlider = null!;

        // Voice AI Controls
        private CheckBox _isJarvisEnabledCheckBox = null!;
        private Slider _minConfidenceSlider = null!;

        // Offline Pre-Caching Controls
        private TextBlock _offlineConnectionStatus = null!;
        private TextBlock _offlineVoskStatus = null!;
        private TextBlock _offlineTtsStatus = null!;
        private TextBlock _offlineProgressText = null!;

        // Aliases Tab Controls
        private StackPanel _aliasListStack = null!;
        private TextBox _newAliasKeyBox = null!;
        private TextBox _newAliasValueBox = null!;

        public static void OpenSettings()
        {
            ShowSettings();
        }

        public static void ShowSettings()
        {
            if (_instance == null || !_instance.IsLoaded || !_instance.IsVisible)
            {
                _instance = new SettingsOverlay();
                _instance.Show();
            }
            else
            {
                _instance.Activate();
                _instance.BringToFront();
                _instance.Focus();
            }
        }

        public static void ShowOverlay()
        {
            ShowSettings();
        }

        public SettingsOverlay()
            : base("⚙️ MASTER SETTINGS & CONFIGURATION STUDIO", width: 760, height: 700)
        {
            this.Closed += (s, e) => { _instance = null; };

            var workArea = SystemParameters.WorkArea;
            this.Left = (workArea.Width - this.Width) / 2;
            this.Top = (workArea.Height - this.Height) / 2;

            var mainGrid = new Grid { Margin = new Thickness(8) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                     // Tab Bar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                     // Action Bar

            // ── Glassmorphic Tab Bar ───────────────────────────────────────────────────
            var tabBarGrid = new UniformGrid
            {
                Columns = 6,
                Margin = new Thickness(0, 0, 0, 10)
            };

            string[] tabNames = new[] { "⚙️ General", "🤖 LLM", "🗣️ TTS", "🎙️ Voice AI", "📶 Offline", "🏷️ Aliases" };
            for (int i = 0; i < tabNames.Length; i++)
            {
                int tabIdx = i;
                var btn = new Button
                {
                    Content = tabNames[i],
                    Padding = new Thickness(4, 8, 4, 8),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(2, 0, 2, 0),
                    Cursor = Cursors.Hand
                };
                btn.Click += (s, e) => SelectTab(tabIdx);
                _tabButtons.Add(btn);
                tabBarGrid.Children.Add(btn);
            }
            Grid.SetRow(tabBarGrid, 0);
            mainGrid.Children.Add(tabBarGrid);

            // ── Content Container Grid ─────────────────────────────────────────────────
            _contentGrid = new Grid();
            Grid.SetRow(_contentGrid, 1);

            _tabPanels.Add(BuildGeneralTab());
            _tabPanels.Add(BuildLlmTab());
            _tabPanels.Add(BuildTtsTab());
            _tabPanels.Add(BuildVoiceAiTab());
            _tabPanels.Add(BuildOfflineTab());
            _tabPanels.Add(BuildAliasesTab());

            foreach (var panel in _tabPanels)
            {
                _contentGrid.Children.Add(panel);
            }
            mainGrid.Children.Add(_contentGrid);

            // Select initial tab
            SelectTab(0);

            // ── Action Bar: Save & Close ───────────────────────────────────────────────
            var actionBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var saveBtn = CreateButton("💾 Save All Settings");
            saveBtn.Padding = new Thickness(20, 8, 20, 8);
            saveBtn.FontWeight = FontWeights.Bold;
            saveBtn.Click += (s, e) => SaveAllSettings();
            actionBar.Children.Add(saveBtn);

            Grid.SetRow(actionBar, 2);
            mainGrid.Children.Add(actionBar);

            this.UserContent = mainGrid;
        }

        private void SelectTab(int index)
        {
            for (int i = 0; i < _tabPanels.Count; i++)
            {
                bool isSel = (i == index);
                _tabPanels[i].Visibility = isSel ? Visibility.Visible : Visibility.Collapsed;

                var btn = _tabButtons[i];
                if (isSel)
                {
                    btn.SetResourceReference(Button.BackgroundProperty, "AccentBrush");
                    btn.Foreground = Brushes.White;
                }
                else
                {
                    btn.SetResourceReference(Button.BackgroundProperty, "CardBackgroundBrush");
                    btn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
                }
            }
        }

        // ── TAB 1 BUILDER: General & UI ──────────────────────────────────────────────
        private UIElement BuildGeneralTab()
        {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root = new StackPanel { Margin = new Thickness(4) };
            scroll.Content = root;

            var settings = SettingsManager.Current;

            root.Children.Add(CreateHeader("🎨 Interface & Visual Theme"));

            _themeComboBox = new ComboBox { Margin = new Thickness(0, 0, 0, 8), FontSize = 12, Padding = new Thickness(6, 4, 6, 4) };
            foreach (var t in new[] { "purple", "dark", "cyberpunk", "emerald", "sunset", "ocean", "midnight", "rose" })
                _themeComboBox.Items.Add(t);
            _themeComboBox.SelectedItem = settings.Theme;
            _themeComboBox.SelectionChanged += (s, e) =>
            {
                if (_themeComboBox.SelectedItem is string th) ThemeManager.ApplyTheme(th);
            };
            root.Children.Add(_themeComboBox);

            root.Children.Add(CreateLabel("Search Engine:"));
            _searchEngineComboBox = new ComboBox { Margin = new Thickness(0, 0, 0, 8), FontSize = 12, Padding = new Thickness(6, 4, 6, 4) };
            foreach (var se in new[] { "Google", "DuckDuckGo", "Bing", "YouTube", "GitHub", "Wikipedia" })
                _searchEngineComboBox.Items.Add(se);
            _searchEngineComboBox.SelectedItem = settings.DefaultSearchEngine;
            root.Children.Add(_searchEngineComboBox);

            root.Children.Add(CreateLabel($"Window Opacity:"));
            _opacitySlider = new Slider { Minimum = 0.3, Maximum = 1.0, Value = settings.WindowOpacity, Margin = new Thickness(0, 2, 0, 8) };
            root.Children.Add(_opacitySlider);

            root.Children.Add(CreateHeader("⚙️ System Behavior"));

            _startWithWinCheckBox = CreateCheckBox("🚀 Launch automatically when Windows starts", settings.StartWithWindows);
            root.Children.Add(_startWithWinCheckBox);

            _playSoundsCheckBox = CreateCheckBox("🔊 Play sound effects on command execution", settings.PlaySounds);
            root.Children.Add(_playSoundsCheckBox);

            _autoHideCheckBox = CreateCheckBox("🙈 Auto-hide HUD after launching commands", settings.AutoHideOnExecute);
            root.Children.Add(_autoHideCheckBox);

            _alwaysOnTopCheckBox = CreateCheckBox("📌 Always Keep Launcher on Top", settings.AlwaysOnTop);
            root.Children.Add(_alwaysOnTopCheckBox);

            root.Children.Add(CreateHeader("🔑 API Credentials & Downloads"));

            root.Children.Add(CreateLabel("Google Gemini API Key:"));
            _googleKeyBox = CreateTextBox(settings.GoogleAIKey);
            root.Children.Add(_googleKeyBox);

            root.Children.Add(CreateLabel("GitHub Access Token:"));
            _githubTokenBox = CreateTextBox(settings.GithubToken);
            root.Children.Add(_githubTokenBox);

            root.Children.Add(CreateLabel("Default Downloads Folder:"));
            _downloadDirBox = CreateTextBox(settings.DownloadDirectory);
            root.Children.Add(_downloadDirBox);

            root.Children.Add(CreateLabel("Mobile Bridge Server Port (default 8080):"));
            _mobilePortBox = CreateTextBox(settings.MobilePort.ToString());
            root.Children.Add(_mobilePortBox);

            return scroll;
        }

        // ── TAB 2 BUILDER: LLM Engines ───────────────────────────────────────────────
        private UIElement BuildLlmTab()
        {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root = new StackPanel { Margin = new Thickness(4) };
            scroll.Content = root;

            var settings = SettingsManager.Current;

            root.Children.Add(CreateHeader("🤖 Active LLM Backend Engine"));

            _llmBackendCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 10), FontSize = 12, Padding = new Thickness(6, 4, 6, 4) };
            foreach (var b in new[] { "Gemini", "OpenAI", "Ollama", "Custom", "P2P" }) _llmBackendCombo.Items.Add(b);
            _llmBackendCombo.SelectedItem = settings.LlmBackend;
            _llmBackendCombo.SelectionChanged += (s, e) => UpdateLlmPanels();
            root.Children.Add(_llmBackendCombo);

            // Gemini Panel
            _geminiPanel = new StackPanel();
            _geminiPanel.Children.Add(CreateLabel("Gemini API Key is configured under General Tab."));
            root.Children.Add(_geminiPanel);

            // OpenAI Panel
            _openAiPanel = new StackPanel();
            _openAiPanel.Children.Add(CreateLabel("OpenAI API Key (or LM Studio key):"));
            var oaiKey = CreateTextBox(settings.OpenAIKey);
            oaiKey.TextChanged += (s, e) => settings.OpenAIKey = oaiKey.Text.Trim();
            _openAiPanel.Children.Add(oaiKey);
            _openAiPanel.Children.Add(CreateLabel("Base URL (default https://api.openai.com/v1):"));
            var oaiBase = CreateTextBox(settings.OpenAIBaseUrl);
            oaiBase.TextChanged += (s, e) => settings.OpenAIBaseUrl = oaiBase.Text.Trim();
            _openAiPanel.Children.Add(oaiBase);
            root.Children.Add(_openAiPanel);

            // Ollama Panel
            _ollamaPanel = new StackPanel();
            _ollamaPanel.Children.Add(CreateLabel("Ollama Endpoint (default http://localhost:11434):"));
            var ollamaUrl = CreateTextBox(settings.OllamaEndpoint);
            ollamaUrl.TextChanged += (s, e) => settings.OllamaEndpoint = ollamaUrl.Text.Trim();
            _ollamaPanel.Children.Add(ollamaUrl);
            _ollamaPanel.Children.Add(CreateLabel("Active Ollama Model (e.g. llama3.2, deepseek-r1):"));
            var ollamaModel = CreateTextBox(settings.OllamaModel);
            ollamaModel.TextChanged += (s, e) => settings.OllamaModel = ollamaModel.Text.Trim();
            _ollamaPanel.Children.Add(ollamaModel);

            var detectBtn = CreateButton("🔍 Auto-Detect Installed Ollama Models");
            detectBtn.Click += async (s, e) =>
            {
                var models = await LlmRouter.GetOllamaModelsAsync();
                if (models.Count > 0) ollamaModel.Text = models[0];
                TextOverlay.Show(models.Count > 0 ? $"✅ Found ({models.Count}): {string.Join(", ", models)}" : "⚠️ No local models found.", 3000);
            };
            _ollamaPanel.Children.Add(detectBtn);

            root.Children.Add(_ollamaPanel);

            // Custom Panel
            _customPanel = new StackPanel();
            _customPanel.Children.Add(CreateLabel("Custom Endpoint URL:"));
            var customUrl = CreateTextBox(settings.CustomLlmEndpoint);
            customUrl.TextChanged += (s, e) => settings.CustomLlmEndpoint = customUrl.Text.Trim();
            _customPanel.Children.Add(customUrl);
            root.Children.Add(_customPanel);

            // 1-Click Installers & Model Pullers
            root.Children.Add(CreateHeader("🛠️ 1-Click Local LLM Installers & Pullers"));
            var llmGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 4, 0, 8) };

            var instOllama = CreateButton("📥 Install Ollama Engine");
            instOllama.Click += (s, e) => Process.Start("cmd.exe", "/c start cmd /k \"winget install Ollama.Ollama || start https://ollama.com/download\"");
            llmGrid.Children.Add(instOllama);

            var pullDeepseek = CreateButton("🧠 Pull DeepSeek R1 (7B)");
            pullDeepseek.Click += (s, e) => Process.Start("cmd.exe", "/c start cmd /k \"ollama pull deepseek-r1:7b\"");
            llmGrid.Children.Add(pullDeepseek);

            var pullLlama = CreateButton("🦙 Pull Llama 3.2 (3B)");
            pullLlama.Click += (s, e) => Process.Start("cmd.exe", "/c start cmd /k \"ollama pull llama3.2\"");
            llmGrid.Children.Add(pullLlama);

            var openHf = CreateButton("🤗 Hugging Face Grabber");
            openHf.Click += (s, e) => HuggingFaceOverlay.ShowOverlay();
            llmGrid.Children.Add(openHf);

            root.Children.Add(llmGrid);

            // Dual-LLM Co-Pilot Processor Section
            root.Children.Add(CreateHeader("⚡ Dual-LLM Co-Pilot Processor (Optional)"));

            _enableDualLlmCheckBox = CreateCheckBox("⚡ Enable Parallel Dual-LLM Co-Pilot Processing (Default Disabled)", settings.EnableDualLlmCopilot);
            root.Children.Add(_enableDualLlmCheckBox);

            root.Children.Add(CreateLabel("Co-Pilot Engine Backend:"));
            _dualLlmBackendCombo = new ComboBox { Margin = new Thickness(0, 2, 0, 6), FontSize = 12, Padding = new Thickness(6, 4, 6, 4) };
            foreach (var b in new[] { "Ollama", "Gemini", "OpenAI" }) _dualLlmBackendCombo.Items.Add(b);
            _dualLlmBackendCombo.SelectedItem = settings.DualLlmBackend;
            root.Children.Add(_dualLlmBackendCombo);

            root.Children.Add(CreateLabel("Recommended Co-Pilot Models:"));
            _dualLlmModelCombo = new ComboBox { Margin = new Thickness(0, 2, 0, 8), FontSize = 12, Padding = new Thickness(6, 4, 6, 4) };
            foreach (var m in DualLlmCopilot.RecommendedModels) _dualLlmModelCombo.Items.Add(m);

            string currentCopilotModel = settings.DualLlmModel;
            int matchIdx = DualLlmCopilot.RecommendedModels.FindIndex(m => m.StartsWith(currentCopilotModel, StringComparison.OrdinalIgnoreCase));
            if (matchIdx >= 0) _dualLlmModelCombo.SelectedIndex = matchIdx;
            else _dualLlmModelCombo.SelectedIndex = 0;
            root.Children.Add(_dualLlmModelCombo);

            UpdateLlmPanels();

            return scroll;
        }

        private void UpdateLlmPanels()
        {
            string sel = (_llmBackendCombo.SelectedItem as string) ?? "Gemini";
            _geminiPanel.Visibility = sel == "Gemini" ? Visibility.Visible : Visibility.Collapsed;
            _openAiPanel.Visibility = sel == "OpenAI" ? Visibility.Visible : Visibility.Collapsed;
            _ollamaPanel.Visibility = sel == "Ollama" ? Visibility.Visible : Visibility.Collapsed;
            _customPanel.Visibility = sel == "Custom" ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── TAB 3 BUILDER: TTS & Voice Studio ────────────────────────────────────────
        private UIElement BuildTtsTab()
        {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root = new StackPanel { Margin = new Thickness(4) };
            scroll.Content = root;

            root.Children.Add(CreateHeader("🔊 Windows Installed System TTS Voices"));

            _ttsVoiceCombo = new ComboBox { Margin = new Thickness(0, 2, 0, 8), FontSize = 12, Padding = new Thickness(6, 4, 6, 4) };
            var voices = TtsManager.GetInstalledVoices();
            foreach (var v in voices) _ttsVoiceCombo.Items.Add(v);

            string currentVoice = SettingsManager.Current.SelectedTtsVoice;
            if (!string.IsNullOrEmpty(currentVoice) && _ttsVoiceCombo.Items.Contains(currentVoice))
                _ttsVoiceCombo.SelectedItem = currentVoice;
            else if (_ttsVoiceCombo.Items.Count > 0)
                _ttsVoiceCombo.SelectedIndex = 0;

            _ttsVoiceCombo.SelectionChanged += (s, e) =>
            {
                if (_ttsVoiceCombo.SelectedItem is string sel) TtsManager.SetVoice(sel);
            };
            root.Children.Add(_ttsVoiceCombo);

            root.Children.Add(CreateLabel("Speech Speed (-10 Slow ... +10 Fast):"));
            _ttsSpeedSlider = new Slider { Minimum = -10, Maximum = 10, Value = SettingsManager.Current.TtsSpeechRate, SmallChange = 1, Margin = new Thickness(0, 2, 0, 6) };
            _ttsSpeedSlider.ValueChanged += (s, e) => TtsManager.SetRate((int)_ttsSpeedSlider.Value);
            root.Children.Add(_ttsSpeedSlider);

            root.Children.Add(CreateLabel("Speech Volume (0 Quiet ... 100 Loud):"));
            _ttsVolumeSlider = new Slider { Minimum = 0, Maximum = 100, Value = SettingsManager.Current.TtsSpeechVolume, SmallChange = 5, Margin = new Thickness(0, 2, 0, 8) };
            _ttsVolumeSlider.ValueChanged += (s, e) => TtsManager.SetVolume((int)_ttsVolumeSlider.Value);
            root.Children.Add(_ttsVolumeSlider);

            var testBtn = CreateButton("⚡ Test Selected Voice & AI Speech");
            testBtn.Height = 32;
            testBtn.FontWeight = FontWeights.Bold;
            testBtn.Click += (s, e) =>
            {
                string sel = (_ttsVoiceCombo.SelectedItem as string) ?? "Microsoft Voice";
                TtsManager.Speak($"Hello! I am Jarvis, speaking with your chosen voice: {sel}.", isShortSpeech: false);
            };
            root.Children.Add(testBtn);

            root.Children.Add(CreateHeader("🌐 GitHub Custom MP3 Voice Samples (yaph/tts-samples)"));
            var openLibraryBtn = CreateButton("🎵 Open GitHub Custom Voice Library Studio");
            openLibraryBtn.Click += (s, e) => TtsVoiceLibraryOverlay.ShowOverlay();
            root.Children.Add(openLibraryBtn);

            return scroll;
        }

        // ── TAB 4 BUILDER: Voice AI & Speech ─────────────────────────────────────────
        private UIElement BuildVoiceAiTab()
        {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root = new StackPanel { Margin = new Thickness(4) };
            scroll.Content = root;

            var settings = SettingsManager.Current;

            root.Children.Add(CreateHeader("🎙️ Master Voice Activation"));

            _isJarvisEnabledCheckBox = CreateCheckBox("🎙️ Enable 100% Offline Voice Wake-Word (\"Hey Jarvis\")", settings.IsJarvisEnabled);
            _isJarvisEnabledCheckBox.Checked += (s, e) => settings.IsJarvisEnabled = true;
            _isJarvisEnabledCheckBox.Unchecked += (s, e) => settings.IsJarvisEnabled = false;
            root.Children.Add(_isJarvisEnabledCheckBox);

            root.Children.Add(CreateHeader("🎛️ Acoustic Microphone Sensitivity"));

            root.Children.Add(CreateLabel("Minimum Speech Confidence Gate (0% Permissive ... 100% Strict):"));
            _minConfidenceSlider = new Slider { Minimum = 0.05, Maximum = 0.95, Value = settings.MinVoiceConfidence, SmallChange = 0.05, Margin = new Thickness(0, 2, 0, 8) };
            _minConfidenceSlider.ValueChanged += (s, e) => settings.MinVoiceConfidence = _minConfidenceSlider.Value;
            root.Children.Add(_minConfidenceSlider);

            root.Children.Add(CreateHeader("🎙️ Acoustic Voice Training & Calibration"));

            var trainBtn = CreateButton("🎙️ Open Voice AI Training & Memo Studio");
            trainBtn.Click += (s, e) => VoiceStudioOverlay.ShowOverlay();
            root.Children.Add(trainBtn);

            var voskBtn = CreateButton("📥 Download Official Vosk Offline Speech Model (~40MB)");
            voskBtn.Click += async (s, e) => await VoskEngine.EnsureModelDownloadedAsync(showToast: true);
            root.Children.Add(voskBtn);

            return scroll;
        }

        // ── TAB 5 BUILDER: Offline Pre-Caching ───────────────────────────────────────
        private UIElement BuildOfflineTab()
        {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root = new StackPanel { Margin = new Thickness(4) };
            scroll.Content = root;

            root.Children.Add(CreateHeader("📶 Offline Mode & Wi-Fi Pre-Caching"));

            _offlineConnectionStatus = new TextBlock { FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 2, 0, 4) };
            _offlineConnectionStatus.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            root.Children.Add(_offlineConnectionStatus);

            _offlineVoskStatus = new TextBlock { FontSize = 12, Margin = new Thickness(0, 2, 0, 4) };
            _offlineVoskStatus.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            root.Children.Add(_offlineVoskStatus);

            _offlineTtsStatus = new TextBlock { FontSize = 12, Margin = new Thickness(0, 2, 0, 8) };
            _offlineTtsStatus.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            root.Children.Add(_offlineTtsStatus);

            var preCacheBtn = CreateButton("📶 Pre-Cache All Features For Offline Use");
            preCacheBtn.Height = 34;
            preCacheBtn.FontWeight = FontWeights.Bold;
            preCacheBtn.Click += async (s, e) =>
            {
                preCacheBtn.IsEnabled = false;
                await OfflineCacheManager.PreCacheAllForOfflineAsync(status =>
                {
                    Application.Current.Dispatcher.Invoke(() => _offlineProgressText.Text = status);
                });
                RefreshOfflineStatus();
                preCacheBtn.IsEnabled = true;
            };
            root.Children.Add(preCacheBtn);

            _offlineProgressText = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
            _offlineProgressText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            root.Children.Add(_offlineProgressText);

            RefreshOfflineStatus();
            return scroll;
        }

        private void RefreshOfflineStatus()
        {
            bool online = OfflineCacheManager.IsInternetAvailable();
            _offlineConnectionStatus.Text = online ? "📡 Network: 🟢 Connected (Wi-Fi / Ethernet)" : "📡 Network: 🔴 Offline Mode Active";

            bool voskReady = Directory.Exists(VoskEngine.ModelDirectory);
            _offlineVoskStatus.Text = voskReady ? "🎙️ Vosk Neural Model: ✅ Ready Offline" : "🎙️ Vosk Neural Model: ⚠️ Not Downloaded";

            string voiceDir = TtsSampleDownloader.VoiceDirectory;
            int cachedVoices = Directory.Exists(voiceDir) ? Directory.GetFiles(voiceDir, "*.mp3").Length : 0;
            _offlineTtsStatus.Text = cachedVoices > 0 ? $"🎵 GitHub TTS Voices: ✅ {cachedVoices} cached offline" : "🎵 GitHub TTS Voices: ⚠️ Not Cached";
        }

        // ── TAB 6 BUILDER: Custom Aliases & Commands ─────────────────────────────────
        private UIElement BuildAliasesTab()
        {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root = new StackPanel { Margin = new Thickness(4) };
            scroll.Content = root;

            root.Children.Add(CreateHeader("🏷️ Custom Alias & Command Studio"));

            var info = new TextBlock
            {
                Text = "Map short keyword aliases to full commands or chained multi-action pipelines (e.g., 'g' -> 'open google', 'work' -> 'open chrome | open vscode | volume 30').",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            info.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            root.Children.Add(info);

            // Add Alias Inputs
            var addGrid = new Grid { Margin = new Thickness(0, 2, 0, 8) };
            addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _newAliasKeyBox = CreateTextBox("e.g. g");
            Grid.SetColumn(_newAliasKeyBox, 0);
            addGrid.Children.Add(_newAliasKeyBox);

            _newAliasValueBox = CreateTextBox("e.g. open google");
            _newAliasValueBox.Margin = new Thickness(6, 0, 0, 0);
            Grid.SetColumn(_newAliasValueBox, 1);
            addGrid.Children.Add(_newAliasValueBox);

            var addBtn = CreateButton("➕ Add Alias");
            addBtn.Margin = new Thickness(6, 0, 0, 0);
            addBtn.Click += (s, e) =>
            {
                string k = _newAliasKeyBox.Text.Trim().ToLower();
                string v = _newAliasValueBox.Text.Trim();
                if (string.IsNullOrEmpty(k) || string.IsNullOrEmpty(v)) return;

                SettingsManager.Current.Aliases[k] = v;
                SettingsManager.Save();
                _newAliasKeyBox.Text = "";
                _newAliasValueBox.Text = "";
                RefreshAliasList();
                TextOverlay.Show($"✅ Added Alias: '{k}' ➔ '{v}'", 2500);
            };
            Grid.SetColumn(addBtn, 2);
            addGrid.Children.Add(addBtn);

            root.Children.Add(addGrid);

            // Preset Alias Packs
            root.Children.Add(CreateHeader("⚡ 1-Click Preset Alias Packs"));
            var presetGrid = new UniformGrid { Columns = 3, Margin = new Thickness(0, 4, 0, 8) };

            var packProductivity = CreateButton("🚀 Productivity Pack");
            packProductivity.Click += (s, e) =>
            {
                var dict = SettingsManager.Current.Aliases;
                dict["g"] = "open google";
                dict["yt"] = "open youtube";
                dict["gpt"] = "open chatgpt";
                dict["code"] = "open vscode";
                dict["note"] = "stickynotes";
                SettingsManager.Save();
                RefreshAliasList();
                TextOverlay.Show("✅ Imported Productivity Alias Pack!", 2500);
            };
            presetGrid.Children.Add(packProductivity);

            var packSystem = CreateButton("💻 System & Power Pack");
            packSystem.Click += (s, e) =>
            {
                var dict = SettingsManager.Current.Aliases;
                dict["re"] = "restart";
                dict["off"] = "shutdown";
                dict["lock"] = "lock pc";
                dict["vol50"] = "volume 50";
                dict["mute"] = "mute volume";
                SettingsManager.Save();
                RefreshAliasList();
                TextOverlay.Show("✅ Imported System & Power Alias Pack!", 2500);
            };
            presetGrid.Children.Add(packSystem);

            var packAi = CreateButton("🤖 AI & Voice Pack");
            packAi.Click += (s, e) =>
            {
                var dict = SettingsManager.Current.Aliases;
                dict["deep"] = "deepseek";
                dict["llama"] = "llama3";
                dict["voices"] = "ttsvoices";
                dict["cache"] = "precache";
                dict["hf"] = "huggingface";
                SettingsManager.Save();
                RefreshAliasList();
                TextOverlay.Show("✅ Imported AI & Voice Alias Pack!", 2500);
            };
            presetGrid.Children.Add(packAi);

            root.Children.Add(presetGrid);

            // Registered Aliases List
            root.Children.Add(CreateHeader("📋 Active Configured Aliases"));
            _aliasListStack = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
            root.Children.Add(_aliasListStack);

            RefreshAliasList();
            return scroll;
        }

        private void RefreshAliasList()
        {
            if (_aliasListStack == null) return;
            _aliasListStack.Children.Clear();

            var aliases = SettingsManager.Current.Aliases;
            if (aliases.Count == 0)
            {
                var empty = new TextBlock { Text = "No custom aliases created yet. Add one above or import a 1-click pack!", FontSize = 11, FontStyle = FontStyles.Italic };
                empty.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                _aliasListStack.Children.Add(empty);
                return;
            }

            foreach (var kvp in aliases.ToList())
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

                var txt = new TextBlock
                {
                    Text = $"🏷️ '{kvp.Key}' ➔ '{kvp.Value}'",
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
                string targetKey = kvp.Key;
                delBtn.Click += (s, e) =>
                {
                    SettingsManager.Current.Aliases.Remove(targetKey);
                    SettingsManager.Save();
                    RefreshAliasList();
                };
                Grid.SetColumn(delBtn, 1);
                grid.Children.Add(delBtn);

                card.Child = grid;
                _aliasListStack.Children.Add(card);
            }
        }

        private void SaveAllSettings()
        {
            var settings = SettingsManager.Current;

            if (_themeComboBox.SelectedItem is string th) settings.Theme = th;
            if (_searchEngineComboBox.SelectedItem is string se) settings.DefaultSearchEngine = se;

            settings.WindowOpacity = _opacitySlider.Value;
            settings.StartWithWindows = _startWithWinCheckBox.IsChecked == true;
            settings.PlaySounds = _playSoundsCheckBox.IsChecked == true;
            settings.AutoHideOnExecute = _autoHideCheckBox.IsChecked == true;
            settings.AlwaysOnTop = _alwaysOnTopCheckBox.IsChecked == true;

            settings.GoogleAIKey = _googleKeyBox.Text.Trim();
            settings.GithubToken = _githubTokenBox.Text.Trim();
            settings.DownloadDirectory = _downloadDirBox.Text.Trim();
            if (int.TryParse(_mobilePortBox.Text.Trim(), out int port)) settings.MobilePort = port;

            if (_llmBackendCombo.SelectedItem is string llm) settings.LlmBackend = llm;
            if (_ttsVoiceCombo.SelectedItem is string voice) TtsManager.SetVoice(voice);

            settings.EnableDualLlmCopilot = _enableDualLlmCheckBox.IsChecked == true;
            if (_dualLlmBackendCombo.SelectedItem is string db) settings.DualLlmBackend = db;
            if (_dualLlmModelCombo.SelectedItem is string dm) settings.DualLlmModel = DualLlmCopilot.ExtractModelName(dm);

            SettingsManager.Save();
            TextOverlay.Show("💾 Saved All Master Settings!", 3000);
            this.FadeOutAndClose();
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

        private static CheckBox CreateCheckBox(string content, bool isChecked)
        {
            var cb = new CheckBox
            {
                Content = content,
                IsChecked = isChecked,
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 4),
                Cursor = Cursors.Hand
            };
            cb.SetResourceReference(CheckBox.ForegroundProperty, "TextPrimaryBrush");
            return cb;
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
    }
}
