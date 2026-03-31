using System;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace MessagingApp
{
    internal sealed class AppThemePalette
    {
        public AppThemePalette(
            string themeSelection,
            string themeName,
            Color backgroundColor,
            Color sidebarColor,
            Color surfaceColor,
            Color composerColor,
            Color accentColor,
            Color accentSoftColor,
            Color textColor,
            Color mutedTextColor,
            Color borderColor,
            Color sidebarTextColor,
            Color sidebarMutedTextColor,
            Color dangerColor)
        {
            ThemeSelection = themeSelection;
            ThemeName = themeName;
            BackgroundColor = backgroundColor;
            SidebarColor = sidebarColor;
            SurfaceColor = surfaceColor;
            ComposerColor = composerColor;
            AccentColor = accentColor;
            AccentSoftColor = accentSoftColor;
            TextColor = textColor;
            MutedTextColor = mutedTextColor;
            BorderColor = borderColor;
            SidebarTextColor = sidebarTextColor;
            SidebarMutedTextColor = sidebarMutedTextColor;
            DangerColor = dangerColor;
        }

        public string ThemeSelection { get; }
        public string ThemeName { get; }
        public Color BackgroundColor { get; }
        public Color SidebarColor { get; }
        public Color SurfaceColor { get; }
        public Color ComposerColor { get; }
        public Color AccentColor { get; }
        public Color AccentSoftColor { get; }
        public Color TextColor { get; }
        public Color MutedTextColor { get; }
        public Color BorderColor { get; }
        public Color SidebarTextColor { get; }
        public Color SidebarMutedTextColor { get; }
        public Color DangerColor { get; }
    }

    internal static class AppThemeManager
    {
        internal const string DefaultThemeName = "Ocean Blue";

        internal static readonly string[] ThemeNames =
        {
            "Light",
            "Dark",
            "Grey",
            "High Contrast (Dark)",
            "High Contrast (Light)",
            "Forest Green",
            "Ocean Blue",
            "Sunset",
            "Warm",
            "Aqua",
            "Lavender",
            "Emerald",
            "Cherry Blossom",
            "Tangerine",
            "Plum",
            "Sapphire",
            "Random",
            "RandomCool"
        };

        internal static string LoadSavedThemeSelection()
        {
            string path = GetThemeSettingsPath();
            if (!File.Exists(path))
            {
                File.WriteAllText(path, DefaultThemeName);
                return DefaultThemeName;
            }

            string selection = File.ReadAllText(path).Trim();
            return string.IsNullOrWhiteSpace(selection) ? DefaultThemeName : selection;
        }

        internal static void SaveThemeSelection(string selection)
        {
            File.WriteAllText(GetThemeSettingsPath(), string.IsNullOrWhiteSpace(selection) ? DefaultThemeName : selection);
        }

        internal static AppThemePalette ResolveTheme(string selection)
        {
            string requestedSelection = string.IsNullOrWhiteSpace(selection) ? DefaultThemeName : selection.Trim();
            if (TryCreateRandomPalette(requestedSelection, out AppThemePalette? randomPalette))
            {
                return randomPalette!;
            }

            return requestedSelection switch
            {
                "Light" => CreatePalette("Light", requestedSelection, FromHex("F7F8FC"), FromHex("32415E"), FromHex("FFFFFF"), FromHex("F3F6FB"), FromHex("4C7CF0"), FromHex("1D2433"), FromHex("6D7890"), FromHex("D7DFEA")),
                "Dark" => CreatePalette("Dark", requestedSelection, FromHex("0F172A"), FromHex("111827"), FromHex("1E293B"), FromHex("162132"), FromHex("60A5FA"), FromHex("F8FAFC"), FromHex("9FB0C7"), FromHex("334155")),
                "Grey" => CreatePalette("Grey", requestedSelection, FromHex("EEF2F5"), FromHex("455468"), FromHex("FFFFFF"), FromHex("F6F8FB"), FromHex("5F85DB"), FromHex("253244"), FromHex("78869C"), FromHex("D5DEE8")),
                "High Contrast (Dark)" => CreatePalette("High Contrast (Dark)", requestedSelection, Color.Black, Color.Black, FromHex("101010"), FromHex("080808"), FromHex("FFE600"), Color.White, FromHex("E5E5E5"), Color.White),
                "High Contrast (Light)" => CreatePalette("High Contrast (Light)", requestedSelection, Color.White, Color.White, Color.White, FromHex("FAFAFA"), FromHex("005FCC"), FromHex("101010"), FromHex("4B4B4B"), Color.Black),
                "Forest Green" => CreatePalette("Forest Green", requestedSelection, FromHex("F1F8F3"), FromHex("1E4D3B"), FromHex("FFFFFF"), FromHex("F4FAF6"), FromHex("2FA36A"), FromHex("20352B"), FromHex("6E8379"), FromHex("D5E5D9")),
                "Ocean Blue" => CreatePalette("Ocean Blue", requestedSelection, FromHex("F2F6FC"), FromHex("203B63"), FromHex("FFFFFF"), FromHex("F5F8FD"), FromHex("3E7BFA"), FromHex("1C2940"), FromHex("70809A"), FromHex("D6E0EE")),
                "Sunset" => CreatePalette("Sunset", requestedSelection, FromHex("FFF4EE"), FromHex("694133"), FromHex("FFFDFC"), FromHex("FFF7F2"), FromHex("EB7D4F"), FromHex("4F342E"), FromHex("957169"), FromHex("F0DAD0")),
                "Warm" => CreatePalette("Warm", requestedSelection, FromHex("FCF6ED"), FromHex("6D5339"), FromHex("FFFDF9"), FromHex("FBF2E5"), FromHex("C68A41"), FromHex("4E3A28"), FromHex("90735A"), FromHex("E7D6C0")),
                "Aqua" => CreatePalette("Aqua", requestedSelection, FromHex("EFFBFB"), FromHex("1D5962"), FromHex("FFFFFF"), FromHex("F1FBFC"), FromHex("17A9BC"), FromHex("1E373C"), FromHex("6E8991"), FromHex("D4E8EC")),
                "Lavender" => CreatePalette("Lavender", requestedSelection, FromHex("F7F3FD"), FromHex("4D4077"), FromHex("FFFFFF"), FromHex("F6F3FD"), FromHex("8F73E7"), FromHex("342C4D"), FromHex("7C7098"), FromHex("DDD5EE")),
                "Emerald" => CreatePalette("Emerald", requestedSelection, FromHex("F2FAF6"), FromHex("174A39"), FromHex("FFFFFF"), FromHex("F3FCF7"), FromHex("18B36C"), FromHex("1F3A2F"), FromHex("6B897C"), FromHex("D2E5DB")),
                "Cherry Blossom" => CreatePalette("Cherry Blossom", requestedSelection, FromHex("FFF5F8"), FromHex("704A61"), FromHex("FFFFFF"), FromHex("FFF7F9"), FromHex("E78FB2"), FromHex("4B2F40"), FromHex("957388"), FromHex("F0DAE3")),
                "Tangerine" => CreatePalette("Tangerine", requestedSelection, FromHex("FFF6EE"), FromHex("784924"), FromHex("FFFFFF"), FromHex("FFF8F1"), FromHex("F18E36"), FromHex("50351F"), FromHex("96745C"), FromHex("F0DCCB")),
                "Plum" => CreatePalette("Plum", requestedSelection, FromHex("F8F3FA"), FromHex("5A3E63"), FromHex("FFFFFF"), FromHex("F9F5FA"), FromHex("B478D0"), FromHex("3D2E45"), FromHex("87728E"), FromHex("E4D8E6")),
                "Sapphire" => CreatePalette("Sapphire", requestedSelection, FromHex("F2F7FD"), FromHex("1B3F68"), FromHex("FFFFFF"), FromHex("F4F8FE"), FromHex("4D8AF2"), FromHex("21364F"), FromHex("7388A2"), FromHex("D4E0EE")),
                _ => CreatePalette("Ocean Blue", "Ocean Blue", FromHex("F2F6FC"), FromHex("203B63"), FromHex("FFFFFF"), FromHex("F5F8FD"), FromHex("3E7BFA"), FromHex("1C2940"), FromHex("70809A"), FromHex("D6E0EE"))
            };
        }

        private static bool TryCreateRandomPalette(string selection, out AppThemePalette? palette)
        {
            palette = null;
            if (selection.StartsWith("Random|", StringComparison.Ordinal))
            {
                int seed = ParseSeed(selection, "Random|");
                palette = CreateSeededPalette("Random", $"Random|{seed}", seed, false);
                return true;
            }

            if (selection.Equals("Random", StringComparison.Ordinal))
            {
                int seed = Guid.NewGuid().GetHashCode();
                palette = CreateSeededPalette("Random", $"Random|{seed}", seed, false);
                return true;
            }

            if (selection.StartsWith("RandomCool|", StringComparison.Ordinal))
            {
                int seed = ParseSeed(selection, "RandomCool|");
                palette = CreateSeededPalette("RandomCool", $"RandomCool|{seed}", seed, true);
                return true;
            }

            if (selection.Equals("RandomCool", StringComparison.Ordinal))
            {
                int seed = Guid.NewGuid().GetHashCode();
                palette = CreateSeededPalette("RandomCool", $"RandomCool|{seed}", seed, true);
                return true;
            }

            return false;
        }

        private static AppThemePalette CreateSeededPalette(string themeName, string themeSelection, int seed, bool coolRange)
        {
            Random random = new Random(seed);
            double hue = coolRange ? random.Next(180, 251) : random.Next(0, 360);
            double saturation = coolRange ? 0.58 + (random.NextDouble() * 0.12) : 0.50 + (random.NextDouble() * 0.18);
            double lightness = coolRange ? 0.50 + (random.NextDouble() * 0.08) : 0.46 + (random.NextDouble() * 0.12);

            Color accent = FromHsl(hue, saturation, lightness);
            Color background = FromHsl(hue, Math.Max(0.12, saturation * 0.18), 0.96);
            Color surface = Color.White;
            Color composer = FromHsl(hue, Math.Max(0.10, saturation * 0.14), 0.965);
            Color sidebar = FromHsl(hue, Math.Min(0.42, saturation * 0.50), coolRange ? 0.22 : 0.28);
            Color text = FromHex("1F2937");
            Color muted = FromHex("74839B");
            Color border = Blend(surface, accent, 0.18);

            return CreatePalette(themeName, themeSelection, background, sidebar, surface, composer, accent, text, muted, border);
        }

        private static AppThemePalette CreatePalette(string themeName, string themeSelection, Color background, Color sidebar, Color surface, Color composer, Color accent, Color text, Color muted, Color border)
        {
            Color accentSoft = Blend(surface, accent, 0.78);
            Color sidebarText = GetReadableTextColor(sidebar);
            Color sidebarMuted = Blend(sidebarText, sidebar, 0.38);
            Color danger = GetReadableTextColor(background) == Color.White ? FromHex("FF8F8F") : FromHex("D95D5D");

            return new AppThemePalette(
                themeSelection,
                themeName,
                background,
                sidebar,
                surface,
                composer,
                accent,
                accentSoft,
                text,
                muted,
                border,
                sidebarText,
                sidebarMuted,
                danger);
        }

        private static string GetThemeSettingsPath()
        {
            string settingsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings");
            Directory.CreateDirectory(settingsDirectory);
            return Path.Combine(settingsDirectory, "ThemeSettings.txt");
        }

        private static int ParseSeed(string selection, string prefix)
        {
            string seedText = selection.Substring(prefix.Length);
            return int.TryParse(seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed)
                ? seed
                : Guid.NewGuid().GetHashCode();
        }

        private static Color GetReadableTextColor(Color background)
        {
            double luminance = ((0.299 * background.R) + (0.587 * background.G) + (0.114 * background.B)) / 255d;
            return luminance > 0.62 ? FromHex("1F2937") : Color.White;
        }

        private static Color Blend(Color baseColor, Color overlayColor, double amount)
        {
            amount = Math.Clamp(amount, 0d, 1d);
            int red = (int)Math.Round(baseColor.R + ((overlayColor.R - baseColor.R) * amount));
            int green = (int)Math.Round(baseColor.G + ((overlayColor.G - baseColor.G) * amount));
            int blue = (int)Math.Round(baseColor.B + ((overlayColor.B - baseColor.B) * amount));
            return Color.FromArgb(red, green, blue);
        }

        private static Color FromHex(string hex)
        {
            return ColorTranslator.FromHtml("#" + hex);
        }

        private static Color FromHsl(double hue, double saturation, double lightness)
        {
            hue %= 360d;
            if (hue < 0d)
            {
                hue += 360d;
            }

            double chroma = (1d - Math.Abs((2d * lightness) - 1d)) * saturation;
            double segment = hue / 60d;
            double x = chroma * (1d - Math.Abs((segment % 2d) - 1d));

            (double red, double green, double blue) = segment switch
            {
                >= 0d and < 1d => (chroma, x, 0d),
                >= 1d and < 2d => (x, chroma, 0d),
                >= 2d and < 3d => (0d, chroma, x),
                >= 3d and < 4d => (0d, x, chroma),
                >= 4d and < 5d => (x, 0d, chroma),
                _ => (chroma, 0d, x)
            };

            double match = lightness - (chroma / 2d);
            return Color.FromArgb(
                (int)Math.Round((red + match) * 255d),
                (int)Math.Round((green + match) * 255d),
                (int)Math.Round((blue + match) * 255d));
        }
    }
}
