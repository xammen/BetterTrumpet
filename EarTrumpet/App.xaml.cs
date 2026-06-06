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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;

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
        public DataModel.Audio.IAudioDeviceManager AudioDeviceManager => _deviceManager;

        private static readonly Stopwatch s_appTimer = Stopwatch.StartNew();
        private FlyoutViewModel _flyoutViewModel;

        private ShellNotifyIcon _trayIcon;
        private TaskbarIconSource _trayIconSource;
        private WindowHolder _mixerWindow;
        private WindowHolder _settingsWindow;
        private ErrorReporter _errorReporter;
        private MediaPopupWindow _mediaPopup;
        private System.Windows.Threading.DispatcherTimer _mediaPopupDelayTimer;
        private Popup _trayHoverTooltipPopup;
        private Border _trayHoverTooltipBorder;
        private TextBlock _trayHoverTooltipText;
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

        public void ShowQuickTrumpetConfirmation(string presetName, VolumeProfileService.ApplyProfileResult result)
        {
            if (!Settings.ShowQuickTrumpetConfirmation) return;

            var apps = result?.AppsApplied ?? 0;
            var devices = result?.DevicesApplied ?? 0;
            _trayIcon?.ShowNotification(
                EarTrumpet.Properties.Resources.QuickTrumpetAppliedTitle,
                string.Format(EarTrumpet.Properties.Resources.QuickTrumpetAppliedMessage, presetName, apps, devices));
        }

        private void EnsureTrayHoverTooltipPopup()
        {
            if (_trayHoverTooltipPopup != null)
            {
                return;
            }

            _trayHoverTooltipText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.NoWrap,
            };

            _trayHoverTooltipBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 6, 10, 6),
                Background = new SolidColorBrush(Color.FromArgb(232, 28, 28, 30)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 14,
                    ShadowDepth = 2,
                    Opacity = 0.35,
                    Color = Colors.Black,
                },
                Child = _trayHoverTooltipText,
            };

            _trayHoverTooltipPopup = new Popup
            {
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
                StaysOpen = true,
                Placement = PlacementMode.AbsolutePoint,
                IsHitTestVisible = false,
                Child = _trayHoverTooltipBorder,
            };
        }

        private void ShowTrayHoverTooltip(string text, Rect iconBounds)
        {
            EnsureTrayHoverTooltipPopup();

            if (string.IsNullOrWhiteSpace(text))
            {
                HideTrayHoverTooltip();
                return;
            }

            _trayHoverTooltipText.Text = text;
            _trayHoverTooltipPopup.IsOpen = false;

            _trayHoverTooltipBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desired = _trayHoverTooltipBorder.DesiredSize;
            var width = Math.Ceiling(desired.Width);
            var height = Math.Ceiling(desired.Height);

            var popupTop = iconBounds.Top - (_mediaPopup?.Height ?? 185) - height - 12;
            var popupLeft = iconBounds.Left + (iconBounds.Width / 2) - (width / 2);
            var workArea = SystemParameters.WorkArea;

            popupLeft = Math.Max(workArea.Left + 4, Math.Min(popupLeft, workArea.Right - width - 4));
            popupTop = Math.Max(workArea.Top + 4, Math.Min(popupTop, workArea.Bottom - height - 4));

            _trayHoverTooltipPopup.HorizontalOffset = popupLeft;
            _trayHoverTooltipPopup.VerticalOffset = popupTop;
            _trayHoverTooltipPopup.IsOpen = true;
        }

        private void HideTrayHoverTooltip()
        {
            if (_trayHoverTooltipPopup != null)
            {
                _trayHoverTooltipPopup.IsOpen = false;
            }
        }

        private string GetTrayTooltipTextOrEmpty()
        {
            if (Settings == null || CollectionViewModel == null || !Settings.ShowAppTooltips)
            {
                return string.Empty;
            }

            return CollectionViewModel.GetTrayToolTip();
        }

        private void RefreshTrayTooltipPresentation()
        {
            if (_trayIcon == null)
            {
                return;
            }

            if (_mediaPopup != null && _mediaPopup.IsShowing && Settings.MediaPopupEnabled)
            {
                if (Settings.ShowAppTooltips && _trayIcon.IsMouseOver)
                {
                    _trayIcon.SetTooltip(string.Empty);
                    ShowTrayHoverTooltip(CollectionViewModel.GetTrayToolTip(), _trayIcon.IconBounds);
                }
                else
                {
                    HideTrayHoverTooltip();
                    _trayIcon.SetTooltip(string.Empty);
                }

                return;
            }

            HideTrayHoverTooltip();
            _trayIcon.SetTooltip(GetTrayTooltipTextOrEmpty());
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
            // ══════════════════════════════════════════════════════════════
            // PHASE 1: Instant Tray Icon (0-500ms)
            // Show tray icon immediately with loading state
            // ══════════════════════════════════════════════════════════════
            ((UI.Themes.Manager)Resources["ThemeManager"]).Load();

            // Start audio device manager initialization in background (non-blocking)
            _deviceManager = WindowsAudioFactory.Create(AudioDeviceKind.Playback);
            CollectionViewModel = new DeviceCollectionViewModel(_deviceManager, Settings);

            // Show tray icon immediately (even before audio is ready)
            _trayIconSource = new TaskbarIconSource(CollectionViewModel, Settings);
            _trayIcon = new ShellNotifyIcon(_trayIconSource);
            _trayIcon.SetTooltip("BetterTrumpet (Loading...)");
            _trayIcon.IsVisible = true; // Tray icon visible NOW
            Exit += (_, __) => _trayIcon.IsVisible = false;

            Trace.WriteLine($"Startup: Tray icon visible at {Duration.TotalMilliseconds:F0}ms");

            // ══════════════════════════════════════════════════════════════
            // PHASE 2: Audio Background Loading (500ms-2s)
            // When audio loads, connect to UI and initialize FlyoutWindow + features
            // ══════════════════════════════════════════════════════════════
            _deviceManager.Loaded += (_, __) =>
            {
                Trace.WriteLine($"Startup: Audio loaded at {Duration.TotalMilliseconds:F0}ms");

                // Connect audio data to tray icon
                CollectionViewModel.TrayPropertyChanged += RefreshTrayTooltipPresentation;
                Settings.AppTooltipsChanged += () => Dispatcher.BeginInvoke((Action)RefreshTrayTooltipPresentation);
                RefreshTrayTooltipPresentation(); // Update tooltip with real data

                // Initialize FlyoutWindow now that audio is ready
                _flyoutViewModel = new FlyoutViewModel(CollectionViewModel, () => _trayIcon.SetFocus(), Settings);
                FlyoutWindow = new FlyoutWindow(_flyoutViewModel, TryGetTrayIconBounds);
                FlyoutWindow.Initialize();

                Trace.WriteLine($"Startup: FlyoutWindow initialized at {Duration.TotalMilliseconds:F0}ms");

                // Complete startup with all remaining features in parallel
                CompleteStartup();
            };
        }

        private void CompleteStartup()
        {
            // ══════════════════════════════════════════════════════════════
            // STARTUP PHASE 3: UI Components (critical for interaction)
            // ══════════════════════════════════════════════════════════════
            _mixerWindow = new WindowHolder(CreateMixerExperience);
            _settingsWindow = new WindowHolder(CreateSettingsExperience);

            _trayIcon.PrimaryInvoke += (_, type) => _flyoutViewModel.OpenFlyout(type);
            _trayIcon.SecondaryInvoke += (_, args) => _trayIcon.ShowContextMenu(GetTrayContextMenuItems(), args.Point);
            _trayIcon.TertiaryInvoke += (_, __) => CollectionViewModel.Default?.ToggleMute.Execute(null);
            _trayIcon.Scrolled += trayIconScrolled;
            // Tray icon is already visible from ContinueStartup

            Trace.WriteLine($"Startup: UI components ready at {Duration.TotalMilliseconds:F0}ms");

            // ══════════════════════════════════════════════════════════════
            // STARTUP PHASE 4: Parallel Feature Loading (background)
            // Load non-critical features in parallel for maximum performance
            // ══════════════════════════════════════════════════════════════
            Task.Run(() =>
            {
                var tasks = new List<Task>();

                // Task 1: Addons
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        AddonManager.Load(shouldLoadInternalAddons: HasDevIdentity);
                        Dispatcher.BeginInvoke((Action)(() => Exit += (_, __) => AddonManager.Shutdown()));
                        Trace.WriteLine($"Startup: Addons loaded at {Duration.TotalMilliseconds:F0}ms");
                    }
                    catch (Exception ex) { Trace.WriteLine($"Startup: Addons failed to load: {ex.Message}"); }

#if DEBUG
                    try
                    {
                        Dispatcher.BeginInvoke((Action)(() => DebugHelpers.Add()));
                        Trace.WriteLine($"Startup: DebugHelpers loaded at {Duration.TotalMilliseconds:F0}ms");
                    }
                    catch (Exception ex) { Trace.WriteLine($"Startup: DebugHelpers failed: {ex.Message}"); }
#endif
                }));

                // Task 2: Hotkeys
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        Dispatcher.Invoke((Action)(() =>
                        {
                            Settings.FlyoutHotkeyTyped += () => _flyoutViewModel.OpenFlyout(InputType.Keyboard);
                            Settings.MixerHotkeyTyped += () => _mixerWindow.OpenOrClose();
                            Settings.SettingsHotkeyTyped += () => _settingsWindow.OpenOrBringToFront();
                            Settings.AbsoluteVolumeUpHotkeyTyped += AbsoluteVolumeIncrement;
                            Settings.AbsoluteVolumeDownHotkeyTyped += AbsoluteVolumeDecrement;
                            Settings.SwitchDeviceHotkeyTyped += CycleDefaultDevice;
                            Settings.QuickTrumpetPresetHotkeyTyped += ApplyQuickTrumpetPreset;
                            Settings.RegisterHotkeys();
                        }));
                        Trace.WriteLine($"Startup: Hotkeys registered at {Duration.TotalMilliseconds:F0}ms");
                    }
                    catch (Exception ex) { Trace.WriteLine($"Startup: Hotkeys registration failed: {ex.Message}"); }
                }));

                // Task 3: Media popup (only if enabled)
                if (Settings.MediaPopupEnabled)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            Dispatcher.Invoke((Action)(() =>
                            {
                                _mediaPopup = new MediaPopupWindow(Settings);
                                InitializeMediaPopup();
                            }));
                            Trace.WriteLine($"Startup: MediaPopup initialized at {Duration.TotalMilliseconds:F0}ms");
                        }
                        catch (Exception ex) { Trace.WriteLine($"Startup: MediaPopup failed: {ex.Message}"); }
                    }));
                }
                else
                {
                    Trace.WriteLine("Startup: MediaPopup skipped (feature disabled)");
                }

                // Task 4: Health monitoring
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        HealthMonitor.Start();
                        Trace.WriteLine($"Startup: HealthMonitor started at {Duration.TotalMilliseconds:F0}ms");
                    }
                    catch (Exception ex) { Trace.WriteLine($"Startup: HealthMonitor failed: {ex.Message}"); }
                }));

                // Task 5: Update checker
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        Dispatcher.Invoke((Action)(() =>
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
                        }));
                        Trace.WriteLine($"Startup: UpdateService initialized at {Duration.TotalMilliseconds:F0}ms");
                    }
                    catch (Exception ex) { Trace.WriteLine($"Startup: UpdateService failed: {ex.Message}"); }
                }));

                // Task 6: CLI pipe server
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        Dispatcher.Invoke((Action)(() =>
                        {
                            _cliHandler = new CliHandler(() => CollectionViewModel, () => Settings, () => _deviceManager);
                            if (_updateService != null) _cliHandler.SetUpdateServiceProvider(() => _updateService);
                            _pipeServer = new PipeServer();
                            _pipeServer.CommandReceived += _cliHandler.ProcessCommand;
                            _pipeServer.Start();
                            Exit += (_, __) => _pipeServer?.Dispose();
                        }));
                        Trace.WriteLine($"Startup: PipeServer started at {Duration.TotalMilliseconds:F0}ms");
                    }
                    catch (Exception ex) { Trace.WriteLine($"Startup: PipeServer failed: {ex.Message}"); }
                }));

                // Wait for all parallel tasks to complete
                Task.WhenAll(tasks).ContinueWith(_ =>
                {
                    Trace.WriteLine($"Startup: All background tasks completed at {Duration.TotalMilliseconds:F0}ms");

                    // Display first-run and changelog on UI thread (must be sequential, not parallel)
                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        try { DisplayFirstRunExperience(); }
                        catch (Exception ex) { Trace.WriteLine($"Startup: FirstRun dialog failed: {ex.Message}"); }

                        try { DisplayChangelogIfUpdated(); }
                        catch (Exception ex) { Trace.WriteLine($"Startup: Changelog failed: {ex.Message}"); }

                        Trace.WriteLine($"Startup: Complete in {Duration.TotalMilliseconds:F0}ms");
                    }));
                });
            });
        }

        /// <summary>
        /// Sets up media popup hover behavior. Isolated from CompleteStartup for clarity.
        /// Timer creation deferred to first Show() call for lazy initialization.
        /// </summary>
        private void InitializeMediaPopup()
        {
            if (_mediaPopup == null) return;

            // Timer will be created on first Show() call - deferred initialization
            _trayIcon.MouseHoverChanged += (_, isOver) =>
            {
                if (!Settings.MediaPopupEnabled) return;

                if (isOver)
                {
                    // Lazy-create timer on first hover
                    if (_mediaPopupDelayTimer == null)
                    {
                        _mediaPopupDelayTimer = new System.Windows.Threading.DispatcherTimer
                        {
                            Interval = TimeSpan.FromSeconds(Settings.MediaPopupHoverDelay)
                        };

                        Settings.MediaPopupSettingsChanged += () =>
                        {
                            if (_mediaPopupDelayTimer != null)
                            {
                                _mediaPopupDelayTimer.Interval = TimeSpan.FromSeconds(Settings.MediaPopupHoverDelay);
                            }
                        };

                        _mediaPopupDelayTimer.Tick += (s, e) =>
                        {
                            _mediaPopupDelayTimer.Stop();

                            if (Settings.MediaPopupShowOnlyWhenPlaying && !DataModel.MediaSessionService.Instance.IsMediaPlaying)
                            {
                                return;
                            }

                            _mediaPopup.ShowPopup(_trayIcon.IconBounds);
                            RefreshTrayTooltipPresentation();
                        };

                        Trace.WriteLine("MediaPopup: Timer created on first hover (lazy init)");
                    }

                    if (_mediaPopup.IsShowing)
                    {
                        RefreshTrayTooltipPresentation();
                    }
                    else
                    {
                        _mediaPopupDelayTimer.Start();
                    }
                }
                else
                {
                    _mediaPopupDelayTimer?.Stop();
                    _mediaPopup.StartHideTimer();
                    RefreshTrayTooltipPresentation();
                }
            };

            _mediaPopup.PopupHidden += (_, __) =>
            {
                RefreshTrayTooltipPresentation();
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

            var hiddenByDevice = CollectionViewModel.AllDevices
                .Where(device => device.HiddenAppsCount > 0)
                .OrderBy(device => device.DisplayName)
                .ToList();

            if (hiddenByDevice.Any())
            {
                var hiddenChildren = hiddenByDevice.Select(device =>
                {
                    var perDeviceEntries = CollectionViewModel.GetHiddenAppsForDevice(device.Id);
                    var perDeviceChildren = perDeviceEntries.Select(entry => new ContextMenuItem
                    {
                        DisplayName = CollectionViewModel.GetHiddenAppLabel(entry),
                        Command = new RelayCommand(() => CollectionViewModel.UnhideAppOnDevice(device.Id, entry.AppId, entry.ExeName)),
                    }).ToList();

                    perDeviceChildren.Add(new ContextMenuSeparator());
                    perDeviceChildren.Add(new ContextMenuItem
                    {
                        DisplayName = EarTrumpet.Properties.Resources.RestoreHiddenAppsForDeviceAll,
                        Command = new RelayCommand(() => CollectionViewModel.UnhideAllAppsForDevice(device.Id)),
                    });

                    return new ContextMenuItem
                    {
                        DisplayName = string.Format(EarTrumpet.Properties.Resources.ContextMenuRestoreHiddenAppsForDeviceFormat, device.DisplayName, device.HiddenAppsCount),
                        Children = perDeviceChildren,
                    };
                }).ToList();

                hiddenChildren.Add(new ContextMenuSeparator());
                hiddenChildren.Add(new ContextMenuItem
                {
                    DisplayName = EarTrumpet.Properties.Resources.ContextMenuRestoreAllHiddenApps,
                    Command = new RelayCommand(() => CollectionViewModel.UnhideAllApps()),
                });

                ret.Add(new ContextMenuSeparator());
                ret.Add(new ContextMenuItem
                {
                    DisplayName = string.Format(EarTrumpet.Properties.Resources.ContextMenuHiddenAppsTitleFormat, hiddenByDevice.Sum(device => device.HiddenAppsCount)),
                    Glyph = "\xE738",
                    Children = hiddenChildren,
                });
            }

            var hiddenDevicesCount = CollectionViewModel.GetTotalHiddenDevicesCount();
            if (hiddenDevicesCount > 0)
            {
                var hiddenDevices = CollectionViewModel.GetHiddenDevices();
                var hiddenDeviceChildren = hiddenDevices.Select(entry => new ContextMenuItem
                {
                    DisplayName = entry.DisplayName ?? entry.DeviceId,
                    Command = new RelayCommand(() => CollectionViewModel.UnhideDevice(entry.DeviceId)),
                }).ToList();

                hiddenDeviceChildren.Add(new ContextMenuSeparator());
                hiddenDeviceChildren.Add(new ContextMenuItem
                {
                    DisplayName = EarTrumpet.Properties.Resources.ContextMenuRestoreAllHiddenDevices,
                    Command = new RelayCommand(() => CollectionViewModel.UnhideAllDevices()),
                });

                if (!hiddenByDevice.Any())
                {
                    ret.Add(new ContextMenuSeparator());
                }

                ret.Add(new ContextMenuItem
                {
                    DisplayName = string.Format(EarTrumpet.Properties.Resources.ContextMenuHiddenDevicesTitleFormat, hiddenDevicesCount),
                    Glyph = "\xE8EA",
                    Children = hiddenDeviceChildren,
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
                    DisplayName = string.Format(EarTrumpet.Properties.Resources.UpdateContextMenu, _updateService.LatestVersion),
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

        private Rect? TryGetTrayIconBounds()
        {
            Rect bounds;
            if (_trayIcon != null && _trayIcon.TryGetIconBounds(out bounds))
            {
                return bounds;
            }

            return null;
        }

        private void ApplyQuickTrumpetPreset(string nameOrSlug)
        {
            try
            {
                var service = new VolumeProfileService(Settings);
                var profile = service.FindProfile(nameOrSlug);
                if (profile == null || CollectionViewModel == null) return;

                var result = service.ApplyProfile(profile, CollectionViewModel, _deviceManager as IAudioDeviceManagerWindowsAudio);
                ShowQuickTrumpetConfirmation(profile.Name, result);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"ApplyQuickTrumpetPreset failed: {ex.Message}");
            }
        }
    }
}
