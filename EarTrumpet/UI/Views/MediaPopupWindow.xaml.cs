using EarTrumpet.DataModel;
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
        private readonly DispatcherTimer _marqueeTimer;
        private readonly DispatcherTimer _progressTimer;
        private readonly DispatcherTimer _delayedActionTimer;

        private double _marqueePosition;
        private double _cachedTextWidth;
        private bool _isShowing;
        private bool _isMouseOverPopup;
        private bool _isExpanded;
        private double _collapsedTop;
        private BitmapImage _cachedThumbnail;
        private Color _cachedDominantColor = Color.FromRgb(107, 77, 230);
        private CancellationTokenSource _thumbnailCts;
        private CancellationTokenSource _trackChangeCts;
        private CancellationTokenSource _albumArtCts;
        private Action _pendingAction;

        private const double CollapsedHeight = 185;
        private const double ExpandedHeight = 405;
        private const double ContainerWidth = 268;

        public MediaPopupWindow(AppSettings settings)
        {
            _settings = settings;
            InitializeComponent();

            // Load expanded state from settings
            if (_settings.MediaPopupRememberExpanded)
            {
                _isExpanded = _settings.MediaPopupIsExpanded;
            }

            // Timer to hide popup after mouse leaves
            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _hideTimer.Tick += HideTimer_Tick;

            // Timer for marquee animation
            _marqueeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _marqueeTimer.Tick += MarqueeTimer_Tick;

            // Timer for progress bar updates (1 second)
            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _progressTimer.Tick += ProgressTimer_Tick;

            // Reusable timer for delayed actions
            _delayedActionTimer = new DispatcherTimer();
            _delayedActionTimer.Tick += DelayedActionTimer_Tick;

            // Track mouse enter/leave on popup itself
            MouseEnter += (s, e) => { _isMouseOverPopup = true; _hideTimer.Stop(); };
            MouseLeave += (s, e) => { _isMouseOverPopup = false; StartHideTimer(); };

            // Subscribe to media changes
            MediaSessionService.Instance.MediaPlaybackChanged += OnMediaPlaybackChanged;
            MediaSessionService.Instance.MediaTrackChanged += OnMediaTrackChanged;
            MediaSessionService.Instance.TimelineChanged += OnTimelineChanged;

            // Subscribe to settings changes
            _settings.MediaPopupSettingsChanged += OnSettingsChanged;

            // Cleanup on close
            Closed += OnWindowClosed;

            // Pre-load thumbnail in background
            PreloadThumbnail();
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            // Unsubscribe all event handlers to prevent memory leaks
            MediaSessionService.Instance.MediaPlaybackChanged -= OnMediaPlaybackChanged;
            MediaSessionService.Instance.MediaTrackChanged -= OnMediaTrackChanged;
            MediaSessionService.Instance.TimelineChanged -= OnTimelineChanged;
            _settings.MediaPopupSettingsChanged -= OnSettingsChanged;

            // Stop all timers
            _hideTimer.Stop();
            _marqueeTimer.Stop();
            _progressTimer.Stop();
            _delayedActionTimer.Stop();

            // Cancel and dispose any pending operations
            _thumbnailCts?.Cancel();
            _thumbnailCts?.Dispose();
            _trackChangeCts?.Cancel();
            _trackChangeCts?.Dispose();
            _albumArtCts?.Cancel();
            _albumArtCts?.Dispose();
        }

        private void OnSettingsChanged()
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                AlbumArtBlur.Radius = _settings.MediaPopupBlurRadius;
            }));
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

        private void PreloadThumbnail()
        {
            _thumbnailCts?.Cancel();
            _thumbnailCts = new CancellationTokenSource();
            var token = _thumbnailCts.Token;

            Task.Run(() =>
            {
                try
                {
                    if (token.IsCancellationRequested) return;

                    var thumbnail = MediaSessionService.Instance.GetCurrentThumbnail();
                    if (thumbnail != null && !token.IsCancellationRequested)
                    {
                        var color = GetDominantColor(thumbnail);
                        if (!token.IsCancellationRequested)
                        {
                            _cachedThumbnail = thumbnail;
                            _cachedDominantColor = color;
                        }
                    }
                }
                catch (Exception ex) { Trace.WriteLine($"MediaPopupWindow: PreloadThumbnail failed - {ex.Message}"); }
            }, token);
        }

        private void OnMediaTrackChanged()
        {
            PreloadThumbnail();

            Dispatcher.BeginInvoke((Action)(() =>
            {
                if (_isShowing)
                {
                    PlayTrackChangeAnimation();
                }
                else
                {
                    UpdateAllContent();
                }
            }));
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
            var outStoryboard = (Storyboard)FindResource("TrackChangeOut");
            outStoryboard.Completed += OnTrackChangeOutCompleted;
            outStoryboard.Begin(this, true);
        }

        private void OnTrackChangeOutCompleted(object sender, EventArgs e)
        {
            try
            {
                var outStoryboard = (Storyboard)FindResource("TrackChangeOut");
                outStoryboard.Completed -= OnTrackChangeOutCompleted;

                UpdateTitle();
                UpdatePlayPauseIcon();
                UpdateProgress();
                UpdateShuffleRepeatState();
                StartMarqueeIfNeeded();

                // Update album art on background thread
                _trackChangeCts?.Cancel();
                _trackChangeCts?.Dispose();
                _trackChangeCts = new CancellationTokenSource();
                var token = _trackChangeCts.Token;
                Task.Run(() =>
                {
                    try
                    {
                        var thumbnail = MediaSessionService.Instance.GetCurrentThumbnail();
                        if (token.IsCancellationRequested) return;

                        Dispatcher.BeginInvoke((Action)(() =>
                        {
                            try
                            {
                                if (thumbnail != null)
                                {
                                    _cachedThumbnail = thumbnail;
                                    ApplyThumbnail(thumbnail);

                                    var color = GetDominantColor(thumbnail);
                                    _cachedDominantColor = color;
                                    UpdateColors(color);
                                }

                                var inStoryboard = (Storyboard)FindResource("TrackChangeIn");
                                inStoryboard.Begin(this, true);
                            }
                            catch (Exception ex) { Trace.WriteLine($"MediaPopupWindow: TrackChange UI update failed - {ex.Message}"); }
                        }));
                    }
                    catch (Exception ex) { Trace.WriteLine($"MediaPopupWindow: TrackChange thumbnail load failed - {ex.Message}"); }
                }, token);
            }
            catch (Exception ex) { Trace.WriteLine($"MediaPopupWindow: OnTrackChangeOutCompleted failed - {ex.Message}"); }
        }

        private void UpdateColors(Color color)
        {
            GlowColor.Color = color;
            FlashColor.Color = Color.FromArgb(200, color.R, color.G, color.B);
            ProgressGradient1.Color = color;
            ProgressGradient2.Color = Color.FromArgb(255,
                (byte)Math.Min(255, color.R + 50),
                (byte)Math.Min(255, color.G + 50),
                (byte)Math.Min(255, color.B + 50));
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

                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        try
                        {
                            if (thumbnail != null)
                            {
                                _cachedThumbnail = thumbnail;
                                ApplyThumbnail(thumbnail);

                                var color = GetDominantColor(thumbnail);
                                _cachedDominantColor = color;
                                UpdateColors(color);
                            }
                            else if (_cachedThumbnail == null)
                            {
                                AlbumArtBackground.Source = null;
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
            AlbumArtBackground.Source = thumbnail;

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



        private Color GetDominantColor(BitmapImage bitmap)
        {
            try
            {
                var resized = new TransformedBitmap(bitmap, new ScaleTransform(32.0 / bitmap.PixelWidth, 32.0 / bitmap.PixelHeight));
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
            UpdateProgress();
        }

        private void UpdateProgress()
        {
            try
            {
                TimeSpan position, duration;
                MediaSessionService.Instance.GetTimelineInfo(out position, out duration);

                PositionText.Text = FormatTime(position);
                DurationText.Text = FormatTime(duration);

                if (duration.TotalSeconds > 0)
                {
                    var progress = position.TotalSeconds / duration.TotalSeconds;
                    var containerWidth = ProgressBarContainer.ActualWidth;
                    if (containerWidth > 0)
                    {
                        ProgressBarFill.Width = containerWidth * Math.Min(1, Math.Max(0, progress));
                    }
                }
                else
                {
                    ProgressBarFill.Width = 0;
                }
            }
            catch (Exception ex) { Trace.WriteLine($"MediaPopupWindow: UpdateProgress failed - {ex.Message}"); }
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
                ShuffleIcon.Foreground = new SolidColorBrush(isShuffleEnabled == true ? _cachedDominantColor : Color.FromArgb(128, 255, 255, 255));

                // Repeat state
                RepeatButton.Visibility = repeatSupported ? Visibility.Visible : Visibility.Collapsed;
                switch (repeatMode)
                {
                    case 0: // None
                        RepeatIcon.Text = "\uE8EE";
                        RepeatIcon.Foreground = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255));
                        break;
                    case 1: // List
                        RepeatIcon.Text = "\uE8EE";
                        RepeatIcon.Foreground = new SolidColorBrush(_cachedDominantColor);
                        break;
                    case 2: // Track
                        RepeatIcon.Text = "\uE8ED";
                        RepeatIcon.Foreground = new SolidColorBrush(_cachedDominantColor);
                        break;
                }
            }
            catch (Exception ex) { Trace.WriteLine($"MediaPopupWindow: UpdateShuffleRepeatState failed - {ex.Message}"); }
        }

        public void ShowPopup(Rect iconBounds)
        {
            if (_isShowing) return;

            AlbumArtBlur.Radius = _settings.MediaPopupBlurRadius;

            ContentGrid.Opacity = 1;
            ContentGrid.RenderTransform = new ScaleTransform(1, 1);
            TransitionFlash.Opacity = 0;

            UpdateAllContent();

            if (_isExpanded)
            {
                Height = ExpandedHeight;
                ExpandedCover.Visibility = Visibility.Visible;
                ExpandedCover.Opacity = 1;
                if (_cachedThumbnail != null) ApplyThumbnail(_cachedThumbnail);
                ExpandArrow.RenderTransform = new RotateTransform(180);
            }
            else
            {
                Height = CollapsedHeight;
                ExpandedCover.Visibility = Visibility.Collapsed;
                ExpandedCover.Opacity = 0;
                ExpandArrow.RenderTransform = new RotateTransform(0);
            }

            Left = iconBounds.Left + (iconBounds.Width / 2) - (Width / 2);
            Top = iconBounds.Top - Height - 5;
            _collapsedTop = iconBounds.Top - CollapsedHeight - 5;

            var screen = SystemParameters.WorkArea;
            if (Left < screen.Left) Left = screen.Left + 10;
            if (Left + Width > screen.Right) Left = screen.Right - Width - 10;
            if (Top < screen.Top) Top = screen.Top + 10;

            _isShowing = true;
            Show();

            var storyboard = (Storyboard)FindResource("SlideIn");
            storyboard.Begin(this);

            var pulseStoryboard = (Storyboard)FindResource("PulseAnimation");
            pulseStoryboard.Begin(this, true);

            StartMarqueeIfNeeded();

            if (MediaSessionService.Instance.IsMediaPlaying)
            {
                _progressTimer.Start();
            }
        }

        public void StartHideTimer()
        {
            if (!_isMouseOverPopup)
            {
                _hideTimer.Start();
            }
        }

        private void HideTimer_Tick(object sender, EventArgs e)
        {
            _hideTimer.Stop();
            if (!_isMouseOverPopup)
            {
                HidePopup();
            }
        }

        public void HidePopup()
        {
            if (!_isShowing) return;

            _marqueeTimer.Stop();
            _progressTimer.Stop();
            _delayedActionTimer.Stop();

            BeginAnimation(TopProperty, null);

            var pulseStoryboard = (Storyboard)FindResource("PulseAnimation");
            pulseStoryboard.Stop(this);

            var storyboard = (Storyboard)FindResource("SlideOut");
            storyboard.Completed += OnSlideOutCompleted;
            storyboard.Begin(this);
        }

        private void OnSlideOutCompleted(object sender, EventArgs e)
        {
            var storyboard = (Storyboard)FindResource("SlideOut");
            storyboard.Completed -= OnSlideOutCompleted;

            _isShowing = false;
            Hide();
            PopupHidden?.Invoke(this, EventArgs.Empty);
        }

        public void CancelHide()
        {
            _hideTimer.Stop();
        }

        private void UpdateTitle()
        {
            var title = MediaSessionService.Instance.GetCurrentMediaInfo();
            MarqueeText.Text = string.IsNullOrEmpty(title) ? "No media playing" : title;
            _marqueePosition = 0;
            MarqueeText.SetValue(System.Windows.Controls.Canvas.LeftProperty, 0.0);
            _cachedTextWidth = -1;
        }

        private void UpdatePlayPauseIcon()
        {
            PlayPauseIcon.Text = MediaSessionService.Instance.IsMediaPlaying ? "\uE103" : "\uE102";
        }

        private void StartMarqueeIfNeeded()
        {
            if (_cachedTextWidth < 0)
            {
                MarqueeText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                _cachedTextWidth = MarqueeText.DesiredSize.Width;
            }

            if (_cachedTextWidth > ContainerWidth)
            {
                _marqueePosition = 0;
                _marqueeTimer.Start();
            }
            else
            {
                _marqueeTimer.Stop();
                MarqueeText.SetValue(System.Windows.Controls.Canvas.LeftProperty, (ContainerWidth - _cachedTextWidth) / 2);
            }
        }

        private void MarqueeTimer_Tick(object sender, EventArgs e)
        {
            _marqueePosition -= 1;

            if (_marqueePosition < -_cachedTextWidth)
            {
                _marqueePosition = ContainerWidth;
            }

            MarqueeText.SetValue(System.Windows.Controls.Canvas.LeftProperty, _marqueePosition);
        }

        private void ProgressBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var clickPos = e.GetPosition(ProgressBarContainer);
                var progress = clickPos.X / ProgressBarContainer.ActualWidth;

                TimeSpan position, duration;
                MediaSessionService.Instance.GetTimelineInfo(out position, out duration);
                if (duration.TotalSeconds > 0)
                {
                    var seekPosition = TimeSpan.FromSeconds(duration.TotalSeconds * Math.Min(1, Math.Max(0, progress)));
                    MediaSessionService.Instance.SeekTo(seekPosition);
                    UpdateProgress();
                }
            }
            catch (Exception ex) { Trace.WriteLine($"MediaPopupWindow: ProgressBar seek failed - {ex.Message}"); }
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
            ExpandedCover.Opacity = 0;
            ExpandedCover.Visibility = Visibility.Visible;

            var heightAnim = new DoubleAnimation
            {
                To = ExpandedHeight,
                Duration = TimeSpan.FromMilliseconds(350),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(HeightProperty, heightAnim);

            var opacityAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(100),
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ExpandedCover.BeginAnimation(OpacityProperty, opacityAnim);

            var rotateAnim = new DoubleAnimation
            {
                To = 180,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ((RotateTransform)ExpandArrow.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, rotateAnim);

            var posAnim = new DoubleAnimation
            {
                To = _collapsedTop - (ExpandedHeight - CollapsedHeight),
                Duration = TimeSpan.FromMilliseconds(350),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(TopProperty, posAnim);
        }

        private void CollapsePopup()
        {
            _isExpanded = false;

            if (_settings.MediaPopupRememberExpanded)
            {
                _settings.MediaPopupIsExpanded = false;
            }

            // Fade out cover first
            var coverFadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            coverFadeOut.Completed += (s, e) =>
            {
                ExpandedCover.Visibility = Visibility.Collapsed;
            };
            ExpandedCover.BeginAnimation(OpacityProperty, coverFadeOut);

            // Rotate arrow back
            var rotateAnim = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            ((RotateTransform)ExpandArrow.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, rotateAnim);

            // Animate height down
            var heightAnim = new DoubleAnimation
            {
                To = CollapsedHeight,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            BeginAnimation(HeightProperty, heightAnim);

            // Animate position back up
            var posAnim = new DoubleAnimation
            {
                To = _collapsedTop,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            BeginAnimation(TopProperty, posAnim);
        }

        // ═══════════════════════════════════
        // Volume Control
        // ═══════════════════════════════════

        private int _currentVolume = 100;
        private bool _isDraggingVolume;

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
                else
                {
                    var collection = ((App)Application.Current).CollectionViewModel;
                    if (collection?.Default != null)
                    {
                        collection.Default.IsMuted = !collection.Default.IsMuted;
                        UpdateVolumeVisual(collection.Default.Volume, collection.Default.IsMuted);
                    }
                }
            }
            catch (Exception ex) { Trace.WriteLine($"MediaPopupWindow: VolumeButton_Click failed - {ex.Message}"); }
        }

        private void VolumeTrack_MouseDown(object sender, MouseButtonEventArgs e)
        {
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

                var app = FindCurrentMediaApp();
                if (app != null)
                {
                    app.Volume = volume;
                    if (app.IsMuted && volume > 0) app.IsMuted = false;
                }
                else
                {
                    var collection = ((App)Application.Current).CollectionViewModel;
                    if (collection?.Default != null)
                    {
                        collection.Default.Volume = volume;
                        if (collection.Default.IsMuted && volume > 0) collection.Default.IsMuted = false;
                    }
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
                    _currentVolume = app.Volume;
                    UpdateVolumeVisual(app.Volume, app.IsMuted);
                }
                else
                {
                    var collection = ((App)Application.Current).CollectionViewModel;
                    if (collection?.Default != null)
                    {
                        _currentVolume = collection.Default.Volume;
                        UpdateVolumeVisual(collection.Default.Volume, collection.Default.IsMuted);
                    }
                }
            }
            catch (Exception ex) { Trace.WriteLine($"MediaPopupWindow: UpdateVolumeState failed - {ex.Message}"); }
        }

        private void UpdateVolumeVisual(int volume, bool isMuted)
        {
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
                VolumeIcon.Text = "\uE74F"; // Mute
            else if (volume < 33)
                VolumeIcon.Text = "\uE993"; // Low
            else if (volume < 66)
                VolumeIcon.Text = "\uE994"; // Medium
            else
                VolumeIcon.Text = "\uE995"; // High

            // Update fill color to match accent
            VolGradient1.Color = _cachedDominantColor;
            VolGradient2.Color = Color.FromArgb(255,
                (byte)Math.Min(255, _cachedDominantColor.R + 50),
                (byte)Math.Min(255, _cachedDominantColor.G + 50),
                (byte)Math.Min(255, _cachedDominantColor.B + 50));
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

                // Try to get the source app ID from SMTC
                string sourceApp = null;
                try
                {
                    var sessions = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                    // Use cached info from MediaSessionService
                    if (MediaSessionService.Instance.IsUsingLegacyPlayer)
                    {
                        sourceApp = MediaSessionService.Instance.LegacyPlayerName;
                    }
                }
                catch { }

                // Search through all devices and apps
                foreach (var device in collection.AllDevices)
                {
                    foreach (var app in device.Apps)
                    {
                        // Match by ExeName
                        if (!string.IsNullOrEmpty(sourceApp) && 
                            !string.IsNullOrEmpty(app.ExeName) &&
                            app.ExeName.IndexOf(sourceApp, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return app;
                        }

                        // Match common media apps by name
                        var exeLower = (app.ExeName ?? "").ToLowerInvariant();
                        if (exeLower.Contains("spotify") || exeLower.Contains("chrome") || 
                            exeLower.Contains("firefox") || exeLower.Contains("msedge") ||
                            exeLower.Contains("vlc") || exeLower.Contains("musicbee") ||
                            exeLower.Contains("foobar") || exeLower.Contains("winamp") ||
                            exeLower.Contains("aimp") || exeLower.Contains("itunes") ||
                            exeLower.Contains("groove") || exeLower.Contains("wmplayer"))
                        {
                            // Only return if it's the active (non-zero peak) session
                            if (app.PeakValue1 > 0 || app.PeakValue2 > 0)
                            {
                                return app;
                            }
                        }
                    }
                }

                // Fallback: find any app with audio activity (skip system sounds)
                foreach (var device in collection.AllDevices)
                {
                    foreach (var app in device.Apps)
                    {
                        if ((app.PeakValue1 > 0 || app.PeakValue2 > 0) && !string.IsNullOrEmpty(app.ExeName))
                        {
                            return app;
                        }
                    }
                }
            }
            catch (Exception ex) { Trace.WriteLine($"MediaPopupWindow: FindCurrentMediaApp failed - {ex.Message}"); }

            return null;
        }

        public bool IsShowing => _isShowing;
        public bool IsExpanded => _isExpanded;
    }
}
