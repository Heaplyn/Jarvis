// Developer: heaplyn
// Date: 2026-08-09
// Summary: Reusable, glassmorphic input prompt overlay window to gather arguments for CLI commands visually on the screen.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JarvisLauncher
{
    public class InputPromptOverlay : BaseOverlay
    {
        private readonly TextBox _inputTextBox;
        private readonly Action<string> _onSubmit;

        public static void Show(string promptMessage, Action<string> onSubmit, string defaultText = "")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var prompt = new InputPromptOverlay(promptMessage, onSubmit, defaultText);
                prompt.Show();
                prompt.Activate();
            });
        }

        private InputPromptOverlay(string promptMessage, Action<string> onSubmit, string defaultText)
            : base("JARVIS INPUT REQUIRED", width: 420, height: 130)
        {
            _onSubmit = onSubmit;

            var grid = new Grid { Margin = new Thickness(8) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = promptMessage,
                Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI Semibold"),
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            _inputTextBox = new TextBox
            {
                Text = defaultText,
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
                CaretBrush = (Brush)Application.Current.Resources["AccentCaretBrush"],
                BorderBrush = (Brush)Application.Current.Resources["SelectedBorderBrush"],
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 14,
                FontFamily = new FontFamily("Segoe UI")
            };
            _inputTextBox.KeyDown += TextBox_KeyDown;
            Grid.SetRow(_inputTextBox, 1);
            grid.Children.Add(_inputTextBox);

            this.UserContent = grid;

            this.Loaded += (s, e) =>
            {
                _inputTextBox.Focus();
                _inputTextBox.SelectAll();
            };
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string input = _inputTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(input))
                {
                    _onSubmit?.Invoke(input);
                    FadeOutAndClose();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                FadeOutAndClose();
                e.Handled = true;
            }
        }
    }
}
