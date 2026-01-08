using EarTrumpet.UI.Helpers;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;

namespace EarTrumpet.UI.ViewModels
{
    public class ColorTheme
    {
        public string Name { get; set; }
        public Color ThumbColor { get; set; }
        public Color TrackFillColor { get; set; }
        public Color TrackBackgroundColor { get; set; }
        public Color PeakMeterColor { get; set; }
        
        public ColorTheme(string name, Color thumb, Color fill, Color background, Color peak)
        {
            Name = name;
            ThumbColor = thumb;
            TrackFillColor = fill;
            TrackBackgroundColor = background;
            PeakMeterColor = peak;
        }
    }

    public class EarTrumpetCustomizationSettingsPageViewModel : SettingsPageViewModel
    {
        private readonly AppSettings _settings;
        
        // Default colors (Windows accent blue as fallback)
        private static readonly Color DefaultAccentColor = Color.FromRgb(0, 120, 215);
        private static readonly Color DefaultTrackBackground = Color.FromRgb(80, 80, 80);
        private static readonly Color DefaultPeakMeter = Color.FromRgb(255, 255, 255);

        // Predefined themes
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
        public ICommand SavePresetCommand { get; }

        // Animation settings
        public bool UseSmoothVolumeAnimation
        {
            get => _settings.UseSmoothVolumeAnimation;
            set
            {
                _settings.UseSmoothVolumeAnimation = value;
                RaisePropertyChanged(nameof(UseSmoothVolumeAnimation));
            }
        }

        // Animation speed: 1 (slow) to 10 (fast), maps to 0.02-0.5 internally
        public int VolumeAnimationSpeed
        {
            get
            {
                // Convert internal value (0.02-0.5) to UI value (1-10)
                double internalValue = _settings.VolumeAnimationSpeed;
                int uiValue = (int)System.Math.Round((internalValue - 0.02) / 0.053 + 1);
                return System.Math.Max(1, System.Math.Min(10, uiValue));
            }
            set
            {
                // Convert UI value (1-10) to internal value (0.02-0.5)
                double internalValue = 0.02 + (value - 1) * 0.053;
                _settings.VolumeAnimationSpeed = internalValue;
                RaisePropertyChanged(nameof(VolumeAnimationSpeed));
            }
        }

        // Color settings
        public bool UseCustomSliderColors
        {
            get => _settings.UseCustomSliderColors;
            set
            {
                _settings.UseCustomSliderColors = value;
                RaisePropertyChanged(nameof(UseCustomSliderColors));
            }
        }

        // Slider Thumb Color - RGB components
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
                RaisePropertyChanged(nameof(SliderThumbColorR));
                RaisePropertyChanged(nameof(SliderThumbColorG));
                RaisePropertyChanged(nameof(SliderThumbColorB));
            }
        }

        public byte SliderThumbColorR
        {
            get => SliderThumbColor.R;
            set => SliderThumbColor = Color.FromRgb(value, SliderThumbColor.G, SliderThumbColor.B);
        }

        public byte SliderThumbColorG
        {
            get => SliderThumbColor.G;
            set => SliderThumbColor = Color.FromRgb(SliderThumbColor.R, value, SliderThumbColor.B);
        }

        public byte SliderThumbColorB
        {
            get => SliderThumbColor.B;
            set => SliderThumbColor = Color.FromRgb(SliderThumbColor.R, SliderThumbColor.G, value);
        }

        // Track Fill Color - RGB components
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
                RaisePropertyChanged(nameof(SliderTrackFillColorR));
                RaisePropertyChanged(nameof(SliderTrackFillColorG));
                RaisePropertyChanged(nameof(SliderTrackFillColorB));
            }
        }

        public byte SliderTrackFillColorR
        {
            get => SliderTrackFillColor.R;
            set => SliderTrackFillColor = Color.FromRgb(value, SliderTrackFillColor.G, SliderTrackFillColor.B);
        }

        public byte SliderTrackFillColorG
        {
            get => SliderTrackFillColor.G;
            set => SliderTrackFillColor = Color.FromRgb(SliderTrackFillColor.R, value, SliderTrackFillColor.B);
        }

        public byte SliderTrackFillColorB
        {
            get => SliderTrackFillColor.B;
            set => SliderTrackFillColor = Color.FromRgb(SliderTrackFillColor.R, SliderTrackFillColor.G, value);
        }

        // Track Background Color - RGB components
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
                RaisePropertyChanged(nameof(SliderTrackBackgroundColorR));
                RaisePropertyChanged(nameof(SliderTrackBackgroundColorG));
                RaisePropertyChanged(nameof(SliderTrackBackgroundColorB));
            }
        }

        public byte SliderTrackBackgroundColorR
        {
            get => SliderTrackBackgroundColor.R;
            set => SliderTrackBackgroundColor = Color.FromRgb(value, SliderTrackBackgroundColor.G, SliderTrackBackgroundColor.B);
        }

        public byte SliderTrackBackgroundColorG
        {
            get => SliderTrackBackgroundColor.G;
            set => SliderTrackBackgroundColor = Color.FromRgb(SliderTrackBackgroundColor.R, value, SliderTrackBackgroundColor.B);
        }

        public byte SliderTrackBackgroundColorB
        {
            get => SliderTrackBackgroundColor.B;
            set => SliderTrackBackgroundColor = Color.FromRgb(SliderTrackBackgroundColor.R, SliderTrackBackgroundColor.G, value);
        }

        // Peak Meter Color - RGB components
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
                RaisePropertyChanged(nameof(PeakMeterColorR));
                RaisePropertyChanged(nameof(PeakMeterColorG));
                RaisePropertyChanged(nameof(PeakMeterColorB));
            }
        }

        public byte PeakMeterColorR
        {
            get => PeakMeterColor.R;
            set => PeakMeterColor = Color.FromRgb(value, PeakMeterColor.G, PeakMeterColor.B);
        }

        public byte PeakMeterColorG
        {
            get => PeakMeterColor.G;
            set => PeakMeterColor = Color.FromRgb(PeakMeterColor.R, value, PeakMeterColor.B);
        }

        public byte PeakMeterColorB
        {
            get => PeakMeterColor.B;
            set => PeakMeterColor = Color.FromRgb(PeakMeterColor.R, PeakMeterColor.G, value);
        }

        public EarTrumpetCustomizationSettingsPageViewModel(AppSettings settings) : base(null)
        {
            _settings = settings;
            Title = Properties.Resources.CustomizationSettingsPageText;
            Glyph = "\xE790"; // Paintbrush icon

            // Initialize predefined themes
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

            // Commands
            ResetToDefaultCommand = new RelayCommand(() => ResetToDefault());
        }

        private void ApplyTheme(ColorTheme theme)
        {
            if (theme == null) return;
            
            SliderThumbColor = theme.ThumbColor;
            SliderTrackFillColor = theme.TrackFillColor;
            SliderTrackBackgroundColor = theme.TrackBackgroundColor;
            PeakMeterColor = theme.PeakMeterColor;
            
            // Auto-enable custom colors when a theme is selected
            if (!UseCustomSliderColors)
            {
                UseCustomSliderColors = true;
            }
        }

        private void ResetToDefault()
        {
            // Reset to first theme (Default)
            if (AvailableThemes.Count > 0)
            {
                var defaultTheme = AvailableThemes[0];
                SliderThumbColor = defaultTheme.ThumbColor;
                SliderTrackFillColor = defaultTheme.TrackFillColor;
                SliderTrackBackgroundColor = defaultTheme.TrackBackgroundColor;
                PeakMeterColor = defaultTheme.PeakMeterColor;
            }
            
            // Disable custom colors to use system accent
            UseCustomSliderColors = false;
            _selectedTheme = null;
            RaisePropertyChanged(nameof(SelectedTheme));
        }

        // Called when the page is navigated to
        public override void NavigatedTo()
        {
            base.NavigatedTo();
            // Open the mixer window so user can see color changes in real-time
            try
            {
                // Use reflection to call OpenMixerWindow on the App instance
                var appType = System.Windows.Application.Current.GetType();
                var method = appType.GetMethod("OpenMixerWindow");
                method?.Invoke(System.Windows.Application.Current, null);
            }
            catch { /* Ignore errors if method not found */ }
        }
    }
}
