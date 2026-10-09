using EarTrumpet.DataModel;
using EarTrumpet.Extensions;
using EarTrumpet.Interop;
using EarTrumpet.Interop.Helpers;
using EarTrumpet.UI.Helpers;
using EarTrumpet.UI.ViewModels;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Shell;

namespace EarTrumpet.UI.Views
{
    public partial class WebSettingsWindow : Window
    {
        private const string SettingsHostName = "bettertrumpet.settings";
        private const int WmNcLButtonDown = 0x00A1;
        private static readonly IntPtr HtCaption = new IntPtr(2);

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private readonly SettingsViewModel _viewModel;
        private SettingsWindow _legacyWindow;
        private bool _isInitialized;
        private bool _isCapturingHotkey;

        internal WebSettingsWindow(SettingsViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            InitializeComponent();
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 0,
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = new Thickness(0),
                ResizeBorderThickness = SystemParameters.WindowResizeBorderThickness,
                UseAeroCaptionButtons = false,
            });
            Trace.WriteLine("WebSettingsWindow .ctor");

            // Keep the web UI in sync when the default playback device
            // changes outside the settings window (tray, hotkey, Windows).
            var deviceManager = (Application.Current as App)?.AudioDeviceManager;
            if (deviceManager != null)
            {
                EventHandler<EarTrumpet.DataModel.Audio.IAudioDevice> onDefaultChanged = (_, __) =>
                {
                    if (_isInitialized)
                    {
                        Dispatcher.BeginInvoke(new Action(PostState));
                    }
                };
                deviceManager.DefaultChanged += onDefaultChanged;
                Closed += (_, __) => deviceManager.DefaultChanged -= onDefaultChanged;
            }

            Loaded += async (_, __) => await InitializeWebViewAsync();
            Loaded += (_, __) => StartSplashPulse();
            Closed += (_, __) =>
            {
                ResumeHotkeys();
                StopStatusTimer();
                Trace.WriteLine("WebSettingsWindow Closed");
            };

            SourceInitialized += (sender, __) =>
            {
                this.Cloak();
                this.EnableRoundedCornersIfApplicable();

                if (App.Settings.SettingsWindowPlacement != null)
                {
                    User32.SetWindowPlacement(new WindowInteropHelper((Window)sender).Handle, App.Settings.SettingsWindowPlacement.Value);
                }
            };

            StateChanged += OnWindowStateChanged;
            Closing += (sender, __) =>
            {
                if (User32.GetWindowPlacement(new WindowInteropHelper((Window)sender).Handle, out var placement))
                {
                    App.Settings.SettingsWindowPlacement = placement;
                }
            };
        }

        private async Task InitializeWebViewAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            ErrorOverlay.Visibility = Visibility.Collapsed;

            // Native acrylic behind the web view: the web UI keeps its own translucent
            // sage surface so the Windows backdrop shows through, like the flyout.
            try
            {
                var isDark = !EarTrumpet.DataModel.SystemSettings.IsLightTheme;
                var tint = isDark
                    ? System.Windows.Media.Color.FromArgb(0xC8, 0x14, 0x12, 0x18)
                    : System.Windows.Media.Color.FromArgb(0xC8, 0xEF, 0xED, 0xF5);
                AccentPolicyLibrary.EnableAcrylic(SettingsWebView, tint, EarTrumpet.Interop.User32.AccentFlags.None);
                Trace.WriteLine("WebSettingsWindow acrylic enabled");
            }
            catch (Exception accentEx)
            {
                Trace.WriteLine($"WebSettingsWindow acrylic unavailable: {accentEx.Message}");
            }

            try
            {
                var bundlePath = Path.Combine(AppContext.BaseDirectory, "SettingsWeb");
                var indexPath = Path.Combine(bundlePath, "index.html");
                if (!File.Exists(indexPath))
                {
                    throw new FileNotFoundException("The compiled settings bundle was not found.", indexPath);
                }

                var userDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BetterTrumpet",
                    "WebView2");
                Directory.CreateDirectory(userDataPath);

                var environment = await CoreWebView2Environment.CreateAsync(null, userDataPath);
                await SettingsWebView.EnsureCoreWebView2Async(environment);

                var core = SettingsWebView.CoreWebView2;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.AreDevToolsEnabled = IsDebugBuild();
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = false;

                // Disable all browser shortcuts (Ctrl+F, Ctrl+G, F5, etc.)
                core.Settings.AreBrowserAcceleratorKeysEnabled = false;

                core.SetVirtualHostNameToFolderMapping(
                    SettingsHostName,
                    bundlePath,
                    CoreWebView2HostResourceAccessKind.DenyCors);

                core.NavigationStarting += Core_NavigationStarting;
                core.NavigationCompleted += Core_NavigationCompleted;
                core.WebMessageReceived += Core_WebMessageReceived;
                core.ProcessFailed += (_, args) =>
                {
                    Trace.WriteLine($"WebSettingsWindow WebView2 process failed: {args.ProcessFailedKind}");
                    Dispatcher.BeginInvoke(new Action(ShowLoadError));
                };

                _isInitialized = true;
                StartStatusTimer();
                // The virtual-host mapping is served through Chromium's HTTP
                // cache, which can keep serving a stale index.html after a
                // rebuild (the page URL itself never changes). A version query
                // busts that entry; the hashed asset filenames inside the page
                // handle caching of the JS/CSS bundles.
                var bundleStamp = File.Exists(indexPath) ? File.GetLastWriteTimeUtc(indexPath).Ticks : 0;
                core.Navigate($"https://{SettingsHostName}/index.html?v={bundleStamp}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"WebSettingsWindow initialization failed: {ex}");
                ShowLoadError();
            }
        }

        private bool _splashHidden;

        private void StartSplashPulse()
        {
            if (!SystemParameters.ClientAreaAnimation) return;
            var pulse = new DoubleAnimation(1, 0.7, TimeSpan.FromMilliseconds(1100))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            SplashLogo.BeginAnimation(UIElement.OpacityProperty, pulse);
        }

        private void HideSplash()
        {
            if (_splashHidden) return;
            _splashHidden = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (SystemParameters.ClientAreaAnimation)
                {
                    var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(320))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    };
                    fade.Completed += (_, __) => LoadingSplash.Visibility = Visibility.Collapsed;
                    LoadingSplash.BeginAnimation(UIElement.OpacityProperty, fade);
                }
                else
                {
                    LoadingSplash.Visibility = Visibility.Collapsed;
                }
            }));
        }

        private System.Windows.Threading.DispatcherTimer _statusTimer;

        // Live status for the performance page: effective peak-meter FPS follows
        // eco mode and battery rules, so it can change while the window is open.
        private void StartStatusTimer()
        {
            if (_statusTimer != null) return;
            _statusTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _statusTimer.Tick += (_, __) =>
            {
                if (_isInitialized && SettingsWebView.CoreWebView2 != null)
                {
                    PostMessage(new
                    {
                        type = "status",
                        data = new
                        {
                            effectivePeakMeterFps = App.Settings.EffectivePeakMeterFps,
                            ecoModeActive = App.Settings.IsEffectiveEcoMode,
                        },
                    });
                }
            };
            _statusTimer.Start();
        }

        private void StopStatusTimer()
        {
            _statusTimer?.Stop();
            _statusTimer = null;
        }

        private void Core_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {            if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Host, SettingsHostName, StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                Trace.WriteLine($"WebSettingsWindow blocked navigation: {e.Uri}");
            }
        }

        private void Core_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                Trace.WriteLine($"WebSettingsWindow navigation failed: {e.WebErrorStatus}");
                ShowLoadError();
            }
        }

        private void Core_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var document = JsonDocument.Parse(e.WebMessageAsJson);
                var root = document.RootElement;
                if (!root.TryGetProperty("type", out var typeElement))
                {
                    return;
                }

                switch (typeElement.GetString())
                {
                    case "ready":
                        PostState();
                        break;
                    case "setSetting":
                        ApplySetting(root);
                        break;
                    case "openLegacy":
                        var pageId = root.TryGetProperty("pageId", out var pageElement) && pageElement.ValueKind == JsonValueKind.String
                            ? pageElement.GetString()
                            : null;
                        OpenLegacySettings(pageId);
                        break;
                    case "windowAction":
                        HandleWindowAction(root);
                        break;
                    case "action":
                        HandleAction(root);
                        break;
                    case "hotkeyCaptureStarted":
                        if (!_isCapturingHotkey)
                        {
                            HotkeyManager.Current.Pause();
                            _isCapturingHotkey = true;
                        }
                        break;
                    case "hotkeyCaptureEnded":
                        // The web UI abandoned capture (e.g. the button lost
                        // focus) without recording a key; resume hotkeys so
                        // global shortcuts do not stay paused forever.
                        ResumeHotkeys();
                        break;
                    case "setHotkey":
                        SetHotkey(root);
                        break;
                    case "rendered":
                        // The React app has painted its first real frame:
                        // fade the branded splash away.
                        HideSplash();
                        break;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"WebSettingsWindow message failed: {ex}");
                PostMessage(new { type = "error", message = ex.Message });
            }
        }

        private void HandleWindowAction(JsonElement message)
        {
            var action = message.TryGetProperty("action", out var actionElement) &&
                actionElement.ValueKind == JsonValueKind.String
                ? actionElement.GetString()
                : null;

            switch (action)
            {
                case "minimize":
                    WindowState = WindowState.Minimized;
                    break;
                case "close":
                    Close();
                    break;
                case "drag":
                    ReleaseCapture();
                    User32.SendMessage(new WindowInteropHelper(this).Handle, WmNcLButtonDown, HtCaption, IntPtr.Zero);
                    break;
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReleaseCapture();

        private void ApplySetting(JsonElement message)
        {
            if (!message.TryGetProperty("key", out var keyElement) ||
                !message.TryGetProperty("value", out var valueElement))
            {
                return;
            }

            var key = keyElement.GetString();
            var colors = GetPage<EarTrumpetColorsSettingsPageViewModel>();
            var updates = GetPage<EarTrumpetUpdatesPageViewModel>();
            var privacy = GetPage<EarTrumpetPrivacyPageViewModel>();
            var about = GetPage<EarTrumpetAboutPageViewModel>();

            switch (key)
            {
                case "runAtStartup":
                    App.Settings.RunAtStartup = valueElement.GetBoolean();
                    break;
                case "useLegacyIcon":
                    App.Settings.UseLegacyIcon = valueElement.GetBoolean();
                    break;
                case "showAppTooltips":
                    App.Settings.ShowAppTooltips = valueElement.GetBoolean();
                    break;
                case "useScrollWheelInTray":
                    App.Settings.UseScrollWheelInTray = valueElement.GetBoolean();
                    break;
                case "useGlobalMouseWheelHook":
                    App.Settings.UseGlobalMouseWheelHook = valueElement.GetBoolean();
                    break;
                case "useLogarithmicVolume":
                    App.Settings.UseLogarithmicVolume = valueElement.GetBoolean();
                    break;
                case "useVolumeTickSound":
                    App.Settings.UseVolumeTickSound = valueElement.GetBoolean();
                    break;
                case "notifyOnDeviceChange":
                    App.Settings.NotifyOnDefaultDeviceChange = valueElement.GetBoolean();
                    break;
                case "showDeviceSwitchNotification":
                    App.Settings.ShowDeviceSwitchNotification = valueElement.GetBoolean();
                    break;
                case "useFocusLostVolume":
                    App.Settings.UseFocusLostVolume = valueElement.GetBoolean();
                    break;
                case "focusLostAttenuatePercent":
                    App.Settings.FocusLostAttenuatePercent = Math.Max(0, Math.Min(100, valueElement.GetInt32()));
                    break;
                case "focusLostFadeDurationMs":
                    App.Settings.FocusLostFadeDurationMs = Math.Max(0, Math.Min(EarTrumpet.Logic.FocusLostFadePolicy.MaxDurationMs, valueElement.GetInt32()));
                    break;
                case "focusLostSelectedAppsOnly":
                    App.Settings.FocusLostSelectedAppsOnly = valueElement.GetBoolean();
                    break;
                case "showQuickTrumpetConfirmation":
                    App.Settings.ShowQuickTrumpetConfirmation = valueElement.GetBoolean();
                    break;
                case "quickTrumpetNotificationSeconds":
                    App.Settings.QuickTrumpetNotificationSeconds = valueElement.GetInt32();
                    break;
                case "mediaPopupEnabled":
                    App.Settings.MediaPopupEnabled = valueElement.GetBoolean();
                    break;
                case "mediaPopupHoverDelay":
                    App.Settings.MediaPopupHoverDelay = valueElement.GetDouble();
                    break;
                case "showWhenPaused":
                    App.Settings.MediaPopupShowOnlyWhenPlaying = !valueElement.GetBoolean();
                    break;
                case "mediaPopupRememberExpanded":
                    App.Settings.MediaPopupRememberExpanded = valueElement.GetBoolean();
                    break;
                case "ecoMode":
                    App.Settings.EcoMode = valueElement.GetBoolean();
                    break;
                case "autoEcoMode":
                    App.Settings.AutoEcoMode = valueElement.GetBoolean();
                    break;
                case "useSmoothVolumeAnimation":
                    App.Settings.UseSmoothVolumeAnimation = valueElement.GetBoolean();
                    break;
                case "volumeAnimationSpeed":
                    App.Settings.VolumeAnimationSpeed = 0.02 + (Math.Max(1, Math.Min(10, valueElement.GetInt32())) - 1) * 0.053;
                    break;
                case "peakMeterFps":
                    App.Settings.PeakMeterFps = valueElement.GetInt32();
                    break;
                case "useCustomSliderColors" when colors != null:
                    colors.UseCustomSliderColors = valueElement.GetBoolean();
                    break;
                case "peakMeterStyleIndex" when colors != null:
                    colors.PeakMeterStyleIndex = valueElement.GetInt32();
                    break;
                case "windowBackgroundOpacity" when colors != null:
                    colors.WindowBackgroundOpacity = valueElement.GetDouble();
                    break;
                case "useDynamicAlbumArtTheme" when colors != null:
                    colors.UseDynamicAlbumArtTheme = valueElement.GetBoolean();
                    break;
                case "sliderThumbColor" when colors != null:
                    colors.SliderThumbColorHex = valueElement.GetString();
                    break;
                case "sliderTrackFillColor" when colors != null:
                    colors.SliderTrackFillColorHex = valueElement.GetString();
                    break;
                case "sliderTrackBackgroundColor" when colors != null:
                    colors.SliderTrackBackgroundColorHex = valueElement.GetString();
                    break;
                case "peakMeterColor" when colors != null:
                    colors.PeakMeterColorHex = valueElement.GetString();
                    break;
                case "windowBackgroundColor" when colors != null:
                    colors.WindowBackgroundColorHex = valueElement.GetString();
                    break;
                case "textColor" when colors != null:
                    colors.TextColorHex = valueElement.GetString();
                    break;
                case "accentGlowColor" when colors != null:
                    colors.AccentGlowColorHex = valueElement.GetString();
                    break;
                case "isTelemetryEnabled" when privacy != null:
                    privacy.IsTelemetryEnabled = valueElement.GetBoolean();
                    break;
                case "announcementsEnabled":
                    App.Settings.AnnouncementsEnabled = valueElement.GetBoolean();
                    break;
                case "autoCheckForUpdates" when updates != null:
                    updates.AutoCheckForUpdates = valueElement.GetBoolean();
                    break;
                case "updateChannelIndex" when updates != null:
                    updates.UpdateChannelIndex = valueElement.GetInt32();
                    break;
                case "useMonkeyTickSound" when about != null:
                    about.UseMonkeyTickSound = valueElement.GetBoolean();
                    break;
                default:
                    return;
            }

            PostState();
        }

        private void HandleAction(JsonElement message)
        {
            var action = GetString(message, "action");
            var profiles = GetPage<EarTrumpetVolumeProfilesSettingsPageViewModel>();
            var rules = GetPage<EarTrumpetAppRulesSettingsPageViewModel>();
            var colors = GetPage<EarTrumpetColorsSettingsPageViewModel>();
            var updates = GetPage<EarTrumpetUpdatesPageViewModel>();
            var privacy = GetPage<EarTrumpetPrivacyPageViewModel>();
            var about = GetPage<EarTrumpetAboutPageViewModel>();

            switch (action)
            {
                case "restoreHiddenApp":
                    App.Settings.UnhideAppForDevice(GetString(message, "deviceId"), GetString(message, "appId"), GetString(message, "exeName"));
                    break;
                case "restoreAllHiddenApps":
                    App.Settings.UnhideAllApps();
                    break;
                case "restoreHiddenDevice":
                    App.Settings.UnhideDevice(GetString(message, "deviceId"));
                    break;
                case "restoreAllHiddenDevices":
                    App.Settings.UnhideAllDevices();
                    break;
                case "profileSelect" when profiles != null:
                    SelectProfile(profiles, message);
                    break;
                case "profileCapture" when profiles != null:
                    profiles.CaptureProfile(GetString(message, "name"), ReadCaptureOptions(message));
                    break;
                case "profileUpdateFromCurrent" when profiles != null:
                    profiles.UpdateProfileFromCurrent(ResolveProfile(profiles, message));
                    break;
                case "profileSetInclude" when profiles != null:
                    profiles.SetProfileIncludes(ResolveProfile(profiles, message),
                        GetOptionalBoolean(message, "deviceVolumes"),
                        GetOptionalBoolean(message, "appVolumes"),
                        GetOptionalBoolean(message, "routeApps"));
                    break;
                case "profileDeviceUpdate" when profiles != null:
                    profiles.SetProfileDeviceEntry(ResolveProfile(profiles, message), GetString(message, "key"),
                        GetOptionalInt32(message, "volume"), GetOptionalBoolean(message, "muted"));
                    break;
                case "profileDeviceRemove" when profiles != null:
                    profiles.RemoveProfileDeviceEntry(ResolveProfile(profiles, message), GetString(message, "key"));
                    break;
                case "profileDeviceAdd" when profiles != null:
                    profiles.AddProfileDeviceEntry(ResolveProfile(profiles, message), GetString(message, "key"));
                    break;
                case "profileAppUpdate" when profiles != null:
                    profiles.SetProfileAppEntry(ResolveProfile(profiles, message), GetString(message, "key"),
                        GetOptionalInt32(message, "volume"), GetOptionalBoolean(message, "muted"));
                    break;
                case "profileAppRemove" when profiles != null:
                    profiles.RemoveProfileAppEntry(ResolveProfile(profiles, message), GetString(message, "key"));
                    break;
                case "profileSetDefault" when profiles != null:
                    var role = GetInt32(message, "role");
                    if (Enum.IsDefined(typeof(VolumeProfileService.DefaultDeviceRole), role))
                    {
                        profiles.SetProfileDefaultDevice(ResolveProfile(profiles, message), (VolumeProfileService.DefaultDeviceRole)role, GetString(message, "deviceId"));
                    }
                    break;
                // Web actions never raise WPF MessageBoxes: applying is
                // immediate and destructive actions are confirmed inline by
                // the web UI, which sends `confirmed: true`.
                case "profileApply" when profiles != null:
                    SelectProfile(profiles, message);
                    profiles.ApplySelectedProfile(confirm: false);
                    break;
                case "profileDelete" when profiles != null:
                    SelectProfile(profiles, message);
                    profiles.DeleteSelectedProfile(confirm: false);
                    break;
                case "profileExport" when profiles != null:
                    SelectProfile(profiles, message);
                    profiles.ExportProfileCommand.Execute(null);
                    break;
                case "profileImport" when profiles != null:
                    profiles.ImportProfileCommand.Execute(null);
                    break;
                case "profileAppsOnly" when profiles != null:
                    SelectProfile(profiles, message);
                    profiles.SelectedProfileApplyAppsOnly = GetBoolean(message, "value");
                    break;
                case "profileRename" when profiles != null:
                    SelectProfile(profiles, message);
                    profiles.RenameSelectedProfile(GetString(message, "name"));
                    break;
                case "appRuleAdd":
                    // Adding creates a neutral profile; every behavior stays
                    // opt-in, matching the classic window.
                    var exeName = EarTrumpet.Logic.AppIdentity.NormalizeExeName(GetString(message, "exeName"));
                    if (!string.IsNullOrWhiteSpace(exeName))
                    {
                        App.Settings.AddAppRule(exeName, exeName);
                    }
                    break;
                case "appRuleBrowse" when rules != null:
                    rules.BrowseForExeCommand.Execute(null);
                    break;
                case "appRuleUpdate":
                    UpdateAppRule(message);
                    break;
                case "appRuleRemove":
                    App.Settings.RemoveAppRule(GetString(message, "exeName"));
                    break;
                case "appRuleClear" when rules != null:
                    rules.ClearAllRules(confirm: false);
                    break;
                case "folderRuleAdd" when rules != null:
                    rules.AddFolderVolumeRuleCommand.Execute(null);
                    break;
                case "folderRuleUpdate":
                    App.Settings.UpdateFolderVolumeRule(GetString(message, "id"), GetString(message, "folderPath"), GetInt32(message, "volumePercent"));
                    break;
                case "folderRuleBrowse" when rules != null:
                    var folderRule = rules.FolderVolumeRules.FirstOrDefault(rule => rule.Id == GetString(message, "id"));
                    if (folderRule != null)
                    {
                        rules.BrowseForFolderVolumeRuleCommand.Execute(folderRule);
                    }
                    break;
                case "folderRuleRemove":
                    App.Settings.RemoveFolderVolumeRule(GetString(message, "id"));
                    break;
                case "themeSelect" when colors != null:
                    colors.SelectedTheme = colors.AvailableThemes.Concat(colors.CustomThemes)
                        .FirstOrDefault(theme => string.Equals(theme.Name, GetString(message, "name"), StringComparison.Ordinal));
                    break;
                case "themeRandomize" when colors != null:
                    colors.RandomizeColorsCommand.Execute(null);
                    break;
                case "themeReset" when colors != null:
                    colors.ResetToDefaultCommand.Execute(null);
                    break;
                case "themeSave" when colors != null:
                    colors.NewThemeName = GetString(message, "name");
                    colors.SaveCustomThemeCommand.Execute(null);
                    break;
                case "themeExport" when colors != null:
                    colors.ExportThemeToFileCommand.Execute(null);
                    break;
                case "themeImport" when colors != null:
                    colors.ImportThemeFromFileCommand.Execute(null);
                    break;
                case "themeDelete" when colors != null:
                    var customTheme = colors.CustomThemes.FirstOrDefault(theme =>
                        string.Equals(theme.Name, GetString(message, "name"), StringComparison.Ordinal));
                    if (customTheme != null)
                    {
                        colors.DeleteCustomThemeCommand.Execute(customTheme);
                    }
                    break;
                case "checkUpdate" when updates != null:
                    updates.CheckForUpdateCommand.Execute(null);
                    break;
                case "installUpdate" when updates != null:
                    updates.DownloadAndInstall();
                    break;
                case "diagnostics" when about != null:
                    about.OpenDiagnosticsCommand.Execute(null);
                    break;
                case "github" when about != null:
                    about.OpenGitHubCommand.Execute(null);
                    break;
                case "feedback" when about != null:
                    about.OpenFeedbackCommand.Execute(null);
                    break;
                case "bugReport" when about != null:
                    about.OpenBugReportCommand.Execute(null);
                    break;
                case "settingsExport" when privacy != null:
                    privacy.ExportSettingsCommand.Execute(null);
                    break;
                case "settingsImport" when privacy != null:
                    privacy.ImportSettingsCommand.Execute(null);
                    break;
                case "openUrl":
                    EarTrumpet.Interop.Helpers.ProcessHelper.OpenWebLinkNoThrow(GetString(message, "url"));
                    break;
                default:
                    return;
            }

            PostState();
        }

        private void PostState()
        {
            var legacy = GetPage<EarTrumpetLegacySettingsPageViewModel>();
            var profiles = GetPage<EarTrumpetVolumeProfilesSettingsPageViewModel>();
            var colors = GetPage<EarTrumpetColorsSettingsPageViewModel>();
            var updates = GetPage<EarTrumpetUpdatesPageViewModel>();
            var about = GetPage<EarTrumpetAboutPageViewModel>();

            // Hotkeys, the CLI and settings import write presets through their own
            // service instances; pick their changes up before describing the list.
            profiles?.RefreshProfiles();
            var audio = BuildAudioState();

            var categories = _viewModel.Categories.Select(category => new
            {
                category.Title,
                pages = category.Pages.Select(page => new
                {
                    id = GetPageId(page),
                    page.Title,
                    // The shared privacy subtitle still mentions diagnostics, which
                    // live on About in the web UI.
                    subtitle = page is EarTrumpetPrivacyPageViewModel ? R("WebSettingsPrivacyPageSubtitle") : page.Subtitle ?? string.Empty,
                    migrated = IsMigrated(page),
                }),
            });

            var payload = new
            {
                appName = "BetterTrumpet",
                locale = CultureInfo.CurrentUICulture.Name,
                categories,
                labels = StripTrailingColons(new Dictionary<string, string>
                {
                    ["searchPlaceholder"] = R("WebSettingsSearchPlaceholder"), ["classicSettings"] = R("WebSettingsClassicButton"),
                    ["noResults"] = R("WebSettingsNoResults"),
                    ["minimize"] = R("WebSettingsMinimize"), ["close"] = R("WebSettingsClose"),
                    ["navigation"] = R("WebSettingsNavigation"), ["collapseSidebar"] = R("WebSettingsCollapseSidebar"),
                    ["expandSidebar"] = R("WebSettingsExpandSidebar"), ["closeNavigation"] = R("WebSettingsCloseNavigation"),
                    ["openNavigation"] = R("WebSettingsOpenNavigation"), ["loading"] = R("WebSettingsLoading"),
                    ["pressShortcut"] = R("WebSettingsPressShortcut"), ["ecoActive"] = R("WebSettingsEcoActive"),
                    ["profileSelected"] = R("WebSettingsProfileSelected"),
                    ["copyright"] = $"© {DateTime.Now.Year} xammen",
                    ["essentials"] = R("WebSettingsCategoryEssentials"), ["audio"] = R("WebSettingsCategoryAudio"),
                    ["experience"] = R("WebSettingsCategoryExperience"), ["application"] = R("AppCategoryTitle"),
                    ["startupTitle"] = R("SettingsStartup"), ["startupDescription"] = R("SettingsStartupDesc"),
                    ["runAtStartup"] = R("SettingsRunAtStartup"), ["trayTitle"] = R("SettingsTrayIcon"),
                    ["trayDescription"] = R("SettingsTrayIconDesc"), ["useLegacyIcon"] = R("SettingsUseLegacyEarTrumpetIcon"),
                    ["showAppTooltips"] = R("SettingsShowAppTooltips"), ["showAppTooltipsDescription"] = R("SettingsAppTooltipsDesc"),
                    ["hiddenApps"] = R("SettingsHiddenAppsTitle"), ["hiddenAppsDescription"] = R("SettingsHiddenAppsDesc"),
                    ["restoreAll"] = R("SettingsRestoreHiddenApps"), ["restoreAllDevices"] = R("WebSettingsRestoreAllDevices"),
                    ["restore"] = R("WebSettingsRestoreButton"),
                    ["hiddenDevices"] = R("WebSettingsHiddenDevices"),
                    ["scrollWheelTitle"] = R("SettingsScrollWheel"), ["scrollWheelDescription"] = R("SettingsScrollWheelDesc"),
                    ["useScrollWheelInTray"] = R("SettingsUseScrollWheelInTray"), ["useScrollWheelInTrayDescription"] = R("SettingsScrollWheelTrayTip"),
                    ["useGlobalMouseWheelHook"] = R("SettingsUseGlobalMouseWheelHook"), ["useGlobalMouseWheelHookDescription"] = R("SettingsScrollWheelGlobalTip"),
                    ["volumeScaleTitle"] = R("SettingsVolumeScale"), ["volumeScaleDescription"] = R("SettingsVolumeScaleDesc"),
                    ["useLogarithmicVolume"] = R("SettingsUseLogarithmicVolume"), ["useVolumeTickSound"] = R("SettingsVolumeTickSound"),
                    ["useVolumeTickSoundDescription"] = R("SettingsVolumeTickSoundTip"), ["shortcuts"] = R("ShortcutsPageText"),
                    ["deviceChangeTitle"] = R("SettingsDeviceChangeNotify"), ["deviceChangeDescription"] = R("SettingsDeviceChangeNotifyDesc"),
                    ["notifyOnDeviceChange"] = R("SettingsNotifyOnDeviceChange"),
                    ["showDeviceSwitchNotification"] = R("WebSettingsShowDeviceSwitchNotification"),
                    ["showDeviceSwitchNotificationDescription"] = R("WebSettingsShowDeviceSwitchNotificationDesc"),
                    ["focusLostTitle"] = R("SettingsFocusLostVolume"), ["focusLostDescription"] = R("SettingsFocusLostVolumeDesc"),
                    ["useFocusLostVolume"] = R("SettingsUseFocusLostVolume"),
                    ["focusLostAttenuate"] = R("SettingsFocusLostAttenuate"), ["focusLostAttenuateHint"] = R("SettingsFocusLostAttenuateHint"),
                    ["focusLostFade"] = R("SettingsFocusLostFade"), ["focusLostFadeHint"] = R("SettingsFocusLostFadeHint"),
                    ["focusLostScope"] = R("SettingsFocusLostScope"), ["focusLostAllApps"] = R("SettingsFocusLostAllApps"),
                    ["focusLostSelectedApps"] = R("SettingsFocusLostSelectedApps"), ["focusLostSelectedHint"] = R("SettingsFocusLostSelectedHint"),
                    ["recordShortcut"] = R("WebSettingsRecordShortcut"), ["clearShortcut"] = R("WebSettingsClearShortcut"),
                    ["deviceShortcuts"] = R("WebSettingsDeviceShortcuts"), ["deviceShortcutsDesc"] = R("WebSettingsDeviceShortcutsDesc"),
                    ["defaultDeviceBadge"] = R("WebSettingsDefaultDeviceBadge"),
                    ["profileCapture"] = R("SettingsSaveCurrentVolumes"), ["profileCaptureDescription"] = R("SettingsSaveCurrentVolumesDesc"),
                    ["profileName"] = R("WebSettingsPresetName"), ["presetName"] = R("WebSettingsPresetName"),
                    ["themeName"] = R("WebSettingsThemeName"), ["allDevices"] = R("SettingsQuickTrumpetAllDevices"),
                    ["confirmation"] = R("SettingsQuickTrumpetConfirmation"), ["savedProfiles"] = R("SettingsSavedProfiles"),
                    ["apply"] = R("SettingsProfileApply"), ["delete"] = R("SettingsProfileDelete"),
                    ["save"] = R("WebSettingsSave"), ["rename"] = R("WebSettingsRename"),
                    ["emptyProfilesHint"] = R("WebSettingsQuickTrumpetEmptyHint"),
                    ["appsOnlyDescription"] = R("SettingsQuickTrumpetAppsOnlyDesc"),
                    ["export"] = R("SettingsProfileExport"), ["import"] = R("SettingsProfileImport"),
                    ["appsOnly"] = R("SettingsQuickTrumpetAppsOnly"), ["appRules"] = R("AppRulesListHeaderText"),
                    ["appRulesDescription"] = R("AppRulesListHeaderDesc"), ["addApp"] = R("AppRulesAddRuleButtonText"),
                    ["appPlaceholder"] = R("AppRulesAddPlaceholderText"), ["browse"] = R("AppRulesBrowseButtonText"),
                    ["hardMute"] = R("AppRulesHardMuteColumnText"), ["focusLostRule"] = R("AppRulesFocusLostColumnText"), ["volumeBehavior"] = R("AppRulesVolumeBehaviorText"),
                    ["targetVolume"] = R("AppRulesTargetVolumeText"), ["modeNone"] = R("AppRulesModeNoneText"),
                    ["modeLaunch"] = R("AppRulesModeLaunchText"), ["modeLock"] = R("AppRulesModeLockText"),
                    ["clearAllRules"] = R("AppRulesClearAllButtonText"), ["folderRules"] = R("FolderVolumeRulesHeaderText"),
                    ["folderRulesDescription"] = R("FolderVolumeRulesHeaderDesc"),
                    ["addFolder"] = R("FolderVolumeRulesAddButtonText"), ["folderRulesEmpty"] = R("FolderVolumeRulesEmptyText"),
                    ["mediaPopup"] = R("SettingsMediaPopup"), ["mediaPopupDescription"] = R("SettingsMediaPopupDesc"),
                    ["enableMediaPopup"] = R("SettingsEnableMediaPopup"), ["interaction"] = R("SettingsInteraction"),
                    ["hoverDelay"] = R("SettingsHoverDelay"), ["showWhenPaused"] = R("SettingsShowWhenPaused"),
                    ["rememberExpanded"] = R("SettingsRememberExpanded"), ["ecoMode"] = R("SettingsEcoMode"),
                    ["ecoModeDescription"] = R("SettingsEcoModeDesc"), ["enableEcoMode"] = R("SettingsEcoModeEnable"),
                    ["autoEcoMode"] = R("SettingsAutoEcoMode"), ["animations"] = R("SettingsAnimations"),
                    ["smoothAnimation"] = R("SettingsSmoothVolumeAnimation"), ["animationSpeed"] = R("SettingsAnimationSpeedLabel"),
                    ["peakMeter"] = R("SettingsPeakMeter"), ["refreshRate"] = R("SettingsRefreshRate"),
                    ["effectiveRate"] = R("WebSettingsEffectiveRate"), ["effectiveRateHint"] = R("WebSettingsEffectiveRateHint"),
                    ["appearanceDescription"] = R("WebSettingsAppearanceDesc"),
                    ["dynamicAlbum"] = R("SettingsDynamicAlbumArt"), ["dynamicAlbumDescription"] = R("SettingsDynamicAlbumArtDesc"),
                    ["enableDynamicAlbum"] = R("SettingsEnableDynamicAlbumArt"), ["presets"] = R("SettingsTabPresets"),
                    ["customColors"] = R("SettingsCustomColors"), ["customColorsDescription"] = R("SettingsCustomColorsDesc"),
                    ["customColorsTweakHint"] = R("WebSettingsCustomColorsTweakHint"),
                    ["useCustomColors"] = R("SettingsUseCustomSliderColors"), ["windowOpacity"] = R("SettingsWindowOpacity"),
                    ["peakStyle"] = R("SettingsPeakMeterStyle"), ["randomize"] = R("SettingsRandomizeTooltip"),
                    ["reset"] = R("SettingsResetToDefault"), ["saveTheme"] = R("SettingsSaveTheme"),
                    ["sliderThumb"] = R("SettingsSliderThumbColor"), ["sliderFill"] = R("SettingsSliderTrackFillColor"),
                    ["sliderTrack"] = R("SettingsSliderTrackBackgroundColor"), ["peakColor"] = R("SettingsPeakMeterColor"),
                    ["windowColor"] = R("SettingsWindowBgColor"), ["textColor"] = R("SettingsTextColor"),
                    ["accentColor"] = R("SettingsAccentGlowColor"), ["updates"] = R("SettingsUpdates"),
                    ["updatesDescription"] = R("SettingsUpdatesDesc"), ["autoUpdates"] = R("AutoUpdateCheckboxText"),
                    ["notifyFor"] = R("SettingsNotifyFor"), ["checkUpdate"] = R("SettingsCheckUpdate"),
                    ["installUpdate"] = R("SettingsInstallUpdate"), ["privacyPageTitle"] = R("PrivacySettingsPageText"),
                    ["privacyPageSubtitle"] = R("WebSettingsPrivacyPageSubtitle"), ["privacy"] = R("PrivacyCheckboxText"),
                    ["privacyDescription"] = R("SettingsTelemetryDesc"), ["privacyPolicy"] = R("PrivacyPolicyText"),
                    ["whatsNewFeed"] = R("PrivacyAnnouncementsText"), ["whatsNewFeedDesc"] = R("PrivacyAnnouncementsDesc"),
                    ["settingsData"] = R("SettingsExportImport"),
                    ["settingsDataDescription"] = R("SettingsExportImportDesc"), ["exportSettings"] = R("SettingsExportSettings"),
                    ["importSettings"] = R("SettingsImportSettings"),
                    ["diagnostics"] = R("WebSettingsDiagnostics"), ["exportDiagnostics"] = R("WebSettingsExportDiagnostics"),
                    ["diagnosticsDescription"] = R("WebSettingsDiagnosticsDesc"),
                    ["github"] = R("AboutGitHub"), ["feedback"] = R("AboutFeedback"), ["bugReport"] = R("AboutReportBug"),
                    ["monkeySound"] = R("SettingsUseMonkeyTickSound"), ["monkeySoundDescription"] = R("SettingsUseMonkeyTickSoundTip"),
                    ["empty"] = R("WebSettingsEmpty"), ["seconds"] = R("WebSettingsSeconds"),
                    ["profileShortcut"] = R("SettingsQuickTrumpetShortcut"),
                    ["profileShortcutDescription"] = R("SettingsQuickTrumpetShortcutDesc"),
                    ["presetsDescription"] = R("WebSettingsQtPresetsDesc"),
                    ["newPreset"] = R("WebSettingsQtNewPreset"), ["newPresetDesc"] = R("WebSettingsQtNewPresetDesc"),
                    ["whatToSave"] = R("WebSettingsQtWhatToSave"), ["nothingSelected"] = R("WebSettingsQtNothingSelected"),
                    ["includeDefaults"] = R("WebSettingsQtIncludeDefaults"), ["includeDefaultsDesc"] = R("WebSettingsQtIncludeDefaultsDesc"),
                    ["includeDevices"] = R("WebSettingsQtIncludeDevices"), ["includeDevicesDesc"] = R("WebSettingsQtIncludeDevicesDesc"),
                    ["includeApps"] = R("WebSettingsQtIncludeApps"), ["includeAppsDesc"] = R("WebSettingsQtIncludeAppsDesc"),
                    ["appsOpenFormat"] = R("WebSettingsQtAppsOpenFormat"), ["devicesCheckedFormat"] = R("WebSettingsQtDevicesCheckedFormat"),
                    ["choose"] = R("WebSettingsQtChoose"),
                    ["rolePlayback"] = R("QuickTrumpetRolePlayback"), ["rolePlaybackComms"] = R("QuickTrumpetRolePlaybackComms"),
                    ["roleRecording"] = R("QuickTrumpetRoleRecording"), ["roleRecordingComms"] = R("QuickTrumpetRoleRecordingComms"),
                    ["dontChange"] = R("WebSettingsQtDontChange"), ["notConnected"] = R("WebSettingsQtNotConnected"),
                    ["noDevice"] = R("WebSettingsQtNoDevice"),
                    ["routeApps"] = R("WebSettingsQtRouteApps"), ["routeAppsDesc"] = R("WebSettingsQtRouteAppsDesc"),
                    ["updateFromCurrent"] = R("WebSettingsQtUpdateFromCurrent"), ["updateConfirm"] = R("WebSettingsQtUpdateConfirm"),
                    ["updateFromCurrentDesc"] = R("WebSettingsQtUpdateFromCurrentDesc"),
                    ["updated"] = R("WebSettingsQtUpdated"),
                    ["addDevice"] = R("WebSettingsQtAddDevice"), ["removeEntry"] = R("WebSettingsQtRemoveEntry"),
                    ["muteEntry"] = R("WebSettingsQtMuteEntry"),
                    ["noDeviceEntries"] = R("WebSettingsQtNoDeviceEntries"), ["noAppEntries"] = R("WebSettingsQtNoAppEntries"),
                    ["lastApplied"] = R("WebSettingsQtLastApplied"),
                    ["cycleTitle"] = R("WebSettingsQtCycleTitle"), ["cycleDesc"] = R("WebSettingsQtCycleDesc"),
                    ["nextPreset"] = R("SettingsQuickTrumpetNextHotkey"), ["previousPreset"] = R("SettingsQuickTrumpetPreviousHotkey"),
                    ["qtNotification"] = R("WebSettingsQtNotificationTitle"),
                    ["notificationDuration"] = R("WebSettingsQtNotificationDuration"),
                    ["notificationDurationDesc"] = R("WebSettingsQtNotificationDurationDesc"),
                    ["changeFolder"] = R("FolderVolumeRulesBrowseButtonText"),
                    ["deleteTheme"] = R("SettingsDeleteTitle"),
                    ["updateChannel0"] = R("SettingsUpdateChannelAllDesc"),
                    ["updateChannel1"] = R("SettingsUpdateChannelMinorMajorDesc"),
                    ["updateChannel2"] = R("SettingsUpdateChannelMajorOnlyDesc"),
                    ["updateChannel3"] = R("SettingsUpdateChannelNoneDesc"),
                    ["peakStyleClassic"] = R("WebSettingsPeakStyleClassic"), ["peakStyleDotted"] = R("WebSettingsPeakStyleDotted"),
                    ["peakStyleBlocks"] = R("WebSettingsPeakStyleBlocks"), ["peakStyleBars"] = R("WebSettingsPeakStyleBars"),
                    ["peakStyleWave"] = R("WebSettingsPeakStyleWave"), ["secondsShort"] = R("WebSettingsSecondsShort"),
                    ["shortcutsGlobal"] = R("WebSettingsShortcutsGlobal"), ["updatesSection"] = R("WebSettingsUpdatesSection"),
                    ["mediaPopupSection"] = R("WebSettingsMediaPopupSection"), ["privacySection"] = R("WebSettingsPrivacySection"),
                    ["privacySectionDescription"] = R("WebSettingsPrivacySectionDesc"),
                    ["customTheme"] = R("WebSettingsCustomTheme"), ["confirmDelete"] = R("WebSettingsConfirmDelete"),
                    ["confirmClearAll"] = R("WebSettingsConfirmClearAll"), ["cancel"] = R("WebSettingsCancel"),
                    ["saved"] = R("WebSettingsSaved"), ["applied"] = R("WebSettingsApplied"),
                    ["hexColor"] = R("WebSettingsHexColor"), ["preview"] = R("WebSettingsPreview"),
                    ["appHealth"] = R("WebSettingsAppHealth"), ["healthHint"] = R("WebSettingsHealthHint"),["searchShortcut"] = R("WebSettingsSearchShortcut"),
                    ["disabledByEco"] = R("WebSettingsDisabledByEco"),
                    ["requiresSmoothAnimation"] = R("WebSettingsRequiresSmoothAnimation"),
                    ["requiresAutoCheck"] = R("WebSettingsRequiresAutoCheck"),
                }),
                values = new
                {
                    runAtStartup = App.Settings.RunAtStartup,
                    useLegacyIcon = App.Settings.UseLegacyIcon,
                    showAppTooltips = App.Settings.ShowAppTooltips,
                    useScrollWheelInTray = App.Settings.UseScrollWheelInTray,
                    useGlobalMouseWheelHook = App.Settings.UseGlobalMouseWheelHook,
                    useLogarithmicVolume = App.Settings.UseLogarithmicVolume,
                    useVolumeTickSound = App.Settings.UseVolumeTickSound,
                    notifyOnDeviceChange = App.Settings.NotifyOnDefaultDeviceChange,
                    showDeviceSwitchNotification = App.Settings.ShowDeviceSwitchNotification,
                    useFocusLostVolume = App.Settings.UseFocusLostVolume,
                    focusLostAttenuatePercent = App.Settings.FocusLostAttenuatePercent,
                    focusLostFadeDurationMs = App.Settings.FocusLostFadeDurationMs,
                    focusLostSelectedAppsOnly = App.Settings.FocusLostSelectedAppsOnly,
                    showQuickTrumpetConfirmation = App.Settings.ShowQuickTrumpetConfirmation,
                    quickTrumpetNotificationSeconds = App.Settings.QuickTrumpetNotificationSeconds,
                    mediaPopupEnabled = App.Settings.MediaPopupEnabled,
                    mediaPopupHoverDelay = App.Settings.MediaPopupHoverDelay,
                    showWhenPaused = !App.Settings.MediaPopupShowOnlyWhenPlaying,
                    mediaPopupRememberExpanded = App.Settings.MediaPopupRememberExpanded,
                    ecoMode = App.Settings.EcoMode,
                    autoEcoMode = App.Settings.AutoEcoMode,
                    useSmoothVolumeAnimation = App.Settings.UseSmoothVolumeAnimation,
                    volumeAnimationSpeed = Math.Max(1, Math.Min(10, (int)Math.Round((App.Settings.VolumeAnimationSpeed - 0.02) / 0.053 + 1))),
                    peakMeterFps = App.Settings.PeakMeterFps,
                    useCustomSliderColors = colors?.UseCustomSliderColors ?? App.Settings.UseCustomSliderColors,
                    peakMeterStyleIndex = colors?.PeakMeterStyleIndex ?? (int)App.Settings.PeakMeterStyle,
                    windowBackgroundOpacity = colors?.WindowBackgroundOpacity ?? App.Settings.WindowBackgroundOpacity,
                    useDynamicAlbumArtTheme = colors?.UseDynamicAlbumArtTheme ?? App.Settings.UseDynamicAlbumArtTheme,
                    sliderThumbColor = colors?.SliderThumbColorHex ?? App.Settings.SliderThumbColor.ToString(),
                    sliderTrackFillColor = colors?.SliderTrackFillColorHex ?? App.Settings.SliderTrackFillColor.ToString(),
                    sliderTrackBackgroundColor = colors?.SliderTrackBackgroundColorHex ?? App.Settings.SliderTrackBackgroundColor.ToString(),
                    peakMeterColor = colors?.PeakMeterColorHex ?? App.Settings.PeakMeterColor.ToString(),
                    windowBackgroundColor = colors?.WindowBackgroundColorHex ?? App.Settings.WindowBackgroundColor.ToString(),
                    textColor = colors?.TextColorHex ?? App.Settings.TextColor.ToString(),
                    accentGlowColor = colors?.AccentGlowColorHex ?? App.Settings.AccentGlowColor.ToString(),
                    isTelemetryEnabled = App.Settings.IsTelemetryEnabled,
                    announcementsEnabled = App.Settings.AnnouncementsEnabled,
                    autoCheckForUpdates = App.Settings.AutoCheckForUpdates,
                    updateChannelIndex = (int)App.Settings.UpdateNotifyChannel,
                    useMonkeyTickSound = App.Settings.UseMonkeyTickSound,
                },
                collections = new
                {
                    hiddenApps = legacy?.HiddenApps.Select(item => new { item.DeviceId, item.AppId, item.ExeName, item.DisplayName, item.DeviceName }).Cast<object>() ?? Enumerable.Empty<object>(),
                    hiddenDevices = App.Settings.GetHiddenDevices().Select(item => new { item.DeviceId, item.DisplayName }),
                    hotkeys = BuildHotkeys(),
                    deviceHotkeys = GetPlaybackDevices()?.Select(device => new
                    {
                        id = "device:" + device.Id,
                        label = device.DisplayName,
                        description = R("WebSettingsDeviceShortcutDesc"),
                        isDefault = string.Equals(device.Id, GetDefaultDeviceId(), StringComparison.OrdinalIgnoreCase),
                        value = App.Settings.GetDeviceHotkey(device.Id)?.ToString()
                    }).ToArray() ?? Array.Empty<object>(),
                    profiles = profiles?.Profiles.Select((profile, index) => BuildProfile(profile, index, audio)).ToArray() ?? Array.Empty<object>(),
                    audio = audio.Payload,
                    selectedProfileIndex = profiles?.SelectedProfile == null ? -1 : profiles.Profiles.IndexOf(profiles.SelectedProfile),
                    appRules = App.Settings.GetAppRules().Select(rule => new { rule.ExeName, rule.DisplayName, rule.HardMuted, focusLost = rule.FocusLostEnabled, volumeMode = (int)rule.VolumeMode, rule.VolumePercent }),
                    folderRules = App.Settings.GetFolderVolumeRules().Select(rule => new { rule.Id, rule.FolderPath, rule.VolumePercent }),
                    themes = colors?.AvailableThemes.Concat(colors.CustomThemes).GroupBy(theme => theme.Name).Select(group => group.First()).Select(theme => new
                    {
                        theme.Name,
                        theme.Category,
                        theme.IsCustom,
                        colors = new[] { ToHex(theme.ThumbColor), ToHex(theme.TrackFillColor), ToHex(theme.PeakMeterColor), ToHex(theme.WindowBackgroundColor) },
                    }).Cast<object>() ?? Enumerable.Empty<object>(),
                    activeThemeName = App.Settings.ActiveThemeName,
                },
                status = new
                {
                    version = about?.AboutText ?? $"v{App.PackageVersion}",
                    health = LocalizeHealthSummary(about?.HealthSummary),
                    updateText = updates?.UpdateStatusText ?? string.Empty,
                    updateDetail = updates?.LastCheckText ?? string.Empty,
                    updateAvailable = updates?.IsUpdateAvailable ?? false,
                    updateBusy = updates?.IsDownloading ?? false,
                    effectivePeakMeterFps = App.Settings.EffectivePeakMeterFps,
                    ecoModeActive = App.Settings.IsEffectiveEcoMode,
                    monkeyUnlocked = App.Settings.MonkeyTickSoundUnlocked,
                },
            };

            PostMessage(new { type = "state", data = payload });
        }

        private T GetPage<T>() where T : SettingsPageViewModel
        {
            return _viewModel.Categories.SelectMany(category => category.Pages).OfType<T>().FirstOrDefault();
        }

        private static string R(string key)
        {
            return EarTrumpet.Properties.Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        }

        // Many labels are shared with the classic window, where they sit in
        // front of a control ("Couleur du curseur :"). The web UI lays them out
        // as titles, so a trailing colon never belongs there.
        private static Dictionary<string, string> StripTrailingColons(Dictionary<string, string> labels)
        {
            foreach (var key in labels.Keys.ToList())
            {
                var value = labels[key] ?? string.Empty;
                labels[key] = value.TrimEnd().TrimEnd(':', '\uFF1A').TrimEnd();
            }

            return labels;
        }

        // HealthMonitor builds an English string for diagnostics exports; only
        // the copy shown in the web About page is localized.
        private static string LocalizeHealthSummary(string summary)
        {
            if (string.IsNullOrEmpty(summary))
            {
                return string.Empty;
            }

            if (summary == "Health data unavailable")
            {
                return R("WebSettingsHealthUnavailable");
            }

            return summary
                .Replace("Memory:", R("WebSettingsHealthMemory") + ":")
                .Replace("(peak:", "(" + R("WebSettingsHealthPeak") + ":")
                .Replace("GDI:", R("WebSettingsHealthGdi") + ":")
                .Replace("User:", R("WebSettingsHealthUser") + ":")
                .Replace("Threads:", R("WebSettingsHealthThreads") + ":")
                .Replace("Uptime:", R("WebSettingsHealthUptime") + ":");
        }

        private static System.Collections.ObjectModel.ObservableCollection<EarTrumpet.DataModel.Audio.IAudioDevice> GetPlaybackDevices()
        {
            return (Application.Current as App)?.AudioDeviceManager?.Devices;
        }

        private static string GetDefaultDeviceId()
        {
            return (Application.Current as App)?.AudioDeviceManager?.Default?.Id;
        }

        private static string GetString(JsonElement message, string name)
        {
            return message.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
        }

        private static int GetInt32(JsonElement message, string name)
        {
            return message.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;
        }

        private static bool GetBoolean(JsonElement message, string name)
        {
            return message.TryGetProperty(name, out var value) &&
                (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False) &&
                value.GetBoolean();
        }

        private static int? GetOptionalInt32(JsonElement message, string name)
        {
            return message.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : (int?)null;
        }

        private static bool? GetOptionalBoolean(JsonElement message, string name)
        {
            return message.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                ? value.GetBoolean()
                : (bool?)null;
        }

        private static List<string> GetStringArray(JsonElement message, string name)
        {
            if (!message.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) return null;
            return value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()).Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
        }

        private static VolumeProfileService.CaptureOptions ReadCaptureOptions(JsonElement message)
        {
            // Older pages only sent `allDevices`; keep honoring it.
            if (!message.TryGetProperty("includeDeviceVolumes", out _))
            {
                return new VolumeProfileService.CaptureOptions
                {
                    AllDevices = GetBoolean(message, "allDevices"),
                    RouteApps = true,
                };
            }

            var includeDevices = GetBoolean(message, "includeDeviceVolumes");
            var roles = new List<VolumeProfileService.DefaultDeviceRole>();
            if (message.TryGetProperty("defaultRoles", out var rolesElement) && rolesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in rolesElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Number && Enum.IsDefined(typeof(VolumeProfileService.DefaultDeviceRole), item.GetInt32()))
                    {
                        roles.Add((VolumeProfileService.DefaultDeviceRole)item.GetInt32());
                    }
                }
            }

            return new VolumeProfileService.CaptureOptions
            {
                IncludeDeviceVolumes = includeDevices,
                DeviceIds = includeDevices ? (GetStringArray(message, "deviceIds") ?? new List<string>()) : new List<string>(),
                IncludeAppVolumes = GetBoolean(message, "includeAppVolumes"),
                RouteApps = GetBoolean(message, "routeApps"),
                DefaultRoles = roles,
            };
        }

        private static VolumeProfileService.VolumeProfile ResolveProfile(EarTrumpetVolumeProfilesSettingsPageViewModel profiles, JsonElement message)
        {
            var id = GetString(message, "id");
            var profile = string.IsNullOrWhiteSpace(id) ? null : profiles.FindProfile(id);
            return profile ?? profiles.Profiles.ElementAtOrDefault(GetInt32(message, "index"));
        }

        private static void SelectProfile(EarTrumpetVolumeProfilesSettingsPageViewModel profiles, JsonElement message)
        {
            profiles.SelectedProfile = ResolveProfile(profiles, message);
        }

        /// <summary>Live audio state the QuickTrumpet page needs to capture and edit presets.</summary>
        private sealed class AudioState
        {
            public HashSet<string> PlaybackIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> RecordingIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public object Payload { get; set; }
        }

        private static AudioState BuildAudioState()
        {
            var state = new AudioState();
            var collection = (Application.Current as App)?.CollectionViewModel;
            var playbackEndpoints = AudioEndpointHelper.GetActiveEndpoints(capture: false);
            var recordingEndpoints = AudioEndpointHelper.GetActiveEndpoints(capture: true);
            foreach (var endpoint in playbackEndpoints) state.PlaybackIds.Add(endpoint.Id);
            foreach (var endpoint in recordingEndpoints) state.RecordingIds.Add(endpoint.Id);

            var devices = collection?.AllDevices.ToList() ?? new List<DeviceViewModel>();
            state.Payload = new
            {
                // Output devices with a volume the preset can save.
                devices = devices.Select(device => new
                {
                    id = device.Id,
                    name = device.DisplayName,
                    volume = device.Volume,
                    muted = device.IsMuted,
                    isDefault = string.Equals(device.Id, collection?.Default?.Id, StringComparison.OrdinalIgnoreCase),
                }).ToArray(),
                // Endpoints a default-device slot can point to.
                playbackEndpoints = playbackEndpoints.Select(endpoint => new { id = endpoint.Id, name = endpoint.Name }).ToArray(),
                recordingEndpoints = recordingEndpoints.Select(endpoint => new { id = endpoint.Id, name = endpoint.Name }).ToArray(),
                defaults = VolumeProfileService.AllRoles.Select(role =>
                {
                    var current = VolumeProfileService.ReadCurrentDefault(role);
                    return new { role = (int)role, deviceId = current?.DeviceId ?? string.Empty, name = current?.DisplayName ?? string.Empty };
                }).ToArray(),
                appCount = QuickTrumpetSummary.CountDistinctApps(devices.SelectMany(device => device.Apps).Select(app => new VolumeProfileService.AppVolumeEntry { ExeName = app.ExeName, AppId = app.AppId })),
            };
            return state;
        }

        private static object BuildProfile(VolumeProfileService.VolumeProfile profile, int index, AudioState audio)
        {
            return new
            {
                index,
                id = profile.Id,
                name = profile.Name,
                slug = VolumeProfileService.GetSlug(profile),
                details = QuickTrumpetSummary.DescribeProfile(profile),
                applyAppsOnly = profile.ApplyAppsOnly,
                includeDeviceVolumes = profile.IncludeDeviceVolumes,
                includeAppVolumes = profile.IncludeAppVolumes,
                routeApps = profile.RouteApps,
                isLastApplied = !string.IsNullOrEmpty(profile.Id) && string.Equals(profile.Id, App.Settings.QuickTrumpetLastAppliedId, StringComparison.OrdinalIgnoreCase),
                hotkey = profile.Hotkey?.ToString() ?? string.Empty,
                defaults = VolumeProfileService.AllRoles.Select(role =>
                {
                    var entry = profile.DefaultDevices?.Get(role);
                    var known = VolumeProfileService.IsCaptureRole(role) ? audio.RecordingIds : audio.PlaybackIds;
                    return new
                    {
                        role = (int)role,
                        deviceId = entry?.DeviceId ?? string.Empty,
                        name = entry?.DisplayName ?? string.Empty,
                        missing = entry != null && !known.Contains(entry.DeviceId),
                    };
                }).ToArray(),
                devices = profile.VolumeDevices.Select(device => new
                {
                    key = device.DeviceId ?? string.Empty,
                    name = device.DisplayName ?? device.DeviceId ?? string.Empty,
                    volume = device.Volume,
                    muted = device.IsMuted,
                    missing = !audio.PlaybackIds.Contains(device.DeviceId ?? string.Empty),
                }).ToArray(),
                apps = profile.AllApps
                    .GroupBy(VolumeProfileService.GetAppKey, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .Select(app => new
                    {
                        key = VolumeProfileService.GetAppKey(app),
                        name = string.IsNullOrWhiteSpace(app.DisplayName) ? app.ExeName ?? string.Empty : app.DisplayName,
                        exeName = app.ExeName ?? string.Empty,
                        deviceName = app.DeviceDisplayName ?? string.Empty,
                        volume = app.Volume,
                        muted = app.IsMuted,
                    }).ToArray(),
            };
        }

        private static string ToHex(System.Windows.Media.Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private static object[] BuildHotkeys()
        {
            // Read from AppSettings (the live source of truth). The WPF viewmodels
            // hold a snapshot taken when the settings pages were created and are
            // never updated when the web bridge writes AppSettings directly, so
            // preferring them here made recorded shortcuts never display.
            return new object[]
            {
                new { id = "flyout", label = R("SettingsOpenEarTrumpetText"), description = R("WebSettingsHotkeyFlyoutDesc"), value = App.Settings.FlyoutHotkey.ToString() },
                new { id = "mixer", label = R("SettingsOpenMixerText"), description = R("WebSettingsHotkeyMixerDesc"), value = App.Settings.MixerHotkey.ToString() },
                new { id = "settings", label = R("SettingsOpenSettingsText"), description = R("WebSettingsHotkeySettingsDesc"), value = App.Settings.SettingsHotkey.ToString() },
                new { id = "volumeUp", label = R("SettingsAbsoluteVolumeUpText"), description = R("WebSettingsHotkeyVolumeUpDesc"), value = App.Settings.AbsoluteVolumeUpHotkey.ToString() },
                new { id = "volumeDown", label = R("SettingsAbsoluteVolumeDownText"), description = R("WebSettingsHotkeyVolumeDownDesc"), value = App.Settings.AbsoluteVolumeDownHotkey.ToString() },
                new { id = "switchDevice", label = R("SettingsSwitchDevice"), description = R("WebSettingsHotkeySwitchDeviceDesc"), value = App.Settings.SwitchDeviceHotkey.ToString() },
                new { id = "presetNext", label = R("SettingsQuickTrumpetNextHotkey"), description = R("WebSettingsHotkeyNextPresetDesc"), value = App.Settings.QuickTrumpetNextHotkey.ToString() },
                new { id = "presetPrevious", label = R("SettingsQuickTrumpetPreviousHotkey"), description = R("WebSettingsHotkeyPreviousPresetDesc"), value = App.Settings.QuickTrumpetPreviousHotkey.ToString() },
            };
        }

        private static void UpdateAppRule(JsonElement message)
        {
            var exeName = GetString(message, "exeName");
            var existing = App.Settings.GetAppRules().FirstOrDefault(rule => string.Equals(rule.ExeName, exeName, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                return;
            }

            var hardMuted = message.TryGetProperty("hardMuted", out _) ? GetBoolean(message, "hardMuted") : existing.HardMuted;
            var focusLost = message.TryGetProperty("focusLost", out _) ? GetBoolean(message, "focusLost") : existing.FocusLostEnabled;
            var mode = message.TryGetProperty("volumeMode", out _) ? GetInt32(message, "volumeMode") : (int)existing.VolumeMode;
            var volume = message.TryGetProperty("volumePercent", out _) ? GetInt32(message, "volumePercent") : existing.VolumePercent;
            App.Settings.SetAppHardMuted(exeName, hardMuted, existing.DisplayName);
            App.Settings.SetAppFocusLost(exeName, focusLost, existing.DisplayName);
            App.Settings.SetAppVolumeRule(exeName, (AppSettings.VolumeRuleMode)Math.Max(0, Math.Min(2, mode)), Math.Max(0, Math.Min(100, volume)), existing.DisplayName);
        }

        private void SetHotkey(JsonElement message)
        {
            try
            {
                var keyCode = GetInt32(message, "keyCode");
                var modifiers = System.Windows.Forms.Keys.None;
                if (GetBoolean(message, "ctrlKey")) modifiers |= System.Windows.Forms.Keys.Control;
                if (GetBoolean(message, "altKey")) modifiers |= System.Windows.Forms.Keys.Alt;
                if (GetBoolean(message, "shiftKey")) modifiers |= System.Windows.Forms.Keys.Shift;
                if (GetBoolean(message, "metaKey")) modifiers |= System.Windows.Forms.Keys.LWin;

                var hotkey = new EarTrumpet.Interop.Helpers.HotkeyData
                {
                    Key = (System.Windows.Forms.Keys)keyCode,
                    Modifiers = modifiers,
                };

                switch (GetString(message, "id"))
                {
                    case "flyout": App.Settings.FlyoutHotkey = hotkey; break;
                    case "mixer": App.Settings.MixerHotkey = hotkey; break;
                    case "settings": App.Settings.SettingsHotkey = hotkey; break;
                    case "volumeUp": App.Settings.AbsoluteVolumeUpHotkey = hotkey; break;
                    case "volumeDown": App.Settings.AbsoluteVolumeDownHotkey = hotkey; break;
                    case "switchDevice": App.Settings.SwitchDeviceHotkey = hotkey; break;
                    case "presetNext": App.Settings.QuickTrumpetNextHotkey = hotkey; break;
                    case "presetPrevious": App.Settings.QuickTrumpetPreviousHotkey = hotkey; break;
                    case string deviceId when deviceId.StartsWith("device:"):
                        var id = deviceId.Substring(7);
                        App.Settings.SetDeviceHotkey(id, hotkey);
                        break;
                    case string profileId when profileId.StartsWith("profile:", StringComparison.Ordinal) &&
                        int.TryParse(profileId.Substring("profile:".Length), out var profileIndex):
                        GetPage<EarTrumpetVolumeProfilesSettingsPageViewModel>()?.SetProfileHotkey(profileIndex, hotkey);
                        break;
                }
            }
            finally
            {
                ResumeHotkeys();
                PostState();
            }
        }

        private void ResumeHotkeys()
        {
            if (_isCapturingHotkey)
            {
                HotkeyManager.Current.Resume();
                _isCapturingHotkey = false;
            }
        }

        private void PostMessage(object message)
        {
            SettingsWebView.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(message, JsonOptions));
        }

        private void OpenLegacySettings(string pageId)
        {
            Trace.WriteLine($"WebSettingsWindow opening classic settings page: {pageId ?? "home"}");

            try
            {
                if (_legacyWindow != null)
                {
                    _legacyWindow.RaiseWindow();
                    return;
                }

                if (!string.IsNullOrWhiteSpace(pageId))
                {
                    foreach (var category in _viewModel.Categories)
                    {
                        var page = category.Pages.FirstOrDefault(candidate => GetPageId(candidate) == pageId);
                        if (page != null)
                        {
                            _viewModel.InvokeSearchResult(category, page);
                            break;
                        }
                    }
                }

                _legacyWindow = new SettingsWindow { DataContext = _viewModel };
                _legacyWindow.Closed += (_, __) =>
                {
                    _legacyWindow = null;
                };

                // SettingsWindow cloaks itself in SourceInitialized. The entrance
                // animation is responsible for uncloaking it after Show().
                _legacyWindow.Show();
                WindowAnimationLibrary.BeginWindowEntranceAnimation(_legacyWindow, () => { });
                _legacyWindow.RaiseWindow();

                // This is a hand-off, not a two-window navigation stack. Closing
                // the classic window must not silently reopen the WebView settings.
                Close();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"WebSettingsWindow classic settings failed: {ex}");
                _legacyWindow = null;
                Show();
                this.RaiseWindow();
                PostMessage(new
                {
                    type = "error",
                    message = EarTrumpet.Properties.Resources.WebSettingsOpenFailed,
                });
            }
        }

        private static string GetPageId(SettingsPageViewModel page)
        {
            return page.GetType().Name switch
            {
                nameof(EarTrumpetLegacySettingsPageViewModel) => "general",
                nameof(EarTrumpetMouseSettingsPageViewModel) => "mouse",
                nameof(EarTrumpetShortcutsPageViewModel) => "shortcuts",
                nameof(EarTrumpetVolumeProfilesSettingsPageViewModel) => "profiles",
                nameof(EarTrumpetAppRulesSettingsPageViewModel) => "app-rules",
                nameof(EarTrumpetColorsSettingsPageViewModel) => "appearance",
                nameof(EarTrumpetMediaPopupSettingsPageViewModel) => "media",
                nameof(EarTrumpetAnimationSettingsPageViewModel) => "performance",
                nameof(EarTrumpetUpdatesPageViewModel) => "updates",
                nameof(EarTrumpetPrivacyPageViewModel) => "privacy",
                nameof(EarTrumpetAboutPageViewModel) => "about",
                _ => page.GetType().Name,
            };
        }

        private static bool IsMigrated(SettingsPageViewModel page)
        {
            return page is EarTrumpetLegacySettingsPageViewModel or EarTrumpetMouseSettingsPageViewModel;
        }

        private void ShowLoadError()
        {
            _splashHidden = true;
            LoadingSplash.Visibility = Visibility.Collapsed;
            ErrorOverlay.Visibility = Visibility.Visible;
        }

        private async void Retry_Click(object sender, RoutedEventArgs e)
        {
            ErrorOverlay.Visibility = Visibility.Collapsed;
            _isInitialized = SettingsWebView.CoreWebView2 != null;
            if (_isInitialized)
            {
                SettingsWebView.CoreWebView2.Reload();
            }
            else
            {
                await InitializeWebViewAsync();
            }
        }

        private void OpenClassic_Click(object sender, RoutedEventArgs e)
        {
            OpenLegacySettings(null);
        }

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(this);
            if (chrome != null)
            {
                chrome.ResizeBorderThickness = WindowState == WindowState.Maximized
                    ? new Thickness(0)
                    : SystemParameters.WindowResizeBorderThickness;
            }

            if (WindowState == WindowState.Maximized)
            {
                WindowSizeHelper.RestrictMaximizedSizeToWorkArea(this);
            }
        }

        private static bool IsDebugBuild()
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }
}
