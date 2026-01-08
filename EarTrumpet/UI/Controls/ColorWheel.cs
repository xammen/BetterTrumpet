using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EarTrumpet.UI.Controls
{
    /// <summary>
    /// An HSV color wheel control for selecting colors visually.
    /// Hue is selected by angle, saturation by distance from center.
    /// </summary>
    public class ColorWheel : FrameworkElement
    {
        #region Dependency Properties
        
        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register("SelectedColor", typeof(Color), typeof(ColorWheel),
                new FrameworkPropertyMetadata(Colors.Red, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        public static readonly DependencyProperty HueProperty =
            DependencyProperty.Register("Hue", typeof(double), typeof(ColorWheel),
                new PropertyMetadata(0.0, OnHsvChanged));

        public double Hue
        {
            get => (double)GetValue(HueProperty);
            set => SetValue(HueProperty, value);
        }

        public static readonly DependencyProperty SaturationProperty =
            DependencyProperty.Register("Saturation", typeof(double), typeof(ColorWheel),
                new PropertyMetadata(1.0, OnHsvChanged));

        public double Saturation
        {
            get => (double)GetValue(SaturationProperty);
            set => SetValue(SaturationProperty, value);
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(ColorWheel),
                new PropertyMetadata(1.0, OnHsvChanged));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        #endregion

        public event EventHandler ColorChanged;

        private DrawingVisual _wheelVisual;
        private DrawingVisual _selectorVisual;
        private DrawingVisual _centerPreviewVisual;
        private WriteableBitmap _wheelBitmap;
        private bool _isDragging;
        private bool _isUpdating;
        private double _wheelRadius;
        private Point _wheelCenter;
        private double _lastRenderedValue = -1; // Track when wheel needs redraw
        private int _lastRenderedSize = -1;

        public ColorWheel()
        {
            _wheelVisual = new DrawingVisual();
            _selectorVisual = new DrawingVisual();
            _centerPreviewVisual = new DrawingVisual();
            
            AddVisualChild(_wheelVisual);
            AddVisualChild(_centerPreviewVisual);
            AddVisualChild(_selectorVisual);
            
            Width = 180;
            Height = 180;
            
            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
        }

        protected override int VisualChildrenCount => 3;

        protected override Visual GetVisualChild(int index)
        {
            if (index == 0) return _wheelVisual;
            if (index == 1) return _centerPreviewVisual;
            if (index == 2) return _selectorVisual;
            throw new ArgumentOutOfRangeException();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RenderWheelIfNeeded(force: true);
            UpdateSelectorPosition();
            UpdateCenterPreview();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderWheelIfNeeded(force: true);
            UpdateSelectorPosition();
            UpdateCenterPreview();
        }

        /// <summary>
        /// Only re-renders the wheel bitmap if Value or size changed.
        /// This is the expensive operation we want to minimize.
        /// </summary>
        private void RenderWheelIfNeeded(bool force = false)
        {
            int size = (int)Math.Min(ActualWidth, ActualHeight);
            if (size <= 0) size = 180;
            
            // Check if we actually need to re-render
            bool needsRender = force || 
                               Math.Abs(_lastRenderedValue - Value) > 0.001 || 
                               _lastRenderedSize != size;
            
            if (!needsRender) return;
            
            _lastRenderedValue = Value;
            _lastRenderedSize = size;
            
            _wheelRadius = size / 2.0 - 10; // Padding for selector
            _wheelCenter = new Point(size / 2.0, size / 2.0);
            
            // Create bitmap for wheel
            _wheelBitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
            byte[] pixels = new byte[size * size * 4];
            
            int stride = size * 4;
            double centerX = size / 2.0;
            double centerY = size / 2.0;
            double currentValue = Value;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int index = (y * size + x) * 4;
                    
                    double dx = x - centerX;
                    double dy = y - centerY;
                    double distance = Math.Sqrt(dx * dx + dy * dy);
                    
                    if (distance <= _wheelRadius)
                    {
                        // Calculate hue from angle
                        double angle = Math.Atan2(dy, dx);
                        double hue = (angle + Math.PI) / (2 * Math.PI) * 360; // 0-360
                        
                        // Calculate saturation from distance
                        double sat = distance / _wheelRadius;
                        
                        // Convert HSV to RGB
                        Color rgb = HsvToRgb(hue, sat, currentValue);
                        
                        pixels[index] = rgb.B;     // Blue
                        pixels[index + 1] = rgb.G; // Green
                        pixels[index + 2] = rgb.R; // Red
                        pixels[index + 3] = 255;   // Alpha
                    }
                    else if (distance <= _wheelRadius + 2)
                    {
                        // Anti-aliased edge
                        double alpha = Math.Max(0, 1 - (distance - _wheelRadius) / 2);
                        
                        double angle = Math.Atan2(dy, dx);
                        double hue = (angle + Math.PI) / (2 * Math.PI) * 360;
                        Color rgb = HsvToRgb(hue, 1.0, currentValue);
                        
                        pixels[index] = rgb.B;
                        pixels[index + 1] = rgb.G;
                        pixels[index + 2] = rgb.R;
                        pixels[index + 3] = (byte)(255 * alpha);
                    }
                    else
                    {
                        // Transparent outside
                        pixels[index] = 0;
                        pixels[index + 1] = 0;
                        pixels[index + 2] = 0;
                        pixels[index + 3] = 0;
                    }
                }
            }
            
            _wheelBitmap.WritePixels(new Int32Rect(0, 0, size, size), pixels, stride, 0);
            
            // Draw wheel bitmap to visual (just the wheel, no preview)
            using (var dc = _wheelVisual.RenderOpen())
            {
                dc.DrawImage(_wheelBitmap, new Rect(0, 0, size, size));
            }
        }

        /// <summary>
        /// Updates only the center preview circle - very cheap operation.
        /// </summary>
        private void UpdateCenterPreview()
        {
            if (_wheelRadius <= 0) return;
            
            using (var dc = _centerPreviewVisual.RenderOpen())
            {
                dc.DrawEllipse(
                    new SolidColorBrush(SelectedColor),
                    new Pen(new SolidColorBrush(Color.FromRgb(80, 80, 80)), 2),
                    _wheelCenter,
                    18, 18);
            }
        }

        private void UpdateSelectorPosition()
        {
            if (_wheelRadius <= 0) return;
            
            // Convert HSV to position
            double angle = Hue / 360.0 * 2 * Math.PI - Math.PI;
            double distance = Saturation * _wheelRadius;
            
            double x = _wheelCenter.X + Math.Cos(angle) * distance;
            double y = _wheelCenter.Y + Math.Sin(angle) * distance;
            
            using (var dc = _selectorVisual.RenderOpen())
            {
                // Outer ring (white)
                dc.DrawEllipse(
                    null,
                    new Pen(Brushes.White, 3),
                    new Point(x, y),
                    8, 8);
                
                // Inner fill with selected color
                dc.DrawEllipse(
                    new SolidColorBrush(SelectedColor),
                    new Pen(new SolidColorBrush(Color.FromRgb(60, 60, 60)), 1),
                    new Point(x, y),
                    5, 5);
            }
            
            // Update center preview (cheap operation)
            UpdateCenterPreview();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            
            var pos = e.GetPosition(this);
            double dx = pos.X - _wheelCenter.X;
            double dy = pos.Y - _wheelCenter.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            
            // Only capture if clicking inside the wheel (not center preview)
            if (distance <= _wheelRadius && distance > 20)
            {
                _isDragging = true;
                CaptureMouse();
                UpdateColorFromPoint(pos);
                e.Handled = true;
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            
            if (_isDragging)
            {
                UpdateColorFromPoint(e.GetPosition(this));
                e.Handled = true;
            }
        }

        private void UpdateColorFromPoint(Point pos)
        {
            double dx = pos.X - _wheelCenter.X;
            double dy = pos.Y - _wheelCenter.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            
            // Clamp to wheel radius
            if (distance > _wheelRadius)
            {
                double scale = _wheelRadius / distance;
                dx *= scale;
                dy *= scale;
                distance = _wheelRadius;
            }
            
            // Calculate HSV
            double angle = Math.Atan2(dy, dx);
            double hue = (angle + Math.PI) / (2 * Math.PI) * 360;
            double sat = distance / _wheelRadius;
            
            _isUpdating = true;
            Hue = hue;
            Saturation = sat;
            SelectedColor = HsvToRgb(hue, sat, Value);
            _isUpdating = false;
            
            UpdateSelectorPosition();
            ColorChanged?.Invoke(this, EventArgs.Empty);
        }

        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var wheel = (ColorWheel)d;
            if (wheel._isUpdating) return;
            
            var color = (Color)e.NewValue;
            HsvColor hsv = RgbToHsv(color);
            
            wheel._isUpdating = true;
            wheel.Hue = hsv.H;
            wheel.Saturation = hsv.S;
            wheel.Value = hsv.V;
            wheel._isUpdating = false;
            
            // Only re-render wheel if Value changed (brightness)
            wheel.RenderWheelIfNeeded();
            wheel.UpdateSelectorPosition();
        }

        private static void OnHsvChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var wheel = (ColorWheel)d;
            if (wheel._isUpdating) return;
            
            wheel._isUpdating = true;
            wheel.SelectedColor = HsvToRgb(wheel.Hue, wheel.Saturation, wheel.Value);
            wheel._isUpdating = false;
            
            // Only re-render wheel if Value changed (brightness)
            wheel.RenderWheelIfNeeded();
            wheel.UpdateSelectorPosition();
            wheel.ColorChanged?.Invoke(wheel, EventArgs.Empty);
        }

        #region Color Conversion

        // Simple struct to avoid ValueTuple (not available in .NET 4.6.2)
        private struct HsvColor
        {
            public double H;
            public double S;
            public double V;
        }

        private static Color HsvToRgb(double h, double s, double v)
        {
            h = h % 360;
            if (h < 0) h += 360;
            
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c;
            
            double r, g, b;
            
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }
            
            return Color.FromRgb(
                (byte)((r + m) * 255),
                (byte)((g + m) * 255),
                (byte)((b + m) * 255)
            );
        }

        private static HsvColor RgbToHsv(Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;
            
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;
            
            double h = 0;
            double s = max == 0 ? 0 : delta / max;
            double v = max;
            
            if (delta != 0)
            {
                if (max == r)
                    h = 60 * (((g - b) / delta) % 6);
                else if (max == g)
                    h = 60 * ((b - r) / delta + 2);
                else
                    h = 60 * ((r - g) / delta + 4);
            }
            
            if (h < 0) h += 360;
            
            return new HsvColor { H = h, S = s, V = v };
        }

        #endregion
    }
}
