using EarTrumpet.DataModel.Audio;
using EarTrumpet.UI.Helpers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

namespace EarTrumpet.UI.ViewModels
{
    class OnboardingViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler Completed;

        // Navigation
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage != value)
                {
                    _currentPage = value;
                    Raise(nameof(CurrentPage));
                    Raise(nameof(IsPage0));
                    Raise(nameof(IsPage1));
                    Raise(nameof(IsPage2));
                    Raise(nameof(IsPage3));
                    Raise(nameof(IsPage4));
                    Raise(nameof(CanGoBack));
                    Raise(nameof(NextButtonText));
                    Raise(nameof(SubtitleText));
                }
            }
        }

        public bool IsPage0 => _currentPage == 0;
        public bool IsPage1 => _currentPage == 1;
        public bool IsPage2 => _currentPage == 2;
        public bool IsPage3 => _currentPage == 3;
        public bool IsPage4 => _currentPage == 4;
        public bool CanGoBack => _currentPage > 0 && _currentPage < 4;

        public string NextButtonText
        {
            get
            {
                switch (_currentPage)
                {
                    case 0: return "Commencer";
                    case 3: return "Terminer";
                    case 4: return "C'est parti !";
                    default: return "Suivant";
                }
            }
        }

        public string SubtitleText
        {
            get
            {
                switch (_currentPage)
                {
                    case 0: return "Le mixeur audio, en mieux.";
                    case 1: return "Choisissez votre p\u00e9riph\u00e9rique audio par d\u00e9faut.";
                    case 2: return "Personnalisez l'apparence.";
                    case 3: return "Vos donn\u00e9es restent les v\u00f4tres.";
                    case 4: return "Tout est pr\u00eat.";
                    default: return "";
                }
            }
        }

        // Page 1: Audio devices
        public ObservableCollection<AudioDeviceChoice> AudioDevices { get; } = new ObservableCollection<AudioDeviceChoice>();
        public AudioDeviceChoice SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (_selectedDevice != value)
                {
                    _selectedDevice = value;
                    Raise(nameof(SelectedDevice));
                }
            }
        }

        // Page 2: Theme
        public int SelectedThemeIndex
        {
            get => _selectedThemeIndex;
            set
            {
                if (_selectedThemeIndex != value)
                {
                    _selectedThemeIndex = value;
                    Raise(nameof(SelectedThemeIndex));
                }
            }
        }

        // Page 3: Privacy
        public bool IsTelemetryEnabled
        {
            get => _settings.IsTelemetryEnabled;
            set
            {
                _settings.IsTelemetryEnabled = value;
                Raise(nameof(IsTelemetryEnabled));
                Raise(nameof(TelemetryStatusText));
            }
        }

        public string TelemetryStatusText => IsTelemetryEnabled ? "Activ\u00e9" : "D\u00e9sactiv\u00e9";

        // Page 4: Startup toggle
        public bool RunAtStartup
        {
            get => _settings.RunAtStartup;
            set
            {
                _settings.RunAtStartup = value;
                Raise(nameof(RunAtStartup));
            }
        }

        // Commands
        public ICommand NextCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand SkipCommand { get; }

        private int _currentPage;
        private AudioDeviceChoice _selectedDevice;
        private int _selectedThemeIndex;
        private readonly AppSettings _settings;
        private readonly IAudioDeviceManager _deviceManager;

        public OnboardingViewModel(AppSettings settings, IAudioDeviceManager deviceManager)
        {
            _settings = settings;
            _deviceManager = deviceManager;

            NextCommand = new RelayCommand(GoNext);
            BackCommand = new RelayCommand(GoBack);
            SkipCommand = new RelayCommand(Skip);

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
                    if (choice.IsDefault) _selectedDevice = choice;
                }
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
                case 1:
                    ApplyDefaultDevice();
                    break;
                case 2:
                    ApplyTheme();
                    break;
            }

            if (_currentPage < 4)
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
            if (_currentPage > 0) CurrentPage--;
        }

        private void Skip()
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyDefaultDevice()
        {
            if (_selectedDevice != null && !_selectedDevice.IsDefault)
            {
                try
                {
                    var dev = _deviceManager.Devices.FirstOrDefault(d => d.Id == _selectedDevice.Id);
                    if (dev != null)
                    {
                        _deviceManager.Default = dev;
                        Trace.WriteLine($"Onboarding: Set default device to {_selectedDevice.DisplayName}");
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Onboarding: Failed to set default device: {ex.Message}");
                }
            }
        }

        private void ApplyTheme()
        {
            if (_selectedThemeIndex == 1)
            {
                _settings.UseCustomSliderColors = true;
            }
        }

        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class AudioDeviceChoice
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public bool IsDefault { get; set; }
    }
}
