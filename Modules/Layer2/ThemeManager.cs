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

            // Reset background mode if it was forced to RGB by a previous theme
            // (Unless it was manually set to Solid or Media in settings)
            if (SettingsManager.Current.BACKGROUND_MODE == "RGB")
            {
                SettingsManager.Current.BACKGROUND_MODE = "Gradient";
            }

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

                case "rainbow":
                case "rgb_dynamic":
                    bgHex = "#F5050505";
                    borderHex = "#FF00FFFF";
                    caretHex = "#FFFFFF";
                    hoverHex = "#22FFFFFF";
                    selectedHex = "#4400FFFF";
                    selectedBorderHex = "#FF00FFFF";
                    gradientStartHex = "#F5101010";
                    gradientEndHex = "#F5000000";
                    SettingsManager.Current.BACKGROUND_MODE = "RGB"; // Force RGB mode for this theme
                    break;

                case "aurora":
                case "northern_lights":
                    bgHex = "#F5021008";
                    borderHex = "#00FF9D";
                    caretHex = "#70FF00";
                    hoverHex = "#2200FF9D";
                    selectedHex = "#44008E84";
                    selectedBorderHex = "#00FF9D";
                    gradientStartHex = "#F5082E1A";
                    gradientEndHex = "#F5020804";
                    break;

                case "lava":
                case "magma":
                case "inferno":
                    bgHex = "#F5150500";
                    borderHex = "#FF4500";
                    caretHex = "#FFD700";
                    hoverHex = "#22FF4500";
                    selectedHex = "#448B0000";
                    selectedBorderHex = "#FF4500";
                    gradientStartHex = "#F52E0A00";
                    gradientEndHex = "#F50F0200";
                    break;

                case "cyber_glitch":
                case "malfunction":
                    bgHex = "#F50A0A0F";
                    borderHex = "#00F2FF";
                    caretHex = "#FF003C";
                    hoverHex = "#1C00F2FF";
                    selectedHex = "#33FF003C";
                    selectedBorderHex = "#00F2FF";
                    gradientStartHex = "#F5151525";
                    gradientEndHex = "#F5050508";
                    break;

                case "nebula":
                case "deep_space":
                    bgHex = "#F8050015";
                    borderHex = "#8A2BE2";
                    caretHex = "#00BFFF";
                    hoverHex = "#1C8A2BE2";
                    selectedHex = "#334B0082";
                    selectedBorderHex = "#8A2BE2";
                    gradientStartHex = "#F80F0025";
                    gradientEndHex = "#F802000A";
                    break;

                case "emerald_pulse":
                    bgHex = "#F8000F05";
                    borderHex = "#00FF41";
                    caretHex = "#FFFFFF";
                    hoverHex = "#1C00FF41";
                    selectedHex = "#33003B0D";
                    selectedBorderHex = "#00FF41";
                    gradientStartHex = "#F800260B";
                    gradientEndHex = "#F8000502";
                    break;

                case "iridescent":
                case "opal":
                    bgHex = "#F81A1A1A";
                    borderHex = "#E0FFFF";
                    caretHex = "#FF00FF";
                    hoverHex = "#22FFFFFF";
                    selectedHex = "#4400FFFF";
                    selectedBorderHex = "#FF00FF";
                    gradientStartHex = "#F82C2C2C";
                    gradientEndHex = "#F80E0E0E";
                    break;

                case "solar_flare":
                case "sun":
                    bgHex = "#F81F0A00";
                    borderHex = "#FFD700";
                    caretHex = "#FF4500";
                    hoverHex = "#22FFD700";
                    selectedHex = "#44FF8C00";
                    selectedBorderHex = "#FFD700";
                    gradientStartHex = "#F83D1200";
                    gradientEndHex = "#F80F0500";
                    break;

                case "abyssal":
                case "deep_ocean":
                    bgHex = "#F8000510";
                    borderHex = "#0077BE";
                    caretHex = "#00FFFF";
                    hoverHex = "#1C0077BE";
                    selectedHex = "#3300008B";
                    selectedBorderHex = "#0077BE";
                    gradientStartHex = "#F8000A25";
                    gradientEndHex = "#F800020A";
                    break;

                case "glitch_wave":
                    bgHex = "#F80A000F";
                    borderHex = "#00FFDD";
                    caretHex = "#FF00FF";
                    hoverHex = "#2200FFDD";
                    selectedHex = "#44FF00FF";
                    selectedBorderHex = "#00FFDD";
                    gradientStartHex = "#F8150025";
                    gradientEndHex = "#F8050008";
                    break;

                case "spectrum":
                case "prism":
                    bgHex = "#F2050505";
                    borderHex = "#FFFFFF";
                    caretHex = "#00FFFF";
                    hoverHex = "#33FFFFFF";
                    selectedHex = "#55FFFFFF";
                    selectedBorderHex = "#00FFFF";
                    gradientStartHex = "#F21A1A1A";
                    gradientEndHex = "#F2050505";
                    SettingsManager.Current.BACKGROUND_MODE = "RGB";
                    break;

                case "midnight_neon":
                    bgHex = "#F2020205";
                    borderHex = "#39FF14";
                    caretHex = "#FF00FF";
                    hoverHex = "#2239FF14";
                    selectedHex = "#44000000";
                    selectedBorderHex = "#FF00FF";
                    gradientStartHex = "#F2050510";
                    gradientEndHex = "#F2010105";
                    break;

                case "frozen_fire":
                    bgHex = "#F20A0515";
                    borderHex = "#FF4500";
                    caretHex = "#00FFFF";
                    hoverHex = "#2200FFFF";
                    selectedHex = "#44FF4500";
                    selectedBorderHex = "#00FFFF";
                    gradientStartHex = "#F210082E";
                    gradientEndHex = "#F205020F";
                    break;

                case "quantum_flux":
                case "matrix_cyan":
                    bgHex = "#F8000A0A";
                    borderHex = "#00FFDD";
                    caretHex = "#00FF99";
                    hoverHex = "#2200FFDD";
                    selectedHex = "#44002222";
                    selectedBorderHex = "#00FFDD";
                    gradientStartHex = "#F8001A1A";
                    gradientEndHex = "#F8000505";
                    break;

                case "hyper_neon":
                    bgHex = "#F80A000A";
                    borderHex = "#CC00FF";
                    caretHex = "#00FFFF";
                    hoverHex = "#22CC00FF";
                    selectedHex = "#44220033";
                    selectedBorderHex = "#CC00FF";
                    gradientStartHex = "#F81A001A";
                    gradientEndHex = "#F8050005";
                    break;

                case "plasma_core":
                    bgHex = "#F805000F";
                    borderHex = "#7F00FF";
                    caretHex = "#E0B0FF";
                    hoverHex = "#227F00FF";
                    selectedHex = "#441A0033";
                    selectedBorderHex = "#7F00FF";
                    gradientStartHex = "#F80F0025";
                    gradientEndHex = "#F802000A";
                    break;

                case "matrix_red":
                case "blood_code":
                    bgHex = "#F80A0000";
                    borderHex = "#FF0000";
                    caretHex = "#FF4500";
                    hoverHex = "#22FF0000";
                    selectedHex = "#44330000";
                    selectedBorderHex = "#FF0000";
                    gradientStartHex = "#F81A0000";
                    gradientEndHex = "#F8050000";
                    break;

                case "hologram":
                case "cyber_ghost":
                    bgHex = "#CC0A1520";
                    borderHex = "#00FFFF";
                    caretHex = "#FFFFFF";
                    hoverHex = "#1C00FFFF";
                    selectedHex = "#3300FFFF";
                    selectedBorderHex = "#00FFFF";
                    gradientStartHex = "#CC152535";
                    gradientEndHex = "#CC050A10";
                    break;

                case "supernova":
                case "cosmic_blast":
                    bgHex = "#F81A002A";
                    borderHex = "#FF00FF";
                    caretHex = "#FFD700";
                    hoverHex = "#22FF00FF";
                    selectedHex = "#444B0082";
                    selectedBorderHex = "#FFD700";
                    gradientStartHex = "#F82D004D";
                    gradientEndHex = "#F80D0015";
                    break;

                case "electric_storm":
                    bgHex = "#F800001A";
                    borderHex = "#BF00FF";
                    caretHex = "#00E5FF";
                    hoverHex = "#22BF00FF";
                    selectedHex = "#4400008B";
                    selectedBorderHex = "#00E5FF";
                    gradientStartHex = "#F800002E";
                    gradientEndHex = "#F800000F";
                    break;

                case "vapor_wave":
                case "retro_grid":
                    bgHex = "#F50D011F";
                    borderHex = "#FF00FF";
                    caretHex = "#00FFFF";
                    hoverHex = "#22FF00FF";
                    selectedHex = "#4400FFFF";
                    selectedBorderHex = "#FFFFFF";
                    gradientStartHex = "#F522023F";
                    gradientEndHex = "#F5080010";
                    break;

                case "toxic":
                case "biohazard":
                    bgHex = "#F5050F02";
                    borderHex = "#CCFF00";
                    caretHex = "#39FF14";
                    hoverHex = "#22CCFF00";
                    selectedHex = "#44003300";
                    selectedBorderHex = "#CCFF00";
                    gradientStartHex = "#F50A2605";
                    gradientEndHex = "#F5020501";
                    break;

                case "monolith":
                case "brutalist":
                    bgHex = "#FF121212";
                    borderHex = "#404040";
                    caretHex = "#FFFFFF";
                    hoverHex = "#33FFFFFF";
                    selectedHex = "#55FFFFFF";
                    selectedBorderHex = "#FFFFFF";
                    gradientStartHex = "#FF1A1A1A";
                    gradientEndHex = "#FF0A0A0A";
                    break;

                case "glitch_cyan":
                case "cyber_vibe":
                    bgHex = "#F8050A15";
                    borderHex = "#00FFFF";
                    caretHex = "#FF007F";
                    hoverHex = "#1C00FFFF";
                    selectedHex = "#44002222";
                    selectedBorderHex = "#00FFFF";
                    gradientStartHex = "#F80D1525";
                    gradientEndHex = "#F802050A";
                    break;

                case "magma_flow":
                    bgHex = "#F81A0500";
                    borderHex = "#FF4500";
                    caretHex = "#FFD700";
                    hoverHex = "#22FF4500";
                    selectedHex = "#44330000";
                    selectedBorderHex = "#FF4500";
                    gradientStartHex = "#F82E0A00";
                    gradientEndHex = "#F80F0200";
                    break;

                case "emerald_city":
                    bgHex = "#F8000F05";
                    borderHex = "#00FF41";
                    caretHex = "#00FF41";
                    hoverHex = "#1C00FF41";
                    selectedHex = "#33002205";
                    selectedBorderHex = "#00FF41";
                    gradientStartHex = "#F8001A08";
                    gradientEndHex = "#F8000502";
                    break;

                case "royal_gold":
                case "emperor":
                    bgHex = "#F8151205";
                    borderHex = "#FFD700";
                    caretHex = "#FFD700";
                    hoverHex = "#22FFD700";
                    selectedHex = "#442A200B";
                    selectedBorderHex = "#FFD700";
                    gradientStartHex = "#F82A220B";
                    gradientEndHex = "#F80C0903";
                    break;

                case "blood_moon":
                case "eclipse":
                    bgHex = "#F81A0000";
                    borderHex = "#FF0000";
                    caretHex = "#8B0000";
                    hoverHex = "#22FF0000";
                    selectedHex = "#44400000";
                    selectedBorderHex = "#FF0000";
                    gradientStartHex = "#F82D0000";
                    gradientEndHex = "#F80A0000";
                    break;

                case "cyber_forest":
                    bgHex = "#F804150A";
                    borderHex = "#00FF41";
                    caretHex = "#00FF41";
                    hoverHex = "#2200FF41";
                    selectedHex = "#33002612";
                    selectedBorderHex = "#00FF41";
                    gradientStartHex = "#F8082E1A";
                    gradientEndHex = "#F8020804";
                    break;

                case "void_pulse":
                case "singularity":
                    bgHex = "#F8050010";
                    borderHex = "#FFFFFF";
                    caretHex = "#00FFFF";
                    hoverHex = "#22FFFFFF";
                    selectedHex = "#44101010";
                    selectedBorderHex = "#FFFFFF";
                    gradientStartHex = "#F8101010";
                    gradientEndHex = "#F8000000";
                    SettingsManager.Current.BACKGROUND_MODE = "RGB";
                    break;

                case "spectrum_shift":
                case "rainbow_vibe":
                    bgHex = "#F5050505";
                    borderHex = "#FF00FF";
                    caretHex = "#00FFFF";
                    hoverHex = "#22FFFFFF";
                    selectedHex = "#44FF00FF";
                    selectedBorderHex = "#00FFFF";
                    gradientStartHex = "#F5101010";
                    gradientEndHex = "#F5000000";
                    SettingsManager.Current.BACKGROUND_MODE = "RGB";
                    break;

                case "nebula_gas":
                case "deep_purple":
                    bgHex = "#F50A0015";
                    borderHex = "#8A2BE2";
                    caretHex = "#FF00FF";
                    hoverHex = "#228A2BE2";
                    selectedHex = "#444B0082";
                    selectedBorderHex = "#8A2BE2";
                    gradientStartHex = "#F51A002A";
                    gradientEndHex = "#F505000A";
                    break;

                case "frozen_wasteland":
                    bgHex = "#F5000A1A";
                    borderHex = "#00FFFF";
                    caretHex = "#E0FFFF";
                    hoverHex = "#2200FFFF";
                    selectedHex = "#4400008B";
                    selectedBorderHex = "#00FFFF";
                    gradientStartHex = "#F5001A2E";
                    gradientEndHex = "#F500050F";
                    break;

                case "acid_burn":
                case "toxic_waste":
                    bgHex = "#F5050F00";
                    borderHex = "#CCFF00";
                    caretHex = "#CCFF00";
                    hoverHex = "#22CCFF00";
                    selectedHex = "#44003300";
                    selectedBorderHex = "#CCFF00";
                    gradientStartHex = "#F50A2600";
                    gradientEndHex = "#F5020500";
                    break;

                case "obsidian_flow":
                    bgHex = "#F8050505";
                    borderHex = "#FF0000";
                    caretHex = "#FFFFFF";
                    hoverHex = "#22FF0000";
                    selectedHex = "#44000000";
                    selectedBorderHex = "#FF0000";
                    gradientStartHex = "#F8101010";
                    gradientEndHex = "#F8000000";
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

                    // Increased alpha for better visibility (from 0x33 to 0xEE)
                    byte alpha = 0xEE;
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
