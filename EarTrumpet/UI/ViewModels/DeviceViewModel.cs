using EarTrumpet.DataModel.Audio;
using EarTrumpet.DataModel.WindowsAudio;
using EarTrumpet.Extensions;
using EarTrumpet.Interop.MMDeviceAPI;
using EarTrumpet.Logic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace EarTrumpet.UI.ViewModels
{
    public class DeviceViewModel : AudioSessionViewModel, IDeviceViewModel
    {
        public class DisplayNameComparer : IComparer<DeviceViewModel>
        {
            public int Compare(DeviceViewModel one, DeviceViewModel two)
            {
                return string.Compare(one.DisplayName, two.DisplayName, StringComparison.CurrentCultureIgnoreCase);
            }
        }

        public static readonly DisplayNameComparer CompareByDisplayName = new DisplayNameComparer();

        public enum DeviceIconKind
        {
            Mute,
            Bar0,
            Bar1,
            Bar2,
            Bar3,
            Microphone,
        }

        public override string DisplayName => _device.DisplayName;
        protected override bool IsDevice => true;
        public string AccessibleName => IsMuted ? Properties.Resources.AppOrDeviceMutedFormatAccessibleText.Replace("{Name}", DisplayName) :
            Properties.Resources.AppOrDeviceFormatAccessibleText.Replace("{Name}", DisplayName).Replace("{Volume}", Volume.ToString());
        public string DeviceDescription => ((IAudioDeviceWindowsAudio)_device).DeviceDescription;
        public string EnumeratorName => ((IAudioDeviceWindowsAudio)_device).EnumeratorName;
        public string InterfaceName => ((IAudioDeviceWindowsAudio)_device).InterfaceName;
        public ObservableCollection<IAppItemViewModel> Apps { get; }
        public int HiddenAppsCount
        {
            get => _hiddenAppsCount;
            private set
            {
                if (_hiddenAppsCount != value)
                {
                    _hiddenAppsCount = value;
                    RaisePropertyChanged(nameof(HiddenAppsCount));
                    RaisePropertyChanged(nameof(HasHiddenApps));
                }
            }
        }
        public bool HasHiddenApps => HiddenAppsCount > 0;

        public bool IsDisplayNameVisible
        {
            get => _isDisplayNameVisible;
            set
            {
                if (_isDisplayNameVisible != value)
                {
                    _isDisplayNameVisible = value;
                    RaisePropertyChanged(nameof(IsDisplayNameVisible));
                }
            }
        }

        public DeviceIconKind IconKind
        {
            get => _iconKind;
            set
            {
                if (_iconKind != value)
                {
                    _iconKind = value;
                    RaisePropertyChanged(nameof(IconKind));
                }
            }
        }

        protected readonly IAudioDevice _device;
        protected readonly IAudioDeviceManager _deviceManager;
        protected readonly WeakReference<DeviceCollectionViewModel> _parent;
        private readonly AppSettings _settings;
        private bool _isDisplayNameVisible;
        private DeviceIconKind _iconKind;
        private int _hiddenAppsCount;

        public DeviceViewModel(DeviceCollectionViewModel parent, IAudioDeviceManager deviceManager, AppSettings settings, IAudioDevice device) : base(device)
        {
            _deviceManager = deviceManager;
            _settings = settings;
            _device = device;
            _parent = new WeakReference<DeviceCollectionViewModel>(parent);
            Apps = new ObservableCollection<IAppItemViewModel>();

            _device.PropertyChanged += OnPropertyChanged;
            _device.Groups.CollectionChanged += OnCollectionChanged;

            RebuildAppsCollection();
            RefreshHiddenCount();

            UpdateMasterVolumeIcon();
        }

        ~DeviceViewModel()
        {
            _device.PropertyChanged -= OnPropertyChanged;
            _device.Groups.CollectionChanged -= OnCollectionChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_device.IsMuted) ||
                e.PropertyName == nameof(_device.Volume))
            {
                UpdateMasterVolumeIcon();
                RaisePropertyChanged(nameof(AccessibleName));
            }
            else if (e.PropertyName == nameof(_device.DisplayName))
            {
                RaisePropertyChanged(nameof(DisplayName));
                RaisePropertyChanged(nameof(AccessibleName));
            }
        }

        public override void UpdatePeakValueForeground()
        {
            base.UpdatePeakValueForeground();

            foreach (var app in Apps)
            {
                app.UpdatePeakValueForeground();
            }
        }

        private void UpdateMasterVolumeIcon()
        {
            if (_device.Parent.Kind == AudioDeviceKind.Recording.ToString())
            {
                IconKind = DeviceIconKind.Microphone;
            }
            else
            {
                var isOnWindows11 = Environment.OSVersion.IsAtLeast(OSVersions.Windows11);
                if (_device.IsMuted)
                {
                    IconKind = DeviceIconKind.Mute;
                }
                else if (isOnWindows11 && _device.Volume > 0.66f)
                {
                    IconKind = DeviceIconKind.Bar3;
                }
                else if (!isOnWindows11 && _device.Volume >= 0.66f)
                {
                    IconKind = DeviceIconKind.Bar3;
                }
                else if (isOnWindows11 && _device.Volume > 0.33f)
                {
                    IconKind = DeviceIconKind.Bar2;
                }
                else if (!isOnWindows11 && _device.Volume >= 0.33f)
                {
                    IconKind = DeviceIconKind.Bar2;
                }
                else if (_device.Volume > 0.00f)
                {
                    IconKind = DeviceIconKind.Bar1;
                }
                else
                {
                    IconKind = DeviceIconKind.Bar0;
                }
            }
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    Debug.Assert(e.NewItems.Count == 1);
                    AddSession((IAudioDeviceSession)e.NewItems[0], true);
                    break;

                case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                    Debug.Assert(e.OldItems.Count == 1);
                    var existing = Apps.FirstOrDefault(x => x.Id == ((IAudioDeviceSession)e.OldItems[0]).Id);
                    if (existing != null)
                    {
                        StopWatchingApp(existing);
                        Apps.Remove(existing);
                    }
                    break;

                case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                case System.Collections.Specialized.NotifyCollectionChangedAction.Move:
                case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                    RebuildAppsCollection();
                    break;

                default:
                    Trace.WriteLine($"DeviceViewModel OnCollectionChanged ignored action {e.Action}");
                    break;
            }
        }

        private void AddSession(IAudioDeviceSession session, bool animateOnLoad)
        {
            if (_settings != null && _settings.IsAppHiddenForDevice(_device.Id, session.AppId, session.ExeName))
            {
                return;
            }

            var newSession = new AppItemViewModel(this, session, animateOnLoad: animateOnLoad);

            foreach (var app in Apps)
            {
                if (app.DoesGroupWith(newSession))
                {
                    newSession.Volume = app.Volume;
                    newSession.IsMuted = app.IsMuted;
                    StopWatchingApp(app);
                    Apps.Remove(app);
                    break;
                }
            }

            // Rules are applied after the merge above, not before: the merge copies the
            // existing group's volume and mute onto the new session and would overwrite them.
            ApplyRuleToApp(newSession, isNewSession: true);

            StartWatchingApp(newSession);
            Apps.AddSorted(newSession, AppItemViewModel.CompareByExeName);
        }

        private void RebuildAppsCollection()
        {
            foreach (var app in Apps)
            {
                StopWatchingApp(app);
            }

            Apps.Clear();
            foreach (var session in _device.Groups)
            {
                AddSession(session, false);
            }
        }

        private void ReconcileAppsWithHiddenState()
        {
            foreach (var app in Apps.ToArray())
            {
                if (_settings != null && _settings.IsAppHiddenForDevice(_device.Id, app.AppId, app.ExeName))
                {
                    if (app is TemporaryAppItemViewModel temporaryApp)
                    {
                        temporaryApp.Expired -= OnAppExpired;
                    }

                    StopWatchingApp(app);
                    Apps.Remove(app);
                }
            }

            foreach (var session in _device.Groups)
            {
                if (_settings != null && _settings.IsAppHiddenForDevice(_device.Id, session.AppId, session.ExeName))
                {
                    continue;
                }

                if (!Apps.Any(app => AppMatchesSession(app, session)))
                {
                    AddSession(session, true);
                }
            }
        }

        private static bool AppMatchesSession(IAppItemViewModel app, IAudioDeviceSession session)
        {
            return string.Equals(app.Id, session.Id, StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrWhiteSpace(app.AppId) && string.Equals(app.AppId, session.AppId, StringComparison.OrdinalIgnoreCase));
        }

        private void RefreshHiddenCount()
        {
            HiddenAppsCount = _settings?.GetHiddenAppCountForDevice(_device.Id) ?? 0;
        }

        internal void RefreshHiddenApps()
        {
            ReconcileAppsWithHiddenState();
            RefreshHiddenCount();
        }

        /// <summary>
        /// Re-asserts the standing rules (hard mute, volume Lock) on the sessions that are
        /// already live. Launch is deliberately not re-applied here: this runs on every rule
        /// change, and re-running Launch would reset the volume of unrelated apps.
        /// </summary>
        internal void ApplyAppRules()
        {
            foreach (var app in Apps)
            {
                ApplyRuleToApp(app, isNewSession: false);
            }
        }

        /// <summary>
        /// Applies folder defaults immediately to matching live sessions that do not have their
        /// own volume rule. This is intentionally separate from ApplyAppRules so changing a
        /// folder setting cannot reset unrelated launch rules.
        /// </summary>
        internal void ApplyFolderVolumeDefaults()
        {
            foreach (var app in Apps)
            {
                var explicitRule = _settings?.GetAppRule(app.ExeName);
                if (explicitRule?.HasVolumeRule == true || !TryGetFolderDefaultVolume(app, out var volumePercent))
                {
                    continue;
                }

                LaunchVolumeTracker.Release(app.ProcessId);
                if (LaunchVolumeTracker.TryClaim(app.ProcessId) && app.Volume != volumePercent)
                {
                    SetVolumeSilently(app, volumePercent);
                }
            }
        }

        /// <summary>
        /// Applies one app's rule right now, including Launch, and re-arms the launch
        /// tracker so a freshly edited rule takes effect without waiting for a relaunch.
        /// </summary>
        internal void ApplyRuleToAppNow(IAppItemViewModel app)
        {
            LaunchVolumeTracker.Release(app.ProcessId);
            ApplyRuleToApp(app, isNewSession: true);
        }

        private void ApplyRuleToApp(IAppItemViewModel app, bool isNewSession)
        {
            var rule = _settings?.GetAppRule(app.ExeName);
            var isLocked = rule?.VolumeMode == AppSettings.VolumeRuleMode.Lock;

            // Set unconditionally, including when the rule is gone: this is what re-enables
            // the slider in the flyout after the user removes a Lock.
            if (app is AppItemViewModel appItem)
            {
                appItem.IsVolumeLocked = isLocked;
            }

            if (isLocked)
            {
                if (app.Volume != rule.VolumePercent)
                {
                    SetVolumeSilently(app, rule.VolumePercent);
                }
            }
            else if (rule?.VolumeMode == AppSettings.VolumeRuleMode.Launch &&
                     isNewSession &&
                     LaunchVolumeTracker.TryClaim(app.ProcessId))
            {
                SetVolumeSilently(app, rule.VolumePercent);
            }
            else if ((rule == null || !rule.HasVolumeRule) &&
                     isNewSession &&
                     TryGetFolderDefaultVolume(app, out var folderVolumePercent) &&
                     LaunchVolumeTracker.TryClaim(app.ProcessId))
            {
                SetVolumeSilently(app, folderVolumePercent);
            }
            else if ((rule == null || !rule.HasVolumeRule) &&
                     isNewSession &&
                     RemoteDesktopIdentity.IsRemoteDesktopExe(app.ExeName) &&
                     _settings != null &&
                     _settings.TryGetRemoteDesktopVolume(out var remoteDesktopVolume) &&
                     LaunchVolumeTracker.TryClaim(app.ProcessId))
            {
                SetVolumeSilently(app, remoteDesktopVolume);
            }

            // Must come after the volume write: setting a non-zero volume clears the
            // session's mute (AudioDeviceSession.Volume setter), which would undo a hard mute.
            if (rule?.HardMuted == true && !app.IsMuted)
            {
                app.IsMuted = true;
            }
        }

        private bool TryGetFolderDefaultVolume(IAppItemViewModel app, out int volumePercent)
        {
            volumePercent = 0;
            return _settings != null &&
                   _settings.TryGetFolderVolume(app.AppId, out volumePercent);
        }

        // Rule enforcement can fire repeatedly, so it must not record undo steps.
        private static void SetVolumeSilently(IAppItemViewModel app, int volumePercent)
        {
            if (app is AudioSessionViewModel session)
            {
                session.SetVolumeWithoutUndo(volumePercent);
            }
            else
            {
                app.Volume = volumePercent;
            }
        }

        private void StartWatchingApp(IAppItemViewModel app)
        {
            app.PropertyChanged += App_PropertyChanged;
        }

        private void StopWatchingApp(IAppItemViewModel app)
        {
            app.PropertyChanged -= App_PropertyChanged;
        }

        // Keeps Lock and hard mute honest against changes we didn't make (the app itself,
        // the Windows mixer, another tool). Re-entrancy stops on its own: once the value
        // matches the rule, the comparisons below are false and nothing is written.
        private void App_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(IAppItemViewModel.Volume) &&
                e.PropertyName != nameof(IAppItemViewModel.IsMuted))
            {
                return;
            }

            if (sender is IAppItemViewModel app)
            {
                if (e.PropertyName == nameof(IAppItemViewModel.Volume) &&
                    _settings != null &&
                    RemoteDesktopIdentity.IsRemoteDesktopExe(app.ExeName) &&
                    !VolumeWriteScope.IsActive)
                {
                    _settings.RemoteDesktopLastVolume = app.Volume;
                }

                ApplyRuleToApp(app, isNewSession: false);
            }
        }

        public void AppMovingToThisDevice(TemporaryAppItemViewModel app)
        {
            app.Expired += OnAppExpired;

            foreach (var childApp in app.ChildApps)
            {
                ((IAudioDeviceManagerWindowsAudio)_deviceManager).UnhideSessionsForProcessId(_device.Id, childApp.ProcessId);
            }

            bool hasExistingAppGroup = false;
            foreach (var a in Apps)
            {
                if (a.DoesGroupWith(app))
                {
                    hasExistingAppGroup = true;
                    break;
                }
            }

            var isHiddenOnThisDevice = _settings != null && _settings.IsAppHiddenForDevice(_device.Id, app.AppId, app.ExeName);
            if (!hasExistingAppGroup && !isHiddenOnThisDevice)
            {
                Apps.AddSorted(app, AppItemViewModel.CompareByExeName);
            }
        }

        private void OnAppExpired(object sender, EventArgs e)
        {
            var app = (TemporaryAppItemViewModel)sender;
            if (Apps.Contains(app))
            {
                app.Expired -= OnAppExpired;
                StopWatchingApp(app);
                Apps.Remove(app);
            }
        }

        internal void AppLeavingFromThisDevice(IAppItemViewModel app)
        {
            if (app is TemporaryAppItemViewModel)
            {
                StopWatchingApp(app);
                Apps.Remove(app);
            }
        }

        public void MakeDefaultDevice() => _deviceManager.Default = _device;
        public bool TryMakeDefaultDevice(ERole role) =>
            _deviceManager is IAudioDeviceManagerWindowsAudio manager && manager.SetDefaultDevice(_device, role);
        public void IncrementVolume(int delta) => Volume += delta;
        public override string ToString() => AccessibleName;
    }
}
