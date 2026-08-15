// Developer: heaplyn
// Date: 2026-08-13
// Summary: Customizer overlay for animations, transition speeds, opacity, drop shadow glow, and HUD visual effects.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JarvisLauncher
{
    public class AnimationOptionsOverlay : BaseOverlay
    {
        private static AnimationOptionsOverlay? _instance;

        public static void ShowOverlay()
        {
            if (_instance == null || !_instance.IsLoaded)
            {
                _instance = new AnimationOptionsOverlay();
            }
            _instance.Show();
            _instance.BringToFront();
            _instance.Focus();
        }

        private CheckBox _enableAnimCheck;
        private ComboBox _speedCombo;
        private Slider _opacitySlider;
        private TextBlock _opacityValText;
        private Slider _textOpacitySlider;
        private TextBlock _textOpacityValText;
        private TextBlock _statusText;

        public AnimationOptionsOverlay() : base("✨ JARVIS ANIMATIONS & VISUAL EFFECTS OPTIONS", 680, 480)
        {
            var mainGrid = new Grid { Margin = new Thickness(14) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "Visual Animations & HUD Motion Settings",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Cyan,
                Margin = new Thickness(0, 0, 0, 14)
            });

            // 1. Enable Animations Checkbox
            _enableAnimCheck = new CheckBox
            {
                Content = "Enable Motion Animations (Slide-in / Fade-out transitions)",
                IsChecked = SettingsManager.Current.ENABLE_ANIMATIONS,
                FontSize = 12,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 14)
            };
            _enableAnimCheck.Click += (s, e) =>
            {
                SettingsManager.Current.ENABLE_ANIMATIONS = _enableAnimCheck.IsChecked == true;
                SettingsManager.Save();
                UpdateStatus("Saved animation state preference.");
            };
            stack.Children.Add(_enableAnimCheck);

            // 2. Speed Preset Combo
            var speedGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            speedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            speedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var speedLabel = new TextBlock { Text = "Transition Speed Preset:", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(speedLabel, 0);
            speedGrid.Children.Add(speedLabel);

            _speedCombo = new ComboBox
            {
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 12
            };
            _speedCombo.Items.Add("⚡ Fast (120ms)");
            _speedCombo.Items.Add("🎬 Standard (220ms)");
            _speedCombo.Items.Add("✨ Smooth Spring (350ms)");
            _speedCombo.SelectedIndex = 1;
            _speedCombo.SelectionChanged += (s, e) => UpdateStatus($"Selected speed preset: {_speedCombo.SelectedItem}");
            Grid.SetColumn(_speedCombo, 1);
            speedGrid.Children.Add(_speedCombo);

            stack.Children.Add(speedGrid);

            // 3. Window Opacity Slider
            stack.Children.Add(new TextBlock { Text = "Overall Overlay Window Fill Opacity:", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.LightGray, Margin = new Thickness(0, 4, 0, 4) });

            var opGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            opGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            opGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _opacitySlider = new Slider
            {
                Minimum = 0.3,
                Maximum = 1.0,
                Value = SettingsManager.Current.WINDOW_OPACITY,
                TickFrequency = 0.05,
                IsSnapToTickEnabled = true
            };
            _opacitySlider.ValueChanged += (s, e) =>
            {
                SettingsManager.Current.WINDOW_OPACITY = Math.Round(_opacitySlider.Value, 2);
                if (_opacityValText != null) _opacityValText.Text = $"{Math.Round(SettingsManager.Current.WINDOW_OPACITY * 100)}%";
                SettingsManager.Save();
                UpdateStatus($"Updated window opacity to {Math.Round(SettingsManager.Current.WINDOW_OPACITY * 100)}%");
            };
            Grid.SetColumn(_opacitySlider, 0);
            opGrid.Children.Add(_opacitySlider);

            _opacityValText = new TextBlock
            {
                Text = $"{Math.Round(SettingsManager.Current.WINDOW_OPACITY * 100)}%",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Cyan,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_opacityValText, 1);
            opGrid.Children.Add(_opacityValText);

            stack.Children.Add(opGrid);

            // 4. Text Opacity Slider
            stack.Children.Add(new TextBlock { Text = "Text & Foreground Opacity Level:", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.LightGray, Margin = new Thickness(0, 4, 0, 4) });

            var textOpGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            textOpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            textOpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _textOpacitySlider = new Slider
            {
                Minimum = 0.2,
                Maximum = 1.0,
                Value = 1.0,
                TickFrequency = 0.05,
                IsSnapToTickEnabled = true
            };
            _textOpacitySlider.ValueChanged += (s, e) =>
            {
                double val = Math.Round(_textOpacitySlider.Value, 2);
                if (_textOpacityValText != null) _textOpacityValText.Text = $"{Math.Round(val * 100)}%";
                CommandParser.TriggerTextOpacityChange(val);
                UpdateStatus($"Updated text opacity to {Math.Round(val * 100)}%");
            };
            Grid.SetColumn(_textOpacitySlider, 0);
            textOpGrid.Children.Add(_textOpacitySlider);

            _textOpacityValText = new TextBlock
            {
                Text = "100%",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Cyan,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_textOpacityValText, 1);
            textOpGrid.Children.Add(_textOpacityValText);

            stack.Children.Add(textOpGrid);

            Grid.SetRow(stack, 0);
            mainGrid.Children.Add(stack);

            // Footer Status
            _statusText = new TextBlock
            {
                Text = "Animation settings active.",
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(4, 6, 0, 0)
            };
            Grid.SetRow(_statusText, 1);
            mainGrid.Children.Add(_statusText);

            this.UserContent = mainGrid;
        }

        private void UpdateStatus(string msg)
        {
            if (_statusText != null)
            {
                _statusText.Text = $"✅ {msg}";
                _statusText.Foreground = Brushes.LightGreen;
            }
        }
    }
}
