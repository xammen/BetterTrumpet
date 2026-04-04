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
        private bool _showUpdateBadge;
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

            _settings.UseLegacyIconChanged += (_, __) => OnLegacyIconChanged();
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

                // Use Background priority so peak meters and UI input aren't starved
                _animationTimer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(150) // ~7 fps — tray icons are tiny, 12fps was overkill
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

        private void OnLegacyIconChanged()
        {
            try
            {
                if (_settings.UseLegacyIcon)
                {
                    // Legacy icon doesn't animate — stop animation and force refresh
                    StopAnimation();
                }
                else if (MediaSessionService.Instance.IsMediaPlaying && _volumeIconGenerator != null)
                {
                    // Switched back to modern icon while music is playing — restart animation
                    StartAnimation();
                    return;
                }
                _hash = null; // Force refresh
                CheckForUpdate();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"TaskbarIconSource OnLegacyIconChanged error: {ex.Message}");
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
            if (_isAnimating || _volumeIconGenerator == null || _settings.UseLegacyIcon) return;

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
                    var oldIcon = Current;

                    Current = frame;

                    // Notify the shell
                    Changed?.Invoke(this);

                    // Frames are clones, safe to dispose
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

        public bool ShowUpdateBadge
        {
            get => _showUpdateBadge;
            set
            {
                if (_showUpdateBadge != value)
                {
                    _showUpdateBadge = value;
                    _hash = null; // Force icon refresh
                    if (!_isAnimating) CheckForUpdate();
                }
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
                var old = Current;
                Current = ApplyUpdateBadge(SelectAndLoadIcon(_kind));
                Changed?.Invoke(this);
                old?.Dispose();
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
            // Use our custom generated icon for speaker states (with waves visible)
            // Skip when legacy icon is enabled — fall through to system SndVolSSO icons
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
            $"isLegacy={_settings.UseLegacyIcon} " +
            $"badge={_showUpdateBadge}";

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

        private Icon ApplyUpdateBadge(Icon icon)
        {
            if (!_showUpdateBadge || icon == null) return icon;

            try
            {
                using (var bmp = icon.ToBitmap())
                {
                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        int dotSize = Math.Max(4, bmp.Width / 4);
                        int x = bmp.Width - dotSize - 1;
                        int y = 1;
                        using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 220, 50, 50)))
                        {
                            g.FillEllipse(brush, x, y, dotSize, dotSize);
                        }
                    }
                    var newIcon = System.Drawing.Icon.FromHandle(bmp.GetHicon());
                    icon.Dispose();
                    return newIcon;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"TaskbarIconSource: Badge overlay failed — {ex.Message}");
                return icon;
            }
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
