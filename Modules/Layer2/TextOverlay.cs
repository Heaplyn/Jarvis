// Developer: heaplyn
// Date: 2026-08-09
// Summary: Draggable text notification overlay inheriting BaseOverlay that auto-closes after a set duration.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace JarvisLauncher
{
    public class TextOverlay : BaseOverlay
    {
        private static TextOverlay? _lastOverlay;
        private static string _lastText = string.Empty;

        public static void Show(
            string text, 
            int durationMs = 1500, 
            double width = 350, 
            double height = 120, 
            double fontSize = 20, 
            string backgroundColor = "#F2140D24", 
            string textColor = "#FFFFFF",
            string borderColor = "#808050E6")
        {
            if (string.IsNullOrEmpty(text)) return;

            // Execute on UI Dispatcher Thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Simple Debounce: Don't show the exact same message if one is already visible
                if (_lastOverlay != null && _lastOverlay.IsVisible && _lastText == text)
                {
                    return;
                }

                // Close previous toast to prevent stacking if it's the same type of notification
                if (_lastOverlay != null && _lastOverlay.IsVisible)
                {
                    _lastOverlay.FadeOutAndClose();
                }

                var overlay = new TextOverlay(text, width, height, fontSize, backgroundColor, textColor, borderColor);
                _lastOverlay = overlay;
                _lastText = text;
                overlay.Show();

                if (durationMs > 0)
                {
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        if (_lastOverlay == overlay) _lastOverlay = null;
                        overlay.FadeOutAndClose();
                    };
                    timer.Start();
                }
            });
        }

        private TextOverlay(
            string text, 
            double width, 
            double height, 
            double fontSize, 
            string bgColor, 
            string txtColor,
            string bdrColor)
            : base("NOTIFICATION", width, height, bgColor, txtColor, bdrColor)
        {
            var brushConverter = new BrushConverter();
            var txtBrush = (Brush)(brushConverter.ConvertFromString(txtColor) ?? Brushes.White);

            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = txtBrush,
                FontSize = fontSize,
                FontFamily = new FontFamily("Segoe UI Semibold, Arial"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };

            this.UserContent = textBlock;
        }
    }
}
