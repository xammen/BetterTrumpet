using EarTrumpet.DataModel;
using EarTrumpet.UI.Helpers;
using EarTrumpet.Interop.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace EarTrumpet.UI.ViewModels
{
    public class FlyoutViewModel : BindableBase, IPopupHostViewModel, IFlyoutViewModel, IDisposable
    {
        private bool _disposed = false;
        public event EventHandler<object> WindowSizeInvalidated;
        public event EventHandler<object> StateChanged;

        public ModalDialogViewModel Dialog { get; }
        public bool IsExpanded { get; private set; }
        public bool IsExpandingOrCollapsing { get; private set; }
        // True for exactly one open cycle when that open was triggered by expand/collapse,
        // so the view swaps the content inside the visible surface instead of a full entrance.
        public bool IsReopeningFromExpand { get; set; }
        public bool CanExpand => _mainViewModel.VisibleDevices.Count > 1;
        public string DeviceNameText => Devices.Count > 0 ? Devices[0].DisplayName : null;
        public FlyoutViewState State { get; private set; }
        public ObservableCollection<DeviceViewModel> Devices { get; private set; }
        public ICommand ExpandCollapse { get; private set; }
        public ICommand TogglePin { get; private set; }
        public ObservableCollection<ContextMenuItem> RestoreHiddenAppsMenu { get; }
        public ObservableCollection<ContextMenuItem> RestoreHiddenDevicesMenu { get; }
        public bool HasHiddenAppsForHeaderDevice => RestoreHiddenAppsMenu.Count > 0;
        public bool HasHiddenDevices => RestoreHiddenDevicesMenu.Count > 0;
        public InputType LastInput { get; private set; }
        public bool WasLastInputMouse => LastInput == InputType.Mouse;
        public ICommand DisplaySettingsChanged { get; }

        private bool _isUpdateAvailable;
        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            set
            {
                if (_isUpdateAvailable != value)
                {
                    _isUpdateAvailable = value;
                    RaisePropertyChanged(nameof(IsUpdateAvailable));
                    RaisePropertyChanged(nameof(UpdateText));
                }
            }
        }
        public string UpdateText => _updateService?.UpdateText ?? "";
        public bool IsUpdateBusy => _updateService?.IsBusy ?? false;

        private DataModel.UpdateService _updateService;
        public void SetUpdateService(DataModel.UpdateService svc)
        {
            _updateService = svc;
            svc.PropertyChanged += (_, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(DataModel.UpdateService.IsUpdateAvailable):
                        _currentDispatcher.BeginInvoke(() => IsUpdateAvailable = svc.IsUpdateAvailable);
                        break;
                    case nameof(DataModel.UpdateService.UpdateText):
                        _currentDispatcher.BeginInvoke(() =>
                        {
                            RaisePropertyChanged(nameof(UpdateText));
                            RaisePropertyChanged(nameof(IsUpdateBusy));
                        });
                        break;
                }
            };
            IsUpdateAvailable = svc.IsUpdateAvailable;
        }

        public void OpenUpdatePage()
        {
            _updateService?.DownloadAndInstallAsync();
        }

        private bool _isPinned;
        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (_isPinned != value)
                {
                    _isPinned = value;
                    RaisePropertyChanged(nameof(IsPinned));
                    RaisePropertyChanged(nameof(PinGlyph));
                }
            }
        }
        public string PinGlyph => IsPinned ? "\xE841" : "\xE840";

        private readonly DeviceCollectionViewModel _mainViewModel;
        private readonly DispatcherTimer _deBounceTimer;
        private readonly DispatcherTimer _invalidateSizeDebounceTimer;
        private readonly Dispatcher _currentDispatcher = Dispatcher.CurrentDispatcher;
private readonly Action _returnFocusToTray;
        private readonly AppSettings _settings;
        private bool _openAfterClose;
        private InputType _openAfterCloseInput;
        // Timestamp of the last light-dismiss deactivation. A tray click on an already-open
        // flyout first deactivates it (closing it), then delivers the click — without this
        // guard that trailing click re-opens the flyout. Used to absorb that click.
        private DateTime _lastDeactivatedAt = DateTime.MinValue;
        private MouseHook _mh;
        private Rect _winRect;

        public FlyoutViewModel(DeviceCollectionViewModel mainViewModel, Action returnFocusToTray, AppSettings settings)
        {
            _settings = settings;
            IsExpanded = _settings.IsExpanded;
            Dialog = new ModalDialogViewModel();
            Devices = new ObservableCollection<DeviceViewModel>();
            RestoreHiddenAppsMenu = new ObservableCollection<ContextMenuItem>();
            RestoreHiddenDevicesMenu = new ObservableCollection<ContextMenuItem>();
            _returnFocusToTray = returnFocusToTray;
            _mainViewModel = mainViewModel;
            _mainViewModel.DefaultChanged += OnDefaultPlaybackDeviceChanged;
            _mainViewModel.VisibleDevices.CollectionChanged += AllDevices_CollectionChanged;
            _settings.HiddenAppsChanged += OnHiddenAppsChanged;
            _settings.HiddenDevicesChanged += OnHiddenDevicesChanged;

            // This timer is used to enable clicking on the tray icon while the flyout is open, and not causing a
            // rapid hide and show cycle.  This time represents the minimum time between which the flyout may be opened.
            _deBounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _deBounceTimer.Tick += OnDeBounceTimerTick;

            // Coalesce rapid InvalidateWindowSize calls during device churn so the
            // VirtualizingStackPanel never sees a partially-populated ObservableCollection.
            _invalidateSizeDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _invalidateSizeDebounceTimer.Tick += OnInvalidateSizeDebounceTick;

            AllDevices_CollectionChanged(null, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

            ExpandCollapse = new RelayCommand(() =>
            {
                IsExpandingOrCollapsing = true;
                BeginClose(LastInput);
            });
            TogglePin = new RelayCommand(() => IsPinned = !IsPinned);
            DisplaySettingsChanged = new RelayCommand(() => BeginClose(InputType.Command));

            _mh = new MouseHook();
            _mh.MouseWheelEvent += OnMouseWheelEvent;
        }

        public void UpdateWindowPos(double top, double left, double height, double width)
        {
            _winRect = new Rect(left, top, width, height);
        }

        private void OnDeBounceTimerTick(object sender, EventArgs e)
        {
            Debug.Assert(State == FlyoutViewState.Closing_Stage2);
            _deBounceTimer.Stop();
            ChangeState(FlyoutViewState.Hidden);
        }

        private void AddDevice(DeviceViewModel device)
        {
            if (IsExpanded || Devices.Count == 0)
            {
                // Collapsed mode shows exactly one device: always prefer the default so a
                // transient device arrival (e.g. a headset coming online) cannot become the
                // stuck collapsed header instead of the actual default device.
                if (!IsExpanded && _mainViewModel.Default != null && device != _mainViewModel.Default)
                {
                    return;
                }

                device.Apps.CollectionChanged += Apps_CollectionChanged;
                Devices.Insert(0, device);
            }
        }

        private void Apps_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Remove:
                    if (Dialog.Focused is FocusedAppItemViewModel &&
                        (IAppItemViewModel)e.OldItems[0] == ((FocusedAppItemViewModel)Dialog.Focused)?.App)
                    {
                        Dialog.IsVisible = false;
                    }
                    break;
            }

            InvalidateWindowSize();
        }

        private void RemoveDevice(string id)
        {
            var existing = Devices.FirstOrDefault(d => d.Id == id);
            if (existing != null)
            {
                existing.Apps.CollectionChanged -= Apps_CollectionChanged;
                Devices.Remove(existing);
            }
        }

        private void AllDevices_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    AddDevice((DeviceViewModel)e.NewItems[0]);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    RemoveDevice(((DeviceViewModel)e.OldItems[0]).Id);
                    break;

                case NotifyCollectionChangedAction.Reset:
                    for (int i = Devices.Count - 1; i >= 0; i--)
                    {
                        RemoveDevice(Devices[i].Id);
                    }

                    foreach (var device in _mainViewModel.VisibleDevices)
                    {
                        AddDevice(device);
                    }

                    OnDefaultPlaybackDeviceChanged(null, _mainViewModel.Default);
                    break;

                default:
                    Trace.WriteLine($"FlyoutViewModel OnCollectionChanged ignored action {e.Action}");
                    break;
            }

            UpdateTextVisibility();
            RaiseDevicesChanged();
        }

        private void RaiseDevicesChanged()
        {
            RaisePropertyChanged(nameof(IsExpanded));
            RaisePropertyChanged(nameof(CanExpand));
            RaisePropertyChanged(nameof(DeviceNameText));
            RebuildRestoreHiddenAppsMenu();
            RebuildRestoreHiddenDevicesMenu();
            InvalidateWindowSize();
        }

        private void OnHiddenAppsChanged()
        {
            // OPTIMIZATION: Check if already on UI thread to avoid unnecessary BeginInvoke
            if (_currentDispatcher.CheckAccess())
            {
                RebuildRestoreHiddenAppsMenu();
            }
            else
            {
                _currentDispatcher.BeginInvoke((Action)(RebuildRestoreHiddenAppsMenu));
            }
        }

        private void OnHiddenDevicesChanged()
        {
            // OPTIMIZATION: Check if already on UI thread to avoid unnecessary BeginInvoke
            if (_currentDispatcher.CheckAccess())
            {
                RebuildRestoreHiddenDevicesMenu();
            }
            else
            {
                _currentDispatcher.BeginInvoke((Action)(RebuildRestoreHiddenDevicesMenu));
            }
        }

        private void RebuildRestoreHiddenAppsMenu()
        {
            RestoreHiddenAppsMenu.Clear();

            var headerDevice = Devices.FirstOrDefault();
            if (headerDevice == null)
            {
                RaisePropertyChanged(nameof(HasHiddenAppsForHeaderDevice));
                return;
            }

            var hiddenEntries = _mainViewModel.GetHiddenAppsForDevice(headerDevice.Id);
            foreach (var entry in hiddenEntries)
            {
                var localEntry = entry;
                RestoreHiddenAppsMenu.Add(new ContextMenuItem
                {
                    DisplayName = _mainViewModel.GetHiddenAppLabel(localEntry),
                    Command = new RelayCommand(() => _mainViewModel.UnhideAppOnDevice(headerDevice.Id, localEntry.AppId, localEntry.ExeName)),
                });
            }

            if (hiddenEntries.Any())
            {
                RestoreHiddenAppsMenu.Add(new ContextMenuSeparator());
                RestoreHiddenAppsMenu.Add(new ContextMenuItem
                {
                    DisplayName = Properties.Resources.RestoreHiddenAppsForDeviceAll,
                    Command = new RelayCommand(() => _mainViewModel.UnhideAllAppsForDevice(headerDevice.Id)),
                });
            }

            RaisePropertyChanged(nameof(HasHiddenAppsForHeaderDevice));
        }

        private void RebuildRestoreHiddenDevicesMenu()
        {
            RestoreHiddenDevicesMenu.Clear();

            var hiddenDevices = _mainViewModel.GetHiddenDevices();
            foreach (var device in hiddenDevices)
            {
                var localDevice = device;
                var displayName = string.IsNullOrWhiteSpace(device.DisplayName) ? device.DeviceId : device.DisplayName;
                RestoreHiddenDevicesMenu.Add(new ContextMenuItem
                {
                    DisplayName = displayName,
                    Command = new RelayCommand(() => _mainViewModel.UnhideDevice(localDevice.DeviceId)),
                });
            }

            if (hiddenDevices.Any())
            {
                RestoreHiddenDevicesMenu.Add(new ContextMenuSeparator());
                RestoreHiddenDevicesMenu.Add(new ContextMenuItem
                {
                    DisplayName = Properties.Resources.RestoreHiddenDevicesAll,
                    Command = new RelayCommand(() => _mainViewModel.UnhideAllDevices()),
                });
            }

            RaisePropertyChanged(nameof(HasHiddenDevices));
        }

        private void OnDefaultPlaybackDeviceChanged(object sender, DeviceViewModel e)
        {
            // No longer any devices.
            if (e == null) return;

            var foundDevice = Devices.FirstOrDefault(d => d.Id == e.Id);
            if (foundDevice != null)
            {
                // Push to bottom.
                Devices.Move(Devices.IndexOf(foundDevice), Devices.Count - 1);
            }
            else
            {
                var foundAllDevice = _mainViewModel.VisibleDevices.FirstOrDefault(d => d.Id == e.Id);
                if (foundAllDevice != null)
                {
                    // We found the device in VisibleDevices which was not in Devices.
                    // Thus: We are collapsed and can dump the single device in Devices:
                    foreach (var shown in Devices)
                    {
                        shown.Apps.CollectionChanged -= Apps_CollectionChanged;
                    }
                    Devices.Clear();
                    foundAllDevice.Apps.CollectionChanged += Apps_CollectionChanged;
                    Devices.Add(foundAllDevice);
                }
            }
            UpdateTextVisibility();
            RaiseDevicesChanged();
        }

        private void UpdateTextVisibility()
        {
            // Show display name on only the "top" device, which handles Expanded and Collapsed.
            for (var i = 0; i < Devices.Count; i++)
            {
                Devices[i].IsDisplayNameVisible = i > 0;
            }
        }

        public void DoExpandCollapse()
        {
            IsExpanded = !IsExpanded;
            _settings.IsExpanded = IsExpanded;
            if (IsExpanded)
            {
                // Add any that aren't existing.
                foreach (var device in _mainViewModel.VisibleDevices)
                {
                    if (!Devices.Contains(device))
                    {
                        device.Apps.CollectionChanged += Apps_CollectionChanged;
                        Devices.Insert(0, device);
                    }
                }
            }
            else
            {
                // Remove all but the default.
                for (int i = Devices.Count - 1; i >= 0; i--)
                {
                    var device = Devices[i];
                    if (device.Id != _mainViewModel.Default?.Id)
                    {
                        device.Apps.CollectionChanged -= Apps_CollectionChanged;
                        Devices.Remove(device);
                    }
                }

                // The default device may be missing from Devices (e.g. it was hidden or a
                // non-default device was shown while collapsed). Restore it so collapsing
                // always lands on the actual default device.
                if (_mainViewModel.Default != null && !Devices.Contains(_mainViewModel.Default))
                {
                    _mainViewModel.Default.Apps.CollectionChanged += Apps_CollectionChanged;
                    Devices.Insert(0, _mainViewModel.Default);
                }
            }

            UpdateTextVisibility();
            RaiseDevicesChanged();
        }

        private void InvalidateWindowSize()
        {
            // Debounce: rapid collection changes during device churn fire many Add/Remove
            // events in quick succession. Each one triggers layout; if Verify() runs between
            // two InsertItem calls the VirtualizingStackPanel throws InvalidOperationException.
            // Coalescing to a single invalidation per 16 ms frame prevents the race.
            _invalidateSizeDebounceTimer.Stop();
            _invalidateSizeDebounceTimer.Start();
        }

        private void OnInvalidateSizeDebounceTick(object sender, EventArgs e)
        {
            _invalidateSizeDebounceTimer.Stop();

            if (_currentDispatcher.CheckAccess())
            {
                WindowSizeInvalidated?.Invoke(this, null);
            }
            else
            {
                _currentDispatcher.BeginInvoke((Action)(() =>
                {
                    WindowSizeInvalidated?.Invoke(this, null);
                }));
            }
        }

        public void ChangeState(FlyoutViewState state)
        {
            Trace.WriteLine($"FlyoutViewModel ChangeState {state}");
            ValidateStateChange(state);
            State = state;
            StateChanged?.Invoke(this, null);

            // Switch on the requested state, not State. The view defers its follow-up
            // transitions instead of re-entering ChangeState from StateChanged, but should
            // anything ever re-enter, each state's side effects still run once, for that state.
            switch (state)
            {
                case FlyoutViewState.Open:
                    _mainViewModel.OnTrayFlyoutShown();
                    break;
                case FlyoutViewState.Closing_Stage1:
                    _mainViewModel.OnTrayFlyoutHidden();
                    Dialog.IsVisible = false;

                    if (LastInput == InputType.Keyboard && !IsExpandingOrCollapsing)
                    {
                        _returnFocusToTray.Invoke();
                    }
                    break;
                case FlyoutViewState.Closing_Stage2:
                    _deBounceTimer.Start();
                    break;
                case FlyoutViewState.Hidden:
                    if (IsExpandingOrCollapsing)
                    {
                        IsExpandingOrCollapsing = false;
                        // The expand re-open below already satisfies a tray click that landed
                        // during the swap; a leftover flag would re-open after the next close.
                        _openAfterClose = false;
                        // Mark the upcoming open as expand-triggered so the view swaps the
                        // content inside the visible surface — only this re-open, then reset.
                        IsReopeningFromExpand = true;
                        DoExpandCollapse();
                        BeginOpen(LastInput);
                    }
                    else if (_openAfterClose)
                    {
                        var inputType = _openAfterCloseInput;
                        _openAfterClose = false;
                        BeginOpen(inputType);
                    }
                    break;
            }
        }

        private void ValidateStateChange(FlyoutViewState newState)
        {
            var oldState = State;
            bool isValidStateTransition =
                (oldState == FlyoutViewState.NotLoaded && newState == FlyoutViewState.Hidden) ||
                (oldState == FlyoutViewState.Hidden && newState == FlyoutViewState.Opening) ||
                (oldState == FlyoutViewState.Opening && newState == FlyoutViewState.Open) ||
                // A close requested during the entrance retargets it (the view ignores the
                // superseded entrance's completion).
                (oldState == FlyoutViewState.Opening && newState == FlyoutViewState.Closing_Stage1) ||
                (oldState == FlyoutViewState.Open && newState == FlyoutViewState.Closing_Stage1) ||
                (oldState == FlyoutViewState.Closing_Stage1 && newState == FlyoutViewState.Closing_Stage2) ||
                (oldState == FlyoutViewState.Closing_Stage1 && newState == FlyoutViewState.Hidden) ||
                (oldState == FlyoutViewState.Closing_Stage2 && newState == FlyoutViewState.Hidden);
            Debug.Assert(isValidStateTransition);
        }

        public void OpenPopup(object vm, FrameworkElement container)
        {
            Dialog.IsVisible = false;

            if (vm is IAppItemViewModel)
            {
                Dialog.Focused = new FocusedAppItemViewModel(_mainViewModel, (IAppItemViewModel)vm);
            }
            else if (vm is DeviceViewModel)
            {
                var deviceViewModel = new FocusedDeviceViewModel(_mainViewModel, (DeviceViewModel)vm);
                if (deviceViewModel.IsApplicable)
                {
                    Dialog.Focused = deviceViewModel;
                }
            }
            else
            {
                Dialog.Focused = (IFocusedViewModel)vm;
            }

            if (Dialog.Focused != null)
            {
                Dialog.Focused.RequestClose += () => Dialog.IsVisible = false;
                Dialog.Source = container;
                Dialog.IsVisible = true;
            }
        }

        private int OnMouseWheelEvent(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            var existing = _mainViewModel.Default;
            if (existing != null)
            {
                if (!_winRect.Contains(new Point(e.X, e.Y)))
                {
                    existing.IncrementVolume(Math.Sign(e.Delta) * 2);
                    return -1;
                }
            }
            return 0;
        }

        public void BeginOpen(InputType inputType)
        {
            if (State == FlyoutViewState.Hidden)
            {
                LastInput = inputType;
                ChangeState(FlyoutViewState.Opening);
            }

            if (_settings.UseGlobalMouseWheelHook)
            {
                _mh.SetHook();
            }
        }

        public void BeginClose(InputType inputType)
        {
            // Closing during the entrance (Escape, Alt+F4, expand/collapse, display change) no
            // longer waits for the entrance to finish: the view reverses from wherever the
            // surface is. Light dismiss is still ignored while Opening (OnDeactivated), and a
            // tray click while Opening still does nothing (OpenFlyout), as before.
            if (State == FlyoutViewState.Open || State == FlyoutViewState.Opening)
            {
                LastInput = inputType;
                ChangeState(FlyoutViewState.Closing_Stage1);
            }

            _mh.UnHook();
        }

        public void OpenFlyout(InputType inputType)
        {
            // A mouse click on the tray icon while the flyout is open deactivates it first
            // (light dismiss closes it), then this click is delivered. Without this guard the
            // click would re-open the just-closed flyout. If the click lands right after a
            // deactivation, treat it as the dismiss and absorb it. Keyboard reopen is unaffected.
            if (inputType == InputType.Mouse
                && (State == FlyoutViewState.Closing_Stage1 || State == FlyoutViewState.Closing_Stage2)
                && (DateTime.UtcNow - _lastDeactivatedAt) < TimeSpan.FromMilliseconds(300))
            {
                return;
            }

            switch (State)
            {
                case FlyoutViewState.Hidden:
                    BeginOpen(inputType);
                    break;
                case FlyoutViewState.Open:
                    BeginClose(inputType);
                    break;
                case FlyoutViewState.Closing_Stage1:
                    _openAfterClose = true;
                    _openAfterCloseInput = inputType;
                    break;
                case FlyoutViewState.Closing_Stage2:
                    _openAfterClose = true;
                    _openAfterCloseInput = inputType;
                    _deBounceTimer.Stop();
                    ChangeState(FlyoutViewState.Hidden);
                    break;
            }
        }

        public void OnDeactivated(object sender, EventArgs e)
        {
            if (State == FlyoutViewState.Opening)
            {
                return;
            }
            if (IsPinned)
            {
                return;
            }
            _lastDeactivatedAt = DateTime.UtcNow;
            BeginClose(InputType.Command);
        }

        public void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (Dialog.IsVisible)
                {
                    Dialog.IsVisible = false;
                }
                else
                {
                    BeginClose(InputType.Keyboard);
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.Space)
            {
                // Disable the system menu.
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
            {
                Trace.WriteLine($"FlyoutVM: Ctrl+Z pressed, CanUndo={App.UndoService.CanUndo}, UndoCount={App.UndoService.UndoCount}");
                ApplyUndo();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y)
            {
                Trace.WriteLine($"FlyoutVM: Ctrl+Y pressed, CanRedo={App.UndoService.CanRedo}, RedoCount={App.UndoService.RedoCount}");
                ApplyRedo();
                e.Handled = true;
            }
        }

        private void ApplyUndo()
        {
            var action = App.UndoService.Undo();
            if (action != null) UndoRedoHelper.ApplyAction(action, isUndo: true);
        }

        private void ApplyRedo()
        {
            var action = App.UndoService.Redo();
            if (action != null) UndoRedoHelper.ApplyAction(action, isUndo: false);
        }

        public void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            BeginClose(InputType.Keyboard);
        }

        public void OnLightDismissBorderPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Dialog.IsVisible = false;
            e.Handled = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _mh?.Dispose();
                    _deBounceTimer?.Stop();
                    _invalidateSizeDebounceTimer?.Stop();

                    // Unsubscribe events
                    _mainViewModel.DefaultChanged -= OnDefaultPlaybackDeviceChanged;
                    _mainViewModel.VisibleDevices.CollectionChanged -= AllDevices_CollectionChanged;
                    _settings.HiddenAppsChanged -= OnHiddenAppsChanged;
                    _settings.HiddenDevicesChanged -= OnHiddenDevicesChanged;

                    foreach (var device in Devices)
                    {
                        device.Apps.CollectionChanged -= Apps_CollectionChanged;
                    }
                }
                _disposed = true;
            }
        }

        // OPTIMIZATION: Removed finalizer - proper Dispose pattern is sufficient
        // Finalizers force GC Gen 2 promotion and should only be used for unmanaged resources
        // ~FlyoutViewModel()
        // {
        //     Dispose(false);
        // }
    }
}
