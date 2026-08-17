using EarTrumpet.Logic;
using EarTrumpet.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Threading;
using EarTrumpet.Interop;

namespace EarTrumpet.UI.Helpers
{
    /// <summary>
    /// Polls the foreground window and applies <see cref="FocusLostSupervisor"/> to live sessions.
    /// </summary>
    public sealed class FocusLostService
    {
        private readonly DeviceCollectionViewModel _collection;
        private readonly AppSettings _settings;
        private readonly FocusLostSupervisor _supervisor = new FocusLostSupervisor();
        private readonly DispatcherTimer _timer;

        public FocusLostService(DeviceCollectionViewModel collection, AppSettings settings)
        {
            _collection = collection;
            _settings = settings;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _timer.Tick += (_, __) => Poll();
        }

        public void Start()
        {
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }
        }

        public void Stop()
        {
            _timer.Stop();
            Apply(FocusLostMode.Off, 0, 0);
        }

        private void Poll()
        {
            var hwnd = User32.GetForegroundWindow();
            uint pid = 0;
            if (hwnd != IntPtr.Zero)
            {
                User32.GetWindowThreadProcessId(hwnd, out pid);
            }

            var mode = FocusLostVolumePolicy.ResolveMode(
                _settings != null && _settings.UseFocusLostVolume,
                _settings?.FocusLostAttenuatePercent ?? 0);
            Apply(mode, (int)pid, _settings?.FocusLostAttenuatePercent ?? 0);
        }

        private void Apply(FocusLostMode mode, int foregroundPid, int attenuatePercent)
        {
            try
            {
                var sessions = new List<FocusLostSession>();
                var appsByKey = new Dictionary<string, IAppItemViewModel>(StringComparer.Ordinal);
                if (_collection?.AllDevices != null)
                {
                    foreach (var device in _collection.AllDevices)
                    {
                        if (device?.Apps == null)
                        {
                            continue;
                        }

                        foreach (var app in device.Apps)
                        {
                            if (app == null || string.IsNullOrEmpty(app.Id))
                            {
                                continue;
                            }

                            appsByKey[app.Id] = app;
                            var rule = _settings?.GetAppRule(app.ExeName);
                            var canAdjust = rule == null ||
                                            (!rule.HardMuted && rule.VolumeMode != AppSettings.VolumeRuleMode.Lock);
                            sessions.Add(new FocusLostSession(
                                app.Id,
                                app.ProcessId,
                                app.Volume,
                                app.IsMuted,
                                canAdjust));
                        }
                    }
                }

                var adjustments = _supervisor.OnForegroundChanged(
                    foregroundPid,
                    sessions,
                    mode,
                    attenuatePercent,
                    Environment.ProcessId);

                if (adjustments.Count == 0)
                {
                    return;
                }

                using (VolumeWriteScope.Begin())
                {
                    foreach (var adjustment in adjustments)
                    {
                        IAppItemViewModel app;
                        if (!appsByKey.TryGetValue(adjustment.Key, out app) || app == null)
                        {
                            continue;
                        }

                        if (app is AudioSessionViewModel session)
                        {
                            session.SetVolumeWithoutUndo(adjustment.Volume);
                            session.SetMuteWithoutUndo(adjustment.IsMuted);
                        }
                        else
                        {
                            if (app.Volume != adjustment.Volume)
                            {
                                app.Volume = adjustment.Volume;
                            }

                            if (app.IsMuted != adjustment.IsMuted)
                            {
                                app.IsMuted = adjustment.IsMuted;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"FocusLostService Apply failed: {ex.Message}");
            }
        }
    }
}
