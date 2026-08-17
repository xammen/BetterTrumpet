using EarTrumpet.Logic;
using EarTrumpet.UI.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace EarTrumpet.UI.ViewModels
{
    /// <summary>
    /// The manageable list of per-app rules: one row per app, editable in place.
    /// Rules can also be created here for apps that aren't running, which is the only
    /// way to reach an app that never shows up in the flyout.
    /// </summary>
    public class EarTrumpetAppRulesSettingsPageViewModel : SettingsPageViewModel
    {
        private readonly AppSettings _settings;
        private bool _isSubscribed;
        private bool _syncPending;
        private bool _isAddRulePanelOpen;

        public ObservableCollection<AppRuleItemViewModel> Rules { get; } = new ObservableCollection<AppRuleItemViewModel>();
        public ObservableCollection<FolderVolumeRuleItemViewModel> FolderVolumeRules { get; } = new ObservableCollection<FolderVolumeRuleItemViewModel>();

        public bool HasRules => Rules.Count > 0;
        public bool IsEmpty => Rules.Count == 0 && FolderVolumeRules.Count == 0;
        public bool ShowAppRulesEmptyHint => Rules.Count == 0 && FolderVolumeRules.Count == 0;
        public bool HasFolderVolumeRules => FolderVolumeRules.Count > 0;
        public bool HasNoFolderVolumeRules => FolderVolumeRules.Count == 0;

        private string _newRuleExeName = "";
        public string NewRuleExeName
        {
            get => _newRuleExeName;
            set
            {
                _newRuleExeName = value;
                RaisePropertyChanged(nameof(NewRuleExeName));
                RaisePropertyChanged(nameof(CanAddRule));
            }
        }

        public bool CanAddRule => !string.IsNullOrWhiteSpace(NewRuleExeName);

        public bool IsAddRulePanelOpen
        {
            get => _isAddRulePanelOpen;
            set
            {
                if (_isAddRulePanelOpen != value)
                {
                    _isAddRulePanelOpen = value;
                    RaisePropertyChanged(nameof(IsAddRulePanelOpen));
                }
            }
        }

        public ICommand AddRuleCommand { get; }
        public ICommand BrowseForExeCommand { get; }
        public ICommand RemoveRuleCommand { get; }
        public ICommand ClearAllRulesCommand { get; }
        public ICommand ToggleAddRulePanelCommand { get; }
        public ICommand AddFolderVolumeRuleCommand { get; }
        public ICommand BrowseForFolderVolumeRuleCommand { get; }
        public ICommand RemoveFolderVolumeRuleCommand { get; }

        public EarTrumpetAppRulesSettingsPageViewModel(AppSettings settings) : base(null)
        {
            _settings = settings;
            Title = Properties.Resources.AppRulesSettingsPageText;
            Subtitle = Properties.Resources.AppRulesSettingsPageSubtitle;
            Glyph = "\xE72E"; // Lock icon

            AddRuleCommand = new RelayCommand(AddRuleFromExeName);
            BrowseForExeCommand = new RelayCommand(BrowseForExe);
            RemoveRuleCommand = new RelayCommand<AppRuleItemViewModel>(RemoveRule);
            ClearAllRulesCommand = new RelayCommand(ClearAllRules);
            ToggleAddRulePanelCommand = new RelayCommand(() => IsAddRulePanelOpen = !IsAddRulePanelOpen);
            AddFolderVolumeRuleCommand = new RelayCommand(AddFolderVolumeRule);
            BrowseForFolderVolumeRuleCommand = new RelayCommand<FolderVolumeRuleItemViewModel>(BrowseForFolderVolumeRule);
            RemoveFolderVolumeRuleCommand = new RelayCommand<FolderVolumeRuleItemViewModel>(RemoveFolderVolumeRule);

            SyncRules();
            SyncFolderVolumeRules();
        }

        /// <summary>
        /// Subscribe only while this page is visible. This keeps flyout edits live without
        /// letting the long-lived settings object retain closed settings page instances.
        /// </summary>
        public override void NavigatedTo()
        {
            if (!_isSubscribed)
            {
                _settings.AppRulesChanged += OnAppRulesChanged;
                _isSubscribed = true;
            }

            SyncRules();
            SyncFolderVolumeRules();
        }

        public override bool NavigatingFrom(NavigationCookie cookie)
        {
            if (_isSubscribed)
            {
                _settings.AppRulesChanged -= OnAppRulesChanged;
                _isSubscribed = false;
            }

            foreach (var row in Rules)
            {
                row.Detach();
            }

            Rules.Clear();
            FolderVolumeRules.Clear();
            RaiseRuleCollectionStateChanged();
            return base.NavigatingFrom(cookie);
        }

        private void OnAppRulesChanged()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || _syncPending)
            {
                return;
            }

            _syncPending = true;
            dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() =>
            {
                _syncPending = false;
                if (_isSubscribed)
                {
                    SyncRules();
                }
            }));
        }

        private void SyncRules()
        {
            try
            {
                var runningApps = GetRunningApps();
                var storedRules = _settings.GetAppRules();
                var rowsByExeName = Rules.ToDictionary(row => row.ExeName, StringComparer.OrdinalIgnoreCase);
                var retainedExeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int index = 0; index < storedRules.Count; index++)
                {
                    var rule = storedRules[index];
                    retainedExeNames.Add(rule.ExeName);
                    runningApps.TryGetValue(rule.ExeName, out var liveApp);

                    if (!rowsByExeName.TryGetValue(rule.ExeName, out var row))
                    {
                        row = new AppRuleItemViewModel(_settings, rule, liveApp);
                        Rules.Insert(Math.Min(index, Rules.Count), row);
                        rowsByExeName.Add(rule.ExeName, row);
                    }
                    else
                    {
                        row.Apply(rule, liveApp);
                        var currentIndex = Rules.IndexOf(row);
                        if (currentIndex != index)
                        {
                            Rules.Move(currentIndex, index);
                        }
                    }
                }

                for (int index = Rules.Count - 1; index >= 0; index--)
                {
                    var row = Rules[index];
                    if (!retainedExeNames.Contains(row.ExeName))
                    {
                        row.Detach();
                        Rules.RemoveAt(index);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"AppRulesVM SyncRules failed: {ex.Message}");
            }

            RaiseRuleCollectionStateChanged();
        }

        private void RaiseRuleCollectionStateChanged()
        {
            RaisePropertyChanged(nameof(HasRules));
            RaisePropertyChanged(nameof(IsEmpty));
            RaisePropertyChanged(nameof(ShowAppRulesEmptyHint));
            RaisePropertyChanged(nameof(HasFolderVolumeRules));
            RaisePropertyChanged(nameof(HasNoFolderVolumeRules));
        }

        private void SyncFolderVolumeRules()
        {
            FolderVolumeRules.Clear();
            foreach (var rule in _settings.GetFolderVolumeRules())
            {
                FolderVolumeRules.Add(new FolderVolumeRuleItemViewModel(_settings, rule));
            }

            RaiseRuleCollectionStateChanged();
        }

        private Dictionary<string, IAppItemViewModel> GetRunningApps()
        {
            var running = new Dictionary<string, IAppItemViewModel>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var collection = ((App)Application.Current).CollectionViewModel;
                if (collection == null)
                {
                    return running;
                }

                foreach (var device in collection.AllDevices)
                {
                    foreach (var app in device.Apps)
                    {
                        if (!string.IsNullOrWhiteSpace(app.ExeName))
                        {
                            if (!running.ContainsKey(app.ExeName))
                            {
                                running.Add(app.ExeName, app);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"AppRulesVM GetRunningApps failed: {ex.Message}");
            }

            return running;
        }

        // A new rule starts as a hard mute: that's the only state that means something
        // on its own, and the row's controls take it from there.
        private void AddRuleFromExeName()
        {
            var exeName = AppIdentity.NormalizeExeName(NewRuleExeName);
            if (string.IsNullOrEmpty(exeName))
            {
                return;
            }

            _settings.SetAppHardMuted(exeName, true, exeName);
            NewRuleExeName = "";
            IsAddRulePanelOpen = false;
            SyncRules();
        }

        private void BrowseForExe()
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = Properties.Resources.AppRulesBrowseDialogTitle,
                    Filter = Properties.Resources.AppRulesBrowseDialogFilter,
                    DefaultExt = ".exe",
                    CheckFileExists = true,
                };

                if (dlg.ShowDialog() == true)
                {
                    var exeName = AppIdentity.NormalizeExeName(dlg.FileName);
                    _settings.SetAppHardMuted(exeName, true, exeName, dlg.FileName, true);
                    NewRuleExeName = "";
                    IsAddRulePanelOpen = false;
                    SyncRules();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"AppRulesVM BrowseForExe failed: {ex.Message}");
            }
        }

        private void AddFolderVolumeRule()
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = Properties.Resources.FolderVolumeRulesBrowseDialogTitle,
                };

                if (dlg.ShowDialog() == true)
                {
                    _settings.AddFolderVolumeRule(dlg.FolderName);
                    SyncFolderVolumeRules();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"AppRulesVM AddFolderVolumeRule failed: {ex.Message}");
            }
        }

        private void BrowseForFolderVolumeRule(FolderVolumeRuleItemViewModel rule)
        {
            if (rule == null)
            {
                return;
            }

            try
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = Properties.Resources.FolderVolumeRulesBrowseDialogTitle,
                    FolderName = rule.FolderPath,
                };

                if (dlg.ShowDialog() == true)
                {
                    rule.FolderPath = dlg.FolderName;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"AppRulesVM BrowseForFolderVolumeRule failed: {ex.Message}");
            }
        }

        private void RemoveFolderVolumeRule(FolderVolumeRuleItemViewModel rule)
        {
            if (rule == null)
            {
                return;
            }

            _settings.RemoveFolderVolumeRule(rule.Id);
            FolderVolumeRules.Remove(rule);
            RaiseRuleCollectionStateChanged();
        }

        private void RemoveRule(AppRuleItemViewModel row)
        {
            if (row == null)
            {
                return;
            }

            _settings.RemoveAppRule(row.ExeName);
            SyncRules();
        }

        private void ClearAllRules()
        {
            if (Rules.Count == 0)
            {
                return;
            }

            var result = MessageBox.Show(
                Properties.Resources.AppRulesClearAllConfirmText,
                Properties.Resources.AppRulesClearAllConfirmTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _settings.ClearAppRules();
                SyncRules();
            }
        }
    }

    public class FolderVolumeRuleItemViewModel : BindableBase
    {
        private readonly AppSettings _settings;
        private string _folderPath;
        private int _volumePercent;

        public string Id { get; }

        public string FolderPath
        {
            get => _folderPath;
            set
            {
                if (string.Equals(_folderPath, value, StringComparison.Ordinal))
                {
                    return;
                }

                _folderPath = value;
                _settings.UpdateFolderVolumeRule(Id, _folderPath, _volumePercent);
                RaisePropertyChanged(nameof(FolderPath));
            }
        }

        public int VolumePercent
        {
            get => _volumePercent;
            set
            {
                if (_volumePercent == value)
                {
                    return;
                }

                _volumePercent = value;
                _settings.UpdateFolderVolumeRule(Id, _folderPath, _volumePercent);
                RaisePropertyChanged(nameof(VolumePercent));
                RaisePropertyChanged(nameof(VolumePercentText));
            }
        }

        public string VolumePercentText => VolumePercent + "%";

        public FolderVolumeRuleItemViewModel(AppSettings settings, AppSettings.FolderVolumeRuleEntry rule)
        {
            _settings = settings;
            Id = rule.Id;
            _folderPath = rule.FolderPath;
            _volumePercent = rule.VolumePercent;
        }
    }
}
