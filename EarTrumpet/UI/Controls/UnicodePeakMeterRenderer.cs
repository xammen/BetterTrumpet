using System;
using System.Text;

namespace EarTrumpet.UI.Controls
{
    /// <summary>
    /// Renders peak meter levels as Unicode character strings.
    /// Each style uses different Unicode block/braille characters to create
    /// a visual meter that scales to a given character width.
    /// 
    /// Usage: UnicodePeakMeterRenderer.Render(style, ratio, charWidth)
    /// where ratio is 0.0..1.0 and charWidth is the number of characters to fill.
    /// </summary>
    public static class UnicodePeakMeterRenderer
    {
        // ─── Dotted (Braille) ───────────────────────────────
        // Full: ⣿  Mid: ⣤  Low: ⣀  Empty: ⠀ (braille blank)
        private const char BrailleFull = '\u28FF';   // ⣿
        private const char BrailleHigh = '\u28F6';   // ⢶ (3/4)
        private const char BrailleMid = '\u28A4';    // ⣤ (1/2)
        private const char BrailleLow = '\u2880';    // ⢀ (1/4)
        private const char BrailleEmpty = '\u2800';  // ⠀

        // ─── Blocks ─────────────────────────────────────────
        // Full: █  3/4: ▓  1/2: ▒  1/4: ░  Empty: ·
        private const char BlockFull = '\u2588';     // █
        private const char BlockHigh = '\u2593';     // ▓
        private const char BlockMid = '\u2592';      // ▒
        private const char BlockLow = '\u2591';      // ░
        private const char BlockEmpty = '\u00B7';    // · (middle dot)

        // ─── Bars (box-drawing thin bars) ───────────────────
        // Active: ┃  Dim: ╎  Empty: ·
        private const char BarActive = '\u2503';     // ┃
        private const char BarDim = '\u254E';        // ╎
        private const char BarEmpty = ' ';

        // ─── Wave ───────────────────────────────────────────
        // Active: ≋  Mid: ∿  Dim: ⋯  Empty: ·
        private const char WaveActive = '\u224B';    // ≋
        private const char WaveMid = '\u223F';       // ∿
        private const char WaveDim = '\u22EF';       // ⋯
        private const char WaveEmpty = '\u00B7';     // ·

        /// <summary>
        /// Render a peak meter string for the given style.
        /// </summary>
        /// <param name="style">The visual style to use</param>
        /// <param name="ratio">Peak level from 0.0 to 1.0</param>
        /// <param name="charWidth">Number of characters in the output string</param>
        /// <returns>A Unicode string representing the peak level</returns>
        public static string Render(PeakMeterStyle style, double ratio, int charWidth)
        {
            if (charWidth <= 0) return string.Empty;
            ratio = Math.Max(0.0, Math.Min(1.0, ratio));

            switch (style)
            {
                case PeakMeterStyle.Dotted:
                    return RenderGradient(ratio, charWidth, BrailleFull, BrailleHigh, BrailleMid, BrailleLow, BrailleEmpty);
                case PeakMeterStyle.Blocks:
                    return RenderGradient(ratio, charWidth, BlockFull, BlockHigh, BlockMid, BlockLow, BlockEmpty);
                case PeakMeterStyle.Bars:
                    return RenderBars(ratio, charWidth);
                case PeakMeterStyle.Wave:
                    return RenderGradient(ratio, charWidth, WaveActive, WaveActive, WaveMid, WaveDim, WaveEmpty);
                default:
                    return string.Empty; // Classic mode doesn't use text rendering
            }
        }

        /// <summary>
        /// Gradient-style rendering: filled chars transition through intensity levels.
        /// The "edge" character (the one at the boundary) uses a fractional symbol.
        /// </summary>
        private static string RenderGradient(double ratio, int width, char full, char high, char mid, char low, char empty)
        {
            var sb = new StringBuilder(width);
            double scaledPos = ratio * width;
            int fullChars = (int)scaledPos;
            double fraction = scaledPos - fullChars;

            for (int i = 0; i < width; i++)
            {
                if (i < fullChars)
                {
                    sb.Append(full);
                }
                else if (i == fullChars)
                {
                    // Edge character: pick intensity based on fraction
                    if (fraction >= 0.75) sb.Append(high);
                    else if (fraction >= 0.5) sb.Append(mid);
                    else if (fraction >= 0.25) sb.Append(low);
                    else sb.Append(empty);
                }
                else
                {
                    sb.Append(empty);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Bar-style rendering: active bars, then dim bars, no gradient.
        /// Creates a clean "VU meter" look with discrete steps.
        /// </summary>
        private static string RenderBars(double ratio, int width)
        {
            var sb = new StringBuilder(width);
            int activeCount = (int)Math.Round(ratio * width);

            for (int i = 0; i < width; i++)
            {
                if (i < activeCount)
                    sb.Append(BarActive);
                else
                    sb.Append(BarDim);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get a preview string (fixed 12 chars at ~60% level) for settings UI.
        /// </summary>
        public static string GetPreview(PeakMeterStyle style)
        {
            if (style == PeakMeterStyle.Classic)
                return "████████████"; // Fake solid bar preview
            return Render(style, 0.6, 12);
        }

        /// <summary>
        /// Get the display name for a peak meter style.
        /// </summary>
        public static string GetDisplayName(PeakMeterStyle style)
        {
            switch (style)
            {
                case PeakMeterStyle.Classic: return "Classic";
                case PeakMeterStyle.Dotted: return "Dotted";
                case PeakMeterStyle.Blocks: return "Blocks";
                case PeakMeterStyle.Bars: return "Bars";
                case PeakMeterStyle.Wave: return "Wave";
                default: return style.ToString();
            }
        }
    }
}
