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
            Title = "Colors"; // TODO: Add to Resources
            Glyph = "\xE790"; // Paintbrush icon

            AvailableThemes = new ObservableCollection<ColorTheme>
            {
                new ColorTheme("Default (Windows Accent)", DefaultAccentColor, DefaultAccentColor, DefaultTrackBackground, DefaultPeakMeter),
                new ColorTheme("OLED Dark", Color.FromRgb(255, 255, 255), Color.FromRgb(255, 255, 255), Color.FromRgb(30, 30, 30), Color.FromRgb(100, 100, 100)),
                new ColorTheme("Fallout Pip-Boy", Color.FromRgb(16, 255, 16), Color.FromRgb(16, 255, 16), Color.FromRgb(20, 40, 20), Color.FromRgb(10, 180, 10)),
                new ColorTheme("Cyberpunk", Color.FromRgb(255, 0, 128), Color.FromRgb(0, 255, 255), Color.FromRgb(20, 20, 40), Color.FromRgb(255, 0, 255)),
                new ColorTheme("Sunset", Color.FromRgb(255, 100, 50), Color.FromRgb(255, 150, 50), Color.FromRgb(60, 30, 30), Color.FromRgb(255, 200, 100)),
                new ColorTheme("Ocean", Color.FromRgb(0, 150, 255), Color.FromRgb(0, 100, 200), Color.FromRgb(20, 40, 60), Color.FromRgb(100, 200, 255)),
                new ColorTheme("Forest", Color.FromRgb(50, 180, 80), Color.FromRgb(40, 150, 60), Color.FromRgb(20, 40, 25), Color.FromRgb(100, 200, 120)),
                new ColorTheme("Monochrome", Color.FromRgb(200, 200, 200), Color.FromRgb(150, 150, 150), Color.FromRgb(50, 50, 50), Color.FromRgb(100, 100, 100)),
                new ColorTheme("Blood Red", Color.FromRgb(200, 0, 0), Color.FromRgb(180, 0, 0), Color.FromRgb(40, 15, 15), Color.FromRgb(255, 50, 50)),
                new ColorTheme("Purple Haze", Color.FromRgb(150, 50, 255), Color.FromRgb(120, 40, 200), Color.FromRgb(30, 20, 50), Color.FromRgb(180, 100, 255)),
                new ColorTheme("Gold Luxury", Color.FromRgb(255, 215, 0), Color.FromRgb(218, 165, 32), Color.FromRgb(40, 35, 20), Color.FromRgb(255, 230, 100)),
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
