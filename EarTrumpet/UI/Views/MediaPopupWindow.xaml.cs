using EarTrumpet.DataModel;
using EarTrumpet.Extensions;
using EarTrumpet.Interop;
using EarTrumpet.Interop.Helpers;
using EarTrumpet.UI.Helpers;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EarTrumpet.UI.Views
{
    public partial class MediaPopupWindow : Window
    {
        public event EventHandler PopupHidden;

        private readonly AppSettings _settings;
        private readonly DispatcherTimer _hideTimer;
        private readonly DispatcherTimer _progressTimer;
        private readonly DispatcherTimer _mediaRefreshTimer;
        private readonly DispatcherTimer _delayedActionTimer;
        private readonly DispatcherTimer _proximityTimer;

        private bool _marqueeRunning;
        private int _marqueeGeneration;
        private double _cachedTextWidth;
        private bool _isShowing;
        private bool _isMouseOverPopup;
        private bool _coverIsStale;
        private bool _isExpanded;
        private double _collapsedTop;
        private double _workAreaTop;
        private double _geometryFromTop, _geometryToTop, _geometryFromHeight, _geometryToHeight;
        private double _geometryCurrentTop, _geometryCurrentHeight;
        private bool _geometryAnimating;
        private BitmapImage _cachedThumbnail;
        private Color _cachedDominantColor = Color.FromRgb(107, 77, 230);
        private string _cachedTitle;
        private CancellationTokenSource _trackChangeCts;
        private CancellationTokenSource _albumArtCts;
        private CancellationTokenSource _seekCts;
        private Action _pendingAction;
        private readonly SolidColorBrush _accentBrush = new SolidColorBrush(Color.FromRgb(107, 77, 230));
        private readonly SolidColorBrush _inactiveIconBrush = new SolidColorBrush(Color.FromArgb(144, 255, 255, 255));
        private readonly Storyboard _slideInStoryboard;
        private readonly Storyboard _slideOutStoryboard;
        private readonly Storyboard _trackChangeOutStoryboard;
        private readonly Storyboard _trackChangeInStoryboard;
        private bool _isDraggingProgress;
        private bool _updatingProgressSlider;
        private TimeSpan _currentDuration;
        private TimeSpan _progressAnchorPosition;
        private DateTime _progressAnchorTimestamp;
        private bool _hasProgressAnchor;
        private TimeSpan? _pendingSeekPosition;
        private DateTime _pendingSeekExpiresAt;
        private bool _trackRefreshInProgress;
        private int _mediaInfoRefreshInProgress;

        // The card fills the window: the acrylic backdrop covers the whole window rect, so any
        // transparent margin around the card showed up as a blurred frame.
        private const double CollapsedHeight = 169;
        private const double ExpandedHeight = 389;
        private const double IconGap = 13;
        private const double WorkAreaInset = 18;
        private const int HoverTolerancePx = 28;
        private const double ContainerWidth = 268;
        private const double MarqueeSpeed = 20; // DIP per second, as before
        private const int MarqueeFps = 30;
        private const byte MinimumPopupTintAlpha = 0xA8;

        public MediaPopupWindow(AppSettings settings)
        {
            _settings = settings;
            InitializeComponent();

            _slideInStoryboard = (Storyboard)FindResource("SlideIn");
            _slideOutStoryboard = (Storyboard)FindResource("SlideOut");
            _trackChangeOutStoryboard = (Storyboard)FindResource("TrackChangeOut");
            _trackChangeInStoryboard = (Storyboard)FindResource("TrackChangeIn");
            _slideInStoryboard.Completed += OnSlideInCompleted;
            _trackChangeOutStoryboard.Completed += OnTrackChangeOutCompleted;
            ProgressSlider.Foreground = _accentBrush;

            // Load expanded state from settings
            if (_settings.MediaPopupRememberExpanded)
            {
                _isExpanded = _settings.MediaPopupIsExpanded;
            }

            // Timer to hide popup after mouse leaves (increased from 300ms to 800ms for better stability)
            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            _hideTimer.Tick += HideTimer_Tick;

            // Render locally between the less frequent timeline updates supplied by SMTC.
            // 4 Hz stays under a pixel of bar travel for any real track, and each tick repaints
            // the whole layered window.
            _progressTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _progressTimer.Tick += ProgressTimer_Tick;

            // SMTC providers can occasionally miss MediaPropertiesChanged while a popup is open.
            _mediaRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _mediaRefreshTimer.Tick += MediaRefreshTimer_Tick;

            // Reusable timer for delayed actions
            _delayedActionTimer = new DispatcherTimer();
            _delayedActionTimer.Tick += DelayedActionTimer_Tick;

            // Polls while the cursor sits in the tolerance zone around the popup.
            _proximityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _proximityTimer.Tick += (s, e) =>
            {
                _proximityTimer.Stop();
                CheckMouseProximityAndStartHideTimer();
            };

            // Track mouse enter/leave on popup itself
            MouseEnter += (s, e) => { _isMouseOverPopup = true; _hideTimer.Stop(); _proximityTimer.Stop(); };
            MouseLeave += (s, e) =>
            {
                _isMouseOverPopup = false;
                // Check if mouse is still near the popup (tolerance zone)
                CheckMouseProximityAndStartHideTimer();
            };
            MouseMove += (s, e) =>
            {
                // Keep popup alive while mouse is moving over it
                if (_isMouseOverPopup && _hideTimer.IsEnabled)
                {
                    _hideTimer.Stop();
                }
            };

            // Subscribe to media changes
            MediaSessionService.Instance.MediaPlaybackChanged += OnMediaPlaybackChanged;
            MediaSessionService.Instance.MediaTrackChanged += OnMediaTrackChanged;
            MediaSessionService.Instance.TimelineChanged += OnTimelineChanged;

            SourceInitialized += OnSourceInitialized;
            Themes.Manager.Current.ThemeChanged += OnThemeChanged;

            // Cleanup on close
            Closed += OnWindowClosed;
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            // Unsubscribe all event handlers to prevent memory leaks
            MediaSessionService.Instance.MediaPlaybackChanged -= OnMediaPlaybackChanged;
            MediaSessionService.Instance.MediaTrackChanged -= OnMediaTrackChanged;
            MediaSessionService.Instance.TimelineChanged -= OnTimelineChanged;
            Themes.Manager.Current.ThemeChanged -= OnThemeChanged;

            // Stop all timers
            _hideTimer.Stop();
            StopMarquee();
            _progressTimer.Stop();
            _mediaRefreshTimer.Stop();
            _delayedActionTimer.Stop();
            _proximityTimer.Stop();

            // Cancel and dispose any pending operations
            _trackChangeCts?.Cancel();
            _trackChangeCts?.Dispose();
            _albumArtCts?.Cancel();
            _albumArtCts?.Dispose();
            _seekCts?.Cancel();
            _seekCts?.Dispose();
            _slideInStoryboard.Completed -= OnSlideInCompleted;
            _trackChangeOutStoryboard.Completed -= OnTrackChangeOutCompleted;
            AccentPolicyLibrary.DisableAcrylic(this);
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            this.EnableRoundedCornersIfApplicable();
            ApplyFlyoutAcrylic();
        }

        private void OnThemeChanged()
        {
            if (_isShowing)
            {
                Dispatcher.BeginInvoke((Action)ApplyFlyoutAcrylic);
            }
        }

        private void ApplyFlyoutAcrylic()
        {
            if (PresentationSource.FromVisual(this) == null) return;

            Color tint = Themes.Manager.Current.ResolveRef(this, "AcrylicColor_Flyout");
            if (tint.A < MinimumPopupTintAlpha)
            {
                tint = Color.FromArgb(MinimumPopupTintAlpha, tint.R, tint.G, tint.B);
            }

            AccentPolicyLibrary.EnableAcrylic(
                this,
                tint,
                User32.AccentFlags.DrawAllBorders);
        }

        private void DelayedActionTimer_Tick(object sender, EventArgs e)
        {
            _delayedActionTimer.Stop();
            _pendingAction?.Invoke();
            _pendingAction = null;
        }

        private void ExecuteAfterDelay(int delayMs, Action action)
        {
            _pendingAction = action;
            _delayedActionTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
            _delayedActionTimer.Start();
        }

        private void OnMediaTrackChanged()
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                _seekCts?.Cancel();
                _pendingSeekPosition = null;
                _hasProgressAnchor = false;

                if (_isShowing)
                {
                    PlayTrackChangeAnimation();
                }
                else
                {
                    // Fetched on the next show instead: preloading here decoded artwork for
                    // every track and threw it away, since ShowPopup refetches anyway.
                    _cachedTitle = null;
                    _coverIsStale = true;
                }
            }));
        }

        private void MediaRefreshTimer_Tick(object sender, EventArgs e)
        {
            if (!_isShowing || _trackRefreshInProgress || Interlocked.Exchange(ref _mediaInfoRefreshInProgress, 1) != 0)
            {
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    string title = MediaSessionService.Instance.GetCurrentMediaInfo();
                    if (string.IsNullOrEmpty(title)) return;

                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        if (_isShowing && !_trackRefreshInProgress &&
                            !string.Equals(_cachedTitle, title, StringComparison.Ordinal))
                        {
                            MediaSessionService.Instance.InvalidateThumbnailCache();
                            PlayTrackChangeAnimation();
                        }
                    }));
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"MediaPopupWindow: Media refresh check failed - {ex.Message}");
                }
                finally
                {
                    Interlocked.Exchange(ref _mediaInfoRefreshInProgress, 0);
                }
            });
        }

        private void UpdateAllContent()
        {
            UpdateTitle();
            UpdatePlayPauseIcon();
            UpdateAlbumArt();
            UpdateProgress();
            UpdateShuffleRepeatState();
            UpdateVolumeState();
            StartMarqueeIfNeeded();
        }

        private void PlayTrackChangeAnimation()
        {
            _trackRefreshInProgress = true;
            _trackChangeCts?.Cancel();
            _trackChangeCts?.Dispose();
            _trackChangeCts = new CancellationTokenSource();

            _trackChangeInStoryboard.Stop(this);
            _trackChangeOutStoryboard.Begin(this, true);
        }

        private void OnTrackChangeOutCompleted(object sender, EventArgs e)
        {
            CancellationToken token = _trackChangeCts?.Token ?? CancellationToken.None;
            UpdatePlayPauseIcon();
            UpdateProgress();
            UpdateShuffleRepeatState();
            UpdateVolumeState();

            Task.Run(() => RefreshTrackContent(token), token);
        }

        private void RefreshTrackContent(CancellationToken token)
        {
            try
            {
                string title = MediaSessionService.Instance.GetCurrentMediaInfo();
                if (token.IsCancellationRequested) return;

                Dispatcher.BeginInvoke((Action)(() =>
                {
                    if (token.IsCancellationRequested) return;
                    ApplyTitle(title);
                    _trackRefreshInProgress = false;
                    _trackChangeInStoryboard.Begin(this, true);
                }));

                BitmapImage thumbnail = MediaSessionService.Instance.GetCurrentThumbnail();
                if (token.IsCancellationRequested || thumbnail == null) return;

                Color color = GetDominantColor(thumbnail);
                if (token.IsCancellationRequested) return;

                Dispatcher.BeginInvoke((Action)(() =>
                {
                    if (token.IsCancellationRequested) return;
                    _cachedThumbnail = thumbnail;
                    _cachedDominantColor = color;
                    ApplyTrackThumbnail(thumbnail);
                    UpdateColors(color);
                }));
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaPopupWindow: Track content refresh failed - {ex.Message}");
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        _trackRefreshInProgress = false;
                        _trackChangeInStoryboard.Begin(this, true);
                    }
                }));
            }
        }

        private void UpdateColors(Color color)
        {
            _cachedDominantColor = color;

            var lighterColor = Color.FromArgb(255,
                (byte)Math.Min(255, color.R + 50),
                (byte)Math.Min(255, color.G + 50),
                (byte)Math.Min(255, color.B + 50));
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(320);

            AnimateBrushColor(_accentBrush, color, duration, easing);
            AnimateGradientColor(VolGradient1, color, duration, easing);
            AnimateGradientColor(VolGradient2, lighterColor, duration, easing);
        }

        private static void AnimateBrushColor(SolidColorBrush brush, Color target, TimeSpan duration, IEasingFunction easing)
        {
            var current = brush.Color;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            brush.Color = target;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(current, target, duration)
            {
                EasingFunction = easing
            });
        }

        private static void AnimateGradientColor(GradientStop stop, Color target, TimeSpan duration, IEasingFunction easing)
        {
            var current = stop.Color;
            stop.BeginAnimation(GradientStop.ColorProperty, null);
            stop.Color = target;
            stop.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(current, target, duration)
            {
                EasingFunction = easing
            });
        }

        private void UpdateAlbumArt()
        {
            if (_cachedThumbnail != null)
            {
                ApplyThumbnail(_cachedThumbnail);
                UpdateColors(_cachedDominantColor);
            }

            _albumArtCts?.Cancel();
            _albumArtCts?.Dispose();
            _albumArtCts = new CancellationTokenSource();
            var albumToken = _albumArtCts.Token;
            Task.Run(() =>
            {
                try
                {
                    var thumbnail = MediaSessionService.Instance.GetCurrentThumbnail();
                    if (albumToken.IsCancellationRequested) return;
                    var color = thumbnail != null ? GetDominantColor(thumbnail) : default;

                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        try
                        {
                            if (albumToken.IsCancellationRequested) return;
                            if (thumbnail != null)
                            {
                                _cachedThumbnail = thumbnail;
                                ApplyThumbnail(thumbnail);
                                _cachedDominantColor = color;
                                UpdateColors(color);
                            }
                            else if (_cachedThumbnail == null)
                            {
                                ExpandedCoverImage.Source = null;
                                ExpandedCoverBlur.Source = null;
                                UpdateColors(Color.FromRgb(107, 77, 230));
                            }
                        }
                        catch (Exception ex) { Trace.WriteLine($"MediaPopupWindow: UpdateAlbumArt UI failed - {ex.Message}"); }
                    }));
                }
                catch (Exception ex) { Trace.WriteLine($"MediaPopupWindow: UpdateAlbumArt load failed - {ex.Message}"); }
            }, albumToken);
        }

        private void ApplyThumbnail(BitmapImage thumbnail)
        {
            bool isLowRes = Math.Max(thumbnail.PixelWidth, thumbnail.PixelHeight) < 300;
            if (isLowRes)
            {
                // Low-res: show blurred base + hide sharp overlay
                ExpandedCoverBlur.Source = thumbnail;
                ExpandedCoverImage.Source = null;
                ExpandedCoverBlurEffect.Radius = 2;
            }
            else
            {
                // High-res: show sharp image, hide blur layer
                ExpandedCoverBlur.Source = null;
                ExpandedCoverImage.Source = thumbnail;
                ExpandedCoverBlurEffect.Radius = 0;
            }
        }



        private static Color GetDominantColor(BitmapImage bitmap)
        {
            try
            {
                // Normalize to BGRA: grey, 24-bit and indexed covers used to be read as BGRA anyway.
                var resized = new FormatConvertedBitmap(
                    new TransformedBitmap(bitmap, new ScaleTransform(32.0 / bitmap.PixelWidth, 32.0 / bitmap.PixelHeight)),
                    PixelFormats.Bgra32, null, 0);
                var pixels = new byte[resized.PixelWidth * resized.PixelHeight * 4];
                resized.CopyPixels(pixels, resized.PixelWidth * 4, 0);

                long totalR = 0, totalG = 0, totalB = 0;
                int count = 0;

                for (int i = 0; i < pixels.Length; i += 4)
                {
                    var b = pixels[i];
                    var g = pixels[i + 1];
                    var r = pixels[i + 2];

                    if ((r + g + b) > 50 && (r + g + b) < 700)
                    {
                        totalR += r;
                        totalG += g;
                        totalB += b;
                        count++;
                    }
                }

                if (count > 0)
                {
                    var avgR = (byte)(totalR / count);
                    var avgG = (byte)(totalG / count);
                    var avgB = (byte)(totalB / count);

                    var max = Math.Max(avgR, Math.Max(avgG, avgB));
                    if (max > 0)
                    {
                        var factor = 255.0 / max * 0.8;
                        avgR = (byte)Math.Min(255, avgR * factor);
                        avgG = (byte)Math.Min(255, avgG * factor);
                        avgB = (byte)Math.Min(255, avgB * factor);
                    }

                    return Color.FromRgb(avgR, avgG, avgB);
                }
            }
            catch (Exception ex) { Trace.WriteLine($"MediaPopupWindow: GetDominantColor failed - {ex.Message}"); }

            return Color.FromRgb(107, 77, 230);
        }

        private void OnMediaPlaybackChanged(bool isPlaying)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                UpdatePlayPauseIcon();
                UpdateTitle();
                UpdateProgress();
                StartMarqueeIfNeeded();

                if (isPlaying && _isShowing)
                {
                    _progressTimer.Start();
                }
                else
                {
                    _progressTimer.Stop();
                }
            }));
        }

        private void OnTimelineChanged()
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                if (_isShowing)
                {
                    UpdateProgress();
                }
            }));
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            RenderInterpolatedProgress();
        }

        private void UpdateProgress()
        {
            try
            {
                TimeSpan position, duration;
                MediaSessionService.Instance.GetTimelineInfo(out position, out duration);

                _currentDuration = duration;
                DurationText.Text = FormatTime(duration);
                _updatingProgressSlider = true;
                ProgressSlider.Maximum = Math.Max(1, duration.TotalSeconds);
                _updatingProgressSlider = false;

                if (_isDraggingProgress) return;

                var now = DateTime.UtcNow;
                if (_pendingSeekPosition.HasValue)
                {
                    TimeSpan expected = _progressAnchorPosition;
                    if (MediaSessionService.Instance.IsMediaPlaying)
                    {
                        expected += now - _progressAnchorTimestamp;
                    }

                    if (Math.Abs((position - expected).TotalSeconds) <= 2)
                    {
                        _pendingSeekPosition = null;
                    }
                    else if (now < _pendingSeekExpiresAt)
                    {
                        return;
                    }
                    else
                    {
                        _pendingSeekPosition = null;
                    }
                }

                _progressAnchorPosition = position;
                _progressAnchorTimestamp = now;
                _hasProgressAnchor = true;
                RenderProgress(position);
            }
            catch (Exception ex)
            {
                _updatingProgressSlider = false;
                Trace.WriteLine($"MediaPopupWindow: UpdateProgress failed - {ex.Message}");
            }
        }

        private string FormatTime(TimeSpan time)
        {
            if (time.TotalHours >= 1)
            {
                return $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}";
            }
            return $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
        }

        private void UpdateShuffleRepeatState()
        {
            try
            {
                bool? isShuffleEnabled, isRepeatEnabled;
                bool shuffleSupported, repeatSupported;
                MediaSessionService.Instance.GetPlaybackControls(out isShuffleEnabled, out isRepeatEnabled, out shuffleSupported, out repeatSupported);
                var repeatMode = MediaSessionService.Instance.GetRepeatMode();

                // Shuffle state
                ShuffleButton.Visibility = shuffleSupported ? Visibility.Visible : Visibility.Collapsed;
                ShuffleIcon.Fill = isShuffleEnabled == true ? _accentBrush : _inactiveIconBrush;

                // Repeat state
                RepeatButton.Visibility = repeatSupported ? Visibility.Visible : Visibility.Collapsed;
                switch (repeatMode)
                {
                    case 0: // None
                        RepeatIcon.Data = PhosphorIconData.Repeat;
                        RepeatIcon.Fill = _inactiveIconBrush;
                        break;
                    case 1: // List
                        RepeatIcon.Data = PhosphorIconData.Repeat;
                        RepeatIcon.Fill = _accentBrush;
                        break;
                    case 2: // Track
                        RepeatIcon.Data = PhosphorIconData.RepeatOnce;
                        RepeatIcon.Fill = _accentBrush;
                        break;
                }
            }
            catch (Exception ex) { Trace.WriteLine($"MediaPopupWindow: UpdateShuffleRepeatState failed - {ex.Message}"); }
        }

        public void ShowPopup(Rect iconBounds)
        {
            if (_isShowing) return;

            ContentGrid.Opacity = 1;
            MarqueeText.Opacity = 1;

            // The popup may have been hidden while the track changed. Refresh both
            // caches so a new hover cannot resurrect the previous track's content.
            _cachedTitle = null;
            _cachedThumbnail = null;
            MediaSessionService.Instance.InvalidateThumbnailCache();

            // The track changed while hidden: never flash the previous cover.
            if (_coverIsStale)
            {
                _coverIsStale = false;
                ExpandedCoverImage.Source = null;
                ExpandedCoverBlur.Source = null;
            }

            UpdateAllContent();

            if (_isExpanded)
            {
                Height = ExpandedHeight;
                ExpandedCover.Visibility = Visibility.Visible;
                ExpandedCover.Opacity = 1;
                if (_cachedThumbnail != null) ApplyThumbnail(_cachedThumbnail);
                SetExpandArrowState(true);
            }
            else
            {
                Height = CollapsedHeight;
                ExpandedCover.Visibility = Visibility.Collapsed;
                ExpandedCover.Opacity = 0;
                SetExpandArrowState(false);
            }

            // Shell_NotifyIconGetRect and Screen.WorkingArea are physical pixels,
            // while WPF Window coordinates are DIPs under PerMonitorV2.
            var iconCenter = new System.Drawing.Point(
                (int)Math.Round(iconBounds.Left + iconBounds.Width / 2),
                (int)Math.Round(iconBounds.Top + iconBounds.Height / 2));
            var screen = System.Windows.Forms.Screen.FromPoint(iconCenter);
            var dpiScale = Math.Max(1, WindowsTaskbar.Dpi / (double)96);
            var iconLeft = iconBounds.Left / dpiScale;
            var iconTop = iconBounds.Top / dpiScale;
            var iconWidth = iconBounds.Width / dpiScale;
            var workAreaLeft = screen.WorkingArea.Left / dpiScale;
            var workAreaTop = screen.WorkingArea.Top / dpiScale;
            _workAreaTop = workAreaTop;
            var workAreaRight = screen.WorkingArea.Right / dpiScale;

            Left = iconLeft + (iconWidth / 2) - (Width / 2);
            Top = iconTop - Height - IconGap;
            _collapsedTop = iconTop - CollapsedHeight - IconGap;

            if (Left < workAreaLeft) Left = workAreaLeft + WorkAreaInset;
            if (Left + Width > workAreaRight) Left = workAreaRight - Width - WorkAreaInset;
            if (Top < workAreaTop) Top = workAreaTop + WorkAreaInset;

            _isShowing = true;
            Show();
            ApplyFlyoutAcrylic();

            _slideInStoryboard.Begin(this, true);

            StartMarqueeIfNeeded();

            if (MediaSessionService.Instance.IsMediaPlaying)
            {
                _progressTimer.Start();
            }

            _mediaRefreshTimer.Start();
        }

        public void StartHideTimer()
        {
            // The tray reports every mouse-leave, also while the popup is hidden.
            if (!_isShowing) return;
            CheckMouseProximityAndStartHideTimer();
        }

        private bool IsCursorNearPopup()
        {
            var toleranceBounds = GetPopupBoundsInPhysicalPixels();
            toleranceBounds.Inflate(HoverTolerancePx, HoverTolerancePx);
            return toleranceBounds.Contains(System.Windows.Forms.Cursor.Position);
        }

        private void CheckMouseProximityAndStartHideTimer()
        {
            if (!_isShowing || _isMouseOverPopup) return;

            if (IsCursorNearPopup())
            {
                // Still near: check again soon instead of hiding.
                _proximityTimer.Start();
            }
            else
            {
                _hideTimer.Start();
            }
        }

        private void HideTimer_Tick(object sender, EventArgs e)
        {
            _hideTimer.Stop();

            if (_isMouseOverPopup) return;

            if (IsCursorNearPopup())
            {
                // Back in the tolerance zone without entering the popup: keep watching, or the
                // popup stayed open for good once the cursor left from there.
                _proximityTimer.Start();
            }
            else
            {
                HidePopup();
            }
        }

        private System.Drawing.Rectangle GetPopupBoundsInPhysicalPixels()
        {
            var dpiScale = Math.Max(1, WindowsTaskbar.Dpi / (double)96);
            return new System.Drawing.Rectangle(
                (int)Math.Round(Left * dpiScale),
                (int)Math.Round(Top * dpiScale),
                (int)Math.Round(Width * dpiScale),
                (int)Math.Round(Height * dpiScale));
        }

        public void HidePopup()
        {
            if (!_isShowing) return;

            StopMarquee();
            _progressTimer.Stop();
            _mediaRefreshTimer.Stop();
            _delayedActionTimer.Stop();
            _proximityTimer.Stop();
            _hideTimer.Stop();

            BeginAnimation(TopProperty, null);
            // Land on the target so Top/Height describe the real window again.
            if (_geometryAnimating)
            {
                SetPopupGeometry(_geometryToHeight, _geometryToTop);
            }

            _slideOutStoryboard.Completed += OnSlideOutCompleted;
            _slideOutStoryboard.Begin(this);
        }

        private void OnSlideOutCompleted(object sender, EventArgs e)
        {
            _slideOutStoryboard.Completed -= OnSlideOutCompleted;

            _isShowing = false;
            AccentPolicyLibrary.DisableAcrylic(this);
            Hide();
            PopupHidden?.Invoke(this, EventArgs.Empty);
        }

        private void OnSlideInCompleted(object sender, EventArgs e)
        {
            CompleteSlideInAnimation();
        }

        private void CompleteSlideInAnimation()
        {
            Opacity = 1;
            _slideInStoryboard.Stop(this);

            if (MainBorder.RenderTransform is TransformGroup transforms)
            {
                ((TranslateTransform)transforms.Children[0]).Y = 0;
                ((ScaleTransform)transforms.Children[1]).ScaleX = 1;
                ((ScaleTransform)transforms.Children[1]).ScaleY = 1;
            }
        }

        private void UpdateTitle()
        {
            // Show cached title immediately if available
            if (!string.IsNullOrEmpty(_cachedTitle))
            {
                MarqueeText.Text = _cachedTitle;
            }

            // Fetch fresh title on background thread (GetCurrentMediaInfo blocks up to 100ms)
            Task.Run(() =>
            {
                try
                {
                    var title = MediaSessionService.Instance.GetCurrentMediaInfo();
                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        ApplyTitle(title);
                    }));
                }
                catch { }
            });

            MarqueeHost.SetValue(System.Windows.Controls.Canvas.LeftProperty, 0.0);
            _cachedTextWidth = -1;
        }

        private void ApplyTitle(string title)
        {
            // A slow SMTC read returns null; while something plays, keep what is shown rather
            // than flashing the empty state (the refresh timer retries every second).
            if (string.IsNullOrEmpty(title) && MediaSessionService.Instance.IsMediaPlaying &&
                !string.IsNullOrEmpty(_cachedTitle))
            {
                return;
            }

            string displayTitle = string.IsNullOrEmpty(title) ? EarTrumpet.Properties.Resources.MediaPopupNoMedia : title;
            if (string.Equals(_cachedTitle, displayTitle, StringComparison.Ordinal) &&
                string.Equals(MarqueeText.Text, displayTitle, StringComparison.Ordinal))
            {
                return;
            }

            _cachedTitle = displayTitle;
            MarqueeText.Text = displayTitle;
            MarqueeHost.SetValue(System.Windows.Controls.Canvas.LeftProperty, 0.0);
            _cachedTextWidth = -1;
            StartMarqueeIfNeeded();
        }

        private void UpdatePlayPauseIcon()
        {
            // Legacy players (no SMTC) are only detected, they cannot be controlled or seeked.
            bool controllable = !MediaSessionService.Instance.IsUsingLegacyPlayer;
            PlayPauseButton.IsEnabled = controllable;
            PrevButton.IsEnabled = controllable;
            NextButton.IsEnabled = controllable;
            ProgressSlider.IsEnabled = controllable;

            bool isPlaying = MediaSessionService.Instance.IsMediaPlaying;
            PlayPauseIcon.Data = isPlaying ? PhosphorIconData.Pause : PhosphorIconData.Play;
            PlayPauseIcon.Margin = isPlaying ? new Thickness(0) : new Thickness(2, 0, 0, 0);
        }

        private void StartMarqueeIfNeeded()
        {
            bool textChanged = _cachedTextWidth < 0;
            if (textChanged)
            {
                MarqueeText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                _cachedTextWidth = MarqueeText.DesiredSize.Width;
            }

            if (_cachedTextWidth <= ContainerWidth)
            {
                StopMarquee();
                MarqueeHost.SetValue(System.Windows.Controls.Canvas.LeftProperty, (ContainerWidth - _cachedTextWidth) / 2);
                return;
            }

            MarqueeHost.SetValue(System.Windows.Controls.Canvas.LeftProperty, 0.0);

            // Playback/track events also land here while the popup is hidden; an animation on an
            // invisible window would keep WPF rendering forever (GitHub #74). ShowPopup calls this
            // again once _isShowing is set.
            if (!_isShowing)
            {
                StopMarquee();
            }
            else if (!_marqueeRunning || textChanged)
            {
                BeginMarquee();
            }
        }

        // Scroll the title out to the left, then loop it in from the right edge.
        private void BeginMarquee()
        {
            StopMarquee();
            _marqueeRunning = true;
            int generation = _marqueeGeneration;
            double width = _cachedTextWidth;

            var firstPass = new DoubleAnimation(0, -width, TimeSpan.FromSeconds(width / MarqueeSpeed));
            Timeline.SetDesiredFrameRate(firstPass, MarqueeFps);
            firstPass.Completed += (s, e) =>
            {
                if (generation != _marqueeGeneration) return;

                var loop = new DoubleAnimation(ContainerWidth, -width, TimeSpan.FromSeconds((ContainerWidth + width) / MarqueeSpeed))
                {
                    RepeatBehavior = RepeatBehavior.Forever
                };
                Timeline.SetDesiredFrameRate(loop, MarqueeFps);
                MarqueeScroll.BeginAnimation(TranslateTransform.XProperty, loop);
            };
            MarqueeScroll.BeginAnimation(TranslateTransform.XProperty, firstPass);
        }

        private void StopMarquee()
        {
            _marqueeRunning = false;
            _marqueeGeneration++;
            MarqueeScroll.BeginAnimation(TranslateTransform.XProperty, null);
            MarqueeScroll.X = 0;
        }

        private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentDuration.TotalSeconds <= 0 || ProgressSlider.ActualWidth <= 0)
            {
                e.Handled = true;
                return;
            }

            _isDraggingProgress = true;
            _progressTimer.Stop();
            ProgressSlider.CaptureMouse();
            UpdateProgressFromPointer(e);
            e.Handled = true;
        }

        private void ProgressSlider_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingProgress || e.LeftButton != MouseButtonState.Pressed) return;

            UpdateProgressFromPointer(e);
            e.Handled = true;
        }

        private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingProgress) return;

            UpdateProgressFromPointer(e);
            _isDraggingProgress = false;
            ProgressSlider.ReleaseMouseCapture();
            CommitProgressSeek();
            e.Handled = true;
        }

        private void ProgressSlider_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (!_isDraggingProgress) return;

            _isDraggingProgress = false;
            CommitProgressSeek();
        }

        private void UpdateProgressFromPointer(MouseEventArgs e)
        {
            double width = ProgressSlider.ActualWidth;
            if (width <= 0 || _currentDuration.TotalSeconds <= 0) return;

            double ratio = Math.Max(0, Math.Min(1, e.GetPosition(ProgressSlider).X / width));
            double seconds = _currentDuration.TotalSeconds * ratio;

            _updatingProgressSlider = true;
            ProgressSlider.Value = seconds;
            _updatingProgressSlider = false;
            PositionText.Text = FormatTime(TimeSpan.FromSeconds(seconds));
        }

        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updatingProgressSlider) return;

            PositionText.Text = FormatTime(TimeSpan.FromSeconds(Math.Max(0, e.NewValue)));
            if (!_isDraggingProgress && ProgressSlider.IsKeyboardFocusWithin)
            {
                CommitProgressSeek();
            }
        }

        private void RenderInterpolatedProgress()
        {
            if (_isDraggingProgress || !_hasProgressAnchor) return;

            TimeSpan position = _progressAnchorPosition;
            if (MediaSessionService.Instance.IsMediaPlaying)
            {
                position += DateTime.UtcNow - _progressAnchorTimestamp;
            }

            RenderProgress(position);
        }

        private void RenderProgress(TimeSpan position)
        {
            double maxSeconds = Math.Max(0, _currentDuration.TotalSeconds);
            double seconds = Math.Max(0, position.TotalSeconds);
            if (maxSeconds > 0)
            {
                seconds = Math.Min(maxSeconds, seconds);
            }

            PositionText.Text = FormatTime(TimeSpan.FromSeconds(seconds));
            _updatingProgressSlider = true;
            ProgressSlider.Value = Math.Min(ProgressSlider.Maximum, seconds);
            _updatingProgressSlider = false;
        }

        private void ApplyTrackThumbnail(BitmapImage thumbnail)
        {
            bool fadeLateArtwork = ExpandedCover.Visibility == Visibility.Visible && ExpandedCover.Opacity >= 0.95;
            if (fadeLateArtwork)
            {
                ExpandedCover.BeginAnimation(OpacityProperty, null);
                ExpandedCover.Opacity = 0.68;
            }

            ApplyThumbnail(thumbnail);

            if (fadeLateArtwork)
            {
                ExpandedCover.BeginAnimation(
                    OpacityProperty,
                    new DoubleAnimation(1, TimeSpan.FromMilliseconds(140))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    });
            }
        }

        private void CommitProgressSeek()
        {
            if (_currentDuration.TotalSeconds <= 0) return;

            double seconds = Math.Min(_currentDuration.TotalSeconds, Math.Max(0, ProgressSlider.Value));
            TimeSpan seekPosition = TimeSpan.FromSeconds(seconds);
            _progressAnchorPosition = seekPosition;
            _progressAnchorTimestamp = DateTime.UtcNow;
            _hasProgressAnchor = true;
            _pendingSeekPosition = seekPosition;
            _pendingSeekExpiresAt = DateTime.UtcNow.AddSeconds(5);
            RenderProgress(seekPosition);

            _seekCts?.Cancel();
            _seekCts?.Dispose();
            _seekCts = new CancellationTokenSource();
            _ = SendSeekWithRetryAsync(seekPosition, _seekCts.Token);

            if (MediaSessionService.Instance.IsMediaPlaying)
            {
                _progressTimer.Start();
            }
        }

        private async Task SendSeekWithRetryAsync(TimeSpan position, CancellationToken token)
        {
            try
            {
                bool accepted = await MediaSessionService.Instance.SeekToAsync(position);
                if (token.IsCancellationRequested) return;

                await Task.Delay(180, token);
                if (token.IsCancellationRequested) return;

                // Some SMTC providers acknowledge the first request before applying it.
                bool confirmedRetry = await MediaSessionService.Instance.SeekToAsync(position);
                if (!accepted && !confirmedRetry && !token.IsCancellationRequested)
                {
                    await Task.Delay(320, token);
                    if (!token.IsCancellationRequested)
                    {
                        await MediaSessionService.Instance.SeekToAsync(position);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MediaPopupWindow: Seek retry failed - {ex.Message}");
            }
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            MediaSessionService.Instance.Previous();
            ExecuteAfterDelay(500, () =>
            {
                UpdateTitle();
                StartMarqueeIfNeeded();
            });
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            MediaSessionService.Instance.PlayPause();
            ExecuteAfterDelay(100, UpdatePlayPauseIcon);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            MediaSessionService.Instance.Next();
            ExecuteAfterDelay(500, () =>
            {
                UpdateTitle();
                StartMarqueeIfNeeded();
            });
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            MediaSessionService.Instance.ToggleShuffle();
            ExecuteAfterDelay(200, UpdateShuffleRepeatState);
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            MediaSessionService.Instance.ToggleRepeat();
            ExecuteAfterDelay(200, UpdateShuffleRepeatState);
        }

        private void ExpandButton_Click(object sender, RoutedEventArgs e)
        {
            CompleteSlideInAnimation();

            if (_isExpanded)
            {
                CollapsePopup();
            }
            else
            {
                ExpandPopup();
            }
        }

        private void ExpandPopup()
        {
            _isExpanded = true;
            _collapsedTop = Top;

            if (_settings.MediaPopupRememberExpanded)
            {
                _settings.MediaPopupIsExpanded = true;
            }

            if (_cachedThumbnail != null) ApplyThumbnail(_cachedThumbnail);
            ExpandedCover.BeginAnimation(OpacityProperty, null);
            ExpandedCover.Opacity = 0;
            ExpandedCover.Visibility = Visibility.Visible;

            double expandedTop = _collapsedTop - (ExpandedHeight - CollapsedHeight);
            if (expandedTop < _workAreaTop + WorkAreaInset)
            {
                expandedTop = _collapsedTop;
            }
            AnimatePopupGeometry(ExpandedHeight, expandedTop, 240);

            var coverTransforms = (TransformGroup)ExpandedCover.RenderTransform;
            var coverScale = (ScaleTransform)coverTransforms.Children[0];
            var coverTranslate = (TranslateTransform)coverTransforms.Children[1];
            coverScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            coverScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            coverTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            coverScale.ScaleX = 0.985;
            coverScale.ScaleY = 0.985;
            coverTranslate.Y = 6;

            var opacityAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ExpandedCover.BeginAnimation(OpacityProperty, opacityAnim);
            coverScale.BeginAnimation(ScaleTransform.ScaleXProperty, CreateCoverAnimation(0.985, 1, 220));
            coverScale.BeginAnimation(ScaleTransform.ScaleYProperty, CreateCoverAnimation(0.985, 1, 220));
            coverTranslate.BeginAnimation(TranslateTransform.YProperty, CreateCoverAnimation(6, 0, 220));

            AnimateExpandArrow(true);
        }

        private void CollapsePopup()
        {
            _isExpanded = false;

            if (_settings.MediaPopupRememberExpanded)
            {
                _settings.MediaPopupIsExpanded = false;
            }

            ExpandedCover.BeginAnimation(OpacityProperty, null);
            var coverTransforms = (TransformGroup)ExpandedCover.RenderTransform;
            var coverScale = (ScaleTransform)coverTransforms.Children[0];
            var coverTranslate = (TranslateTransform)coverTransforms.Children[1];
            var coverFadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            coverFadeOut.Completed += (s, e) =>
            {
                if (!_isExpanded)
                {
                    ExpandedCover.BeginAnimation(OpacityProperty, null);
                    ExpandedCover.Opacity = 0;
                    ExpandedCover.Visibility = Visibility.Collapsed;
                }
            };
            ExpandedCover.BeginAnimation(OpacityProperty, coverFadeOut);
            coverScale.BeginAnimation(ScaleTransform.ScaleXProperty, CreateCoverAnimation(1, 0.99, 140, EasingMode.EaseIn));
            coverScale.BeginAnimation(ScaleTransform.ScaleYProperty, CreateCoverAnimation(1, 0.99, 140, EasingMode.EaseIn));
            coverTranslate.BeginAnimation(TranslateTransform.YProperty, CreateCoverAnimation(0, -4, 140, EasingMode.EaseIn));

            AnimateExpandArrow(false);
            AnimatePopupGeometry(CollapsedHeight, _collapsedTop, 200);
        }

        private static DoubleAnimation CreateCoverAnimation(double from, double to, int durationMs, EasingMode easingMode = EasingMode.EaseOut)
        {
            return new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMs))
            {
                EasingFunction = new CubicEase { EasingMode = easingMode }
            };
        }

        // Top and Height animated on separate clocks meant two SetWindowPos calls per frame, with
        // the window visibly jittering between them. One progress value drives both instead.
        private static readonly DependencyProperty GeometryProgressProperty = DependencyProperty.Register(
            "GeometryProgress", typeof(double), typeof(MediaPopupWindow),
            new PropertyMetadata(1.0, (d, e) => ((MediaPopupWindow)d).OnGeometryProgressChanged((double)e.NewValue)));

        private void SetPopupGeometry(double height, double top)
        {
            _geometryAnimating = false;
            BeginAnimation(GeometryProgressProperty, null);
            BeginAnimation(HeightProperty, null);
            BeginAnimation(TopProperty, null);
            Height = height;
            Top = top;
        }

        private void AnimatePopupGeometry(double targetHeight, double targetTop, int durationMs)
        {
            // Mid-animation the window is where the last frame put it, not at Top/Height.
            _geometryFromHeight = _geometryAnimating ? _geometryCurrentHeight : (ActualHeight > 0 ? ActualHeight : Height);
            _geometryFromTop = _geometryAnimating ? _geometryCurrentTop : Top;
            _geometryCurrentHeight = _geometryFromHeight;
            _geometryCurrentTop = _geometryFromTop;
            _geometryAnimating = true;
            _geometryToHeight = targetHeight;
            _geometryToTop = targetTop;

            var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            animation.Completed += (s, e) =>
            {
                // Stale if another expand/collapse started meanwhile.
                if (_geometryToHeight == targetHeight && _geometryToTop == targetTop)
                {
                    SetPopupGeometry(targetHeight, targetTop);
                }
            };
            BeginAnimation(GeometryProgressProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private void OnGeometryProgressChanged(double t)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero || !_isShowing) return;

            if (!_geometryAnimating) return;

            double top = _geometryFromTop + (_geometryToTop - _geometryFromTop) * t;
            double height = _geometryFromHeight + (_geometryToHeight - _geometryFromHeight) * t;
            _geometryCurrentTop = top;
            _geometryCurrentHeight = height;
            var dpiScale = Math.Max(1, WindowsTaskbar.Dpi / (double)96);

            User32.SetWindowPos(hwnd, IntPtr.Zero,
                (int)Math.Round(Left * dpiScale),
                (int)Math.Round(top * dpiScale),
                (int)Math.Round(Width * dpiScale),
                (int)Math.Round(height * dpiScale),
                User32.WindowPosFlags.SWP_NOZORDER | User32.WindowPosFlags.SWP_NOACTIVATE | User32.WindowPosFlags.SWP_NOOWNERZORDER);
        }

        private void SetExpandArrowState(bool expanded)
        {
            ExpandArrowDown.BeginAnimation(OpacityProperty, null);
            ExpandArrowUp.BeginAnimation(OpacityProperty, null);
            ExpandArrowDown.Opacity = expanded ? 1 : 0;
            ExpandArrowUp.Opacity = expanded ? 0 : 1;

            var downTranslate = GetArrowTranslate(ExpandArrowDown);
            var upTranslate = GetArrowTranslate(ExpandArrowUp);
            downTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            upTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            downTranslate.Y = 0;
            upTranslate.Y = 0;
        }

        private void AnimateExpandArrow(bool expanded)
        {
            var incoming = expanded ? ExpandArrowDown : ExpandArrowUp;
            var outgoing = expanded ? ExpandArrowUp : ExpandArrowDown;
            var direction = expanded ? -1d : 1d;
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };

            outgoing.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(80),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            });
            GetArrowTranslate(outgoing).BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
            {
                To = direction * 2,
                Duration = TimeSpan.FromMilliseconds(80),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            });

            incoming.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = easeOut
            });
            GetArrowTranslate(incoming).BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
            {
                From = -direction * 2,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = easeOut
            });
        }

        private static TranslateTransform GetArrowTranslate(System.Windows.Shapes.Path arrow)
        {
            if (arrow.RenderTransform is TransformGroup group)
            {
                return (TranslateTransform)group.Children[1];
            }

            return (TranslateTransform)arrow.RenderTransform;
        }

        // ═══════════════════════════════════
        // Volume Control
        // ═══════════════════════════════════

        private int _currentVolume = 100;
        private bool _currentVolumeMuted;
        private bool _isDraggingVolume;
        private ViewModels.IAppItemViewModel _volumeDragTarget;

        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var app = FindCurrentMediaApp();
                if (app != null)
                {
                    app.IsMuted = !app.IsMuted;
                    UpdateVolumeVisual(app.Volume, app.IsMuted);
                }
            }
            catch (Exception ex) { Trace.WriteLine($"MediaPopupWindow: VolumeButton_Click failed - {ex.Message}"); }
        }

        private void VolumeTrack_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _volumeDragTarget = FindCurrentMediaApp();
            if (_volumeDragTarget == null)
            {
                e.Handled = true;
                return;
            }

            _isDraggingVolume = true;
            ((System.Windows.IInputElement)sender).CaptureMouse();
            ApplyVolumeFromMouse(e);
            e.Handled = true;
        }

        private void VolumeTrack_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingVolume && e.LeftButton == MouseButtonState.Pressed)
            {
                ApplyVolumeFromMouse(e);
            }
        }

        private void VolumeTrack_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingVolume = false;
            _volumeDragTarget = null;
            ((System.Windows.IInputElement)sender).ReleaseMouseCapture();
            e.Handled = true;
        }

        private void ApplyVolumeFromMouse(MouseEventArgs e)
        {
            try
            {
                var pos = e.GetPosition(VolumeTrackContainer);
                var ratio = Math.Max(0, Math.Min(1, pos.X / VolumeTrackContainer.ActualWidth));
                var volume = (int)(ratio * 100);

                _currentVolume = volume;
                UpdateVolumeVisual(volume, false);

                var app = _volumeDragTarget;
                if (app != null)
                {
                    app.Volume = volume;
                    if (app.IsMuted && volume > 0) app.IsMuted = false;
                }
            }
            catch (Exception ex) { Trace.WriteLine($"MediaPopupWindow: ApplyVolumeFromMouse failed - {ex.Message}"); }
        }

        private void UpdateVolumeState()
        {
            try
            {
                var app = FindCurrentMediaApp();
                if (app != null)
                {
                    SetVolumeControlEnabled(true);
                    _currentVolume = app.Volume;
                    UpdateVolumeVisual(app.Volume, app.IsMuted);
                }
                else
                {
                    SetVolumeControlEnabled(false);
                }
            }
            catch (Exception ex) { Trace.WriteLine($"MediaPopupWindow: UpdateVolumeState failed - {ex.Message}"); }
        }

        private void SetVolumeControlEnabled(bool enabled)
        {
            VolumeControl.IsEnabled = enabled;
            VolumeControl.Opacity = enabled ? 1 : 0.45;
            VolumeTrackContainer.Cursor = enabled ? Cursors.Hand : Cursors.Arrow;
        }

        private void VolumeTrackContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > 0)
            {
                UpdateVolumeVisual(_currentVolume, _currentVolumeMuted);
            }
        }

        private void UpdateVolumeVisual(int volume, bool isMuted)
        {
            _currentVolume = volume;
            _currentVolumeMuted = isMuted;

            // Update fill width + thumb position
            var containerWidth = VolumeTrackContainer.ActualWidth;
            if (containerWidth > 0)
            {
                var ratio = volume / 100.0;
                VolumeTrackFill.Width = containerWidth * ratio;
                System.Windows.Controls.Canvas.SetLeft(VolumeThumb, containerWidth * ratio - 5);
            }

            // Update text
            VolumeText.Text = volume.ToString();

            // Update icon
            if (isMuted || volume == 0)
                VolumeIcon.Data = PhosphorIconData.SpeakerSlash;
            else if (volume < 33)
                VolumeIcon.Data = PhosphorIconData.SpeakerNone;
            else if (volume < 66)
                VolumeIcon.Data = PhosphorIconData.SpeakerLow;
            else
                VolumeIcon.Data = PhosphorIconData.SpeakerHigh;

        }

        /// <summary>
        /// Find the audio session for the currently playing media app.
        /// Matches by SMTC source app ID or legacy player exe name.
        /// </summary>
        private ViewModels.IAppItemViewModel FindCurrentMediaApp()
        {
            try
            {
                var collection = ((App)Application.Current).CollectionViewModel;
                if (collection == null) return null;

                string sourceApp = MediaSessionService.Instance.IsUsingLegacyPlayer
                    ? MediaSessionService.Instance.LegacyPlayerName
                    : MediaSessionService.Instance.CurrentSourceAppId;

                if (string.IsNullOrWhiteSpace(sourceApp)) return null;

                // Exact matches first: the loose pass ("chrome" inside another app's id) used to
                // win whenever its app happened to come first in device order.
                foreach (bool strict in new[] { true, false })
                {
                    foreach (var device in collection.AllDevices)
                    {
                        foreach (var app in device.Apps)
                        {
                            if (DoesMediaSourceMatch(app, sourceApp, strict))
                            {
                                return app;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Trace.WriteLine($"MediaPopupWindow: FindCurrentMediaApp failed - {ex.Message}"); }

            return null;
        }

        private static bool DoesMediaSourceMatch(ViewModels.IAppItemViewModel app, string sourceApp, bool strict)
        {
            string exeName = System.IO.Path.GetFileNameWithoutExtension(app.ExeName ?? string.Empty);
            string sourceName = System.IO.Path.GetFileNameWithoutExtension(sourceApp);

            if (strict)
            {
                return string.Equals(app.AppId, sourceApp, StringComparison.OrdinalIgnoreCase) ||
                       (exeName.Length > 0 && string.Equals(exeName, sourceName, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(app.AppId) &&
                (sourceApp.IndexOf(app.AppId, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 app.AppId.IndexOf(sourceApp, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            return exeName.Length >= 3 && sourceApp.IndexOf(exeName, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public bool IsShowing => _isShowing;
        public bool IsExpanded => _isExpanded;
    }
}
