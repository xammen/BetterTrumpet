using EarTrumpet.DataModel;
using EarTrumpet.UI.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace EarTrumpet.UI.ViewModels
{
    public class EarTrumpetVolumeProfilesSettingsPageViewModel : SettingsPageViewModel
    {
        private readonly AppSettings _settings;
        private readonly VolumeProfileService _profileService;

        public ObservableCollection<VolumeProfileService.VolumeProfile> Profiles => _profileService.Profiles;

        private VolumeProfileService.VolumeProfile _selectedProfile;
        public VolumeProfileService.VolumeProfile SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                _selectedProfile = value;
                RaisePropertyChanged(nameof(SelectedProfile));
                RaisePropertyChanged(nameof(HasSelectedProfile));
                RaisePropertyChanged(nameof(SelectedProfileDetails));
            }
        }

        public bool HasSelectedProfile => _selectedProfile != null;

        public string SelectedProfileDetails
        {
            get
            {
                if (_selectedProfile == null) return "";
                var devices = _selectedProfile.Devices?.Count ?? 0;
                var apps = _selectedProfile.Devices?.Sum(d => d.Apps?.Count ?? 0) ?? 0;
                return $"{devices} device(s), {apps} app(s) | {_selectedProfile.CreatedAt}";
            }
        }

        private string _newProfileName = "";
        public string NewProfileName
        {
            get => _newProfileName;
            set
            {
                _newProfileName = value;
                RaisePropertyChanged(nameof(NewProfileName));
            }
        }

        // Commands
        public ICommand SaveCurrentCommand { get; }
        public ICommand ApplyProfileCommand { get; }
        public ICommand DeleteProfileCommand { get; }
        public ICommand ExportProfileCommand { get; }
        public ICommand ImportProfileCommand { get; }

        public EarTrumpetVolumeProfilesSettingsPageViewModel(AppSettings settings) : base(null)
        {
            _settings = settings;
            Title = "Volume Profiles";
            Glyph = "\xE9CE"; // Save icon

            _profileService = new VolumeProfileService(settings);

            SaveCurrentCommand = new RelayCommand(SaveCurrentProfile);
            ApplyProfileCommand = new RelayCommand(ApplySelectedProfile);
            DeleteProfileCommand = new RelayCommand(DeleteSelectedProfile);
            ExportProfileCommand = new RelayCommand(ExportSelectedProfile);
            ImportProfileCommand = new RelayCommand(ImportProfileFromFile);
        }

        private DeviceCollectionViewModel GetCollectionViewModel()
        {
            try
            {
                return ((App)Application.Current).CollectionViewModel;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"VolumeProfilesVM: GetCollectionViewModel failed - {ex.Message}");
                return null;
            }
        }

        private void SaveCurrentProfile()
        {
            var name = string.IsNullOrWhiteSpace(_newProfileName) 
                ? $"Profile {DateTime.Now:yyyy-MM-dd HH:mm}" 
                : _newProfileName.Trim();

            var collection = GetCollectionViewModel();
            if (collection == null) return;

            var profile = _profileService.CaptureCurrentState(name, collection);
            _profileService.SaveProfile(profile);
            SelectedProfile = profile;
            NewProfileName = "";

            RaisePropertyChanged(nameof(Profiles));
            Trace.WriteLine($"VolumeProfilesVM: Saved profile '{name}'");
        }

        private void ApplySelectedProfile()
        {
            if (_selectedProfile == null) return;

            var collection = GetCollectionViewModel();
            if (collection == null) return;

            var result = MessageBox.Show(
                $"Apply profile \"{_selectedProfile.Name}\"?\n\nThis will change volumes for all matching devices and apps.",
                "Apply Volume Profile",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _profileService.ApplyProfile(_selectedProfile, collection);
            }
        }

        private void DeleteSelectedProfile()
        {
            if (_selectedProfile == null) return;

            var result = MessageBox.Show(
                $"Delete profile \"{_selectedProfile.Name}\"?",
                "Delete Volume Profile",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var toDelete = _selectedProfile;
                SelectedProfile = null;
                _profileService.DeleteProfile(toDelete);
                RaisePropertyChanged(nameof(Profiles));
            }
        }

        private void ExportSelectedProfile()
        {
            if (_selectedProfile == null) return;

            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Volume Profile",
                    Filter = "BetterTrumpet Profile (*.btprofile)|*.btprofile|JSON Files (*.json)|*.json",
                    DefaultExt = ".btprofile",
                    FileName = SanitizeFileName(_selectedProfile.Name)
                };

                if (dlg.ShowDialog() == true)
                {
                    var json = _profileService.ExportProfile(_selectedProfile);
                    System.IO.File.WriteAllText(dlg.FileName, json, System.Text.Encoding.UTF8);
                    Trace.WriteLine($"VolumeProfilesVM: Exported to {dlg.FileName}");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"VolumeProfilesVM: Export failed - {ex.Message}");
            }
        }

        private void ImportProfileFromFile()
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import Volume Profile",
                    Filter = "BetterTrumpet Profile (*.btprofile)|*.btprofile|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = ".btprofile"
                };

                if (dlg.ShowDialog() == true)
                {
                    var json = System.IO.File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8);
                    var profile = _profileService.ImportProfile(json);
                    if (profile != null)
                    {
                        SelectedProfile = profile;
                        RaisePropertyChanged(nameof(Profiles));
                        Trace.WriteLine($"VolumeProfilesVM: Imported from {dlg.FileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"VolumeProfilesVM: Import failed - {ex.Message}");
            }
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "profile";
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
