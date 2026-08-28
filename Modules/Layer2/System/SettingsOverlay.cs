// Developer: heaplyn
// Date: 2026-08-12
// Summary: Master Settings dashboard with RadTabControl integration.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace JarvisLauncher
{
    public class SettingsOverlay : BaseOverlay
    {
        private static SettingsOverlay? _instance;

        private CheckBox _startWinCheck = null!;
        private CheckBox _playSoundCheck = null!;
        private CheckBox _autoHideCheck = null!;
        private CheckBox _roundedCornersCheck = null!;
        private CheckBox _jarvisEnabledCheck = null!;
        private CheckBox _voiceModeActiveCheck = null!;
        private CheckBox _speakerVerifyCheck = null!;
        private CheckBox _teacherModeCheck = null!;
        private CheckBox _autonomousModeCheck = null!;
        private CheckBox _clickSpotCheck = null!;
        private TextBox _ollamaModelBox = null!;
        private TextBox _openaiModelBox = null!;
        private TextBox _openaiUrlBox = null!;
        private TextBox _ollamaUrlBox = null!;
        private TextBox _customFontPathBox = null!;
        private TextBox _downloadDirBox = null!;
        private Slider _chatHistorySlider = null!;
        private CheckBox _chatDebugCheck = null!;
        private Slider _guiScaleSlider = null!;
        private TabControl _mainTabControl = null!;

        public static void OpenSettings() => ShowOverlay();
        public static void ShowSettings() => ShowOverlay();
        public static void ShowOverlay() {
            Application.Current.Dispatcher.Invoke(() => {
                if (_instance == null || !_instance.IsLoaded) _instance = new SettingsOverlay();
                _instance.Show(); _instance.BringToFront();
            });
        }

        private SettingsOverlay() : base("⚙️ MASTER SYSTEM SETTINGS", 820, 680)
        {
            _instance = this;
            this.Closed += (s, e) => _instance = null;
            this.ResizeMode = ResizeMode.CanResizeWithGrip;

            var mainGrid = new Grid { Margin = new Thickness(15) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _mainTabControl = new TabControl();
            StyleTabControl(_mainTabControl);

            _mainTabControl.Items.Add(new TabItem { Header = "⚙️ Gen", Content = BuildGeneralTab() });
            _mainTabControl.Items.Add(new TabItem { Header = "🎨 Visuals", Content = BuildVisualsTab() });
            _mainTabControl.Items.Add(new TabItem { Header = "🤖 LLM", Content = BuildLlmTab() });
            _mainTabControl.Items.Add(new TabItem { Header = "🗣️ TTS", Content = BuildTtsTab() });
            _mainTabControl.Items.Add(new TabItem { Header = "🎙️ Vox", Content = BuildVoiceAiTab() });
            _mainTabControl.Items.Add(new TabItem { Header = "🧹 Data", Content = BuildDataTab() });
            _mainTabControl.Items.Add(new TabItem { Header = "🔄 Sync", Content = BuildSyncTab() });
            _mainTabControl.Items.Add(new TabItem { Header = "📶 Off", Content = BuildOfflineTab() });
            _mainTabControl.Items.Add(new TabItem { Header = "🏷️ Map", Content = BuildAliasesTab() });
            _mainTabControl.Items.Add(new TabItem { Header = "💬 Chat", Content = BuildChatTab() });

            Grid.SetRow(_mainTabControl, 0);
            mainGrid.Children.Add(_mainTabControl);

            var saveBtn = CreateStyledButton("💾 SYNCHRONIZE SYSTEM STATE", (s, e) => SaveAllSettings(), isPrimary: true, fontSize: 13);
            saveBtn.Height = 45; Grid.SetRow(saveBtn, 1); mainGrid.Children.Add(saveBtn);

            this.UserContent = mainGrid;
        }

        private UIElement BuildGeneralTab() {
            var s = new StackPanel(); var set = SettingsManager.Current;
            s.Children.Add(CreateHeader("System Lifecycle & Automation"));
            _startWinCheck = CreateCheckBox("Auto-Launch with Windows", set.START_WITH_WINDOWS, v => set.START_WITH_WINDOWS = v); s.Children.Add(_startWinCheck);
            s.Children.Add(CreateCheckBox("Always on Top (HUD Priority)", set.ALWAYS_ON_TOP, v => set.ALWAYS_ON_TOP = v));
            _autoHideCheck = CreateCheckBox("Auto-Hide HUD on Command Execution", set.AUTO_HIDE_ON_EXECUTE, v => set.AUTO_HIDE_ON_EXECUTE = v); s.Children.Add(_autoHideCheck);
            _playSoundCheck = CreateCheckBox("Play System Audio Feedback", set.PLAY_SOUNDS, v => set.PLAY_SOUNDS = v); s.Children.Add(_playSoundCheck);
            _autonomousModeCheck = CreateCheckBox("Enable Autonomous Proactive Interjections", set.IS_AUTONOMOUS_MODE_ENABLED, v => set.IS_AUTONOMOUS_MODE_ENABLED = v); s.Children.Add(_autonomousModeCheck);
            s.Children.Add(CreateHeader("Glassmorphic UI & Aesthetics"));
            s.Children.Add(CreateCheckBox("Enable Fluid Window Animations", set.ENABLE_ANIMATIONS, v => set.ENABLE_ANIMATIONS = v));
            s.Children.Add(CreateCheckBox("Use High-Fidelity Dynamic Gradients", set.USE_GRADIENT_BACKGROUND, v => set.USE_GRADIENT_BACKGROUND = v));
            _roundedCornersCheck = CreateCheckBox("Rounded Corner Smoothing (Modern)", set.USE_ROUNDED_CORNERS, v => set.USE_ROUNDED_CORNERS = v); s.Children.Add(_roundedCornersCheck);
            _clickSpotCheck = CreateCheckBox("Enable Click Visual Feedback (Ripples)", set.ENABLE_CLICK_DARK_SPOT, v => set.ENABLE_CLICK_DARK_SPOT = v); s.Children.Add(_clickSpotCheck);
            s.Children.Add(CreateLabel("Animation Speed Multiplier:"));
            var speedSlider = CreateSettingsSlider(0.1, 5.0, set.ANIMATION_SPEED, 0.1);
            speedSlider.ValueChanged += (obj, e) => set.ANIMATION_SPEED = speedSlider.Value; s.Children.Add(speedSlider);
            s.Children.Add(CreateHeader("System Geometry & Scaling"));
            s.Children.Add(CreateCheckBox("📐 Adaptive Auto-Scaling (Sync to Resolution)", set.AUTO_GUI_SCALE_TO_SCREEN, v => set.AUTO_GUI_SCALE_TO_SCREEN = v));
            s.Children.Add(CreateLabel("Universal GUI Scale (0.3x - 4.0x):"));
            _guiScaleSlider = CreateSettingsSlider(0.3, 4.0, set.GUI_SCALE, 0.1);
            s.Children.Add(_guiScaleSlider);
            s.Children.Add(CreateHeader("HUD Positioning & Geometry"));
            s.Children.Add(CreateLabel("Active HUD Font Family:"));
            var fontCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(6, 4, 6, 4) };
            foreach (var f in Fonts.SystemFontFamilies.OrderBy(x => x.Source)) fontCombo.Items.Add(f.Source);
            fontCombo.SelectedItem = set.CUSTOM_FONT_FAMILY; fontCombo.SelectionChanged += (obj, e) => set.CUSTOM_FONT_FAMILY = fontCombo.SelectedItem.ToString() ?? "Segoe UI";
            s.Children.Add(fontCombo);
            s.Children.Add(CreateLabel("External Font Asset Path (.ttf/.otf):"));
            var fontGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            fontGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fontGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _customFontPathBox = CreateTextBox();
            _customFontPathBox.Text = set.CUSTOM_FONT_PATH;
            _customFontPathBox.Height = 32;
            _customFontPathBox.VerticalContentAlignment = VerticalAlignment.Center;
            _customFontPathBox.Padding = new Thickness(6, 4, 6, 4);

            var browseFontBtn = CreateStyledButton("📁", (obj, ev) =>
            {
                var dlg = new OpenFileDialog { Filter = "Font Files (*.ttf;*.otf)|*.ttf;*.otf|All Files (*.*)|*.*" };
                if (dlg.ShowDialog() == true) _customFontPathBox.Text = dlg.FileName;
            });
            browseFontBtn.Width = 40;
            browseFontBtn.Height = 32;
            browseFontBtn.Margin = new Thickness(5, 0, 0, 0);

            Grid.SetColumn(_customFontPathBox, 0);
            fontGrid.Children.Add(_customFontPathBox);
            Grid.SetColumn(browseFontBtn, 1);
            fontGrid.Children.Add(browseFontBtn);
            s.Children.Add(fontGrid);

            return new ScrollViewer { Content = s, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private UIElement BuildVisualsTab() {
            var s = new StackPanel();
            var set = SettingsManager.Current;
            s.Children.Add(CreateHeader("Visual Suite & HUD Customization"));
            s.Children.Add(CreateLabel("Consolidate all system visual options, colors, fonts, outer glow, window drag physics, and background media in the Jarvis Visual Studio."));

            var openBtn = CreateStyledButton("🎨 OPEN VISUAL SUITE", (obj, e) => {
                JarvisVisualsOverlay.ShowOverlay();
                this.Hide();
            }, isPrimary: true, fontSize: 13);
            openBtn.Height = 40;
            openBtn.Margin = new Thickness(0, 15, 0, 15);
            s.Children.Add(openBtn);

            s.Children.Add(CreateHeader("Quick Visual Options"));
            s.Children.Add(CreateCheckBox("Enable Fluid Window Animations", set.ENABLE_ANIMATIONS, v => set.ENABLE_ANIMATIONS = v));
            s.Children.Add(CreateCheckBox("Use High-Fidelity Dynamic Gradients", set.USE_GRADIENT_BACKGROUND, v => set.USE_GRADIENT_BACKGROUND = v));
            s.Children.Add(CreateCheckBox("Enable Click Visual Feedback (Ripples)", set.ENABLE_CLICK_DARK_SPOT, v => set.ENABLE_CLICK_DARK_SPOT = v));
            s.Children.Add(CreateCheckBox("Adaptive Auto-Scaling", set.AUTO_GUI_SCALE_TO_SCREEN, v => set.AUTO_GUI_SCALE_TO_SCREEN = v));
            s.Children.Add(CreateCheckBox("Low-VFX Performance Mode (Disables Blurs)", set.LOW_VFX_MODE, v => { set.LOW_VFX_MODE = v; ThemeManager.ApplyVisualOverrides(); }));

            return new ScrollViewer { Content = s, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private UIElement BuildLlmTab() {
            var s = new StackPanel(); var set = SettingsManager.Current;
            s.Children.Add(CreateHeader("Global LLM Orchestration"));
            s.Children.Add(CreateLabel("Primary Intelligence Node:"));
            var backCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(6, 4, 6, 4) };
            string[] backends = { "Gemini", "Groq", "OpenAI", "Anthropic", "Mistral", "OpenRouter", "Perplexity", "Lemonade", "Ollama" };
            foreach (var b in backends) backCombo.Items.Add(b); backCombo.SelectedItem = set.LLM_BACKEND;
            backCombo.SelectionChanged += (obj, e) => set.LLM_BACKEND = backCombo.SelectedItem.ToString() ?? "Gemini";
            s.Children.Add(backCombo);
            _openaiModelBox = CreateLabeledTextBox(s, "OpenAI Model Target:", set.OPENAI_MODEL);
            _openaiUrlBox = CreateLabeledTextBox(s, "OpenAI Base Endpoint URL:", set.OPENAI_BASE_URL);
            _ollamaModelBox = CreateLabeledTextBox(s, "Ollama Model Target:", set.OLLAMA_MODEL);
            _ollamaUrlBox = CreateLabeledTextBox(s, "Ollama API Endpoint:", set.OLLAMA_ENDPOINT);
            return new ScrollViewer { Content = s, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private UIElement BuildTtsTab() {
            var s = new StackPanel(); var set = SettingsManager.Current;
            s.Children.Add(CreateHeader("Acoustic Synthesis (TTS)"));
            s.Children.Add(CreateLabel("Engine:"));
            var eCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
            eCombo.Items.Add("System"); eCombo.Items.Add("Google"); eCombo.Items.Add("ElevenLabs");
            eCombo.SelectedItem = set.TTS_ENGINE; eCombo.SelectionChanged += (obj, ev) => set.TTS_ENGINE = eCombo.SelectedItem.ToString() ?? "System";
            s.Children.Add(eCombo);
            s.Children.Add(CreateLabel("Speech Rate:")); var rS = CreateSettingsSlider(-10, 10, set.TTS_SPEECH_RATE, 1);
            rS.ValueChanged += (obj, ev) => set.TTS_SPEECH_RATE = (int)rS.Value; s.Children.Add(rS);
            return new ScrollViewer { Content = s, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private UIElement BuildVoiceAiTab() {
            var s = new StackPanel(); var set = SettingsManager.Current;
            s.Children.Add(CreateHeader("Aural Perception & Vox Engine"));
            _voiceModeActiveCheck = CreateCheckBox("Active Voice Listening", set.IS_VOICE_MODE_ACTIVE, v => set.IS_VOICE_MODE_ACTIVE = v); s.Children.Add(_voiceModeActiveCheck);
            s.Children.Add(CreateCheckBox("Enable Local Whisper Transcription", set.VOX_USE_LOCAL_WHISPER, v => set.VOX_USE_LOCAL_WHISPER = v));
            s.Children.Add(CreateLabel("Noise Gate Threshold (dB):"));
            var gateS = CreateSettingsSlider(-60, 0, set.MIC_NOISE_GATE_DB, 1);
            gateS.ValueChanged += (obj, ev) => set.MIC_NOISE_GATE_DB = gateS.Value; s.Children.Add(gateS);
            return new ScrollViewer { Content = s, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private UIElement BuildDataTab() {
            var s = new StackPanel(); var set = SettingsManager.Current;
            s.Children.Add(CreateHeader("Knowledge Harvesting & Persistence"));
            s.Children.Add(CreateCheckBox("Automatic App Indexing", set.ENABLE_WINDOWS_APP_INDEXING, v => set.ENABLE_WINDOWS_APP_INDEXING = v));
            s.Children.Add(CreateCheckBox("Context Scraping Active", set.DATA_ENABLE_AUTO_SCRAPE, v => set.DATA_ENABLE_AUTO_SCRAPE = v));

            s.Children.Add(CreateHeader("System Storage & Downloads"));
            s.Children.Add(CreateLabel("Download Directory:"));
            var folderGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            folderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            folderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _downloadDirBox = CreateTextBox();
            _downloadDirBox.Text = set.DOWNLOAD_DIRECTORY;
            Grid.SetColumn(_downloadDirBox, 0);
            folderGrid.Children.Add(_downloadDirBox);

            var browseBtn = CreateStyledButton("📁 BROWSE", (obj, e) => {
                var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select Download Directory" };
                if (dlg.ShowDialog() == true) {
                    _downloadDirBox.Text = dlg.FolderName;
                }
            }, isPrimary: false, fontSize: 10);
            browseBtn.Height = 32;
            browseBtn.Margin = new Thickness(8, 0, 0, 8);
            Grid.SetColumn(browseBtn, 1);
            folderGrid.Children.Add(browseBtn);

            s.Children.Add(folderGrid);
            return new ScrollViewer { Content = s, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private UIElement BuildSyncTab() {
            var s = new StackPanel(); var set = SettingsManager.Current;
            s.Children.Add(CreateHeader("P2P Synchronization & Backup"));
            s.Children.Add(CreateCheckBox("Enable P2P Server Node", set.P2P_SERVER_ENABLED, v => set.P2P_SERVER_ENABLED = v));
            s.Children.Add(CreateCheckBox("Auto-Sync with Backup Cluster", set.AUTO_SYNC_WITH_BACKUP, v => set.AUTO_SYNC_WITH_BACKUP = v));
            return new ScrollViewer { Content = s, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private UIElement BuildOfflineTab() {
            var s = new StackPanel(); var set = SettingsManager.Current;
            s.Children.Add(CreateHeader("Offline & Edge Capabilities"));
            s.Children.Add(CreateCheckBox("Enable Teacher Mode (Offline Manuals)", set.IS_TEACHER_MODE_ENABLED, v => set.IS_TEACHER_MODE_ENABLED = v));
            return s;
        }

        private UIElement BuildAliasesTab() {
            var s = new StackPanel(); var set = SettingsManager.Current;
            s.Children.Add(CreateHeader("Command Mapping & Aliases"));
            foreach (var a in set.ALIASES) s.Children.Add(CreateLabel($"{a.Key} → {a.Value}", 10, false));
            return new ScrollViewer { Content = s, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private UIElement BuildChatTab() {
            var s = new StackPanel(); var set = SettingsManager.Current;
            s.Children.Add(CreateHeader("Chat & HUD Messaging"));
            s.Children.Add(CreateLabel("Max Visible History turns:"));
            _chatHistorySlider = CreateSettingsSlider(5, 200, set.CHAT_MAX_HISTORY_DISPLAY, 5); s.Children.Add(_chatHistorySlider);
            _chatDebugCheck = CreateCheckBox("Show AI Reasoning Context (Debug)", set.CHAT_SHOW_DEBUG_DETAILS, v => set.CHAT_SHOW_DEBUG_DETAILS = v); s.Children.Add(_chatDebugCheck);
            return new ScrollViewer { Content = s, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private void SaveAllSettings() {
            try {
                var s = SettingsManager.Current;
                s.OPENAI_MODEL = _openaiModelBox.Text.Trim();
                s.OPENAI_BASE_URL = _openaiUrlBox.Text.Trim();
                s.OLLAMA_MODEL = _ollamaModelBox.Text.Trim();
                s.OLLAMA_ENDPOINT = _ollamaUrlBox.Text.Trim();
                s.CHAT_MAX_HISTORY_DISPLAY = (int)_chatHistorySlider.Value;
                if (_chatDebugCheck != null) s.CHAT_SHOW_DEBUG_DETAILS = _chatDebugCheck.IsChecked == true;
                if (_customFontPathBox != null) s.CUSTOM_FONT_PATH = _customFontPathBox.Text.Trim();
                if (_downloadDirBox != null) s.DOWNLOAD_DIRECTORY = _downloadDirBox.Text.Trim();
                if (_guiScaleSlider != null) s.GUI_SCALE = _guiScaleSlider.Value;
                ThemeManager.ApplyVisualOverrides();
                BaseOverlay.UpdateAllScales();
                SettingsManager.Save();
                TextOverlay.Show("⚙️ SYSTEM PARAMETERS SYNCHRONIZED", 2000);
                this.Hide();
            } catch (Exception ex) { MessageBox.Show("Failed to synchronize: " + ex.Message); }
        }

        private CheckBox CreateCheckBox(string text, bool val, Action<bool> changed) {
            var cb = new CheckBox { Content = text, IsChecked = val, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 8), FontSize = 12 };
            cb.Checked += (s, e) => changed(true); cb.Unchecked += (s, e) => changed(false); return cb;
        }

        private Slider CreateSettingsSlider(double min, double max, double val, double tick) {
            var slider = new Slider { Minimum = min, Maximum = max, Value = val, TickFrequency = tick, IsSnapToTickEnabled = true, Margin = new Thickness(0, 8, 0, 12), AutoToolTipPlacement = System.Windows.Controls.Primitives.AutoToolTipPlacement.TopLeft };
            slider.PreviewMouseLeftButtonDown += (s, e) => { try { slider.Focus(); } catch { } }; return slider;
        }

        private static TextBlock CreateHeader(string text) => new TextBlock { Text = text, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Brushes.Cyan, Margin = new Thickness(0, 15, 0, 10) };
    }
}
