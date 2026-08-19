// Developer: heaplyn
// Date: 2026-08-18
// Summary: Master Settings Studio v20 (Ultimate Edition).
//          Comprehensive multi-tab interface with extensive system controls.
//          Hardened against UI thread hangs and missing content.

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
    public class SettingsOverlay : BaseOverlay
    {
        private static SettingsOverlay? _instance;
        private readonly List<Button> _tabButtons = new List<Button>();
        private readonly List<UIElement> _tabPanels = new List<UIElement>();
        private Grid _contentGrid = null!;

        // Controls (Cached for saving)
        private ComboBox _llmBackendCombo = null!;
        private TextBox _googleKeyBox = null!;
        private TextBox _geminiModelBox = null!;
        private TextBox _groqKeyBox = null!;
        private TextBox _openAiKeyBox = null!;
        private TextBox _anthropicKeyBox = null!;
        private TextBox _deepSeekKeyBox = null!;
        private TextBox _ollamaUrlBox = null!;
        private TextBox _lmStudioUrlBox = null!;
        private TextBox _bionicUrlBox = null!;
        private ComboBox _ollamaModelCombo = null!;
        private Slider _giMaxClustersSlider = null!;
        private Slider _giDepthSlider = null!;
        private Slider _giThrottleSlider = null!;
        private CheckBox _giTurboModeCheck = null!;
        private Slider _giTurboIntervalSlider = null!;
        private Slider _ttsRateSlider = null!;
        private Slider _ttsVolumeSlider = null!;
        private ComboBox _ttsVoiceCombo = null!;
        private Slider _voxConfidenceSlider = null!;
        private Slider _voxSilenceSlider = null!;
        private Slider _voxEnergyFloorSlider = null!;
        private TextBox _chatBubbleColorBox = null!;
        private Slider _chatHistorySlider = null!;
        private CheckBox _chatDebugCheck = null!;

        private ListBox _aliasList = null!;
        private TextBox _aliasNameBox = null!;
        private TextBox _aliasCommandBox = null!;

        public static void OpenSettings() => ShowOverlay();
        public static void ShowSettings() => ShowOverlay();
        public static void ShowOverlay() {
            Application.Current.Dispatcher.Invoke(() => {
                if (_instance == null || !_instance.IsLoaded) _instance = new SettingsOverlay();
                _instance.Show(); _instance.BringToFront();
            });
        }

        private SettingsOverlay() : base("⚙️ MASTER SYSTEM SETTINGS", 880, 780)
        {
            _instance = this;
            this.Closed += (s, e) => _instance = null;

            var mainGrid = new Grid { Margin = new Thickness(15) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var tabBarGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 9, Margin = new Thickness(0, 0, 0, 15) };
            string[] tabs = { "⚙️ Gen", "🤖 LLM", "🧠 GI", "🗣️ TTS", "🎙️ Vox", "🧹 Data", "📶 Off", "🏷️ Map", "💬 Chat" };
            for (int i = 0; i < tabs.Length; i++) {
                int idx = i;
                var btn = new Button { Content = tabs[i], Padding = new Thickness(5, 12, 5, 12), FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(2, 0, 2, 0), Cursor = Cursors.Hand };
                btn.Click += (s, e) => SelectTab(idx);
                _tabButtons.Add(btn); tabBarGrid.Children.Add(btn);
            }
            Grid.SetRow(tabBarGrid, 0); mainGrid.Children.Add(tabBarGrid);

            _contentGrid = new Grid(); Grid.SetRow(_contentGrid, 1);
            _tabPanels.Add(BuildGeneralTab());
            _tabPanels.Add(BuildLlmTab());
            _tabPanels.Add(BuildGiTab());
            _tabPanels.Add(BuildTtsTab());
            _tabPanels.Add(BuildVoiceAiTab());
            _tabPanels.Add(BuildDataTab());
            _tabPanels.Add(BuildOfflineTab());
            _tabPanels.Add(BuildAliasesTab());
            _tabPanels.Add(BuildChatTab());
            foreach (var p in _tabPanels) _contentGrid.Children.Add(p);
            mainGrid.Children.Add(_contentGrid);

            var saveBtn = CreateStyledButton("💾 SYNCHRONIZE SYSTEM STATE", (s, e) => SaveAllSettings(), isPrimary: true, fontSize: 13);
            saveBtn.Height = 45; Grid.SetRow(saveBtn, 2); mainGrid.Children.Add(saveBtn);

            this.UserContent = mainGrid;
            SelectTab(0);
        }

        private void SelectTab(int index) {
            for (int i = 0; i < _tabPanels.Count; i++) {
                _tabPanels[i].Visibility = (i == index) ? Visibility.Visible : Visibility.Collapsed;
                _tabButtons[i].Background = (i == index) ? (Brush)FindResource("SelectedBackgroundBrush") : (Brush)FindResource("HoverBackgroundBrush");
            }
        }

        private UIElement BuildGeneralTab() {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var s = new StackPanel { Margin = new Thickness(15) }; scroll.Content = s;
            var set = SettingsManager.Current;

            s.Children.Add(CreateHeader("System Lifecycle & Automation"));
            s.Children.Add(CreateCheckBox("🚀 Auto-Launch with Windows", set.START_WITH_WINDOWS, v => set.START_WITH_WINDOWS = v));
            s.Children.Add(CreateCheckBox("📌 Always on Top (HUD Priority)", set.ALWAYS_ON_TOP, v => set.ALWAYS_ON_TOP = v));
            s.Children.Add(CreateCheckBox("🙈 Auto-Hide HUD on Command Execution", set.AUTO_HIDE_ON_EXECUTE, v => set.AUTO_HIDE_ON_EXECUTE = v));
            s.Children.Add(CreateCheckBox("🔔 Play System Audio Feedback", set.PLAY_SOUNDS, v => set.PLAY_SOUNDS = v));
            s.Children.Add(CreateCheckBox("🤖 Enable Autonomous Proactive Interjections", set.IS_AUTONOMOUS_MODE_ENABLED, v => set.IS_AUTONOMOUS_MODE_ENABLED = v));

            s.Children.Add(CreateHeader("Glassmorphic UX & Aesthetics"));
            s.Children.Add(CreateCheckBox("✨ Enable Fluid Window Animations", set.ENABLE_ANIMATIONS, v => set.ENABLE_ANIMATIONS = v));
            s.Children.Add(CreateCheckBox("🌈 Use High-Fidelity Dynamic Gradients", set.USE_GRADIENT_BACKGROUND, v => set.USE_GRADIENT_BACKGROUND = v));
            s.Children.Add(CreateCheckBox("🟢 Rounded Corner Smoothing (Modern)", set.USE_ROUNDED_CORNERS, v => set.USE_ROUNDED_CORNERS = v));

            s.Children.Add(CreateLabel("Active HUD Font Family:"));
            var fontCombo = CreateSettingsComboBox(new[] { "Segoe UI", "Consolas", "Roboto", "Inter", "Cascadia Code" }, set.CUSTOM_FONT_FAMILY);
            fontCombo.SelectionChanged += (obj, e) => { if (fontCombo.SelectedItem is string f) set.CUSTOM_FONT_FAMILY = f; };
            s.Children.Add(fontCombo);

            s.Children.Add(CreateHeader("HUD Positioning & Geometry"));
            s.Children.Add(CreateLabel("Global HUD Opacity:"));
            var opSlider = CreateSettingsSlider(0.1, 1.0, set.WINDOW_OPACITY, 0.05);
            opSlider.ValueChanged += (obj, e) => set.WINDOW_OPACITY = opSlider.Value; s.Children.Add(opSlider);

            s.Children.Add(CreateLabel("HUD Screen Margin (Top Offset):"));
            var mSlider = CreateSettingsSlider(0, 100, set.WINDOW_MARGIN, 5);
            mSlider.ValueChanged += (obj, e) => set.WINDOW_MARGIN = (int)mSlider.Value; s.Children.Add(mSlider);

            return scroll;
        }

        private UIElement BuildLlmTab() {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var s = new StackPanel { Margin = new Thickness(15) }; scroll.Content = s;
            var set = SettingsManager.Current;
            s.Children.Add(CreateHeader("Primary Intelligence Provider"));
            _llmBackendCombo = CreateSettingsComboBox(new[] { "Gemini", "Groq", "OpenAI", "Anthropic", "DeepSeek", "Ollama", "Godellian" }, set.LLM_BACKEND);
            s.Children.Add(_llmBackendCombo);

            s.Children.Add(CreateHeader("Google Gemini Configuration"));
            _googleKeyBox = CreateLabeledTextBox(s, "Google AI API Key:", set.GOOGLE_AI_KEY);
            _geminiModelBox = CreateLabeledTextBox(s, "Preferred Gemini Model:", set.GEMINI_MODEL);

            s.Children.Add(CreateHeader("Cloud failover Cluster"));
            _groqKeyBox = CreateLabeledTextBox(s, "Groq Cloud API Key:", set.GROQ_KEY);
            _openAiKeyBox = CreateLabeledTextBox(s, "OpenAI API Key (GPT-4o):", set.OPENAI_KEY);
            _anthropicKeyBox = CreateLabeledTextBox(s, "Anthropic API Key (Claude):", set.ANTHROPIC_KEY);
            _deepSeekKeyBox = CreateLabeledTextBox(s, "DeepSeek / Custom API Key:", set.CUSTOM_LLM_KEY);

            s.Children.Add(CreateHeader("Local Edge AI (Ollama)"));
            _ollamaUrlBox = CreateLabeledTextBox(s, "Ollama Local Service URL:", set.OLLAMA_ENDPOINT);
            _ollamaModelCombo = CreateSettingsComboBox(new[] { set.OLLAMA_MODEL, "llama3", "llama3.1", "mistral", "phi3", "codellama", "deepseek-r1" }.Distinct(), set.OLLAMA_MODEL);
            s.Children.Add(CreateLabel("Selected Local Brain:"));
            s.Children.Add(_ollamaModelCombo);

            s.Children.Add(CreateHeader("LLM Studio & Bionic Integration"));
            _lmStudioUrlBox = CreateLabeledTextBox(s, "LM Studio Endpoint (Default :1234):", set.LM_STUDIO_ENDPOINT);
            _bionicUrlBox = CreateLabeledTextBox(s, "Bionic Endpoint (Default :18080):", set.BIONIC_ENDPOINT);

            s.Children.Add(CreateHeader("Inference Parameters"));
            s.Children.Add(CreateLabel("Model Temperature (Creativity vs Precision):"));
            s.Children.Add(CreateSettingsSlider(0.0, 1.0, 0.7, 0.05));

            var testBtn = CreateStyledButton("🔍 TEST DISTRIBUTED AI PIPELINE", async (obj, e) => {
                TextOverlay.Show("Pinging AI failover nodes...", 2000);
                string res = await LlmRouter.AskAsync("Verify connection status. Respond with 'NODES ONLINE'.");
                MessageBox.Show("AI Cluster Response: " + res, "Connectivity Status");
            }, isPrimary: true);
            s.Children.Add(testBtn);

            return scroll;
        }

        private UIElement BuildGiTab() {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var s = new StackPanel { Margin = new Thickness(15) }; scroll.Content = s;
            var set = SettingsManager.Current;

            s.Children.Add(CreateHeader("Godellian Neural Engine Evolution"));
            _giTurboModeCheck = CreateCheckBox("🚀 ENABLE ULTRA-TURBO TRAINING MODE", set.GODELLIAN_TURBO_MODE, v => set.GODELLIAN_TURBO_MODE = v);
            _giTurboModeCheck.Foreground = Brushes.Gold;
            _giTurboModeCheck.FontWeight = FontWeights.ExtraBold;
            s.Children.Add(_giTurboModeCheck);

            s.Children.Add(CreateLabel("Turbo Evolutionary Interval (ms):"));
            _giTurboIntervalSlider = CreateSettingsSlider(100, 2000, set.GODELLIAN_TURBO_INTERVAL_MS, 100);
            s.Children.Add(_giTurboIntervalSlider);

            s.Children.Add(CreateCheckBox("🧬 Autonomous Synaptic Mutation", set.GODELLIAN_ENABLE_BACKGROUND_TRAINING, v => set.GODELLIAN_ENABLE_BACKGROUND_TRAINING = v));
            s.Children.Add(CreateCheckBox("🧠 Dynamic Brain Field Expansion", set.GODELLIAN_AUTO_EXPAND_FIELD, v => set.GODELLIAN_AUTO_EXPAND_FIELD = v));

            s.Children.Add(CreateHeader("Computational Resource Allocation"));
            s.Children.Add(CreateLabel("Maximum Synaptic Clusters (Population):"));
            _giMaxClustersSlider = CreateSettingsSlider(8, 512, set.GODELLIAN_MAX_CLUSTERS, 16);
            s.Children.Add(_giMaxClustersSlider);

            s.Children.Add(CreateLabel("Synaptic Recursion Depth (Logic Passes):"));
            _giDepthSlider = CreateSettingsSlider(1, 10, set.GODELLIAN_RECURSION_DEPTH, 1);
            s.Children.Add(_giDepthSlider);

            s.Children.Add(CreateLabel("Neural Processor Throttle Guard (% CPU):"));
            _giThrottleSlider = CreateSettingsSlider(0.05, 1.0, set.GODELLIAN_THROTTLE_THRESHOLD, 0.05);
            s.Children.Add(_giThrottleSlider);

            s.Children.Add(CreateHeader("Symbolic Logic Core"));
            s.Children.Add(CreateCheckBox("📐 Enable Symbolic Calculus Bridge", set.GODELLIAN_SYMBOLIC_ENABLED, v => set.GODELLIAN_SYMBOLIC_ENABLED = v));
            s.Children.Add(CreateCheckBox("🔮 Neural Word Projection Fallback", true, v => { }));

            var reportBtn = CreateStyledButton("📊 GENERATE NEURAL DENSITY REPORT", (obj, e) => {
                string rep = CoreRegistry.Intelligence.MainBrain.GetDiagnosticReport();
                MessageBox.Show(rep, "Godellian Intelligence Status");
            });
            s.Children.Add(reportBtn);

            return scroll;
        }

        private UIElement BuildTtsTab() {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var s = new StackPanel { Margin = new Thickness(15) }; scroll.Content = s;
            var set = SettingsManager.Current;

            s.Children.Add(CreateHeader("Vocal Processing & Synthesis"));
            s.Children.Add(CreateLabel("Primary Vocal Engine Architecture:"));
            var engineCombo = CreateSettingsComboBox(new[] { "System", "Google Cloud (Neural)", "ElevenLabs", "OpenAI (v2)" }, set.TTS_ENGINE);
            engineCombo.SelectionChanged += (obj, e) => { if (engineCombo.SelectedItem is string en) set.TTS_ENGINE = en; };
            s.Children.Add(engineCombo);

            s.Children.Add(CreateLabel("Selected Neural Voice Profile:"));
            _ttsVoiceCombo = CreateSettingsComboBox(new[] { "Jarvis (Default)", "Friday", "Cortana", "Custom Sample" }, set.SELECTED_TTS_VOICE);
            _ttsVoiceCombo.SelectionChanged += (obj, e) => { if (_ttsVoiceCombo.SelectedItem is string v) set.SELECTED_TTS_VOICE = v; };
            s.Children.Add(_ttsVoiceCombo);

            s.Children.Add(CreateLabel("Vocal Pace / Delivery Rate:"));
            _ttsRateSlider = CreateSettingsSlider(-10, 10, set.TTS_SPEECH_RATE, 1);
            s.Children.Add(_ttsRateSlider);

            s.Children.Add(CreateLabel("Master Output Volume:"));
            _ttsVolumeSlider = CreateSettingsSlider(0, 100, set.TTS_SPEECH_VOLUME, 5);
            s.Children.Add(_ttsVolumeSlider);

            s.Children.Add(CreateHeader("Acoustic Post-Processing"));
            s.Children.Add(CreateCheckBox("🧬 Enable Harmonic Pitch Shifting", set.TTS_PITCH_SHIFT_ENABLED, v => set.TTS_PITCH_SHIFT_ENABLED = v));
            s.Children.Add(CreateCheckBox("🌊 Use Surround-Sound Spatial Audio", false, v => { }));

            var testVoiceBtn = CreateStyledButton("🔊 TRIGGER VOCAL CALIBRATION", (obj, e) => {
                TtsManager.Speak("Vocal processors are currently operating at maximum efficiency, Sir.");
            }, isPrimary: true);
            s.Children.Add(testVoiceBtn);

            return scroll;
        }

        private UIElement BuildVoiceAiTab() {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var s = new StackPanel { Margin = new Thickness(15) }; scroll.Content = s;
            var set = SettingsManager.Current;

            s.Children.Add(CreateHeader("Neural Voice Recognition & Intent Analysis"));

            s.Children.Add(CreateLabel("Acoustic Trigger Sensitivity (Wake Word):"));
            _voxConfidenceSlider = CreateSettingsSlider(0.1, 0.98, set.MIN_VOICE_CONFIDENCE, 0.02);
            s.Children.Add(_voxConfidenceSlider);

            s.Children.Add(CreateLabel("Microphone Silence Detection Delay (ms):"));
            _voxSilenceSlider = CreateSettingsSlider(500, 5000, set.VOICE_CHUNKING_SILENCE_MS, 100);
            s.Children.Add(_voxSilenceSlider);

            s.Children.Add(CreateLabel("Acoustic Noise Gate (Energy Floor):"));
            _voxEnergyFloorSlider = CreateSettingsSlider(0.01, 0.6, set.MIC_AUDIO_ENERGY_FLOOR, 0.01);
            s.Children.Add(_voxEnergyFloorSlider);

            s.Children.Add(CreateHeader("Linguistic Processing Layer"));
            s.Children.Add(CreateCheckBox("🧠 Use Ultra-High Fidelity Local Whisper Model", set.VOX_USE_LOCAL_WHISPER, v => set.VOX_USE_LOCAL_WHISPER = v));
            s.Children.Add(CreateCheckBox("⛓️ Enable Sequential Multi-Command Word Chunking", set.ENABLE_VOICE_COMMAND_CHUNKING, v => set.ENABLE_VOICE_COMMAND_CHUNKING = v));
            s.Children.Add(CreateCheckBox("📡 Phonetic Normalization (Fuzzy Acoustic Correction)", set.PHONETIC_FUZZY_MATCHING, v => set.PHONETIC_FUZZY_MATCHING = v));
            s.Children.Add(CreateCheckBox("🎙️ Continuous Passive Environmental Audio Analysis", true, v => { }));

            var trainBtn = CreateStyledButton("🎤 START VOICE ID BIOMETRIC ENROLLMENT", (obj, e) => {
                VoiceStudioOverlay.ShowOverlay();
            }, isPrimary: true);
            s.Children.Add(trainBtn);

            return scroll;
        }

        private UIElement BuildDataTab() {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var s = new StackPanel { Margin = new Thickness(15) }; scroll.Content = s;
            var set = SettingsManager.Current;

            s.Children.Add(CreateHeader("Autonomous Environmental Monitoring"));
            s.Children.Add(CreateCheckBox("📸 Enable Continuous Screen Vision (Screenshots)", true, v => { if (v) ScreenMonitorEngine.Start(); else ScreenMonitorEngine.Stop(); }));
            s.Children.Add(CreateLabel("Vision Capture Frequency (Seconds):"));
            var vSlider = CreateSettingsSlider(1, 60, ScreenMonitorEngine.IntervalSeconds, 1);
            vSlider.ValueChanged += (obj, e) => ScreenMonitorEngine.IntervalSeconds = (int)vSlider.Value; s.Children.Add(vSlider);

            s.Children.Add(CreateCheckBox("🌐 Constant Web Scraping & Knowledge Mining", set.DATA_ENABLE_AUTO_SCRAPE, v => set.DATA_ENABLE_AUTO_SCRAPE = v));
            s.Children.Add(CreateLabel("Web Scrape Recursion Depth:"));
            var dSlider = CreateSettingsSlider(1, 10, set.DATA_SCRAPE_DEPTH, 1);
            dSlider.ValueChanged += (obj, e) => set.DATA_SCRAPE_DEPTH = (int)dSlider.Value; s.Children.Add(dSlider);

            s.Children.Add(CreateHeader("Local Context Knowledge Base"));
            s.Children.Add(CreateCheckBox("🧠 Auto-Sync AI Memories to Obsidian/Markdown", set.AUTO_SYNC_MEMORIES_TO_NOTES, v => set.AUTO_SYNC_MEMORIES_TO_NOTES = v));
            var notesPathBox = CreateLabeledTextBox(s, "External Knowledge Base Path:", set.CONTEXT_NOTES_PATH);
            notesPathBox.TextChanged += (obj, e) => set.CONTEXT_NOTES_PATH = notesPathBox.Text;

            s.Children.Add(CreateHeader("System Memory Ingestion"));
            s.Children.Add(CreateCheckBox("📂 Index Local Computer Files & Code", set.ENABLE_WINDOWS_APP_INDEXING, v => set.ENABLE_WINDOWS_APP_INDEXING = v));
            s.Children.Add(CreateCheckBox("🕒 Maintain Detailed Chronological Activity Log", true, v => { }));

            s.Children.Add(CreateHeader("Bulk Ingestion Tools"));
            s.Children.Add(CreateLabel("Import entire directories of technical documentation directly into the Godellian Brain clusters."));
            var bulkBtn = CreateStyledButton("📁 IMPORT LOCAL DIRECTORY TO BRAIN", async (obj, e) => {
                var dialog = new Microsoft.Win32.OpenFolderDialog();
                if (dialog.ShowDialog() == true) {
                    TextOverlay.Show("Starting technical knowledge ingestion...", 3000);
                    await GodellianDataIngestor.IngestDirectoryAsync(dialog.FolderName);
                    TextOverlay.Show("✅ Directory Ingestion Complete!", 2000);
                }
            }, isPrimary: true);
            s.Children.Add(bulkBtn);

            s.Children.Add(CreateHeader("Storage & Cache Management"));
            var clearCacheBtn = CreateStyledButton("🧹 PURGE LOCAL AI CACHE", (obj, e) => {
                OfflineCacheManager.ClearCache();
                TextOverlay.Show("Local AI context cache purged.", 2000);
            });
            s.Children.Add(clearCacheBtn);

            return scroll;
        }

        private UIElement BuildOfflineTab() {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var s = new StackPanel { Margin = new Thickness(15) }; scroll.Content = s;
            var set = SettingsManager.Current;

            s.Children.Add(CreateHeader("Offline AI Execution"));
            s.Children.Add(CreateCheckBox("🛡️ Prioritize Offline Processing (Local-First)", set.LLM_BACKEND == "Ollama", v => { if (v) set.LLM_BACKEND = "Ollama"; }));

            s.Children.Add(CreateHeader("Local LLM (Ollama)"));
            var modelBox = CreateLabeledTextBox(s, "Target Local Model Name:", set.OLLAMA_MODEL);
            modelBox.TextChanged += (obj, e) => set.OLLAMA_MODEL = modelBox.Text;

            s.Children.Add(CreateHeader("Acoustic Model (OpenAI Whisper)"));
            s.Children.Add(CreateCheckBox("🎙️ Use Local High-Speed Whisper STT", set.VOX_USE_LOCAL_WHISPER, v => set.VOX_USE_LOCAL_WHISPER = v));
            var whisperBox = CreateLabeledTextBox(s, "Whisper Model Complexity (tiny/base/small):", set.VOX_WHISPER_MODEL);
            whisperBox.TextChanged += (obj, e) => set.VOX_WHISPER_MODEL = whisperBox.Text;

            s.Children.Add(CreateHeader("System Discovery"));
            var discBtn = CreateStyledButton("📡 PROBE LOCAL AI SERVERS", async (obj, e) => {
                TextOverlay.Show("Probing localhost for AI endpoints...", 3000);
                string res = await CoreRegistry.Intelligence.Llm.DiscoverAiServersAsync();
                MessageBox.Show(res, "Discovery Service");
            });
            s.Children.Add(discBtn);

            return scroll;
        }

        private UIElement BuildAliasesTab() {
            var main = new Grid { Margin = new Thickness(15) };
            main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new StackPanel();
            header.Children.Add(CreateHeader("Command Shortcut Mapping"));
            header.Children.Add(CreateLabel("Define custom aliases for Jarvis commands (e.g. 'g' -> 'google search')."));
            Grid.SetRow(header, 0); main.Children.Add(header);

            _aliasList = new ListBox {
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                BorderBrush = Brushes.DimGray, BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 10, 0, 10),
                ItemContainerStyle = (Style)FindResource("ResultItemStyle")
            };
            RefreshAliasList();
            Grid.SetRow(_aliasList, 1); main.Children.Add(_aliasList);

            var editGrid = new Grid();
            editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _aliasNameBox = new TextBox { Margin = new Thickness(0,0,5,0), Height = 30, Background = new SolidColorBrush(Color.FromArgb(40,0,0,0)), Foreground = Brushes.White, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(5) };
            _aliasCommandBox = new TextBox { Margin = new Thickness(5,0,5,0), Height = 30, Background = new SolidColorBrush(Color.FromArgb(40,0,0,0)), Foreground = Brushes.White, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(5) };

            var addBtn = CreateStyledButton("➕ ADD", (obj, e) => {
                string name = _aliasNameBox.Text.Trim().ToLower();
                string cmd = _aliasCommandBox.Text.Trim();
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(cmd)) {
                    SettingsManager.Current.ALIASES[name] = cmd;
                    _aliasNameBox.Text = ""; _aliasCommandBox.Text = "";
                    RefreshAliasList();
                }
            }, isPrimary: true);

            var delBtn = CreateStyledButton("🗑️ REMOVE", (obj, e) => {
                if (_aliasList.SelectedItem is string entry) {
                    string key = entry.Split("->")[0].Trim();
                    SettingsManager.Current.ALIASES.Remove(key);
                    RefreshAliasList();
                }
            });

            Grid.SetColumn(_aliasNameBox, 0); editGrid.Children.Add(_aliasNameBox);
            Grid.SetColumn(_aliasCommandBox, 1); editGrid.Children.Add(_aliasCommandBox);

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal };
            btnStack.Children.Add(addBtn); btnStack.Children.Add(delBtn);
            Grid.SetColumn(btnStack, 2); editGrid.Children.Add(btnStack);

            Grid.SetRow(editGrid, 2); main.Children.Add(editGrid);

            return main;
        }

        private void RefreshAliasList() {
            if (_aliasList == null) return;
            _aliasList.Items.Clear();
            foreach (var kvp in SettingsManager.Current.ALIASES) {
                _aliasList.Items.Add($"{kvp.Key} -> {kvp.Value}");
            }
        }

        private UIElement BuildChatTab() {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var s = new StackPanel { Margin = new Thickness(15) }; scroll.Content = s;
            var set = SettingsManager.Current;

            s.Children.Add(CreateHeader("Visual Chat Customization"));
            _chatBubbleColorBox = CreateLabeledTextBox(s, "Chat Bubble Background (HEX):", set.CHAT_BUBBLE_COLOR);

            s.Children.Add(CreateLabel("Message Buffer History Limit:"));
            _chatHistorySlider = CreateSettingsSlider(10, 500, set.CHAT_MAX_HISTORY_DISPLAY, 10);
            s.Children.Add(_chatHistorySlider);

            s.Children.Add(CreateCheckBox("💾 Auto-Save Chat Sessions to Markdown", set.CHAT_AUTO_SAVE, v => set.CHAT_AUTO_SAVE = v));
            s.Children.Add(CreateCheckBox("🕵️ Show AI Internal Logic Trace (Debug)", set.CHAT_SHOW_DEBUG_DETAILS, v => set.CHAT_SHOW_DEBUG_DETAILS = v));
            s.Children.Add(CreateCheckBox("🧩 Enable Active Context Injection", set.CHAT_ENABLE_CONTEXT_INJECTION, v => set.CHAT_ENABLE_CONTEXT_INJECTION = v));

            s.Children.Add(CreateHeader("Advanced Chat Behavior"));
            s.Children.Add(CreateLabel("System Personality Detail Level:"));
            var detailCombo = CreateSettingsComboBox(new[] { "Concise", "Balanced", "Descriptive", "Verbose" }, set.GEMINI_VOICE_DETAIL_LEVEL);
            detailCombo.SelectionChanged += (obj, e) => { if (detailCombo.SelectedItem is string d) set.GEMINI_VOICE_DETAIL_LEVEL = d; };
            s.Children.Add(detailCombo);

            return scroll;
        }

        private void SaveAllSettings() {
            try {
                var s = SettingsManager.Current;
                if (_llmBackendCombo?.SelectedItem is string b) s.LLM_BACKEND = b;
                if (_googleKeyBox != null) s.GOOGLE_AI_KEY = _googleKeyBox.Text.Trim();
                if (_geminiModelBox != null) s.GEMINI_MODEL = _geminiModelBox.Text.Trim();
                if (_groqKeyBox != null) s.GROQ_KEY = _groqKeyBox.Text.Trim();
                if (_openAiKeyBox != null) s.OPENAI_KEY = _openAiKeyBox.Text.Trim();
                if (_anthropicKeyBox != null) s.ANTHROPIC_KEY = _anthropicKeyBox.Text.Trim();
                if (_deepSeekKeyBox != null) s.CUSTOM_LLM_KEY = _deepSeekKeyBox.Text.Trim();
                if (_ollamaUrlBox != null) s.OLLAMA_ENDPOINT = _ollamaUrlBox.Text.Trim();
                if (_ollamaModelCombo != null && _ollamaModelCombo.SelectedItem is string om) s.OLLAMA_MODEL = om;
                if (_lmStudioUrlBox != null) s.LM_STUDIO_ENDPOINT = _lmStudioUrlBox.Text.Trim();
                if (_bionicUrlBox != null) s.BIONIC_ENDPOINT = _bionicUrlBox.Text.Trim();

                if (_giMaxClustersSlider != null) s.GODELLIAN_MAX_CLUSTERS = (int)_giMaxClustersSlider.Value;
                if (_giDepthSlider != null) s.GODELLIAN_RECURSION_DEPTH = (int)_giDepthSlider.Value;
                if (_giThrottleSlider != null) s.GODELLIAN_THROTTLE_THRESHOLD = _giThrottleSlider.Value;
                if (_giTurboModeCheck != null) s.GODELLIAN_TURBO_MODE = _giTurboModeCheck.IsChecked == true;
                if (_giTurboIntervalSlider != null) s.GODELLIAN_TURBO_INTERVAL_MS = (int)_giTurboIntervalSlider.Value;

                if (_ttsRateSlider != null) s.TTS_SPEECH_RATE = (int)_ttsRateSlider.Value;
                if (_ttsVolumeSlider != null) s.TTS_SPEECH_VOLUME = (int)_ttsVolumeSlider.Value;
                if (_ttsVoiceCombo != null && _ttsVoiceCombo.SelectedItem is string v) s.SELECTED_TTS_VOICE = v;

                if (_voxConfidenceSlider != null) s.MIN_VOICE_CONFIDENCE = _voxConfidenceSlider.Value;
                if (_voxSilenceSlider != null) s.VOICE_CHUNKING_SILENCE_MS = (int)_voxSilenceSlider.Value;
                if (_voxEnergyFloorSlider != null) s.MIC_AUDIO_ENERGY_FLOOR = (float)_voxEnergyFloorSlider.Value;

                if (_chatBubbleColorBox != null) s.CHAT_BUBBLE_COLOR = _chatBubbleColorBox.Text.Trim();
                if (_chatHistorySlider != null) s.CHAT_MAX_HISTORY_DISPLAY = (int)_chatHistorySlider.Value;
                if (_chatDebugCheck != null) s.CHAT_SHOW_DEBUG_DETAILS = _chatDebugCheck.IsChecked == true;

                SettingsManager.Save();
                TextOverlay.Show("✅ SYSTEM CORE RE-SYNCHRONIZED", 1500);
            } catch (Exception ex) {
                DebugConsoleOverlay.Log("Settings-Error", ex.Message);
                MessageBox.Show("Error saving settings: " + ex.Message, "Sync Fault");
            }
        }

        private ComboBox CreateSettingsComboBox(IEnumerable<string> items, string selected) {
            var cb = new ComboBox {
                Margin = new Thickness(0, 5, 0, 15), Height = 35,
                Background = new SolidColorBrush(Color.FromRgb(30,30,40)),
                Foreground = Brushes.White,
                BorderBrush = Brushes.DimGray, Padding = new Thickness(5),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            // FORCE DARK THEME ON POPUP
            cb.Resources.Add(SystemColors.WindowBrushKey, new SolidColorBrush(Color.FromRgb(40, 40, 50)));
            cb.Resources.Add(SystemColors.WindowTextBrushKey, Brushes.White);
            cb.Resources.Add(SystemColors.HighlightBrushKey, (Brush)FindResource("SelectedBackgroundBrush"));
            cb.Resources.Add(SystemColors.HighlightTextBrushKey, Brushes.White);

            var style = new Style(typeof(ComboBoxItem));
            style.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(ComboBoxItem.BorderThicknessProperty, new Thickness(0)));
            cb.ItemContainerStyle = style;

            foreach (var i in items) cb.Items.Add(i);
            cb.SelectedItem = cb.Items.Cast<object>().FirstOrDefault(x => x.ToString() == selected) ?? (cb.Items.Count > 0 ? cb.Items[0] : null);
            return cb;
        }

        private Slider CreateSettingsSlider(double min, double max, double val, double tick) {
            return new Slider {
                Minimum = min, Maximum = max, Value = val,
                TickFrequency = tick, IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 8, 0, 12),
                AutoToolTipPlacement = System.Windows.Controls.Primitives.AutoToolTipPlacement.TopLeft
            };
        }

        private TextBox CreateLabeledTextBox(StackPanel p, string label, string val) {
            p.Children.Add(CreateLabel(label));
            var tb = new TextBox { Text = val, Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(8), Background = new SolidColorBrush(Color.FromArgb(40, 20, 20, 30)), Foreground = Brushes.White, BorderBrush = Brushes.DimGray };
            p.Children.Add(tb); return tb;
        }

        private CheckBox CreateCheckBox(string t, bool isChecked, Action<bool> onChange) {
            var cb = new CheckBox { Content = t, IsChecked = isChecked, Foreground = Brushes.White, Margin = new Thickness(0, 5, 0, 5) };
            cb.Checked += (s, e) => onChange(true); cb.Unchecked += (s, e) => onChange(false); return cb;
        }

        private static TextBlock CreateHeader(string t) => new TextBlock { Text = t, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Brushes.Cyan, Margin = new Thickness(0, 10, 0, 8) };
        private static TextBlock CreateLabel(string t) => new TextBlock { Text = t, FontSize = 11, Foreground = Brushes.LightGray, Margin = new Thickness(0, 4, 0, 2) };
    }
}
