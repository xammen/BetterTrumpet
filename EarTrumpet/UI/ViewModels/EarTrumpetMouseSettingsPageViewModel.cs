

namespace EarTrumpet.UI.ViewModels
{
    public class EarTrumpetMouseSettingsPageViewModel : SettingsPageViewModel
    {
        public bool UseScrollWheelInTray
        {
            get => _settings.UseScrollWheelInTray;
            set => _settings.UseScrollWheelInTray = value;
        }

        public bool UseGlobalMouseWheelHook
        {
            get => _settings.UseGlobalMouseWheelHook;
            set => _settings.UseGlobalMouseWheelHook = value;
        }

        // Logarithmic volume scaling (perceptual loudness). Merged here from the
        // former standalone "Community" page so all volume-adjustment behavior
        // lives in one place.
        public bool UseLogarithmicVolume
        {
            get => _settings.UseLogarithmicVolume;
            set => _settings.UseLogarithmicVolume = value;
        }

        // Volume tick sound effect
        public bool UseVolumeTickSound
        {
            get => _settings.UseVolumeTickSound;
            set
            {
                _settings.UseVolumeTickSound = value;
                RaisePropertyChanged(nameof(UseVolumeTickSound));
            }
        }

        // Same order as TickSoundKind.
        public string[] TickSoundOptions { get; } =
        {
            Properties.Resources.SettingsTickSoundClassic,
            Properties.Resources.SettingsTickSoundCrystal,
            Properties.Resources.SettingsTickSoundLow,
            Properties.Resources.SettingsTickSoundMuffled,
            Properties.Resources.SettingsTickSoundSharp,
        };

        public int TickSoundIndex
        {
            get => (int)_settings.TickSound;
            set
            {
                if (value < 0 || value == (int)_settings.TickSound) return;
                _settings.TickSound = (TickSoundKind)value;
                RaisePropertyChanged(nameof(TickSoundIndex));
                UI.Controls.TickSoundPlayer.Play(60, _settings.TickSound);
            }
        }

        public bool NotifyOnDefaultDeviceChange
        {
            get => _settings.NotifyOnDefaultDeviceChange;
            set => _settings.NotifyOnDefaultDeviceChange = value;
        }

        public bool NotifyOnDeviceHotkeyChange
        {
            get => _settings.NotifyOnDeviceHotkeyChange;
            set => _settings.NotifyOnDeviceHotkeyChange = value;
        }

        public bool UseFocusLostVolume
        {
            get => _settings.UseFocusLostVolume;
            set
            {
                _settings.UseFocusLostVolume = value;
                RaisePropertyChanged(nameof(UseFocusLostVolume));
            }
        }

        public int FocusLostAttenuatePercent
        {
            get => _settings.FocusLostAttenuatePercent;
            set
            {
                _settings.FocusLostAttenuatePercent = value;
                RaisePropertyChanged(nameof(FocusLostAttenuatePercent));
            }
        }

        public int FocusLostFadeDurationMs
        {
            get => _settings.FocusLostFadeDurationMs;
            set
            {
                _settings.FocusLostFadeDurationMs = value;
                RaisePropertyChanged(nameof(FocusLostFadeDurationMs));
            }
        }

        public bool FocusLostSelectedAppsOnly
        {
            get => _settings.FocusLostSelectedAppsOnly;
            set
            {
                _settings.FocusLostSelectedAppsOnly = value;
                RaisePropertyChanged(nameof(FocusLostSelectedAppsOnly));
            }
        }

        public bool FocusLostAllApps
        {
            get => !_settings.FocusLostSelectedAppsOnly;
            set
            {
                if (value)
                {
                    _settings.FocusLostSelectedAppsOnly = false;
                    RaisePropertyChanged(nameof(FocusLostSelectedAppsOnly));
                }

                RaisePropertyChanged(nameof(FocusLostAllApps));
            }
        }

        private readonly AppSettings _settings;

        public EarTrumpetMouseSettingsPageViewModel(AppSettings settings) : base(null)
        {
            _settings = settings;
            Title = Properties.Resources.VolumeMouseSettingsPageText;
            Subtitle = Properties.Resources.VolumeMouseSettingsPageSubtitle;
            Glyph = "\xE962";
        }
    }
}
