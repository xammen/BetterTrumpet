using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using EarTrumpet.UI.ViewModels;

namespace EarTrumpet.UI.Controls
{
    public class VolumeSlider : Slider
    {
        // Smoothing factor for peak meter: higher = faster response, lower = smoother (0.0 - 1.0)
        private const double PeakSmoothingFactor = 0.35;
        private const string ThumbBrushRef = "Theme=SystemAccent, HighContrast=ControlText";
        private const string TrackFillBrushRef = "SystemAccent";
        private const string TrackBackgroundBrushRef = ":Theme=Control{Theme}SliderTrackFillDisabled, :HighContrast=ControlText, Flyout:Theme=FlyoutThemeTrackRightBackground, Flyout:HighContrast=ControlText";
        private const string PeakMeterBrushRef = "Theme=SystemAccent, HighContrast=HotTrack";
        
        // Default smoothing factor for volume slider animation when clicking on track.
        // This value is kept for settings compatibility; it is mapped to a real duration
        // below so the animation feels consistent regardless of the render rate.
        private const double DefaultVolumeSmoothingFactor = 0.08;
        
        // Get the smoothing factor from settings (or use default)
        private double VolumeSmoothingFactor => App.Settings?.VolumeAnimationSpeed ?? DefaultVolumeSmoothingFactor;
        
        // Check if smooth animation is enabled in settings
        private bool IsSmoothAnimationEnabled => App.Settings?.UseSmoothVolumeAnimation ?? true;

        // Sound effect tracking
        private double _lastSoundValue = -1;
        private DateTime _lastSoundTime = DateTime.MinValue;
        private const int SoundThrottleMs = 50; // Min time between sounds

        public float PeakValue1
        {
            get { return (float)this.GetValue(PeakValue1Property); }
            set { this.SetValue(PeakValue1Property, value); }
        }
        public static readonly DependencyProperty PeakValue1Property = DependencyProperty.Register(
          "PeakValue1", typeof(float), typeof(VolumeSlider), new PropertyMetadata(0f, new PropertyChangedCallback(PeakValueChanged)));

        public float PeakValue2
        {
            get { return (float)this.GetValue(PeakValue2Property); }
            set { this.SetValue(PeakValue2Property, value); }
        }
        public static readonly DependencyProperty PeakValue2Property = DependencyProperty.Register(
          "PeakValue2", typeof(float), typeof(VolumeSlider), new PropertyMetadata(0f, new PropertyChangedCallback(PeakValueChanged)));

        // Custom color brushes - bindable from XAML
        public Brush CustomThumbBrush
        {
            get { return (Brush)GetValue(CustomThumbBrushProperty); }
            set { SetValue(CustomThumbBrushProperty, value); }
        }
        public static readonly DependencyProperty CustomThumbBrushProperty = DependencyProperty.Register(
            "CustomThumbBrush", typeof(Brush), typeof(VolumeSlider), new PropertyMetadata(null));

        public Brush CustomTrackFillBrush
        {
            get { return (Brush)GetValue(CustomTrackFillBrushProperty); }
            set { SetValue(CustomTrackFillBrushProperty, value); }
        }
        public static readonly DependencyProperty CustomTrackFillBrushProperty = DependencyProperty.Register(
            "CustomTrackFillBrush", typeof(Brush), typeof(VolumeSlider), new PropertyMetadata(null));

        public Brush CustomTrackBackgroundBrush
        {
            get { return (Brush)GetValue(CustomTrackBackgroundBrushProperty); }
            set { SetValue(CustomTrackBackgroundBrushProperty, value); }
        }
        public static readonly DependencyProperty CustomTrackBackgroundBrushProperty = DependencyProperty.Register(
            "CustomTrackBackgroundBrush", typeof(Brush), typeof(VolumeSlider), new PropertyMetadata(null));

        public Brush CustomPeakMeterBrush
        {
            get { return (Brush)GetValue(CustomPeakMeterBrushProperty); }
            set { SetValue(CustomPeakMeterBrushProperty, value); }
        }
        public static readonly DependencyProperty CustomPeakMeterBrushProperty = DependencyProperty.Register(
            "CustomPeakMeterBrush", typeof(Brush), typeof(VolumeSlider), new PropertyMetadata(null));

        private Border _peakMeter1;
        private Border _peakMeter2;
        private Thumb _thumb;
        private Track _track;
        private RepeatButton _sliderLeft;
        private RepeatButton _sliderRight;
        private Point _lastMousePosition;
        
        // Peak meter style state
        private PeakMeterStyle _currentPeakStyle = PeakMeterStyle.Classic;
        
        // Smooth animation state for peak meters
        private double _currentWidth1;
        private double _currentWidth2;
        private double _targetWidth1;
        private double _targetWidth2;
        private bool _isAnimating;
        
        // Smooth animation state for volume slider
        private const double DragHandoffDurationMs = 50.0;
        private double _renderedValue;
        private double _volumeAnimationStartValue;
        private double _targetValue;
        private double _volumeAnimationDurationMs;
        private long _volumeAnimationStartTimestamp;
        private DoubleAnimation _volumeTrackAnimation;
        private bool _isAnimatingValue;
        private bool _isDragHandoffAnimating;
        private long _trackMouseDownTimestamp;
        private bool _isDragging;
        private bool _clickedOnTrack; // Track if initial click was on track (not thumb)
        
        // Conditional rendering - only animate when there's actual work to do
        private bool _hasPeakActivity; // True when peak values are non-zero
        private TimeSpan _lastPeakActivity = TimeSpan.Zero;
        private const double PeakIdleTimeoutMs = 500; // Stop rendering after 500ms of silence
        
        // FPS limiting for eco mode
        private TimeSpan _lastRenderTime = TimeSpan.Zero;
        private int _targetFps = 60;
        private double _frameInterval = 1000.0 / 60.0; // milliseconds between frames

        public VolumeSlider() : base()
        {
            PreviewTouchDown += OnTouchDown;
            PreviewMouseDown += OnMouseDown;
            TouchUp += OnTouchUp;
            MouseUp += OnMouseUp;
            TouchMove += OnTouchMove;
            MouseMove += OnMouseMove;
            MouseWheel += OnMouseWheel;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            IsVisibleChanged += OnIsVisibleChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _thumb = (Thumb)GetTemplateChild("SliderThumb");
            _track = (Track)GetTemplateChild("PART_Track");
            _peakMeter1 = (Border)GetTemplateChild("PeakMeter1");
            _peakMeter2 = (Border)GetTemplateChild("PeakMeter2");
            _sliderLeft = (RepeatButton)GetTemplateChild("SliderLeft");
            _sliderRight = (RepeatButton)GetTemplateChild("SliderRight");
            
            // Initialize current widths
            _currentWidth1 = 0;
            _currentWidth2 = 0;
            _renderedValue = Value;
            
            // Initialize peak meter style
            ApplyPeakMeterStyle();
            
            // Apply custom colors if enabled.
            ApplyCustomColors();

            // The theme system (ThemeBindingInfo) applies Theme:Brush values to the template
            // parts (thumb, track, peak meters) in their own Loaded handlers, as local values.
            // Depending on Loaded ordering, that can overwrite the custom colors we just set,
            // which is why a freshly created slider (new/relaunched app) can revert to the
            // default theme while sliders present at startup stay themed (a global ThemeChanged
            // re-applies over them). Re-apply after the theme system finishes so custom colors
            // always win, regardless of Loaded ordering.
            // OPTIMIZATION: Use InvokeAsync instead of BeginInvoke for better performance
            _ = Dispatcher.InvokeAsync(ApplyCustomColors, System.Windows.Threading.DispatcherPriority.Loaded);
            
            // Subscribe to settings changes for live preview
            if (App.Settings != null)
            {
                App.Settings.CustomSliderColorsChanged += OnCustomSliderColorsChanged;
                App.Settings.EcoModeChanged += OnEcoModeChanged;
                App.Settings.PeakMeterStyleChanged += OnPeakMeterStyleChanged;
            }
            
            // Re-apply custom colors after any theme change (the theme system
            // re-sets Foreground via local values, so we must re-override)
            if (UI.Themes.Manager.Current != null)
            {
                UI.Themes.Manager.Current.ThemeChanged += OnThemeChangedReapplyColors;
            }
            
            // Initialize FPS limiting
            UpdateTargetFps();
            
            // DON'T start animation loop on load - only start when there's actual activity
            // This saves CPU when the slider is idle (most of the time)
            // Animation will auto-start when peak values change or volume animation is triggered
        }
        
        private void UpdateTargetFps()
        {
            if (App.Settings != null)
            {
                _targetFps = App.Settings.EffectivePeakMeterFps;
            }
            else
            {
                _targetFps = 60;
            }
            _frameInterval = 1000.0 / _targetFps;
        }
        
        private void OnEcoModeChanged()
        {
            // Update FPS target when eco mode changes
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(UpdateTargetFps);
                return;
            }
            UpdateTargetFps();
        }
        
        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                // Resume the peak meters if the (still bound) peak values are non-zero.
                SizeOrVolumeOrPeakValueChanged();
                return;
            }

            // The flyout is hidden, not unloaded, when it closes. Peak values stop updating at that
            // point and keep their last non-zero level if audio was playing, which used to keep this
            // CompositionTarget.Rendering loop (and with it the WPF render loop) running every frame
            // for as long as the app lived (GitHub #74). Settle everything and start from empty
            // meters the next time the slider is shown.
            CompleteVolumeAnimation();
            StopAnimation();
            _hasPeakActivity = false;
            _currentWidth1 = 0;
            _currentWidth2 = 0;
            if (_peakMeter1 != null)
            {
                _peakMeter1.Width = 0;
            }
            if (_peakMeter2 != null)
            {
                _peakMeter2.Width = 0;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CompleteVolumeAnimation();
            StopAnimation();
            
            // Unsubscribe from settings changes
            if (App.Settings != null)
            {
                App.Settings.CustomSliderColorsChanged -= OnCustomSliderColorsChanged;
                App.Settings.EcoModeChanged -= OnEcoModeChanged;
                App.Settings.PeakMeterStyleChanged -= OnPeakMeterStyleChanged;
            }
            if (UI.Themes.Manager.Current != null)
            {
                UI.Themes.Manager.Current.ThemeChanged -= OnThemeChangedReapplyColors;
            }
        }
        
        private void OnPeakMeterStyleChanged()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(ApplyPeakMeterStyle);
                return;
            }
            ApplyPeakMeterStyle();
        }
        
        /// <summary>
        /// Apply visual style to peak meter Borders.
        /// Each style changes height, opacity, corner radius, and margin to create
        /// a distinct visual feel — all using the same Border elements.
        /// 
        /// Classic:  2 thick bars (4px), full opacity — the original look
        /// Dotted:   1 thin bar (1.5px), low opacity — subtle, minimal
        /// Blocks:   1 medium bar (2.5px), dashed look via OpacityMask
        /// Bars:     1 ultra-thin line (1px), very low opacity — barely visible whisper
        /// Wave:     1 thin bar (2px), medium opacity — clean single line
        /// </summary>
        private void ApplyPeakMeterStyle()
        {
            if (App.Settings == null) return;
            
            _currentPeakStyle = App.Settings.PeakMeterStyle;
            
            if (_peakMeter1 == null || _peakMeter2 == null) return;
            
            // Dim the slider track fill when using non-Classic styles
            // so the peak meter pattern pops against a darker background
            if (_sliderLeft != null)
            {
                _sliderLeft.Opacity = _currentPeakStyle == PeakMeterStyle.Classic ? 1.0 : 0.4;
            }
            
            switch (_currentPeakStyle)
            {
                case PeakMeterStyle.Classic:
                    // Original: two thick bars, stereo
                    _peakMeter1.Height = 4;
                    _peakMeter1.Margin = new Thickness(0, -3, 0, 0);
                    _peakMeter1.CornerRadius = new CornerRadius(2);
                    _peakMeter1.Opacity = 0.6;
                    _peakMeter1.Visibility = Visibility.Visible;
                    _peakMeter1.OpacityMask = null;
                    _peakMeter2.Height = 4;
                    _peakMeter2.Margin = new Thickness(0, 2, 0, 0);
                    _peakMeter2.CornerRadius = new CornerRadius(2);
                    _peakMeter2.Opacity = 0.6;
                    _peakMeter2.Visibility = Visibility.Visible;
                    _peakMeter2.OpacityMask = null;
                    break;
                    
                case PeakMeterStyle.Dotted:
                    // Dotted: small squares with tiny gaps, punchy and visible
                    _peakMeter1.Height = 3;
                    _peakMeter1.Margin = new Thickness(0);
                    _peakMeter1.CornerRadius = new CornerRadius(0);
                    _peakMeter1.Opacity = 0.7;
                    _peakMeter1.Visibility = Visibility.Visible;
                    _peakMeter1.OpacityMask = CreateDottedBrush(3, 2);
                    _peakMeter2.Visibility = Visibility.Collapsed;
                    break;
                    
                case PeakMeterStyle.Blocks:
                    // Blocks: wide chunky segments, LED meter style
                    _peakMeter1.Height = 4;
                    _peakMeter1.Margin = new Thickness(0);
                    _peakMeter1.CornerRadius = new CornerRadius(0);
                    _peakMeter1.Opacity = 0.7;
                    _peakMeter1.Visibility = Visibility.Visible;
                    _peakMeter1.OpacityMask = CreateDottedBrush(6, 2);
                    _peakMeter2.Visibility = Visibility.Collapsed;
                    break;
                    
                case PeakMeterStyle.Bars:
                    // Line: single clean thin bar, no pattern
                    _peakMeter1.Height = 2;
                    _peakMeter1.Margin = new Thickness(0);
                    _peakMeter1.CornerRadius = new CornerRadius(1);
                    _peakMeter1.Opacity = 0.55;
                    _peakMeter1.Visibility = Visibility.Visible;
                    _peakMeter1.OpacityMask = null;
                    _peakMeter2.Visibility = Visibility.Collapsed;
                    break;
                    
                case PeakMeterStyle.Wave:
                    // Dashes: wide spaced dashes, retro feel
                    _peakMeter1.Height = 3;
                    _peakMeter1.Margin = new Thickness(0);
                    _peakMeter1.CornerRadius = new CornerRadius(1);
                    _peakMeter1.Opacity = 0.65;
                    _peakMeter1.Visibility = Visibility.Visible;
                    _peakMeter1.OpacityMask = CreateDottedBrush(5, 4);
                    _peakMeter2.Visibility = Visibility.Collapsed;
                    break;
            }
        }
        
        /// <summary>
        /// Creates a horizontal repeating pattern brush for dotted/dashed effects.
        /// Uses a DrawingBrush tiled horizontally to create segment gaps.
        /// </summary>
        private static Brush CreateDottedBrush(double segmentWidth, double gapWidth)
        {
            var totalWidth = segmentWidth + gapWidth;
            var drawing = new GeometryDrawing
            {
                Geometry = new RectangleGeometry(new System.Windows.Rect(0, 0, segmentWidth, 1)),
                Brush = Brushes.White
            };
            var brush = new DrawingBrush
            {
                Drawing = drawing,
                TileMode = TileMode.Tile,
                Viewport = new System.Windows.Rect(0, 0, totalWidth, 1),
                ViewportUnits = BrushMappingMode.Absolute,
                Viewbox = new System.Windows.Rect(0, 0, totalWidth, 1),
                ViewboxUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.None
            };
            brush.Freeze();
            return brush;
        }
        
        private void OnThemeChangedReapplyColors()
        {
            // After the theme system re-paints all elements, re-apply custom colors
            // Use BeginInvoke so we run AFTER the theme system finishes its updates
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(ApplyCustomColors));
                return;
            }
            Dispatcher.BeginInvoke(new Action(ApplyCustomColors), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        
        private void OnCustomSliderColorsChanged()
        {
            // Ensure we're on the UI thread
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(ApplyCustomColors);
                return;
            }
            ApplyCustomColors();
        }
        
        private void ApplyCustomColors()
        {
            var settings = App.Settings;
            if (settings == null) return;
            
            if (settings.UseCustomSliderColors)
            {
                // Update DependencyProperties (for DataTrigger bindings). Stored Transparent
                // means "use the current default", not "fall back to the white WPF theme".
                var thumbColor = settings.SliderThumbColor;
                CustomThumbBrush = new SolidColorBrush(thumbColor != Colors.Transparent ? thumbColor : ThemeRegistry.DefaultAccentColor);
                
                var trackFillColor = settings.SliderTrackFillColor;
                CustomTrackFillBrush = new SolidColorBrush(trackFillColor != Colors.Transparent ? trackFillColor : ThemeRegistry.DefaultAccentColor);
                
                var trackBgColor = settings.SliderTrackBackgroundColor;
                CustomTrackBackgroundBrush = new SolidColorBrush(trackBgColor != Colors.Transparent ? trackBgColor : ThemeRegistry.DefaultTrackBackground);
                
                var peakColor = settings.PeakMeterColor;
                CustomPeakMeterBrush = new SolidColorBrush(peakColor != Colors.Transparent ? peakColor : ThemeRegistry.DefaultPeakMeter);
                
                // Also apply directly to visual elements — the Theme:Brush system sets local
                // values (priority 11) which override DataTrigger setters (priority 5), so we
                // must set local values ourselves to win the priority battle.
                ApplyColorsToVisualElements();
            }
            else
            {
                // Clear custom brushes
                CustomThumbBrush = null;
                CustomTrackFillBrush = null;
                CustomTrackBackgroundBrush = null;
                CustomPeakMeterBrush = null;
                
                // Clear local values so theme system can take over again
                ResetVisualElementColors();
            }
        }
        
        private void ApplyColorsToVisualElements()
        {
            if (_thumb != null && CustomThumbBrush != null)
                _thumb.Foreground = CustomThumbBrush;
            if (_sliderLeft != null && CustomTrackFillBrush != null)
                _sliderLeft.Foreground = CustomTrackFillBrush;
            if (_sliderRight != null && CustomTrackBackgroundBrush != null)
                _sliderRight.Foreground = CustomTrackBackgroundBrush;
            if (_peakMeter1 != null && CustomPeakMeterBrush != null)
                _peakMeter1.Background = CustomPeakMeterBrush;
            if (_peakMeter2 != null && CustomPeakMeterBrush != null)
                _peakMeter2.Background = CustomPeakMeterBrush;

        }
        
        private void ResetVisualElementColors()
        {
            if (_thumb != null)
            {
                _thumb.ClearValue(Control.ForegroundProperty);
                _thumb.Foreground = ResolveThemeBrush(_thumb, ThumbBrushRef, ThemeRegistry.DefaultAccentColor);
            }

            if (_sliderLeft != null)
            {
                _sliderLeft.ClearValue(Control.ForegroundProperty);
                _sliderLeft.Foreground = ResolveThemeBrush(_sliderLeft, TrackFillBrushRef, ThemeRegistry.DefaultAccentColor);
            }

            if (_sliderRight != null)
            {
                _sliderRight.ClearValue(Control.ForegroundProperty);
                _sliderRight.Foreground = ResolveThemeBrush(_sliderRight, TrackBackgroundBrushRef, ThemeRegistry.DefaultTrackBackground);
            }

            if (_peakMeter1 != null)
            {
                _peakMeter1.ClearValue(Border.BackgroundProperty);
                _peakMeter1.Background = ResolveThemeBrush(_peakMeter1, PeakMeterBrushRef, ThemeRegistry.DefaultPeakMeter);
            }

            if (_peakMeter2 != null)
            {
                _peakMeter2.ClearValue(Border.BackgroundProperty);
                _peakMeter2.Background = ResolveThemeBrush(_peakMeter2, PeakMeterBrushRef, ThemeRegistry.DefaultPeakMeter);
            }
        }

        private static Brush ResolveThemeBrush(DependencyObject target, string themeBrushRef, Color fallback)
        {
            try
            {
                if (UI.Themes.Manager.Current != null)
                {
                    return new SolidColorBrush(UI.Themes.Manager.Current.ResolveRef(target, themeBrushRef));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"VolumeSlider: Failed to resolve theme brush '{themeBrushRef}' — {ex.Message}");
            }

            return new SolidColorBrush(fallback);
        }
        
        private void StartAnimation()
        {
            if (!_isAnimating)
            {
                _isAnimating = true;
                CompositionTarget.Rendering += OnRendering;
            }
        }
        
        private void StopAnimation()
        {
            if (_isAnimating)
            {
                _isAnimating = false;
                CompositionTarget.Rendering -= OnRendering;
            }
        }

        private void CompleteVolumeAnimation()
        {
            if (_isAnimatingValue)
            {
                _isAnimatingValue = false;
                _isDragHandoffAnimating = false;
                StopVolumeTrackAnimation();
                Value = _targetValue;
                ApplyRenderedTrackValue(_targetValue);
            }
        }

        private void StopVolumeTrackAnimation()
        {
            if (_track != null)
            {
                _track.BeginAnimation(Track.ValueProperty, null);
            }

            _volumeTrackAnimation = null;
        }

        private void StartVolumeTrackAnimation(double startValue, double targetValue, double durationMs)
        {
            if (_track == null)
            {
                _volumeTrackAnimation = null;
                return;
            }

            var animation = new DoubleAnimation(startValue, targetValue, TimeSpan.FromMilliseconds(durationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd,
            };

            _volumeTrackAnimation = animation;
            animation.Completed += (sender, args) => OnVolumeTrackAnimationCompleted(animation);
            _track.BeginAnimation(Track.ValueProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private void OnVolumeTrackAnimationCompleted(DoubleAnimation animation)
        {
            if (!ReferenceEquals(animation, _volumeTrackAnimation))
            {
                return;
            }

            _isAnimatingValue = false;
            _isDragHandoffAnimating = false;
            StopVolumeTrackAnimation();
            Value = _targetValue;
            ApplyRenderedTrackValue(_targetValue);
        }

        private void ApplyRenderedTrackValue(double value)
        {
            _renderedValue = value;

            if (_track != null && Math.Abs(_track.Value - value) > 0.001)
            {
                _track.Value = value;
            }
        }

        private void CommitAnimatedVolumeValue(double value)
        {
            var direction = Math.Sign(_targetValue - _volumeAnimationStartValue);
            var committedValue = direction < 0 ? Math.Ceiling(value) : Math.Floor(value);
            committedValue = Bound(committedValue);

            // Keep the audio binding integer-granular, but let the template Track
            // render the fractional position so the thumb remains visually smooth.
            if (Math.Abs(Value - committedValue) > 0.001)
            {
                Value = committedValue;
            }

            _renderedValue = value;
        }

        private void ApplyManualAnimatedVolumeValue(double value)
        {
            CommitAnimatedVolumeValue(value);
            ApplyRenderedTrackValue(value);
        }

        private double GetVolumeAnimationDurationMs()
        {
            var speed = Math.Max(0.02, Math.Min(0.5, VolumeSmoothingFactor));
            var normalizedSpeed = (speed - 0.02) / 0.48;

            // Keep the interaction quick at normal settings while leaving enough
            // time at the slowest setting for the movement to remain readable.
            return 320.0 - (normalizedSpeed * 220.0);
        }
        
        private void OnRendering(object sender, EventArgs e)
        {
            // FPS limiting: use vsync timestamp for accurate frame timing
            var renderArgs = e as System.Windows.Media.RenderingEventArgs;
            var now = renderArgs != null ? renderArgs.RenderingTime : TimeSpan.FromTicks(DateTime.UtcNow.Ticks);
            var elapsed = (now - _lastRenderTime).TotalMilliseconds;
            
            // For peak meter updates, respect FPS limit
            // But always process volume animation for responsiveness
            // Note: only update _lastRenderTime when we actually process peak meters,
            // otherwise elapsed stays small and we skip most frames
            // Use 0.8x tolerance to avoid skipping frames due to float precision
            // (at 60fps vsync, elapsed ≈ 16.67ms and _frameInterval = 16.67ms — without
            // tolerance, half the frames get skipped due to timing jitter)
            bool shouldUpdatePeakMeter = elapsed >= _frameInterval * 0.8;
            
            // Track if we're actually doing any work this frame
            bool didWork = false;
            
            if (shouldUpdatePeakMeter)
            {
                _lastRenderTime = now;
                
                // Check if peak meters need updating (non-zero targets or current values still animating down)
                bool peakNeedsUpdate = _targetWidth1 > 0.1 || _targetWidth2 > 0.1 || 
                                       _currentWidth1 > 0.1 || _currentWidth2 > 0.1;
                
                if (peakNeedsUpdate)
                {
                    _lastPeakActivity = now;
                    _hasPeakActivity = true;
                    didWork = true;
                    
                    // Lerp current values toward target values for peak meters
                    _currentWidth1 = Lerp(_currentWidth1, _targetWidth1, PeakSmoothingFactor);
                    _currentWidth2 = Lerp(_currentWidth2, _targetWidth2, PeakSmoothingFactor);
                    
                    // Apply smoothed values to Border widths
                    if (_peakMeter1 != null)
                    {
                        if (_currentPeakStyle == PeakMeterStyle.Classic)
                        {
                            // Classic: each channel gets its own bar
                            _peakMeter1.Width = Math.Max(0, _currentWidth1);
                        }
                        else
                        {
                            // Non-classic: single bar uses the max of both channels
                            _peakMeter1.Width = Math.Max(0, Math.Max(_currentWidth1, _currentWidth2));
                        }
                    }
                    
                    if (_peakMeter2 != null && _currentPeakStyle == PeakMeterStyle.Classic)
                    {
                        _peakMeter2.Width = Math.Max(0, _currentWidth2);
                    }
                }
                else if (_hasPeakActivity)
                {
                    // Check if we've been idle long enough to stop the render loop
                    if ((now - _lastPeakActivity).TotalMilliseconds > PeakIdleTimeoutMs)
                    {
                        _hasPeakActivity = false;
                    }
                }
            }
            
            // Animate the initial track click and the short handoff into dragging.
            // This always runs for responsive feel, including while the drag state
            // is already active during the handoff.
            if (_isAnimatingValue)
            {
                didWork = true;

                if (_track != null && _volumeTrackAnimation != null)
                {
                    // WPF owns the fractional visual value through its render-timed
                    // animation clock. Only commit the integer audio value here.
                    CommitAnimatedVolumeValue(_track.Value);
                }
                else
                {
                    var elapsedMs = (Stopwatch.GetTimestamp() - _volumeAnimationStartTimestamp) * 1000.0 / Stopwatch.Frequency;
                    var progress = Math.Max(0.0, Math.Min(1.0, elapsedMs / _volumeAnimationDurationMs));
                    var easedProgress = EaseOutCubic(progress);
                    var newValue = _volumeAnimationStartValue + ((_targetValue - _volumeAnimationStartValue) * easedProgress);

                    if (progress >= 1.0)
                    {
                        _isAnimatingValue = false;
                        _isDragHandoffAnimating = false;
                        Value = _targetValue;
                        ApplyRenderedTrackValue(_targetValue);
                    }
                    else
                    {
                        ApplyManualAnimatedVolumeValue(newValue);
                    }
                }
            }
            
            // Auto-stop animation loop when nothing needs animating
            // This saves significant CPU when the slider is idle
            // Also keep running if there are non-zero targets (data arriving but FPS-skipped this frame)
            bool hasActiveTargets = _targetWidth1 > 0.1 || _targetWidth2 > 0.1;
            if (!didWork && !_isAnimatingValue && !_hasPeakActivity && !hasActiveTargets)
            {
                StopAnimation();
            }
        }
        
        private static double Lerp(double current, double target, double factor)
        {
            return current + (target - current) * factor;
        }

        private static double EaseOutCubic(double progress)
        {
            var inverseProgress = 1.0 - progress;
            return 1.0 - (inverseProgress * inverseProgress * inverseProgress);
        }
        
        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            var ret = base.ArrangeOverride(arrangeBounds);
            SizeOrVolumeOrPeakValueChanged();
            return ret;
        }

        private static void PeakValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((VolumeSlider)d).SizeOrVolumeOrPeakValueChanged();
        }

        private void SizeOrVolumeOrPeakValueChanged()
        {
            if (_thumb == null) return;
            
            // Calculate target widths (the animation will smoothly interpolate toward these)
            _targetWidth1 = (ActualWidth - _thumb.ActualWidth) * PeakValue1 * (Value / 100f);
            _targetWidth2 = (ActualWidth - _thumb.ActualWidth) * PeakValue2 * (Value / 100f);
            
            // Auto-start animation loop when peak values change (conditional rendering optimization).
            // Never for an invisible slider: a volume/size change while the flyout is hidden would
            // otherwise restart the loop on stale peak values (see OnIsVisibleChanged).
            if (IsVisible && (_targetWidth1 > 0.1 || _targetWidth2 > 0.1))
            {
                StartAnimation();
            }
        }

        private void OnTouchDown(object sender, TouchEventArgs e)
        {
            VisualStateManager.GoToState((FrameworkElement)sender, "Pressed", true);

            // Ensure we have the thumb reference
            if (_thumb == null)
            {
                _thumb = GetTemplateChild("SliderThumb") as Thumb;
            }
            
            // Ensure animation loop is running
            StartAnimation();
            
            // Touch down on track - animate smoothly, then commit the target
            // when the interaction ends or the flyout unloads.
            _clickedOnTrack = true;
            _isDragging = false;
            SetPositionByControlPoint(e.GetTouchPoint(this).Position, animate: true);
            CaptureTouch(e.TouchDevice);

            e.Handled = true;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _lastMousePosition = e.GetPosition(this);
                VisualStateManager.GoToState((FrameworkElement)sender, "Pressed", true);

                // Ensure we have the thumb reference (may not be set if template applied late)
                if (_thumb == null)
                {
                    _thumb = GetTemplateChild("SliderThumb") as Thumb;
                }
                
                // Ensure animation loop is running
                StartAnimation();

                // Only start dragging if we KNOW we clicked on the thumb
                // Otherwise (clicked on track, or thumb not found), animate
                // quickly toward the target. MouseUp/Unloaded commits the
                // final value if the flyout closes before the render loop ends.
                if (_thumb != null && _thumb.IsMouseOver)
                {
                    // Click on thumb - start dragging immediately
                    _clickedOnTrack = false;
                    _isDragHandoffAnimating = false;
                    _isAnimatingValue = false;
                    StopVolumeTrackAnimation();
                    _isDragging = true;
                }
                else
                {
                    // Click on track (or thumb not found) - animate smoothly.
                    _clickedOnTrack = true;
                    _isDragHandoffAnimating = false;
                    _isDragging = false;
                    _isAnimatingValue = false;
                    _trackMouseDownTimestamp = Stopwatch.GetTimestamp();
                    SetPositionByControlPoint(_lastMousePosition, animate: true);
                }

                CaptureMouse();
                e.Handled = true;
            }
        }

        private void OnTouchUp(object sender, TouchEventArgs e)
        {
            VisualStateManager.GoToState((FrameworkElement)sender, "Normal", true);
            _isDragging = false;

            ReleaseTouchCapture(e.TouchDevice);
            e.Handled = true;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseCaptured)
            {
                _isDragging = false;
                _clickedOnTrack = false;
                _isDragHandoffAnimating = false;
                
                // If the point is outside of the control, clear the hover state.
                Rect rcSlider = new Rect(0, 0, ActualWidth, ActualHeight);
                if (!rcSlider.Contains(e.GetPosition(this)))
                {
                    VisualStateManager.GoToState((FrameworkElement)sender, "Normal", true);
                }

                ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void OnTouchMove(object sender, TouchEventArgs e)
        {
            if (AreAnyTouchesCaptured)
            {
                // Touch move is like dragging - instant updates
                _isDragging = true;
                _isAnimatingValue = false;
                SetPositionByControlPoint(e.GetTouchPoint(this).Position, animate: false);
                e.Handled = true;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var mousePosition = e.GetPosition(this);
            if (IsMouseCaptured && mousePosition != _lastMousePosition)
            {
                _lastMousePosition = mousePosition;
                
                if (_clickedOnTrack)
                {
                    _clickedOnTrack = false;
                    _isDragging = true;

                    var heldMs = (Stopwatch.GetTimestamp() - _trackMouseDownTimestamp) * 1000.0 / Stopwatch.Frequency;
                    if (heldMs >= DragHandoffDurationMs)
                    {
                        // The press had time to start its glide: bridge it into the
                        // drag with the short catch-up animation.
                        _isDragHandoffAnimating = true;
                        SetPositionByControlPoint(mousePosition, animate: true);
                    }
                    else
                    {
                        // The pointer moved immediately after pressing: this is a
                        // drag, not a click-to-jump. Follow the cursor directly so
                        // a far-away press cannot fire a confusing catch-up slide.
                        _isDragHandoffAnimating = false;
                        SetPositionByControlPoint(mousePosition, animate: false);
                    }
                }
                else if (_isDragging)
                {
                    if (_isDragHandoffAnimating && GetRemainingHandoffDurationMs() <= 16.0)
                    {
                        // Budget spent: finish as a plain direct drag instead of
                        // restarting micro-animations on every move.
                        _isDragHandoffAnimating = false;
                    }

                    // Keep the original handoff deadline if the pointer moves
                    // again. Restarting a fresh 50 ms animation here makes the
                    // thumb perpetually lag behind a continuously moving cursor.
                    SetPositionByControlPoint(mousePosition, animate: _isDragHandoffAnimating,
                        preserveHandoffTiming: _isDragHandoffAnimating);
                }
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var oldValue = Value;
            var amount = Math.Sign(e.Delta) * 2.0;
            ChangePositionByAmount(amount);

            // Play tick sound when scrolling
            try
            {
                if (Math.Abs(Value - oldValue) > 0.5)
                {
                    PlayVolumeTickSound(Value);
                }
            }
            catch { /* Ignore sound errors */ }

            e.Handled = true;
        }

        public void SetPositionByControlPoint(Point point, bool animate = false)
        {
            SetPositionByControlPoint(point, animate, preserveHandoffTiming: false);
        }

        private void SetPositionByControlPoint(Point point, bool animate, bool preserveHandoffTiming)
        {
            var thumbWidth = _thumb?.ActualWidth ?? 0;
            var trackWidth = ActualWidth - thumbWidth;
            double percent;
            if (trackWidth > 0)
            {
                percent = (point.X - thumbWidth / 2.0) / trackWidth;
            }
            else
            {
                percent = point.X / ActualWidth;
            }
            // percent is normalized against the track, so it has to be mapped back onto
            // [Minimum, Maximum]. Minimum happens to be 0 today, but dropping it here makes
            // the mapping silently depend on that.
            var newValue = Bound(Minimum + (Maximum - Minimum) * percent);
            
            // Only animate if requested AND smooth animation is enabled in settings
            if (animate && IsSmoothAnimationEnabled)
            {
                // Ensure animation loop is running
                StartAnimation();
                
                // Retarget from the value currently rendered so a second click feels
                // continuous instead of jumping back to the previous target.
                _volumeAnimationStartValue = preserveHandoffTiming && _track != null
                    ? _track.Value
                    : _renderedValue;
                _targetValue = newValue;
                if (!preserveHandoffTiming)
                {
                    _volumeAnimationDurationMs = _isDragHandoffAnimating
                        ? DragHandoffDurationMs
                        : GetVolumeAnimationDurationMs();
                    _volumeAnimationStartTimestamp = Stopwatch.GetTimestamp();
                }
                else if (_track != null)
                {
                    _volumeAnimationDurationMs = GetRemainingHandoffDurationMs();
                }

                _isAnimatingValue = true;
                StartVolumeTrackAnimation(_volumeAnimationStartValue, _targetValue, _volumeAnimationDurationMs);
            }
            else
            {
                // Instant update (for dragging or when animation is disabled)
                _isAnimatingValue = false;
                _isDragHandoffAnimating = false;
                StopVolumeTrackAnimation();
                Value = newValue;
                ApplyRenderedTrackValue(newValue);
            }
        }

        private double GetRemainingHandoffDurationMs()
        {
            var elapsedMs = (Stopwatch.GetTimestamp() - _volumeAnimationStartTimestamp) * 1000.0 / Stopwatch.Frequency;
            return Math.Max(1.0, DragHandoffDurationMs - elapsedMs);
        }

        protected override void OnValueChanged(double oldValue, double newValue)
        {
            base.OnValueChanged(oldValue, newValue);

            if (!_isAnimatingValue)
            {
                _renderedValue = newValue;
            }

            // Play tick sound when value changes (only if user is interacting)
            // Dragging and click-glide animations both count as interaction;
            // purely programmatic changes stay silent.
            try
            {
                if ((_isDragging || _isAnimatingValue) && Math.Abs(newValue - oldValue) > 0.5)
                {
                    PlayVolumeTickSound(newValue);
                }
            }
            catch { /* Ignore sound errors */ }
        }

        public void ChangePositionByAmount(double amount)
        {
            Value = Bound(Value + amount);
        }

        public double Bound(double val)
        {
            return Math.Max(Minimum, Math.Min(Maximum, val));
        }

        private void PlayVolumeTickSound(double newValue)
        {
            // Check if tick sound is enabled in settings
            if (App.Settings?.UseVolumeTickSound != true)
                return;

            var useMonkeySound = App.Settings?.MonkeyTickSoundUnlocked == true &&
                                 App.Settings?.UseMonkeyTickSound == true;

            // Throttle sounds to avoid overwhelming audio feedback
            var now = DateTime.UtcNow;
            if ((now - _lastSoundTime).TotalMilliseconds < SoundThrottleMs)
                return;

            // Only play if value actually changed by a meaningful amount
            if (!useMonkeySound && _lastSoundValue >= 0 && Math.Abs(newValue - _lastSoundValue) < 1)
                return;

            _lastSoundTime = now;
            _lastSoundValue = newValue;

            try
            {
                if (useMonkeySound)
                {
                    MonkeySoundPlayer.Play(newValue);
                    return;
                }

                // Played from a dedicated thread: the first tick no longer stalls the drag.
                TickSoundPlayer.Play(newValue);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"VolumeSlider: Failed to play tick sound — {ex.Message}");
            }
        }

    }
}
