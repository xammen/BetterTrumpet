namespace EarTrumpet.UI.ViewModels
{
    public class EarTrumpetMediaPopupSettingsPageViewModel : SettingsPageViewModel
    {
        private readonly AppSettings _settings;

        // Enable/disable media popup
        public bool MediaPopupEnabled
        {
            get => _settings.MediaPopupEnabled;
            set
            {
                _settings.MediaPopupEnabled = value;
                RaisePropertyChanged(nameof(MediaPopupEnabled));
            }
        }

        // Hover delay in seconds (displayed as slider 0.5 - 5)
        public double HoverDelay
        {
            get => _settings.MediaPopupHoverDelay;
            set
            {
                _settings.MediaPopupHoverDelay = value;
                RaisePropertyChanged(nameof(HoverDelay));
                RaisePropertyChanged(nameof(HoverDelayText));
            }
        }

        public string HoverDelayText => $"{HoverDelay:F2}s";

        // Blur radius (0 - 30)
        public double BlurRadius
        {
            get => _settings.MediaPopupBlurRadius;
            set
            {
                _settings.MediaPopupBlurRadius = value;
                RaisePropertyChanged(nameof(BlurRadius));
                RaisePropertyChanged(nameof(BlurRadiusText));
            }
        }

        public string BlurRadiusText => $"{BlurRadius:F0}px";

        // Only show when media is playing
        public bool ShowOnlyWhenPlaying
        {
            get => _settings.MediaPopupShowOnlyWhenPlaying;
            set
            {
                _settings.MediaPopupShowOnlyWhenPlaying = value;
                RaisePropertyChanged(nameof(ShowOnlyWhenPlaying));
            }
        }

        // Remember expanded state
        public bool RememberExpanded
        {
            get => _settings.MediaPopupRememberExpanded;
            set
            {
                _settings.MediaPopupRememberExpanded = value;
                RaisePropertyChanged(nameof(RememberExpanded));
            }
        }

        public EarTrumpetMediaPopupSettingsPageViewModel(AppSettings settings) : base(null)
        {
            _settings = settings;
            Title = "Media Popup";
            Glyph = "\xE8D6"; // Music icon
        }
    }
}
