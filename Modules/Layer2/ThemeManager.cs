// Developer: heaplyn
// Date: 2026-08-09
// Summary: Coordinates dynamic loading, parsing, and applying of window themes (colors, borders, carets, selection states) live via Application Resources.

using System;
using System.Windows;
using System.Windows.Media;

namespace JarvisLauncher
{
    public static class ThemeManager
    {
        public static void ApplyTheme(string themeName)
        {
            themeName = themeName.Trim().ToLower();

            // Default: Purple Theme Accents
            string bgHex = "#F2140D24"; 
            string borderHex = "#ffffffe6";
            string caretHex = "#FFD9CCFF";
            string hoverHex = "#1C8050E6";
            string selectedHex = "#338050E6";
            string selectedBorderHex = "#808050E6";

            switch (themeName)
            {
                case "dark":
                case "slate":
                case "charcoal":
                case "black":
                    bgHex = "#F2121214"; 
                    borderHex = "#2Bffffff";
                    caretHex = "#ffffffff";
                    hoverHex = "#1Cffffff";
                    selectedHex = "#33ffffff";
                    selectedBorderHex = "#66ffffff";
                    break;

                case "blue":
                case "space":
                case "ocean":
                    bgHex = "#F2080F1E"; 
                    borderHex = "#2600bfff";
                    caretHex = "#00bffff9";
                    hoverHex = "#1C00bfff";
                    selectedHex = "#3300bfff";
                    selectedBorderHex = "#8000bfff";
                    break;

                case "green":
                case "matrix":
                case "terminal":
                case "hacker":
                    bgHex = "#F2020A05"; 
                    borderHex = "#4000ff00";
                    caretHex = "#00ff00ff";
                    hoverHex = "#1C00ff00";
                    selectedHex = "#3300ff00";
                    selectedBorderHex = "#8000ff00";
                    break;

                case "cyberpunk":
                case "neon":
                    bgHex = "#F208050E"; 
                    borderHex = "#ff007fff"; 
                    caretHex = "#00ffff"; 
                    hoverHex = "#1C00ffff";
                    selectedHex = "#33ff007f";
                    selectedBorderHex = "#ffff00ff";
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
                    break;

                case "purple":
                default:
                    // Keep default purple accents
                    break;
            }

            // Apply accent and background brushes
            SetColorResource("WindowBackgroundBrush", bgHex);
            SetColorResource("WindowBorderBrush", borderHex);
            SetColorResource("AccentCaretBrush", caretHex);
            SetColorResource("HoverBackgroundBrush", hoverHex);
            SetColorResource("SelectedBackgroundBrush", selectedHex);
            SetColorResource("SelectedBorderBrush", selectedBorderHex);

            // Configure text colors dynamically depending on light/dark themes
            bool isLightTheme = (themeName == "light" || themeName == "glass" || themeName == "white");
            string textPrimary = isLightTheme ? "#111111" : "#FFFFFF";
            string textPlaceholder = isLightTheme ? "#5A000000" : "#5AFFFFFF";
            string textSecondary = isLightTheme ? "#8C000000" : "#8CFFFFFF";

            SetColorResource("TextPrimaryBrush", textPrimary);
            SetColorResource("TextPlaceholderBrush", textPlaceholder);
            SetColorResource("TextSecondaryBrush", textSecondary);
        }

        private static void SetColorResource(string key, string hexColor)
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
