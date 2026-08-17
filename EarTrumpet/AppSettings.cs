using EarTrumpet.DataModel;
using EarTrumpet.DataModel.Storage;
using EarTrumpet.Interop.Helpers;
using EarTrumpet.Logic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
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
        public event Action AppRulesChanged;
        public event Action FolderVolumeRulesChanged;

        private ISettingsBag _settings = StorageFactory.GetSettings();
        private const string HiddenAppEntriesJsonKey = "HiddenAppEntriesJson";
        private const string HiddenDeviceEntriesJsonKey = "HiddenDeviceEntriesJson";
        private const string AppRuleEntriesJsonKey = "AppRuleEntriesJson";
        private const string FolderVolumeRuleEntriesJsonKey = "FolderVolumeRuleEntriesJson";
        // Pre-3.2.1 key, still read once to migrate hard mutes into AppRuleEntriesJson.
        private const string LegacyHardMutedAppEntriesJsonKey = "HardMutedAppEntriesJson";
        private readonly object _hiddenAppsSync = new object();
        private readonly object _hiddenDevicesSync = new object();
        private readonly object _appRulesSync = new object();
        private readonly object _folderVolumeRulesSync = new object();
        private bool _hiddenAppsLoaded;
        private bool _hiddenDevicesLoaded;
        private bool _appRulesLoaded;
        private bool _folderVolumeRulesLoaded;
        private bool _hotkeyPressHandlerRegistered;
        private DateTime _lastQuickTrumpetHotkeyAt = DateTime.MinValue;
        private string _lastQuickTrumpetHotkey;
        private List<HiddenAppEntry> _hiddenAppEntries = new List<HiddenAppEntry>();
        private List<HiddenDeviceEntry> _hiddenDeviceEntries = new List<HiddenDeviceEntry>();
        private List<AppRuleEntry> _appRuleEntries = new List<AppRuleEntry>();
        private List<FolderVolumeRuleEntry> _folderVolumeRules = new List<FolderVolumeRuleEntry>();
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
        /// How a per-app volume rule behaves. Launch and Lock are mutually exclusive:
        /// a rule carries one mode and one volume, never "launch at X but lock at Y".
        /// </summary>
        public enum VolumeRuleMode
        {
            None = 0,
            /// <summary>Set the volume once when a new instance of the app appears, then leave it alone.</summary>
            Launch = 1,
            /// <summary>Hold the volume at the rule value, reverting any change from the app or the OS.</summary>
            Lock = 2,
        }

        /// <summary>
        /// A persistent per-app rule. Hard mute and the volume rule are independent axes
        /// of the same entry, so the settings list can show one row per app.
        /// </summary>
        public class AppRuleEntry
        {
            public string ExeName { get; set; }
            public string DisplayName { get; set; }
            public string IconPath { get; set; }
            public bool IsDesktopApp { get; set; }
            public bool HardMuted { get; set; }
            public VolumeRuleMode VolumeMode { get; set; }
            public int VolumePercent { get; set; }
            public DateTime CreatedAtUtc { get; set; }

            // Derived, so they must not be persisted: serializing them bloats every
            // entry and invites drift if the rules behind them ever change.
            [Newtonsoft.Json.JsonIgnore]
            public bool HasVolumeRule => VolumeMode != VolumeRuleMode.None;

            [Newtonsoft.Json.JsonIgnore]
            public bool IsEmpty => !HardMuted && VolumeMode == VolumeRuleMode.None;
        }

        // Legacy shape, only deserialized during the one-shot migration.
        private class LegacyHardMutedAppEntry
        {
            public string ExeName { get; set; }
            public string DisplayName { get; set; }
            public DateTime HardMutedAtUtc { get; set; }
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

        // App Rules Methods
        // A rule is applied every time one of the app's audio sessions appears,
        // including after the app relaunches or the machine reboots. Keyed by ExeName
        // because AppId and session ids are not stable across restarts.
        public bool IsAppHardMuted(string exeName)
        {
            return GetAppRule(exeName)?.HardMuted ?? false;
        }

        public AppRuleEntry GetAppRule(string exeName)
        {
            var normalizedExeName = NormalizeHiddenKeyValue(exeName);
            if (string.IsNullOrEmpty(normalizedExeName))
            {
                return null;
            }

            lock (_appRulesSync)
            {
                EnsureAppRulesLoaded();
                var existing = _appRuleEntries.FirstOrDefault(entry => entry.ExeName == normalizedExeName);
                return existing == null ? null : CloneRule(existing);
            }
        }

        public List<AppRuleEntry> GetAppRules()
        {
            lock (_appRulesSync)
            {
                EnsureAppRulesLoaded();
                return _appRuleEntries
                    .OrderBy(entry => string.IsNullOrEmpty(entry.DisplayName) ? entry.ExeName : entry.DisplayName)
                    .ThenBy(entry => entry.ExeName)
                    .Select(CloneRule)
                    .ToList();
            }
        }

        public void SetAppHardMuted(
            string exeName,
            bool hardMuted,
            string displayName = null,
            string iconPath = null,
            bool? isDesktopApp = null)
        {
            UpdateRule(exeName, displayName, iconPath, isDesktopApp, rule =>
            {
                if (rule.HardMuted == hardMuted)
                {
                    return false;
                }

                rule.HardMuted = hardMuted;
                return true;
            });
        }

        public void SetAppVolumeRule(
            string exeName,
            VolumeRuleMode mode,
            int volumePercent,
            string displayName = null,
            string iconPath = null,
            bool? isDesktopApp = null)
        {
            var boundedVolume = Math.Max(0, Math.Min(100, volumePercent));

            UpdateRule(exeName, displayName, iconPath, isDesktopApp, rule =>
            {
                if (rule.VolumeMode == mode && (mode == VolumeRuleMode.None || rule.VolumePercent == boundedVolume))
                {
                    return false;
                }

                rule.VolumeMode = mode;
                rule.VolumePercent = mode == VolumeRuleMode.None ? 0 : boundedVolume;
                return true;
            });
        }

        // Applies a mutation to one app's rule, creating it if needed and dropping it
        // once nothing is left to remember. Returns without notifying if nothing changed.
        private void UpdateRule(
            string exeName,
            string displayName,
            string iconPath,
            bool? isDesktopApp,
            Func<AppRuleEntry, bool> mutate)
        {
            var normalizedExeName = NormalizeHiddenKeyValue(exeName);
            if (string.IsNullOrEmpty(normalizedExeName))
            {
                return;
            }

            var safeDisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            var safeIconPath = string.IsNullOrWhiteSpace(iconPath) ? string.Empty : iconPath.Trim();
            bool changed;

            lock (_appRulesSync)
            {
                EnsureAppRulesLoaded();
                var existing = _appRuleEntries.FirstOrDefault(entry => entry.ExeName == normalizedExeName);
                var rule = existing ?? new AppRuleEntry
                {
                    ExeName = normalizedExeName,
                    DisplayName = safeDisplayName,
                    IconPath = safeIconPath,
                    IsDesktopApp = isDesktopApp ?? false,
                    CreatedAtUtc = DateTime.UtcNow,
                };

                changed = mutate(rule);

                if (existing == null)
                {
                    if (rule.IsEmpty)
                    {
                        return;
                    }

                    _appRuleEntries.Add(rule);
                }
                else
                {
                    if (!string.IsNullOrEmpty(safeDisplayName) && rule.DisplayName != safeDisplayName)
                    {
                        rule.DisplayName = safeDisplayName;
                        changed = true;
                    }

                    if (!string.IsNullOrEmpty(safeIconPath) &&
                        (rule.IconPath != safeIconPath || (isDesktopApp.HasValue && rule.IsDesktopApp != isDesktopApp.Value)))
                    {
                        rule.IconPath = safeIconPath;
                        rule.IsDesktopApp = isDesktopApp ?? rule.IsDesktopApp;
                        changed = true;
                    }

                    if (!changed)
                    {
                        return;
                    }
                }

                // An entry with no mute and no volume rule carries no information.
                _appRuleEntries.RemoveAll(entry => !entry.HardMuted && entry.VolumeMode == VolumeRuleMode.None);
                SaveAppRulesUnsafe();
            }

            AppRulesChanged?.Invoke();
        }

        public void RemoveAppRule(string exeName)
        {
            var normalizedExeName = NormalizeHiddenKeyValue(exeName);
            if (string.IsNullOrEmpty(normalizedExeName))
            {
                return;
            }

            bool changed = false;
            lock (_appRulesSync)
            {
                EnsureAppRulesLoaded();
                if (_appRuleEntries.RemoveAll(entry => entry.ExeName == normalizedExeName) > 0)
                {
                    SaveAppRulesUnsafe();
                    changed = true;
                }
            }

            if (changed)
            {
                AppRulesChanged?.Invoke();
            }
        }

        public void ClearAppRules()
        {
            bool changed = false;
            lock (_appRulesSync)
            {
                EnsureAppRulesLoaded();
                if (_appRuleEntries.Count > 0)
                {
                    _appRuleEntries.Clear();
                    SaveAppRulesUnsafe();
                    changed = true;
                }
            }

            if (changed)
            {
                AppRulesChanged?.Invoke();
            }
        }

        private void EnsureAppRulesLoaded()
        {
            if (_appRulesLoaded)
            {
                return;
            }

            try
            {
                var json = _settings.Get(AppRuleEntriesJsonKey, "[]");
                var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<List<AppRuleEntry>>(json) ?? new List<AppRuleEntry>();
                _appRuleEntries = NormalizeAppRuleEntries(loaded);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"AppSettings EnsureAppRulesLoaded failed: {ex.Message}");
                _appRuleEntries = new List<AppRuleEntry>();
            }

            // One-shot migration from the pre-merge hard-mute-only list.
            if (_appRuleEntries.Count == 0)
            {
                MigrateLegacyHardMutedAppsUnsafe();
            }

            _appRulesLoaded = true;
        }

        // Converts HardMutedAppEntriesJson (BetterTrumpet <= 3.2.0) into the merged rule list.
        // The legacy key is left untouched so downgrading keeps working.
        private void MigrateLegacyHardMutedAppsUnsafe()
        {
            try
            {
                if (!_settings.HasKey(LegacyHardMutedAppEntriesJsonKey))
                {
                    return;
                }

                var legacyJson = _settings.Get(LegacyHardMutedAppEntriesJsonKey, "[]");
                var legacy = Newtonsoft.Json.JsonConvert.DeserializeObject<List<LegacyHardMutedAppEntry>>(legacyJson);
                if (legacy == null || legacy.Count == 0)
                {
                    return;
                }

                _appRuleEntries = NormalizeAppRuleEntries(legacy
                    .Where(entry => entry != null)
                    .Select(entry => new AppRuleEntry
                    {
                        ExeName = entry.ExeName,
                        DisplayName = entry.DisplayName,
                        HardMuted = true,
                        VolumeMode = VolumeRuleMode.None,
                        VolumePercent = 0,
                        CreatedAtUtc = entry.HardMutedAtUtc,
                    })
                    .ToList());

                SaveAppRulesUnsafe();
                Trace.WriteLine($"AppSettings migrated {_appRuleEntries.Count} legacy hard-mute entries to app rules");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"AppSettings MigrateLegacyHardMutedApps failed: {ex.Message}");
            }
        }

        private void SaveAppRulesUnsafe()
        {
            _settings.Set(AppRuleEntriesJsonKey, Newtonsoft.Json.JsonConvert.SerializeObject(_appRuleEntries));
        }

        // Drops malformed entries, lowercases the exe key, dedupes on it, clamps the
        // volume, and forgets rules that no longer do anything.
        private List<AppRuleEntry> NormalizeAppRuleEntries(List<AppRuleEntry> entries)
        {
            var dedup = new HashSet<string>(StringComparer.Ordinal);
            var normalizedEntries = new List<AppRuleEntry>();

            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                var normalizedExeName = NormalizeHiddenKeyValue(entry.ExeName);
                if (string.IsNullOrEmpty(normalizedExeName) || !dedup.Add(normalizedExeName))
                {
                    continue;
                }

                // Hand-edited or downgraded JSON can carry a mode we don't know.
                var mode = Enum.IsDefined(typeof(VolumeRuleMode), entry.VolumeMode) ? entry.VolumeMode : VolumeRuleMode.None;
                var percent = Math.Max(0, Math.Min(100, entry.VolumePercent));

                if (!entry.HardMuted && mode == VolumeRuleMode.None)
                {
                    continue;
                }

                normalizedEntries.Add(new AppRuleEntry
                {
                    ExeName = normalizedExeName,
                    DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? string.Empty : entry.DisplayName.Trim(),
                    IconPath = string.IsNullOrWhiteSpace(entry.IconPath) ? string.Empty : entry.IconPath.Trim(),
                    IsDesktopApp = entry.IsDesktopApp,
                    HardMuted = entry.HardMuted,
                    VolumeMode = mode,
                    VolumePercent = percent,
                    CreatedAtUtc = entry.CreatedAtUtc,
                });
            }

            return normalizedEntries;
        }

        // Rules are handed out as copies so callers can't mutate the cached list
        // without going through SetApp* and raising AppRulesChanged.
        private static AppRuleEntry CloneRule(AppRuleEntry entry)
        {
            return new AppRuleEntry
            {
                ExeName = entry.ExeName,
                DisplayName = entry.DisplayName,
                IconPath = entry.IconPath,
                IsDesktopApp = entry.IsDesktopApp,
                HardMuted = entry.HardMuted,
                VolumeMode = entry.VolumeMode,
                VolumePercent = entry.VolumePercent,
                CreatedAtUtc = entry.CreatedAtUtc,
            };
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

        public bool HasStoredTelemetryConsent => _settings.HasKey("IsTelemetryEnabled");

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

        // Enable/disable volume tick sound effect
        public bool UseVolumeTickSound
        {
            get => _settings.Get("UseVolumeTickSound", true);
            set => _settings.Set("UseVolumeTickSound", value);
        }

        public bool MonkeyTickSoundUnlocked
        {
            get => _settings.Get("MonkeyTickSoundUnlocked", false);
            set => _settings.Set("MonkeyTickSoundUnlocked", value);
        }

        public bool UseMonkeyTickSound
        {
            get => MonkeyTickSoundUnlocked && _settings.Get("UseMonkeyTickSound", false);
            set => _settings.Set("UseMonkeyTickSound", MonkeyTickSoundUnlocked && value);
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

        public double WindowBackgroundOpacity
        {
            get => System.Math.Max(0.05, System.Math.Min(1.0, _settings.Get("WindowBackgroundOpacity", 0.7)));
            set
            {
                _settings.Set("WindowBackgroundOpacity", System.Math.Max(0.05, System.Math.Min(1.0, value)));
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

        // Per-app rules JSON storage (passthrough for settings export/import).
        public string AppRulesJson
        {
            get
            {
                // Force the migration so exporting right after an upgrade carries the rules.
                lock (_appRulesSync)
                {
                    EnsureAppRulesLoaded();
                }
                return _settings.Get(AppRuleEntriesJsonKey, "[]");
            }
            set
            {
                _settings.Set(AppRuleEntriesJsonKey, string.IsNullOrWhiteSpace(value) ? "[]" : value);
                lock (_appRulesSync)
                {
                    _appRulesLoaded = false;
                }
                AppRulesChanged?.Invoke();
            }
        }

        /// <summary>
        /// A launch-volume default for every desktop application whose executable is below a
        /// folder. App rules remain more specific and therefore always take precedence.
        /// </summary>
        public class FolderVolumeRuleEntry
        {
            public string Id { get; set; }
            public string FolderPath { get; set; }
            public int VolumePercent { get; set; }
            public DateTime CreatedAtUtc { get; set; }
        }

        // Folder defaults are kept separately from app rules: their match key is a full path,
        // while an app rule intentionally continues to use the stable executable name.
        public string FolderVolumeRulesJson
        {
            get
            {
                lock (_folderVolumeRulesSync)
                {
                    EnsureFolderVolumeRulesLoaded();
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_folderVolumeRules);
                }
            }
            set
            {
                lock (_folderVolumeRulesSync)
                {
                    _settings.Set(FolderVolumeRuleEntriesJsonKey, string.IsNullOrWhiteSpace(value) ? "[]" : value);
                    _folderVolumeRulesLoaded = false;
                }
                FolderVolumeRulesChanged?.Invoke();
            }
        }

        public List<FolderVolumeRuleEntry> GetFolderVolumeRules()
        {
            lock (_folderVolumeRulesSync)
            {
                EnsureFolderVolumeRulesLoaded();
                return _folderVolumeRules
                    .OrderBy(entry => entry.CreatedAtUtc)
                    .Select(CloneFolderVolumeRule)
                    .ToList();
            }
        }

        public void AddFolderVolumeRule(string folderPath, int volumePercent = 5)
        {
            var normalizedFolder = NormalizeFolderPath(folderPath);
            if (string.IsNullOrEmpty(normalizedFolder))
            {
                return;
            }

            var boundedVolume = Math.Max(0, Math.Min(100, volumePercent));
            bool changed = false;
            lock (_folderVolumeRulesSync)
            {
                EnsureFolderVolumeRulesLoaded();
                var existing = _folderVolumeRules.FirstOrDefault(entry =>
                    string.Equals(entry.FolderPath, normalizedFolder, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    _folderVolumeRules.Add(new FolderVolumeRuleEntry
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        FolderPath = normalizedFolder,
                        VolumePercent = boundedVolume,
                        CreatedAtUtc = DateTime.UtcNow,
                    });
                    changed = true;
                }
                else if (existing.VolumePercent != boundedVolume)
                {
                    existing.VolumePercent = boundedVolume;
                    changed = true;
                }

                if (changed)
                {
                    SaveFolderVolumeRulesUnsafe();
                }
            }

            if (changed)
            {
                FolderVolumeRulesChanged?.Invoke();
            }
        }

        public void UpdateFolderVolumeRule(string id, string folderPath, int volumePercent)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            var normalizedFolder = NormalizeFolderPath(folderPath);
            if (string.IsNullOrEmpty(normalizedFolder))
            {
                return;
            }

            var boundedVolume = Math.Max(0, Math.Min(100, volumePercent));
            bool changed = false;
            lock (_folderVolumeRulesSync)
            {
                EnsureFolderVolumeRulesLoaded();
                var rule = _folderVolumeRules.FirstOrDefault(entry => entry.Id == id);
                if (rule == null)
                {
                    return;
                }

                if (!string.Equals(rule.FolderPath, normalizedFolder, StringComparison.OrdinalIgnoreCase) ||
                    rule.VolumePercent != boundedVolume)
                {
                    rule.FolderPath = normalizedFolder;
                    rule.VolumePercent = boundedVolume;
                    SaveFolderVolumeRulesUnsafe();
                    changed = true;
                }
            }

            if (changed)
            {
                FolderVolumeRulesChanged?.Invoke();
            }
        }

        public void RemoveFolderVolumeRule(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            bool changed = false;
            lock (_folderVolumeRulesSync)
            {
                EnsureFolderVolumeRulesLoaded();
                if (_folderVolumeRules.RemoveAll(entry => entry.Id == id) > 0)
                {
                    SaveFolderVolumeRulesUnsafe();
                    changed = true;
                }
            }

            if (changed)
            {
                FolderVolumeRulesChanged?.Invoke();
            }
        }

        public bool TryGetFolderVolume(string executablePath, out int volumePercent)
        {
            volumePercent = 0;
            lock (_folderVolumeRulesSync)
            {
                EnsureFolderVolumeRulesLoaded();
                var rules = new List<FolderVolumeRule>(_folderVolumeRules.Count);
                foreach (var entry in _folderVolumeRules)
                {
                    rules.Add(new FolderVolumeRule(entry.FolderPath, entry.VolumePercent, entry.CreatedAtUtc));
                }

                return FolderVolumeRuleMatcher.TryMatch(executablePath, rules, out volumePercent);
            }
        }

        public bool TryGetRemoteDesktopVolume(out int volumePercent)
        {
            return RemoteDesktopIdentity.TryGetRememberedVolume(RemoteDesktopLastVolume, out volumePercent);
        }

        private void EnsureFolderVolumeRulesLoaded()
        {
            if (_folderVolumeRulesLoaded)
            {
                return;
            }

            try
            {
                var json = _settings.Get(FolderVolumeRuleEntriesJsonKey, "[]");
                var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<List<FolderVolumeRuleEntry>>(json) ?? new List<FolderVolumeRuleEntry>();
                _folderVolumeRules = NormalizeFolderVolumeRules(loaded);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"AppSettings EnsureFolderVolumeRulesLoaded failed: {ex.Message}");
                _folderVolumeRules = new List<FolderVolumeRuleEntry>();
            }

            _folderVolumeRulesLoaded = true;
        }

        private void SaveFolderVolumeRulesUnsafe()
        {
            _settings.Set(FolderVolumeRuleEntriesJsonKey, Newtonsoft.Json.JsonConvert.SerializeObject(_folderVolumeRules));
        }

        private static List<FolderVolumeRuleEntry> NormalizeFolderVolumeRules(List<FolderVolumeRuleEntry> entries)
        {
            var knownFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normalizedEntries = new List<FolderVolumeRuleEntry>();

            foreach (var entry in entries.Where(entry => entry != null))
            {
                var normalizedFolder = FolderVolumeRuleMatcher.NormalizeFolderPath(entry.FolderPath);
                if (string.IsNullOrEmpty(normalizedFolder) || !knownFolders.Add(normalizedFolder))
                {
                    continue;
                }

                normalizedEntries.Add(new FolderVolumeRuleEntry
                {
                    Id = string.IsNullOrWhiteSpace(entry.Id) ? Guid.NewGuid().ToString("N") : entry.Id,
                    FolderPath = normalizedFolder,
                    VolumePercent = Math.Max(0, Math.Min(100, entry.VolumePercent)),
                    CreatedAtUtc = entry.CreatedAtUtc == default ? DateTime.UtcNow : entry.CreatedAtUtc,
                });
            }

            return normalizedEntries;
        }

        private static FolderVolumeRuleEntry CloneFolderVolumeRule(FolderVolumeRuleEntry entry)
        {
            return new FolderVolumeRuleEntry
            {
                Id = entry.Id,
                FolderPath = entry.FolderPath,
                VolumePercent = entry.VolumePercent,
                CreatedAtUtc = entry.CreatedAtUtc,
            };
        }

        /// <summary>
        /// Import-only passthrough for pre-3.2.1 exports, which carried hard mutes under
        /// their own key. Merges them into the rule list instead of replacing it.
        /// </summary>
        public string LegacyHardMutedAppsJson
        {
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                try
                {
                    var legacy = Newtonsoft.Json.JsonConvert.DeserializeObject<List<LegacyHardMutedAppEntry>>(value);
                    if (legacy == null)
                    {
                        return;
                    }

                    foreach (var entry in legacy.Where(e => e != null))
                    {
                        SetAppHardMuted(entry.ExeName, true, entry.DisplayName);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"AppSettings LegacyHardMutedAppsJson import failed: {ex.Message}");
                }
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

        public double MixerWindowWidth
        {
            get => _settings.Get("MixerWindowWidth", 0.0);
            set => _settings.Set("MixerWindowWidth", value);
        }

        public double MixerWindowHeight
        {
            get => _settings.Get("MixerWindowHeight", 0.0);
            set => _settings.Set("MixerWindowHeight", value);
        }

        public bool NotifyOnDefaultDeviceChange
        {
            get => _settings.Get("NotifyOnDefaultDeviceChange", false);
            set => _settings.Set("NotifyOnDefaultDeviceChange", value);
        }

        public bool UseFocusLostVolume
        {
            get => _settings.Get("UseFocusLostVolume", false);
            set => _settings.Set("UseFocusLostVolume", value);
        }

        public int FocusLostAttenuatePercent
        {
            get => FocusLostVolumePolicy.ClampAttenuatePercent(_settings.Get("FocusLostAttenuatePercent", 0));
            set => _settings.Set("FocusLostAttenuatePercent", FocusLostVolumePolicy.ClampAttenuatePercent(value));
        }

        public int RemoteDesktopLastVolume
        {
            get => _settings.Get("RemoteDesktopLastVolume", -1);
            set => _settings.Set("RemoteDesktopLastVolume", RemoteDesktopIdentity.ClampStoredVolume(value));
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
                            var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
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
