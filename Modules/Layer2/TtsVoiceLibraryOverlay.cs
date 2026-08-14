// Developer: heaplyn
// Date: 2026-08-13
// Summary: Custom TTS Voice Library & System Voice Selector Overlay.
// Provides Windows installed voice selection (David, Zira, Mark, etc.), speed/volume sliders, test speech button, & GitHub voice samples.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JarvisLauncher
{
    public class TtsVoiceLibraryOverlay : BaseOverlay
    {
        private static TtsVoiceLibraryOverlay? _instance;
        private ComboBox _voiceCombo = null!;
        private Slider _speedSlider = null!;
        private Slider _volumeSlider = null!;
        private StackPanel _voiceStack = null!;
        private TextBlock _statusText = null!;

        public TtsVoiceLibraryOverlay()
            : base("TTS VOICE SELECTOR & STUDIO", width: 540, height: 720)
        {
            var workArea = SystemParameters.WorkArea;
            this.Left = (workArea.Width - this.Width) / 2;
            this.Top = (workArea.Height - this.Height) / 2;

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root = new StackPanel { Margin = new Thickness(6) };
            scroll.Content = root;

            // ── Section 1: Windows System TTS Voices ───────────────────────────
            root.Children.Add(CreateHeader("🔊 Installed Windows System Voices"));

            var installedVoices = TtsManager.GetInstalledVoices();
            _voiceCombo = new ComboBox
            {
                Margin = new Thickness(0, 4, 0, 8),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13
            };
            foreach (var v in installedVoices)
            {
                _voiceCombo.Items.Add(v);
            }

            string currentVoice = SettingsManager.Current.SelectedTtsVoice;
            if (!string.IsNullOrEmpty(currentVoice) && _voiceCombo.Items.Contains(currentVoice))
            {
                _voiceCombo.SelectedItem = currentVoice;
            }
            else if (_voiceCombo.Items.Count > 0)
            {
                _voiceCombo.SelectedIndex = 0;
            }

            _voiceCombo.SelectionChanged += (s, e) =>
            {
                if (_voiceCombo.SelectedItem is string sel)
                {
                    TtsManager.SetVoice(sel);
                }
            };
            root.Children.Add(_voiceCombo);

            // Speech Rate / Speed Slider
            root.Children.Add(CreateLabel($"Speech Speed (-10 Slow ... +10 Fast):"));
            _speedSlider = new Slider
            {
                Minimum = -10,
                Maximum = 10,
                Value = SettingsManager.Current.TtsSpeechRate,
                SmallChange = 1,
                LargeChange = 2,
                Margin = new Thickness(0, 2, 0, 6)
            };
            _speedSlider.ValueChanged += (s, e) =>
            {
                TtsManager.SetRate((int)_speedSlider.Value);
            };
            root.Children.Add(_speedSlider);

            // Speech Volume Slider
            root.Children.Add(CreateLabel("Speech Volume (0 Quiet ... 100 Loud):"));
            _volumeSlider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Value = SettingsManager.Current.TtsSpeechVolume,
                SmallChange = 5,
                LargeChange = 10,
                Margin = new Thickness(0, 2, 0, 8)
            };
            _volumeSlider.ValueChanged += (s, e) =>
            {
                TtsManager.SetVolume((int)_volumeSlider.Value);
            };
            root.Children.Add(_volumeSlider);

            // Test Voice Button
            var testVoiceBtn = CreateButton("⚡ Test Selected Voice & AI Speech");
            testVoiceBtn.Height = 32;
            testVoiceBtn.FontWeight = FontWeights.Bold;
            testVoiceBtn.Click += (s, e) =>
            {
                string sel = (_voiceCombo.SelectedItem as string) ?? "Microsoft Voice";
                TtsManager.Speak($"Hello! I am Jarvis, speaking with your chosen voice: {sel}.", isShortSpeech: false);
            };
            root.Children.Add(testVoiceBtn);

            // ── Section 2: GitHub Custom TTS Voices ─────────────────────────────
            root.Children.Add(CreateHeader("🌐 GitHub Custom TTS Voices (yaph/tts-samples)"));

            var info = new TextBlock
            {
                Text = "Browse, preview, and select custom TTS voice MP3 samples directly from the yaph/tts-samples GitHub repository.",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            info.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            root.Children.Add(info);

            var refreshBtn = CreateButton("🔄 Refresh Voice Library from GitHub");
            refreshBtn.Click += async (s, e) => await LoadVoiceSamplesAsync();
            root.Children.Add(refreshBtn);

            // ── Voice List Stack ──────────────────────────────────────────────────────
            root.Children.Add(CreateHeader("🎵 GitHub MP3 Voice Samples"));
            _voiceStack = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
            root.Children.Add(_voiceStack);

            _statusText = new TextBlock
            {
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };
            _statusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            root.Children.Add(_statusText);

            this.UserContent = scroll;

            // Load samples on launch
            Task.Run(async () => await LoadVoiceSamplesAsync());
        }

        private async Task LoadVoiceSamplesAsync()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _voiceStack.Children.Clear();
                var loading = new TextBlock { Text = "⏳ Fetching TTS voice samples from GitHub...", FontSize = 12, FontStyle = FontStyles.Italic };
                loading.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                _voiceStack.Children.Add(loading);
            });

            var voices = await TtsSampleDownloader.FetchVoiceSamplesAsync();

            Application.Current.Dispatcher.Invoke(() =>
            {
                _voiceStack.Children.Clear();
                if (voices.Count == 0)
                {
                    var empty = new TextBlock { Text = "No voice samples returned from GitHub repo.", FontSize = 12, FontStyle = FontStyles.Italic };
                    empty.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                    _voiceStack.Children.Add(empty);
                    return;
                }

                foreach (var voice in voices)
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
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var txt = new TextBlock
                    {
                        Text = $"🎵 {voice.name}",
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    txt.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    Grid.SetColumn(txt, 0);
                    grid.Children.Add(txt);

                    var prevBtn = CreateButton("🔊 Preview");
                    var targetVoice = voice;
                    prevBtn.Click += async (s, e) => await TtsSampleDownloader.PreviewVoiceSampleAsync(targetVoice);
                    Grid.SetColumn(prevBtn, 1);
                    grid.Children.Add(prevBtn);

                    var setBtn = CreateButton("📥 Set Active");
                    setBtn.Margin = new Thickness(4, 0, 0, 0);
                    setBtn.Click += async (s, e) =>
                    {
                        await TtsSampleDownloader.SetCustomVoiceSampleAsync(targetVoice);
                        this.FadeOutAndClose();
                    };
                    Grid.SetColumn(setBtn, 2);
                    grid.Children.Add(setBtn);

                    card.Child = grid;
                    _voiceStack.Children.Add(card);
                }
            });
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

        private static Button CreateButton(string content)
        {
            var btn = new Button
            {
                Content = content,
                Margin = new Thickness(0, 2, 0, 2),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11,
                Cursor = Cursors.Hand
            };
            return btn;
        }

        public static void ShowOverlay()
        {
            if (_instance == null || !_instance.IsLoaded)
            {
                _instance = new TtsVoiceLibraryOverlay();
                _instance.Show();
            }
            else
            {
                _instance.Activate();
            }
        }
    }
}
