using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;
using EarTrumpet.DataModel.WindowsAudio;
using EarTrumpet.Interop.Helpers;
using EarTrumpet.UI.ViewModels;

namespace EarTrumpet.DataModel
{
    /// <summary>
    /// Stores and restores volume profiles (snapshots of device + app volumes).
    /// Profiles are persisted as JSON in AppSettings.
    /// </summary>
    public class VolumeProfileService
    {
        public enum CaptureScope
        {
            CurrentDevice = 0,
            AllDevices = 1,
        }

        public class ApplyProfileResult
        {
            public string Name { get; set; }
            public int DevicesApplied { get; set; }
            public int AppsApplied { get; set; }
            public int AppsMissing { get; set; }
            public int AppsRouted { get; set; }
            public List<string> Warnings { get; set; } = new List<string>();
        }

        public class AppVolumeEntry
        {
            public string ExeName { get; set; }
            public string AppId { get; set; }
            public string DisplayName { get; set; }
            public string DeviceId { get; set; }
            public string DeviceDisplayName { get; set; }
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
            public int SchemaVersion { get; set; } = 2;
            public string Id { get; set; }
            public string Name { get; set; }
            public string Slug { get; set; }
            public string CreatedAt { get; set; }
            public string UpdatedAt { get; set; }
            public CaptureScope CaptureScope { get; set; }
            public bool ApplyAppsOnly { get; set; }
            public HotkeyData Hotkey { get; set; } = new HotkeyData();
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
        public VolumeProfile CaptureCurrentState(string profileName, DeviceCollectionViewModel collection, CaptureScope captureScope = CaptureScope.CurrentDevice)
        {
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            var profile = new VolumeProfile
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = profileName,
                Slug = ToSlug(profileName),
                CreatedAt = now,
                UpdatedAt = now,
                CaptureScope = captureScope,
                Devices = new List<DeviceVolumeEntry>()
            };

            var devices = captureScope == CaptureScope.AllDevices
                ? collection.AllDevices
                : collection.AllDevices.Where(d => d.Id == collection.Default?.Id).DefaultIfEmpty(collection.Default).Where(d => d != null);

            foreach (var device in devices)
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
                        DeviceId = device.Id,
                        DeviceDisplayName = device.DisplayName,
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
        public ApplyProfileResult ApplyProfile(VolumeProfile profile, DeviceCollectionViewModel collection, IAudioDeviceManagerWindowsAudio deviceManager = null)
        {
            var result = new ApplyProfileResult { Name = profile?.Name };
            if (profile == null || collection == null) return result;

            foreach (var savedDevice in profile.Devices)
            {
                // Match device by ID first, then by display name
                var device = collection.AllDevices.FirstOrDefault(d => d.Id == savedDevice.DeviceId)
                          ?? collection.AllDevices.FirstOrDefault(d => d.DisplayName == savedDevice.DisplayName);

                if (device == null)
                {
                    var warning = $"Device not found: {savedDevice.DisplayName}";
                    result.Warnings.Add(warning);
                    Trace.WriteLine($"VolumeProfileService: {warning}");
                    continue;
                }

                // Suppress undo recording for bulk profile restore
                App.UndoService.BeginUndoRedo();

                if (!profile.ApplyAppsOnly)
                {
                    device.Volume = savedDevice.Volume;
                    device.IsMuted = savedDevice.IsMuted;
                    result.DevicesApplied++;
                }

                foreach (var savedApp in savedDevice.Apps)
                {
                    var routed = 0;
                    if (deviceManager != null)
                    {
                        routed = TryRouteApp(savedApp, device, collection, deviceManager);
                        result.AppsRouted += routed;
                    }

                    if (ApplyAppState(savedApp, collection))
                    {
                        result.AppsApplied++;
                        if (routed > 0)
                        {
                            ApplyAppStateAfterRoutingSettles(savedApp, collection);
                        }
                    }
                    else
                    {
                        result.AppsMissing++;
                        var warning = $"App not found: {savedApp.DisplayName} ({savedApp.ExeName})";
                        result.Warnings.Add(warning);
                        Trace.WriteLine($"VolumeProfileService: {warning}");
                    }
                }

                App.UndoService.EndUndoRedo();
            }

            Trace.WriteLine($"VolumeProfileService: Applied profile '{profile.Name}'");
            return result;
        }

        private static bool ApplyAppState(AppVolumeEntry savedApp, DeviceCollectionViewModel collection)
        {
            var matches = collection.AllDevices
                .SelectMany(d => d.Apps)
                .Where(a =>
                    (!string.IsNullOrEmpty(savedApp.ExeName) && string.Equals(a.ExeName, savedApp.ExeName, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(savedApp.AppId) && string.Equals(a.AppId, savedApp.AppId, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var app in matches)
            {
                app.Volume = savedApp.Volume;
                app.IsMuted = savedApp.IsMuted;
            }

            return matches.Count > 0;
        }

        private static void ApplyAppStateAfterRoutingSettles(AppVolumeEntry savedApp, DeviceCollectionViewModel collection)
        {
            var remainingTicks = 3;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += (s, e) =>
            {
                ApplyAppState(savedApp, collection);
                remainingTicks--;
                if (remainingTicks <= 0)
                {
                    timer.Stop();
                }
            };
            timer.Start();
        }

        public VolumeProfile FindProfile(string nameOrSlug)
        {
            if (string.IsNullOrWhiteSpace(nameOrSlug)) return null;
            var slug = ToSlug(nameOrSlug);
            return Profiles.FirstOrDefault(p => string.Equals(GetSlug(p), slug, StringComparison.OrdinalIgnoreCase))
                ?? Profiles.FirstOrDefault(p => string.Equals(p.Name, nameOrSlug, StringComparison.OrdinalIgnoreCase));
        }

        public void SaveProfile(VolumeProfile profile)
        {
            // Replace existing profile with same name, or add new
            var existing = Profiles.FirstOrDefault(p => p.Name == profile.Name);
            if (existing != null)
            {
                var idx = Profiles.IndexOf(existing);
                profile.Id = string.IsNullOrWhiteSpace(existing.Id) ? profile.Id : existing.Id;
                profile.CreatedAt = string.IsNullOrWhiteSpace(existing.CreatedAt) ? profile.CreatedAt : existing.CreatedAt;
                profile.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
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
                    NormalizeProfile(profile);
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
                    NormalizeProfile(profile);
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

        public static string ToSlug(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var chars = value.Trim().ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '-')
                .ToArray();
            var slug = new string(chars);
            while (slug.Contains("--")) slug = slug.Replace("--", "-");
            return slug.Trim('-');
        }

        private static string GetSlug(VolumeProfile profile)
        {
            if (profile == null) return string.Empty;
            return string.IsNullOrWhiteSpace(profile.Slug) ? ToSlug(profile.Name) : profile.Slug;
        }

        private static void NormalizeProfile(VolumeProfile profile)
        {
            if (profile == null) return;
            if (profile.SchemaVersion <= 0) profile.SchemaVersion = 1;
            if (string.IsNullOrWhiteSpace(profile.Id)) profile.Id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(profile.Slug)) profile.Slug = ToSlug(profile.Name);
            if (string.IsNullOrWhiteSpace(profile.UpdatedAt)) profile.UpdatedAt = profile.CreatedAt;
            if (profile.Hotkey == null) profile.Hotkey = new HotkeyData();
            if (profile.Devices == null) profile.Devices = new List<DeviceVolumeEntry>();
            foreach (var device in profile.Devices)
            {
                if (device.Apps == null) device.Apps = new List<AppVolumeEntry>();
                foreach (var app in device.Apps)
                {
                    if (string.IsNullOrWhiteSpace(app.DeviceId)) app.DeviceId = device.DeviceId;
                    if (string.IsNullOrWhiteSpace(app.DeviceDisplayName)) app.DeviceDisplayName = device.DisplayName;
                }
            }
        }

        private static int TryRouteApp(AppVolumeEntry savedApp, DeviceViewModel targetDevice, DeviceCollectionViewModel collection, IAudioDeviceManagerWindowsAudio deviceManager)
        {
            if (savedApp == null || targetDevice == null || collection == null || deviceManager == null)
            {
                return 0;
            }

            var routed = 0;
            foreach (var device in collection.AllDevices)
            {
                foreach (var app in device.Apps)
                {
                    if ((!string.IsNullOrWhiteSpace(savedApp.ExeName) && string.Equals(app.ExeName, savedApp.ExeName, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(savedApp.AppId) && string.Equals(app.AppId, savedApp.AppId, StringComparison.OrdinalIgnoreCase)))
                    {
                        try
                        {
                            deviceManager.SetDefaultEndPoint(targetDevice.Id, app.ProcessId);
                            routed++;
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"VolumeProfileService: Failed to route {savedApp.DisplayName} - {ex.Message}");
                        }
                    }
                }
            }
            return routed;
        }
    }
}
