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
        public ICommand ExportSettingsCommand { get; }
        public ICommand ImportSettingsCommand { get; }
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
            Properties.Resources.SettingsTelemetryDesc;

        /// <summary>
        /// Status text: "Activ\u00e9" or "D\u00e9sactiv\u00e9"
        /// </summary>
        public string TelemetryStatusText =>
            IsTelemetryEnabled ? Properties.Resources.SettingsTelemetryEnabled : Properties.Resources.SettingsTelemetryDisabled;

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
            Properties.Resources.SettingsUpdateChannelAllDesc,
            Properties.Resources.SettingsUpdateChannelMinorMajorDesc,
            Properties.Resources.SettingsUpdateChannelMajorOnlyDesc,
            Properties.Resources.SettingsUpdateChannelNoneDesc
        };

        public string UpdateChannelDescription
        {
            get
            {
                switch ((DataModel.UpdateChannel)UpdateChannelIndex)
                {
                    case DataModel.UpdateChannel.All: return Properties.Resources.SettingsUpdateChannelAllDetail;
                    case DataModel.UpdateChannel.MinorAndMajor: return Properties.Resources.SettingsUpdateChannelMinorMajorDetail;
                    case DataModel.UpdateChannel.MajorOnly: return Properties.Resources.SettingsUpdateChannelMajorOnlyDetail;
                    case DataModel.UpdateChannel.None: return Properties.Resources.SettingsUpdateChannelNoneDetail;
                    default: return "";
                }
            }
        }

        public bool IsUpdateAvailable => _updateService?.IsUpdateAvailable ?? false;
        public bool IsDownloading => _updateService?.IsDownloading ?? false;

        public string UpdateStatusText
        {
            get
            {
                var svc = _updateService;
                if (svc == null) return "";
                if (svc.IsDownloading) return Properties.Resources.SettingsUpdateStatusDownloading;
                if (svc.IsChecking) return Properties.Resources.SettingsUpdateStatusChecking;
                if (svc.IsUpdateAvailable) return string.Format(Properties.Resources.SettingsUpdateStatusAvailable, svc.LatestVersion);
                return Properties.Resources.SettingsUpdateStatusUpToDate;
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
            Subtitle = Properties.Resources.SettingsAboutSubtitle;
            AboutText = $"v{App.PackageVersion}";

            OpenAboutCommand = new RelayCommand(OpenAbout);
            OpenDiagnosticsCommand = new RelayCommand(OpenDiagnostics);
            OpenFeedbackCommand = new RelayCommand(OpenGitHubIssueChooser);
            OpenPrivacyPolicyCommand = new RelayCommand(OpenPrivacyPolicy);
            CheckForUpdateCommand = new RelayCommand(CheckForUpdate);
            ExportSettingsCommand = new RelayCommand(() => DataModel.SettingsExportService.ExportWithDialog(_settings));
            ImportSettingsCommand = new RelayCommand(() =>
            {
                if (DataModel.SettingsExportService.ImportWithDialog(_settings))
                {
                    System.Windows.MessageBox.Show(
                        Properties.Resources.SettingsImportSuccess,
                        Properties.Resources.SettingsImportTitle,
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            });
        }

        public void SetUpdateService(DataModel.UpdateService svc)
        {
            _updateService = svc;
            svc.PropertyChanged += (_, args) =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateStatusText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastCheckText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUpdateAvailable)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDownloading)));
            };
        }

        public void DownloadAndInstall()
        {
            _updateService?.DownloadAndInstallAsync();
        }

        /// <summary>
        /// DEBUG: simulate a fake update available to test the UI effect.
        /// Hold Left Ctrl when clicking "Vérifier" to trigger.
        /// </summary>
        private void SimulateFakeUpdate()
        {
            if (_updateService == null) return;
            // Use reflection to set private fields for testing
            var type = _updateService.GetType();
            var latestProp = type.GetProperty(nameof(DataModel.UpdateService.LatestVersion));
            latestProp?.SetValue(_updateService, "99.0.0");
            var updateProp = type.GetProperty(nameof(DataModel.UpdateService.IsUpdateAvailable));
            // IsUpdateAvailable has a private setter, so we set it via the backing field
            var field = type.GetField("_isUpdateAvailable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(_updateService, true);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUpdateAvailable)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateStatusText)));
            }
            System.Diagnostics.Trace.WriteLine("DEBUG: Simulated fake update v99.0.0");
        }

        private void CheckForUpdate()
        {
            // DEBUG: Hold Left Ctrl to simulate a fake update available
            if (Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                SimulateFakeUpdate();
                return;
            }
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
