// Developer: heaplyn
// Date: 2026-08-13
// Summary: Interactive WPF Overlay for Voice AI Training, Endless Auto-Advancing Teleprompter, Multi-Word Chunk Batch Trainer, Audio Recording, Waveform Visualizer, Guided Script Reader, and Voice Command Customization.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace JarvisLauncher
{
    public class VoiceStudioOverlay : BaseOverlay
    {
        private static VoiceStudioOverlay? _instance;

        public static void ShowOverlay()
        {
            if (_instance == null || !_instance.IsLoaded)
            {
                _instance = new VoiceStudioOverlay();
            }
            _instance.Show();
            _instance.BringToFront();
            _instance.Focus();
        }

        private TextBlock _statusText;
        private ProgressBar _audioLevelBar;
        private Button _recordBtn;
        private Button _playBtn;
        private TextBox _phraseBox;
        private TextBox _commandBox;
        private StackPanel _samplesPanel;
        private StackPanel _shortcutsPanel;
        private Slider _sensitivitySlider;
        private TextBlock _sensitivityValText;
        private DispatcherTimer _levelTimer;
        private VoiceSample? _lastRecordedSample;

        // Guided Training Script Reader State
        private int _scriptIndex = 0;
        private TextBlock _scriptCounterText;
        private TextBlock _scriptPromptText;
        private ProgressBar _scriptProgressBar;
        private Button _scriptRecordBtn;
        private Button _scriptPlayBtn;
        private VoiceSample? _lastScriptSample;

        // Multi-Word Chunk Trainer State
        private TextBox _multiWordBox;
        private Button _chunkRecordBtn;

        // Endless Hands-Free Teleprompter State
        private bool _isEndlessActive = false;
        private int _endlessTrainedCount = 0;
        private int _endlessWordIndex = 0;
        private TextBlock _endlessCountText;
        private TextBlock _endlessCurrentWordText;
        private TextBlock _endlessNextWordText;
        private Button _endlessToggleBtn;
        private DispatcherTimer _endlessSilenceTimer;
        private bool _endlessSpeechSpiked = false;

        private readonly string[] _endlessWordBank = new string[]
        {
            "Jarvis", "quantum", "cybernetic", "protocol", "matrix", "override", "algorithm", "hyperdrive",
            "holographic", "interface", "terminal", "powershell", "execute", "firewall", "security", "database",
            "optimizer", "subsystem", "network", "router", "telemetry", "diagnostics", "frequency", "satellite",
            "analyzer", "system", "command", "desktop", "downloads", "music", "playlist", "volume", "sticky",
            "notes", "calendar", "reminders", "focus", "pomodoro", "chunk", "dopamine", "process", "window",
            "screenshot", "clipboard", "tunnel", "cloudflare", "ngrok", "mobile", "bridge", "pairing", "codebase",
            "visual", "studio", "blender", "roblox", "dragon", "blox", "ultra", "ring", "level", "speech", "trainer",
            "acoustic", "normalizer", "phonetic", "dictionary", "dictation", "hypothesis", "confidence", "threshold"
        };

        private readonly string[] _trainingPrompts = new string[]
        {
            "Jarvis, status report on primary systems.",
            "Hey Jarvis, what is on my calendar for today?",
            "Jarvis, open Visual Studio Code and start a new session.",
            "The quick brown fox jumps over the lazy dog.",
            "Jarvis, play my favorite playlist and set volume to eighty percent.",
            "Jarvis, remind me in ten minutes to check the oven.",
            "OK Jarvis, search for quantum computing articles.",
            "Jarvis, lock the workstation and enter sleep mode.",
            "Jarvis, how are you doing today?",
            "Jarvis, chunk this project into micro-steps."
        };

        public VoiceStudioOverlay() : base("🎙️ JARVIS VOICE STUDIO & ENDLESS TELEPROMPTER", 820, 600)
        {
            var mainGrid = new Grid { Margin = new Thickness(10) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header / Tabs
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status Footer

            // Tab Control
            var tabControl = new TabControl
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };

            // ── TAB 1: ♾️ Endless Hands-Free Teleprompter ───────────────────────────
            var endlessTab = new TabItem { Header = "♾️ Endless Voice Teleprompter" };
            var endlessGrid = new Grid { Margin = new Thickness(14) };
            endlessGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header & Count
            endlessGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Teleprompter Card
            endlessGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Action Controls

            var endlessHeaderStack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            _endlessCountText = new TextBlock
            {
                Text = "♾️ Controlled Voice Teleprompter • Words Trained: 0",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Cyan
            };
            endlessHeaderStack.Children.Add(_endlessCountText);
            endlessHeaderStack.Children.Add(new TextBlock
            {
                Text = "Read the word out loud. Click 'Record Word', speak at your own pace, and click 'Stop & Submit Word' when ready to advance!",
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetRow(endlessHeaderStack, 0);
            endlessGrid.Children.Add(endlessHeaderStack);

            // Teleprompter Card
            var promptCard = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 56, 189, 248)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(24),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var promptCardStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            promptCardStack.Children.Add(new TextBlock
            {
                Text = "SAY THIS WORD OUT LOUD:",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            });

            _endlessCurrentWordText = new TextBlock
            {
                Text = $"\"{_endlessWordBank[0]}\"",
                FontSize = 34,
                FontWeight = FontWeights.ExtraBold,
                Foreground = Brushes.Cyan,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            };
            promptCardStack.Children.Add(_endlessCurrentWordText);

            _endlessNextWordText = new TextBlock
            {
                Text = $"Next Word: \"{_endlessWordBank[1]}\"",
                FontSize = 13,
                Foreground = Brushes.LightGray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };
            promptCardStack.Children.Add(_endlessNextWordText);

            promptCard.Child = promptCardStack;
            Grid.SetRow(promptCard, 1);
            endlessGrid.Children.Add(promptCard);

            // Action Controls
            var endlessControlsStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            _endlessToggleBtn = CreateStyledButton("🔴 Record Word", (s, e) => ToggleEndlessHandsFreeMode(), isPrimary: true);
            _endlessToggleBtn.Width = 220;
            _endlessToggleBtn.Height = 38;

            var skipBtn = CreateStyledButton("Skip Word ➡️", (s, e) => AdvanceEndlessWord());
            skipBtn.Width = 120;
            skipBtn.Margin = new Thickness(10, 0, 0, 0);

            endlessControlsStack.Children.Add(_endlessToggleBtn);
            endlessControlsStack.Children.Add(skipBtn);
            Grid.SetRow(endlessControlsStack, 2);
            endlessGrid.Children.Add(endlessControlsStack);

            endlessTab.Content = endlessGrid;
            tabControl.Items.Add(endlessTab);

            // ── TAB 2: 🎙️ Single Voice Trainer ─────────────────────────────────────
            var trainerTab = new TabItem { Header = "🎙️ Single Voice Trainer" };
            var trainerGrid = new Grid { Margin = new Thickness(10) };
            trainerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            trainerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            trainerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var controlsBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var controlsStack = new StackPanel();

            var inputsGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            inputsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var phraseStack = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
            phraseStack.Children.Add(new TextBlock { Text = "Target Phrase / Wake Word:", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.LightGray });
            _phraseBox = new TextBox { Text = "Hey Jarvis", Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 4, 0, 0), FontSize = 12 };
            _phraseBox.SetResourceReference(TextBox.BackgroundProperty, "WindowBackgroundBrush");
            _phraseBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            phraseStack.Children.Add(_phraseBox);
            Grid.SetColumn(phraseStack, 0);
            inputsGrid.Children.Add(phraseStack);

            var cmdStack = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
            cmdStack.Children.Add(new TextBlock { Text = "Action / Command (Optional):", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.LightGray });
            _commandBox = new TextBox { Text = "music", Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 4, 0, 0), FontSize = 12 };
            _commandBox.SetResourceReference(TextBox.BackgroundProperty, "WindowBackgroundBrush");
            _commandBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            cmdStack.Children.Add(_commandBox);
            Grid.SetColumn(cmdStack, 1);
            inputsGrid.Children.Add(cmdStack);

            controlsStack.Children.Add(inputsGrid);

            var btnsStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };

            _recordBtn = CreateStyledButton("🔴 Start Recording", (s, e) => ToggleRecording(), isPrimary: true);
            _recordBtn.Width = 140;

            _playBtn = CreateStyledButton("▶ Play Last Recording", (s, e) => PlayLastSample());
            _playBtn.Width = 150;
            _playBtn.IsEnabled = false;

            var saveBtn = CreateStyledButton("💾 Save Voice Command", (s, e) => SaveVoiceCommand());
            saveBtn.Width = 160;

            btnsStack.Children.Add(_recordBtn);
            btnsStack.Children.Add(_playBtn);
            btnsStack.Children.Add(saveBtn);
            controlsStack.Children.Add(btnsStack);

            controlsBorder.Child = controlsStack;
            Grid.SetRow(controlsBorder, 0);
            trainerGrid.Children.Add(controlsBorder);

            var meterStack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            meterStack.Children.Add(new TextBlock { Text = "🎙️ Live Audio Input Level Meter:", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Gray, Margin = new Thickness(2, 0, 0, 4) });

            _audioLevelBar = new ProgressBar
            {
                Height = 14,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Foreground = Brushes.LimeGreen,
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255))
            };
            meterStack.Children.Add(_audioLevelBar);
            Grid.SetRow(meterStack, 1);
            trainerGrid.Children.Add(meterStack);

            var samplesScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _samplesPanel = new StackPanel();
            samplesScroll.Content = _samplesPanel;
            Grid.SetRow(samplesScroll, 2);
            trainerGrid.Children.Add(samplesScroll);

            trainerTab.Content = trainerGrid;
            tabControl.Items.Add(trainerTab);

            // ── TAB 3: ⚡ Multi-Word Chunk Trainer (Batch Mode) ────────────────────
            var chunkTab = new TabItem { Header = "⚡ Multi-Word Chunk Trainer" };
            var chunkGrid = new Grid { Margin = new Thickness(14) };
            chunkGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            chunkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            chunkGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var chunkTopStack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            chunkTopStack.Children.Add(new TextBlock
            {
                Text = "⚡ Train Multiple Words & Phrases in One Single Recording:",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Cyan
            });
            chunkTopStack.Children.Add(new TextBlock
            {
                Text = "Read the paragraph below naturally into your mic. Jarvis will automatically slice the audio into individual word tokens in 1 go!",
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 2, 0, 8),
                TextWrapping = TextWrapping.Wrap
            });

            var presetsStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            presetsStack.Children.Add(new TextBlock { Text = "Presets: ", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });

            var p1Btn = CreateStyledButton("⚡ System Power", (s, e) => _multiWordBox.Text = "Jarvis status report sleep hibernate shutdown lock computer flush dns firewall");
            p1Btn.Padding = new Thickness(6, 2, 6, 2);
            p1Btn.Margin = new Thickness(0, 0, 4, 0);

            var p2Btn = CreateStyledButton("🎵 Media & Tasks", (s, e) => _multiWordBox.Text = "Jarvis play music volume up volume down mute next track sticky notes focus twenty five");
            p2Btn.Padding = new Thickness(6, 2, 6, 2);
            p2Btn.Margin = new Thickness(0, 0, 4, 0);

            var p3Btn = CreateStyledButton("📂 Organization", (s, e) => _multiWordBox.Text = "Jarvis organize desktop organize downloads clean empty sort by date deduplicate folder");
            p3Btn.Padding = new Thickness(6, 2, 6, 2);

            presetsStack.Children.Add(p1Btn);
            presetsStack.Children.Add(p2Btn);
            presetsStack.Children.Add(p3Btn);
            chunkTopStack.Children.Add(presetsStack);

            Grid.SetRow(chunkTopStack, 0);
            chunkGrid.Children.Add(chunkTopStack);

            _multiWordBox = new TextBox
            {
                Text = "Jarvis status report sleep hibernate shutdown lock computer flush dns firewall play music volume up organize desktop focus twenty five",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(10),
                FontSize = 13,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            _multiWordBox.SetResourceReference(TextBox.BackgroundProperty, "WindowBackgroundBrush");
            _multiWordBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            Grid.SetRow(_multiWordBox, 1);
            chunkGrid.Children.Add(_multiWordBox);

            var chunkControls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
            _chunkRecordBtn = CreateStyledButton("🔴 Record Multi-Word Chunk", (s, e) => ToggleMultiWordChunkRecording(), isPrimary: true);
            _chunkRecordBtn.Width = 240;
            chunkControls.Children.Add(_chunkRecordBtn);

            Grid.SetRow(chunkControls, 2);
            chunkGrid.Children.Add(chunkControls);

            chunkTab.Content = chunkGrid;
            tabControl.Items.Add(chunkTab);

            // ── TAB 4: 📜 Guided Script Reader Wizard ──────────────────────────────
            var scriptTab = new TabItem { Header = "📜 Guided Script Reader" };
            var scriptGrid = new Grid { Margin = new Thickness(14) };
            scriptGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            scriptGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            scriptGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var scriptHeaderStack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            _scriptCounterText = new TextBlock { Text = "Prompt 1 of 10", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Brushes.Cyan };
            scriptHeaderStack.Children.Add(_scriptCounterText);

            _scriptProgressBar = new ProgressBar { Height = 10, Minimum = 0, Maximum = 10, Value = 1, Margin = new Thickness(0, 6, 0, 0), Foreground = Brushes.Cyan };
            scriptHeaderStack.Children.Add(_scriptProgressBar);
            Grid.SetRow(scriptHeaderStack, 0);
            scriptGrid.Children.Add(scriptHeaderStack);

            var promptCard2 = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(35, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 56, 189, 248)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var promptStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            promptStack.Children.Add(new TextBlock { Text = "Read the sentence out loud into your microphone:", FontSize = 12, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 10) });

            _scriptPromptText = new TextBlock
            {
                Text = $"\"{_trainingPrompts[0]}\"",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            };
            promptStack.Children.Add(_scriptPromptText);
            promptCard2.Child = promptStack;

            Grid.SetRow(promptCard2, 1);
            scriptGrid.Children.Add(promptCard2);

            var scriptBtnGrid = new Grid();
            scriptBtnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            scriptBtnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            scriptBtnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var prevBtn = CreateStyledButton("⏮️ Prev Prompt", (s, e) => NavigateScriptPrompt(-1));
            Grid.SetColumn(prevBtn, 0);
            scriptBtnGrid.Children.Add(prevBtn);

            var scriptCenterStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            _scriptRecordBtn = CreateStyledButton("🔴 Record Prompt", (s, e) => ToggleScriptRecording(), isPrimary: true);
            _scriptRecordBtn.Width = 140;

            _scriptPlayBtn = CreateStyledButton("▶ Playback", (s, e) => PlayScriptSample());
            _scriptPlayBtn.Width = 120;
            _scriptPlayBtn.IsEnabled = false;

            scriptCenterStack.Children.Add(_scriptRecordBtn);
            scriptCenterStack.Children.Add(_scriptPlayBtn);
            Grid.SetColumn(scriptCenterStack, 1);
            scriptBtnGrid.Children.Add(scriptCenterStack);

            var nextBtn = CreateStyledButton("Next Prompt ➡️", (s, e) => NavigateScriptPrompt(1));
            Grid.SetColumn(nextBtn, 2);
            scriptBtnGrid.Children.Add(nextBtn);

            Grid.SetRow(scriptBtnGrid, 2);
            scriptGrid.Children.Add(scriptBtnGrid);

            scriptTab.Content = scriptGrid;
            tabControl.Items.Add(scriptTab);

            // ── TAB 5: Voice Shortcuts & Calibration ────────────────────────────────
            var calibrationTab = new TabItem { Header = "⚙ Calibration & Shortcuts" };
            var calibGrid = new Grid { Margin = new Thickness(14) };
            calibGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            calibGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var calibStack = new StackPanel();
            calibStack.Children.Add(new TextBlock { Text = "Voice Recognition Sensitivity Threshold:", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.LightGray });

            var sliderGrid = new Grid { Margin = new Thickness(0, 8, 0, 16) };
            sliderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sliderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _sensitivitySlider = new Slider
            {
                Minimum = 0.1,
                Maximum = 1.0,
                Value = VoiceTrainerManager.Profile.SensitivityThreshold,
                TickFrequency = 0.05,
                IsSnapToTickEnabled = true
            };
            _sensitivitySlider.ValueChanged += (s, e) =>
            {
                VoiceTrainerManager.Profile.SensitivityThreshold = Math.Round(_sensitivitySlider.Value, 2);
                if (_sensitivityValText != null) _sensitivityValText.Text = $"{VoiceTrainerManager.Profile.SensitivityThreshold * 100}%";
                VoiceTrainerManager.SaveProfile();
            };
            Grid.SetColumn(_sensitivitySlider, 0);
            sliderGrid.Children.Add(_sensitivitySlider);

            _sensitivityValText = new TextBlock
            {
                Text = $"{VoiceTrainerManager.Profile.SensitivityThreshold * 100}%",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Cyan,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_sensitivityValText, 1);
            sliderGrid.Children.Add(_sensitivityValText);

            calibStack.Children.Add(sliderGrid);
            Grid.SetRow(calibStack, 0);
            calibGrid.Children.Add(calibStack);

            var shortcutsScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _shortcutsPanel = new StackPanel();
            shortcutsScroll.Content = _shortcutsPanel;
            Grid.SetRow(shortcutsScroll, 1);
            calibGrid.Children.Add(shortcutsScroll);

            calibrationTab.Content = calibGrid;
            tabControl.Items.Add(calibrationTab);

            Grid.SetRow(tabControl, 1);
            mainGrid.Children.Add(tabControl);

            _statusText = new TextBlock
            {
                Text = "Ready to record voice samples and train speech profiles.",
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(4, 6, 0, 0)
            };
            Grid.SetRow(_statusText, 2);
            mainGrid.Children.Add(_statusText);

            this.UserContent = mainGrid;

            // Audio Meter & Endless Auto-Advancing Teleprompter Timer
            _levelTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            _levelTimer.Tick += (s, e) => UpdateAudioMeterAndTeleprompter();
            _levelTimer.Start();

            RefreshSamplesList();
            RefreshShortcutsList();

            this.Closed += (s, e) =>
            {
                _levelTimer.Stop();
                if (_isEndlessActive) VoiceTrainerManager.StopRecording();
                _instance = null;
            };
        }

        private void ToggleEndlessHandsFreeMode()
        {
            string currentWord = _endlessWordBank[_endlessWordIndex];

            if (VoiceTrainerManager.IsRecording)
            {
                // Stop recording & submit word to trained profile!
                var sample = VoiceTrainerManager.StopRecording(currentWord);
                _endlessToggleBtn.Content = "🔴 Record Word";

                if (sample != null)
                {
                    _endlessTrainedCount++;
                    _endlessCountText.Text = $"♾️ Controlled Voice Teleprompter • Words Trained: {_endlessTrainedCount}";
                    _statusText.Text = $"✅ Submitted recording for \"{currentWord}\"!";
                    TextOverlay.Show($"✅ Saved sample for \"{currentWord}\"!", 2000);
                    try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
                    RefreshSamplesList();
                }

                // Advance to next word
                AdvanceEndlessWord();
            }
            else
            {
                // Start recording active word
                VoiceTrainerManager.StartRecording(currentWord);
                _endlessToggleBtn.Content = "⏹ Stop & Submit Word";
                _statusText.Text = $"🔴 Recording: \"{currentWord}\"... Click 'Stop & Submit' when finished speaking.";
                TextOverlay.Show($"🔴 Recording: \"{currentWord}\"", 1500);
            }
        }

        private void AdvanceEndlessWord()
        {
            _endlessWordIndex = (_endlessWordIndex + 1) % _endlessWordBank.Length;
            int nextIndex = (_endlessWordIndex + 1) % _endlessWordBank.Length;

            _endlessCurrentWordText.Text = $"\"{_endlessWordBank[_endlessWordIndex]}\"";
            _endlessNextWordText.Text = $"Next Word: \"{_endlessWordBank[nextIndex]}\"";
            _endlessToggleBtn.Content = "🔴 Record Word";
            _statusText.Text = $"Current Word: \"{_endlessWordBank[_endlessWordIndex]}\". Click 'Record Word' to begin.";
        }

        private void UpdateAudioMeterAndTeleprompter()
        {
            if (VoiceTrainerManager.IsRecording)
            {
                double volume = VoiceTrainerManager.GetLiveAudioLevel();
                _audioLevelBar.Value = Math.Min(100, Math.Max(0, volume * 1.5));
            }
            else
            {
                _audioLevelBar.Value = Math.Max(0, _audioLevelBar.Value - 10);
            }
        }

        private void ToggleMultiWordChunkRecording()
        {
            if (VoiceTrainerManager.IsRecording)
            {
                string text = _multiWordBox.Text.Trim();
                var samples = VoiceTrainerManager.StopRecordingAndChunkWords(text);
                _chunkRecordBtn.Content = "🔴 Record Multi-Word Chunk";
                _statusText.Text = $"⚡ Sliced & saved {samples.Count} individual word samples in 1 go!";
                TextOverlay.Show($"⚡ Auto-chunked {samples.Count} word samples into voice profile!", 3000);
                RefreshSamplesList();
            }
            else
            {
                string text = _multiWordBox.Text.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    TextOverlay.Show("⚠️ Enter a multi-word paragraph first!", 2000);
                    return;
                }
                VoiceTrainerManager.StartRecording("Multi-Word Chunk");
                _chunkRecordBtn.Content = "⏹ Stop & Auto-Chunk Words";
                _statusText.Text = "🔴 Recording multi-word paragraph continuously...";
            }
        }

        private void NavigateScriptPrompt(int delta)
        {
            _scriptIndex += delta;
            if (_scriptIndex < 0) _scriptIndex = 0;
            if (_scriptIndex >= _trainingPrompts.Length) _scriptIndex = _trainingPrompts.Length - 1;

            _scriptCounterText.Text = $"Prompt {_scriptIndex + 1} of {_trainingPrompts.Length}";
            _scriptProgressBar.Value = _scriptIndex + 1;
            _scriptPromptText.Text = $"\"{_trainingPrompts[_scriptIndex]}\"";
            _lastScriptSample = null;
            _scriptPlayBtn.IsEnabled = false;
        }

        private void ToggleScriptRecording()
        {
            if (VoiceTrainerManager.IsRecording)
            {
                _lastScriptSample = VoiceTrainerManager.StopRecording();
                _scriptRecordBtn.Content = "🔴 Record Prompt";
                if (_lastScriptSample != null)
                {
                    _lastScriptSample.Phrase = _trainingPrompts[_scriptIndex];
                    VoiceTrainerManager.SaveVoiceSample(_lastScriptSample);
                    _scriptPlayBtn.IsEnabled = true;
                    _statusText.Text = $"✅ Saved recording for Prompt {_scriptIndex + 1}!";
                    RefreshSamplesList();
                }
            }
            else
            {
                VoiceTrainerManager.StartRecording(_trainingPrompts[_scriptIndex]);
                _scriptRecordBtn.Content = "⏹ Stop Recording";
                _statusText.Text = $"🔴 Recording Prompt {_scriptIndex + 1}...";
            }
        }

        private void PlayScriptSample()
        {
            if (_lastScriptSample != null)
            {
                VoiceTrainerManager.PlaySample(_lastScriptSample);
            }
        }

        private void ToggleRecording()
        {
            if (VoiceTrainerManager.IsRecording)
            {
                _lastRecordedSample = VoiceTrainerManager.StopRecording();
                _recordBtn.Content = "🔴 Start Recording";
                if (_lastRecordedSample != null)
                {
                    _playBtn.IsEnabled = true;
                    _statusText.Text = $"Recorded sample ({_lastRecordedSample.DurationSeconds:F1}s)";
                }
            }
            else
            {
                string phrase = _phraseBox.Text.Trim();
                if (string.IsNullOrEmpty(phrase)) phrase = "Hey Jarvis";

                VoiceTrainerManager.StartRecording(phrase);
                _recordBtn.Content = "⏹ Stop Recording";
                _statusText.Text = $"Recording sample for \"{phrase}\"...";
            }
        }

        private void PlayLastSample()
        {
            if (_lastRecordedSample != null)
            {
                VoiceTrainerManager.PlaySample(_lastRecordedSample);
            }
        }

        private void SaveVoiceCommand()
        {
            if (_lastRecordedSample == null)
            {
                TextOverlay.Show("⚠️ Record an audio sample first!", 2000);
                return;
            }

            string phrase = _phraseBox.Text.Trim();
            string command = _commandBox.Text.Trim();

            if (string.IsNullOrEmpty(phrase))
            {
                TextOverlay.Show("⚠️ Enter a target phrase!", 2000);
                return;
            }

            _lastRecordedSample.Phrase = phrase;
            _lastRecordedSample.AssociatedCommand = command;

            VoiceTrainerManager.SaveVoiceSample(_lastRecordedSample);

            if (!string.IsNullOrEmpty(command))
            {
                VoiceTrainerManager.SetCustomVoiceShortcut(phrase, command);
            }

            TextOverlay.Show($"💾 Saved Voice Command: \"{phrase}\"!", 2500);
            _statusText.Text = $"Saved voice command for \"{phrase}\"";

            RefreshSamplesList();
            RefreshShortcutsList();
        }

        private void RefreshSamplesList()
        {
            _samplesPanel.Children.Clear();
            var samples = VoiceTrainerManager.Profile.Samples;

            if (samples.Count == 0)
            {
                _samplesPanel.Children.Add(new TextBlock
                {
                    Text = "No trained voice samples yet. Click 'Start Recording' above!",
                    FontSize = 11,
                    Foreground = Brushes.DarkGray,
                    Margin = new Thickness(4)
                });
                return;
            }

            foreach (var s in samples)
            {
                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var infoStack = new StackPanel();
                infoStack.Children.Add(new TextBlock { Text = $"🗣 \"{s.Phrase}\"", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
                infoStack.Children.Add(new TextBlock { Text = $"{s.RecordedAt:yyyy-MM-dd HH:mm} • {s.DurationSeconds:F1}s", FontSize = 10, Foreground = Brushes.Gray });

                Grid.SetColumn(infoStack, 0);
                grid.Children.Add(infoStack);

                var btnsStack = new StackPanel { Orientation = Orientation.Horizontal };
                var playBtn = CreateStyledButton("▶ Play", (snd, ev) => VoiceTrainerManager.PlaySample(s));
                playBtn.Padding = new Thickness(6, 2, 6, 2);
                playBtn.Margin = new Thickness(0, 0, 4, 0);

                var delBtn = CreateStyledButton("🗑", (snd, ev) =>
                {
                    VoiceTrainerManager.DeleteSample(s.Id);
                    RefreshSamplesList();
                });
                delBtn.Padding = new Thickness(6, 2, 6, 2);

                btnsStack.Children.Add(playBtn);
                btnsStack.Children.Add(delBtn);
                Grid.SetColumn(btnsStack, 1);
                grid.Children.Add(btnsStack);

                card.Child = grid;
                _samplesPanel.Children.Add(card);
            }
        }

        private void RefreshShortcutsList()
        {
            _shortcutsPanel.Children.Clear();
            var shortcuts = VoiceTrainerManager.Profile.CustomVoiceShortcuts;

            if (shortcuts.Count == 0)
            {
                _shortcutsPanel.Children.Add(new TextBlock
                {
                    Text = "No custom voice shortcuts mapped yet.",
                    FontSize = 11,
                    Foreground = Brushes.DarkGray,
                    Margin = new Thickness(4)
                });
                return;
            }

            foreach (var kvp in shortcuts)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var text = new TextBlock
                {
                    Text = $"🗣 \"{kvp.Key}\"  ➜  ⚡ {kvp.Value}",
                    FontSize = 12,
                    Foreground = Brushes.LightCyan,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(text, 0);
                row.Children.Add(text);

                var delBtn = CreateStyledButton("🗑", (s, e) =>
                {
                    VoiceTrainerManager.RemoveCustomVoiceShortcut(kvp.Key);
                    RefreshShortcutsList();
                });
                delBtn.Padding = new Thickness(6, 2, 6, 2);
                Grid.SetColumn(delBtn, 1);
                row.Children.Add(delBtn);

                _shortcutsPanel.Children.Add(row);
            }
        }
    }
}
