using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EarTrumpet.UI.Controls
{
    public class VolumeSlider : Slider
    {
        // Smoothing factor for peak meter: higher = faster response, lower = smoother (0.0 - 1.0)
        private const double PeakSmoothingFactor = 0.15;
        
        // Default smoothing factor for volume slider animation when clicking on track
        // Lower = slower/smoother animation, Higher = faster (0.0 - 1.0)
        private const double DefaultVolumeSmoothingFactor = 0.08;
        
        // Get the smoothing factor from settings (or use default)
        private double VolumeSmoothingFactor => App.Settings?.VolumeAnimationSpeed ?? DefaultVolumeSmoothingFactor;
        
        // Check if smooth animation is enabled in settings
        private bool IsSmoothAnimationEnabled => App.Settings?.UseSmoothVolumeAnimation ?? true;
        
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
        private RepeatButton _sliderLeft;
        private RepeatButton _sliderRight;
        private Point _lastMousePosition;
        
        // Smooth animation state for peak meters
        private double _currentWidth1;
        private double _currentWidth2;
        private double _targetWidth1;
        private double _targetWidth2;
        private bool _isAnimating;
        
        // Smooth animation state for volume slider
        private double _targetValue;
        private bool _isAnimatingValue;
        private bool _isDragging;
        private bool _clickedOnTrack; // Track if initial click was on track (not thumb)
        
        // FPS limiting for eco mode
        private DateTime _lastFrameTime = DateTime.MinValue;
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
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _thumb = (Thumb)GetTemplateChild("SliderThumb");
            _peakMeter1 = (Border)GetTemplateChild("PeakMeter1");
            _peakMeter2 = (Border)GetTemplateChild("PeakMeter2");
            _sliderLeft = (RepeatButton)GetTemplateChild("SliderLeft");
            _sliderRight = (RepeatButton)GetTemplateChild("SliderRight");
            
            // Initialize current widths
            _currentWidth1 = 0;
            _currentWidth2 = 0;
            
            // Apply custom colors if enabled
            ApplyCustomColors();
            
            // Subscribe to settings changes for live preview
            if (App.Settings != null)
            {
                App.Settings.CustomSliderColorsChanged += OnCustomSliderColorsChanged;
                App.Settings.EcoModeChanged += OnEcoModeChanged;
            }
            
            // Initialize FPS limiting
            UpdateTargetFps();
            
            // Start the render loop for smooth animation
            StartAnimation();
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
        
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopAnimation();
            
            // Unsubscribe from settings changes
            if (App.Settings != null)
            {
                App.Settings.CustomSliderColorsChanged -= OnCustomSliderColorsChanged;
                App.Settings.EcoModeChanged -= OnEcoModeChanged;
            }
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
                // Set custom brushes via DependencyProperties
                var thumbColor = settings.SliderThumbColor;
                CustomThumbBrush = thumbColor != Colors.Transparent ? new SolidColorBrush(thumbColor) : null;
                
                var trackFillColor = settings.SliderTrackFillColor;
                CustomTrackFillBrush = trackFillColor != Colors.Transparent ? new SolidColorBrush(trackFillColor) : null;
                
                var trackBgColor = settings.SliderTrackBackgroundColor;
                CustomTrackBackgroundBrush = trackBgColor != Colors.Transparent ? new SolidColorBrush(trackBgColor) : null;
                
                var peakColor = settings.PeakMeterColor;
                CustomPeakMeterBrush = peakColor != Colors.Transparent ? new SolidColorBrush(peakColor) : null;
                
                // Apply colors directly to visual elements (bypassing theme system)
                ApplyColorsToVisualElements();
            }
            else
            {
                // Clear custom brushes
                CustomThumbBrush = null;
                CustomTrackFillBrush = null;
                CustomTrackBackgroundBrush = null;
                CustomPeakMeterBrush = null;
                
                // Reset visual elements to use theme colors
                ResetVisualElementColors();
            }
        }
        
        private void ApplyColorsToVisualElements()
        {
            // Apply thumb color directly
            if (_thumb != null && CustomThumbBrush != null)
            {
                _thumb.Foreground = CustomThumbBrush;
            }
            
            // Apply track fill color (left part)
            if (_sliderLeft != null && CustomTrackFillBrush != null)
            {
                _sliderLeft.Foreground = CustomTrackFillBrush;
            }
            
            // Apply track background color (right part)
            if (_sliderRight != null && CustomTrackBackgroundBrush != null)
            {
                _sliderRight.Foreground = CustomTrackBackgroundBrush;
            }
            
            // Apply peak meter color
            if (_peakMeter1 != null && CustomPeakMeterBrush != null)
            {
                _peakMeter1.Background = CustomPeakMeterBrush;
            }
            if (_peakMeter2 != null && CustomPeakMeterBrush != null)
            {
                _peakMeter2.Background = CustomPeakMeterBrush;
            }
        }
        
        private void ResetVisualElementColors()
        {
            // Reset to default by clearing local values - let theme system take over
            if (_thumb != null)
            {
                _thumb.ClearValue(Control.ForegroundProperty);
            }
            if (_sliderLeft != null)
            {
                _sliderLeft.ClearValue(Control.ForegroundProperty);
            }
            if (_sliderRight != null)
            {
                _sliderRight.ClearValue(Control.ForegroundProperty);
            }
            if (_peakMeter1 != null)
            {
                _peakMeter1.ClearValue(Border.BackgroundProperty);
            }
            if (_peakMeter2 != null)
            {
                _peakMeter2.ClearValue(Border.BackgroundProperty);
            }
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
        
        private void OnRendering(object sender, EventArgs e)
        {
            // FPS limiting: skip frames if we're updating too fast
            var now = DateTime.Now;
            var elapsed = (now - _lastFrameTime).TotalMilliseconds;
            
            // For peak meter updates, respect FPS limit
            // But always process volume animation for responsiveness
            bool shouldUpdatePeakMeter = elapsed >= _frameInterval;
            
            if (shouldUpdatePeakMeter)
            {
                _lastFrameTime = now;
                
                // Lerp current values toward target values for peak meters
                _currentWidth1 = Lerp(_currentWidth1, _targetWidth1, PeakSmoothingFactor);
                _currentWidth2 = Lerp(_currentWidth2, _targetWidth2, PeakSmoothingFactor);
                
                // Apply smoothed values
                if (_peakMeter1 != null)
                {
                    _peakMeter1.Width = Math.Max(0, _currentWidth1);
                }
                
                if (_peakMeter2 != null)
                {
                    _peakMeter2.Width = Math.Max(0, _currentWidth2);
                }
            }
            
            // Animate volume slider value when clicking on track (not dragging)
            // This always runs for responsive feel
            if (_isAnimatingValue && !_isDragging)
            {
                var newValue = Lerp(Value, _targetValue, VolumeSmoothingFactor);
                
                // Stop animating when close enough to target
                if (Math.Abs(newValue - _targetValue) < 0.5)
                {
                    Value = _targetValue;
                    _isAnimatingValue = false;
                }
                else
                {
                    Value = newValue;
                }
            }
            
            // Force custom colors every frame to override theme system hover effects
            if (CustomThumbBrush != null && _thumb != null && _thumb.Foreground != CustomThumbBrush)
            {
                _thumb.Foreground = CustomThumbBrush;
            }
        }
        
        private static double Lerp(double current, double target, double factor)
        {
            return current + (target - current) * factor;
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
            
            // Touch down on track - animate smoothly
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
                // Otherwise (clicked on track, or thumb not found), animate smoothly
                if (_thumb != null && _thumb.IsMouseOver)
                {
                    // Click on thumb - start dragging immediately
                    _clickedOnTrack = false;
                    _isDragging = true;
                }
                else
                {
                    // Click on track (or thumb not found) - animate smoothly to target
                    _clickedOnTrack = true;
                    _isDragging = false;
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
                    // User clicked on track - they're now dragging after the initial click
                    // Stop animation and switch to instant updates
                    _clickedOnTrack = false;
                    _isDragging = true;
                    _isAnimatingValue = false;
                }
                
                if (_isDragging)
                {
                    // When dragging, we want instant updates (no animation)
                    SetPositionByControlPoint(mousePosition, animate: false);
                }
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var amount = Math.Sign(e.Delta) * 2.0;
            ChangePositionByAmount(amount);
            e.Handled = true;
        }

        public void SetPositionByControlPoint(Point point, bool animate = false)
        {
            var percent = point.X / ActualWidth;
            var newValue = Bound((Maximum - Minimum) * percent);
            
            // Only animate if requested AND smooth animation is enabled in settings
            if (animate && IsSmoothAnimationEnabled)
            {
                // Ensure animation loop is running
                StartAnimation();
                
                // Smooth animation to target value
                _targetValue = newValue;
                _isAnimatingValue = true;
            }
            else
            {
                // Instant update (for dragging or when animation is disabled)
                Value = newValue;
            }
        }

        public void ChangePositionByAmount(double amount)
        {
            Value = Bound(Value + amount);
        }

        public double Bound(double val)
        {
            return Math.Max(Minimum, Math.Min(Maximum, val));
        }
    }
}
