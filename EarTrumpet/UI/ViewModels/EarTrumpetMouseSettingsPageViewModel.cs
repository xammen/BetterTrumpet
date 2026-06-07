

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
