using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace EarTrumpet.UI.Controls
{
    public class VolumeSlider : Slider
    {
        // Smoothing factor: higher = faster response, lower = smoother (0.0 - 1.0)
        private const double SmoothingFactor = 0.15;
        
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

        private Border _peakMeter1;
        private Border _peakMeter2;
        private Thumb _thumb;
        private Point _lastMousePosition;
        
        // Smooth animation state
        private double _currentWidth1;
        private double _currentWidth2;
        private double _targetWidth1;
        private double _targetWidth2;
        private bool _isAnimating;

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
            
            // Initialize current widths
            _currentWidth1 = 0;
            _currentWidth2 = 0;
            
            // Start the render loop for smooth animation
            StartAnimation();
        }
        
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopAnimation();
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
            // Lerp current values toward target values
            _currentWidth1 = Lerp(_currentWidth1, _targetWidth1, SmoothingFactor);
            _currentWidth2 = Lerp(_currentWidth2, _targetWidth2, SmoothingFactor);
            
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

            SetPositionByControlPoint(e.GetTouchPoint(this).Position);
            CaptureTouch(e.TouchDevice);

            e.Handled = true;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _lastMousePosition = e.GetPosition(this);
                VisualStateManager.GoToState((FrameworkElement)sender, "Pressed", true);

                if (!_thumb.IsMouseOver)
                {
                    SetPositionByControlPoint(_lastMousePosition);
                }

                CaptureMouse();
                e.Handled = true;
            }
        }

        private void OnTouchUp(object sender, TouchEventArgs e)
        {
            VisualStateManager.GoToState((FrameworkElement)sender, "Normal", true);

            ReleaseTouchCapture(e.TouchDevice);
            e.Handled = true;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseCaptured)
            {
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
                SetPositionByControlPoint(e.GetTouchPoint(this).Position);
                e.Handled = true;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var mousePosition = e.GetPosition(this);
            if (IsMouseCaptured && mousePosition != _lastMousePosition)
            {
                _lastMousePosition = mousePosition;
                SetPositionByControlPoint(e.GetPosition(this));
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var amount = Math.Sign(e.Delta) * 2.0;
            ChangePositionByAmount(amount);
            e.Handled = true;
        }

        public void SetPositionByControlPoint(Point point)
        {
            var percent = point.X / ActualWidth;
            Value = Bound((Maximum - Minimum) * percent);
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
