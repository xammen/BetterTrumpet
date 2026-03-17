using EarTrumpet.CLI;
using EarTrumpet.DataModel;
using EarTrumpet.DataModel.WindowsAudio;
using EarTrumpet.Diagnosis;
using EarTrumpet.Extensibility;
using EarTrumpet.Extensibility.Hosting;
using EarTrumpet.Extensions;
using EarTrumpet.Interop;
using EarTrumpet.Interop.Helpers;
using EarTrumpet.UI.Helpers;
using EarTrumpet.UI.ViewModels;
using EarTrumpet.UI.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace EarTrumpet
{
    public partial class App
    {
        public static bool IsShuttingDown { get; private set; }
        public static bool HasIdentity { get; private set; }
        public static bool HasDevIdentity { get; private set; }
        public static string PackageName { get; private set; }
        public static Version PackageVersion { get; private set; }
        public static TimeSpan Duration => s_appTimer.Elapsed;

        public FlyoutWindow FlyoutWindow { get; private set; }
        public DeviceCollectionViewModel CollectionViewModel { get; private set; }

        private static readonly Stopwatch s_appTimer = Stopwatch.StartNew();
        private FlyoutViewModel _flyoutViewModel;

        private ShellNotifyIcon _trayIcon;
        private TaskbarIconSource _trayIconSource;
        private WindowHolder _mixerWindow;
        private WindowHolder _settingsWindow;
        private ErrorReporter _errorReporter;
        private MediaPopupWindow _mediaPopup;
        private System.Windows.Threading.DispatcherTimer _mediaPopupDelayTimer;
        private PipeServer _pipeServer;
        private CliHandler _cliHandler;
        private DataModel.Audio.IAudioDeviceManager _deviceManager;
        private DataModel.UpdateService _updateService;

        public static AppSettings Settings { get; private set; }
        public static VolumeUndoService UndoService { get; } = new VolumeUndoService();

        public void OpenMixerWindow()
        {
            _mixerWindow?.OpenOrBringToFront();
        }

        private void OnAppStartup(object sender, StartupEventArgs e)
        {
            // ══════════════════════════════════════════════════════════════
            // CLI INTERCEPT: If launched with CLI args, send to running instance and exit
            // ══════════════════════════════════════════════════════════════
            if (CliEntryPoint.TryHandleCliArgs(Environment.GetCommandLineArgs().Skip(1).ToArray()))
            {
                Shutdown();
                return;
            }

            // ══════════════════════════════════════════════════════════════
            // STARTUP PHASE 1: Core (fatal if fails — crash report + exit)
            // ══════════════════════════════════════════════════════════════

            Exit += (_, __) =>
            {
                IsShuttingDown = true;
                ErrorReporter.Shutdown(); // Flush Sentry events before exit
            };
            HasIdentity = PackageHelper.CheckHasIdentity();
            HasDevIdentity = PackageHelper.HasDevIdentity();
            PackageVersion = PackageHelper.GetVersion(HasIdentity);
            PackageName = PackageHelper.GetFamilyName(HasIdentity);

            Settings = new AppSettings();
            _errorReporter = new ErrorReporter(Settings);
            CrashHandler.Initialize();

            if (SingleInstanceAppMutex.TakeExclusivity())
            {
                Exit += (_, __) => SingleInstanceAppMutex.ReleaseExclusivity();

                try
                {
                    ContinueStartup();
                }
                catch (Exception ex) when (IsCriticalFontLoadFailure(ex))
                {
                    ErrorReporter.LogWarning(ex);
                    OnCriticalFontLoadFailure();
                }
            }
            else
            {
                Shutdown();
            }
        }

        private void ContinueStartup()
        {
            ((UI.Themes.Manager)Resources["ThemeManager"]).Load();

            _deviceManager = WindowsAudioFactory.Create(AudioDeviceKind.Playback);
            _deviceManager.Loaded += (_, __) => CompleteStartup();
            CollectionViewModel = new DeviceCollectionViewModel(_deviceManager, Settings);

            _trayIconSource = new TaskbarIconSource(CollectionViewModel, Settings);
            _trayIcon = new ShellNotifyIcon(_trayIconSource);
            Exit += (_, __) => _trayIcon.IsVisible = false;
            CollectionViewModel.TrayPropertyChanged += () => _trayIcon.SetTooltip(CollectionViewModel.GetTrayToolTip());

            _flyoutViewModel = new FlyoutViewModel(CollectionViewModel, () => _trayIcon.SetFocus(), Settings);
            FlyoutWindow = new FlyoutWindow(_flyoutViewModel);
            // Initialize the FlyoutWindow last because its Show/Hide cycle will pump messages, causing UI frames
            // to be executed, breaking the assumption that startup is complete.
            FlyoutWindow.Initialize();
        }

        private void CompleteStartup()
        {
            // ══════════════════════════════════════════════════════════════
            // STARTUP PHASE 2: UI (degraded mode if fails)
            // ══════════════════════════════════════════════════════════════
            _mixerWindow = new WindowHolder(CreateMixerExperience);
            _settingsWindow = new WindowHolder(CreateSettingsExperience);

            _trayIcon.PrimaryInvoke += (_, type) => _flyoutViewModel.OpenFlyout(type);
            _trayIcon.SecondaryInvoke += (_, args) => _trayIcon.ShowContextMenu(GetTrayContextMenuItems(), args.Point);
            _trayIcon.TertiaryInvoke += (_, __) => CollectionViewModel.Default?.ToggleMute.Execute(null);
            _trayIcon.Scrolled += trayIconScrolled;
            _trayIcon.SetTooltip(CollectionViewModel.GetTrayToolTip());
            _trayIcon.IsVisible = true;

            // ══════════════════════════════════════════════════════════════
            // STARTUP PHASE 3: Features (each isolated — failure = feature disabled)
            // ══════════════════════════════════════════════════════════════

            // 3a. Addons
            try
            {
                AddonManager.Load(shouldLoadInternalAddons: HasDevIdentity);
                Exit += (_, __) => AddonManager.Shutdown();
            }
            catch (Exception ex) { Trace.WriteLine($"Startup: Addons failed to load: {ex.Message}"); }

#if DEBUG
            try { DebugHelpers.Add(); }
            catch (Exception ex) { Trace.WriteLine($"Startup: DebugHelpers failed: {ex.Message}"); }
#endif

            // 3b. Hotkeys
            try
            {
                Settings.FlyoutHotkeyTyped += () => _flyoutViewModel.OpenFlyout(InputType.Keyboard);
                Settings.MixerHotkeyTyped += () => _mixerWindow.OpenOrClose();
                Settings.SettingsHotkeyTyped += () => _settingsWindow.OpenOrBringToFront();
                Settings.AbsoluteVolumeUpHotkeyTyped += AbsoluteVolumeIncrement;
                Settings.AbsoluteVolumeDownHotkeyTyped += AbsoluteVolumeDecrement;
                Settings.SwitchDeviceHotkeyTyped += CycleDefaultDevice;
                Settings.RegisterHotkeys();
            }
            catch (Exception ex) { Trace.WriteLine($"Startup: Hotkeys registration failed: {ex.Message}"); }

            // 3c. Media popup
            try
            {
                _mediaPopup = new MediaPopupWindow(Settings);
                InitializeMediaPopup();
            }
            catch (Exception ex) { Trace.WriteLine($"Startup: MediaPopup failed: {ex.Message}"); }

            // 3d. Health monitoring
            try { HealthMonitor.Start(); }
            catch (Exception ex) { Trace.WriteLine($"Startup: HealthMonitor failed: {ex.Message}"); }

            // 3f. Update checker
            try
            {
                _updateService = new DataModel.UpdateService();
                _updateService.Channel = Settings.UpdateNotifyChannel;
                _flyoutViewModel.SetUpdateService(_updateService);
                _updateService.UpdateAvailableChanged += () =>
                {
                    if (_trayIconSource != null) _trayIconSource.ShowUpdateBadge = _updateService.IsUpdateAvailable;
                };
                if (Settings.AutoCheckForUpdates && Settings.UpdateNotifyChannel != DataModel.UpdateChannel.None)
                {
                    _updateService.Start();
                }
            }
            catch (Exception ex) { Trace.WriteLine($"Startup: UpdateService failed: {ex.Message}"); }

            // 3g. CLI pipe server
            try
            {
                _cliHandler = new CliHandler(() => CollectionViewModel, () => Settings, () => _deviceManager);
                if (_updateService != null) _cliHandler.SetUpdateServiceProvider(() => _updateService);
                _pipeServer = new PipeServer();
                _pipeServer.CommandReceived += _cliHandler.ProcessCommand;
                _pipeServer.Start();
                Exit += (_, __) => _pipeServer?.Dispose();
            }
            catch (Exception ex) { Trace.WriteLine($"Startup: PipeServer failed: {ex.Message}"); }

            // 3e. First-run experience
            try { DisplayFirstRunExperience(); }
            catch (Exception ex) { Trace.WriteLine($"Startup: FirstRun dialog failed: {ex.Message}"); }

            // 3h. What's New changelog (show after version upgrade, not on first run)
            try { DisplayChangelogIfUpdated(); }
            catch (Exception ex) { Trace.WriteLine($"Startup: Changelog failed: {ex.Message}"); }

            Trace.WriteLine($"Startup: Complete in {Duration.TotalMilliseconds:F0}ms");
        }

        /// <summary>
        /// Sets up media popup hover behavior. Isolated from CompleteStartup for clarity.
        /// </summary>
        private void InitializeMediaPopup()
        {
            if (_mediaPopup == null) return;

            _mediaPopupDelayTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(Settings.MediaPopupHoverDelay)
            };

            Settings.MediaPopupSettingsChanged += () =>
            {
                _mediaPopupDelayTimer.Interval = TimeSpan.FromSeconds(Settings.MediaPopupHoverDelay);
            };

            _mediaPopupDelayTimer.Tick += (s, e) =>
            {
                _mediaPopupDelayTimer.Stop();

                if (Settings.MediaPopupShowOnlyWhenPlaying && !DataModel.MediaSessionService.Instance.IsMediaPlaying)
                {
                    return;
                }

                _mediaPopup.ShowPopup(_trayIcon.IconBounds);
            };
            _mediaPopup.PopupHidden += (_, __) =>
            {
                _trayIcon.SetTooltip(CollectionViewModel.GetTrayToolTip());
            };
            _trayIcon.MouseHoverChanged += (_, isOver) =>
            {
                if (!Settings.MediaPopupEnabled) return;

                if (isOver)
                {
                    _trayIcon.SetTooltip("");
                    if (!_mediaPopup.IsShowing)
                    {
                        _mediaPopupDelayTimer.Start();
                    }
                }
                else
                {
                    _mediaPopupDelayTimer.Stop();
                    if (!_mediaPopup.IsShowing)
                    {
                        _trayIcon.SetTooltip(CollectionViewModel.GetTrayToolTip());
                    }
                    _mediaPopup.StartHideTimer();
                }
            };
        }

        private void trayIconScrolled(object _, int wheelDelta)
        {
            if (Settings.UseScrollWheelInTray && (!Settings.UseGlobalMouseWheelHook || _flyoutViewModel.State == FlyoutViewState.Hidden))
            {
                var hWndTray = WindowsTaskbar.GetTrayToolbarWindowHwnd();
                var hWndTooltip = User32.SendMessage(hWndTray, User32.TB_GETTOOLTIPS, IntPtr.Zero, IntPtr.Zero);
                User32.SendMessage(hWndTooltip, User32.TTM_POPUP, IntPtr.Zero, IntPtr.Zero);
                
                CollectionViewModel.Default?.IncrementVolume(Math.Sign(wheelDelta) * 2);
            }
        }

        private void DisplayFirstRunExperience()
        {
            if (!Settings.HasShownFirstRun
                || Keyboard.IsKeyDown(Key.LeftCtrl)
                )
            {
                Trace.WriteLine($"App DisplayFirstRunExperience Showing onboarding");
                Settings.HasShownFirstRun = true;

                var vm = new OnboardingViewModel(Settings, _deviceManager);
                var window = new OnboardingWindow { DataContext = vm };
                vm.Completed += (s, e) => window.Close();
                window.Show();
            }
        }

        private void DisplayChangelogIfUpdated()
        {
            var currentVersion = App.PackageVersion?.ToString() ?? "";
            var lastSeen = Settings.LastSeenVersion;

            // Hold Shift at startup to force changelog display
            if (Keyboard.IsKeyDown(Key.LeftShift))
            {
                var window = new UI.Views.ChangelogWindow();
                window.Show();
                return;
            }

            // First-run: no previous version seen → seed the version and skip changelog
            // (onboarding already handles first-run experience)
            if (string.IsNullOrEmpty(lastSeen))
            {
                Settings.LastSeenVersion = currentVersion;
                return;
            }

            // Show changelog only when version actually changed (upgrade scenario)
            if (lastSeen != currentVersion)
            {
                Settings.LastSeenVersion = currentVersion;
                var window = new UI.Views.ChangelogWindow();
                window.Show();
            }
        }

        private bool IsCriticalFontLoadFailure(Exception ex)
        {
            return ex.StackTrace.Contains("MS.Internal.Text.TextInterface.FontFamily.GetFirstMatchingFont") ||
                   ex.StackTrace.Contains("MS.Internal.Text.Line.Format");
        }

        private void OnCriticalFontLoadFailure()
        {
            Trace.WriteLine($"App OnCriticalFontLoadFailure");

            new Thread(() =>
            {
                if (MessageBox.Show(
                    EarTrumpet.Properties.Resources.CriticalFailureFontLookupHelpText,
                    EarTrumpet.Properties.Resources.CriticalFailureDialogHeaderText,
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Error,
                    MessageBoxResult.OK) == MessageBoxResult.OK)
                {
                    Trace.WriteLine($"App OnCriticalFontLoadFailure OK");
                    ProcessHelper.StartNoThrow("https://eartrumpet.app/jmp/fixfonts");
                }
                Environment.Exit(0);
            }).Start();

            // Stop execution because callbacks to the UI thread will likely cause another cascading font error.
            new AutoResetEvent(false).WaitOne();
        }

        private IEnumerable<ContextMenuItem> GetTrayContextMenuItems()
        {
            var ret = new List<ContextMenuItem>(CollectionViewModel.AllDevices.OrderBy(x => x.DisplayName).Select(dev => new ContextMenuItem
            {
                DisplayName = dev.DisplayName,
                IsChecked = dev.Id == CollectionViewModel.Default?.Id,
                Command = new RelayCommand(() => dev.MakeDefaultDevice()),
            }));

            if (!ret.Any())
            {
                ret.Add(new ContextMenuItem
                {
                    DisplayName = EarTrumpet.Properties.Resources.ContextMenuNoDevices,
                    IsEnabled = false,
                });
            }

            ret.AddRange(new List<ContextMenuItem>
                {
                    new ContextMenuSeparator(),
                    new ContextMenuItem
                    {
                        DisplayName = EarTrumpet.Properties.Resources.WindowsLegacyMenuText,
                        Children = new List<ContextMenuItem>
                        {
                            new ContextMenuItem { DisplayName = EarTrumpet.Properties.Resources.LegacyVolumeMixerText, Command =  new RelayCommand(LegacyControlPanelHelper.StartLegacyAudioMixer) },
                            new ContextMenuItem { DisplayName = EarTrumpet.Properties.Resources.PlaybackDevicesText, Command = new RelayCommand(() => LegacyControlPanelHelper.Open("playback")) },
                            new ContextMenuItem { DisplayName = EarTrumpet.Properties.Resources.RecordingDevicesText, Command = new RelayCommand(() => LegacyControlPanelHelper.Open("recording")) },
                            new ContextMenuItem { DisplayName = EarTrumpet.Properties.Resources.SoundsControlPanelText, Command = new RelayCommand(() => LegacyControlPanelHelper.Open("sounds")) },
                            new ContextMenuItem { DisplayName = EarTrumpet.Properties.Resources.OpenSoundSettingsText, Command = new RelayCommand(() => SettingsPageHelper.Open("sound")) },
                            new ContextMenuItem {
                                DisplayName = Environment.OSVersion.IsAtLeast(OSVersions.Windows11) ?
                                    EarTrumpet.Properties.Resources.OpenAppsVolume_Windows11_Text
                                    : EarTrumpet.Properties.Resources.OpenAppsVolume_Windows10_Text, Command = new RelayCommand(() => SettingsPageHelper.Open("apps-volume")) },
                        },
                    },
                    new ContextMenuSeparator(),
                });

            var addonItems = AddonManager.Host.TrayContextMenuItems?.OrderBy(x => x.NotificationAreaContextMenuItems.FirstOrDefault()?.DisplayName).SelectMany(ext => ext.NotificationAreaContextMenuItems);
            if (addonItems != null && addonItems.Any())
            {
                ret.AddRange(addonItems);
                ret.Add(new ContextMenuSeparator());
            }

            if (_updateService != null && _updateService.IsUpdateAvailable)
            {
                ret.Add(new ContextMenuItem
                {
                    DisplayName = $"Mettre \u00e0 jour (v{_updateService.LatestVersion})",
                    Glyph = "\xE896",
                    Command = new RelayCommand(() => _updateService.DownloadAndInstallAsync()),
                });
                ret.Add(new ContextMenuSeparator());
            }

            ret.AddRange(new List<ContextMenuItem>
                {
                    new ContextMenuItem { DisplayName = EarTrumpet.Properties.Resources.FullWindowTitleText, Command = new RelayCommand(_mixerWindow.OpenOrBringToFront) },
                    new ContextMenuItem { DisplayName = EarTrumpet.Properties.Resources.SettingsWindowText, Command = new RelayCommand(_settingsWindow.OpenOrBringToFront) },
                    new ContextMenuItem { DisplayName = EarTrumpet.Properties.Resources.ContextMenuExitTitle, Command = new RelayCommand(Shutdown) },
                });
            return ret;
        }

        private Window CreateSettingsExperience()
        {
            var defaultCategory = new SettingsCategoryViewModel(
                EarTrumpet.Properties.Resources.SettingsCategoryTitle,
                "\xE71D",
                EarTrumpet.Properties.Resources.SettingsDescriptionText,
                null,
                new SettingsPageViewModel[]
                    {
                        new EarTrumpetShortcutsPageViewModel(Settings),
                        new EarTrumpetMouseSettingsPageViewModel(Settings),
                        new EarTrumpetCommunitySettingsPageViewModel(Settings),
                        new EarTrumpetLegacySettingsPageViewModel(Settings),
                        CreateAboutPage()
                    });

            var customizationCategory = new SettingsCategoryViewModel(
                EarTrumpet.Properties.Resources.CustomizationCategoryTitle,
                "\xE790", // Paintbrush icon
                EarTrumpet.Properties.Resources.CustomizationCategoryDescription,
                null,
                new SettingsPageViewModel[]
                    {
                        new EarTrumpetAnimationSettingsPageViewModel(Settings),
                        new EarTrumpetColorsSettingsPageViewModel(Settings),
                        new EarTrumpetMediaPopupSettingsPageViewModel(Settings),
                        new EarTrumpetVolumeProfilesSettingsPageViewModel(Settings)
                    });

            var allCategories = new List<SettingsCategoryViewModel>();
            allCategories.Add(defaultCategory);
            allCategories.Add(customizationCategory);

            if (AddonManager.Host.SettingsItems != null)
            {
                allCategories.AddRange(AddonManager.Host.SettingsItems.Select(a => CreateAddonSettingsPage(a)));
            }

            var viewModel = new SettingsViewModel(EarTrumpet.Properties.Resources.SettingsWindowText, allCategories);
            return new SettingsWindow { DataContext = viewModel };
        }

        private SettingsCategoryViewModel CreateAddonSettingsPage(IEarTrumpetAddonSettingsPage addonSettingsPage)
        {
            var addon = (EarTrumpetAddon)addonSettingsPage;
            var category = addonSettingsPage.GetSettingsCategory();

            if (!addon.IsInternal())
            {
                category.Pages.Add(new AddonAboutPageViewModel(addon));
            }
            return category;
        }

        private EarTrumpetAboutPageViewModel CreateAboutPage()
        {
            var vm = new EarTrumpetAboutPageViewModel(() => _errorReporter.DisplayDiagnosticData(), Settings);
            if (_updateService != null) vm.SetUpdateService(_updateService);
            return vm;
        }

        private Window CreateMixerExperience() => new FullWindow { DataContext = new FullWindowViewModel(CollectionViewModel) };

        private void AbsoluteVolumeIncrement()
        {
            foreach (var device in CollectionViewModel.AllDevices.Where(d => !d.IsMuted || d.IsAbsMuted))
            {
                // in any case this device is not abs muted anymore
                device.IsAbsMuted = false;
                device.IncrementVolume(2);
            }
        }

        private void AbsoluteVolumeDecrement()
        {
            foreach (var device in CollectionViewModel.AllDevices.Where(d => !d.IsMuted))
            {
                // if device is not muted but will be muted by 
                bool wasMuted = device.IsMuted;
                // device.IncrementVolume(-2);
                device.Volume -= 2;
                // if device is muted by this absolute down
                // .IsMuted is not already updated
                if (!wasMuted == (device.Volume <= 0))
                {
                    device.IsAbsMuted = true;
                }
            }
        }

        private void CycleDefaultDevice()
        {
            var devices = _deviceManager.Devices;
            if (devices == null || devices.Count < 2) return;

            var current = _deviceManager.Default;
            var list = devices.ToList();
            var idx = current != null ? list.FindIndex(d => d.Id == current.Id) : -1;
            var next = list[(idx + 1) % list.Count];
            _deviceManager.Default = next;
            Trace.WriteLine($"CycleDefaultDevice: switched to '{next.DisplayName}'");
        }
    }
}
