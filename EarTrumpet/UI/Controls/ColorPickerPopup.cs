using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace EarTrumpet.UI.Controls
{
    /// <summary>
    /// A simple color picker popup control
    /// </summary>
    public class ColorPickerPopup : Popup
    {
        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register("SelectedColor", typeof(Color), typeof(ColorPickerPopup),
                new FrameworkPropertyMetadata(Colors.White, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        public event EventHandler ColorChanged;

        private Border _previewBorder;
        private Slider _redSlider;
        private Slider _greenSlider;
        private Slider _blueSlider;
        private TextBox _hexTextBox;
        private bool _isUpdating;

        // Predefined color palette
        private static readonly Color[] PaletteColors = new[]
        {
            // Row 1: Basic colors
            Color.FromRgb(255, 255, 255), // White
            Color.FromRgb(192, 192, 192), // Silver
            Color.FromRgb(128, 128, 128), // Gray
            Color.FromRgb(64, 64, 64),    // Dark Gray
            Color.FromRgb(0, 0, 0),       // Black
            Color.FromRgb(255, 0, 0),     // Red
            Color.FromRgb(255, 128, 0),   // Orange
            Color.FromRgb(255, 255, 0),   // Yellow
            
            // Row 2: Greens and Blues
            Color.FromRgb(128, 255, 0),   // Lime
            Color.FromRgb(0, 255, 0),     // Green
            Color.FromRgb(0, 255, 128),   // Spring
            Color.FromRgb(0, 255, 255),   // Cyan
            Color.FromRgb(0, 128, 255),   // Sky Blue
            Color.FromRgb(0, 0, 255),     // Blue
            Color.FromRgb(128, 0, 255),   // Purple
            Color.FromRgb(255, 0, 255),   // Magenta
            
            // Row 3: Darker variants
            Color.FromRgb(128, 0, 0),     // Maroon
            Color.FromRgb(128, 64, 0),    // Brown
            Color.FromRgb(128, 128, 0),   // Olive
            Color.FromRgb(0, 128, 0),     // Dark Green
            Color.FromRgb(0, 128, 128),   // Teal
            Color.FromRgb(0, 0, 128),     // Navy
            Color.FromRgb(128, 0, 128),   // Purple Dark
            Color.FromRgb(255, 0, 128),   // Pink
            
            // Row 4: Pastel variants
            Color.FromRgb(255, 192, 192), // Light Pink
            Color.FromRgb(255, 224, 192), // Peach
            Color.FromRgb(255, 255, 192), // Light Yellow
            Color.FromRgb(192, 255, 192), // Light Green
            Color.FromRgb(192, 255, 255), // Light Cyan
            Color.FromRgb(192, 192, 255), // Light Blue
            Color.FromRgb(255, 192, 255), // Light Magenta
            Color.FromRgb(16, 255, 16),   // Pip-Boy Green
        };

        public ColorPickerPopup()
        {
            StaysOpen = false;
            AllowsTransparency = true;
            PopupAnimation = PopupAnimation.Fade;
            Placement = PlacementMode.Bottom;

            CreateContent();
        }

        private void CreateContent()
        {
            var mainBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 10,
                    ShadowDepth = 3,
                    Opacity = 0.5
                }
            };

            var mainStack = new StackPanel { Orientation = Orientation.Vertical };

            // Title
            var title = new TextBlock
            {
                Text = "Pick a Color",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 12)
            };
            mainStack.Children.Add(title);

            // Color palette grid
            var paletteGrid = new UniformGrid
            {
                Columns = 8,
                Rows = 4,
                Width = 256,
                Height = 128
            };

            foreach (var color in PaletteColors)
            {
                var colorButton = new Border
                {
                    Width = 28,
                    Height = 28,
                    Margin = new Thickness(2),
                    Background = new SolidColorBrush(color),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Cursor = Cursors.Hand
                };
                colorButton.MouseLeftButtonDown += (s, e) =>
                {
                    SelectedColor = color;
                    e.Handled = true;
                };
                colorButton.MouseEnter += (s, e) =>
                {
                    ((Border)s).BorderBrush = Brushes.White;
                    ((Border)s).BorderThickness = new Thickness(2);
                };
                colorButton.MouseLeave += (s, e) =>
                {
                    ((Border)s).BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100));
                    ((Border)s).BorderThickness = new Thickness(1);
                };
                paletteGrid.Children.Add(colorButton);
            }
            mainStack.Children.Add(paletteGrid);

            // Separator
            mainStack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(80, 80, 80)), Margin = new Thickness(0, 12, 0, 12) });

            // Preview and sliders
            var controlsGrid = new Grid();
            controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Preview
            _previewBorder = new Border
            {
                Width = 50,
                Height = 50,
                CornerRadius = new CornerRadius(6),
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                BorderThickness = new Thickness(2),
                Background = new SolidColorBrush(SelectedColor)
            };
            Grid.SetColumn(_previewBorder, 0);
            controlsGrid.Children.Add(_previewBorder);

            // RGB Sliders
            var slidersStack = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(12, 0, 0, 0) };
            Grid.SetColumn(slidersStack, 1);

            // Red
            var redPanel = CreateSliderRow("R:", Brushes.Red, out _redSlider);
            _redSlider.ValueChanged += OnSliderValueChanged;
            slidersStack.Children.Add(redPanel);

            // Green
            var greenPanel = CreateSliderRow("G:", Brushes.LimeGreen, out _greenSlider);
            _greenSlider.ValueChanged += OnSliderValueChanged;
            slidersStack.Children.Add(greenPanel);

            // Blue
            var bluePanel = CreateSliderRow("B:", Brushes.DodgerBlue, out _blueSlider);
            _blueSlider.ValueChanged += OnSliderValueChanged;
            slidersStack.Children.Add(bluePanel);

            controlsGrid.Children.Add(slidersStack);
            mainStack.Children.Add(controlsGrid);

            // Hex input
            var hexPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            hexPanel.Children.Add(new TextBlock { Text = "Hex:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Width = 35 });
            _hexTextBox = new TextBox
            {
                Width = 80,
                Height = 24,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                Padding = new Thickness(4, 2, 4, 2),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _hexTextBox.LostFocus += OnHexTextBoxLostFocus;
            _hexTextBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) OnHexTextBoxLostFocus(s, e); };
            hexPanel.Children.Add(_hexTextBox);
            mainStack.Children.Add(hexPanel);

            mainBorder.Child = mainStack;
            Child = mainBorder;

            UpdateUIFromColor();
        }

        private StackPanel CreateSliderRow(string label, Brush accentColor, out Slider slider)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            panel.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = Brushes.White,
                Width = 20,
                VerticalAlignment = VerticalAlignment.Center
            });
            slider = new Slider
            {
                Minimum = 0,
                Maximum = 255,
                Width = 130,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(slider);
            return panel;
        }

        private void OnSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;
            
            var newColor = Color.FromRgb(
                (byte)_redSlider.Value,
                (byte)_greenSlider.Value,
                (byte)_blueSlider.Value
            );
            
            _isUpdating = true;
            SelectedColor = newColor;
            _isUpdating = false;
            
            UpdatePreviewAndHex();
        }

        private void OnHexTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            var hex = _hexTextBox.Text.TrimStart('#');
            if (hex.Length == 6)
            {
                try
                {
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    SelectedColor = Color.FromRgb(r, g, b);
                }
                catch { }
            }
        }

        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = (ColorPickerPopup)d;
            picker.UpdateUIFromColor();
            picker.ColorChanged?.Invoke(picker, EventArgs.Empty);
        }

        private void UpdateUIFromColor()
        {
            if (_redSlider == null) return;
            
            _isUpdating = true;
            _redSlider.Value = SelectedColor.R;
            _greenSlider.Value = SelectedColor.G;
            _blueSlider.Value = SelectedColor.B;
            _isUpdating = false;
            
            UpdatePreviewAndHex();
        }

        private void UpdatePreviewAndHex()
        {
            if (_previewBorder != null)
                _previewBorder.Background = new SolidColorBrush(SelectedColor);
            if (_hexTextBox != null)
                _hexTextBox.Text = $"#{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";
        }
    }
}
