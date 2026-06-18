using EarTrumpet.DataModel;
using EarTrumpet.DataModel.Audio;
using EarTrumpet.UI.Helpers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;

namespace EarTrumpet.UI.ViewModels
{
    class OnboardingViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler Completed;

        public const int PageCount = 5;

        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage != value)
                {
                    _currentPage = value;
                    RaisePageState();
                }
            }
        }

        public bool IsPage1 => _currentPage == 0;
        public bool IsPage2 => _currentPage == 1;
        public bool IsPage3 => _currentPage == 2;
        public bool IsPage4 => _currentPage == 3;
        public bool IsPage5 => _currentPage == 4;
        public bool CanGoBack => _currentPage > 0;
        public bool IsLastPage => _currentPage == PageCount - 1;
        public double Progress => (double)(_currentPage + 1) / PageCount;

        public string NextButtonText => IsLastPage
            ? Properties.Resources.OnboardingDone
            : Properties.Resources.OnboardingContinue;

        public string StepLabel => string.Format(Properties.Resources.OnboardingStepFormat, _currentPage + 1, PageCount);

        public string CurrentTitle
        {
            get
            {
                switch (_currentPage)
                {
                    case 0: return Properties.Resources.OnboardingAudioOutput;
                    case 1: return Properties.Resources.OnboardingAppearance;
                    case 2: return Properties.Resources.OnboardingPrivacy;
                    case 3: return Properties.Resources.OnboardingAllReady;
                    case 4: return Properties.Resources.OnboardingPinTitle;
                    default: return string.Empty;
                }
            }
        }

        public string SubtitleText
        {
            get
            {
                switch (_currentPage)
                {
                    case 0: return Properties.Resources.OnboardingSubtitleAudio;
                    case 1: return Properties.Resources.OnboardingSubtitleAppearance;
                    case 2: return Properties.Resources.OnboardingSubtitlePrivacy;
                    case 3: return Properties.Resources.OnboardingAllReadyDesc;
                    case 4: return Properties.Resources.OnboardingPinDesc;
                    default: return string.Empty;
                }
            }
        }

        public ObservableCollection<AudioDeviceChoice> AudioDevices { get; } = new ObservableCollection<AudioDeviceChoice>();

        public AudioDeviceChoice SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (_selectedDevice != value)
                {
                    _selectedDevice = value;
                    foreach (var device in AudioDevices)
                    {
                        device.IsDefault = device == value;
                    }
                    Raise(nameof(SelectedDevice));
                }
            }
        }

        public bool HasNoDevices => AudioDevices.Count == 0;

        public int SelectedThemeIndex
        {
            get => _selectedThemeIndex;
            set
            {
                if (_selectedThemeIndex != value)
                {
                    _selectedThemeIndex = value;
                    RaiseThemeState();
                }
            }
        }

        public bool IsSystemThemeSelected
        {
            get => _selectedThemeIndex == 0;
            set { if (value) SelectedThemeIndex = 0; }
        }

        public bool IsCustomThemeSelected
        {
            get => _selectedThemeIndex == 1;
            set { if (value) SelectedThemeIndex = 1; }
        }

        public bool IsTelemetryEnabled
        {
            get => _isTelemetryEnabled;
            set
            {
                if (_isTelemetryEnabled && !value && !_allowTelemetryDisable)
                {
                    IsTelemetryDisableConfirmationVisible = true;
                    Raise(nameof(IsTelemetryEnabled));
                    return;
                }

                if (_isTelemetryEnabled != value)
                {
                    _isTelemetryEnabled = value;
                    if (_isTelemetryEnabled)
                    {
                        IsTelemetryDisableConfirmationVisible = false;
                    }
                    Raise(nameof(IsTelemetryEnabled));
                    Raise(nameof(ShowTelemetryReassurance));
                }
            }
        }

        public bool ShowTelemetryReassurance => !IsTelemetryEnabled;

        public bool IsTelemetryDisableConfirmationVisible
        {
            get => _isTelemetryDisableConfirmationVisible;
            private set
            {
                if (_isTelemetryDisableConfirmationVisible != value)
                {
                    _isTelemetryDisableConfirmationVisible = value;
                    Raise(nameof(IsTelemetryDisableConfirmationVisible));
                }
            }
        }

        public bool RunAtStartup
        {
            get => _runAtStartup;
            set
            {
                if (_runAtStartup != value)
                {
                    _runAtStartup = value;
                    Raise(nameof(RunAtStartup));
                }
            }
        }

        public ICommand NextCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand SkipCommand { get; }
        public ICommand ConfirmDisableTelemetryCommand { get; }
        public ICommand KeepTelemetryEnabledCommand { get; }

        private int _currentPage;
        private AudioDeviceChoice _selectedDevice;
        private int _selectedThemeIndex;
        private bool _isTelemetryEnabled = true;
        private bool _isTelemetryDisableConfirmationVisible;
        private bool _allowTelemetryDisable;
        private bool _runAtStartup;
        private readonly AppSettings _settings;
        private readonly IAudioDeviceManager _deviceManager;

        public OnboardingViewModel(AppSettings settings, IAudioDeviceManager deviceManager)
        {
            _settings = settings;
            _deviceManager = deviceManager;
            _isTelemetryEnabled = _settings.IsTelemetryEnabled;
            _runAtStartup = _settings.RunAtStartup;
            _selectedThemeIndex = _settings.UseCustomSliderColors ? 1 : 0;

            NextCommand = new RelayCommand(GoNext);
            BackCommand = new RelayCommand(GoBack);
            SkipCommand = new RelayCommand(Skip);
            ConfirmDisableTelemetryCommand = new RelayCommand(ConfirmDisableTelemetry);
            KeepTelemetryEnabledCommand = new RelayCommand(KeepTelemetryEnabled);

            LoadDevices();
        }

        private void LoadDevices()
        {
            try
            {
                var defaultId = _deviceManager.Default?.Id;
                foreach (var dev in _deviceManager.Devices.OrderBy(d => d.DisplayName))
                {
                    var choice = new AudioDeviceChoice
                    {
                        Id = dev.Id,
                        DisplayName = dev.DisplayName,
                        IsDefault = dev.Id == defaultId
                    };

                    AudioDevices.Add(choice);
                    if (choice.IsDefault)
                    {
                        _selectedDevice = choice;
                    }
                }

                Raise(nameof(HasNoDevices));
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Onboarding: Failed to load devices: {ex.Message}");
            }
        }

        private void GoNext()
        {
            switch (_currentPage)
            {
                case 0:
                    ApplyDefaultDevice();
                    break;
                case 1:
                    ApplyTheme();
                    break;
                case 2:
                    ApplyPrivacyAndUpdates();
                    break;
            }

            if (_currentPage < PageCount - 1)
            {
                CurrentPage++;
            }
            else
            {
                Completed?.Invoke(this, EventArgs.Empty);
            }
        }

        private void GoBack()
        {
            if (_currentPage > 0)
            {
                CurrentPage--;
            }
        }

        private void Skip()
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }

        private void ConfirmDisableTelemetry()
        {
            _allowTelemetryDisable = true;
            IsTelemetryDisableConfirmationVisible = false;
            IsTelemetryEnabled = false;
            _allowTelemetryDisable = false;
        }

        private void KeepTelemetryEnabled()
        {
            IsTelemetryDisableConfirmationVisible = false;
            if (!_isTelemetryEnabled)
            {
                _isTelemetryEnabled = true;
            }
            Raise(nameof(IsTelemetryEnabled));
            Raise(nameof(ShowTelemetryReassurance));
        }

        private void ApplyDefaultDevice()
        {
            if (_selectedDevice == null) return;

            try
            {
                var currentDefault = _deviceManager.Default;
                if (currentDefault == null || currentDefault.Id != _selectedDevice.Id)
                {
                    var dev = _deviceManager.Devices.FirstOrDefault(d => d.Id == _selectedDevice.Id);
                    if (dev != null)
                    {
                        _deviceManager.Default = dev;
                        Trace.WriteLine($"Onboarding: Set default device to {_selectedDevice.DisplayName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Onboarding: Failed to set default device: {ex.Message}");
            }
        }

        private void ApplyTheme()
        {
            if (_selectedThemeIndex == 0)
            {
                _settings.BeginBatch();
                _settings.UseCustomSliderColors = false;
                _settings.UseDynamicAlbumArtTheme = false;
                _settings.ActiveThemeName = string.Empty;
                _settings.SliderThumbColor = Colors.Transparent;
                _settings.SliderTrackFillColor = Colors.Transparent;
                _settings.SliderTrackBackgroundColor = Colors.Transparent;
                _settings.PeakMeterColor = Colors.Transparent;
                _settings.WindowBackgroundColor = Colors.Transparent;
                _settings.TextColor = Colors.Transparent;
                _settings.AccentGlowColor = Colors.Transparent;
                _settings.EndBatch();
                return;
            }

            _settings.BeginBatch();
            _settings.UseCustomSliderColors = true;
            _settings.UseDynamicAlbumArtTheme = false;
            _settings.ActiveThemeName = "BetterTrumpet Focus";
            _settings.SliderThumbColor = Color.FromRgb(88, 166, 255);
            _settings.SliderTrackFillColor = Color.FromRgb(59, 158, 255);
            _settings.SliderTrackBackgroundColor = Color.FromRgb(48, 55, 68);
            _settings.PeakMeterColor = Color.FromRgb(78, 213, 168);
            _settings.WindowBackgroundColor = Color.FromRgb(18, 18, 22);
            _settings.TextColor = Color.FromRgb(238, 241, 246);
            _settings.AccentGlowColor = Color.FromRgb(59, 158, 255);
            _settings.EndBatch();
        }

        private void ApplyPrivacyAndUpdates()
        {
            _settings.IsTelemetryEnabled = _isTelemetryEnabled;
            _settings.RunAtStartup = _runAtStartup;
            _settings.UpdateNotifyChannel = UpdateChannel.All;
        }

        private void RaisePageState()
        {
            Raise(nameof(CurrentPage));
            Raise(nameof(IsPage1));
            Raise(nameof(IsPage2));
            Raise(nameof(IsPage3));
            Raise(nameof(IsPage4));
            Raise(nameof(IsPage5));
            Raise(nameof(CanGoBack));
            Raise(nameof(IsLastPage));
            Raise(nameof(NextButtonText));
            Raise(nameof(StepLabel));
            Raise(nameof(CurrentTitle));
            Raise(nameof(SubtitleText));
            Raise(nameof(Progress));
        }

        private void RaiseThemeState()
        {
            Raise(nameof(SelectedThemeIndex));
            Raise(nameof(IsSystemThemeSelected));
            Raise(nameof(IsCustomThemeSelected));
        }

        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class AudioDeviceChoice : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string Id { get; set; }
        public string DisplayName { get; set; }

        private bool _isDefault;
        public bool IsDefault
        {
            get => _isDefault;
            set
            {
                if (_isDefault != value)
                {
                    _isDefault = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDefault)));
                }
            }
        }
    }
}
