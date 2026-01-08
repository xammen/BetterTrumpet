using EarTrumpet.UI.Helpers;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;

namespace EarTrumpet.UI.ViewModels
{
    public class EarTrumpetColorsSettingsPageViewModel : SettingsPageViewModel
    {
        private readonly AppSettings _settings;

        // Default colors
        private static readonly Color DefaultAccentColor = Color.FromRgb(0, 120, 215);
        private static readonly Color DefaultTrackBackground = Color.FromRgb(80, 80, 80);
        private static readonly Color DefaultPeakMeter = Color.FromRgb(255, 255, 255);

        // Themes
        public ObservableCollection<ColorTheme> AvailableThemes { get; }

        private ColorTheme _selectedTheme;
        public ColorTheme SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (_selectedTheme != value && value != null)
                {
                    _selectedTheme = value;
                    ApplyTheme(value);
                    RaisePropertyChanged(nameof(SelectedTheme));
                }
            }
        }

        // Commands
        public ICommand ResetToDefaultCommand { get; }

        // Enable custom colors
        public bool UseCustomSliderColors
        {
            get => _settings.UseCustomSliderColors;
            set
            {
                _settings.UseCustomSliderColors = value;
                RaisePropertyChanged(nameof(UseCustomSliderColors));
            }
        }

        // Thumb Color
        public Color SliderThumbColor
        {
            get
            {
                var color = _settings.SliderThumbColor;
                return color == Colors.Transparent ? DefaultAccentColor : color;
            }
            set
            {
                _settings.SliderThumbColor = value;
                RaisePropertyChanged(nameof(SliderThumbColor));
                RaisePropertyChanged(nameof(SliderThumbColorHex));
            }
        }

        public string SliderThumbColorHex
        {
            get => $"#{SliderThumbColor.R:X2}{SliderThumbColor.G:X2}{SliderThumbColor.B:X2}";
            set
            {
                if (TryParseHexColor(value, out Color color))
                {
                    SliderThumbColor = color;
                }
            }
        }

        // Track Fill Color
        public Color SliderTrackFillColor
        {
            get
            {
                var color = _settings.SliderTrackFillColor;
                return color == Colors.Transparent ? DefaultAccentColor : color;
            }
            set
            {
                _settings.SliderTrackFillColor = value;
                RaisePropertyChanged(nameof(SliderTrackFillColor));
                RaisePropertyChanged(nameof(SliderTrackFillColorHex));
            }
        }

        public string SliderTrackFillColorHex
        {
            get => $"#{SliderTrackFillColor.R:X2}{SliderTrackFillColor.G:X2}{SliderTrackFillColor.B:X2}";
            set
            {
                if (TryParseHexColor(value, out Color color))
                {
                    SliderTrackFillColor = color;
                }
            }
        }

        // Track Background Color
        public Color SliderTrackBackgroundColor
        {
            get
            {
                var color = _settings.SliderTrackBackgroundColor;
                return color == Colors.Transparent ? DefaultTrackBackground : color;
            }
            set
            {
                _settings.SliderTrackBackgroundColor = value;
                RaisePropertyChanged(nameof(SliderTrackBackgroundColor));
                RaisePropertyChanged(nameof(SliderTrackBackgroundColorHex));
            }
        }

        public string SliderTrackBackgroundColorHex
        {
            get => $"#{SliderTrackBackgroundColor.R:X2}{SliderTrackBackgroundColor.G:X2}{SliderTrackBackgroundColor.B:X2}";
            set
            {
                if (TryParseHexColor(value, out Color color))
                {
                    SliderTrackBackgroundColor = color;
                }
            }
        }

        // Peak Meter Color
        public Color PeakMeterColor
        {
            get
            {
                var color = _settings.PeakMeterColor;
                return color == Colors.Transparent ? DefaultPeakMeter : color;
            }
            set
            {
                _settings.PeakMeterColor = value;
                RaisePropertyChanged(nameof(PeakMeterColor));
                RaisePropertyChanged(nameof(PeakMeterColorHex));
            }
        }

        public string PeakMeterColorHex
        {
            get => $"#{PeakMeterColor.R:X2}{PeakMeterColor.G:X2}{PeakMeterColor.B:X2}";
            set
            {
                if (TryParseHexColor(value, out Color color))
                {
                    PeakMeterColor = color;
                }
            }
        }

        public EarTrumpetColorsSettingsPageViewModel(AppSettings settings) : base(null)
        {
            _settings = settings;
            Title = "Appearance";
            Glyph = "\xE790"; // Paintbrush icon

            AvailableThemes = new ObservableCollection<ColorTheme>
            {
                // === DEFAULT ===
                new ColorTheme("Default (Windows Accent)", DefaultAccentColor, DefaultAccentColor, DefaultTrackBackground, DefaultPeakMeter),
                
                // === OLED OPTIMIZED - Minimal bright pixels ===
                new ColorTheme("OLED Pure", Color.FromRgb(0, 200, 200), Color.FromRgb(0, 150, 150), Color.FromRgb(0, 0, 0), Color.FromRgb(0, 80, 80)),
                new ColorTheme("OLED Amber", Color.FromRgb(255, 176, 0), Color.FromRgb(200, 140, 0), Color.FromRgb(0, 0, 0), Color.FromRgb(120, 80, 0)),
                
                // === BRAND INSPIRED ===
                new ColorTheme("Spotify", Color.FromRgb(30, 215, 96), Color.FromRgb(29, 185, 84), Color.FromRgb(18, 18, 18), Color.FromRgb(20, 120, 60)),
                new ColorTheme("Discord", Color.FromRgb(88, 101, 242), Color.FromRgb(71, 82, 196), Color.FromRgb(30, 31, 34), Color.FromRgb(60, 70, 150)),
                new ColorTheme("YouTube", Color.FromRgb(255, 0, 0), Color.FromRgb(200, 0, 0), Color.FromRgb(15, 15, 15), Color.FromRgb(150, 0, 0)),
                
                // === RETRO / AESTHETIC ===
                new ColorTheme("Synthwave", Color.FromRgb(255, 0, 128), Color.FromRgb(0, 255, 255), Color.FromRgb(20, 0, 40), Color.FromRgb(180, 0, 255)),
                new ColorTheme("Matrix", Color.FromRgb(0, 255, 65), Color.FromRgb(0, 200, 50), Color.FromRgb(0, 10, 0), Color.FromRgb(0, 120, 30)),
                new ColorTheme("Amber CRT", Color.FromRgb(255, 180, 0), Color.FromRgb(200, 140, 0), Color.FromRgb(20, 15, 0), Color.FromRgb(150, 100, 0)),
                new ColorTheme("Pip-Boy", Color.FromRgb(16, 255, 16), Color.FromRgb(16, 220, 16), Color.FromRgb(10, 25, 10), Color.FromRgb(10, 150, 10)),
                
                // === DEV THEMES ===
                new ColorTheme("Dracula", Color.FromRgb(189, 147, 249), Color.FromRgb(255, 121, 198), Color.FromRgb(40, 42, 54), Color.FromRgb(139, 233, 253)),
                new ColorTheme("Nord", Color.FromRgb(136, 192, 208), Color.FromRgb(129, 161, 193), Color.FromRgb(46, 52, 64), Color.FromRgb(94, 129, 172)),
                new ColorTheme("Monokai", Color.FromRgb(249, 38, 114), Color.FromRgb(166, 226, 46), Color.FromRgb(39, 40, 34), Color.FromRgb(102, 217, 239)),
                
                // === NATURE ===
                new ColorTheme("Aurora", Color.FromRgb(0, 255, 170), Color.FromRgb(120, 0, 255), Color.FromRgb(10, 20, 30), Color.FromRgb(0, 180, 120)),
                new ColorTheme("Sunset", Color.FromRgb(255, 95, 109), Color.FromRgb(255, 195, 113), Color.FromRgb(30, 20, 25), Color.FromRgb(200, 100, 80)),
                new ColorTheme("Ocean Deep", Color.FromRgb(0, 180, 216), Color.FromRgb(0, 119, 182), Color.FromRgb(3, 4, 30), Color.FromRgb(0, 100, 130)),
                
                // === PREMIUM ===
                new ColorTheme("Rose Gold", Color.FromRgb(183, 110, 121), Color.FromRgb(150, 90, 100), Color.FromRgb(30, 25, 27), Color.FromRgb(220, 150, 160)),
                new ColorTheme("Midnight Blue", Color.FromRgb(100, 149, 237), Color.FromRgb(65, 105, 225), Color.FromRgb(15, 20, 35), Color.FromRgb(70, 100, 180)),
                
                // === ACCESSIBILITY ===
                new ColorTheme("High Contrast", Color.FromRgb(255, 255, 255), Color.FromRgb(255, 255, 0), Color.FromRgb(0, 0, 0), Color.FromRgb(0, 255, 255)),
                new ColorTheme("Grayscale", Color.FromRgb(220, 220, 220), Color.FromRgb(160, 160, 160), Color.FromRgb(35, 35, 35), Color.FromRgb(100, 100, 100)),
            };

            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        }
        
        /// <summary>
        /// Called by the ColorPicker when a color is selected
        /// </summary>
        public void ApplyPickedColor(Color color, string propertyName)
        {
            switch (propertyName)
            {
                case "Thumb":
                    SliderThumbColor = color;
                    break;
                case "TrackFill":
                    SliderTrackFillColor = color;
                    break;
                case "TrackBackground":
                    SliderTrackBackgroundColor = color;
                    break;
                case "PeakMeter":
                    PeakMeterColor = color;
                    break;
            }
            
            if (!UseCustomSliderColors)
            {
                UseCustomSliderColors = true;
            }
        }

        private void ApplyTheme(ColorTheme theme)
        {
            if (theme == null) return;

            SliderThumbColor = theme.ThumbColor;
            SliderTrackFillColor = theme.TrackFillColor;
            SliderTrackBackgroundColor = theme.TrackBackgroundColor;
            PeakMeterColor = theme.PeakMeterColor;

            if (!UseCustomSliderColors)
            {
                UseCustomSliderColors = true;
            }
        }

        private void ResetToDefault()
        {
            UseCustomSliderColors = false;
            _selectedTheme = null;
            RaisePropertyChanged(nameof(SelectedTheme));
        }

        private bool TryParseHexColor(string hex, out Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrWhiteSpace(hex)) return false;

            hex = hex.TrimStart('#');
            if (hex.Length != 6) return false;

            try
            {
                byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
                color = Color.FromRgb(r, g, b);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override void NavigatedTo()
        {
            base.NavigatedTo();
            try
            {
                var appType = System.Windows.Application.Current.GetType();
                var method = appType.GetMethod("OpenMixerWindow");
                method?.Invoke(System.Windows.Application.Current, null);
            }
            catch { }
        }
    }
}
