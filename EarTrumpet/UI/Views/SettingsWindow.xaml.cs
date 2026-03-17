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

        /// <summary>
        /// Handle click on theme cards to apply the theme
        /// </summary>
        private void ThemeCard_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.Tag is ColorTheme theme)
            {
                var viewModel = DataContext as SettingsViewModel;
                if (viewModel?.Selected?.Selected is EarTrumpetColorsSettingsPageViewModel colorsVm)
                {
                    colorsVm.SelectedTheme = theme;
                }
            }
            e.Handled = true;
        }

        /// <summary>
        /// Handle click on reset button (custom styled border instead of Button)
        /// </summary>
        private void ResetButton_Click(object sender, MouseButtonEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                Properties.Resources.SettingsResetConfirm, Properties.Resources.SettingsResetTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                var viewModel = DataContext as SettingsViewModel;
                if (viewModel?.Selected?.Selected is EarTrumpetColorsSettingsPageViewModel colorsVm)
                {
                    if (colorsVm.ResetToDefaultCommand?.CanExecute(null) == true)
                    {
                        colorsVm.ResetToDefaultCommand.Execute(null);
                    }
                }
            }
            e.Handled = true;
        }

        /// <summary>
        /// Handle save custom theme
        /// </summary>
        private void SaveCustomTheme_Click(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetColorsSettingsPageViewModel colorsVm)
            {
                colorsVm.SaveCustomThemeCommand?.Execute(null);
            }
            e.Handled = true;
        }

        /// <summary>
        /// Handle delete custom theme
        /// </summary>
        private void DeleteCustomTheme_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.Tag is ColorTheme theme)
            {
                var result = System.Windows.MessageBox.Show(
                    string.Format(Properties.Resources.SettingsDeleteConfirm, theme.Name), Properties.Resources.SettingsDeleteTitle,
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    var viewModel = DataContext as SettingsViewModel;
                    if (viewModel?.Selected?.Selected is EarTrumpetColorsSettingsPageViewModel colorsVm)
                    {
                        colorsVm.DeleteCustomThemeCommand?.Execute(theme);
                    }
                }
            }
            e.Handled = true;
        }

        /// <summary>
        /// Handle export theme to clipboard
        /// </summary>
        private void ExportTheme_Click(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetColorsSettingsPageViewModel colorsVm)
            {
                colorsVm.ExportThemeCommand?.Execute(null);
            }
            e.Handled = true;
        }

        /// <summary>
        /// Handle import theme from clipboard
        /// </summary>
        private void ImportTheme_Click(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetColorsSettingsPageViewModel colorsVm)
            {
                colorsVm.ImportThemeCommand?.Execute(null);
            }
            e.Handled = true;
        }

        /// <summary>
        /// Handle export theme to .bttheme file
        /// </summary>
        private void ExportThemeToFile_Click(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetColorsSettingsPageViewModel colorsVm)
            {
                colorsVm.ExportThemeToFileCommand?.Execute(null);
            }
            e.Handled = true;
        }

        /// <summary>
        /// Handle import theme from .bttheme file
        /// </summary>
        private void ImportThemeFromFile_Click(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetColorsSettingsPageViewModel colorsVm)
            {
                colorsVm.ImportThemeFromFileCommand?.Execute(null);
            }
            e.Handled = true;
        }

        // ═══════════════════════════════════
        // Volume Profile handlers
        // ═══════════════════════════════════

        private void SaveProfile_Click(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetVolumeProfilesSettingsPageViewModel profilesVm)
            {
                profilesVm.SaveCurrentCommand?.Execute(null);
            }
            e.Handled = true;
        }

        private void ApplyProfile_Click(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetVolumeProfilesSettingsPageViewModel profilesVm)
            {
                profilesVm.ApplyProfileCommand?.Execute(null);
            }
            e.Handled = true;
        }

        private void DeleteProfile_Click(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetVolumeProfilesSettingsPageViewModel profilesVm)
            {
                profilesVm.DeleteProfileCommand?.Execute(null);
            }
            e.Handled = true;
        }

        private void ExportProfile_Click(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetVolumeProfilesSettingsPageViewModel profilesVm)
            {
                profilesVm.ExportProfileCommand?.Execute(null);
            }
            e.Handled = true;
        }

        private void ImportProfile_Click(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetVolumeProfilesSettingsPageViewModel profilesVm)
            {
                profilesVm.ImportProfileCommand?.Execute(null);
            }
            e.Handled = true;
        }

        // ═══════════════════════════════════
        // Appearance tab switching
        // ═══════════════════════════════════

        private void AppearanceTab0_Click(object sender, MouseButtonEventArgs e)
        {
            SetAppearanceTab(0);
            e.Handled = true;
        }

        private void AppearanceTab1_Click(object sender, MouseButtonEventArgs e)
        {
            SetAppearanceTab(1);
            e.Handled = true;
        }

        private void AppearanceTab2_Click(object sender, MouseButtonEventArgs e)
        {
            SetAppearanceTab(2);
            e.Handled = true;
        }

        private void SetAppearanceTab(int tab)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetColorsSettingsPageViewModel colorsVm)
            {
                // Auto-enable custom colors when clicking any tab
                if (!colorsVm.UseCustomSliderColors)
                {
                    colorsVm.UseCustomSliderColors = true;
                }
                colorsVm.SelectedTab = tab;
            }
        }

        // ═══════════════════════════════════
        // Appearance category switching
        // ═══════════════════════════════════

        private void CategoryAll_Click(object sender, MouseButtonEventArgs e)
        {
            SetAppearanceCategory("All");
            e.Handled = true;
        }

        private void CategoryMinimal_Click(object sender, MouseButtonEventArgs e)
        {
            SetAppearanceCategory("Minimal");
            e.Handled = true;
        }

        private void CategoryBrands_Click(object sender, MouseButtonEventArgs e)
        {
            SetAppearanceCategory("Brand");
            e.Handled = true;
        }

        private void CategoryRetro_Click(object sender, MouseButtonEventArgs e)
        {
            SetAppearanceCategory("Retro / Gaming");
            e.Handled = true;
        }

        private void CategoryDev_Click(object sender, MouseButtonEventArgs e)
        {
            SetAppearanceCategory("Developer");
            e.Handled = true;
        }

        private void CategoryNature_Click(object sender, MouseButtonEventArgs e)
        {
            SetAppearanceCategory("Nature / Premium");
            e.Handled = true;
        }

        private void CategoryAccessibility_Click(object sender, MouseButtonEventArgs e)
        {
            SetAppearanceCategory("Accessibility");
            e.Handled = true;
        }

        private void SetAppearanceCategory(string category)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetColorsSettingsPageViewModel colorsVm)
            {
                colorsVm.SelectedCategory = category;
            }
        }

        // ═══════════════════════════════════
        // Randomize colors
        // ═══════════════════════════════════

        private void RandomizeColors_Click(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetColorsSettingsPageViewModel colorsVm)
            {
                colorsVm.RandomizeColorsCommand?.Execute(null);
            }
            e.Handled = true;
        }

        // ═══════════════════════════════════
        // Overflow menu
        // ═══════════════════════════════════

        private void OverflowMenu_Click(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetColorsSettingsPageViewModel colorsVm)
            {
                colorsVm.IsOverflowOpen = !colorsVm.IsOverflowOpen;
            }
            e.Handled = true;
        }

        /// <summary>
        /// Handle FPS card clicks
        /// </summary>
        private void Fps5Card_Click(object sender, MouseButtonEventArgs e)
        {
            SetPeakMeterFps(5);
            e.Handled = true;
        }

        private void Fps20Card_Click(object sender, MouseButtonEventArgs e)
        {
            SetPeakMeterFps(20);
            e.Handled = true;
        }

        private void Fps30Card_Click(object sender, MouseButtonEventArgs e)
        {
            SetPeakMeterFps(30);
            e.Handled = true;
        }

        private void Fps60Card_Click(object sender, MouseButtonEventArgs e)
        {
            SetPeakMeterFps(60);
            e.Handled = true;
        }

        private void SetPeakMeterFps(int fps)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetAnimationSettingsPageViewModel animVm)
            {
                animVm.PeakMeterFps = fps;
            }
        }

        /// <summary>
        /// Handle Peak Meter Style card clicks
        /// </summary>
        private void PeakStyleClassic_Click(object sender, MouseButtonEventArgs e)
        {
            SetPeakMeterStyle(0);
            e.Handled = true;
        }

        private void PeakStyleDotted_Click(object sender, MouseButtonEventArgs e)
        {
            SetPeakMeterStyle(1);
            e.Handled = true;
        }

        private void PeakStyleBlocks_Click(object sender, MouseButtonEventArgs e)
        {
            SetPeakMeterStyle(2);
            e.Handled = true;
        }

        private void PeakStyleBars_Click(object sender, MouseButtonEventArgs e)
        {
            SetPeakMeterStyle(3);
            e.Handled = true;
        }

        private void PeakStyleWave_Click(object sender, MouseButtonEventArgs e)
        {
            SetPeakMeterStyle(4);
            e.Handled = true;
        }

        private void SetPeakMeterStyle(int styleIndex)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetColorsSettingsPageViewModel colorsVm)
            {
                colorsVm.PeakMeterStyleIndex = styleIndex;
            }
        }

        private void ExportSettings_Click(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetAboutPageViewModel aboutVm)
            {
                aboutVm.ExportSettingsCommand?.Execute(null);
            }
            e.Handled = true;
        }

        private void ImportSettings_Click(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetAboutPageViewModel aboutVm)
            {
                aboutVm.ImportSettingsCommand?.Execute(null);
            }
            e.Handled = true;
        }

        private void CheckUpdate_Click(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetAboutPageViewModel aboutVm)
            {
                aboutVm.CheckForUpdateCommand?.Execute(null);
            }
            e.Handled = true;
        }

        private void InstallUpdate_Click(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetAboutPageViewModel aboutVm)
            {
                aboutVm.DownloadAndInstall();
            }
            e.Handled = true;
        }

        private void UpdateChannel0_Click(object sender, MouseButtonEventArgs e) { SetUpdateChannel(0); e.Handled = true; }
        private void UpdateChannel1_Click(object sender, MouseButtonEventArgs e) { SetUpdateChannel(1); e.Handled = true; }
        private void UpdateChannel2_Click(object sender, MouseButtonEventArgs e) { SetUpdateChannel(2); e.Handled = true; }
        private void UpdateChannel3_Click(object sender, MouseButtonEventArgs e) { SetUpdateChannel(3); e.Handled = true; }

        private void SetUpdateChannel(int index)
        {
            var viewModel = DataContext as SettingsViewModel;
            if (viewModel?.Selected?.Selected is EarTrumpetAboutPageViewModel aboutVm)
            {
                aboutVm.UpdateChannelIndex = index;
            }
        }
    }
}
