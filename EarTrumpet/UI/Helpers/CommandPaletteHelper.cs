using EarTrumpet.Interop.Helpers;
using EarTrumpet.UI.Views;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Entry = EarTrumpet.UI.Views.CommandPaletteWindow.CommandEntry;

namespace EarTrumpet.UI.Helpers
{
    /// <summary>
    /// Builds the command list and shows the Command Palette window.
    /// Triggered by Ctrl+Shift+P from flyout or full window.
    /// </summary>
    public static class CommandPaletteHelper
    {
        private static CommandPaletteWindow _current;

        public static void Show()
        {
            // If already open, just focus it
            if (_current != null)
            {
                try { _current.Activate(); return; } catch { _current = null; }
            }

            var commands = BuildCommands();
            _current = new CommandPaletteWindow(commands);
            _current.Closed += (_, __) => _current = null;
            _current.Show();
        }

        private static List<Entry> BuildCommands()
        {
            var app = (App)Application.Current;
            var list = new List<Entry>();

            // ─── Undo / Redo ───
            list.Add(new Entry
            {
                Name = "Undo",
                Shortcut = "Ctrl+Z",
                SearchTokens = new[] { "undo", "revert", "back" },
                Execute = () =>
                {
                    var action = App.UndoService.Undo();
                    if (action != null) UndoRedoHelper.ApplyAction(action, isUndo: true);
                }
            });
            list.Add(new Entry
            {
                Name = "Redo",
                Shortcut = "Ctrl+Y",
                SearchTokens = new[] { "redo", "forward" },
                Execute = () =>
                {
                    var action = App.UndoService.Redo();
                    if (action != null) UndoRedoHelper.ApplyAction(action, isUndo: false);
                }
            });

            // ─── Windows ───
            list.Add(new Entry
            {
                Name = "Open Volume Mixer",
                Shortcut = "",
                SearchTokens = new[] { "mixer", "full", "window", "volume" },
                Execute = () => app.OpenMixerWindow()
            });
            list.Add(new Entry
            {
                Name = "Open Settings",
                Shortcut = "",
                SearchTokens = new[] { "settings", "preferences", "config", "options" },
                Execute = () =>
                {
                    // Open settings via tray icon context menu approach
                    var fields = app.GetType().GetField("_settingsWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (fields != null)
                    {
                        var holder = fields.GetValue(app) as WindowHolder;
                        holder?.OpenOrBringToFront();
                    }
                }
            });

            // ─── System sound panels ───
            list.Add(new Entry
            {
                Name = "Open Playback Devices",
                Shortcut = "",
                SearchTokens = new[] { "playback", "output", "speakers", "devices", "control panel" },
                Execute = () => LegacyControlPanelHelper.Open("playback")
            });
            list.Add(new Entry
            {
                Name = "Open Recording Devices",
                Shortcut = "",
                SearchTokens = new[] { "recording", "input", "microphone", "mic", "devices" },
                Execute = () => LegacyControlPanelHelper.Open("recording")
            });
            list.Add(new Entry
            {
                Name = "Open Sound Settings",
                Shortcut = "",
                SearchTokens = new[] { "sound", "settings", "windows", "system" },
                Execute = () => SettingsPageHelper.Open("sound")
            });

            // ─── Device actions ───
            var collection = app.CollectionViewModel;
            if (collection != null)
            {
                foreach (var device in collection.AllDevices)
                {
                    var dev = device; // capture
                    list.Add(new Entry
                    {
                        Name = $"Mute/Unmute: {dev.DisplayName}",
                        Shortcut = "",
                        SearchTokens = new[] { "mute", "unmute", "toggle", dev.DisplayName.ToLowerInvariant() },
                        Execute = () =>
                        {
                            App.UndoService.RecordMuteChange(dev.Id, dev.DisplayName, true, dev.IsMuted, !dev.IsMuted);
                            dev.IsMuted = !dev.IsMuted;
                        }
                    });
                    list.Add(new Entry
                    {
                        Name = $"Set Default: {dev.DisplayName}",
                        Shortcut = "",
                        SearchTokens = new[] { "default", "output", "device", dev.DisplayName.ToLowerInvariant() },
                        Execute = () =>
                        {
                            // Use device manager to set default
                            Trace.WriteLine($"CommandPalette: Set default device to {dev.DisplayName}");
                            // Setting default requires IAudioDeviceManager which we access via reflection
                            var field = app.GetType().GetField("_deviceManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (field != null)
                            {
                                var mgr = field.GetValue(app) as DataModel.Audio.IAudioDeviceManager;
                                var audioDevice = mgr?.Devices.FirstOrDefault(d => d.Id == dev.Id);
                                if (audioDevice != null)
                                {
                                    mgr.Default = audioDevice;
                                }
                            }
                        }
                    });

                    // App-level mute for each visible app
                    foreach (var appItem in dev.Apps)
                    {
                        var a = appItem; // capture
                        var devName = dev.DisplayName;
                        list.Add(new Entry
                        {
                            Name = $"Mute/Unmute: {a.DisplayName}",
                            Shortcut = "",
                            SearchTokens = new[] { "mute", "unmute", "app", a.DisplayName.ToLowerInvariant(), a.ExeName?.ToLowerInvariant() ?? "" },
                            Execute = () =>
                            {
                                App.UndoService.RecordMuteChange(a.Id, a.DisplayName, false, a.IsMuted, !a.IsMuted);
                                a.IsMuted = !a.IsMuted;
                            }
                        });
                    }
                }
            }

            // ─── Quit ───
            list.Add(new Entry
            {
                Name = "Exit BetterTrumpet",
                Shortcut = "",
                SearchTokens = new[] { "exit", "quit", "close", "shutdown" },
                Execute = () => Application.Current.Shutdown()
            });

            return list;
        }
    }
}
