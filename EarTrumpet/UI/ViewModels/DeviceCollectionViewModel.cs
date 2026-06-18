using EarTrumpet.DataModel.Audio;
using EarTrumpet.DataModel.WindowsAudio;
using EarTrumpet.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using System.Windows.Threading;

namespace EarTrumpet.UI.ViewModels
{
    public class DeviceCollectionViewModel : BindableBase
    {
        private static readonly string DefaultDeviceChangedProperty = "DefaultDeviceChangedProperty";
        private const int AppHideAnimationDurationMs = 140;

        public event EventHandler<DeviceViewModel> DefaultChanged;
        public event Action TrayPropertyChanged;

        public ObservableCollection<DeviceViewModel> AllDevices { get; private set; } = new ObservableCollection<DeviceViewModel>();
        public ObservableCollection<DeviceViewModel> VisibleDevices { get; private set; } = new ObservableCollection<DeviceViewModel>();
        public DeviceViewModel Default { get; private set; }

        private readonly IAudioDeviceManager _deviceManager;
        private readonly Timer _peakMeterTimer;
        private readonly AppSettings _settings;
        private readonly Dispatcher _currentDispatcher = Dispatcher.CurrentDispatcher;
        private bool _isFlyoutVisible;
        private bool _isFullWindowVisible;

        public DeviceCollectionViewModel(IAudioDeviceManager deviceManager, AppSettings settings)
        {
            _settings = settings;
            _settings.HiddenAppsChanged += OnHiddenAppsChanged;
            _settings.HiddenDevicesChanged += OnHiddenDevicesChanged;
            _deviceManager = deviceManager;
            _deviceManager.DefaultChanged += OnDefaultChanged;
            _deviceManager.Devices.CollectionChanged += OnCollectionChanged;
            OnCollectionChanged(null, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

            var fps = _settings?.EffectivePeakMeterFps ?? 30;
            _peakMeterTimer = new Timer(1000.0 / fps);
            _peakMeterTimer.AutoReset = true;
            _peakMeterTimer.Elapsed += PeakMeterTimer_Elapsed;

            // Update timer interval when eco mode or FPS setting changes
            if (_settings != null)
            {
                _settings.EcoModeChanged += () =>
                {
                    _peakMeterTimer.Interval = 1000.0 / (_settings.EffectivePeakMeterFps);
                };
            }
        }

        private void OnDefaultChanged(object sender, IAudioDevice newDevice)
        {
            if (newDevice == null)
            {
                SetDefault(null);
            }
            else
            {
                var device = AllDevices.FirstOrDefault(d => d.Id == newDevice.Id);
                if (device == null)
                {
                    AddDevice(newDevice);
                    device = AllDevices.FirstOrDefault(d => d.Id == newDevice.Id);
                }
                SetDefault(device);
            }
        }

        private void SetDefault(DeviceViewModel device)
        {
            if (Default != null)
            {
                Default.PropertyChanged -= OnDefaultDevicePropertyChanged;
            }

            Default = device;
            DefaultChanged?.Invoke(this, Default);

            if (Default != null)
            {
                Default.PropertyChanged += OnDefaultDevicePropertyChanged;
            }

            // Let clients know that even though no properties changed, the underlying object changed.
            OnDefaultDevicePropertyChanged(this, new PropertyChangedEventArgs(DefaultDeviceChangedProperty));
        }

        private void OnDefaultDevicePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == DefaultDeviceChangedProperty ||
                e.PropertyName == nameof(Default.Volume) ||
                e.PropertyName == nameof(Default.IsMuted) ||
                e.PropertyName == nameof(Default.DisplayName))
            {
                TrayPropertyChanged.Invoke();
            }
        }

        protected virtual void AddDevice(IAudioDevice device)
        {
            var newDevice = new DeviceViewModel(this, _deviceManager, _settings, device);
            AllDevices.AddSorted(newDevice, DeviceViewModel.CompareByDisplayName);

            // Add to VisibleDevices only if not hidden
            if (!_settings.IsDeviceHidden(device.Id))
            {
                VisibleDevices.AddSorted(newDevice, DeviceViewModel.CompareByDisplayName);
            }
        }

        public bool CanHideDevice(DeviceViewModel device)
        {
            // Don't allow hiding if it's the only visible device or if it's the default device
            return device != null && VisibleDevices.Count > 1 && device != Default;
        }

        public void HideDevice(DeviceViewModel device)
        {
            if (!CanHideDevice(device))
            {
                return;
            }

            _settings.HideDevice(device.Id, device.DisplayName);
        }

        public List<AppSettings.HiddenDeviceEntry> GetHiddenDevices()
        {
            return _settings.GetHiddenDevices();
        }

        public void UnhideDevice(string deviceId)
        {
            _settings.UnhideDevice(deviceId);
        }

        public void UnhideAllDevices()
        {
            _settings.UnhideAllDevices();
        }

        public int GetTotalHiddenDevicesCount()
        {
            return _settings.HiddenDevicesCount;
        }

        private void OnHiddenDevicesChanged()
        {
            RefreshVisibleDevices();
            TrayPropertyChanged?.Invoke();
        }

        private void RefreshVisibleDevices()
        {
            VisibleDevices.Clear();
            foreach (var device in AllDevices)
            {
                if (!_settings.IsDeviceHidden(device.Id))
                {
                    VisibleDevices.Add(device);
                }
            }
        }

        public bool CanHideApp(IAppItemViewModel app)
        {
            return app != null && app.Parent != null && !app.IsHiding && !(app is TemporaryAppItemViewModel);
        }

        public void HideAppOnDevice(IAppItemViewModel app)
        {
            if (!CanHideApp(app))
            {
                return;
            }

            var deviceId = app.Parent.Id;
            var appId = app.AppId;
            var exeName = app.ExeName;
            var displayName = app.DisplayName;

            if (!ShouldDelayAppHide())
            {
                _settings.HideAppForDevice(deviceId, appId, exeName, displayName);
                return;
            }

            app.IsHiding = true;

            var timer = new DispatcherTimer(DispatcherPriority.Normal, _currentDispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(AppHideAnimationDurationMs),
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                _settings.HideAppForDevice(deviceId, appId, exeName, displayName);
            };
            timer.Start();
        }

        private bool ShouldDelayAppHide()
        {
            return EarTrumpet.UI.Themes.Manager.Current?.AnimationsEnabled == true &&
                   (_settings?.UseSmoothVolumeAnimation ?? true) &&
                   !(_settings?.IsEffectiveEcoMode ?? false);
        }

        public List<AppSettings.HiddenAppEntry> GetHiddenAppsForDevice(string deviceId)
        {
            return _settings.GetHiddenAppsForDevice(deviceId);
        }

        public void UnhideAppOnDevice(string deviceId, string appId, string exeName)
        {
            _settings.UnhideAppForDevice(deviceId, appId, exeName);
        }

        public string GetHiddenAppLabel(AppSettings.HiddenAppEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry?.DisplayName))
            {
                return entry.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(entry?.ExeName))
            {
                return entry.ExeName;
            }

            if (!string.IsNullOrWhiteSpace(entry?.AppId))
            {
                return entry.AppId;
            }

            return Properties.Resources.SettingsHiddenAppsUnknown;
        }

        public void UnhideAllAppsForDevice(string deviceId)
        {
            _settings.UnhideAppsForDevice(deviceId);
        }

        public void UnhideAllApps()
        {
            _settings.UnhideAllApps();
        }

        public int GetTotalHiddenAppsCount()
        {
            return AllDevices.Sum(device => device.HiddenAppsCount);
        }

        private void OnHiddenAppsChanged()
        {
            foreach (var device in AllDevices)
            {
                device.RefreshHiddenApps();
            }

            TrayPropertyChanged?.Invoke();
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    var added = ((IAudioDevice)e.NewItems[0]);
                    var allExistingAdded = AllDevices.FirstOrDefault(d => d.Id == added.Id);
                    if (allExistingAdded == null)
                    {
                        AddDevice(added);
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    var removed = ((IAudioDevice)e.OldItems[0]).Id;
                    var allExisting = AllDevices.FirstOrDefault(d => d.Id == removed);
                    if (allExisting != null)
                    {
                        AllDevices.Remove(allExisting);
                        VisibleDevices.Remove(allExisting);
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    AllDevices.Clear();
                    VisibleDevices.Clear();
                    foreach (var device in _deviceManager.Devices)
                    {
                        AddDevice(device);
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        private void PeakMeterTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _deviceManager.UpdatePeakValues();

            _currentDispatcher.BeginInvoke((Action)(() =>
            {
                foreach (var device in AllDevices)
                {
                    device.UpdatePeakValueForeground();
                }
            }));
        }

        public void MoveAppToDevice(IAppItemViewModel app, DeviceViewModel dev)
        {
            // Collect all matching apps on all devices.
            var apps = new List<IAppItemViewModel>();
            apps.Add(app);

            foreach (var device in AllDevices)
            {
                foreach (var deviceApp in device.Apps)
                {
                    if (deviceApp.DoesGroupWith(app))
                    {
                        if (!apps.Contains(deviceApp))
                        {
                            apps.Add(deviceApp);
                            break;
                        }
                    }
                }
            }

            foreach (var foundApp in apps)
            {
                MoveAppToDeviceInternal(foundApp, dev);
            }

            // Collect and move any hidden/moved sessions.
            ((IAudioDeviceManagerWindowsAudio)_deviceManager).MoveHiddenAppsToDevice(app.AppId, dev?.Id);
        }

        private void MoveAppToDeviceInternal(IAppItemViewModel app, DeviceViewModel device)
        {
            var searchId = device?.Id;
            if (device == null)
            {
                searchId = _deviceManager.Default.Id;
            }

            try
            {
                DeviceViewModel oldDevice = AllDevices.First(d => d.Apps.Contains(app));
                DeviceViewModel newDevice = AllDevices.First(d => searchId == d.Id);

                bool isLogicallyMovingDevices = (oldDevice != newDevice);

                var tempApp = new TemporaryAppItemViewModel(this, _deviceManager, app);

                app.MoveToDevice(device?.Id, hide: isLogicallyMovingDevices);

                // Update the UI if the device logically changed places.
                if (isLogicallyMovingDevices)
                {
                    oldDevice.AppLeavingFromThisDevice(app);
                    newDevice.AppMovingToThisDevice(tempApp);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"DeviceCollectionViewModel MoveAppToDeviceInternal Failed: {ex}");
            }
        }

        private void StartOrStopPeakTimer()
        {
            _peakMeterTimer.Enabled = _isFlyoutVisible || _isFullWindowVisible;
        }

        public void OnTrayFlyoutShown()
        {
            _isFlyoutVisible = true;
            StartOrStopPeakTimer();
        }

        public void OnTrayFlyoutHidden()
        {
            _isFlyoutVisible = false;
            StartOrStopPeakTimer();
        }

        public void OnFullWindowClosed()
        {
            _isFullWindowVisible = false;
            StartOrStopPeakTimer();
        }

        public void OnFullWindowOpened()
        {
            _isFullWindowVisible = true;
            StartOrStopPeakTimer();
        }

        public string GetTrayToolTip()
        {
            if (Default != null)
            {
                var stateText = Default.IsMuted ? Properties.Resources.MutedText : $"{Default.Volume}%";
                var prefixText = $"BetterTrumpet: {stateText} - ";
                var deviceName = $"{Default.DeviceDescription} ({Default.EnumeratorName})";

                // Remote Audio devices may not contain an enumerator name or description.

                if (string.IsNullOrWhiteSpace(Default.EnumeratorName))
                {
                    deviceName = Default.DeviceDescription;
                }

                if (string.IsNullOrWhiteSpace(Default.DeviceDescription) && string.IsNullOrWhiteSpace(Default.EnumeratorName))
                {
                    deviceName = Default.DisplayName;
                }

                // Device name could be null in transient error cases
                if (deviceName == null)
                {
                    deviceName = "";
                }

                // API Limitation: "less than 64 chars" for the tooltip.
                deviceName = deviceName.Substring(0, Math.Min(63 - prefixText.Length, deviceName.Length));
                return prefixText + deviceName;
            }
            else
            {
                return Properties.Resources.NoDeviceTrayText;
            }
        }
    }
}
