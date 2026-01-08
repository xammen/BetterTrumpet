using EarTrumpet.DataModel.Storage;
using EarTrumpet.Interop.Helpers;
using System;
using System.Diagnostics;
using System.Linq;
using static EarTrumpet.Interop.User32;

namespace EarTrumpet
{
    public class AppSettings
    {
        public event EventHandler<bool> UseLegacyIconChanged;
        public event Action FlyoutHotkeyTyped;
        public event Action MixerHotkeyTyped;
        public event Action SettingsHotkeyTyped;
        public event Action AbsoluteVolumeUpHotkeyTyped;
        public event Action AbsoluteVolumeDownHotkeyTyped;
        public event Action CustomSliderColorsChanged;

        private ISettingsBag _settings = StorageFactory.GetSettings();

        public void RegisterHotkeys()
        {
            HotkeyManager.Current.Register(FlyoutHotkey);
            HotkeyManager.Current.Register(MixerHotkey);
            HotkeyManager.Current.Register(SettingsHotkey);
            HotkeyManager.Current.Register(AbsoluteVolumeUpHotkey);
            HotkeyManager.Current.Register(AbsoluteVolumeDownHotkey);

            HotkeyManager.Current.KeyPressed += (hotkey) =>
            {
                if (hotkey.Equals(FlyoutHotkey))
                {
                    Trace.WriteLine("AppSettings FlyoutHotkeyTyped");
                    FlyoutHotkeyTyped?.Invoke();
                }
                else if (hotkey.Equals(SettingsHotkey))
                {
                    Trace.WriteLine("AppSettings SettingsHotkeyTyped");
                    SettingsHotkeyTyped?.Invoke();
                }
                else if (hotkey.Equals(MixerHotkey))
                {
                    Trace.WriteLine("AppSettings MixerHotkeyTyped");
                    MixerHotkeyTyped?.Invoke();
                }
                else if (hotkey.Equals(AbsoluteVolumeUpHotkey))
                {
                    Trace.WriteLine("AppSettings AbsoluteVolumeUpHotkeyTyped");
                    AbsoluteVolumeUpHotkeyTyped?.Invoke();
                }
                else if (hotkey.Equals(AbsoluteVolumeDownHotkey))
                {
                    Trace.WriteLine("AppSettings AbsoluteVolumeDownHotkeyTyped");
                    AbsoluteVolumeDownHotkeyTyped?.Invoke();
                }
            };
        }

        public HotkeyData FlyoutHotkey
        {
            get => _settings.Get("Hotkey", new HotkeyData { });
            set
            {
                HotkeyManager.Current.Unregister(FlyoutHotkey);
                _settings.Set("Hotkey", value);
                HotkeyManager.Current.Register(FlyoutHotkey);
            }
        }

        public HotkeyData MixerHotkey
        {
            get => _settings.Get("MixerHotkey", new HotkeyData { });
            set
            {
                HotkeyManager.Current.Unregister(MixerHotkey);
                _settings.Set("MixerHotkey", value);
                HotkeyManager.Current.Register(MixerHotkey);
            }
        }

        public HotkeyData SettingsHotkey
        {
            get => _settings.Get("SettingsHotkey", new HotkeyData { });
            set
            {
                HotkeyManager.Current.Unregister(SettingsHotkey);
                _settings.Set("SettingsHotkey", value);
                HotkeyManager.Current.Register(SettingsHotkey);
            }
        }

        public HotkeyData AbsoluteVolumeUpHotkey
        {
            get => _settings.Get("AbsoluteVolumeUpHotkey", new HotkeyData { });
            set
            {
                HotkeyManager.Current.Unregister(AbsoluteVolumeUpHotkey);
                _settings.Set("AbsoluteVolumeUpHotkey", value);
                HotkeyManager.Current.Register(AbsoluteVolumeUpHotkey);
            }
        }

        public HotkeyData AbsoluteVolumeDownHotkey
        {
            get => _settings.Get("AbsoluteVolumeDownHotkey", new HotkeyData { });
            set
            {
                HotkeyManager.Current.Unregister(AbsoluteVolumeDownHotkey);
                _settings.Set("AbsoluteVolumeDownHotkey", value);
                HotkeyManager.Current.Register(AbsoluteVolumeDownHotkey);
            }
        }

        public bool UseLegacyIcon
        {
            get
            {
                // Note: Legacy compat, we used to write string bools.
                var ret = _settings.Get("UseLegacyIcon", "False");
                bool.TryParse(ret, out bool isUseLegacyIcon);
                return isUseLegacyIcon;
            }
            set
            {
                _settings.Set("UseLegacyIcon", value.ToString());
                UseLegacyIconChanged?.Invoke(null, UseLegacyIcon);
            }
        }

        public bool IsExpanded
        {
            get => _settings.Get("IsExpanded", false);
            set => _settings.Set("IsExpanded", value);
        }

        public bool UseScrollWheelInTray
        {
            get => _settings.Get("UseScrollWheelInTray", true);
            set => _settings.Set("UseScrollWheelInTray", value);
        }

        public bool UseGlobalMouseWheelHook
        {
            get => _settings.Get("UseGlobalMouseWheelHook", false);
            set => _settings.Set("UseGlobalMouseWheelHook", value);
        }

        public bool HasShownFirstRun
        {
            get => _settings.HasKey("hasShownFirstRun");
            set => _settings.Set("hasShownFirstRun", value);
        }

        public bool IsTelemetryEnabled
        {
            get
            {
                return _settings.Get("IsTelemetryEnabled", IsTelemetryEnabledByDefault());
            }
            set => _settings.Set("IsTelemetryEnabled", value);
        }

        public bool UseLogarithmicVolume
        {
            get => _settings.Get("UseLogarithmicVolume", false);
            set => _settings.Set("UseLogarithmicVolume", value);
        }

        public bool UseSmoothVolumeAnimation
        {
            get => _settings.Get("UseSmoothVolumeAnimation", true);
            set => _settings.Set("UseSmoothVolumeAnimation", value);
        }

        // Volume animation speed: 0.02 (very slow) to 0.5 (fast). Default 0.08
        public double VolumeAnimationSpeed
        {
            get => _settings.Get("VolumeAnimationSpeed", 0.08);
            set => _settings.Set("VolumeAnimationSpeed", value);
        }

        // Peak meter FPS: 20 (performance), 30 (balanced), or 60 (smooth). Default 30
        // Note: 30fps is a good balance between smoothness and CPU usage
        // Most users won't notice the difference from 60fps for peak meters
        public int PeakMeterFps
        {
            get => _settings.Get("PeakMeterFps", 30);
            set => _settings.Set("PeakMeterFps", value);
        }

        // Eco Mode: Reduces CPU usage by limiting animations and refresh rates
        public bool EcoMode
        {
            get => _settings.Get("EcoMode", false);
            set
            {
                _settings.Set("EcoMode", value);
                EcoModeChanged?.Invoke();
            }
        }

        // Auto Eco Mode: Automatically enable eco mode when on battery power
        public bool AutoEcoMode
        {
            get => _settings.Get("AutoEcoMode", true);
            set => _settings.Set("AutoEcoMode", value);
        }

        // Event fired when eco mode changes
        public event Action EcoModeChanged;

        // Helper to get effective eco mode (considering auto and battery)
        public bool IsEffectiveEcoMode
        {
            get
            {
                if (EcoMode) return true;
                if (AutoEcoMode && IsOnBatteryPower) return true;
                return false;
            }
        }

        // Check if running on battery power
        public bool IsOnBatteryPower
        {
            get
            {
                try
                {
                    var status = System.Windows.Forms.SystemInformation.PowerStatus;
                    return status.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Offline;
                }
                catch
                {
                    return false;
                }
            }
        }

        // Get effective FPS based on eco mode
        public int EffectivePeakMeterFps
        {
            get
            {
                if (IsEffectiveEcoMode) return 20;
                return PeakMeterFps;
            }
        }

        // Custom slider colors
        public bool UseCustomSliderColors
        {
            get => _settings.Get("UseCustomSliderColors", false);
            set
            {
                _settings.Set("UseCustomSliderColors", value);
                CustomSliderColorsChanged?.Invoke();
            }
        }

        public System.Windows.Media.Color SliderThumbColor
        {
            get
            {
                var colorStr = _settings.Get("SliderThumbColor", "");
                if (string.IsNullOrEmpty(colorStr))
                    return System.Windows.Media.Colors.Transparent; // Will use SystemAccent
                try { return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr); }
                catch { return System.Windows.Media.Colors.Transparent; }
            }
            set
            {
                _settings.Set("SliderThumbColor", value.ToString());
                CustomSliderColorsChanged?.Invoke();
            }
        }

        public System.Windows.Media.Color SliderTrackFillColor
        {
            get
            {
                var colorStr = _settings.Get("SliderTrackFillColor", "");
                if (string.IsNullOrEmpty(colorStr))
                    return System.Windows.Media.Colors.Transparent; // Will use SystemAccent
                try { return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr); }
                catch { return System.Windows.Media.Colors.Transparent; }
            }
            set
            {
                _settings.Set("SliderTrackFillColor", value.ToString());
                CustomSliderColorsChanged?.Invoke();
            }
        }

        public System.Windows.Media.Color SliderTrackBackgroundColor
        {
            get
            {
                var colorStr = _settings.Get("SliderTrackBackgroundColor", "");
                if (string.IsNullOrEmpty(colorStr))
                    return System.Windows.Media.Colors.Transparent; // Will use default
                try { return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr); }
                catch { return System.Windows.Media.Colors.Transparent; }
            }
            set
            {
                _settings.Set("SliderTrackBackgroundColor", value.ToString());
                CustomSliderColorsChanged?.Invoke();
            }
        }

        public System.Windows.Media.Color PeakMeterColor
        {
            get
            {
                var colorStr = _settings.Get("PeakMeterColor", "");
                if (string.IsNullOrEmpty(colorStr))
                    return System.Windows.Media.Colors.Transparent; // Will use SystemAccent
                try { return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr); }
                catch { return System.Windows.Media.Colors.Transparent; }
            }
            set
            {
                _settings.Set("PeakMeterColor", value.ToString());
                CustomSliderColorsChanged?.Invoke();
            }
        }

        public WINDOWPLACEMENT? FullMixerWindowPlacement
        {
            get => _settings.Get("FullMixerWindowPlacement", default(WINDOWPLACEMENT?));
            set => _settings.Set("FullMixerWindowPlacement", value);
        }

        public WINDOWPLACEMENT? SettingsWindowPlacement
        {
            get => _settings.Get("SettingsWindowPlacement", default(WINDOWPLACEMENT?));
            set => _settings.Set("SettingsWindowPlacement", value);
        }

        // Media Popup Settings
        public event Action MediaPopupSettingsChanged;

        public bool MediaPopupEnabled
        {
            get => _settings.Get("MediaPopupEnabled", true);
            set
            {
                _settings.Set("MediaPopupEnabled", value);
                MediaPopupSettingsChanged?.Invoke();
            }
        }

        // Hover delay in seconds (0.5 to 5 seconds)
        public double MediaPopupHoverDelay
        {
            get => _settings.Get("MediaPopupHoverDelay", 2.0);
            set
            {
                _settings.Set("MediaPopupHoverDelay", Math.Max(0.5, Math.Min(5.0, value)));
                MediaPopupSettingsChanged?.Invoke();
            }
        }

        // Background blur radius (0 to 30)
        public double MediaPopupBlurRadius
        {
            get => _settings.Get("MediaPopupBlurRadius", 15.0);
            set
            {
                _settings.Set("MediaPopupBlurRadius", Math.Max(0, Math.Min(30, value)));
                MediaPopupSettingsChanged?.Invoke();
            }
        }

        // Only show popup when media is playing
        public bool MediaPopupShowOnlyWhenPlaying
        {
            get => _settings.Get("MediaPopupShowOnlyWhenPlaying", false);
            set
            {
                _settings.Set("MediaPopupShowOnlyWhenPlaying", value);
                MediaPopupSettingsChanged?.Invoke();
            }
        }

        // Remember expanded state between sessions
        public bool MediaPopupRememberExpanded
        {
            get => _settings.Get("MediaPopupRememberExpanded", true);
            set => _settings.Set("MediaPopupRememberExpanded", value);
        }

        // Expanded state (stored if RememberExpanded is true)
        public bool MediaPopupIsExpanded
        {
            get => _settings.Get("MediaPopupIsExpanded", false);
            set => _settings.Set("MediaPopupIsExpanded", value);
        }

        // Run at Windows startup
        private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupValueName = "BetterTrumpet";

        public bool RunAtStartup
        {
            get
            {
                try
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false))
                    {
                        return key?.GetValue(StartupValueName) != null;
                    }
                }
                catch
                {
                    return false;
                }
            }
            set
            {
                try
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true))
                    {
                        if (value)
                        {
                            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                            key?.SetValue(StartupValueName, $"\"{exePath}\"");
                        }
                        else
                        {
                            key?.DeleteValue(StartupValueName, false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Failed to set RunAtStartup: {ex.Message}");
                }
            }
        }

        private bool IsTelemetryEnabledByDefault()
        {
            // Discussion on what to include:
            // https://gist.github.com/henrik/1688572
            var europeanUnionRegions = new string[]
            {
                // EU 28
                "AT", // Austria
                "BE", // Belgium
                "BG", // Bulgaria
                "HR", // Croatia
                "CY", // Cyprus
                "CZ", // Czech Republic
                "DK", // Denmark
                "EE", // Estonia
                "FI", // Finland
                "FR", // France
                "DE", // Germany
                "GR", // Greece
                "HU", // Hungary
                "IE", // Ireland, Republic of (EIRE)
                "IT", // Italy
                "LV", // Latvia
                "LT", // Lithuania
                "LU", // Luxembourg
                "MT", // Malta
                "NL", // Netherlands
                "PL", // Poland
                "PT", // Portugal
                "RO", // Romania
                "SK", // Slovakia
                "SI", // Slovenia
                "ES", // Spain
                "SE", // Sweden
                "GB", // United Kingdom (Great Britain)

                // Outermost Regions (OMR)
                "GF", // French Guiana
                "GP", // Guadeloupe
                "MQ", // Martinique
                "ME", // Montenegro
                "YT", // Mayotte
                "RE", // Réunion
                "MF", // Saint Martin

                // Special Cases: Part of EU
                "GI", // Gibraltar
                "AX", // Åland Islands

                // Overseas Countries and Territories (OCT)
                "PM", // Saint Pierre and Miquelon
                "GL", // Greenland
                "BL", // Saint Bartelemey
                "SX", // Sint Maarten
                "AW", // Aruba
                "CW", // Curacao
                "WF", // Wallis and Futuna
                "PF", // French Polynesia
                "NC", // New Caledonia
                "TF", // French Southern Territories
                "AI", // Anguilla
                "BM", // Bermuda
                "IO", // British Indian Ocean Territory
                "VG", // Virgin Islands, British
                "KY", // Cayman Islands
                "FK", // Falkland Islands (Malvinas)
                "MS", // Montserrat
                "PN", // Pitcairn
                "SH", // Saint Helena
                "GS", // South Georgia and the South Sandwich Islands
                "TC", // Turks and Caicos Islands

                // Microstates
                "AD", // Andorra
                "LI", // Liechtenstein
                "MC", // Monaco
                "SM", // San Marino
                "VA", // Vatican City

                // Other
                "JE", // Jersey
                "GG", // Guernsey
            };
            var region = new Windows.Globalization.GeographicRegion();
            return !europeanUnionRegions.Contains(region.CodeTwoLetter);
        }
    }
}