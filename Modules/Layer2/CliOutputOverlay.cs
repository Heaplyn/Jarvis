// Developer: heaplyn
// Date: 2026-08-09
// Summary: Draggable, scrollable console terminal output window styled in retro green and monospaced font.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JarvisLauncher
{
    public class CliOutputOverlay : BaseOverlay
    {
        public static void Show(string commandTitle, string outputContent)
        {
            // Execute on UI Dispatcher Thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                var overlay = new CliOutputOverlay(commandTitle, outputContent);
                overlay.Show();
            });
        }

        private CliOutputOverlay(string commandTitle, string outputContent)
            : base($"TERMINAL OUTPUT: {commandTitle.ToUpper()}", width: 650, height: 420)
        {
            var textBox = new TextBox
            {
                Text = string.IsNullOrEmpty(outputContent) ? "[No Output Returned]" : outputContent,
                IsReadOnly = true,
                AcceptsReturn = true,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 66)), // Retro terminal green
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 13,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(4)
            };

            this.UserContent = textBox;
        }
    }
}
