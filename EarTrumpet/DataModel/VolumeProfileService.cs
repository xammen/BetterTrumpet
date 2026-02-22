using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace EarTrumpet.DataModel
{
    /// <summary>
    /// Stores and restores volume profiles (snapshots of device + app volumes).
    /// Profiles are persisted as JSON in AppSettings.
    /// </summary>
    public class VolumeProfileService
    {
        public class AppVolumeEntry
        {
            public string ExeName { get; set; }
            public string AppId { get; set; }
            public string DisplayName { get; set; }
            public int Volume { get; set; }
            public bool IsMuted { get; set; }
        }

        public class DeviceVolumeEntry
        {
            public string DeviceId { get; set; }
            public string DisplayName { get; set; }
            public int Volume { get; set; }
            public bool IsMuted { get; set; }
            public List<AppVolumeEntry> Apps { get; set; } = new List<AppVolumeEntry>();
        }

        public class VolumeProfile
        {
            public string Name { get; set; }
            public string CreatedAt { get; set; }
            public List<DeviceVolumeEntry> Devices { get; set; } = new List<DeviceVolumeEntry>();
        }

        private readonly AppSettings _settings;
        public ObservableCollection<VolumeProfile> Profiles { get; } = new ObservableCollection<VolumeProfile>();

        public VolumeProfileService(AppSettings settings)
        {
            _settings = settings;
            LoadProfiles();
        }

        /// <summary>
        /// Capture current volumes from all devices and their apps
        /// </summary>
        public VolumeProfile CaptureCurrentState(string profileName, UI.ViewModels.DeviceCollectionViewModel collection)
        {
            var profile = new VolumeProfile
            {
                Name = profileName,
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Devices = new List<DeviceVolumeEntry>()
            };

            foreach (var device in collection.AllDevices)
            {
                var deviceEntry = new DeviceVolumeEntry
                {
                    DeviceId = device.Id,
                    DisplayName = device.DisplayName,
                    Volume = device.Volume,
                    IsMuted = device.IsMuted,
                    Apps = new List<AppVolumeEntry>()
                };

                foreach (var app in device.Apps)
                {
                    deviceEntry.Apps.Add(new AppVolumeEntry
                    {
                        ExeName = app.ExeName,
                        AppId = app.AppId,
                        DisplayName = app.DisplayName,
                        Volume = app.Volume,
                        IsMuted = app.IsMuted
                    });
                }

                profile.Devices.Add(deviceEntry);
            }

            return profile;
        }

        /// <summary>
        /// Apply a saved profile to current devices
        /// </summary>
        public void ApplyProfile(VolumeProfile profile, UI.ViewModels.DeviceCollectionViewModel collection)
        {
            if (profile == null || collection == null) return;

            foreach (var savedDevice in profile.Devices)
            {
                // Match device by ID first, then by display name
                var device = collection.AllDevices.FirstOrDefault(d => d.Id == savedDevice.DeviceId)
                          ?? collection.AllDevices.FirstOrDefault(d => d.DisplayName == savedDevice.DisplayName);

                if (device == null)
                {
                    Trace.WriteLine($"VolumeProfileService: Device not found: {savedDevice.DisplayName}");
                    continue;
                }

                device.Volume = savedDevice.Volume;
                device.IsMuted = savedDevice.IsMuted;

                foreach (var savedApp in savedDevice.Apps)
                {
                    // Match app by ExeName (most reliable), then AppId
                    var app = device.Apps.FirstOrDefault(a => 
                        !string.IsNullOrEmpty(savedApp.ExeName) && 
                        string.Equals(a.ExeName, savedApp.ExeName, StringComparison.OrdinalIgnoreCase))
                           ?? device.Apps.FirstOrDefault(a =>
                        !string.IsNullOrEmpty(savedApp.AppId) &&
                        string.Equals(a.AppId, savedApp.AppId, StringComparison.OrdinalIgnoreCase));

                    if (app != null)
                    {
                        app.Volume = savedApp.Volume;
                        app.IsMuted = savedApp.IsMuted;
                    }
                    else
                    {
                        Trace.WriteLine($"VolumeProfileService: App not found: {savedApp.DisplayName} ({savedApp.ExeName})");
                    }
                }
            }

            Trace.WriteLine($"VolumeProfileService: Applied profile '{profile.Name}'");
        }

        public void SaveProfile(VolumeProfile profile)
        {
            // Replace existing profile with same name, or add new
            var existing = Profiles.FirstOrDefault(p => p.Name == profile.Name);
            if (existing != null)
            {
                var idx = Profiles.IndexOf(existing);
                Profiles[idx] = profile;
            }
            else
            {
                Profiles.Add(profile);
            }
            PersistProfiles();
        }

        public void DeleteProfile(VolumeProfile profile)
        {
            Profiles.Remove(profile);
            PersistProfiles();
        }

        public void RenameProfile(VolumeProfile profile, string newName)
        {
            profile.Name = newName;
            PersistProfiles();
        }

        private void PersistProfiles()
        {
            try
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(Profiles.ToList(), Newtonsoft.Json.Formatting.Indented);
                _settings.VolumeProfilesJson = json;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"VolumeProfileService: PersistProfiles failed - {ex.Message}");
            }
        }

        private void LoadProfiles()
        {
            try
            {
                var json = _settings.VolumeProfilesJson;
                if (string.IsNullOrWhiteSpace(json) || json == "[]") return;

                var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<VolumeProfile>>(json);
                if (list == null) return;

                foreach (var profile in list)
                {
                    Profiles.Add(profile);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"VolumeProfileService: LoadProfiles failed - {ex.Message}");
            }
        }

        /// <summary>
        /// Export a profile to JSON string
        /// </summary>
        public string ExportProfile(VolumeProfile profile)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(profile, Newtonsoft.Json.Formatting.Indented);
        }

        /// <summary>
        /// Import a profile from JSON string
        /// </summary>
        public VolumeProfile ImportProfile(string json)
        {
            try
            {
                var profile = Newtonsoft.Json.JsonConvert.DeserializeObject<VolumeProfile>(json);
                if (profile != null)
                {
                    // Ensure unique name
                    if (Profiles.Any(p => p.Name == profile.Name))
                    {
                        profile.Name = profile.Name + " (Imported)";
                    }
                    Profiles.Add(profile);
                    PersistProfiles();
                }
                return profile;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"VolumeProfileService: ImportProfile failed - {ex.Message}");
                return null;
            }
        }
    }
}
