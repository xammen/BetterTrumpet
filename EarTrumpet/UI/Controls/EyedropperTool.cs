using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EarTrumpet.UI.Controls
{
    /// <summary>
    /// An eyedropper/color picker tool that captures colors from anywhere on screen.
    /// Click and drag to sample colors, release to confirm selection.
    /// </summary>
    public class EyedropperTool : Window
    {
        #region P/Invoke
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }
        #endregion

        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register("SelectedColor", typeof(Color), typeof(EyedropperTool),
                new PropertyMetadata(Colors.Transparent));

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        public event EventHandler<Color> ColorPicked;
        public event EventHandler Cancelled;

        private readonly Border _previewBorder;
        private readonly TextBlock _hexText;
        private readonly DispatcherTimer _updateTimer;
        private bool _isCapturing;
        private Color _currentColor;

        public EyedropperTool()
        {
            // Window setup - fullscreen transparent overlay
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)); // Nearly transparent
            Topmost = true;
            ShowInTaskbar = false;
            Cursor = Cursors.Cross;

            // Cover all screens
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            // Create preview popup
            var previewPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                Margin = new Thickness(0)
            };

            _previewBorder = new Border
            {
                Width = 80,
                Height = 80,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                BorderThickness = new Thickness(2),
                Margin = new Thickness(8)
            };
            previewPanel.Children.Add(_previewBorder);

            _hexText = new TextBlock
            {
                Text = "#FFFFFF",
                Foreground = Brushes.White,
                FontSize = 12,
                FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };
            previewPanel.Children.Add(_hexText);

            var instructionText = new TextBlock
            {
                Text = "Click to pick • ESC to cancel",
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 8)
            };
            previewPanel.Children.Add(instructionText);

            var previewContainer = new Border
            {
                Child = previewPanel,
                CornerRadius = new CornerRadius(6),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 15,
                    ShadowDepth = 3,
                    Opacity = 0.6
                }
            };

            // Canvas to position preview
            var canvas = new Canvas();
            canvas.Children.Add(previewContainer);
            Content = canvas;

            // Update timer for real-time color preview
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60fps
            };
            _updateTimer.Tick += UpdateTimer_Tick;

            // Event handlers
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            KeyDown += OnKeyDown;

            Loaded += (s, e) =>
            {
                _updateTimer.Start();
                _isCapturing = true;
            };

            Closed += (s, e) =>
            {
                _updateTimer.Stop();
            };
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            if (!_isCapturing) return;

            if (GetCursorPos(out POINT point))
            {
                _currentColor = GetColorAtPoint(point.X, point.Y);
                UpdatePreview(_currentColor, point.X, point.Y);
            }
        }

        private Color GetColorAtPoint(int x, int y)
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            uint pixel = GetPixel(hdc, x, y);
            ReleaseDC(IntPtr.Zero, hdc);

            byte r = (byte)(pixel & 0xFF);
            byte g = (byte)((pixel >> 8) & 0xFF);
            byte b = (byte)((pixel >> 16) & 0xFF);

            return Color.FromRgb(r, g, b);
        }

        private void UpdatePreview(Color color, int screenX, int screenY)
        {
            _previewBorder.Background = new SolidColorBrush(color);
            _hexText.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

            // Position preview near cursor but offset to not cover sampling area
            var canvas = Content as Canvas;
            var preview = canvas?.Children[0] as Border;
            if (preview != null)
            {
                // Convert screen coordinates to window coordinates
                double localX = screenX - Left + 20;
                double localY = screenY - Top - 60;

                // Keep preview on screen
                if (localX + 100 > Width) localX = screenX - Left - 120;
                if (localY < 0) localY = screenY - Top + 20;

                Canvas.SetLeft(preview, localX);
                Canvas.SetTop(preview, localY);
            }
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // Color is already being tracked, just capture for final selection
                CaptureMouse();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            // Real-time update is handled by timer
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
            {
                _isCapturing = false;
                _updateTimer.Stop();
                ReleaseMouseCapture();
                
                SelectedColor = _currentColor;
                ColorPicked?.Invoke(this, _currentColor);
                Close();
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _isCapturing = false;
                _updateTimer.Stop();
                Cancelled?.Invoke(this, EventArgs.Empty);
                Close();
            }
        }

        /// <summary>
        /// Shows the eyedropper tool and returns the picked color.
        /// </summary>
        public static void PickColor(Action<Color> onColorPicked, Action onCancelled = null)
        {
            var eyedropper = new EyedropperTool();
            eyedropper.ColorPicked += (s, color) => onColorPicked?.Invoke(color);
            eyedropper.Cancelled += (s, e) => onCancelled?.Invoke();
            eyedropper.Show();
            eyedropper.Activate();
        }
    }
}
