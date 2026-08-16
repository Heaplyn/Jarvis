// Developer: heaplyn
// Date: 2026-08-09
// Summary: Coordinates dynamic loading, parsing, and applying of window themes (colors, borders, carets, selection states) live via Application Resources.

using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace JarvisLauncher
{
    public static class ThemeManager
    {
        public static void ApplyTheme(string themeName)
        {
            themeName = themeName.Trim().ToLower();

            // Default: Purple Theme Accents
            string bgHex = "#FA1E1035"; 
            string borderHex = "#ffffffe6";
            string caretHex = "#FFD9CCFF";
            string hoverHex = "#1C8050E6";
            string selectedHex = "#338050E6";
            string selectedBorderHex = "#808050E6";
            string gradientStartHex = "#FA301845";
            string gradientEndHex = "#FA100820";

            switch (themeName)
            {
                case "dark":
                case "slate":
                case "charcoal":
                case "black":
                    bgHex = "#FA1A1A1E"; 
                    borderHex = "#2Bffffff";
                    caretHex = "#ffffffff";
                    hoverHex = "#1Cffffff";
                    selectedHex = "#33ffffff";
                    selectedBorderHex = "#66ffffff";
                    gradientStartHex = "#FA2C2C32";
                    gradientEndHex = "#FA0E0E10";
                    break;

                case "blue":
                case "space":
                case "ocean":
                    bgHex = "#FA0C1525"; 
                    borderHex = "#2600bfff";
                    caretHex = "#00bffff9";
                    hoverHex = "#1C00bfff";
                    selectedHex = "#3300bfff";
                    selectedBorderHex = "#8000bfff";
                    gradientStartHex = "#FA182838";
                    gradientEndHex = "#FA04080F";
                    break;

                case "green":
                case "matrix":
                case "terminal":
                case "hacker":
                    bgHex = "#FA04100A"; 
                    borderHex = "#4000ff00";
                    caretHex = "#00ff00ff";
                    hoverHex = "#1C00ff00";
                    selectedHex = "#3300ff00";
                    selectedBorderHex = "#8000ff00";
                    gradientStartHex = "#FA082612";
                    gradientEndHex = "#FA020604";
                    break;

                case "cyberpunk":
                case "neon":
                    bgHex = "#FA100818"; 
                    borderHex = "#ff007fff"; 
                    caretHex = "#00ffff"; 
                    hoverHex = "#1C00ffff";
                    selectedHex = "#33ff007f";
                    selectedBorderHex = "#ffff00ff";
                    gradientStartHex = "#FA251535";
                    gradientEndHex = "#FA07040E";
                    break;

                case "glass":
                case "light":
                case "white":
                    bgHex = "#E6F5F5FA"; 
                    borderHex = "#33000000";
                    caretHex = "#ff007acc";
                    hoverHex = "#14007acc";
                    selectedHex = "#22007acc";
                    selectedBorderHex = "#66007acc";
                    gradientStartHex = "#E6FFFFFF";
                    gradientEndHex = "#E6E5E5EA";
                    break;

                case "dracula":
                case "vampire":
                case "gothic":
                    bgHex = "#FA343640"; 
                    borderHex = "#BD93F9";
                    caretHex = "#FF79C6";
                    hoverHex = "#1CBD93F9";
                    selectedHex = "#33BD93F9";
                    selectedBorderHex = "#80BD93F9";
                    gradientStartHex = "#FA454858";
                    gradientEndHex = "#FA22232E";
                    break;

                case "sunset":
                case "vaporwave":
                case "synthwave":
                case "dusk":
                    bgHex = "#F21A0F2C"; 
                    borderHex = "#FF7B00";
                    caretHex = "#FF007F";
                    hoverHex = "#1CFF7B00";
                    selectedHex = "#33FF007F";
                    selectedBorderHex = "#80FF7B00";
                    gradientStartHex = "#F23D143A";
                    gradientEndHex = "#F2120A1E";
                    break;

                case "crimson":
                case "red":
                case "blood":
                case "ruby":
                    bgHex = "#F21A0508"; 
                    borderHex = "#DC143C";
                    caretHex = "#FF2400";
                    hoverHex = "#1CDC143C";
                    selectedHex = "#33DC143C";
                    selectedBorderHex = "#80DC143C";
                    gradientStartHex = "#F23A0815";
                    gradientEndHex = "#F2100305";
                    break;

                case "gold":
                case "amber":
                case "luxury":
                case "yellow":
                    bgHex = "#F2141005"; 
                    borderHex = "#FFD700";
                    caretHex = "#FFBF00";
                    hoverHex = "#1CFFD700";
                    selectedHex = "#33FFD700";
                    selectedBorderHex = "#80FFD700";
                    gradientStartHex = "#F22A200B";
                    gradientEndHex = "#F20C0903";
                    break;

                case "nordic":
                case "nord":
                case "arctic":
                case "frost":
                    bgHex = "#F22E3440"; 
                    borderHex = "#88C0D0";
                    caretHex = "#81A1C1";
                    hoverHex = "#1C88C0D0";
                    selectedHex = "#3388C0D0";
                    selectedBorderHex = "#8088C0D0";
                    gradientStartHex = "#F23B4455";
                    gradientEndHex = "#F2232730";
                    break;

                case "solarized":
                    bgHex = "#F2002B36";
                    borderHex = "#268BD2";
                    caretHex = "#859900";
                    hoverHex = "#1C268BD2";
                    selectedHex = "#33268BD2";
                    selectedBorderHex = "#80268BD2";
                    gradientStartHex = "#F2073642";
                    gradientEndHex = "#F200212B";
                    break;

                case "forest":
                case "earth":
                case "woodland":
                    bgHex = "#F2141F16";
                    borderHex = "#2E8B57";
                    caretHex = "#8FBC8F";
                    hoverHex = "#1C2E8B57";
                    selectedHex = "#332E8B57";
                    selectedBorderHex = "#802E8B57";
                    gradientStartHex = "#F21D2E20";
                    gradientEndHex = "#F20B0F0C";
                    break;

                case "sakura":
                case "rose":
                case "pink":
                    bgHex = "#F22B1E22";
                    borderHex = "#FFB7C5";
                    caretHex = "#FF69B4";
                    hoverHex = "#1CFFB7C5";
                    selectedHex = "#33FFB7C5";
                    selectedBorderHex = "#80FFB7C5";
                    gradientStartHex = "#F23D2A31";
                    gradientEndHex = "#F21C1216";
                    break;

                case "monochrome":
                case "grayscale":
                case "slate_gray":
                    bgHex = "#F2151515";
                    borderHex = "#888888";
                    caretHex = "#DDDDDD";
                    hoverHex = "#1C888888";
                    selectedHex = "#33888888";
                    selectedBorderHex = "#80888888";
                    gradientStartHex = "#F2252525";
                    gradientEndHex = "#F20A0A0A";
                    break;

                case "cybernetic":
                case "industrial":
                case "hazarding":
                    bgHex = "#F2151805";
                    borderHex = "#FFD700";
                    caretHex = "#FF4500";
                    hoverHex = "#1CFFD700";
                    selectedHex = "#33FFD700";
                    selectedBorderHex = "#80FFD700";
                    gradientStartHex = "#F2262B0D";
                    gradientEndHex = "#F2090B02";
                    break;

                case "oceanic":
                case "sea":
                case "teal":
                    bgHex = "#F2031B24";
                    borderHex = "#008080";
                    caretHex = "#00ced1";
                    hoverHex = "#1C008080";
                    selectedHex = "#33008080";
                    selectedBorderHex = "#80008080";
                    gradientStartHex = "#F2082C3A";
                    gradientEndHex = "#F2010E14";
                    break;

                case "outrun":
                case "neon_grid":
                    bgHex = "#F20D0415";
                    borderHex = "#FF007F";
                    caretHex = "#00FFFF";
                    hoverHex = "#1CFF007F";
                    selectedHex = "#3300FFFF";
                    selectedBorderHex = "#80FF007F";
                    gradientStartHex = "#F2220C33";
                    gradientEndHex = "#F205010B";
                    break;

                case "purple":
                default:
                    // Keep default purple accents
                    break;
            }

            // Apply accent and background brushes
            SetBackgroundResource("WindowBackgroundBrush", bgHex, gradientStartHex, gradientEndHex);
            SetColorResource("WindowBorderBrush", borderHex);
            SetColorResource("AccentCaretBrush", caretHex);
            SetColorResource("HoverBackgroundBrush", hoverHex);
            SetColorResource("SelectedBackgroundBrush", selectedHex);
            SetColorResource("SelectedBorderBrush", selectedBorderHex);

            // Handle Media Background
            if (SettingsManager.Current.BACKGROUND_MODE == "Media" && !string.IsNullOrEmpty(SettingsManager.Current.BACKGROUND_MEDIA_SOURCE))
            {
                try
                {
                    var uri = new Uri(SettingsManager.Current.BACKGROUND_MEDIA_SOURCE, UriKind.RelativeOrAbsolute);
                    var imgSource = new System.Windows.Media.Imaging.BitmapImage(uri);
                    Application.Current.Resources["WindowBackgroundMediaSource"] = imgSource;
                    Application.Current.Resources["WindowMediaVisibility"] = Visibility.Visible;
                }
                catch
                {
                    Application.Current.Resources["WindowMediaVisibility"] = Visibility.Collapsed;
                }
            }
            else
            {
                Application.Current.Resources["WindowMediaVisibility"] = Visibility.Collapsed;
                Application.Current.Resources["WindowBackgroundMediaSource"] = null;
            }

            // Configure text colors dynamically depending on light/dark themes
            bool isLightTheme = (themeName == "light" || themeName == "glass" || themeName == "white");
            string textPrimary = isLightTheme ? "#111111" : "#FFFFFF";
            string textPlaceholder = isLightTheme ? "#5A000000" : "#5AFFFFFF";
            string textSecondary = isLightTheme ? "#8C000000" : "#8CFFFFFF";

            SetColorResource("TextPrimaryBrush", textPrimary);
            SetColorResource("TextPlaceholderBrush", textPlaceholder);
            SetColorResource("TextSecondaryBrush", textSecondary);

            // Global Font Family Resource
            string fontName = string.IsNullOrEmpty(SettingsManager.Current.CUSTOM_FONT_FAMILY) ? "Segoe UI" : SettingsManager.Current.CUSTOM_FONT_FAMILY;
            FontFamily wpffont;
            if (System.IO.File.Exists(fontName) && 
                (fontName.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || 
                 fontName.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    using (var pfc = new System.Drawing.Text.PrivateFontCollection())
                    {
                        pfc.AddFontFile(fontName);
                        if (pfc.Families.Length > 0)
                        {
                            string friendlyName = pfc.Families[0].Name;
                            string folder = System.IO.Path.GetDirectoryName(fontName) ?? "";
                            string filename = System.IO.Path.GetFileName(fontName);
                            string uriFolder = new Uri(folder + "/").AbsoluteUri;
                            wpffont = new FontFamily(new Uri(uriFolder), "./" + filename + "#" + friendlyName);
                        }
                        else
                        {
                            wpffont = new FontFamily(fontName);
                        }
                    }
                }
                catch
                {
                    wpffont = new FontFamily(fontName);
                }
            }
            else
            {
                wpffont = new FontFamily(fontName);
            }
            Application.Current.Resources["ActiveFontFamily"] = wpffont;

            // Global Corner Radius Resources
            bool rounded = SettingsManager.Current.USE_ROUNDED_CORNERS;
            Application.Current.Resources["WindowCornerRadius"] = new CornerRadius(rounded ? 12 : 0);
            Application.Current.Resources["ItemCornerRadius"] = new CornerRadius(rounded ? 6 : 0);
        }

        public static void SetBackgroundResource(string key, string solidHex, string startHex, string endHex)
        {
            try
            {
                Brush brush;
                if (SettingsManager.Current.BACKGROUND_MODE == "RGB")
                {
                    var gradient = new LinearGradientBrush();
                    gradient.StartPoint = new Point(0, 0);
                    gradient.EndPoint = new Point(1, 1);

                    // Using semi-transparent colors for the RGB effect to ensure text readability
                    byte alpha = 0x33; // ~20% opacity for a subtle glow effect
                    var stop1 = new GradientStop(Color.FromArgb(alpha, 255, 0, 0), 0.0);
                    var stop2 = new GradientStop(Color.FromArgb(alpha, 0, 255, 0), 0.5);
                    var stop3 = new GradientStop(Color.FromArgb(alpha, 0, 0, 255), 1.0);

                    gradient.GradientStops.Add(stop1);
                    gradient.GradientStops.Add(stop2);
                    gradient.GradientStops.Add(stop3);

                    if (SettingsManager.Current.ENABLE_ANIMATIONS)
                    {
                        var duration = TimeSpan.FromSeconds(8);

                        var anim1 = new ColorAnimationUsingKeyFrames();
                        anim1.KeyFrames.Add(new LinearColorKeyFrame(Color.FromArgb(alpha, 255, 0, 0), KeyTime.FromPercent(0)));
                        anim1.KeyFrames.Add(new LinearColorKeyFrame(Color.FromArgb(alpha, 0, 255, 0), KeyTime.FromPercent(0.33)));
                        anim1.KeyFrames.Add(new LinearColorKeyFrame(Color.FromArgb(alpha, 0, 0, 255), KeyTime.FromPercent(0.66)));
                        anim1.KeyFrames.Add(new LinearColorKeyFrame(Color.FromArgb(alpha, 255, 0, 0), KeyTime.FromPercent(1.0)));
                        anim1.RepeatBehavior = RepeatBehavior.Forever;
                        anim1.Duration = duration;

                        var anim2 = new ColorAnimationUsingKeyFrames();
                        anim2.KeyFrames.Add(new LinearColorKeyFrame(Color.FromArgb(alpha, 0, 255, 0), KeyTime.FromPercent(0)));
                        anim2.KeyFrames.Add(new LinearColorKeyFrame(Color.FromArgb(alpha, 0, 0, 255), KeyTime.FromPercent(0.33)));
                        anim2.KeyFrames.Add(new LinearColorKeyFrame(Color.FromArgb(alpha, 255, 0, 0), KeyTime.FromPercent(0.66)));
                        anim2.KeyFrames.Add(new LinearColorKeyFrame(Color.FromArgb(alpha, 0, 255, 0), KeyTime.FromPercent(1.0)));
                        anim2.RepeatBehavior = RepeatBehavior.Forever;
                        anim2.Duration = duration;

                        var anim3 = new ColorAnimationUsingKeyFrames();
                        anim3.KeyFrames.Add(new LinearColorKeyFrame(Color.FromArgb(alpha, 0, 0, 255), KeyTime.FromPercent(0)));
                        anim3.KeyFrames.Add(new LinearColorKeyFrame(Color.FromArgb(alpha, 255, 0, 0), KeyTime.FromPercent(0.33)));
                        anim3.KeyFrames.Add(new LinearColorKeyFrame(Color.FromArgb(alpha, 0, 255, 0), KeyTime.FromPercent(0.66)));
                        anim3.KeyFrames.Add(new LinearColorKeyFrame(Color.FromArgb(alpha, 0, 0, 255), KeyTime.FromPercent(1.0)));
                        anim3.RepeatBehavior = RepeatBehavior.Forever;
                        anim3.Duration = duration;

                        stop1.BeginAnimation(GradientStop.ColorProperty, anim1);
                        stop2.BeginAnimation(GradientStop.ColorProperty, anim2);
                        stop3.BeginAnimation(GradientStop.ColorProperty, anim3);
                        brush = gradient;
                    }
                    else
                    {
                        brush = gradient;
                    }
                }
                else if (SettingsManager.Current.BACKGROUND_MODE == "Gradient" || SettingsManager.Current.USE_GRADIENT_BACKGROUND)
                {
                    var colorStart = (Color)ColorConverter.ConvertFromString(startHex);
                    var colorEnd = (Color)ColorConverter.ConvertFromString(endHex);
                    var gradient = new LinearGradientBrush(colorStart, colorEnd, new Point(0, 0), new Point(1, 1));
                    
                    if (SettingsManager.Current.ENABLE_ANIMATIONS)
                    {
                        // Dynamic "Liquid" Gradient Animation
                        // Using slightly larger values to ensure visible movement even on small windows
                        var startAnim = new System.Windows.Media.Animation.PointAnimation
                        {
                            From = new Point(-0.4, -0.4),
                            To = new Point(0.4, 0.4),
                            Duration = TimeSpan.FromSeconds(15),
                            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                            AutoReverse = true,
                            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                        };
                        var endAnim = new System.Windows.Media.Animation.PointAnimation
                        {
                            From = new Point(1.4, 1.4),
                            To = new Point(0.6, 0.6),
                            Duration = TimeSpan.FromSeconds(15),
                            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                            AutoReverse = true,
                            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                        };

                        // Brushes that are animated must NOT be frozen.
                        gradient.BeginAnimation(LinearGradientBrush.StartPointProperty, startAnim);
                        gradient.BeginAnimation(LinearGradientBrush.EndPointProperty, endAnim);
                        brush = gradient;
                    }
                    else
                    {
                        gradient.Freeze();
                        brush = gradient;
                    }
                }
                else
                {
                    var color = (Color)ColorConverter.ConvertFromString(solidHex);
                    brush = new SolidColorBrush(color);
                    brush.Freeze();
                }
                Application.Current.Resources[key] = brush;
            }
            catch
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(solidHex);
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();
                    Application.Current.Resources[key] = brush;
                }
                catch { }
            }
        }

        public static void SetColorResource(string key, string hexColor)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hexColor);
                var brush = new SolidColorBrush(color);
                brush.Freeze(); // Freeze for cross-thread performance optimization

                Application.Current.Resources[key] = brush;
            }
            catch { }
        }
    }
}
