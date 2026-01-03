using EarTrumpet.DataModel;
using EarTrumpet.Interop;
using EarTrumpet.Interop.Helpers;
using EarTrumpet.UI.ViewModels;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Threading;

namespace EarTrumpet.UI.Helpers
{
    public class TaskbarIconSource : IShellNotifyIconSource
    {
        enum IconKind
        {
            EarTrumpet,
            EarTrumpet_LightTheme,
            Muted,
            SpeakerZeroBars,
            SpeakerOneBar,
            SpeakerTwoBars,
            SpeakerThreeBars,
            NoDevice,
        }

        public event Action<IShellNotifyIconSource> Changed;

        public Icon Current { get; private set; }

        private readonly DeviceCollectionViewModel _collection;
        private readonly AppSettings _settings;
        private bool _isMouseOver;
        private string _hash;
        private IconKind _kind;

        // Animation support - using dynamic vector generation
        private VolumeIconGenerator _volumeIconGenerator;
        private DispatcherTimer _animationTimer;
        private int _currentFrame;
        private bool _isAnimating;

        public TaskbarIconSource(DeviceCollectionViewModel collection, AppSettings settings)
        {
            _collection = collection;
            _settings = settings;

            _settings.UseLegacyIconChanged += (_, __) => CheckForUpdate();
            collection.TrayPropertyChanged += OnTrayPropertyChanged;

            InitializeAnimation();
            OnTrayPropertyChanged();
        }

        private void InitializeAnimation()
        {
            try
            {
                // Use standard icon size
                uint dpi = WindowsTaskbar.Dpi;
                int iconSize = User32.GetSystemMetricsForDpi(User32.SystemMetrics.SM_CXSMICON, dpi);
                Trace.WriteLine($"TaskbarIconSource: Icon size = {iconSize} for DPI {dpi}");

                // Create vector-based icon generator (16 frames for smooth animation)
                _volumeIconGenerator = new VolumeIconGenerator(iconSize, 16);
                Trace.WriteLine($"TaskbarIconSource: VolumeIconGenerator created with {_volumeIconGenerator.FrameCount} frames");

                // Use Render priority for smooth animation
                _animationTimer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(50) // ~20 fps for smooth animation
                };
                _animationTimer.Tick += OnAnimationTick;

                // Subscribe to MediaSessionService events
                MediaSessionService.Instance.MediaPlaybackChanged += OnMediaPlaybackChanged;

                // Check initial state (might already be playing)
                if (MediaSessionService.Instance.IsMediaPlaying)
                {
                    StartAnimation();
                }

                Trace.WriteLine($"TaskbarIconSource: Vector animation ready");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"TaskbarIconSource: Failed to initialize: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void OnMediaPlaybackChanged(bool isPlaying)
        {
            Trace.WriteLine($"TaskbarIconSource: Media playback changed - isPlaying={isPlaying}");

            if (isPlaying && _volumeIconGenerator != null)
            {
                StartAnimation();
            }
            else
            {
                StopAnimation();
            }
        }

        private void StartAnimation()
        {
            if (_isAnimating || _volumeIconGenerator == null) return;

            _isAnimating = true;
            _currentFrame = 0;
            _animationTimer?.Start();
            Trace.WriteLine("TaskbarIconSource: Animation started");
        }

        private void StopAnimation()
        {
            if (!_isAnimating) return;

            _isAnimating = false;
            _animationTimer?.Stop();
            _hash = null; // Force icon refresh
            CheckForUpdate(); // Restore normal icon
            Trace.WriteLine("TaskbarIconSource: Animation stopped");
        }

        private void OnAnimationTick(object sender, EventArgs e)
        {
            try
            {
                if (!_isAnimating || _volumeIconGenerator == null) return;

                _currentFrame = (_currentFrame + 1) % _volumeIconGenerator.FrameCount;

                // Get frame based on current system theme
                bool isLightTheme = SystemSettings.IsSystemLightTheme;
                var frame = _volumeIconGenerator.GetFrame(_currentFrame, isLightTheme);

                if (frame != null)
                {
                    // Dispose old icon
                    var oldIcon = Current;

                    Current = frame;

                    // Notify the shell
                    Changed?.Invoke(this);

                    // Dispose old
                    oldIcon?.Dispose();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"TaskbarIconSource OnAnimationTick error: {ex.Message}");
                _isAnimating = false;
                _animationTimer?.Stop();
            }
        }

        public void OnMouseOverChanged(bool isMouseOver)
        {
            _isMouseOver = isMouseOver;
            if (!_isAnimating)
            {
                CheckForUpdate();
            }
        }

        public void CheckForUpdate()
        {
            // Don't update if animating
            if (_isAnimating) return;

            var nextHash = GetHash();
            if (nextHash != _hash)
            {
                Trace.WriteLine($"TaskbarIconSource Changed: {nextHash}");
                _hash = nextHash;
                using (var old = Current)
                {
                    Current = SelectAndLoadIcon(_kind);
                    Changed?.Invoke(this);
                }
            }
        }

        private void OnTrayPropertyChanged()
        {
            _kind = IconKindFromDeviceCollection(_collection);
            if (!_isAnimating)
            {
                CheckForUpdate();
            }
        }

        private Icon SelectAndLoadIcon(IconKind kind)
        {
            if (_settings.UseLegacyIcon)
            {
                kind = IconKind.EarTrumpet;
            }

            // Use our custom generated icon for speaker states (with waves visible)
            if (_volumeIconGenerator != null && !_settings.UseLegacyIcon)
            {
                bool isLightTheme = SystemSettings.IsSystemLightTheme;

                switch (kind)
                {
                    case IconKind.Muted:
                        return _volumeIconGenerator.GetMutedIcon(isLightTheme);
                    case IconKind.SpeakerZeroBars:
                    case IconKind.SpeakerOneBar:
                    case IconKind.SpeakerTwoBars:
                    case IconKind.SpeakerThreeBars:
                        // Return last frame (full waves) as static icon
                        return _volumeIconGenerator.GetStaticIcon(isLightTheme, showWaves: true);
                    case IconKind.NoDevice:
                        // Fall through to default icon loading for NoDevice
                        break;
                    default:
                        break;
                }
            }

            try
            {
                if (System.Windows.SystemParameters.HighContrast)
                {
                    using (var icon = LoadIcon(kind))
                    {
                        return ColorIconForHighContrast(icon, kind, _isMouseOver);
                    }
                }
                else if (SystemSettings.IsSystemLightTheme)
                {
                    if (kind == IconKind.EarTrumpet)
                    {
                        return LoadIcon(IconKind.EarTrumpet_LightTheme);
                    }
                    else
                    {
                        using (var icon = LoadIcon(kind))
                        {
                            return ColorIconForLightTheme(icon, kind);
                        }
                    }
                }
                else
                {
                    return LoadIcon(kind);
                }
            }
            // Legacy fallback if SndVolSSD.dll icons are unavailable.
            catch (Exception ex) when (kind != IconKind.EarTrumpet)
            {
                Trace.WriteLine($"TaskbarIconSource LoadIcon: {ex}");
                return SelectAndLoadIcon(IconKind.EarTrumpet);
            }
        }

        private static Icon LoadIcon(IconKind kind)
        {
            uint dpi = WindowsTaskbar.Dpi;
            switch (kind)
            {
                case IconKind.EarTrumpet:
                    return IconHelper.LoadIconForTaskbar((string)System.Windows.Application.Current.Resources["EarTrumpetIconDark"], dpi);
                case IconKind.EarTrumpet_LightTheme:
                    return IconHelper.LoadIconForTaskbar((string)System.Windows.Application.Current.Resources["EarTrumpetIconLight"], dpi);
                case IconKind.Muted:
                    return IconHelper.LoadIconForTaskbar(SndVolSSO.GetPath(SndVolSSO.IconId.Muted), dpi);
                case IconKind.NoDevice:
                    return IconHelper.LoadIconForTaskbar(SndVolSSO.GetPath(SndVolSSO.IconId.NoDevice), dpi);
                case IconKind.SpeakerZeroBars:
                    return IconHelper.LoadIconForTaskbar(SndVolSSO.GetPath(SndVolSSO.IconId.SpeakerZeroBars), dpi);
                case IconKind.SpeakerOneBar:
                    return IconHelper.LoadIconForTaskbar(SndVolSSO.GetPath(SndVolSSO.IconId.SpeakerOneBar), dpi);
                case IconKind.SpeakerTwoBars:
                    return IconHelper.LoadIconForTaskbar(SndVolSSO.GetPath(SndVolSSO.IconId.SpeakerTwoBars), dpi);
                case IconKind.SpeakerThreeBars:
                    return IconHelper.LoadIconForTaskbar(SndVolSSO.GetPath(SndVolSSO.IconId.SpeakerThreeBars), dpi);
                default: throw new NotImplementedException();
            }
        }

        private string GetHash() =>
            $"kind={_kind} " +
            $"{(System.Windows.SystemParameters.HighContrast ? $"hc=true mouse={_isMouseOver} " : "")}" +
            $"dpi={WindowsTaskbar.Dpi} " +
            $"isSysLight={SystemSettings.IsSystemLightTheme} " +
            $"isLegacy={_settings.UseLegacyIcon}";

        // Only fill part of the icon, so we can preserve the red X.
        private static double GetIconFillPercent(IconKind kind) => kind == IconKind.NoDevice ? 0.4 : 1;

        private static Icon ColorIconForLightTheme(Icon darkIcon, IconKind kind)
        {
            return IconHelper.ColorIcon(darkIcon, GetIconFillPercent(kind), System.Windows.Media.Colors.Black);
        }

        private static Icon ColorIconForHighContrast(Icon darkIcon, IconKind kind, bool isMouseOver)
        {
            return IconHelper.ColorIcon(darkIcon, GetIconFillPercent(kind),
                isMouseOver ? System.Windows.SystemColors.HighlightTextColor : System.Windows.SystemColors.WindowTextColor);
        }

        private static IconKind IconKindFromDeviceCollection(DeviceCollectionViewModel collectionViewModel)
        {
            if (collectionViewModel.Default != null)
            {
                switch (collectionViewModel.Default.IconKind)
                {
                    case DeviceViewModel.DeviceIconKind.Mute:
                        return IconKind.Muted;
                    case DeviceViewModel.DeviceIconKind.Bar0:
                        return IconKind.SpeakerZeroBars;
                    case DeviceViewModel.DeviceIconKind.Bar1:
                        return IconKind.SpeakerOneBar;
                    case DeviceViewModel.DeviceIconKind.Bar2:
                        return IconKind.SpeakerTwoBars;
                    case DeviceViewModel.DeviceIconKind.Bar3:
                        return IconKind.SpeakerThreeBars;
                    default: throw new NotImplementedException();
                }
            }
            return IconKind.NoDevice;
        }
    }
}
