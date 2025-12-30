using EarTrumpet.Extensions;
using EarTrumpet.Interop;
using EarTrumpet.Interop.Helpers;
using EarTrumpet.UI.Controls;
using EarTrumpet.UI.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace EarTrumpet.UI.Views
{
    public partial class SettingsWindow : Window
    {
        private ColorPickerPopup _colorPicker;
        private string _activeColorProperty;

        public SettingsWindow()
        {
            Trace.WriteLine("SettingsWindow .ctor");
            Closed += (_, __) => Trace.WriteLine("SettingsWindow Closed");

            InitializeComponent();

            SourceInitialized += (sender, __) =>
            {
                this.Cloak();
                this.EnableRoundedCornersIfApplicable();

                if (App.Settings.SettingsWindowPlacement != null)
                {
                    User32.SetWindowPlacement(new WindowInteropHelper((Window)sender).Handle, App.Settings.SettingsWindowPlacement.Value);
                }
            };

            StateChanged += OnWindowStateChanged;

            Closing += (sender, __) =>
            {
                if (User32.GetWindowPlacement(new WindowInteropHelper((Window)sender).Handle, out var placement))
                {
                    App.Settings.SettingsWindowPlacement = placement;
                }
            };
        }

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(this);
            chrome.ResizeBorderThickness = WindowState == WindowState.Maximized ? new Thickness(0) : SystemParameters.WindowResizeBorderThickness;

            if (WindowState == WindowState.Maximized)
            {
                WindowSizeHelper.RestrictMaximizedSizeToWorkArea(this);
            }
        }

        /// <summary>
        /// Handle click on color boxes to show the color picker popup
        /// </summary>
        private void ColorBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border == null) return;

            _activeColorProperty = border.Tag as string;
            if (string.IsNullOrEmpty(_activeColorProperty)) return;

            // Get current color from the border
            var brush = border.Background as SolidColorBrush;
            var currentColor = brush?.Color ?? Colors.White;

            // Create or reuse color picker
            if (_colorPicker == null)
            {
                _colorPicker = new ColorPickerPopup();
                _colorPicker.ColorChanged += ColorPicker_ColorChanged;
            }

            _colorPicker.SelectedColor = currentColor;
            _colorPicker.PlacementTarget = border;
            _colorPicker.IsOpen = true;

            e.Handled = true;
        }

        /// <summary>
        /// Apply the selected color to the appropriate property
        /// </summary>
        private void ColorPicker_ColorChanged(object sender, EventArgs e)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetColorsSettingsPageViewModel colorsVm)
            {
                colorsVm.ApplyPickedColor(_colorPicker.SelectedColor, _activeColorProperty);
            }
        }
    }
}
