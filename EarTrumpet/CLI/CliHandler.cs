using EarTrumpet.DataModel;
using EarTrumpet.DataModel.Audio;
using EarTrumpet.DataModel.WindowsAudio;
using EarTrumpet.Interop.MMDeviceAPI;
using EarTrumpet.UI.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;

namespace EarTrumpet.CLI
{
    /// <summary>
    /// Processes CLI commands received via named pipe IPC.
    /// All commands return JSON strings.
    /// 
    /// Commands:
    ///   ping                                        → {"status":"ok","version":"..."}
    ///   list-devices                                → [{"id":"...","name":"...","volume":80,...}]
    ///   list-apps                                   → [{"exeName":"spotify","displayName":"Spotify","volume":80,...}]
    ///   get-volume [--device ID]                    → {"volume":80,"isMuted":false}
    ///   set-volume VALUE [--device ID] [--app NAME] → {"ok":true} (supports +N / -N relative)
    ///   mute [--device ID] [--app NAME]             → {"ok":true}
    ///   unmute [--device ID] [--app NAME]           → {"ok":true}
    ///   toggle-mute [--device ID]                   → {"ok":true,"isMuted":true}
    ///   get-default                                 → {"id":"...","name":"..."}
    ///   set-default DEVICE_NAME                     → {"ok":true,"name":"..."}
    ///   set-device APP_EXE DEVICE_NAME              → {"ok":true,"app":"...","device":"..."}
    ///   list-profiles                               → [{"name":"...","devices":2,"createdAt":"..."}]
    ///   apply-profile NAME                          → {"ok":true,"name":"..."}
    ///   check-update                                → {"updateAvailable":true,"latestVersion":"3.1.0","releaseUrl":"..."}
    ///   watch                                       → streams JSON events until client disconnects
    /// </summary>
    public class CliHandler
    {
        private readonly Func<DeviceCollectionViewModel> _getCollection;
        private readonly Func<AppSettings> _getSettings;
        private readonly Func<IAudioDeviceManager> _getDeviceManager;
        private Func<DataModel.UpdateService> _getUpdateService;

        public CliHandler(
            Func<DeviceCollectionViewModel> getCollection,
            Func<AppSettings> getSettings,
            Func<IAudioDeviceManager> getDeviceManager)
        {
            _getCollection = getCollection;
            _getSettings = getSettings;
            _getDeviceManager = getDeviceManager;
        }

        public void SetUpdateServiceProvider(Func<DataModel.UpdateService> getUpdateService)
        {
            _getUpdateService = getUpdateService;
        }

        /// <summary>
        /// Process a command string and return JSON response.
        /// Called from the pipe server thread — must dispatch to UI thread for audio operations.
        /// </summary>
        public string ProcessCommand(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
                return Error("empty command");

            var parts = ParseCommandLine(commandLine);
            if (parts.Count == 0)
                return Error("empty command");

            var cmd = parts[0].ToLowerInvariant();
            var args = parts.Skip(1).ToList();

            try
            {
                switch (cmd)
                {
                    case "ping":
                        return JsonConvert.SerializeObject(new { status = "ok", version = App.PackageVersion?.ToString() ?? "unknown" });

                    case "list-devices":
                        return DispatchToUI(() => ListDevices());

                    case "list-apps":
                        return DispatchToUI(() => ListApps());

                    case "get-volume":
                        return DispatchToUI(() => GetVolume(args));

                    case "set-volume":
                        return DispatchToUI(() => SetVolume(args));

                    case "mute":
                        return DispatchToUI(() => SetMute(args, true));

                    case "unmute":
                        return DispatchToUI(() => SetMute(args, false));

                    case "toggle-mute":
                        return DispatchToUI(() => ToggleMute(args));

                    case "get-default":
                        return DispatchToUI(() => GetDefault());

                    case "set-default":
                        return DispatchToUI(() => SetDefault(args));

                    case "set-device":
                        return DispatchToUI(() => SetDeviceForApp(args));

                    case "list-profiles":
                        return ListProfiles();

                    case "apply-profile":
                        return DispatchToUI(() => ApplyProfile(args));

                    case "watch":
                        return DispatchToUI(() => WatchSnapshot());

                    case "check-update":
                        return DispatchToUI(() => CheckUpdate());

                    default:
                        return Error($"unknown command: {cmd}");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"CliHandler: Error processing '{cmd}' - {ex.Message}");
                return Error(ex.Message);
            }
        }

        // ═══════════════════════════════════
        // Command implementations
        // ═══════════════════════════════════

        private string ListDevices()
        {
            var collection = _getCollection();
            if (collection == null) return Error("audio not ready");

            var devices = new List<object>();
            var defaultId = collection.Default?.Id;

            foreach (var device in collection.AllDevices)
            {
                var apps = new List<object>();
                foreach (var app in device.Apps)
                {
                    apps.Add(new
                    {
                        id = app.Id,
                        name = app.DisplayName,
                        exeName = app.ExeName,
                        volume = app.Volume,
                        isMuted = app.IsMuted
                    });
                }

                devices.Add(new
                {
                    id = device.Id,
                    name = device.DisplayName,
                    volume = device.Volume,
                    isMuted = device.IsMuted,
                    isDefault = device.Id == defaultId,
                    apps
                });
            }

            return JsonConvert.SerializeObject(devices);
        }

        private string ListApps()
        {
            var collection = _getCollection();
            if (collection == null) return Error("audio not ready");

            var apps = new List<object>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var device in collection.AllDevices)
            {
                foreach (var app in device.Apps)
                {
                    // Deduplicate by exe name (apps can appear on multiple devices)
                    var key = app.ExeName ?? app.DisplayName;
                    if (seen.Contains(key)) continue;
                    seen.Add(key);

                    apps.Add(new
                    {
                        exeName = app.ExeName,
                        displayName = app.DisplayName,
                        volume = app.Volume,
                        isMuted = app.IsMuted,
                        device = device.DisplayName,
                        processId = app.ProcessId
                    });
                }
            }

            return JsonConvert.SerializeObject(apps);
        }

        private string GetVolume(List<string> args)
        {
            var device = ResolveDevice(args);
            if (device == null) return Error("device not found");

            return JsonConvert.SerializeObject(new
            {
                device = device.DisplayName,
                volume = device.Volume,
                isMuted = device.IsMuted
            });
        }

        private string SetVolume(List<string> args)
        {
            if (args.Count == 0) return Error("usage: set-volume VALUE [--device ID] [--app EXENAME]");

            var valueStr = args[0];

            // Check if setting app volume
            var appName = GetArg(args, "--app");

            // Parse volume — supports absolute (0-100) and relative (+N / -N)
            bool isRelative = valueStr.StartsWith("+") || (valueStr.StartsWith("-") && valueStr.Length > 1);

            if (isRelative)
            {
                if (!int.TryParse(valueStr, out int delta))
                    return Error("invalid relative volume value");

                if (appName != null)
                {
                    return SetAppVolumeRelative(appName, delta);
                }

                var device = ResolveDevice(args);
                if (device == null) return Error("device not found");

                int newVolume = Math.Max(0, Math.Min(100, device.Volume + delta));
                device.Volume = newVolume;
                return JsonConvert.SerializeObject(new { ok = true, device = device.DisplayName, volume = newVolume, delta });
            }
            else
            {
                if (!int.TryParse(valueStr, out int volume) || volume < 0 || volume > 100)
                    return Error("volume must be 0-100 (or +N/-N for relative)");

                if (appName != null)
                {
                    return SetAppVolume(appName, volume);
                }

                var device = ResolveDevice(args);
                if (device == null) return Error("device not found");

                device.Volume = volume;
                return JsonConvert.SerializeObject(new { ok = true, device = device.DisplayName, volume });
            }
        }

        private string SetAppVolume(string exeName, int volume)
        {
            var collection = _getCollection();
            if (collection == null) return Error("audio not ready");

            foreach (var device in collection.AllDevices)
            {
                var app = device.Apps.FirstOrDefault(a =>
                    string.Equals(a.ExeName, exeName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a.DisplayName, exeName, StringComparison.OrdinalIgnoreCase));

                if (app != null)
                {
                    app.Volume = volume;
                    return JsonConvert.SerializeObject(new { ok = true, app = app.DisplayName, volume });
                }
            }

            return Error($"app not found: {exeName}");
        }

        private string SetAppVolumeRelative(string exeName, int delta)
        {
            var collection = _getCollection();
            if (collection == null) return Error("audio not ready");

            foreach (var device in collection.AllDevices)
            {
                var app = device.Apps.FirstOrDefault(a =>
                    string.Equals(a.ExeName, exeName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a.DisplayName, exeName, StringComparison.OrdinalIgnoreCase));

                if (app != null)
                {
                    int newVolume = Math.Max(0, Math.Min(100, app.Volume + delta));
                    app.Volume = newVolume;
                    return JsonConvert.SerializeObject(new { ok = true, app = app.DisplayName, volume = newVolume, delta });
                }
            }

            return Error($"app not found: {exeName}");
        }

        private string SetMute(List<string> args, bool mute)
        {
            // Check if muting an app
            var appName = GetArg(args, "--app");
            if (appName != null)
            {
                var collection = _getCollection();
                if (collection == null) return Error("audio not ready");

                foreach (var device in collection.AllDevices)
                {
                    var app = device.Apps.FirstOrDefault(a =>
                        string.Equals(a.ExeName, appName, StringComparison.OrdinalIgnoreCase));
                    if (app != null)
                    {
                        app.IsMuted = mute;
                        return JsonConvert.SerializeObject(new { ok = true, app = app.DisplayName, isMuted = mute });
                    }
                }
                return Error($"app not found: {appName}");
            }

            var device2 = ResolveDevice(args);
            if (device2 == null) return Error("device not found");

            device2.IsMuted = mute;
            return JsonConvert.SerializeObject(new { ok = true, device = device2.DisplayName, isMuted = mute });
        }

        private string ToggleMute(List<string> args)
        {
            var device = ResolveDevice(args);
            if (device == null) return Error("device not found");

            device.IsMuted = !device.IsMuted;
            return JsonConvert.SerializeObject(new { ok = true, device = device.DisplayName, isMuted = device.IsMuted });
        }

        private string GetDefault()
        {
            var collection = _getCollection();
            if (collection == null) return Error("audio not ready");

            var def = collection.Default;
            if (def == null) return Error("no default device");

            return JsonConvert.SerializeObject(new
            {
                id = def.Id,
                name = def.DisplayName,
                volume = def.Volume,
                isMuted = def.IsMuted
            });
        }

        private string SetDefault(List<string> args)
        {
            if (args.Count == 0) return Error("usage: set-default DEVICE_NAME");

            var deviceName = string.Join(" ", args);
            var collection = _getCollection();
            if (collection == null) return Error("audio not ready");

            var mgr = _getDeviceManager() as IAudioDeviceManagerWindowsAudio;
            if (mgr == null) return Error("device manager not available");

            // Find device by name
            var device = collection.AllDevices.FirstOrDefault(d =>
                string.Equals(d.DisplayName, deviceName, StringComparison.OrdinalIgnoreCase))
                ?? collection.AllDevices.FirstOrDefault(d =>
                    d.DisplayName.IndexOf(deviceName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (device == null) return Error($"device not found: {deviceName}");

            // Use MakeDefaultDevice which sets the device manager's Default property
            device.MakeDefaultDevice();

            return JsonConvert.SerializeObject(new { ok = true, name = device.DisplayName, id = device.Id });
        }

        private string SetDeviceForApp(List<string> args)
        {
            if (args.Count < 2) return Error("usage: set-device APP_EXE DEVICE_NAME");

            var appExe = args[0];
            var deviceName = string.Join(" ", args.Skip(1));

            var collection = _getCollection();
            if (collection == null) return Error("audio not ready");

            var mgr = _getDeviceManager() as IAudioDeviceManagerWindowsAudio;
            if (mgr == null) return Error("device manager not available");

            // Find target device
            var targetDevice = collection.AllDevices.FirstOrDefault(d =>
                string.Equals(d.DisplayName, deviceName, StringComparison.OrdinalIgnoreCase))
                ?? collection.AllDevices.FirstOrDefault(d =>
                    d.DisplayName.IndexOf(deviceName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (targetDevice == null) return Error($"device not found: {deviceName}");

            // Find the app across all devices and get its process IDs
            int routed = 0;
            var routedApps = new List<string>();

            foreach (var device in collection.AllDevices)
            {
                foreach (var app in device.Apps)
                {
                    if (string.Equals(app.ExeName, appExe, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(app.DisplayName, appExe, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            mgr.SetDefaultEndPoint(targetDevice.Id, app.ProcessId);
                            routed++;
                            if (!routedApps.Contains(app.DisplayName))
                                routedApps.Add(app.DisplayName);
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"CliHandler: Failed to route PID {app.ProcessId}: {ex.Message}");
                        }
                    }
                }
            }

            // Also try to match by process name for apps not currently playing audio
            if (routed == 0)
            {
                try
                {
                    var exeNoExt = System.IO.Path.GetFileNameWithoutExtension(appExe);
                    var processes = Process.GetProcessesByName(exeNoExt);
                    foreach (var proc in processes)
                    {
                        try
                        {
                            mgr.SetDefaultEndPoint(targetDevice.Id, proc.Id);
                            routed++;
                            routedApps.Add(exeNoExt);
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"CliHandler: Failed to route PID {proc.Id}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"CliHandler: Process lookup failed: {ex.Message}");
                }
            }

            if (routed == 0) return Error($"app not found or no processes running: {appExe}");

            return JsonConvert.SerializeObject(new
            {
                ok = true,
                app = routedApps.FirstOrDefault() ?? appExe,
                device = targetDevice.DisplayName,
                processesRouted = routed
            });
        }

        /// <summary>
        /// Watch returns a one-time snapshot of all devices + volumes.
        /// The client can poll this repeatedly for monitoring.
        /// A true streaming mode would require a different pipe protocol.
        /// </summary>
        private string WatchSnapshot()
        {
            var collection = _getCollection();
            if (collection == null) return Error("audio not ready");

            var snapshot = new List<object>();
            var defaultId = collection.Default?.Id;

            foreach (var device in collection.AllDevices)
            {
                var apps = new List<object>();
                foreach (var app in device.Apps)
                {
                    apps.Add(new
                    {
                        exeName = app.ExeName,
                        name = app.DisplayName,
                        volume = app.Volume,
                        isMuted = app.IsMuted,
                        processId = app.ProcessId
                    });
                }

                snapshot.Add(new
                {
                    name = device.DisplayName,
                    volume = device.Volume,
                    isMuted = device.IsMuted,
                    isDefault = device.Id == defaultId,
                    appCount = device.Apps.Count(),
                    apps
                });
            }

            return JsonConvert.SerializeObject(new
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                devices = snapshot
            });
        }

        private string CheckUpdate()
        {
            var svc = _getUpdateService?.Invoke();
            if (svc == null) return Error("update service not available");

            // Trigger a check (fire-and-forget async, but we can return current state)
            svc.CheckForUpdateAsync();

            // Wait briefly for the check to complete (it's async with HttpClient)
            // Return current known state — the check may still be in-flight
            var waited = 0;
            while (svc.IsChecking && waited < 5000)
            {
                System.Threading.Thread.Sleep(100);
                waited += 100;
            }

            return JsonConvert.SerializeObject(new
            {
                updateAvailable = svc.IsUpdateAvailable,
                currentVersion = App.PackageVersion?.ToString() ?? "unknown",
                latestVersion = svc.LatestVersion ?? "unknown",
                releaseUrl = svc.ReleaseUrl ?? "",
                lastChecked = svc.LastCheckTime.ToString("yyyy-MM-ddTHH:mm:ss")
            });
        }

        private string ListProfiles()
        {
            var settings = _getSettings();
            if (settings == null) return Error("settings not available");

            try
            {
                var json = settings.VolumeProfilesJson;
                if (string.IsNullOrWhiteSpace(json) || json == "[]")
                    return "[]";

                var profiles = JsonConvert.DeserializeObject<List<VolumeProfileService.VolumeProfile>>(json);
                var result = profiles?.Select(p => new
                {
                    name = p.Name,
                    devices = p.Devices?.Count ?? 0,
                    createdAt = p.CreatedAt
                }).ToList();

                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                return Error($"failed to load profiles: {ex.Message}");
            }
        }

        private string ApplyProfile(List<string> args)
        {
            if (args.Count == 0) return Error("usage: apply-profile NAME");

            var profileName = string.Join(" ", args);
            var settings = _getSettings();
            if (settings == null) return Error("settings not available");

            var collection = _getCollection();
            if (collection == null) return Error("audio not ready");

            try
            {
                var json = settings.VolumeProfilesJson;
                if (string.IsNullOrWhiteSpace(json)) return Error("no profiles saved");

                var profiles = JsonConvert.DeserializeObject<List<VolumeProfileService.VolumeProfile>>(json);
                var profile = profiles?.FirstOrDefault(p =>
                    string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));

                if (profile == null)
                    return Error($"profile not found: {profileName}");

                var service = new VolumeProfileService(settings);
                service.ApplyProfile(profile, collection);

                return JsonConvert.SerializeObject(new { ok = true, name = profile.Name, devices = profile.Devices?.Count ?? 0 });
            }
            catch (Exception ex)
            {
                return Error($"failed to apply profile: {ex.Message}");
            }
        }

        // ═══════════════════════════════════
        // Helpers
        // ═══════════════════════════════════

        private DeviceViewModel ResolveDevice(List<string> args)
        {
            var collection = _getCollection();
            if (collection == null) return null;

            var deviceArg = GetArg(args, "--device");

            if (deviceArg == null)
            {
                // Default device
                return collection.Default;
            }

            // Match by ID or display name (case-insensitive)
            return collection.AllDevices.FirstOrDefault(d => d.Id == deviceArg)
                ?? collection.AllDevices.FirstOrDefault(d =>
                    d.DisplayName.IndexOf(deviceArg, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string GetArg(List<string> args, string flag)
        {
            var idx = args.FindIndex(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0 && idx + 1 < args.Count)
            {
                return args[idx + 1];
            }
            return null;
        }

        /// <summary>
        /// Parse a command line string into parts, respecting quoted strings.
        /// </summary>
        private static List<string> ParseCommandLine(string line)
        {
            var parts = new List<string>();
            var current = "";
            bool inQuotes = false;

            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        parts.Add(current);
                        current = "";
                    }
                }
                else
                {
                    current += c;
                }
            }

            if (current.Length > 0) parts.Add(current);
            return parts;
        }

        /// <summary>
        /// Dispatch a function to the UI thread and wait for the result.
        /// Needed because COM audio objects are STA and must be accessed from the UI thread.
        /// </summary>
        private string DispatchToUI(Func<string> action)
        {
            string result = null;
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                return action();
            }

            dispatcher.Invoke(() => { result = action(); });
            return result;
        }

        private static string Error(string message)
        {
            return JsonConvert.SerializeObject(new { error = message });
        }
    }
}
