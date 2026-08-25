// Developer: heaplyn
// Date: 2026-08-21
// Summary: High-performance Outlined Text control for Jarvis HUD.
//          Supports N-Amount of layered strokes, Shadows, Italics, Glow, and Wobbliness.
//          Dynamic category-based profiling.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;

namespace JarvisLauncher
{
    public class OutlinedText : Control
    {
        private static readonly List<WeakReference<OutlinedText>> _instances = new List<WeakReference<OutlinedText>>();
        private static DispatcherTimer? _wobbleTimer;
        private static double _wobblePhase = 0;

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(OutlinedText), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));
        public string Text { get => (string)GetValue(TextProperty) ?? ""; set => SetValue(TextProperty, value); }

        public static readonly DependencyProperty TextAlignmentProperty = DependencyProperty.Register("TextAlignment", typeof(TextAlignment), typeof(OutlinedText), new FrameworkPropertyMetadata(TextAlignment.Left, FrameworkPropertyMetadataOptions.AffectsRender));
        public TextAlignment TextAlignment { get => (TextAlignment)GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }

        public static readonly DependencyProperty CategoryProperty = DependencyProperty.Register("Category", typeof(string), typeof(OutlinedText), new FrameworkPropertyMetadata("Labels", FrameworkPropertyMetadataOptions.AffectsRender));
        public string Category { get => (string)GetValue(CategoryProperty); set => SetValue(CategoryProperty, value); }

        public OutlinedText()
        {
            lock (_instances) _instances.Add(new WeakReference<OutlinedText>(this));
            this.Background = Brushes.Transparent;
            this.IsHitTestVisible = false;
            EnsureWobbleTimer();
        }

        private void EnsureWobbleTimer()
        {
            if (_wobbleTimer == null)
            {
                _wobbleTimer = new DispatcherTimer(DispatcherPriority.Render);
                _wobbleTimer.Interval = TimeSpan.FromMilliseconds(33);
                _wobbleTimer.Tick += (s, e) => {
                    _wobblePhase += 0.1 * SettingsManager.Current.TEXT_WOBBLE_SPEED;
                    if (_wobblePhase > Math.PI * 2) _wobblePhase -= Math.PI * 2;
                    InvalidateAll();
                };
                _wobbleTimer.Start();
            }
        }

        public static void InvalidateAll()
        {
            lock (_instances)
            {
                foreach (var wr in _instances.ToList())
                {
                    if (wr.TryGetTarget(out var target)) target.InvalidateVisual();
                    else _instances.Remove(wr);
                }
            }
        }

        public static void ClearCache()
        {
            // For now, just trigger a global re-render.
            // In the future, if we add image-based caching, this would clear it.
            InvalidateAll();
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (string.IsNullOrEmpty(Text)) return;

            var set = SettingsManager.Current;
            var prof = (set.TEXT_PROFILES != null && set.TEXT_PROFILES.ContainsKey(Category)) ? set.TEXT_PROFILES[Category] : new TextVisualProfile();

            var fontFamily = (prof.FontFamily != "Segoe UI" && prof.FontFamily != null) ? new FontFamily(prof.FontFamily) : (FontFamily)Application.Current.Resources["GlobalFontFamily"] ?? FontFamily;
            double fontSize = FontSize;
            if (double.IsNaN(fontSize) || fontSize <= 0) fontSize = set.GLOBAL_TEXT_SIZE;
            var foreground = Foreground ?? Brushes.White;
            var fontStyle = (prof.IsItalic || set.TEXT_IS_ITALIC) ? FontStyles.Italic : FontStyles.Normal;

            var ft = new FormattedText(Text, CultureInfo.CurrentCulture, this.FlowDirection, new Typeface(fontFamily, fontStyle, FontWeight, FontStretch), fontSize, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            ft.TextAlignment = this.TextAlignment;

            var strokes = prof.Strokes != null && prof.Strokes.Any() ? prof.Strokes : set.TEXT_STROKES;
            double maxStroke = (set.ENABLE_TEXT_STROKE && strokes != null && strokes.Any()) ? strokes.Max(s => s.Thickness) : 0;
            double shadowX = prof.ShadowOffsetX != 0 ? prof.ShadowOffsetX : set.TEXT_SHADOW_OFFSET_X;
            double shadowY = prof.ShadowOffsetY != 0 ? prof.ShadowOffsetY : set.TEXT_SHADOW_OFFSET_Y;
            double glowAmount = prof.GlowAmount > 0 ? prof.GlowAmount : set.TEXT_GLOW_AMOUNT;

            double wobbleX = 0, wobbleY = 0;
            if (set.TEXT_WOBBLINESS > 0) { wobbleX = Math.Sin(_wobblePhase) * set.TEXT_WOBBLINESS; wobbleY = Math.Cos(_wobblePhase * 0.7) * set.TEXT_WOBBLINESS; }

            var origin = new Point(maxStroke + Math.Max(0, -shadowX) + 5 + wobbleX, maxStroke + Math.Max(0, -shadowY) + 5 + wobbleY);
            var geometry = ft.BuildGeometry(origin);

            if (glowAmount > 0) {
                try { var gColor = (Color)ColorConverter.ConvertFromString(prof.GlowAmount > 0 ? prof.GlowColor : set.TEXT_GLOW_COLOR); dc.DrawGeometry(null, new Pen(new SolidColorBrush(gColor), glowAmount * 2), geometry); } catch { }
            }

            bool showShadow = set.ENABLE_TEXT_SHADOW && prof.EnableShadow;
            if (showShadow && (shadowX != 0 || shadowY != 0)) {
                try { var sColor = (Color)ColorConverter.ConvertFromString(prof.ShadowOffsetX != 0 ? prof.ShadowColor : set.TEXT_SHADOW_COLOR); var sGeometry = ft.BuildGeometry(new Point(origin.X + shadowX, origin.Y + shadowY)); dc.DrawGeometry(new SolidColorBrush(sColor), null, sGeometry); } catch { }
            }

            if (set.ENABLE_TEXT_STROKE && strokes != null) {
                Enum.TryParse(set.TEXT_STROKE_LINE_JOIN, out PenLineJoin join);
                foreach (var stroke in strokes.OrderByDescending(s => s.Thickness)) {
                    try { var color = (Color)ColorConverter.ConvertFromString(stroke.Color); dc.DrawGeometry(null, new Pen(new SolidColorBrush(color), stroke.Thickness * 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = join }, geometry); } catch { }
                }
            }

            if (set.ENABLE_CHROMA_SHIFT)
            {
                double amt = set.CHROMA_SHIFT_AMOUNT;
                dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(128, 255, 0, 0)), null, ft.BuildGeometry(new Point(origin.X - amt, origin.Y)));
                dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(128, 0, 0, 255)), null, ft.BuildGeometry(new Point(origin.X + amt, origin.Y)));
            }

            dc.DrawGeometry(foreground, null, geometry);
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (string.IsNullOrEmpty(Text)) return new Size(0,0);
            var set = SettingsManager.Current;
            var prof = (set.TEXT_PROFILES != null && set.TEXT_PROFILES.ContainsKey(Category)) ? set.TEXT_PROFILES[Category] : new TextVisualProfile();
            var fontFamily = (prof.FontFamily != "Segoe UI" && prof.FontFamily != null) ? new FontFamily(prof.FontFamily) : (FontFamily)Application.Current.Resources["GlobalFontFamily"] ?? FontFamily;
            var ft = new FormattedText(Text, CultureInfo.CurrentCulture, this.FlowDirection, new Typeface(fontFamily, (prof.IsItalic || set.TEXT_IS_ITALIC) ? FontStyles.Italic : FontStyles.Normal, FontWeight, FontStretch), FontSize > 0 ? FontSize : set.GLOBAL_TEXT_SIZE, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            double buffer = (SettingsManager.Current.TEXT_WOBBLINESS + 10) * 2 + 30;
            return new Size(ft.Width + buffer, ft.Height + buffer);
        }
    }
}
