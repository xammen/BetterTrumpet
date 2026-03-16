using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace EarTrumpet.DataModel
{
    /// <summary>
    /// Exports and imports all BetterTrumpet settings as a portable JSON file (.btsettings).
    /// </summary>
    public static class SettingsExportService
    {
        private const string FileFilter = "BetterTrumpet Settings (*.btsettings)|*.btsettings";
        private const int FormatVersion = 1;

        /// <summary>
        /// Captures all current settings into a dictionary.
        /// </summary>
        public static Dictionary<string, object> CaptureSettings(AppSettings settings)
        {
            var data = new Dictionary<string, object>
            {
                ["_formatVersion"] = FormatVersion,
                ["_exportedAt"] = DateTime.UtcNow.ToString("o"),
                ["_appVersion"] = App.PackageVersion?.ToString() ?? "unknown",

                // General
                ["UseLegacyIcon"] = settings.UseLegacyIcon,
                ["IsExpanded"] = settings.IsExpanded,
                ["UseScrollWheelInTray"] = settings.UseScrollWheelInTray,
                ["UseGlobalMouseWheelHook"] = settings.UseGlobalMouseWheelHook,
                ["IsTelemetryEnabled"] = settings.IsTelemetryEnabled,
                ["RunAtStartup"] = settings.RunAtStartup,

                // Audio
                ["UseLogarithmicVolume"] = settings.UseLogarithmicVolume,
                ["UseSmoothVolumeAnimation"] = settings.UseSmoothVolumeAnimation,
                ["VolumeAnimationSpeed"] = settings.VolumeAnimationSpeed,
                ["PeakMeterFps"] = settings.PeakMeterFps,
                ["EcoMode"] = settings.EcoMode,
                ["AutoEcoMode"] = settings.AutoEcoMode,

                // Theme / Colors
                ["UseCustomSliderColors"] = settings.UseCustomSliderColors,
                ["SliderThumbColor"] = settings.SliderThumbColor.ToString(),
                ["SliderTrackFillColor"] = settings.SliderTrackFillColor.ToString(),
                ["SliderTrackBackgroundColor"] = settings.SliderTrackBackgroundColor.ToString(),
                ["PeakMeterColor"] = settings.PeakMeterColor.ToString(),
                ["WindowBackgroundColor"] = settings.WindowBackgroundColor.ToString(),
                ["TextColor"] = settings.TextColor.ToString(),
                ["AccentGlowColor"] = settings.AccentGlowColor.ToString(),
                ["CustomThemesJson"] = settings.CustomThemesJson,
                ["ActiveThemeName"] = settings.ActiveThemeName,
                ["UseDynamicAlbumArtTheme"] = settings.UseDynamicAlbumArtTheme,

                // Media Popup
                ["MediaPopupEnabled"] = settings.MediaPopupEnabled,
                ["MediaPopupHoverDelay"] = settings.MediaPopupHoverDelay,
                ["MediaPopupBlurRadius"] = settings.MediaPopupBlurRadius,
                ["MediaPopupShowOnlyWhenPlaying"] = settings.MediaPopupShowOnlyWhenPlaying,
                ["MediaPopupRememberExpanded"] = settings.MediaPopupRememberExpanded,
                ["MediaPopupIsExpanded"] = settings.MediaPopupIsExpanded,

                // Updates
                ["AutoCheckForUpdates"] = settings.AutoCheckForUpdates,
                ["UpdateNotifyChannel"] = (int)settings.UpdateNotifyChannel,

                // Hotkeys
                ["FlyoutHotkey"] = SerializeHotkey(settings.FlyoutHotkey),
                ["MixerHotkey"] = SerializeHotkey(settings.MixerHotkey),
                ["SettingsHotkey"] = SerializeHotkey(settings.SettingsHotkey),
                ["AbsoluteVolumeUpHotkey"] = SerializeHotkey(settings.AbsoluteVolumeUpHotkey),
                ["AbsoluteVolumeDownHotkey"] = SerializeHotkey(settings.AbsoluteVolumeDownHotkey),

                // Volume Profiles (embedded)
                ["VolumeProfilesJson"] = settings.VolumeProfilesJson,
            };

            return data;
        }

        /// <summary>
        /// Shows a Save dialog and exports all settings.
        /// </summary>
        public static bool ExportWithDialog(AppSettings settings)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export BetterTrumpet Settings",
                Filter = FileFilter,
                FileName = $"BetterTrumpet_Settings_{DateTime.Now:yyyy-MM-dd}",
                DefaultExt = ".btsettings",
            };

            if (dialog.ShowDialog() != true) return false;

            try
            {
                var data = CaptureSettings(settings);
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(dialog.FileName, json);
                Trace.WriteLine($"SettingsExportService: Exported to {dialog.FileName}");
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"SettingsExportService: Export failed: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"Failed to export settings:\n{ex.Message}",
                    "Export Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Shows an Open dialog and imports settings from a .btsettings file.
        /// </summary>
        public static bool ImportWithDialog(AppSettings settings)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import BetterTrumpet Settings",
                Filter = FileFilter,
                DefaultExt = ".btsettings",
            };

            if (dialog.ShowDialog() != true) return false;

            return ImportFromFile(settings, dialog.FileName, showErrors: true);
        }

        /// <summary>
        /// Imports settings from a file path (used by CLI and dialog).
        /// </summary>
        public static bool ImportFromFile(AppSettings settings, string filePath, bool showErrors = false)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                if (data == null)
                    throw new InvalidOperationException("Invalid settings file: empty or malformed JSON.");

                ApplySettings(settings, data);
                Trace.WriteLine($"SettingsExportService: Imported from {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"SettingsExportService: Import failed: {ex.Message}");
                if (showErrors)
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to import settings:\n{ex.Message}",
                        "Import Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
                return false;
            }
        }

        /// <summary>
        /// Exports settings to a file path (used by CLI).
        /// </summary>
        public static bool ExportToFile(AppSettings settings, string filePath)
        {
            try
            {
                var data = CaptureSettings(settings);
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(filePath, json);
                Trace.WriteLine($"SettingsExportService: Exported to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"SettingsExportService: Export failed: {ex.Message}");
                return false;
            }
        }

        private static void ApplySettings(AppSettings settings, Dictionary<string, object> data)
        {
            // General
            TrySet(data, "UseLegacyIcon", (bool v) => settings.UseLegacyIcon = v);
            TrySet(data, "IsExpanded", (bool v) => settings.IsExpanded = v);
            TrySet(data, "UseScrollWheelInTray", (bool v) => settings.UseScrollWheelInTray = v);
            TrySet(data, "UseGlobalMouseWheelHook", (bool v) => settings.UseGlobalMouseWheelHook = v);
            TrySet(data, "IsTelemetryEnabled", (bool v) => settings.IsTelemetryEnabled = v);
            // Skip RunAtStartup on import — security sensitive, user should set it themselves

            // Audio
            TrySet(data, "UseLogarithmicVolume", (bool v) => settings.UseLogarithmicVolume = v);
            TrySet(data, "UseSmoothVolumeAnimation", (bool v) => settings.UseSmoothVolumeAnimation = v);
            TrySet(data, "VolumeAnimationSpeed", (double v) => settings.VolumeAnimationSpeed = v);
            TrySet(data, "PeakMeterFps", (int v) => settings.PeakMeterFps = v);
            TrySet(data, "EcoMode", (bool v) => settings.EcoMode = v);
            TrySet(data, "AutoEcoMode", (bool v) => settings.AutoEcoMode = v);

            // Theme / Colors
            TrySet(data, "UseCustomSliderColors", (bool v) => settings.UseCustomSliderColors = v);
            TrySetColor(data, "SliderThumbColor", c => settings.SliderThumbColor = c);
            TrySetColor(data, "SliderTrackFillColor", c => settings.SliderTrackFillColor = c);
            TrySetColor(data, "SliderTrackBackgroundColor", c => settings.SliderTrackBackgroundColor = c);
            TrySetColor(data, "PeakMeterColor", c => settings.PeakMeterColor = c);
            TrySetColor(data, "WindowBackgroundColor", c => settings.WindowBackgroundColor = c);
            TrySetColor(data, "TextColor", c => settings.TextColor = c);
            TrySetColor(data, "AccentGlowColor", c => settings.AccentGlowColor = c);
            TrySet(data, "CustomThemesJson", (string v) => settings.CustomThemesJson = v);
            TrySet(data, "ActiveThemeName", (string v) => settings.ActiveThemeName = v);
            TrySet(data, "UseDynamicAlbumArtTheme", (bool v) => settings.UseDynamicAlbumArtTheme = v);

            // Media Popup
            TrySet(data, "MediaPopupEnabled", (bool v) => settings.MediaPopupEnabled = v);
            TrySet(data, "MediaPopupHoverDelay", (double v) => settings.MediaPopupHoverDelay = v);
            TrySet(data, "MediaPopupBlurRadius", (double v) => settings.MediaPopupBlurRadius = v);
            TrySet(data, "MediaPopupShowOnlyWhenPlaying", (bool v) => settings.MediaPopupShowOnlyWhenPlaying = v);
            TrySet(data, "MediaPopupRememberExpanded", (bool v) => settings.MediaPopupRememberExpanded = v);
            TrySet(data, "MediaPopupIsExpanded", (bool v) => settings.MediaPopupIsExpanded = v);

            // Updates
            TrySet(data, "AutoCheckForUpdates", (bool v) => settings.AutoCheckForUpdates = v);
            TrySet(data, "UpdateNotifyChannel", (int v) => settings.UpdateNotifyChannel = (UpdateChannel)v);

            // Hotkeys
            TrySetHotkey(data, "FlyoutHotkey", h => settings.FlyoutHotkey = h);
            TrySetHotkey(data, "MixerHotkey", h => settings.MixerHotkey = h);
            TrySetHotkey(data, "SettingsHotkey", h => settings.SettingsHotkey = h);
            TrySetHotkey(data, "AbsoluteVolumeUpHotkey", h => settings.AbsoluteVolumeUpHotkey = h);
            TrySetHotkey(data, "AbsoluteVolumeDownHotkey", h => settings.AbsoluteVolumeDownHotkey = h);

            // Volume Profiles
            TrySet(data, "VolumeProfilesJson", (string v) => settings.VolumeProfilesJson = v);
        }

        private static void TrySet<T>(Dictionary<string, object> data, string key, Action<T> setter)
        {
            if (!data.ContainsKey(key)) return;
            try
            {
                var val = data[key];
                if (val is T typed)
                {
                    setter(typed);
                }
                else
                {
                    // Handle Newtonsoft JValue boxing
                    setter((T)Convert.ChangeType(val is JValue jv ? jv.Value : val, typeof(T)));
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"SettingsExportService: Failed to set {key}: {ex.Message}");
            }
        }

        private static void TrySetColor(Dictionary<string, object> data, string key, Action<System.Windows.Media.Color> setter)
        {
            if (!data.ContainsKey(key)) return;
            try
            {
                var str = data[key]?.ToString();
                if (!string.IsNullOrEmpty(str))
                {
                    var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(str);
                    setter(color);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"SettingsExportService: Failed to set color {key}: {ex.Message}");
            }
        }

        private static object SerializeHotkey(Interop.Helpers.HotkeyData hotkey)
        {
            return new Dictionary<string, object>
            {
                ["Modifiers"] = (int)hotkey.Modifiers,
                ["Key"] = (int)hotkey.Key,
            };
        }

        private static void TrySetHotkey(Dictionary<string, object> data, string key, Action<Interop.Helpers.HotkeyData> setter)
        {
            if (!data.ContainsKey(key)) return;
            try
            {
                var obj = data[key];
                JObject jobj = obj is JObject j ? j : JObject.FromObject(obj);
                var hotkey = new Interop.Helpers.HotkeyData
                {
                    Modifiers = (Keys)(int)jobj["Modifiers"],
                    Key = (Keys)(int)jobj["Key"],
                };
                setter(hotkey);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"SettingsExportService: Failed to set hotkey {key}: {ex.Message}");
            }
        }
    }
}
