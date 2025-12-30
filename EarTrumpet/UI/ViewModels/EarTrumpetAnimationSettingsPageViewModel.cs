namespace EarTrumpet.UI.ViewModels
{
    public class EarTrumpetAnimationSettingsPageViewModel : SettingsPageViewModel
    {
        private readonly AppSettings _settings;

        // Eco Mode: Reduces CPU usage globally
        public bool EcoMode
        {
            get => _settings.EcoMode;
            set
            {
                _settings.EcoMode = value;
                RaisePropertyChanged(nameof(EcoMode));
                RaisePropertyChanged(nameof(EffectiveFpsText));
            }
        }

        // Auto Eco Mode: Enable eco mode on battery
        public bool AutoEcoMode
        {
            get => _settings.AutoEcoMode;
            set
            {
                _settings.AutoEcoMode = value;
                RaisePropertyChanged(nameof(AutoEcoMode));
                RaisePropertyChanged(nameof(EffectiveFpsText));
            }
        }

        // Show current battery status
        public bool IsOnBattery => _settings.IsOnBatteryPower;

        // Show effective FPS based on eco mode
        public string EffectiveFpsText
        {
            get
            {
                var fps = _settings.EffectivePeakMeterFps;
                if (_settings.IsEffectiveEcoMode)
                    return $"{fps} FPS (Eco Mode Active)";
                return $"{fps} FPS";
            }
        }

        // Performance mode: disables animations and sets peak meter to 20fps
        public bool PerformanceMode
        {
            get => !_settings.UseSmoothVolumeAnimation && PeakMeterFps == 20;
            set
            {
                if (value)
                {
                    UseSmoothVolumeAnimation = false;
                    PeakMeterFps = 20;
                }
                else
                {
                    UseSmoothVolumeAnimation = true;
                    PeakMeterFps = 60;
                }
                RaisePropertyChanged(nameof(PerformanceMode));
            }
        }

        // Smooth volume animation
        public bool UseSmoothVolumeAnimation
        {
            get => _settings.UseSmoothVolumeAnimation;
            set
            {
                _settings.UseSmoothVolumeAnimation = value;
                RaisePropertyChanged(nameof(UseSmoothVolumeAnimation));
                RaisePropertyChanged(nameof(PerformanceMode));
            }
        }

        // Animation speed: 1 (slow) to 10 (fast)
        public int VolumeAnimationSpeed
        {
            get
            {
                double internalValue = _settings.VolumeAnimationSpeed;
                int uiValue = (int)System.Math.Round((internalValue - 0.02) / 0.053 + 1);
                return System.Math.Max(1, System.Math.Min(10, uiValue));
            }
            set
            {
                double internalValue = 0.02 + (value - 1) * 0.053;
                _settings.VolumeAnimationSpeed = internalValue;
                RaisePropertyChanged(nameof(VolumeAnimationSpeed));
            }
        }

        // Peak meter FPS: 20, 30, 60
        public int PeakMeterFps
        {
            get => _settings.PeakMeterFps;
            set
            {
                _settings.PeakMeterFps = value;
                RaisePropertyChanged(nameof(PeakMeterFps));
                RaisePropertyChanged(nameof(PerformanceMode));
                RaisePropertyChanged(nameof(EffectiveFpsText));
            }
        }

        public EarTrumpetAnimationSettingsPageViewModel(AppSettings settings) : base(null)
        {
            _settings = settings;
            Title = "Animation"; // TODO: Add to Resources
            Glyph = "\xE916"; // Play icon
        }
    }
}
