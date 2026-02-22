using System.Windows.Media;

namespace EarTrumpet.UI.ViewModels
{
    public class ColorTheme
    {
        // Original 4 slider colors
        public string Name { get; set; }
        public string Category { get; set; }
        public Color ThumbColor { get; set; }
        public Color TrackFillColor { get; set; }
        public Color TrackBackgroundColor { get; set; }
        public Color PeakMeterColor { get; set; }

        // Extended theme properties (Niveau 2.1)
        public Color WindowBackgroundColor { get; set; }
        public Color TextColor { get; set; }
        public Color AccentGlowColor { get; set; }

        // Is this a user-created custom theme?
        public bool IsCustom { get; set; }

        /// <summary>
        /// Legacy constructor (4 colors, no category)
        /// </summary>
        public ColorTheme(string name, Color thumb, Color fill, Color background, Color peak)
            : this(name, "Default", thumb, fill, background, peak, 
                   Color.FromRgb(0, 0, 0), Color.FromRgb(255, 255, 255), fill)
        {
        }

        /// <summary>
        /// Full constructor with category + extended colors
        /// </summary>
        public ColorTheme(string name, string category, Color thumb, Color fill, Color background, Color peak,
                          Color windowBg, Color text, Color accentGlow)
        {
            Name = name;
            Category = category;
            ThumbColor = thumb;
            TrackFillColor = fill;
            TrackBackgroundColor = background;
            PeakMeterColor = peak;
            WindowBackgroundColor = windowBg;
            TextColor = text;
            AccentGlowColor = accentGlow;
        }

        /// <summary>
        /// Serialize to JSON string for import/export
        /// </summary>
        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                Name,
                Category,
                Thumb = $"#{ThumbColor.R:X2}{ThumbColor.G:X2}{ThumbColor.B:X2}",
                Fill = $"#{TrackFillColor.R:X2}{TrackFillColor.G:X2}{TrackFillColor.B:X2}",
                Background = $"#{TrackBackgroundColor.R:X2}{TrackBackgroundColor.G:X2}{TrackBackgroundColor.B:X2}",
                Peak = $"#{PeakMeterColor.R:X2}{PeakMeterColor.G:X2}{PeakMeterColor.B:X2}",
                WindowBg = $"#{WindowBackgroundColor.R:X2}{WindowBackgroundColor.G:X2}{WindowBackgroundColor.B:X2}",
                Text = $"#{TextColor.R:X2}{TextColor.G:X2}{TextColor.B:X2}",
                Glow = $"#{AccentGlowColor.R:X2}{AccentGlowColor.G:X2}{AccentGlowColor.B:X2}",
            }, Newtonsoft.Json.Formatting.Indented);
        }

        /// <summary>
        /// Deserialize from JSON string
        /// </summary>
        public static ColorTheme FromJson(string json)
        {
            var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
            return new ColorTheme(
                (string)obj.Name,
                (string)(obj.Category ?? "Custom"),
                ParseHex((string)obj.Thumb),
                ParseHex((string)obj.Fill),
                ParseHex((string)obj.Background),
                ParseHex((string)obj.Peak),
                ParseHex((string)(obj.WindowBg ?? "#000000")),
                ParseHex((string)(obj.Text ?? "#FFFFFF")),
                ParseHex((string)(obj.Glow ?? (string)obj.Fill))
            ) { IsCustom = true };
        }

        private static Color ParseHex(string hex)
        {
            hex = hex.TrimStart('#');
            if (hex.Length != 6) return Colors.White;
            byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
            return Color.FromRgb(r, g, b);
        }
    }
}
