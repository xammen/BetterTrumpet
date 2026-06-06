using EarTrumpet.DataModel;
using EarTrumpet.DataModel.Storage;
using EarTrumpet.Interop.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static EarTrumpet.Interop.User32;

namespace EarTrumpet
{
    /// <summary>
    /// Visual style for the peak meter display.
    /// Classic uses the original solid Border bars.
    /// Unicode styles render text characters for a retro/artistic look.
    /// </summary>
    public enum PeakMeterStyle
    {
        Classic = 0,   // Original solid bars (Border)
        Dotted = 1,    // Braille dots: ⣿⣿⣿⣀⣀⣀
        Blocks = 2,    // Block elements: ▓▓▓▒▒░░
        Bars = 3,      // Thin bars: ┃┃┃┃╎╎╎
        Wave = 4,      // Wavy: ≋≋≋≋⋯⋯⋯
    }

    public class AppSettings
    {
        public event EventHandler<bool> UseLegacyIconChanged;
        public event Action FlyoutHotkeyTyped;
        public event Action MixerHotkeyTyped;
        public event Action SettingsHotkeyTyped;
        public event Action AbsoluteVolumeUpHotkeyTyped;
        public event Action AbsoluteVolumeDownHotkeyTyped;
        public event Action SwitchDeviceHotkeyTyped;
        public event Action<string> QuickTrumpetPresetHotkeyTyped;
        public event Action CustomSliderColorsChanged;
        public event Action HiddenAppsChanged;
        public event Action HiddenDevicesChanged;

        private ISettingsBag _settings = StorageFactory.GetSettings();
        private const string HiddenAppEntriesJsonKey = "HiddenAppEntriesJson";
        private const string HiddenDeviceEntriesJsonKey = "HiddenDeviceEntriesJson";
        private readonly object _hiddenAppsSync = new object();
        private readonly object _hiddenDevicesSync = new object();
        private bool _hiddenAppsLoaded;
        private bool _hiddenDevicesLoaded;
        private bool _hotkeyPressHandlerRegistered;
        private DateTime _lastQuickTrumpetHotkeyAt = DateTime.MinValue;
        private string _lastQuickTrumpetHotkey;
        private List<HiddenAppEntry> _hiddenAppEntries = new List<HiddenAppEntry>();
        private List<HiddenDeviceEntry> _hiddenDeviceEntries = new List<HiddenDeviceEntry>();
        private List<HotkeyData> _quickTrumpetHotkeys = new List<HotkeyData>();

        public class HiddenAppEntry
        {
            public string DeviceId { get; set; }
            public string AppId { get; set; }
            public string ExeName { get; set; }
            public string DisplayName { get; set; }
            public DateTime HiddenAtUtc { get; set; }
        }

        public class HiddenDeviceEntry
        {
            public string DeviceId { get; set; }
            public string DisplayName { get; set; }
            public DateTime HiddenAtUtc { get; set; }
        }

        /// <summary>
        /// Safely parses a color string from settings, returning fallback on failure.
        /// Deduplicates the 7+ ColorConverter.ConvertFromString patterns.
        /// </summary>
        private System.Windows.Media.Color ParseColorSetting(string key, System.Windows.Media.Color fallback = default)
        {
            var colorStr = _settings.Get(key, "");
            if (string.IsNullOrEmpty(colorStr))
                return fallback == default ? System.Windows.Media.Colors.Transparent : fallback;
            try { return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr); }
            catch { return fallback == default ? System.Windows.Media.Colors.Transparent : fallback; }
        }

        public void RegisterHotkeys()
        {
            HotkeyManager.Current.Register(FlyoutHotkey);
            HotkeyManager.Current.Register(MixerHotkey);
            HotkeyManager.Current.Register(SettingsHotkey);
            HotkeyManager.Current.Register(AbsoluteVolumeUpHotkey);
            HotkeyManager.Current.Register(AbsoluteVolumeDownHotkey);
            HotkeyManager.Current.Register(SwitchDeviceHotkey);
            RegisterQuickTrumpetHotkeys();

            if (_hotkeyPressHandlerRegistered)
            {
                return;
            }

            _hotkeyPressHandlerRegistered = true;
            HotkeyManager.Current.KeyPressed += (hotkey) =>
            {
                if (hotkey.Equals(FlyoutHotkey))
                {
                    Trace.WriteLine("AppSettings FlyoutHotkeyTyped");
                    FlyoutHotkeyTyped?.Invoke();
                }
                else if (hotkey.Equals(SettingsHotkey))
                {
                    Trace.WriteLine("AppSettings SettingsHotkeyTyped");
                    SettingsHotkeyTyped?.Invoke();
                }
                else if (hotkey.Equals(MixerHotkey))
                {
                    Trace.WriteLine("AppSettings MixerHotkeyTyped");
                    MixerHotkeyTyped?.Invoke();
                }
                else if (hotkey.Equals(AbsoluteVolumeUpHotkey))
                {
                    Trace.WriteLine("AppSettings AbsoluteVolumeUpHotkeyTyped");
                    AbsoluteVolumeUpHotkeyTyped?.Invoke();
                }
                else if (hotkey.Equals(AbsoluteVolumeDownHotkey))
                {
                    Trace.WriteLine("AppSettings AbsoluteVolumeDownHotkeyTyped");
                    AbsoluteVolumeDownHotkeyTyped?.Invoke();
                }
                else if (hotkey.Equals(SwitchDeviceHotkey))
                {
                    Trace.WriteLine("AppSettings SwitchDeviceHotkeyTyped");
                    SwitchDeviceHotkeyTyped?.Invoke();
                }
                else
                {
                    var profile = GetQuickTrumpetHotkeyProfiles().FirstOrDefault(p => p.Hotkey != null && p.Hotkey.Equals(hotkey));
                    if (profile != null)
                    {
                        var profileKey = string.IsNullOrWhiteSpace(profile.Slug) ? profile.Name : profile.Slug;
                        var now = DateTime.UtcNow;
                        if (string.Equals(_lastQuickTrumpetHotkey, profileKey, StringComparison.OrdinalIgnoreCase) &&
                            (now - _lastQuickTrumpetHotkeyAt).TotalMilliseconds < 700)
                        {
                            return;
                        }

                        _lastQuickTrumpetHotkey = profileKey;
                        _lastQuickTrumpetHotkeyAt = now;
                        Trace.WriteLine($"AppSettings QuickTrumpetPresetHotkeyTyped {profile.Name}");
                        QuickTrumpetPresetHotkeyTyped?.Invoke(profileKey);
                    }
                }
            };
        }

        public void RegisterQuickTrumpetHotkeys()
        {
            foreach (var hotkey in _quickTrumpetHotkeys)
            {
                HotkeyManager.Current.Unregister(hotkey);
            }

            _quickTrumpetHotkeys = GetQuickTrumpetHotkeyProfiles()
                .Select(p => p.Hotkey)
                .Where(h => h != null && !h.IsEmpty)
                .ToList();

            foreach (var hotkey in _quickTrumpetHotkeys)
            {
                HotkeyManager.Current.Register(hotkey);
            }
        }

        private List<VolumeProfileService.VolumeProfile> GetQuickTrumpetHotkeyProfiles()
        {
            try
            {
                var json = VolumeProfilesJson;
                if (string.IsNullOrWhiteSpace(json) || json == "[]") return new List<VolumeProfileService.VolumeProfile>();
                return Newtonsoft.Json.JsonConvert.DeserializeObject<List<VolumeProfileService.VolumeProfile>>(json) ?? new List<VolumeProfileService.VolumeProfile>();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"AppSettings GetQuickTrumpetHotkeyProfiles failed: {ex.Message}");
                return new List<VolumeProfileService.VolumeProfile>();
            }
        }

        public HotkeyData FlyoutHotkey
        {
            get => _settings.Get("Hotkey", new HotkeyData { });
            set
            {
                HotkeyManager.Current.Unregister(FlyoutHotkey);
                _settings.Set("Hotkey", value);
                HotkeyManager.Current.Register(FlyoutHotkey);
            }
        }

        public HotkeyData MixerHotkey
        {
            get => _settings.Get("MixerHotkey", new HotkeyData { });
            set
            {
                HotkeyManager.Current.Unregister(MixerHotkey);
                _settings.Set("MixerHotkey", value);
                HotkeyManager.Current.Register(MixerHotkey);
            }
        }

        public HotkeyData SettingsHotkey
        {
            get => _settings.Get("SettingsHotkey", new HotkeyData { });
            set
            {
                HotkeyManager.Current.Unregister(SettingsHotkey);
                _settings.Set("SettingsHotkey", value);
                HotkeyManager.Current.Register(SettingsHotkey);
            }
        }

        public HotkeyData AbsoluteVolumeUpHotkey
        {
            get => _settings.Get("AbsoluteVolumeUpHotkey", new HotkeyData { });
            set
            {
                HotkeyManager.Current.Unregister(AbsoluteVolumeUpHotkey);
                _settings.Set("AbsoluteVolumeUpHotkey", value);
                HotkeyManager.Current.Register(AbsoluteVolumeUpHotkey);
            }
        }

        public HotkeyData AbsoluteVolumeDownHotkey
        {
            get => _settings.Get("AbsoluteVolumeDownHotkey", new HotkeyData { });
            set
            {
                HotkeyManager.Current.Unregister(AbsoluteVolumeDownHotkey);
                _settings.Set("AbsoluteVolumeDownHotkey", value);
                HotkeyManager.Current.Register(AbsoluteVolumeDownHotkey);
            }
        }

        public HotkeyData SwitchDeviceHotkey
        {
            get => _settings.Get("SwitchDeviceHotkey", new HotkeyData { });
            set
            {
                HotkeyManager.Current.Unregister(SwitchDeviceHotkey);
                _settings.Set("SwitchDeviceHotkey", value);
                HotkeyManager.Current.Register(SwitchDeviceHotkey);
            }
        }

        public bool UseLegacyIcon
        {
            get
            {
                // Note: Legacy compat, we used to write string bools.
                var ret = _settings.Get("UseLegacyIcon", "False");
                bool.TryParse(ret, out bool isUseLegacyIcon);
                return isUseLegacyIcon;
            }
            set
            {
                _settings.Set("UseLegacyIcon", value.ToString());
                UseLegacyIconChanged?.Invoke(null, UseLegacyIcon);
            }
        }

        public bool IsExpanded
        {
            get => _settings.Get("IsExpanded", false);
            set => _settings.Set("IsExpanded", value);
        }

        public int HiddenAppsCount
        {
            get
            {
                lock (_hiddenAppsSync)
                {
                    EnsureHiddenAppsLoaded();
                    return _hiddenAppEntries.Count;
                }
            }
        }

        public bool IsAppHiddenForDevice(string deviceId, string appId, string exeName)
        {
            var normalizedDeviceId = NormalizeHiddenKeyValue(deviceId);
            if (string.IsNullOrEmpty(normalizedDeviceId))
            {
                return false;
            }

            var normalizedAppId = NormalizeHiddenKeyValue(appId);
            var normalizedExeName = NormalizeHiddenKeyValue(exeName);

            if (string.IsNullOrEmpty(normalizedAppId) && string.IsNullOrEmpty(normalizedExeName))
            {
                return false;
            }

            lock (_hiddenAppsSync)
            {
                EnsureHiddenAppsLoaded();
                return _hiddenAppEntries.Any(entry =>
                    entry.DeviceId == normalizedDeviceId &&
                    ((normalizedAppId.Length > 0 && entry.AppId == normalizedAppId) ||
                     (normalizedExeName.Length > 0 && entry.ExeName == normalizedExeName)));
            }
        }

        public int GetHiddenAppCountForDevice(string deviceId)
        {
            var normalizedDeviceId = NormalizeHiddenKeyValue(deviceId);
            if (string.IsNullOrEmpty(normalizedDeviceId))
            {
                return 0;
            }

            lock (_hiddenAppsSync)
            {
                EnsureHiddenAppsLoaded();
                return _hiddenAppEntries.Count(entry => entry.DeviceId == normalizedDeviceId);
            }
        }

        public List<HiddenAppEntry> GetHiddenAppsForDevice(string deviceId)
        {
            var normalizedDeviceId = NormalizeHiddenKeyValue(deviceId);
            if (string.IsNullOrEmpty(normalizedDeviceId))
            {
                return new List<HiddenAppEntry>();
            }

            lock (_hiddenAppsSync)
            {
                EnsureHiddenAppsLoaded();
                return _hiddenAppEntries
                    .Where(entry => entry.DeviceId == normalizedDeviceId)
                    .OrderBy(entry => entry.DisplayName)
                    .ThenBy(entry => entry.ExeName)
                    .ThenBy(entry => entry.AppId)
                    .Select(entry => new HiddenAppEntry
                    {
                        DeviceId = entry.DeviceId,
                        AppId = entry.AppId,
                        ExeName = entry.ExeName,
                        DisplayName = entry.DisplayName,
                        HiddenAtUtc = entry.HiddenAtUtc,
                    })
                    .ToList();
            }
        }

        public List<HiddenAppEntry> GetHiddenApps()
        {
            lock (_hiddenAppsSync)
            {
                EnsureHiddenAppsLoaded();
                return _hiddenAppEntries
                    .OrderBy(entry => entry.DeviceId)
                    .ThenBy(entry => entry.DisplayName)
                    .ThenBy(entry => entry.ExeName)
                    .ThenBy(entry => entry.AppId)
                    .Select(entry => new HiddenAppEntry
                    {
                        DeviceId = entry.DeviceId,
                        AppId = entry.AppId,
                        ExeName = entry.ExeName,
                        DisplayName = entry.DisplayName,
                        HiddenAtUtc = entry.HiddenAtUtc,
                    })
                    .ToList();
            }
        }

        public void HideAppForDevice(string deviceId, string appId, string exeName, string displayName = null)
        {
            var normalizedDeviceId = NormalizeHiddenKeyValue(deviceId);
            var normalizedAppId = NormalizeHiddenKeyValue(appId);
            var normalizedExeName = NormalizeHiddenKeyValue(exeName);
            var safeDisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();

            if (string.IsNullOrEmpty(normalizedDeviceId) || (string.IsNullOrEmpty(normalizedAppId) && string.IsNullOrEmpty(normalizedExeName)))
            {
                return;
            }

            bool changed = false;
            lock (_hiddenAppsSync)
            {
                EnsureHiddenAppsLoaded();

                bool alreadyExists = _hiddenAppEntries.Any(entry =>
                    entry.DeviceId == normalizedDeviceId &&
                    entry.AppId == normalizedAppId &&
                    entry.ExeName == normalizedExeName);

                if (!alreadyExists)
                {
                    _hiddenAppEntries.Add(new HiddenAppEntry
                    {
                        DeviceId = normalizedDeviceId,
                        AppId = normalizedAppId,
                        ExeName = normalizedExeName,
                        DisplayName = safeDisplayName,
                        HiddenAtUtc = DateTime.UtcNow,
                    });

                    SaveHiddenAppsUnsafe();
                    changed = true;
                }
            }

            if (changed)
            {
                HiddenAppsChanged?.Invoke();
            }
        }

        public void UnhideAppsForDevice(string deviceId)
        {
            var normalizedDeviceId = NormalizeHiddenKeyValue(deviceId);
            if (string.IsNullOrEmpty(normalizedDeviceId))
            {
                return;
            }

            bool changed = false;
            lock (_hiddenAppsSync)
            {
                EnsureHiddenAppsLoaded();
                changed = _hiddenAppEntries.RemoveAll(entry => entry.DeviceId == normalizedDeviceId) > 0;
                if (changed)
                {
                    SaveHiddenAppsUnsafe();
                }
            }

            if (changed)
            {
                HiddenAppsChanged?.Invoke();
            }
        }

        public void UnhideAppForDevice(string deviceId, string appId, string exeName)
        {
            var normalizedDeviceId = NormalizeHiddenKeyValue(deviceId);
            var normalizedAppId = NormalizeHiddenKeyValue(appId);
            var normalizedExeName = NormalizeHiddenKeyValue(exeName);

            if (string.IsNullOrEmpty(normalizedDeviceId) || (string.IsNullOrEmpty(normalizedAppId) && string.IsNullOrEmpty(normalizedExeName)))
            {
                return;
            }

            bool changed = false;
            lock (_hiddenAppsSync)
            {
                EnsureHiddenAppsLoaded();
                changed = _hiddenAppEntries.RemoveAll(entry =>
                    entry.DeviceId == normalizedDeviceId &&
                    entry.AppId == normalizedAppId &&
                    entry.ExeName == normalizedExeName) > 0;

                if (changed)
                {
                    SaveHiddenAppsUnsafe();
                }
            }

            if (changed)
            {
                HiddenAppsChanged?.Invoke();
            }
        }

        public void UnhideAllApps()
        {
            bool changed = false;
            lock (_hiddenAppsSync)
            {
                EnsureHiddenAppsLoaded();
                if (_hiddenAppEntries.Count > 0)
                {
                    _hiddenAppEntries.Clear();
                    SaveHiddenAppsUnsafe();
                    changed = true;
                }
            }

            if (changed)
            {
                HiddenAppsChanged?.Invoke();
            }
        }

        private void EnsureHiddenAppsLoaded()
        {
            if (_hiddenAppsLoaded)
            {
                return;
            }

            try
            {
                var json = _settings.Get(HiddenAppEntriesJsonKey, "[]");
                var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<List<HiddenAppEntry>>(json) ?? new List<HiddenAppEntry>();
                _hiddenAppEntries = NormalizeHiddenEntries(loaded);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"AppSettings EnsureHiddenAppsLoaded failed: {ex.Message}");
                _hiddenAppEntries = new List<HiddenAppEntry>();
            }

            _hiddenAppsLoaded = true;
        }

        private void SaveHiddenAppsUnsafe()
        {
            _settings.Set(HiddenAppEntriesJsonKey, Newtonsoft.Json.JsonConvert.SerializeObject(_hiddenAppEntries));
        }

        private List<HiddenAppEntry> NormalizeHiddenEntries(List<HiddenAppEntry> entries)
        {
            var dedup = new HashSet<string>(StringComparer.Ordinal);
            var normalizedEntries = new List<HiddenAppEntry>();

            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                var normalizedDeviceId = NormalizeHiddenKeyValue(entry.DeviceId);
                var normalizedAppId = NormalizeHiddenKeyValue(entry.AppId);
                var normalizedExeName = NormalizeHiddenKeyValue(entry.ExeName);

                if (string.IsNullOrEmpty(normalizedDeviceId) || (string.IsNullOrEmpty(normalizedAppId) && string.IsNullOrEmpty(normalizedExeName)))
                {
                    continue;
                }

                var key = normalizedDeviceId + "|" + normalizedAppId + "|" + normalizedExeName;
                if (!dedup.Add(key))
                {
                    continue;
                }

                normalizedEntries.Add(new HiddenAppEntry
                {
                    DeviceId = normalizedDeviceId,
                    AppId = normalizedAppId,
                    ExeName = normalizedExeName,
                    DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? string.Empty : entry.DisplayName.Trim(),
                    HiddenAtUtc = entry.HiddenAtUtc,
                });
            }

            return normalizedEntries;
        }

        private static string NormalizeHiddenKeyValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        // Hidden Devices Methods
        public int HiddenDevicesCount
        {
            get
            {
                lock (_hiddenDevicesSync)
                {
                    EnsureHiddenDevicesLoaded();
                    return _hiddenDeviceEntries.Count;
                }
            }
        }

        public bool IsDeviceHidden(string deviceId)
        {
            var normalizedDeviceId = NormalizeHiddenKeyValue(deviceId);
            if (string.IsNullOrEmpty(normalizedDeviceId))
            {
                return false;
            }

            lock (_hiddenDevicesSync)
            {
                EnsureHiddenDevicesLoaded();
                return _hiddenDeviceEntries.Any(entry => entry.DeviceId == normalizedDeviceId);
            }
        }

        public List<HiddenDeviceEntry> GetHiddenDevices()
        {
            lock (_hiddenDevicesSync)
            {
                EnsureHiddenDevicesLoaded();
                return _hiddenDeviceEntries
                    .OrderBy(entry => entry.DisplayName)
                    .ThenBy(entry => entry.DeviceId)
                    .Select(entry => new HiddenDeviceEntry
                    {
                        DeviceId = entry.DeviceId,
                        DisplayName = entry.DisplayName,
                        HiddenAtUtc = entry.HiddenAtUtc,
                    })
                    .ToList();
            }
        }

        public void HideDevice(string deviceId, string displayName = null)
        {
            var normalizedDeviceId = NormalizeHiddenKeyValue(deviceId);
            var safeDisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();

            if (string.IsNullOrEmpty(normalizedDeviceId))
            {
                return;
            }

            bool changed = false;
            lock (_hiddenDevicesSync)
            {
                EnsureHiddenDevicesLoaded();

                bool alreadyExists = _hiddenDeviceEntries.Any(entry => entry.DeviceId == normalizedDeviceId);

                if (!alreadyExists)
                {
                    _hiddenDeviceEntries.Add(new HiddenDeviceEntry
                    {
                        DeviceId = normalizedDeviceId,
                        DisplayName = safeDisplayName,
                        HiddenAtUtc = DateTime.UtcNow,
                    });

                    SaveHiddenDevicesUnsafe();
                    changed = true;
                }
            }

            if (changed)
            {
                HiddenDevicesChanged?.Invoke();
            }
        }

        public void UnhideDevice(string deviceId)
        {
            var normalizedDeviceId = NormalizeHiddenKeyValue(deviceId);
            if (string.IsNullOrEmpty(normalizedDeviceId))
            {
                return;
            }

            bool changed = false;
            lock (_hiddenDevicesSync)
            {
                EnsureHiddenDevicesLoaded();
                changed = _hiddenDeviceEntries.RemoveAll(entry => entry.DeviceId == normalizedDeviceId) > 0;

                if (changed)
                {
                    SaveHiddenDevicesUnsafe();
                }
            }

            if (changed)
            {
                HiddenDevicesChanged?.Invoke();
            }
        }

        public void UnhideAllDevices()
        {
            bool changed = false;
            lock (_hiddenDevicesSync)
            {
                EnsureHiddenDevicesLoaded();
                if (_hiddenDeviceEntries.Count > 0)
                {
                    _hiddenDeviceEntries.Clear();
                    SaveHiddenDevicesUnsafe();
                    changed = true;
                }
            }

            if (changed)
            {
                HiddenDevicesChanged?.Invoke();
            }
        }

        private void EnsureHiddenDevicesLoaded()
        {
            if (_hiddenDevicesLoaded)
            {
                return;
            }

            try
            {
                var json = _settings.Get(HiddenDeviceEntriesJsonKey, "[]");
                var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<List<HiddenDeviceEntry>>(json) ?? new List<HiddenDeviceEntry>();
                _hiddenDeviceEntries = NormalizeHiddenDeviceEntries(loaded);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"AppSettings EnsureHiddenDevicesLoaded failed: {ex.Message}");
                _hiddenDeviceEntries = new List<HiddenDeviceEntry>();
            }

            _hiddenDevicesLoaded = true;
        }

        private void SaveHiddenDevicesUnsafe()
        {
            _settings.Set(HiddenDeviceEntriesJsonKey, Newtonsoft.Json.JsonConvert.SerializeObject(_hiddenDeviceEntries));
        }

        private List<HiddenDeviceEntry> NormalizeHiddenDeviceEntries(List<HiddenDeviceEntry> entries)
        {
            var dedup = new HashSet<string>(StringComparer.Ordinal);
            var normalizedEntries = new List<HiddenDeviceEntry>();

            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                var normalizedDeviceId = NormalizeHiddenKeyValue(entry.DeviceId);

                if (string.IsNullOrEmpty(normalizedDeviceId))
                {
                    continue;
                }

                if (!dedup.Add(normalizedDeviceId))
                {
                    continue;
                }

                normalizedEntries.Add(new HiddenDeviceEntry
                {
                    DeviceId = normalizedDeviceId,
                    DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? string.Empty : entry.DisplayName.Trim(),
                    HiddenAtUtc = entry.HiddenAtUtc,
                });
            }

            return normalizedEntries;
        }

        public bool UseScrollWheelInTray
        {
            get => _settings.Get("UseScrollWheelInTray", true);
            set => _settings.Set("UseScrollWheelInTray", value);
        }

        public bool UseGlobalMouseWheelHook
        {
            get => _settings.Get("UseGlobalMouseWheelHook", false);
            set => _settings.Set("UseGlobalMouseWheelHook", value);
        }

        public event Action AppTooltipsChanged;

        public bool ShowAppTooltips
        {
            get => _settings.Get("ShowAppTooltips", true);
            set
            {
                _settings.Set("ShowAppTooltips", value);
                AppTooltipsChanged?.Invoke();
            }
        }

        public bool HasShownFirstRun
        {
            get => _settings.HasKey("hasShownFirstRun");
            set => _settings.Set("hasShownFirstRun", value);
        }

        public event Action TelemetryConsentChanged;

        public bool IsTelemetryEnabled
        {
            get
            {
                return _settings.Get("IsTelemetryEnabled", IsTelemetryEnabledByDefault());
            }
            set
            {
                _settings.Set("IsTelemetryEnabled", value);
                TelemetryConsentChanged?.Invoke();
            }
        }

        public bool UseLogarithmicVolume
        {
            get => _settings.Get("UseLogarithmicVolume", false);
            set => _settings.Set("UseLogarithmicVolume", value);
        }

        public bool UseSmoothVolumeAnimation
        {
            get => _settings.Get("UseSmoothVolumeAnimation", true);
            set => _settings.Set("UseSmoothVolumeAnimation", value);
        }

        // Volume animation speed: 0.02 (very slow) to 0.5 (fast). Default 0.08
        public double VolumeAnimationSpeed
        {
            get => _settings.Get("VolumeAnimationSpeed", 0.08);
            set => _settings.Set("VolumeAnimationSpeed", value);
        }

        // Peak meter FPS: 20 (performance), 30 (balanced), or 60 (smooth). Default 30
        // Note: 30fps is a good balance between smoothness and CPU usage
        // Most users won't notice the difference from 60fps for peak meters
        public int PeakMeterFps
        {
            get => _settings.Get("PeakMeterFps", 30);
            set => _settings.Set("PeakMeterFps", value);
        }

        // Eco Mode: Reduces CPU usage by limiting animations and refresh rates
        public bool EcoMode
        {
            get => _settings.Get("EcoMode", false);
            set
            {
                _settings.Set("EcoMode", value);
                EcoModeChanged?.Invoke();
            }
        }

        // Auto Eco Mode: Automatically enable eco mode when on battery power
        public bool AutoEcoMode
        {
            get => _settings.Get("AutoEcoMode", true);
            set => _settings.Set("AutoEcoMode", value);
        }

        // Event fired when eco mode changes
        public event Action EcoModeChanged;

        // Helper to get effective eco mode (considering auto and battery)
        public bool IsEffectiveEcoMode
        {
            get
            {
                if (EcoMode) return true;
                if (AutoEcoMode && IsOnBatteryPower) return true;
                return false;
            }
        }

        // Check if running on battery power
        public bool IsOnBatteryPower
        {
            get
            {
                try
                {
                    var status = System.Windows.Forms.SystemInformation.PowerStatus;
                    return status.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Offline;
                }
                catch
                {
                    return false;
                }
            }
        }

        // Get effective FPS based on eco mode
        public int EffectivePeakMeterFps
        {
            get
            {
                if (IsEffectiveEcoMode) return 20;
                return PeakMeterFps;
            }
        }

        // Batch mode: suppress individual change events during animated transitions
        private bool _batchMode;
        public void BeginBatch() { _batchMode = true; }
        public void EndBatch()
        {
            _batchMode = false;
            CustomSliderColorsChanged?.Invoke();
        }

        // Custom slider colors
        public bool UseCustomSliderColors
        {
            get => _settings.Get("UseCustomSliderColors", false);
            set
            {
                _settings.Set("UseCustomSliderColors", value);
                CustomSliderColorsChanged?.Invoke();
            }
        }

        public System.Windows.Media.Color SliderThumbColor
        {
            get => ParseColorSetting("SliderThumbColor");
            set
            {
                _settings.Set("SliderThumbColor", value.ToString());
                if (!_batchMode) CustomSliderColorsChanged?.Invoke();
            }
        }

        public System.Windows.Media.Color SliderTrackFillColor
        {
            get => ParseColorSetting("SliderTrackFillColor");
            set
            {
                _settings.Set("SliderTrackFillColor", value.ToString());
                if (!_batchMode) CustomSliderColorsChanged?.Invoke();
            }
        }

        public System.Windows.Media.Color SliderTrackBackgroundColor
        {
            get => ParseColorSetting("SliderTrackBackgroundColor");
            set
            {
                _settings.Set("SliderTrackBackgroundColor", value.ToString());
                if (!_batchMode) CustomSliderColorsChanged?.Invoke();
            }
        }

        public System.Windows.Media.Color PeakMeterColor
        {
            get => ParseColorSetting("PeakMeterColor");
            set
            {
                _settings.Set("PeakMeterColor", value.ToString());
                if (!_batchMode) CustomSliderColorsChanged?.Invoke();
            }
        }

        // Peak meter visual style: Classic (solid bars), Dotted, Blocks, Bars, Wave
        public PeakMeterStyle PeakMeterStyle
        {
            get
            {
                var val = _settings.Get("PeakMeterStyle", 0);
                return (PeakMeterStyle)System.Math.Min(val, 4);
            }
            set
            {
                _settings.Set("PeakMeterStyle", (int)value);
                PeakMeterStyleChanged?.Invoke();
                if (!_batchMode) CustomSliderColorsChanged?.Invoke();
            }
        }

        // Event fired when peak meter style changes (triggers visibility toggle in VolumeSlider)
        public event Action PeakMeterStyleChanged;

        // Custom saved themes (JSON array)
        public string CustomThemesJson
        {
            get => _settings.Get("CustomThemesJson", "[]");
            set
            {
                _settings.Set("CustomThemesJson", value);
                CustomSliderColorsChanged?.Invoke();
            }
        }

        // Active theme name (to restore selected state)
        public string ActiveThemeName
        {
            get => _settings.Get("ActiveThemeName", "");
            set => _settings.Set("ActiveThemeName", value);
        }

        // Last seen version (for changelog display)
        public string LastSeenVersion
        {
            get => _settings.Get("LastSeenVersion", "");
            set => _settings.Set("LastSeenVersion", value);
        }

        // Extended theme colors (Window Background, Text, Accent Glow)
        public System.Windows.Media.Color WindowBackgroundColor
        {
            get => ParseColorSetting("WindowBackgroundColor");
            set
            {
                _settings.Set("WindowBackgroundColor", value.ToString());
                if (!_batchMode) CustomSliderColorsChanged?.Invoke();
            }
        }

        public System.Windows.Media.Color TextColor
        {
            get => ParseColorSetting("TextColor");
            set
            {
                _settings.Set("TextColor", value.ToString());
                if (!_batchMode) CustomSliderColorsChanged?.Invoke();
            }
        }

        public System.Windows.Media.Color AccentGlowColor
        {
            get => ParseColorSetting("AccentGlowColor");
            set
            {
                _settings.Set("AccentGlowColor", value.ToString());
                if (!_batchMode) CustomSliderColorsChanged?.Invoke();
            }
        }

        // Volume profiles JSON storage
        public string VolumeProfilesJson
        {
            get => _settings.Get("VolumeProfilesJson", "[]");
            set
            {
                _settings.Set("VolumeProfilesJson", value);
                RegisterQuickTrumpetHotkeys();
            }
        }

        public bool ShowQuickTrumpetConfirmation
        {
            get => _settings.Get("ShowQuickTrumpetConfirmation", true);
            set => _settings.Set("ShowQuickTrumpetConfirmation", value);
        }

        // Dynamic album art theme mode
        public bool UseDynamicAlbumArtTheme
        {
            get => _settings.Get("UseDynamicAlbumArtTheme", false);
            set
            {
                _settings.Set("UseDynamicAlbumArtTheme", value);
                CustomSliderColorsChanged?.Invoke();
            }
        }

        public WINDOWPLACEMENT? FullMixerWindowPlacement
        {
            get => _settings.Get("FullMixerWindowPlacement", default(WINDOWPLACEMENT?));
            set => _settings.Set("FullMixerWindowPlacement", value);
        }

        public WINDOWPLACEMENT? SettingsWindowPlacement
        {
            get => _settings.Get("SettingsWindowPlacement", default(WINDOWPLACEMENT?));
            set => _settings.Set("SettingsWindowPlacement", value);
        }

        // Media Popup Settings
        public event Action MediaPopupSettingsChanged;

        public bool MediaPopupEnabled
        {
            get => _settings.Get("MediaPopupEnabled", true);
            set
            {
                _settings.Set("MediaPopupEnabled", value);
                MediaPopupSettingsChanged?.Invoke();
            }
        }

        // Hover delay in seconds (0.5 to 5 seconds)
        public double MediaPopupHoverDelay
        {
            get => _settings.Get("MediaPopupHoverDelay", 2.0);
            set
            {
                _settings.Set("MediaPopupHoverDelay", Math.Max(0.5, Math.Min(5.0, value)));
                MediaPopupSettingsChanged?.Invoke();
            }
        }

        // Background blur radius (0 to 30)
        public double MediaPopupBlurRadius
        {
            get => _settings.Get("MediaPopupBlurRadius", 15.0);
            set
            {
                _settings.Set("MediaPopupBlurRadius", Math.Max(0, Math.Min(30, value)));
                MediaPopupSettingsChanged?.Invoke();
            }
        }

        // Only show popup when media is playing
        public bool MediaPopupShowOnlyWhenPlaying
        {
            get => _settings.Get("MediaPopupShowOnlyWhenPlaying", false);
            set
            {
                _settings.Set("MediaPopupShowOnlyWhenPlaying", value);
                MediaPopupSettingsChanged?.Invoke();
            }
        }

        // Remember expanded state between sessions
        public bool MediaPopupRememberExpanded
        {
            get => _settings.Get("MediaPopupRememberExpanded", true);
            set => _settings.Set("MediaPopupRememberExpanded", value);
        }

        // Expanded state (stored if RememberExpanded is true)
        public bool MediaPopupIsExpanded
        {
            get => _settings.Get("MediaPopupIsExpanded", false);
            set => _settings.Set("MediaPopupIsExpanded", value);
        }

        // Auto-check for updates
        public bool AutoCheckForUpdates
        {
            get => _settings.Get("AutoCheckForUpdates", true);
            set => _settings.Set("AutoCheckForUpdates", value);
        }

        /// <summary>
        /// Which updates to notify about: All (patch+minor+major), MinorAndMajor, MajorOnly, None.
        /// </summary>
        public UpdateChannel UpdateNotifyChannel
        {
            get
            {
                var val = _settings.Get("UpdateNotifyChannel", (int)UpdateChannel.All);
                return Enum.IsDefined(typeof(UpdateChannel), val) ? (UpdateChannel)val : UpdateChannel.All;
            }
            set => _settings.Set("UpdateNotifyChannel", (int)value);
        }

        // Run at Windows startup
        private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupValueName = "BetterTrumpet";

        public bool RunAtStartup
        {
            get
            {
                try
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false))
                    {
                        return key?.GetValue(StartupValueName) != null;
                    }
                }
                catch
                {
                    return false;
                }
            }
            set
            {
                try
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true))
                    {
                        if (value)
                        {
                            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                            key?.SetValue(StartupValueName, $"\"{exePath}\"");
                        }
                        else
                        {
                            key?.DeleteValue(StartupValueName, false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Failed to set RunAtStartup: {ex.Message}");
                }
            }
        }

        private bool IsTelemetryEnabledByDefault()
        {
            // Discussion on what to include:
            // https://gist.github.com/henrik/1688572
            var europeanUnionRegions = new string[]
            {
                // EU 28
                "AT", // Austria
                "BE", // Belgium
                "BG", // Bulgaria
                "HR", // Croatia
                "CY", // Cyprus
                "CZ", // Czech Republic
                "DK", // Denmark
                "EE", // Estonia
                "FI", // Finland
                "FR", // France
                "DE", // Germany
                "GR", // Greece
                "HU", // Hungary
                "IE", // Ireland, Republic of (EIRE)
                "IT", // Italy
                "LV", // Latvia
                "LT", // Lithuania
                "LU", // Luxembourg
                "MT", // Malta
                "NL", // Netherlands
                "PL", // Poland
                "PT", // Portugal
                "RO", // Romania
                "SK", // Slovakia
                "SI", // Slovenia
                "ES", // Spain
                "SE", // Sweden
                "GB", // United Kingdom (Great Britain)

                // Outermost Regions (OMR)
                "GF", // French Guiana
                "GP", // Guadeloupe
                "MQ", // Martinique
                "ME", // Montenegro
                "YT", // Mayotte
                "RE", // Réunion
                "MF", // Saint Martin

                // Special Cases: Part of EU
                "GI", // Gibraltar
                "AX", // Åland Islands

                // Overseas Countries and Territories (OCT)
                "PM", // Saint Pierre and Miquelon
                "GL", // Greenland
                "BL", // Saint Bartelemey
                "SX", // Sint Maarten
                "AW", // Aruba
                "CW", // Curacao
                "WF", // Wallis and Futuna
                "PF", // French Polynesia
                "NC", // New Caledonia
                "TF", // French Southern Territories
                "AI", // Anguilla
                "BM", // Bermuda
                "IO", // British Indian Ocean Territory
                "VG", // Virgin Islands, British
                "KY", // Cayman Islands
                "FK", // Falkland Islands (Malvinas)
                "MS", // Montserrat
                "PN", // Pitcairn
                "SH", // Saint Helena
                "GS", // South Georgia and the South Sandwich Islands
                "TC", // Turks and Caicos Islands

                // Microstates
                "AD", // Andorra
                "LI", // Liechtenstein
                "MC", // Monaco
                "SM", // San Marino
                "VA", // Vatican City

                // Other
                "JE", // Jersey
                "GG", // Guernsey
            };
            var region = new Windows.Globalization.GeographicRegion();
            return !europeanUnionRegions.Contains(region.CodeTwoLetter);
        }
    }
}
