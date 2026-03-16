using EarTrumpet.Diagnosis;
using EarTrumpet.Interop.Helpers;
using EarTrumpet.UI.Helpers;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace EarTrumpet.UI.ViewModels
{
    class EarTrumpetAboutPageViewModel : SettingsPageViewModel, INotifyPropertyChanged
    {
        public ICommand OpenDiagnosticsCommand { get; }
        public ICommand OpenAboutCommand { get; }
        public ICommand OpenFeedbackCommand { get; }
        public ICommand OpenPrivacyPolicyCommand { get; }
        public ICommand CheckForUpdateCommand { get; }
        public string AboutText { get; }

        public bool IsTelemetryEnabled
        {
            get => _settings.IsTelemetryEnabled;
            set
            {
                _settings.IsTelemetryEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTelemetryEnabled)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TelemetryStatusText)));
            }
        }

        /// <summary>
        /// Description text explaining what telemetry collects.
        /// </summary>
        public string TelemetryDescription =>
            "Envoyer des rapports de crash anonymes et des statistiques d'utilisation pour am\u00e9liorer BetterTrumpet. " +
            "Aucune donn\u00e9e personnelle n'est collect\u00e9e.";

        /// <summary>
        /// Status text: "Activ\u00e9" or "D\u00e9sactiv\u00e9"
        /// </summary>
        public string TelemetryStatusText =>
            IsTelemetryEnabled ? "Activ\u00e9" : "D\u00e9sactiv\u00e9";

        public bool AutoCheckForUpdates
        {
            get => _settings.AutoCheckForUpdates;
            set
            {
                _settings.AutoCheckForUpdates = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutoCheckForUpdates)));
            }
        }

        /// <summary>
        /// Index into the update channel combo: 0=All, 1=Minor+Major, 2=Major only, 3=None
        /// </summary>
        public int UpdateChannelIndex
        {
            get => (int)_settings.UpdateNotifyChannel;
            set
            {
                var channel = (DataModel.UpdateChannel)value;
                _settings.UpdateNotifyChannel = channel;
                if (_updateService != null)
                {
                    _updateService.Channel = channel;
                    // Re-check with the new filter
                    _updateService.CheckForUpdateAsync();
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateChannelIndex)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateChannelDescription)));
            }
        }

        public string[] UpdateChannelOptions => new[]
        {
            "Toutes les mises \u00e0 jour",
            "Mineures et majeures",
            "Majeures uniquement",
            "Aucune notification"
        };

        public string UpdateChannelDescription
        {
            get
            {
                switch ((DataModel.UpdateChannel)UpdateChannelIndex)
                {
                    case DataModel.UpdateChannel.All: return "Patch, mineures et majeures (ex: 3.0.0 \u2192 3.0.1)";
                    case DataModel.UpdateChannel.MinorAndMajor: return "Mineures et majeures uniquement (ex: 3.0 \u2192 3.1)";
                    case DataModel.UpdateChannel.MajorOnly: return "Majeures uniquement (ex: 3 \u2192 4)";
                    case DataModel.UpdateChannel.None: return "Aucune notification de mise \u00e0 jour";
                    default: return "";
                }
            }
        }

        public string UpdateStatusText
        {
            get
            {
                var svc = _updateService;
                if (svc == null) return "";
                if (svc.IsChecking) return "Checking...";
                return svc.UpdateText;
            }
        }

        public string LastCheckText
        {
            get
            {
                var svc = _updateService;
                if (svc == null) return "";
                return svc.LastCheckText;
            }
        }

        /// <summary>
        /// Health metrics summary from HealthMonitor.
        /// </summary>
        public string HealthSummary => HealthMonitor.GetHealthSummary();

        public new event PropertyChangedEventHandler PropertyChanged;

        private readonly Action _openDiagnostics;
        private readonly AppSettings _settings;
        private DataModel.UpdateService _updateService;

        public EarTrumpetAboutPageViewModel(Action openDiagnostics, AppSettings settings) : base(null)
        {
            _settings = settings;
            _openDiagnostics = openDiagnostics;
            Glyph = "\xE946";
            Title = Properties.Resources.AboutTitle;
            Subtitle = "Version info, telemetry, and diagnostics.";
            AboutText = $"v{App.PackageVersion}";

            OpenAboutCommand = new RelayCommand(OpenAbout);
            OpenDiagnosticsCommand = new RelayCommand(OpenDiagnostics);
            OpenFeedbackCommand = new RelayCommand(OpenGitHubIssueChooser);
            OpenPrivacyPolicyCommand = new RelayCommand(OpenPrivacyPolicy);
            CheckForUpdateCommand = new RelayCommand(CheckForUpdate);
        }

        public void SetUpdateService(DataModel.UpdateService svc)
        {
            _updateService = svc;
            svc.PropertyChanged += (_, args) =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateStatusText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastCheckText)));
            };
        }

        private void CheckForUpdate()
        {
            _updateService?.CheckForUpdateAsync();
        }

        private void OpenDiagnostics()
        {
            if (Keyboard.IsKeyDown(Key.LeftShift) && Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                Trace.WriteLine($"EarTrumpetAboutPageViewModel OpenDiagnostics - CRASH");
                throw new Exception("This is an intentional crash.");
            }

            _openDiagnostics.Invoke();
        }

        private void OpenGitHubIssueChooser() => ProcessHelper.StartNoThrow("https://github.com/xammen/BetterTrumpet/issues/new/choose");
        private void OpenAbout() => ProcessHelper.StartNoThrow("https://bettertrumpet.hiii.boo");
        private void OpenPrivacyPolicy() => ProcessHelper.StartNoThrow("https://github.com/xammen/BetterTrumpet/blob/master/PRIVACY.md");
    }
}
