

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
            set => _settings.UseVolumeTickSound = value;
        }

        public bool NotifyOnDefaultDeviceChange
        {
            get => _settings.NotifyOnDefaultDeviceChange;
            set => _settings.NotifyOnDefaultDeviceChange = value;
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
