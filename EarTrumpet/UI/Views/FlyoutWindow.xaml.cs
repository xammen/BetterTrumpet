using EarTrumpet.Extensions;
using EarTrumpet.Interop;
using EarTrumpet.Interop.Helpers;
using EarTrumpet.UI.Helpers;
using EarTrumpet.UI.ViewModels;
using System;
using System.Windows;

namespace EarTrumpet.UI.Views
{
    public partial class FlyoutWindow
    {
        private readonly IFlyoutViewModel _viewModel;

        public FlyoutWindow(IFlyoutViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = _viewModel;

            InitializeComponent();

            _viewModel.StateChanged += OnStateChanged;
            _viewModel.WindowSizeInvalidated += OnWindowsSizeInvalidated;

            // Sync Topmost with pin state
            if (_viewModel is FlyoutViewModel fvm)
            {
                fvm.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(FlyoutViewModel.IsPinned))
                    {
                        Topmost = fvm.IsPinned;
                    }
                };
            }

            SourceInitialized += (_, __) =>
            {
                this.Cloak();
                this.EnableRoundedCornersIfApplicable();
            };
            Themes.Manager.Current.ThemeChanged += () => EnableAcrylicIfApplicable(WindowsTaskbar.Current);

            // Device list starts collapsed
            Loaded += (s, args) =>
            {
                if (_viewModel is FlyoutViewModel fvm3)
                    fvm3.DevicePicker.IsDeviceListExpanded = false;
            };
        }

        public void Initialize()
        {
            Show();
            Hide();
            // Prevent showing up in Alt+Tab.
            this.ApplyExtendedWindowStyle(User32.WS_EX_TOOLWINDOW);

            _viewModel.ChangeState(FlyoutViewState.Hidden);
        }

        private void OnStateChanged(object sender, object e)
        {
            switch (_viewModel.State)
            {
                case FlyoutViewState.Opening:
                    var taskbar = WindowsTaskbar.Current;

                    Show();
                    EnableAcrylicIfApplicable(taskbar);
                    PositionWindowRelativeToTaskbar(taskbar);

// Focus the first device if available.
                    DevicesList.FindVisualChild<DeviceView>()?.FocusAndRemoveFocusVisual();

                    // Start animation immediately for snappy feel
                    WindowAnimationLibrary.BeginFlyoutEntranceAnimation(this, taskbar, () =>
                    {
                        _viewModel.ChangeState(FlyoutViewState.Open);
                    });
                    break;

                case FlyoutViewState.Closing_Stage1:
                    DevicesList.FindVisualChild<DeviceView>()?.FocusAndRemoveFocusVisual();
                
                    if (_viewModel.IsExpandingOrCollapsing)
                    {
                        WindowAnimationLibrary.BeginFlyoutExitanimation(this, () =>
                        {
                            this.Cloak();
                            AccentPolicyLibrary.DisableAcrylic(this);
                
                            // Go directly to ViewState.Hidden to avoid the stage 2 hide delay (debounce for tray clicks),
                            // we want to show again immediately.
                            _viewModel.ChangeState(FlyoutViewState.Hidden);
                        });
                    }
else
                    {
                        // Smooth exit animation
                        WindowAnimationLibrary.BeginFlyoutExitanimation(this, () =>
                        {
                            this.Cloak();
                            AccentPolicyLibrary.DisableAcrylic(this);
                            Hide();
                            _viewModel.ChangeState(FlyoutViewState.Closing_Stage2);
                        });
                    }
                    break;
            }
        }

        private void OnWindowsSizeInvalidated(object sender, object e)
        {
            // Avoid doing extra work in the background, only update the window when we're actually visible.
            switch (_viewModel.State)
            {
                case FlyoutViewState.Open:
                case FlyoutViewState.Opening:
                    PositionWindowRelativeToTaskbar(WindowsTaskbar.Current);
                    break;
            }
        }

        private void PositionWindowRelativeToTaskbar(WindowsTaskbar.State taskbar)
        {
            // We're not ready if we don't have a taskbar and monitor. (e.g. RDP transition)
            if (taskbar.ContainingScreen == null)
            {
                return;
            }

            // Force layout so we can be sure lists have created/removed containers.
            UpdateLayout();
            LayoutRoot.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            // Working area accounts for normal taskbar and docked windows.
            var adjustedWorkingAreaRight = taskbar.ContainingScreen.WorkingArea.Right;
            var adjustedWorkingAreaLeft = taskbar.ContainingScreen.WorkingArea.Left;
            var adjustedWorkingAreaTop = taskbar.ContainingScreen.WorkingArea.Top;
            var adjustedWorkingAreaBottom = taskbar.ContainingScreen.WorkingArea.Bottom;

            // Taskbar won't carve space out for itself if it's configured to auto-hide, so manually
            // adjust the working area to compensate. This is only done if the working area edge
            // reaches into Taskbar space. This accounts for 0..n docked windows that may not
            // push the working area out far enough.

            if (taskbar.IsAutoHideEnabled)
            {
                switch (taskbar.Location)
                {
                    case WindowsTaskbar.Position.Left:
                        if (taskbar.ContainingScreen.WorkingArea.Left < taskbar.Size.Right)
                        {
                            adjustedWorkingAreaLeft = taskbar.Size.Right;
                        }
                        break;
                    case WindowsTaskbar.Position.Right:
                        if (taskbar.ContainingScreen.WorkingArea.Right > taskbar.Size.Left)
                        {
                            adjustedWorkingAreaRight = taskbar.Size.Left;
                        }
                        break;
                    case WindowsTaskbar.Position.Top:
                        if (taskbar.ContainingScreen.WorkingArea.Top < taskbar.Size.Bottom)
                        {
                            adjustedWorkingAreaTop = taskbar.Size.Bottom;
                        }
                        break;
                    case WindowsTaskbar.Position.Bottom:
                        if (taskbar.ContainingScreen.WorkingArea.Bottom > taskbar.Size.Top)
                        {
                            adjustedWorkingAreaBottom = taskbar.Size.Top;
                        }
                        break;
                }
            }

            double flyoutWidth = Width * this.DpiX();
            double flyoutHeight = (LayoutRoot.DesiredSize.Height) * this.DpiY();

            double yOffset = 0;
            double xOffset = 0;
            if(Environment.OSVersion.IsAtLeast(OSVersions.Windows11))
            {
                xOffset += 12 * this.DpiX();
                yOffset += 12 * this.DpiY();
            }

            var workingAreaHeight = Math.Abs(adjustedWorkingAreaTop - adjustedWorkingAreaBottom) - (yOffset * 2);
            if (flyoutHeight > workingAreaHeight)
            {
                flyoutHeight = workingAreaHeight;
            }

            double top = 0;
            double left = 0;
            switch (taskbar.Location)
            {
                case WindowsTaskbar.Position.Left:
                    top = adjustedWorkingAreaBottom - flyoutHeight;
                    left = adjustedWorkingAreaLeft;
                    break;
                case WindowsTaskbar.Position.Right:
                    top = adjustedWorkingAreaBottom - flyoutHeight;
                    left = adjustedWorkingAreaRight - flyoutWidth;
                    break;
                case WindowsTaskbar.Position.Top:
                    top = adjustedWorkingAreaTop + xOffset;
                    left = FlowDirection == FlowDirection.LeftToRight ? adjustedWorkingAreaRight - flyoutWidth - xOffset : adjustedWorkingAreaLeft + xOffset;
                    break;
                case WindowsTaskbar.Position.Bottom:
                    top = adjustedWorkingAreaBottom - flyoutHeight - yOffset;
                    left = FlowDirection == FlowDirection.LeftToRight ? adjustedWorkingAreaRight - flyoutWidth - xOffset : adjustedWorkingAreaLeft + xOffset;
                    break;
            }
            this.SetWindowPos(top, left, flyoutHeight, flyoutWidth);
            _viewModel.UpdateWindowPos(top, left, flyoutHeight, flyoutWidth);
        }

        private void UpdateBanner_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is FlyoutViewModel fvm)
            {
                fvm.OpenUpdatePage();
            }
        }

        private bool _isAnimatingDeviceList;

        private void ToggleDeviceList_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isAnimatingDeviceList) { e.Handled = true; return; }
            if (!(DataContext is FlyoutViewModel fvm)) return;

            var container = FindName("DeviceListContainer") as FrameworkElement;
            var items = FindName("DeviceListItems") as FrameworkElement;
            if (container == null || items == null) return;

            var expanding = !fvm.DevicePicker.IsDeviceListExpanded;
            _isAnimatingDeviceList = true;

            if (expanding)
            {
                fvm.DevicePicker.IsDeviceListExpanded = true;
                items.Measure(new Size(container.ActualWidth > 0 ? container.ActualWidth : 360, double.PositiveInfinity));
                var targetHeight = items.DesiredSize.Height;

                container.Height = 0;
                var anim = new System.Windows.Media.Animation.DoubleAnimation(0, targetHeight,
                    System.TimeSpan.FromMilliseconds(200))
                { EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                    { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };
                anim.Completed += (s2, e2) =>
                {
                    container.BeginAnimation(HeightProperty, null);
                    container.Height = double.NaN;
                    _isAnimatingDeviceList = false;
                };
                container.BeginAnimation(HeightProperty, anim);
            }
            else
            {
                var currentHeight = container.ActualHeight;
                var anim = new System.Windows.Media.Animation.DoubleAnimation(currentHeight, 0,
                    System.TimeSpan.FromMilliseconds(180))
                { EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                    { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn } };
                anim.Completed += (s2, e2) =>
                {
                    container.BeginAnimation(HeightProperty, null);
                    container.Height = 0;
                    fvm.DevicePicker.IsDeviceListExpanded = false;
                    _isAnimatingDeviceList = false;
                };
                container.BeginAnimation(HeightProperty, anim);
            }

            e.Handled = true;
        }

        private void DeviceRow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var border = sender as FrameworkElement;
            if (border?.Tag is ViewModels.DeviceViewModel device && DataContext is FlyoutViewModel fvm)
            {
                fvm.DevicePicker.SelectedDevice = device;
                e.Handled = true;
            }
        }

        private void PlayPause_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try { DataModel.MediaSessionService.Instance.PlayPause(); } catch { }
            e.Handled = true;
        }

        private void NextTrack_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try { DataModel.MediaSessionService.Instance.Next(); } catch { }
            e.Handled = true;
        }

        private void PreviousTrack_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try { DataModel.MediaSessionService.Instance.Previous(); } catch { }
            e.Handled = true;
        }

        private void UpdateMediaCardColor(System.Windows.Media.Color color)
        {
            var gradientStart = FindName("MediaGradientStart") as System.Windows.Media.GradientStop;
            var glowColor = FindName("GlowColor") as System.Windows.Media.GradientStop;
            if (gradientStart == null) return;

            // Dim the color to ~30% opacity for the gradient background
            var dimmed = System.Windows.Media.Color.FromArgb(70, color.R, color.G, color.B);
            var glow = System.Windows.Media.Color.FromArgb(35, color.R, color.G, color.B);

            // Animate the color transition
            var colorAnim = new System.Windows.Media.Animation.ColorAnimation(dimmed, System.TimeSpan.FromMilliseconds(600));
            gradientStart.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty, colorAnim);

            if (glowColor != null)
            {
                var glowAnim = new System.Windows.Media.Animation.ColorAnimation(glow, System.TimeSpan.FromMilliseconds(600));
                glowColor.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty, glowAnim);
            }
        }

        private void PinButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is FlyoutViewModel fvm)
            {
                fvm.IsPinned = !fvm.IsPinned;
            }
            e.Handled = true;
        }

        private void EnableAcrylicIfApplicable(WindowsTaskbar.State taskbar)
        {
            // Note: Enable when in Opening as well as Open in case we get a theme change during a show cycle.
            if (_viewModel.State == FlyoutViewState.Opening || _viewModel.State == FlyoutViewState.Open)
            {
                AccentPolicyLibrary.EnableAcrylic(this, Themes.Manager.Current.ResolveRef(this, "AcrylicColor_Flyout"), GetAccentFlags(taskbar));
            }
            else
            {
                // Disable to avoid visual issues like showing a pane of acrylic while we're Hidden+cloaked.
                AccentPolicyLibrary.DisableAcrylic(this);
            }
        }

        private User32.AccentFlags GetAccentFlags(WindowsTaskbar.State taskbar)
        {
            if (Environment.OSVersion.IsAtLeast(OSVersions.Windows11))
            {
                return User32.AccentFlags.DrawAllBorders;
            }

            switch (taskbar.Location)
            {
                case WindowsTaskbar.Position.Left:
                    return User32.AccentFlags.DrawRightBorder | User32.AccentFlags.DrawTopBorder;
                case WindowsTaskbar.Position.Right:
                    return User32.AccentFlags.DrawLeftBorder | User32.AccentFlags.DrawTopBorder;
                case WindowsTaskbar.Position.Top:
                    return User32.AccentFlags.DrawLeftBorder | User32.AccentFlags.DrawBottomBorder;
                case WindowsTaskbar.Position.Bottom:
                    return User32.AccentFlags.DrawTopBorder | User32.AccentFlags.DrawLeftBorder;
            }
            return User32.AccentFlags.None;
        }
    }
}
