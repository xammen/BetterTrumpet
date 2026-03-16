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

        /// <summary>
        /// Health metrics summary from HealthMonitor.
        /// </summary>
        public string HealthSummary => HealthMonitor.GetHealthSummary();

        public new event PropertyChangedEventHandler PropertyChanged;

        private readonly Action _openDiagnostics;
        private readonly AppSettings _settings;

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
