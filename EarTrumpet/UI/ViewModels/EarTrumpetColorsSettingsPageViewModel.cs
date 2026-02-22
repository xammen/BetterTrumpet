using EarTrumpet.DataModel;
using EarTrumpet.UI.Helpers;
using EarTrumpet.UI.Themes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace EarTrumpet.UI.ViewModels
{
    public class EarTrumpetColorsSettingsPageViewModel : SettingsPageViewModel
    {
        private readonly AppSettings _settings;

        // Store original Ref values for restoration when disabling custom theme.
        // Instance-scoped: backup is taken fresh each time settings are opened.
        private readonly Dictionary<string, object> _originalRefs = new Dictionary<string, object>();
        private bool _refsBackedUp = false;

        // Default colors from shared registry
        private static readonly Color DefaultAccentColor = ThemeRegistry.DefaultAccentColor;
        private static readonly Color DefaultTrackBackground = ThemeRegistry.DefaultTrackBackground;
        private static readonly Color DefaultPeakMeter = ThemeRegistry.DefaultPeakMeter;

        // All themes: built-in + custom, grouped by category
        public ObservableCollection<ColorTheme> AvailableThemes { get; }
        public IEnumerable<IGrouping<string, ColorTheme>> GroupedThemes => ThemeRegistry.GetGroupedThemes();
        public ObservableCollection<ColorTheme> CustomThemes { get; }

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
                    _settings.ActiveThemeName = value.Name;
                    RaisePropertyChanged(nameof(SelectedTheme));
                }
            }
        }

        // Commands
        public ICommand ResetToDefaultCommand { get; }
        public ICommand SaveCustomThemeCommand { get; }
        public ICommand DeleteCustomThemeCommand { get; }
        public ICommand ExportThemeCommand { get; }
        public ICommand ImportThemeCommand { get; }
        public ICommand ExportThemeToFileCommand { get; }
        public ICommand ImportThemeFromFileCommand { get; }

        // Enable custom colors
        public bool UseCustomSliderColors
        {
            get => _settings.UseCustomSliderColors;
            set
            {
                _settings.UseCustomSliderColors = value;
                RaisePropertyChanged(nameof(UseCustomSliderColors));

                if (value)
                {
                    ApplyExtendedThemeColors();
                }
                else
                {
                    RestoreOriginalThemeRefs();
                }
            }
        }

        // Dynamic album art theme
        public bool UseDynamicAlbumArtTheme
        {
            get => _settings.UseDynamicAlbumArtTheme;
            set
            {
                _settings.UseDynamicAlbumArtTheme = value;
                RaisePropertyChanged(nameof(UseDynamicAlbumArtTheme));
                if (value)
                {
                    StartAlbumArtThemeMonitoring();
                }
                else
                {
                    StopAlbumArtThemeMonitoring();
                }
            }
        }

        // Custom theme name input
        private string _newThemeName = "";
        public string NewThemeName
        {
            get => _newThemeName;
            set { _newThemeName = value; RaisePropertyChanged(nameof(NewThemeName)); }
        }

        // Clipboard export text
        private string _exportedJson = "";
        public string ExportedJson
        {
            get => _exportedJson;
            set { _exportedJson = value; RaisePropertyChanged(nameof(ExportedJson)); }
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

        // Window Background Color
        private static readonly Color DefaultWindowBackground = Color.FromRgb(0x15, 0x15, 0x15);
        public Color WindowBackgroundColor
        {
            get
            {
                var color = _settings.WindowBackgroundColor;
                return color == Colors.Transparent ? DefaultWindowBackground : color;
            }
            set
            {
                _settings.WindowBackgroundColor = value;
                RaisePropertyChanged(nameof(WindowBackgroundColor));
                RaisePropertyChanged(nameof(WindowBackgroundColorHex));
                ApplyExtendedThemeColors();
            }
        }

        public string WindowBackgroundColorHex
        {
            get => $"#{WindowBackgroundColor.R:X2}{WindowBackgroundColor.G:X2}{WindowBackgroundColor.B:X2}";
            set
            {
                if (TryParseHexColor(value, out Color color))
                {
                    WindowBackgroundColor = color;
                }
            }
        }

        // Text Color
        private static readonly Color DefaultTextColor = Color.FromRgb(0xFF, 0xFF, 0xFF);
        public Color TextColorValue
        {
            get
            {
                var color = _settings.TextColor;
                return color == Colors.Transparent ? DefaultTextColor : color;
            }
            set
            {
                _settings.TextColor = value;
                RaisePropertyChanged(nameof(TextColorValue));
                RaisePropertyChanged(nameof(TextColorHex));
                ApplyExtendedThemeColors();
            }
        }

        public string TextColorHex
        {
            get => $"#{TextColorValue.R:X2}{TextColorValue.G:X2}{TextColorValue.B:X2}";
            set
            {
                if (TryParseHexColor(value, out Color color))
                {
                    TextColorValue = color;
                }
            }
        }

        // Accent Glow Color
        public Color AccentGlowColor
        {
            get
            {
                var color = _settings.AccentGlowColor;
                return color == Colors.Transparent ? SliderTrackFillColor : color;
            }
            set
            {
                _settings.AccentGlowColor = value;
                RaisePropertyChanged(nameof(AccentGlowColor));
                RaisePropertyChanged(nameof(AccentGlowColorHex));
                ApplyExtendedThemeColors();
            }
        }

        public string AccentGlowColorHex
        {
            get => $"#{AccentGlowColor.R:X2}{AccentGlowColor.G:X2}{AccentGlowColor.B:X2}";
            set
            {
                if (TryParseHexColor(value, out Color color))
                {
                    AccentGlowColor = color;
                }
            }
        }

        // ═══════════════════════════════════
        // Album art theme monitoring
        // ═══════════════════════════════════
        private DispatcherTimer _albumArtTimer;

        private void StartAlbumArtThemeMonitoring()
        {
            if (_albumArtTimer != null) return;

            _albumArtTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _albumArtTimer.Tick += OnAlbumArtTimerTick;
            _albumArtTimer.Start();

            // Also do an immediate check
            OnAlbumArtTimerTick(null, EventArgs.Empty);
        }

        private void StopAlbumArtThemeMonitoring()
        {
            _albumArtTimer?.Stop();
            _albumArtTimer = null;
        }

        private void OnAlbumArtTimerTick(object sender, EventArgs e)
        {
            if (!_settings.UseDynamicAlbumArtTheme || !_settings.UseCustomSliderColors) return;

            try
            {
                var thumbnail = MediaSessionService.Instance.GetCurrentThumbnail();
                if (thumbnail == null) return;

                var dominant = GetDominantColorFromBitmap(thumbnail);
                if (dominant == Colors.Transparent) return;

                // Create complementary colors from dominant
                var hsv = ColorToHsv(dominant);
                var lighter = HsvToColor(hsv.H, Math.Max(0.3, hsv.S * 0.6), Math.Min(1.0, hsv.V * 1.4));
                var darker = HsvToColor(hsv.H, Math.Min(1.0, hsv.S * 1.2), hsv.V * 0.4);
                var muted = HsvToColor(hsv.H, hsv.S * 0.5, hsv.V * 0.6);

                // Apply as slider colors
                SliderThumbColor = dominant;
                SliderTrackFillColor = lighter;
                SliderTrackBackgroundColor = darker;
                PeakMeterColor = muted;

                // Also apply extended colors from album art
                var veryDark = HsvToColor(hsv.H, Math.Min(1.0, hsv.S * 0.8), hsv.V * 0.15);
                var textLight = HsvToColor(hsv.H, Math.Max(0.05, hsv.S * 0.15), Math.Min(1.0, hsv.V * 0.3 + 0.75));
                _settings.WindowBackgroundColor = veryDark;
                _settings.TextColor = textLight;
                _settings.AccentGlowColor = lighter;
                RaisePropertyChanged(nameof(WindowBackgroundColor));
                RaisePropertyChanged(nameof(TextColorValue));
                RaisePropertyChanged(nameof(AccentGlowColor));
                ApplyExtendedThemeColors();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"EarTrumpetColorsSettingsPageViewModel: Album art theme tick failed - {ex.Message}");
            }
        }

        private Color GetDominantColorFromBitmap(System.Windows.Media.Imaging.BitmapImage bitmap)
        {
            try
            {
                int stride = bitmap.PixelWidth * 4;
                byte[] pixels = new byte[stride * bitmap.PixelHeight];
                bitmap.CopyPixels(pixels, stride, 0);

                long totalR = 0, totalG = 0, totalB = 0;
                int count = 0;
                int step = Math.Max(1, pixels.Length / (4 * 200)); // Sample ~200 pixels

                for (int i = 0; i < pixels.Length - 3; i += step * 4)
                {
                    byte b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
                    // Skip very dark and very light pixels
                    if (r + g + b > 60 && r + g + b < 700)
                    {
                        totalR += r; totalG += g; totalB += b;
                        count++;
                    }
                }

                if (count == 0) return Colors.Transparent;
                return Color.FromRgb((byte)(totalR / count), (byte)(totalG / count), (byte)(totalB / count));
            }
            catch { return Colors.Transparent; }
        }

        private struct HsvColor
        {
            public double H, S, V;
            public HsvColor(double h, double s, double v) { H = h; S = s; V = v; }
        }

        private HsvColor ColorToHsv(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            double h = 0, s = 0, v = max;
            double d = max - min;
            s = max == 0 ? 0 : d / max;
            if (max != min)
            {
                if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
                else if (max == g) h = (b - r) / d + 2;
                else h = (r - g) / d + 4;
                h /= 6;
            }
            return new HsvColor(h, s, v);
        }

        private Color HsvToColor(double h, double s, double v)
        {
            int hi = (int)(h * 6) % 6;
            double f = h * 6 - Math.Floor(h * 6);
            double p = v * (1 - s), q = v * (1 - f * s), t = v * (1 - (1 - f) * s);
            double r, g, b;
            switch (hi)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }
            return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        // ═══════════════════════════════════
        // Constructor
        // ═══════════════════════════════════

        public EarTrumpetColorsSettingsPageViewModel(AppSettings settings) : base(null)
        {
            _settings = settings;
            Title = "Appearance";
            Glyph = "\xE790"; // Paintbrush icon

            AvailableThemes = new ObservableCollection<ColorTheme>(ThemeRegistry.AllThemes);
            CustomThemes = new ObservableCollection<ColorTheme>();
            LoadCustomThemes();

            // Restore active theme selection
            var activeThemeName = _settings.ActiveThemeName;
            if (!string.IsNullOrEmpty(activeThemeName))
            {
                _selectedTheme = AvailableThemes.FirstOrDefault(t => t.Name == activeThemeName)
                              ?? CustomThemes.FirstOrDefault(t => t.Name == activeThemeName);
            }

            // Commands
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
            SaveCustomThemeCommand = new RelayCommand(SaveCustomTheme);
            DeleteCustomThemeCommand = new RelayCommand<ColorTheme>(DeleteCustomTheme);
            ExportThemeCommand = new RelayCommand(ExportCurrentTheme);
            ImportThemeCommand = new RelayCommand(ImportThemeFromClipboard);
            ExportThemeToFileCommand = new RelayCommand(ExportThemeToFile);
            ImportThemeFromFileCommand = new RelayCommand(ImportThemeFromFile);

            // Start album art monitoring if enabled
            if (_settings.UseDynamicAlbumArtTheme)
            {
                StartAlbumArtThemeMonitoring();
            }

            // Re-apply extended theme colors if custom colors are enabled
            if (_settings.UseCustomSliderColors)
            {
                ApplyExtendedThemeColors();
            }
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
                case "WindowBackground":
                    WindowBackgroundColor = color;
                    break;
                case "TextColor":
                    TextColorValue = color;
                    break;
                case "AccentGlow":
                    AccentGlowColor = color;
                    break;
            }
            
            if (!UseCustomSliderColors)
            {
                UseCustomSliderColors = true;
            }
        }

        // ═══════════════════════════════════
        // Animated theme transition (vsync-locked via CompositionTarget.Rendering)
        // ═══════════════════════════════════
        private bool _isTransitioning;
        private Color _fromThumb, _fromFill, _fromTrackBg, _fromPeak, _fromWindowBg, _fromText, _fromGlow;
        private Color _toThumb, _toFill, _toTrackBg, _toPeak, _toWindowBg, _toText, _toGlow;
        private long _transitionStartTick;
        private int _transitionFrameCount;
        private const long TransitionDurationTicks = 300 * TimeSpan.TicksPerMillisecond;
        private const int ExtendedRefreshInterval = 1; // update flyout Refs every frame (60fps)

        private void ApplyTheme(ColorTheme theme)
        {
            if (theme == null) return;

            if (!UseCustomSliderColors)
            {
                UseCustomSliderColors = true;
            }

            // Snapshot current colors
            _fromThumb = SliderThumbColor;
            _fromFill = SliderTrackFillColor;
            _fromTrackBg = SliderTrackBackgroundColor;
            _fromPeak = PeakMeterColor;
            _fromWindowBg = WindowBackgroundColor;
            _fromText = TextColorValue;
            _fromGlow = AccentGlowColor;

            _toThumb = theme.ThumbColor;
            _toFill = theme.TrackFillColor;
            _toTrackBg = theme.TrackBackgroundColor;
            _toPeak = theme.PeakMeterColor;
            _toWindowBg = theme.WindowBackgroundColor;
            _toText = theme.TextColor;
            _toGlow = theme.AccentGlowColor;

            _transitionFrameCount = 0;

            if (!_isTransitioning)
            {
                _isTransitioning = true;
                System.Windows.Media.CompositionTarget.Rendering += OnTransitionFrame;
            }
            _transitionStartTick = DateTime.UtcNow.Ticks;
        }

        private void OnTransitionFrame(object sender, EventArgs e)
        {
            var elapsed = DateTime.UtcNow.Ticks - _transitionStartTick;
            var rawT = Math.Min(1.0, (double)elapsed / TransitionDurationTicks);
            var done = rawT >= 1.0;
            _transitionFrameCount++;

            // Ease-out quart
            var t1 = 1.0 - rawT;
            var t = 1.0 - (t1 * t1 * t1 * t1);

            // Lerp all 7 channels
            var thumb = LerpColor(_fromThumb, _toThumb, t);
            var fill = LerpColor(_fromFill, _toFill, t);
            var trackBg = LerpColor(_fromTrackBg, _toTrackBg, t);
            var peak = LerpColor(_fromPeak, _toPeak, t);
            var windowBg = LerpColor(_fromWindowBg, _toWindowBg, t);
            var text = LerpColor(_fromText, _toText, t);
            var glow = LerpColor(_fromGlow, _toGlow, t);

            // Batch write — single event fire for slider updates
            _settings.BeginBatch();
            _settings.SliderThumbColor = thumb;
            _settings.SliderTrackFillColor = fill;
            _settings.SliderTrackBackgroundColor = trackBg;
            _settings.PeakMeterColor = peak;
            _settings.WindowBackgroundColor = windowBg;
            _settings.TextColor = text;
            _settings.AccentGlowColor = glow;
            _settings.EndBatch();

            // Notify UI (preview swatches)
            RaisePropertyChanged(nameof(SliderThumbColor));
            RaisePropertyChanged(nameof(SliderTrackFillColor));
            RaisePropertyChanged(nameof(SliderTrackBackgroundColor));
            RaisePropertyChanged(nameof(PeakMeterColor));
            RaisePropertyChanged(nameof(WindowBackgroundColor));
            RaisePropertyChanged(nameof(TextColorValue));
            RaisePropertyChanged(nameof(AccentGlowColor));

            // Update flyout/mixer window Refs periodically during transition
            // (not every frame — that's too heavy — but often enough to look smooth)
            if (done || _transitionFrameCount % ExtendedRefreshInterval == 0)
            {
                ApplyExtendedThemeColors();
            }

            if (done)
            {
                System.Windows.Media.CompositionTarget.Rendering -= OnTransitionFrame;
                _isTransitioning = false;
            }
        }

        private static Color LerpColor(Color from, Color to, double t)
        {
            return Color.FromRgb(
                (byte)(from.R + (to.R - from.R) * t),
                (byte)(from.G + (to.G - from.G) * t),
                (byte)(from.B + (to.B - from.B) * t));
        }

        private void ResetToDefault()
        {
            UseCustomSliderColors = false;
            UseDynamicAlbumArtTheme = false;
            _selectedTheme = null;
            _settings.ActiveThemeName = "";

            // Clear extended color settings
            _settings.WindowBackgroundColor = Colors.Transparent;
            _settings.TextColor = Colors.Transparent;
            _settings.AccentGlowColor = Colors.Transparent;
            RaisePropertyChanged(nameof(WindowBackgroundColor));
            RaisePropertyChanged(nameof(WindowBackgroundColorHex));
            RaisePropertyChanged(nameof(TextColorValue));
            RaisePropertyChanged(nameof(TextColorHex));
            RaisePropertyChanged(nameof(AccentGlowColor));
            RaisePropertyChanged(nameof(AccentGlowColorHex));

            // Restore original theme Refs
            RestoreOriginalThemeRefs();

            RaisePropertyChanged(nameof(SelectedTheme));
        }

        /// <summary>
        /// Apply WindowBackground and TextColor to the theme system by overriding Refs.
        /// This changes the actual flyout/mixer background and text color globally.
        /// </summary>
        private void ApplyExtendedThemeColors()
        {
            if (!_settings.UseCustomSliderColors) return;

            try
            {
                BackupOriginalThemeRefs();

                var windowBg = _settings.WindowBackgroundColor;
                var textColor = _settings.TextColor;
                var accentGlow = _settings.AccentGlowColor;

                bool hasWindowBg = windowBg != Colors.Transparent;
                bool hasTextColor = textColor != Colors.Transparent;

                if (!hasWindowBg && !hasTextColor)
                {
                    // No extended colors set, restore originals
                    RestoreOriginalThemeRefs();
                    return;
                }

                var refs = Manager.Current.References;

                if (hasTextColor)
                {
                    var textRef = refs.FirstOrDefault(r => r.Key == "Text");
                    if (textRef != null)
                    {
                        // Override with direct hex color
                        textRef.Value = $"#{textColor.R:X2}{textColor.G:X2}{textColor.B:X2}";
                        textRef.Rules.Clear();
                    }

                    var grayTextRef = refs.FirstOrDefault(r => r.Key == "GrayText");
                    if (grayTextRef != null)
                    {
                        // Make gray text a semi-transparent version of text color
                        var grayVersion = Color.FromArgb(0xAA, textColor.R, textColor.G, textColor.B);
                        grayTextRef.Value = $"#{grayVersion.A:X2}{grayVersion.R:X2}{grayVersion.G:X2}{grayVersion.B:X2}";
                        grayTextRef.Rules.Clear();
                    }
                }

                if (hasWindowBg)
                {
                    var bgHex = $"#{windowBg.R:X2}{windowBg.G:X2}{windowBg.B:X2}";

                    // Override FlyoutBackground
                    var flyoutBgRef = refs.FirstOrDefault(r => r.Key == "FlyoutBackground");
                    if (flyoutBgRef != null)
                    {
                        flyoutBgRef.Value = bgHex;
                        flyoutBgRef.Rules.Clear();
                    }

                    // Override Background
                    var bgRef = refs.FirstOrDefault(r => r.Key == "Background");
                    if (bgRef != null)
                    {
                        bgRef.Value = bgHex;
                        bgRef.Rules.Clear();
                    }

                    // Override PopupBackground
                    var popupBgRef = refs.FirstOrDefault(r => r.Key == "PopupBackground");
                    if (popupBgRef != null)
                    {
                        popupBgRef.Value = bgHex;
                        popupBgRef.Rules.Clear();
                    }

                    // Override AcrylicColor_Flyout (tint for acrylic blur)
                    var acrylicFlyoutRef = refs.FirstOrDefault(r => r.Key == "AcrylicColor_Flyout");
                    if (acrylicFlyoutRef != null)
                    {
                        acrylicFlyoutRef.Value = $"{bgHex}/0.85";
                        acrylicFlyoutRef.Rules.Clear();
                    }

                    // Override AcrylicBackgroundFallback
                    var acrylicFallbackRef = refs.FirstOrDefault(r => r.Key == "AcrylicBackgroundFallback");
                    if (acrylicFallbackRef != null)
                    {
                        acrylicFallbackRef.Value = bgHex;
                        acrylicFallbackRef.Rules.Clear();
                    }
                }

                // Fire theme change to repaint all UI elements
                Manager.Current.NotifyThemeChanged();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"EarTrumpetColorsSettingsPageViewModel: ApplyExtendedThemeColors failed - {ex.Message}");
            }
        }

        /// <summary>
        /// Back up original Ref values/rules so we can restore them when the user resets.
        /// </summary>
        private void BackupOriginalThemeRefs()
        {
            if (_refsBackedUp) return;

            var keysToBackup = new[] { "Text", "GrayText", "Background", "FlyoutBackground", "PopupBackground", "AcrylicColor_Flyout", "AcrylicBackgroundFallback" };
            foreach (var key in keysToBackup)
            {
                var r = Manager.Current.References.FirstOrDefault(x => x.Key == key);
                if (r != null)
                {
                    // Store both Value and a deep copy of Rules
                    _originalRefs[key] = new RefBackup(r.Value, r.Rules.Select(CloneRule).ToList());
                }
            }
            _refsBackedUp = true;
        }

        /// <summary>
        /// Restore original Ref values/rules.
        /// </summary>
        private void RestoreOriginalThemeRefs()
        {
            if (!_refsBackedUp) return;

            try
            {
                foreach (var kvp in _originalRefs)
                {
                    var r = Manager.Current.References.FirstOrDefault(x => x.Key == kvp.Key);
                    if (r != null && kvp.Value is RefBackup backup)
                    {
                        r.Value = backup.Value;
                        r.Rules.Clear();
                        foreach (var rule in backup.Rules)
                        {
                            r.Rules.Add(rule);
                        }
                    }
                }

                Manager.Current.NotifyThemeChanged();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"EarTrumpetColorsSettingsPageViewModel: RestoreOriginalThemeRefs failed - {ex.Message}");
            }
        }

        private class RefBackup
        {
            public string Value { get; }
            public List<Rule> Rules { get; }
            public RefBackup(string value, List<Rule> rules) { Value = value; Rules = rules; }
        }

        private Rule CloneRule(Rule original)
        {
            var clone = new Rule { On = original.On, Value = original.Value };
            foreach (var child in original.Rules)
            {
                clone.Rules.Add(CloneRule(child));
            }
            return clone;
        }

        // ═══════════════════════════════════
        // Custom theme save/load/delete
        // ═══════════════════════════════════

        private void SaveCustomTheme()
        {
            var name = string.IsNullOrWhiteSpace(NewThemeName) ? $"My Theme {CustomThemes.Count + 1}" : NewThemeName.Trim();

            var theme = new ColorTheme(name, "Custom",
                SliderThumbColor, SliderTrackFillColor, SliderTrackBackgroundColor, PeakMeterColor,
                WindowBackgroundColor, TextColorValue, AccentGlowColor)
            { IsCustom = true };

            CustomThemes.Add(theme);
            PersistCustomThemes();
            SelectedTheme = theme;
            NewThemeName = "";
        }

        private void DeleteCustomTheme(ColorTheme theme)
        {
            if (theme == null || !theme.IsCustom) return;
            CustomThemes.Remove(theme);
            PersistCustomThemes();
            if (_selectedTheme == theme)
            {
                _selectedTheme = null;
                _settings.ActiveThemeName = "";
                RaisePropertyChanged(nameof(SelectedTheme));
            }
        }

        private void PersistCustomThemes()
        {
            try
            {
                var jsonArray = CustomThemes.Select(t => t.ToJson()).ToArray();
                _settings.CustomThemesJson = "[" + string.Join(",", jsonArray) + "]";
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"EarTrumpetColorsSettingsPageViewModel: PersistCustomThemes failed - {ex.Message}");
            }
        }

        private void LoadCustomThemes()
        {
            try
            {
                var json = _settings.CustomThemesJson;
                if (string.IsNullOrWhiteSpace(json) || json == "[]") return;

                var array = Newtonsoft.Json.JsonConvert.DeserializeObject<List<dynamic>>(json);
                if (array == null) return;

                foreach (var item in array)
                {
                    try
                    {
                        var theme = ColorTheme.FromJson(Newtonsoft.Json.JsonConvert.SerializeObject(item));
                        CustomThemes.Add(theme);
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"EarTrumpetColorsSettingsPageViewModel: LoadCustomTheme item failed - {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"EarTrumpetColorsSettingsPageViewModel: LoadCustomThemes failed - {ex.Message}");
            }
        }

        // ═══════════════════════════════════
        // Import/Export
        // ═══════════════════════════════════

        private void ExportCurrentTheme()
        {
            var theme = _selectedTheme;
            if (theme == null)
            {
                // Create a theme from current colors (including extended)
                theme = new ColorTheme("Exported Theme", "Custom",
                    SliderThumbColor, SliderTrackFillColor, SliderTrackBackgroundColor, PeakMeterColor,
                    WindowBackgroundColor, TextColorValue, AccentGlowColor);
            }

            var json = theme.ToJson();
            ExportedJson = json;

            try
            {
                Clipboard.SetText(json);
                Trace.WriteLine("EarTrumpetColorsSettingsPageViewModel: Theme copied to clipboard");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"EarTrumpetColorsSettingsPageViewModel: Clipboard copy failed - {ex.Message}");
            }
        }

        private void ImportThemeFromClipboard()
        {
            try
            {
                var json = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(json)) return;

                var theme = ColorTheme.FromJson(json);
                if (theme != null)
                {
                    // Ensure unique name
                    if (CustomThemes.Any(t => t.Name == theme.Name) || AvailableThemes.Any(t => t.Name == theme.Name))
                    {
                        theme.Name = theme.Name + " (Imported)";
                    }

                    CustomThemes.Add(theme);
                    PersistCustomThemes();
                    SelectedTheme = theme;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"EarTrumpetColorsSettingsPageViewModel: ImportTheme failed - {ex.Message}");
            }
        }

        // ═══════════════════════════════════
        // File-based Import/Export
        // ═══════════════════════════════════

        private void ExportThemeToFile()
        {
            try
            {
                var theme = _selectedTheme;
                if (theme == null)
                {
                    theme = new ColorTheme("My Theme", "Custom",
                        SliderThumbColor, SliderTrackFillColor, SliderTrackBackgroundColor, PeakMeterColor,
                        WindowBackgroundColor, TextColorValue, AccentGlowColor);
                }

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Theme",
                    Filter = "BetterTrumpet Theme (*.bttheme)|*.bttheme|JSON Files (*.json)|*.json",
                    DefaultExt = ".bttheme",
                    FileName = SanitizeFileName(theme.Name)
                };

                if (dlg.ShowDialog() == true)
                {
                    System.IO.File.WriteAllText(dlg.FileName, theme.ToJson(), System.Text.Encoding.UTF8);
                    Trace.WriteLine($"EarTrumpetColorsSettingsPageViewModel: Theme exported to {dlg.FileName}");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"EarTrumpetColorsSettingsPageViewModel: ExportThemeToFile failed - {ex.Message}");
            }
        }

        private void ImportThemeFromFile()
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import Theme",
                    Filter = "BetterTrumpet Theme (*.bttheme)|*.bttheme|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = ".bttheme"
                };

                if (dlg.ShowDialog() == true)
                {
                    var json = System.IO.File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8);
                    if (string.IsNullOrWhiteSpace(json)) return;

                    var theme = ColorTheme.FromJson(json);
                    if (theme != null)
                    {
                        // Ensure unique name
                        if (CustomThemes.Any(t => t.Name == theme.Name) || AvailableThemes.Any(t => t.Name == theme.Name))
                        {
                            theme.Name = theme.Name + " (Imported)";
                        }

                        CustomThemes.Add(theme);
                        PersistCustomThemes();
                        SelectedTheme = theme;
                        Trace.WriteLine($"EarTrumpetColorsSettingsPageViewModel: Theme imported from {dlg.FileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"EarTrumpetColorsSettingsPageViewModel: ImportThemeFromFile failed - {ex.Message}");
            }
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "theme";
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        // ═══════════════════════════════════
        // Helpers
        // ═══════════════════════════════════

        private bool TryParseHexColor(string hex, out Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrWhiteSpace(hex)) return false;

            hex = hex.TrimStart('#');
            if (hex.Length != 6) return false;

            try
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                color = Color.FromRgb(r, g, b);
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"EarTrumpetColorsSettingsPageViewModel: TryParseHexColor failed - {ex.Message}");
                return false;
            }
        }

        public override void NavigatedTo()
        {
            base.NavigatedTo();

            // Re-apply extended colors on navigate (in case they were cleared)
            if (_settings.UseCustomSliderColors)
            {
                ApplyExtendedThemeColors();
            }

            // Restart album art monitoring if enabled
            if (_settings.UseDynamicAlbumArtTheme && _albumArtTimer == null)
            {
                StartAlbumArtThemeMonitoring();
            }
        }

        public override bool NavigatingFrom(NavigationCookie cookie)
        {
            // Stop album art polling when leaving this page
            StopAlbumArtThemeMonitoring();

            // Stop any active theme transition
            if (_isTransitioning)
            {
                System.Windows.Media.CompositionTarget.Rendering -= OnTransitionFrame;
                _isTransitioning = false;
            }

            return base.NavigatingFrom(cookie);
        }
    }
}
