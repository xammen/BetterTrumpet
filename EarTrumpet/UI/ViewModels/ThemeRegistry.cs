using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace EarTrumpet.UI.ViewModels
{
    /// <summary>
    /// Central registry of predefined color themes with categories and extended colors.
    /// Every theme uses complementary / split-complementary palettes — no monochrome allowed.
    /// Constructor: (name, category, thumb, fill, background, peak, windowBg, text, accentGlow)
    /// </summary>
    public static class ThemeRegistry
    {
        // Default colors (Windows accent blue as fallback)
        public static readonly Color DefaultAccentColor = Color.FromRgb(0, 120, 215);
        public static readonly Color DefaultTrackBackground = Color.FromRgb(80, 80, 80);
        public static readonly Color DefaultPeakMeter = Color.FromRgb(255, 255, 255);

        // Category constants
        public const string CatDefault = "Minimal";
        public const string CatBrand = "Brand";
        public const string CatRetro = "Retro / Gaming";
        public const string CatDev = "Developer";
        public const string CatNature = "Nature / Premium";
        public const string CatAccessibility = "Accessibility";

        /// <summary>
        /// All category names in display order
        /// </summary>
        public static IReadOnlyList<string> Categories { get; } = new[]
        {
            CatDefault, CatBrand, CatRetro, CatDev, CatNature, CatAccessibility
        };

        public static IReadOnlyList<ColorTheme> AllThemes { get; } = new List<ColorTheme>
        {
            // ═══════════════════════════════════════════
            // MINIMAL — clean, elegant, understated
            // ═══════════════════════════════════════════

            // Windows default: blue accent with warm white text
            new ColorTheme("Default (Windows Accent)", CatDefault,
                DefaultAccentColor, DefaultAccentColor, DefaultTrackBackground, DefaultPeakMeter,
                Color.FromRgb(32, 32, 32), Color.FromRgb(255, 255, 255), DefaultAccentColor),

            // True black + cyan accent, warm amber peak for contrast
            new ColorTheme("OLED Pure", CatDefault,
                Color.FromRgb(0, 210, 210),    // thumb: bright cyan
                Color.FromRgb(0, 160, 160),    // fill: mid cyan
                Color.FromRgb(5, 5, 5),        // track bg: near black
                Color.FromRgb(255, 160, 50),   // peak: warm amber (complement)
                Color.FromRgb(2, 2, 2), Color.FromRgb(0, 230, 220), Color.FromRgb(0, 200, 200)),

            // OLED warm: amber/gold thumb, deep blue peak
            new ColorTheme("OLED Amber", CatDefault,
                Color.FromRgb(255, 180, 0),    // thumb: bright amber
                Color.FromRgb(200, 140, 0),    // fill: darker gold
                Color.FromRgb(5, 5, 5),        // track bg: near black
                Color.FromRgb(80, 140, 255),   // peak: soft blue (complement)
                Color.FromRgb(2, 2, 2), Color.FromRgb(255, 210, 100), Color.FromRgb(255, 176, 0)),

            // Elegant grayscale with a whisper of blue
            new ColorTheme("Grayscale", CatDefault,
                Color.FromRgb(225, 225, 225),  // thumb: bright gray
                Color.FromRgb(140, 150, 165),  // fill: cool steel (hint of blue)
                Color.FromRgb(30, 30, 32),     // track bg: charcoal
                Color.FromRgb(90, 95, 105),    // peak: medium steel
                Color.FromRgb(22, 22, 24), Color.FromRgb(195, 200, 210), Color.FromRgb(160, 165, 175)),

            // ═══════════════════════════════════════════
            // BRAND — inspired by real apps, not just "their color"
            // ═══════════════════════════════════════════

            // Spotify: green accent, warm background, cream text, pink-coral peak
            new ColorTheme("Spotify", CatBrand,
                Color.FromRgb(30, 215, 96),    // thumb: spotify green
                Color.FromRgb(25, 175, 80),    // fill: darker green
                Color.FromRgb(40, 40, 40),     // track bg: spotify dark gray
                Color.FromRgb(255, 120, 100),  // peak: coral/salmon (complement of green)
                Color.FromRgb(18, 18, 18), Color.FromRgb(210, 210, 210), Color.FromRgb(30, 215, 96)),

            // Discord: blurple thumb, green fill for online status, muted rose peak
            new ColorTheme("Discord", CatBrand,
                Color.FromRgb(88, 101, 242),   // thumb: blurple
                Color.FromRgb(87, 242, 135),   // fill: discord green (online)
                Color.FromRgb(30, 31, 34),     // track bg: discord dark
                Color.FromRgb(240, 178, 50),   // peak: warm gold (nitro vibes)
                Color.FromRgb(30, 31, 34), Color.FromRgb(219, 222, 225), Color.FromRgb(88, 101, 242)),

            // YouTube: red thumb, warm white fill, dark track, teal peak
            new ColorTheme("YouTube", CatBrand,
                Color.FromRgb(255, 0, 0),      // thumb: youtube red
                Color.FromRgb(255, 255, 255),  // fill: white (like progress bar)
                Color.FromRgb(38, 38, 38),     // track bg: yt dark
                Color.FromRgb(50, 200, 180),   // peak: teal (complement of red)
                Color.FromRgb(15, 15, 15), Color.FromRgb(241, 241, 241), Color.FromRgb(255, 48, 48)),

            // Twitch: purple thumb, aqua fill, dark bg, warm gold peak
            new ColorTheme("Twitch", CatBrand,
                Color.FromRgb(145, 70, 255),   // thumb: twitch purple
                Color.FromRgb(100, 205, 230),  // fill: aqua (chat vibes)
                Color.FromRgb(24, 24, 27),     // track bg: twitch dark
                Color.FromRgb(255, 180, 60),   // peak: warm gold (bits/subs)
                Color.FromRgb(14, 14, 16), Color.FromRgb(210, 210, 215), Color.FromRgb(145, 70, 255)),

            // Slack: aubergine + multicolor workspace feel
            new ColorTheme("Slack", CatBrand,
                Color.FromRgb(74, 21, 75),     // thumb: aubergine
                Color.FromRgb(54, 197, 171),   // fill: slack teal
                Color.FromRgb(25, 25, 28),     // track bg: dark
                Color.FromRgb(236, 178, 46),   // peak: slack gold
                Color.FromRgb(18, 18, 20), Color.FromRgb(210, 215, 220), Color.FromRgb(74, 21, 75)),

            // ═══════════════════════════════════════════
            // RETRO / GAMING — nostalgic and fun
            // ═══════════════════════════════════════════

            // Neon-soaked 80s: hot pink thumb, cyan fill, purple background, orange peak
            new ColorTheme("Synthwave", CatRetro,
                Color.FromRgb(255, 16, 128),   // thumb: hot pink
                Color.FromRgb(0, 255, 255),    // fill: neon cyan
                Color.FromRgb(25, 5, 45),      // track bg: deep purple
                Color.FromRgb(255, 160, 0),    // peak: neon orange
                Color.FromRgb(15, 2, 30), Color.FromRgb(255, 130, 220), Color.FromRgb(180, 0, 255)),

            // Matrix: green rain, dark bg, amber warnings
            new ColorTheme("Matrix", CatRetro,
                Color.FromRgb(0, 255, 65),     // thumb: matrix green
                Color.FromRgb(0, 190, 50),     // fill: darker green
                Color.FromRgb(5, 12, 5),       // track bg: very dark green
                Color.FromRgb(200, 170, 0),    // peak: amber/gold (contrast)
                Color.FromRgb(3, 8, 3), Color.FromRgb(80, 255, 100), Color.FromRgb(0, 200, 50)),

            // Classic amber CRT with green scanline accents
            new ColorTheme("Amber CRT", CatRetro,
                Color.FromRgb(255, 180, 0),    // thumb: amber
                Color.FromRgb(220, 150, 0),    // fill: gold
                Color.FromRgb(18, 14, 4),      // track bg: dark amber tint
                Color.FromRgb(100, 220, 60),   // peak: phosphor green (classic CRT)
                Color.FromRgb(12, 10, 2), Color.FromRgb(255, 200, 60), Color.FromRgb(200, 140, 0)),

            // Fallout Pip-Boy: green UI, amber highlights
            new ColorTheme("Pip-Boy", CatRetro,
                Color.FromRgb(16, 255, 16),    // thumb: pip-boy green
                Color.FromRgb(12, 200, 12),    // fill: darker green
                Color.FromRgb(12, 28, 12),     // track bg: dark military green
                Color.FromRgb(255, 200, 40),   // peak: amber warning (vault-tec)
                Color.FromRgb(8, 20, 8), Color.FromRgb(50, 255, 60), Color.FromRgb(16, 220, 16)),

            // Arcade neon: multi-color coin-op feel
            new ColorTheme("Arcade", CatRetro,
                Color.FromRgb(255, 255, 0),    // thumb: arcade yellow
                Color.FromRgb(0, 200, 255),    // fill: electric blue
                Color.FromRgb(20, 10, 30),     // track bg: dark purple
                Color.FromRgb(255, 50, 100),   // peak: hot pink
                Color.FromRgb(12, 6, 20), Color.FromRgb(255, 255, 200), Color.FromRgb(255, 200, 0)),

            // ═══════════════════════════════════════════
            // DEVELOPER — accurate editor palettes
            // ═══════════════════════════════════════════

            // Dracula: purple thumb, pink fill, cyan peak (actual dracula palette)
            new ColorTheme("Dracula", CatDev,
                Color.FromRgb(189, 147, 249),  // thumb: purple
                Color.FromRgb(255, 121, 198),  // fill: pink
                Color.FromRgb(40, 42, 54),     // track bg: dracula bg
                Color.FromRgb(139, 233, 253),  // peak: cyan
                Color.FromRgb(40, 42, 54), Color.FromRgb(248, 248, 242), Color.FromRgb(189, 147, 249)),

            // Nord: frost blue thumb, aurora green fill, snow storm peak
            new ColorTheme("Nord", CatDev,
                Color.FromRgb(136, 192, 208),  // thumb: nord frost
                Color.FromRgb(163, 190, 140),  // fill: nord aurora green
                Color.FromRgb(46, 52, 64),     // track bg: nord polar night
                Color.FromRgb(208, 135, 112),  // peak: nord aurora orange
                Color.FromRgb(46, 52, 64), Color.FromRgb(216, 222, 233), Color.FromRgb(136, 192, 208)),

            // Monokai: pink thumb, green fill, cyan peak, orange glow
            new ColorTheme("Monokai", CatDev,
                Color.FromRgb(249, 38, 114),   // thumb: monokai pink
                Color.FromRgb(166, 226, 46),   // fill: monokai green
                Color.FromRgb(39, 40, 34),     // track bg: monokai bg
                Color.FromRgb(102, 217, 239),  // peak: monokai cyan
                Color.FromRgb(39, 40, 34), Color.FromRgb(248, 248, 242), Color.FromRgb(253, 151, 31)),

            // Catppuccin Mocha: mauve thumb, blue fill, green peak
            new ColorTheme("Catppuccin", CatDev,
                Color.FromRgb(203, 166, 247),  // thumb: mauve
                Color.FromRgb(137, 180, 250),  // fill: blue
                Color.FromRgb(30, 30, 46),     // track bg: base
                Color.FromRgb(166, 227, 161),  // peak: green
                Color.FromRgb(30, 30, 46), Color.FromRgb(205, 214, 244), Color.FromRgb(245, 194, 231)),

            // One Dark: blue thumb, green fill, red peak (actual one dark syntax colors)
            new ColorTheme("One Dark", CatDev,
                Color.FromRgb(97, 175, 239),   // thumb: blue
                Color.FromRgb(152, 195, 121),  // fill: green
                Color.FromRgb(40, 44, 52),     // track bg: one dark bg
                Color.FromRgb(224, 108, 117),  // peak: red
                Color.FromRgb(40, 44, 52), Color.FromRgb(171, 178, 191), Color.FromRgb(97, 175, 239)),

            // Gruvbox: warm, earthy, retro IDE feel
            new ColorTheme("Gruvbox", CatDev,
                Color.FromRgb(215, 153, 33),   // thumb: gruvbox yellow
                Color.FromRgb(152, 151, 26),   // fill: gruvbox green
                Color.FromRgb(40, 40, 40),     // track bg: gruvbox bg
                Color.FromRgb(211, 134, 155),  // peak: gruvbox purple/pink
                Color.FromRgb(40, 40, 40), Color.FromRgb(235, 219, 178), Color.FromRgb(254, 128, 25)),

            // Solarized Dark: teal/cyan with warm orange accents
            new ColorTheme("Solarized", CatDev,
                Color.FromRgb(38, 139, 210),   // thumb: solarized blue
                Color.FromRgb(42, 161, 152),   // fill: solarized cyan
                Color.FromRgb(0, 43, 54),      // track bg: solarized base03
                Color.FromRgb(203, 75, 22),    // peak: solarized orange
                Color.FromRgb(0, 43, 54), Color.FromRgb(147, 161, 161), Color.FromRgb(38, 139, 210)),

            // ═══════════════════════════════════════════
            // NATURE / PREMIUM — rich, atmospheric
            // ═══════════════════════════════════════════

            // Northern lights: green-teal thumb, purple fill, starry dark bg
            new ColorTheme("Aurora", CatNature,
                Color.FromRgb(0, 255, 170),    // thumb: aurora green
                Color.FromRgb(120, 60, 220),   // fill: aurora purple
                Color.FromRgb(8, 15, 25),      // track bg: night sky
                Color.FromRgb(255, 140, 180),  // peak: dawn pink
                Color.FromRgb(5, 10, 18), Color.FromRgb(190, 240, 220), Color.FromRgb(0, 220, 150)),

            // Warm sunset gradient feel: coral thumb, gold fill, indigo peak
            new ColorTheme("Sunset", CatNature,
                Color.FromRgb(255, 95, 109),   // thumb: coral
                Color.FromRgb(255, 195, 113),  // fill: warm gold
                Color.FromRgb(28, 18, 25),     // track bg: dusk
                Color.FromRgb(100, 80, 200),   // peak: twilight indigo
                Color.FromRgb(25, 15, 22), Color.FromRgb(255, 225, 210), Color.FromRgb(255, 120, 100)),

            // Deep sea: cyan-teal accents, warm bioluminescent peak
            new ColorTheme("Ocean Deep", CatNature,
                Color.FromRgb(0, 180, 216),    // thumb: ocean cyan
                Color.FromRgb(0, 119, 182),    // fill: deep blue
                Color.FromRgb(3, 8, 28),       // track bg: abyss
                Color.FromRgb(180, 255, 100),  // peak: bioluminescent green
                Color.FromRgb(2, 5, 20), Color.FromRgb(144, 224, 239), Color.FromRgb(0, 180, 216)),

            // Elegant rose with gold accents
            new ColorTheme("Rose Gold", CatNature,
                Color.FromRgb(200, 120, 130),  // thumb: rose
                Color.FromRgb(218, 175, 100),  // fill: gold
                Color.FromRgb(32, 24, 28),     // track bg: deep mauve
                Color.FromRgb(130, 200, 180),  // peak: sage green (complement)
                Color.FromRgb(28, 20, 24), Color.FromRgb(245, 215, 220), Color.FromRgb(200, 140, 130)),

            // Cool midnight with warm star accents
            new ColorTheme("Midnight Blue", CatNature,
                Color.FromRgb(100, 149, 237),  // thumb: cornflower blue
                Color.FromRgb(65, 105, 225),   // fill: royal blue
                Color.FromRgb(12, 16, 32),     // track bg: midnight
                Color.FromRgb(255, 200, 100),  // peak: warm starlight
                Color.FromRgb(8, 12, 28), Color.FromRgb(176, 196, 240), Color.FromRgb(100, 149, 237)),

            // Delicate pink with mint green accents
            new ColorTheme("Cherry Blossom", CatNature,
                Color.FromRgb(255, 150, 170),  // thumb: sakura pink
                Color.FromRgb(255, 200, 210),  // fill: light petal
                Color.FromRgb(32, 22, 28),     // track bg: bark dark
                Color.FromRgb(130, 210, 170),  // peak: spring green (complement)
                Color.FromRgb(28, 18, 24), Color.FromRgb(255, 235, 240), Color.FromRgb(255, 120, 150)),

            // Lush forest: green thumb, earthy brown fill, golden peak
            new ColorTheme("Forest", CatNature,
                Color.FromRgb(60, 180, 75),    // thumb: leaf green
                Color.FromRgb(140, 110, 70),   // fill: bark brown
                Color.FromRgb(15, 22, 12),     // track bg: deep forest
                Color.FromRgb(255, 200, 60),   // peak: sunlight gold
                Color.FromRgb(10, 16, 8), Color.FromRgb(190, 220, 180), Color.FromRgb(80, 200, 90)),

            // ═══════════════════════════════════════════
            // ACCESSIBILITY — high contrast, readable
            // ═══════════════════════════════════════════

            // Maximum contrast: white on black, yellow accents
            new ColorTheme("High Contrast", CatAccessibility,
                Color.FromRgb(255, 255, 255),  // thumb: white
                Color.FromRgb(255, 255, 0),    // fill: yellow
                Color.FromRgb(0, 0, 0),        // track bg: black
                Color.FromRgb(0, 255, 255),    // peak: cyan
                Color.FromRgb(0, 0, 0), Color.FromRgb(255, 255, 255), Color.FromRgb(255, 255, 0)),

            // Deuteranopia-safe: blue/orange instead of red/green
            new ColorTheme("Color Blind Safe", CatAccessibility,
                Color.FromRgb(0, 114, 178),    // thumb: CB blue
                Color.FromRgb(230, 159, 0),    // fill: CB orange
                Color.FromRgb(15, 15, 18),     // track bg: dark
                Color.FromRgb(204, 121, 167),  // peak: CB pink
                Color.FromRgb(10, 10, 12), Color.FromRgb(220, 220, 230), Color.FromRgb(86, 180, 233)),
        };

        /// <summary>
        /// Get themes grouped by category, in display order.
        /// </summary>
        public static IEnumerable<IGrouping<string, ColorTheme>> GetGroupedThemes()
        {
            return AllThemes.GroupBy(t => t.Category)
                           .OrderBy(g => System.Array.IndexOf(Categories.ToArray(), g.Key));
        }
    }
}
