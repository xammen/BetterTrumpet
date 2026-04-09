using System;
using EarTrumpet.Extensions;
using EarTrumpet.UI.ViewModels;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EarTrumpet.UI.Views
{
    public partial class AppItemView : UserControl
    {
        private IAppItemViewModel App => (IAppItemViewModel)DataContext;

        public AppItemView()
        {
            InitializeComponent();

            PreviewMouseRightButtonUp += (_, __) => OpenPopup();

            Loaded += (_, __) =>
            {
                var container = this.FindVisualParent<ListViewItem>();
                if (container != null)
                {
                    container.PreviewKeyDown += OnPreviewKeyDown;
                }

                SubscribeToTooltipSettings();
                UpdateTooltipVisibility();
            };

            Unloaded += (_, __) => UnsubscribeFromTooltipSettings();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.M:
                case Key.OemPeriod:
                    App.IsMuted = !App.IsMuted;
                    e.Handled = true;
                    break;
                case Key.Right:
                case Key.OemPlus:
                    App.Volume++;
                    e.Handled = true;
                    break;
                case Key.Left:
                case Key.OemMinus:
                    App.Volume--;
                    e.Handled = true;
                    break;
                case Key.Space:
                    OpenPopup();
                    e.Handled = true;
                    break;
            }
        }

        private void OpenPopup()
        {
            var viewModel = Window.GetWindow(this).DataContext as IPopupHostViewModel;
            if (viewModel != null && App != null && !App.IsExpanded)
            {
                viewModel.OpenPopup(App, this);
            }
        }

        private void SubscribeToTooltipSettings()
        {
            if (EarTrumpet.App.Settings != null)
            {
                EarTrumpet.App.Settings.AppTooltipsChanged += OnAppTooltipsChanged;
            }
        }

        private void UnsubscribeFromTooltipSettings()
        {
            if (EarTrumpet.App.Settings != null)
            {
                EarTrumpet.App.Settings.AppTooltipsChanged -= OnAppTooltipsChanged;
            }
        }

        private void OnAppTooltipsChanged()
        {
            Dispatcher.BeginInvoke((Action)UpdateTooltipVisibility);
        }

        private void UpdateTooltipVisibility()
        {
            ToolTipService.SetIsEnabled(MuteButton, EarTrumpet.App.Settings?.ShowAppTooltips ?? true);
        }

        private void MuteButton_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            var flyoutWindow = Window.GetWindow(this) as FlyoutWindow;
            if (flyoutWindow != null)
            {
                flyoutWindow.Topmost = true;
            }
        }

        private void MuteButton_ToolTipClosing(object sender, ToolTipEventArgs e)
        {
            var flyoutWindow = Window.GetWindow(this) as FlyoutWindow;
            if (flyoutWindow != null)
            {
                var flyoutViewModel = flyoutWindow.DataContext as FlyoutViewModel;
                flyoutWindow.Topmost = flyoutViewModel != null && flyoutViewModel.IsPinned;
            }
        }

        private void MuteButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle)
            {
                return;
            }

            var betterTrumpetApp = Application.Current as EarTrumpet.App;
            if (betterTrumpetApp?.CollectionViewModel?.CanHideApp(App) == true)
            {
                betterTrumpetApp.CollectionViewModel.HideAppOnDevice(App);
                e.Handled = true;
            }
        }

        private void GridRoot_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var flyoutWindow = Window.GetWindow(this);
            if (App == null || IsPointerOverVolumeSlider(e) || flyoutWindow == null || !flyoutWindow.IsVisible || !flyoutWindow.IsActive)
            {
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                return;
            }
            if (!(App.Parent is DeviceViewModel))
            {
                return;
            }

            ToggleSoloMuteOnDevice();

            App.IsMuted = false;
            e.Handled = true;
        }

        private void ToggleSoloMuteOnDevice()
        {
            if (!(App.Parent is DeviceViewModel parentDevice))
            {
                return;
            }

            var siblings = parentDevice.Apps.Where(x => x != null).ToList();
            var otherApps = siblings.Where(x => x.Id != App.Id).ToList();

            var isSoloStateForCurrentApp = !App.IsMuted && otherApps.All(x => x.IsMuted);
            if (isSoloStateForCurrentApp)
            {
                foreach (var app in otherApps)
                {
                    app.IsMuted = false;
                }

                App.IsMuted = false;
                return;
            }

            foreach (var app in otherApps)
            {
                app.IsMuted = true;
            }

            App.IsMuted = false;
        }

        private bool IsPointerOverVolumeSlider(MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject original)
            {
                return original.FindVisualParent<EarTrumpet.UI.Controls.VolumeSlider>() != null;
            }

            return false;
        }
    }
}
