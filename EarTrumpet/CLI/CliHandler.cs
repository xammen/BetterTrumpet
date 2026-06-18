using EarTrumpet.DataModel;
using EarTrumpet.DataModel.Audio;
using EarTrumpet.DataModel.WindowsAudio;
using EarTrumpet.Interop.MMDeviceAPI;
using EarTrumpet.UI.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
    ///   toggle-mute [--device ID] [--app NAME]      → {"ok":true,"isMuted":true}
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

        private sealed class RuleAssignment
        {
            public string Query { get; set; }
            public int? Volume { get; set; }
            public bool? IsMuted { get; set; }
            public bool IsOthers { get; set; }
        }

        private sealed class RuleSpec
        {
            public List<RuleAssignment> Keep { get; } = new List<RuleAssignment>();
            public RuleAssignment Others { get; set; }
            public bool CaptureAllDevices { get; set; } = true;
            public bool ApplyAppsOnly { get; set; }
        }

        private sealed class RuleAppMatch
        {
            public string Query { get; set; }
            public string MatchType { get; set; }
            public int Score { get; set; }
            public string AppId { get; set; }
            public string ExeName { get; set; }
            public string DisplayName { get; set; }
            public string DeviceId { get; set; }
            public string DeviceName { get; set; }
            public int ProcessId { get; set; }
            public int Volume { get; set; }
            public bool IsMuted { get; set; }
        }

        private sealed class RulePlanChange
        {
            public string Scope { get; set; }
            public string Rule { get; set; }
            public string Query { get; set; }
            public string MatchType { get; set; }
            public string AppId { get; set; }
            public string ExeName { get; set; }
            public string DisplayName { get; set; }
            public string DeviceId { get; set; }
            public string DeviceName { get; set; }
            public int ProcessId { get; set; }
            public int VolumeBefore { get; set; }
            public int VolumeAfter { get; set; }
            public bool IsMutedBefore { get; set; }
            public bool IsMutedAfter { get; set; }
        }

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

            parts = NormalizeCommand(parts);

            var cmd = parts[0].ToLowerInvariant();
            var args = parts.Skip(1).ToList();

            try
            {
                switch (cmd)
                {
                    case "ping":
                        return JsonConvert.SerializeObject(new { status = "ok", version = App.PackageVersion?.ToString() ?? "unknown" });

                    case "doctor":
                        return DispatchToUI(() => Doctor());

                    case "batch":
                        return Batch(args);

                    case "list-devices":
                        return DispatchToUI(() => ListDevices());

                    case "list-apps":
                        return DispatchToUI(() => ListApps());

                    case "resolve-apps":
                        return DispatchToUI(() => ResolveApps(args));

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
                    case "presets":
                        return ListProfiles();

                    case "apply-profile":
                    case "apply":
                        return DispatchToUI(() => ApplyProfile(args));

                    case "save":
                        return DispatchToUI(() => SaveProfile(args));

                    case "delete":
                        return DeleteProfile(args);

                    case "rule-preview":
                        return DispatchToUI(() => PreviewRule(args));

                    case "rule-apply":
                        return DispatchToUI(() => ApplyRule(args));

                    case "preset-create":
                        return DispatchToUI(() => CreatePresetFromRule(args));

                    case "watch":
                        return DispatchToUI(() => WatchSnapshot());

                    case "check-update":
                        return DispatchToUI(() => CheckUpdate());

                    case "export-settings":
                        return ExportSettings(args);

                    case "import-settings":
                        return ImportSettings(args);

                    default:
                        return DispatchToUI(() => ApplyProfile(parts));
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

            foreach (var device in collection.AllDevices ?? Enumerable.Empty<DeviceViewModel>())
            {
                if (device == null) continue;

                var apps = new List<object>();
                foreach (var app in device.Apps ?? Enumerable.Empty<IAppItemViewModel>())
                {
                    if (app == null) continue;

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

            foreach (var device in collection.AllDevices ?? Enumerable.Empty<DeviceViewModel>())
            {
                if (device == null) continue;

                foreach (var app in device.Apps ?? Enumerable.Empty<IAppItemViewModel>())
                {
                    if (app == null) continue;

                    // Deduplicate by exe name (apps can appear on multiple devices)
                    var key = app.ExeName ?? app.DisplayName ?? app.Id;
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

            var app = FindApp(collection, exeName);
            if (app != null)
            {
                app.Volume = volume;
                return JsonConvert.SerializeObject(new { ok = true, app = app.DisplayName, volume });
            }

            return Error($"app not found: {exeName}");
        }

        private string SetAppVolumeRelative(string exeName, int delta)
        {
            var collection = _getCollection();
            if (collection == null) return Error("audio not ready");

            var app = FindApp(collection, exeName);
            if (app != null)
            {
                int newVolume = Math.Max(0, Math.Min(100, app.Volume + delta));
                app.Volume = newVolume;
                return JsonConvert.SerializeObject(new { ok = true, app = app.DisplayName, volume = newVolume, delta });
            }

            return Error($"app not found: {exeName}");
        }

        private string SetMute(List<string> args, bool mute)
        {
            // Check if muting an app
            var appName = GetArg(args, "--app");
            if (appName != null)
            {
                var app = ResolveApp(appName);
                if (app == null) return Error($"app not found: {appName}");

                app.IsMuted = mute;
                return JsonConvert.SerializeObject(new { ok = true, app = app.DisplayName, isMuted = app.IsMuted });
            }

            var device2 = ResolveDevice(args);
            if (device2 == null) return Error("device not found");

            device2.IsMuted = mute;
            return JsonConvert.SerializeObject(new { ok = true, device = device2.DisplayName, isMuted = mute });
        }

        private string ToggleMute(List<string> args)
        {
            var appName = GetArg(args, "--app");
            if (appName != null)
            {
                var app = ResolveApp(appName);
                if (app == null) return Error($"app not found: {appName}");

                app.IsMuted = !app.IsMuted;
                return JsonConvert.SerializeObject(new { ok = true, app = app.DisplayName, isMuted = app.IsMuted });
            }

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

            foreach (var device in collection.AllDevices ?? Enumerable.Empty<DeviceViewModel>())
            {
                if (device == null) continue;

                foreach (var app in device.Apps ?? Enumerable.Empty<IAppItemViewModel>())
                {
                    if (app == null) continue;

                    if (AppMatches(app, appExe))
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

            foreach (var device in collection.AllDevices ?? Enumerable.Empty<DeviceViewModel>())
            {
                if (device == null) continue;

                var apps = new List<object>();
                foreach (var app in device.Apps ?? Enumerable.Empty<IAppItemViewModel>())
                {
                    if (app == null) continue;

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

        private string Doctor()
        {
            var settings = _getSettings();
            var collection = _getCollection();
            var manager = _getDeviceManager();
            var profiles = 0;

            try
            {
                var json = settings?.VolumeProfilesJson;
                if (!string.IsNullOrWhiteSpace(json) && json != "[]")
                {
                    profiles = JsonConvert.DeserializeObject<List<VolumeProfileService.VolumeProfile>>(json)?.Count ?? 0;
                }
            }
            catch
            {
                // Doctor should report degraded state, not fail the whole command.
            }

            return JsonConvert.SerializeObject(new
            {
                ok = collection != null && manager != null,
                version = App.PackageVersion?.ToString() ?? "unknown",
                audioReady = collection != null,
                deviceManagerReady = manager != null,
                settingsReady = settings != null,
                defaultDevice = collection?.Default?.DisplayName,
                devices = collection?.AllDevices?.Count() ?? 0,
                apps = collection?.AllDevices?.Where(d => d != null).Sum(d => d.Apps?.Count() ?? 0) ?? 0,
                presets = profiles
            });
        }

        private string Batch(List<string> args)
        {
            if (args.Count == 0) return Error("usage: batch <command...> [<command...>]");

            var commands = SplitBatchCommands(args);
            if (commands.Count == 0) return Error("usage: batch <command...> [<command...>]");

            var results = new List<object>();
            var allOk = true;

            foreach (var commandParts in commands)
            {
                var commandLine = string.Join(" ", commandParts.Select(QuoteIfNeeded));
                var response = ProcessCommand(commandLine);
                JToken parsed;
                try
                {
                    parsed = JToken.Parse(response);
                }
                catch
                {
                    parsed = JValue.CreateString(response);
                }

                if (parsed.Type == JTokenType.Object && parsed["error"] != null)
                {
                    allOk = false;
                }

                results.Add(new
                {
                    command = commandLine,
                    response = parsed
                });
            }

            return JsonConvert.SerializeObject(new { ok = allOk, results });
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
                    slug = string.IsNullOrWhiteSpace(p.Slug) ? VolumeProfileService.ToSlug(p.Name) : p.Slug,
                    devices = p.Devices?.Count ?? 0,
                    apps = p.Devices?.Sum(d => d.Apps?.Count ?? 0) ?? 0,
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
                var service = new VolumeProfileService(settings);
                var profile = service.FindProfile(profileName);
                if (profile == null)
                    return Error($"QuickTrumpet preset not found: {profileName}");

                var result = service.ApplyProfile(profile, collection, _getDeviceManager() as IAudioDeviceManagerWindowsAudio);

                return JsonConvert.SerializeObject(new
                {
                    ok = true,
                    preset = profile.Name,
                    slug = string.IsNullOrWhiteSpace(profile.Slug) ? VolumeProfileService.ToSlug(profile.Name) : profile.Slug,
                    devicesApplied = result.DevicesApplied,
                    appsApplied = result.AppsApplied,
                    appsMissing = result.AppsMissing,
                    appsRouted = result.AppsRouted,
                    warnings = result.Warnings
                });
            }
            catch (Exception ex)
            {
                return Error($"failed to apply profile: {ex.Message}");
            }
        }

        private string SaveProfile(List<string> args)
        {
            if (args.Count == 0) return Error("usage: save NAME [--all-devices] [--apps-only]");

            var captureAllDevices = args.Any(a => string.Equals(a, "--all-devices", StringComparison.OrdinalIgnoreCase));
            var applyAppsOnly = args.Any(a => string.Equals(a, "--apps-only", StringComparison.OrdinalIgnoreCase));
            var nameParts = args.Where(a =>
                !string.Equals(a, "--all-devices", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(a, "--apps-only", StringComparison.OrdinalIgnoreCase)).ToList();
            var profileName = string.Join(" ", nameParts).Trim();
            if (string.IsNullOrWhiteSpace(profileName)) return Error("usage: save NAME [--all-devices] [--apps-only]");

            var settings = _getSettings();
            if (settings == null) return Error("settings not available");

            var collection = _getCollection();
            if (collection == null) return Error("audio not ready");

            var service = new VolumeProfileService(settings);
            var profile = service.CaptureCurrentState(
                profileName,
                collection,
                captureAllDevices ? VolumeProfileService.CaptureScope.AllDevices : VolumeProfileService.CaptureScope.CurrentDevice);
            profile.ApplyAppsOnly = applyAppsOnly;
            service.SaveProfile(profile);

            return JsonConvert.SerializeObject(new
            {
                ok = true,
                preset = profile.Name,
                slug = profile.Slug,
                captureScope = profile.CaptureScope.ToString(),
                applyAppsOnly = profile.ApplyAppsOnly,
                devices = profile.Devices?.Count ?? 0,
                apps = profile.Devices?.Sum(d => d.Apps?.Count ?? 0) ?? 0
            });
        }

        private string DeleteProfile(List<string> args)
        {
            if (args.Count == 0) return Error("usage: delete NAME");

            var settings = _getSettings();
            if (settings == null) return Error("settings not available");

            var profileName = string.Join(" ", args);
            var service = new VolumeProfileService(settings);
            var profile = service.FindProfile(profileName);
            if (profile == null) return Error($"QuickTrumpet preset not found: {profileName}");

            service.DeleteProfile(profile);
            return JsonConvert.SerializeObject(new { ok = true, preset = profile.Name });
        }

        private string ResolveApps(List<string> args)
        {
            if (args.Count == 0) return Error("usage: resolve-apps QUERY [QUERY...]");

            var collection = _getCollection();
            if (collection == null) return Error("audio not ready");

            var result = args.Select(query => new
            {
                query,
                matches = FindAppMatches(collection, query)
                    .Select(m => new
                    {
                        score = m.Score,
                        matchType = m.MatchType,
                        exeName = m.ExeName,
                        displayName = m.DisplayName,
                        appId = m.AppId,
                        device = m.DeviceName,
                        deviceId = m.DeviceId,
                        processId = m.ProcessId,
                        volume = m.Volume,
                        isMuted = m.IsMuted
                    })
                    .ToList()
            });

            return JsonConvert.SerializeObject(result);
        }

        private string PreviewRule(List<string> args)
        {
            var collection = _getCollection();
            if (collection == null) return Error("audio not ready");

            var parseError = TryParseRule(args, out var spec);
            if (parseError != null) return Error(parseError);

            var plan = BuildRulePlan(collection, spec);
            return JsonConvert.SerializeObject(new
            {
                ok = true,
                applyAppsOnly = spec.ApplyAppsOnly,
                captureScope = spec.CaptureAllDevices ? "AllDevices" : "CurrentDevice",
                changes = plan,
                appsMatched = plan.Select(p => p.AppId ?? $"{p.ExeName}:{p.ProcessId}").Distinct().Count(),
                warnings = BuildRuleWarnings(spec, plan)
            });
        }

        private string ApplyRule(List<string> args)
        {
            var collection = _getCollection();
            if (collection == null) return Error("audio not ready");

            var parseError = TryParseRule(args, out var spec);
            if (parseError != null) return Error(parseError);

            var plan = BuildRulePlan(collection, spec);
            ApplyRulePlan(collection, plan);

            return JsonConvert.SerializeObject(new
            {
                ok = true,
                appsChanged = plan.Count,
                changes = plan,
                warnings = BuildRuleWarnings(spec, plan)
            });
        }

        private string CreatePresetFromRule(List<string> args)
        {
            if (args.Count == 0) return Error("usage: preset-create NAME --keep APP=VOL [--others VOL] [--apps-only] [--all-devices]");

            var name = args[0];
            var ruleArgs = args.Skip(1).ToList();

            var settings = _getSettings();
            if (settings == null) return Error("settings not available");

            var collection = _getCollection();
            if (collection == null) return Error("audio not ready");

            var parseError = TryParseRule(ruleArgs, out var spec);
            if (parseError != null) return Error(parseError);

            var plan = BuildRulePlan(collection, spec);
            ApplyRulePlan(collection, plan);

            var service = new VolumeProfileService(settings);
            var profile = service.CaptureCurrentState(
                name,
                collection,
                spec.CaptureAllDevices ? VolumeProfileService.CaptureScope.AllDevices : VolumeProfileService.CaptureScope.CurrentDevice);
            profile.ApplyAppsOnly = spec.ApplyAppsOnly;
            service.SaveProfile(profile);

            return JsonConvert.SerializeObject(new
            {
                ok = true,
                preset = profile.Name,
                slug = profile.Slug,
                captureScope = profile.CaptureScope.ToString(),
                applyAppsOnly = profile.ApplyAppsOnly,
                appsChanged = plan.Count,
                appsCaptured = profile.Devices?.Sum(d => d.Apps?.Count ?? 0) ?? 0,
                changes = plan,
                warnings = BuildRuleWarnings(spec, plan)
            });
        }

        private string ExportSettings(List<string> args)
        {
            var settings = _getSettings();
            if (settings == null) return Error("settings not available");

            string filePath;
            if (args.Count > 0)
            {
                filePath = args[0];
            }
            else
            {
                filePath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"BetterTrumpet_Settings_{DateTime.Now:yyyy-MM-dd}.btsettings");
            }

            var success = DataModel.SettingsExportService.ExportToFile(settings, filePath);
            return JsonConvert.SerializeObject(new { ok = success, path = filePath });
        }

        private string ImportSettings(List<string> args)
        {
            if (args.Count == 0) return Error("usage: import-settings <path>");

            var filePath = args[0];
            if (!System.IO.File.Exists(filePath))
                return Error($"file not found: {filePath}");

            var settings = _getSettings();
            if (settings == null) return Error("settings not available");

            var success = DataModel.SettingsExportService.ImportFromFile(settings, filePath);
            return JsonConvert.SerializeObject(new { ok = success, path = filePath });
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

        private IAppItemViewModel ResolveApp(string appName)
        {
            var collection = _getCollection();
            if (collection == null) return null;

            return FindApp(collection, appName);
        }

        private static IAppItemViewModel FindApp(DeviceCollectionViewModel collection, string appName)
        {
            if (collection == null || string.IsNullOrWhiteSpace(appName)) return null;

            var apps = collection.AllDevices
                .Where(d => d != null)
                .SelectMany(d => d.Apps ?? Enumerable.Empty<IAppItemViewModel>())
                .Where(a => a != null)
                .ToList();
            return apps.FirstOrDefault(a => AppMatchesExact(a, appName))
                ?? apps.FirstOrDefault(a => AppMatchesPartial(a, appName));
        }

        private static bool AppMatches(IAppItemViewModel app, string appName)
        {
            return AppMatchesExact(app, appName) || AppMatchesPartial(app, appName);
        }

        private static bool AppMatchesExact(IAppItemViewModel app, string appName)
        {
            if (app == null || string.IsNullOrWhiteSpace(appName)) return false;

            var exeName = app.ExeName ?? string.Empty;
            var exeNoExt = System.IO.Path.GetFileNameWithoutExtension(exeName);
            return string.Equals(exeName, appName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(exeNoExt, appName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(app.DisplayName, appName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool AppMatchesPartial(IAppItemViewModel app, string appName)
        {
            if (app == null || string.IsNullOrWhiteSpace(appName)) return false;

            var exeName = app.ExeName ?? string.Empty;
            var displayName = app.DisplayName ?? string.Empty;
            return exeName.IndexOf(appName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   displayName.IndexOf(appName, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<RuleAppMatch> FindAppMatches(DeviceCollectionViewModel collection, string query)
        {
            if (collection == null || string.IsNullOrWhiteSpace(query)) return new List<RuleAppMatch>();

            return (collection.AllDevices ?? Enumerable.Empty<DeviceViewModel>())
                .Where(device => device != null)
                .SelectMany(device => (device.Apps ?? Enumerable.Empty<IAppItemViewModel>())
                    .Where(app => app != null)
                    .Select(app => new { device, app }))
                .Select(x => CreateAppMatch(x.device, x.app, query))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static RuleAppMatch CreateAppMatch(DeviceViewModel device, IAppItemViewModel app, string query)
        {
            if (device == null || app == null) return new RuleAppMatch { Query = query };

            var normalizedQuery = NormalizeToken(query);
            var exeName = app.ExeName ?? string.Empty;
            var exeNoExt = System.IO.Path.GetFileNameWithoutExtension(exeName);
            var displayName = app.DisplayName ?? string.Empty;
            var appId = app.AppId ?? string.Empty;

            var candidates = new[]
            {
                new { Value = exeName, Type = "exe" },
                new { Value = exeNoExt, Type = "exeName" },
                new { Value = displayName, Type = "displayName" },
                new { Value = appId, Type = "appId" },
            };

            var bestScore = 0;
            var bestType = "none";

            foreach (var candidate in candidates)
            {
                var value = NormalizeToken(candidate.Value);
                if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(normalizedQuery)) continue;

                int score;
                if (string.Equals(value, normalizedQuery, StringComparison.OrdinalIgnoreCase))
                {
                    score = 100;
                }
                else if (value.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                {
                    score = 80;
                }
                else if (value.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score = 60;
                }
                else
                {
                    score = 0;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestType = candidate.Type;
                }
            }

            return new RuleAppMatch
            {
                Query = query,
                MatchType = bestType,
                Score = bestScore,
                AppId = app.AppId,
                ExeName = app.ExeName,
                DisplayName = app.DisplayName,
                DeviceId = device.Id,
                DeviceName = device.DisplayName,
                ProcessId = app.ProcessId,
                Volume = app.Volume,
                IsMuted = app.IsMuted
            };
        }

        private static string NormalizeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return System.IO.Path.GetFileNameWithoutExtension(value.Trim()).ToLowerInvariant();
        }

        private static string TryParseRule(List<string> args, out RuleSpec spec)
        {
            spec = new RuleSpec();
            if (args == null || args.Count == 0)
            {
                return "usage: rule-preview --keep APP=VOL [--keep APP=VOL] [--others VOL] [--apps-only] [--current-device]";
            }

            for (var i = 0; i < args.Count; i++)
            {
                var arg = args[i];

                if (IsFlag(arg, "--apps-only"))
                {
                    spec.ApplyAppsOnly = true;
                    continue;
                }

                if (IsFlag(arg, "--all-devices"))
                {
                    spec.CaptureAllDevices = true;
                    continue;
                }

                if (IsFlag(arg, "--current-device"))
                {
                    spec.CaptureAllDevices = false;
                    continue;
                }

                if (IsFlag(arg, "--keep") || IsFlag(arg, "--app"))
                {
                    if (++i >= args.Count) return $"{arg} requires APP=VOL";
                    var assignmentError = TryParseAssignment(args[i], false, out var assignment);
                    if (assignmentError != null) return assignmentError;
                    spec.Keep.Add(assignment);
                    continue;
                }

                if (IsFlag(arg, "--others"))
                {
                    if (++i >= args.Count) return "--others requires VOL";
                    var assignmentError = TryParseAssignment(args[i], true, out var assignment);
                    if (assignmentError != null) return assignmentError;
                    spec.Others = assignment;
                    continue;
                }

                var bareAssignmentError = TryParseAssignment(arg, false, out var bareAssignment);
                if (bareAssignmentError == null && !bareAssignment.IsOthers)
                {
                    spec.Keep.Add(bareAssignment);
                    continue;
                }

                return $"unknown rule argument: {arg}";
            }

            if (spec.Keep.Count == 0 && spec.Others == null)
            {
                return "rule needs at least one --keep APP=VOL or --others VOL";
            }

            return null;
        }

        private static string TryParseAssignment(string value, bool isOthers, out RuleAssignment assignment)
        {
            assignment = new RuleAssignment { IsOthers = isOthers };
            if (string.IsNullOrWhiteSpace(value)) return "empty assignment";

            if (isOthers && value.IndexOf('=') < 0)
            {
                return TryParseTargetState(value, assignment);
            }

            var parts = value.Split(new[] { '=' }, 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
            {
                return "assignment must be APP=VOL, APP=mute, or APP=unmute";
            }

            assignment.Query = parts[0].Trim();
            assignment.IsOthers = isOthers || string.Equals(assignment.Query, "others", StringComparison.OrdinalIgnoreCase) || assignment.Query == "*";
            return TryParseTargetState(parts[1], assignment);
        }

        private static string TryParseTargetState(string value, RuleAssignment assignment)
        {
            if (string.IsNullOrWhiteSpace(value)) return "missing assignment value";
            var normalized = value.Trim().ToLowerInvariant();

            switch (normalized)
            {
                case "mute":
                case "muted":
                    assignment.IsMuted = true;
                    return null;
                case "unmute":
                case "unmuted":
                    assignment.IsMuted = false;
                    return null;
            }

            if (!int.TryParse(normalized.TrimEnd('%'), out var volume) || volume < 0 || volume > 100)
            {
                return "volume must be 0-100, mute, or unmute";
            }

            assignment.Volume = volume;
            assignment.IsMuted = false;
            return null;
        }

        private static List<RulePlanChange> BuildRulePlan(DeviceCollectionViewModel collection, RuleSpec spec)
        {
            var changes = new List<RulePlanChange>();
            var keepIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var assignment in spec.Keep)
            {
                var matches = FindAppMatches(collection, assignment.Query);
                foreach (var match in matches)
                {
                    var key = GetRuleMatchKey(match);
                    keepIds.Add(key);
                    changes.Add(CreateRulePlanChange(match, assignment, "keep"));
                }
            }

            if (spec.Others != null)
            {
                foreach (var device in collection.AllDevices ?? Enumerable.Empty<DeviceViewModel>())
                {
                    if (device == null) continue;

                    foreach (var app in device.Apps ?? Enumerable.Empty<IAppItemViewModel>())
                    {
                        if (app == null) continue;

                        var match = CreateAppMatch(device, app, app.ExeName ?? app.DisplayName);
                        if (keepIds.Contains(GetRuleMatchKey(match))) continue;
                        changes.Add(CreateRulePlanChange(match, spec.Others, "others"));
                    }
                }
            }

            return changes
                .GroupBy(c => c.AppId ?? $"{c.ExeName}:{c.ProcessId}:{c.DeviceId}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        private static RulePlanChange CreateRulePlanChange(RuleAppMatch match, RuleAssignment assignment, string rule)
        {
            return new RulePlanChange
            {
                Scope = "app",
                Rule = rule,
                Query = assignment.Query,
                MatchType = match.MatchType,
                AppId = match.AppId,
                ExeName = match.ExeName,
                DisplayName = match.DisplayName,
                DeviceId = match.DeviceId,
                DeviceName = match.DeviceName,
                ProcessId = match.ProcessId,
                VolumeBefore = match.Volume,
                VolumeAfter = assignment.Volume ?? match.Volume,
                IsMutedBefore = match.IsMuted,
                IsMutedAfter = assignment.IsMuted ?? match.IsMuted
            };
        }

        private static string GetRuleMatchKey(RuleAppMatch match)
        {
            return match.AppId ?? $"{match.ExeName}:{match.ProcessId}:{match.DeviceId}";
        }

        private static List<string> BuildRuleWarnings(RuleSpec spec, List<RulePlanChange> plan)
        {
            var warnings = new List<string>();
            foreach (var keep in spec.Keep)
            {
                if (!plan.Any(p => p.Rule == "keep" && string.Equals(p.Query, keep.Query, StringComparison.OrdinalIgnoreCase)))
                {
                    warnings.Add($"App not found: {keep.Query}");
                }
            }
            return warnings;
        }

        private static void ApplyRulePlan(DeviceCollectionViewModel collection, List<RulePlanChange> plan)
        {
            if (collection == null || plan == null) return;

            var apps = (collection.AllDevices ?? Enumerable.Empty<DeviceViewModel>())
                .Where(d => d != null)
                .SelectMany(d => d.Apps ?? Enumerable.Empty<IAppItemViewModel>())
                .Where(app => app != null)
                .ToList();
            foreach (var change in plan)
            {
                var targets = apps.Where(app =>
                    (!string.IsNullOrWhiteSpace(change.AppId) && string.Equals(app.AppId, change.AppId, StringComparison.OrdinalIgnoreCase)) ||
                    (string.IsNullOrWhiteSpace(change.AppId) &&
                     string.Equals(app.ExeName, change.ExeName, StringComparison.OrdinalIgnoreCase) &&
                     app.ProcessId == change.ProcessId)).ToList();

                foreach (var app in targets)
                {
                    app.Volume = change.VolumeAfter;
                    app.IsMuted = change.IsMutedAfter;
                }
            }
        }

        private static bool IsFlag(string value, string flag)
        {
            return string.Equals(value, flag, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, flag.TrimStart('-'), StringComparison.OrdinalIgnoreCase);
        }

        private static List<List<string>> SplitBatchCommands(List<string> args)
        {
            var commands = new List<List<string>>();
            List<string> current = null;

            foreach (var arg in args)
            {
                if (IsBatchCommandStart(arg))
                {
                    if (current != null && current.Count > 0)
                    {
                        commands.Add(current);
                    }

                    current = new List<string> { TrimCommandPrefix(arg) };
                }
                else
                {
                    if (current == null)
                    {
                        current = new List<string>();
                    }
                    current.Add(arg);
                }
            }

            if (current != null && current.Count > 0)
            {
                commands.Add(current);
            }

            return commands;
        }

        private static bool IsBatchCommandStart(string value)
        {
            var command = TrimCommandPrefix(value).ToLowerInvariant();
            switch (command)
            {
                case "list-devices":
                case "list-apps":
                case "resolve-apps":
                case "get-volume":
                case "set-volume":
                case "mute":
                case "unmute":
                case "toggle-mute":
                case "get-default":
                case "set-default":
                case "set-device":
                case "presets":
                case "apply":
                case "apply-profile":
                case "save":
                case "delete":
                case "rule-preview":
                case "rule-apply":
                case "preset-create":
                case "watch":
                case "check-update":
                case "doctor":
                case "volume":
                case "mode":
                    return true;
                default:
                    return false;
            }
        }

        private static string TrimCommandPrefix(string value)
        {
            return value?.TrimStart('-', '/') ?? string.Empty;
        }

        private static string QuoteIfNeeded(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            return value.IndexOf(' ') >= 0 ? $"\"{value}\"" : value;
        }

        private static List<string> NormalizeCommand(List<string> parts)
        {
            if (parts == null || parts.Count == 0) return parts;

            var first = TrimCommandPrefix(parts[0]).ToLowerInvariant();
            var rest = parts.Skip(1).ToList();

            switch (first)
            {
                case "mode":
                    return new[] { "apply" }.Concat(rest).ToList();

                case "volume":
                    return NormalizeVolumeAlias(rest);

                case "mute":
                case "unmute":
                case "toggle-mute":
                    if (rest.Count == 1 && !rest[0].StartsWith("-") && !rest[0].StartsWith("/"))
                    {
                        return new[] { first, "--app", rest[0] }.ToList();
                    }
                    return new[] { first }.Concat(rest).ToList();

                case "profile":
                case "preset":
                    return NormalizeProfileAlias(rest);

                case "rule":
                    return NormalizeRuleAlias(rest);

                case "device":
                    return NormalizeDeviceAlias(rest);

                case "apps":
                    if (rest.Count == 0 || string.Equals(rest[0], "list", StringComparison.OrdinalIgnoreCase)) return new List<string> { "list-apps" };
                    break;

                case "update":
                    if (rest.Count > 0 && string.Equals(rest[0], "check", StringComparison.OrdinalIgnoreCase)) return new List<string> { "check-update" };
                    break;

                case "settings":
                    if (rest.Count > 0 && string.Equals(rest[0], "export", StringComparison.OrdinalIgnoreCase)) return new[] { "export-settings" }.Concat(rest.Skip(1)).ToList();
                    if (rest.Count > 0 && string.Equals(rest[0], "import", StringComparison.OrdinalIgnoreCase)) return new[] { "import-settings" }.Concat(rest.Skip(1)).ToList();
                    break;
            }

            parts[0] = TrimCommandPrefix(parts[0]);
            return parts;
        }

        private static List<string> NormalizeVolumeAlias(List<string> args)
        {
            if (args.Count == 0) return new List<string> { "get-volume" };

            if (string.Equals(args[0], "get", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "get-volume" }.Concat(args.Skip(1)).ToList();
            }

            if (string.Equals(args[0], "set", StringComparison.OrdinalIgnoreCase))
            {
                args = args.Skip(1).ToList();
            }

            if (args.Count == 1)
            {
                return new List<string> { "set-volume", args[0] };
            }

            if (args.Count >= 2)
            {
                return new[] { "set-volume", args[1], "--app", args[0] }.Concat(args.Skip(2)).ToList();
            }

            return new List<string> { "get-volume" };
        }

        private static List<string> NormalizeProfileAlias(List<string> args)
        {
            if (args.Count == 0 || string.Equals(args[0], "list", StringComparison.OrdinalIgnoreCase)) return new List<string> { "presets" };
            if (string.Equals(args[0], "create", StringComparison.OrdinalIgnoreCase)) return new[] { "preset-create" }.Concat(args.Skip(1)).ToList();
            if (string.Equals(args[0], "load", StringComparison.OrdinalIgnoreCase) || string.Equals(args[0], "apply", StringComparison.OrdinalIgnoreCase)) return new[] { "apply" }.Concat(args.Skip(1)).ToList();
            if (string.Equals(args[0], "save", StringComparison.OrdinalIgnoreCase)) return new[] { "save" }.Concat(args.Skip(1)).ToList();
            if (string.Equals(args[0], "delete", StringComparison.OrdinalIgnoreCase)) return new[] { "delete" }.Concat(args.Skip(1)).ToList();
            return new[] { "apply" }.Concat(args).ToList();
        }

        private static List<string> NormalizeRuleAlias(List<string> args)
        {
            if (args.Count == 0) return new List<string> { "rule-preview" };
            if (string.Equals(args[0], "preview", StringComparison.OrdinalIgnoreCase)) return new[] { "rule-preview" }.Concat(args.Skip(1)).ToList();
            if (string.Equals(args[0], "apply", StringComparison.OrdinalIgnoreCase)) return new[] { "rule-apply" }.Concat(args.Skip(1)).ToList();
            return new[] { "rule-preview" }.Concat(args).ToList();
        }

        private static List<string> NormalizeDeviceAlias(List<string> args)
        {
            if (args.Count == 0 || string.Equals(args[0], "list", StringComparison.OrdinalIgnoreCase)) return new List<string> { "list-devices" };
            if (string.Equals(args[0], "default", StringComparison.OrdinalIgnoreCase)) return new List<string> { "get-default" };
            if (string.Equals(args[0], "set-default", StringComparison.OrdinalIgnoreCase) || string.Equals(args[0], "default-to", StringComparison.OrdinalIgnoreCase)) return new[] { "set-default" }.Concat(args.Skip(1)).ToList();
            return new[] { "set-default" }.Concat(args).ToList();
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
