

namespace EarTrumpet.UI.ViewModels
{
    public class EarTrumpetLegacySettingsPageViewModel : SettingsPageViewModel
    {
        public bool UseLegacyIcon
        {
            get => _settings.UseLegacyIcon;
            set => _settings.UseLegacyIcon = value;
        }

        public bool RunAtStartup
        {
            get => _settings.RunAtStartup;
            set => _settings.RunAtStartup = value;
        }

        private readonly AppSettings _settings;

        public EarTrumpetLegacySettingsPageViewModel(AppSettings settings) : base(null)
        {
            _settings = settings;
            Title = Properties.Resources.LegacySettingsPageText;
            Subtitle = "Startup behavior and legacy icon options.";
            Glyph = "\xE825";
        }
    }
}