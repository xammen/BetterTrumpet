
using EarTrumpet.UI.Helpers;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;


namespace EarTrumpet.UI.ViewModels
{
    public class EarTrumpetLegacySettingsPageViewModel : SettingsPageViewModel
    {
        public class HiddenAppEntryRow
        {
            public string DisplayName { get; set; }
            public string DeviceName { get; set; }
            public string DeviceId { get; set; }
            public string AppId { get; set; }
            public string ExeName { get; set; }
        }

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

        public bool ShowAppTooltips
        {
            get => _settings.ShowAppTooltips;
            set => _settings.ShowAppTooltips = value;
        }

        public bool HasHiddenApps => _settings.HiddenAppsCount > 0;
        public int HiddenAppsCount => _settings.HiddenAppsCount;

        public string HiddenAppsSummaryText => HiddenAppsCount > 0
            ? string.Format(Properties.Resources.SettingsHiddenAppsSummaryFormat, HiddenAppsCount)
            : Properties.Resources.SettingsHiddenAppsNone;

        public ObservableCollection<HiddenAppEntryRow> HiddenApps { get; } = new ObservableCollection<HiddenAppEntryRow>();

        public ICommand RestoreHiddenAppsCommand { get; }
        public ICommand RestoreHiddenAppEntryCommand { get; }

        private readonly AppSettings _settings;

        public EarTrumpetLegacySettingsPageViewModel(AppSettings settings) : base(null)
        {
            _settings = settings;
            Title = Properties.Resources.LegacySettingsPageText;
            Subtitle = "Startup behavior, tray icon, tooltips, and hidden app controls.";
            Glyph = "\xE825";

            _settings.HiddenAppsChanged += OnHiddenAppsChanged;

            RestoreHiddenAppsCommand = new RelayCommand(() => _settings.UnhideAllApps());
            RestoreHiddenAppEntryCommand = new RelayCommand<HiddenAppEntryRow>(RestoreHiddenAppEntry);

            RefreshHiddenAppsRows();
        }

        private void OnHiddenAppsChanged()
        {
            RefreshHiddenAppsRows();
            RaisePropertyChanged(nameof(HasHiddenApps));
            RaisePropertyChanged(nameof(HiddenAppsCount));
            RaisePropertyChanged(nameof(HiddenAppsSummaryText));
        }

        private void RestoreHiddenAppEntry(HiddenAppEntryRow row)
        {
            if (row == null)
            {
                return;
            }

            _settings.UnhideAppForDevice(row.DeviceId, row.AppId, row.ExeName);
        }

        private void RefreshHiddenAppsRows()
        {
            HiddenApps.Clear();

            var app = Application.Current as App;
            var deviceNames = app?.CollectionViewModel?.AllDevices
                .ToDictionary(d => d.Id, d => d.DisplayName, System.StringComparer.OrdinalIgnoreCase);

            foreach (var entry in _settings.GetHiddenApps())
            {
                var label = !string.IsNullOrWhiteSpace(entry.DisplayName)
                    ? entry.DisplayName
                    : !string.IsNullOrWhiteSpace(entry.ExeName)
                        ? entry.ExeName
                        : !string.IsNullOrWhiteSpace(entry.AppId)
                            ? entry.AppId
                            : Properties.Resources.SettingsHiddenAppsUnknown;

                var deviceName = entry.DeviceId;
                if (deviceNames != null && !string.IsNullOrWhiteSpace(entry.DeviceId) && deviceNames.TryGetValue(entry.DeviceId, out var knownName))
                {
                    deviceName = knownName;
                }

                HiddenApps.Add(new HiddenAppEntryRow
                {
                    DisplayName = label,
                    DeviceName = deviceName,
                    DeviceId = entry.DeviceId,
                    AppId = entry.AppId,
                    ExeName = entry.ExeName,
                });
            }

            RaisePropertyChanged(nameof(HiddenApps));
        }
    }
}
