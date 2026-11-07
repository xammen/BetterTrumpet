using EarTrumpet.Extensions;
using EarTrumpet.Interop;
using EarTrumpet.Interop.Helpers;
using EarTrumpet.UI.Helpers;
using EarTrumpet.UI.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Forms = System.Windows.Forms;

namespace EarTrumpet.UI.Views
{
    public partial class FlyoutWindow
    {
        private const int WM_DPICHANGED = 0x02E0;

        private readonly IFlyoutViewModel _viewModel;
        private readonly Func<Rect?> _trayIconBoundsProvider;
        private readonly FlyoutSurfaceMotion _surface;
        private HwndSource _windowSource;

        // Taskbar and monitor of the current show cycle. The close reverses along the same axis.
        private WindowsTaskbar.State _taskbar;
        private Forms.Screen _restScreen;

        // Bumped by every open/close transition: completions of a superseded one are ignored,
        // which is what lets an interrupted animation retarget instead of fighting the new one.
        private int _transition;
        private bool _isBelowTaskbar;

        // Last accent policy sent to DWM. Acrylic stays enabled across show cycles and is only
        // re-sent when the theme or the taskbar edge changes it.
        private bool _isAcrylicApplied;
        private Color _acrylicColor;
        private User32.AccentFlags _acrylicFlags;

        public FlyoutWindow(IFlyoutViewModel viewModel, Func<Rect?> trayIconBoundsProvider = null)
        {
            _viewModel = viewModel;
            _trayIconBoundsProvider = trayIconBoundsProvider;
            _surface = new FlyoutSurfaceMotion(this);
            DataContext = _viewModel;

            InitializeComponent();
            InitializeRowEntrance();

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
                _windowSource = PresentationSource.FromVisual(this) as HwndSource;
                _windowSource?.AddHook(OnWindowMessage);
                this.Cloak();
                this.EnableRoundedCornersIfApplicable();
            };
            Themes.Manager.Current.ThemeChanged += () => ApplyAcrylic(WindowsTaskbar.Current, force: true);
        }

        public void Initialize()
        {
            Show();
            Hide();
            // Prevent showing up in Alt+Tab.
            this.ApplyExtendedWindowStyle(User32.WS_EX_TOOLWINDOW);

            _viewModel.ChangeState(FlyoutViewState.Hidden);

            // Force theme refresh after initialization to ensure all brushes are correctly applied
            // This fixes backdrop color not showing correctly on first startup (GitHub #13)
            Themes.Manager.Current.NotifyThemeChanged();

            // Force immediate acrylic refresh after theme notification to ensure backdrop renders correctly
            // OPTIMIZATION: Use InvokeAsync instead of BeginInvoke for better performance
            _ = Dispatcher.InvokeAsync(() =>
            {
                ApplyAcrylic(WindowsTaskbar.Current, force: true);
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void OnStateChanged(object sender, object e)
        {
            switch (_viewModel.State)
            {
                case FlyoutViewState.Opening:
                    BeginOpenTransition();
                    break;

                case FlyoutViewState.Closing_Stage1:
                    BeginCloseTransition();
                    break;
            }
        }

        // ---- Open / close motion -----------------------------------------------------------
        //
        // The surface is the HWND itself: FlyoutSurfaceMotion slides the window (position only)
        // so the DWM acrylic travels with it, out of the taskbar edge on open and back on close.
        // The content (LayoutRoot) trails the surface by a few DIPs and fades. Every transition
        // bumps _transition; a completion that belongs to a superseded transition does nothing,
        // and every animation starts from the current rendered value, so interruptions retarget.

        private void BeginOpenTransition()
        {
            var transition = ++_transition;
            var taskbar = WindowsTaskbar.Current;
            _taskbar = taskbar;

            // An open triggered by expand/collapse is a content swap inside a surface that never
            // left the screen, not a fresh entrance.
            var flyoutViewModel = _viewModel as FlyoutViewModel;
            bool fromExpand = flyoutViewModel != null && flyoutViewModel.IsReopeningFromExpand;
            if (flyoutViewModel != null)
            {
                flyoutViewModel.IsReopeningFromExpand = false;
            }

            var previousRest = _surface.Rest;

            // Rows caught mid-entrance by the previous close come back at their natural height.
            ResetRowEntrance();

            Show();
            ApplyAcrylic(taskbar, force: false);
            _surface.SetAxis(taskbar.Location);
            PositionWindowRelativeToTaskbar(taskbar);

            // Focus the first device if available.
            DevicesList.FindVisualChild<DeviceView>()?.FocusAndRemoveFocusVisual();

            if (!Themes.Manager.Current.AnimationsEnabled)
            {
                _surface.Snap(0);
                ResetContentVisuals();
                PresentWindow(belowTaskbar: false);
                ChangeStateLater(transition, FlyoutViewState.Opening, FlyoutViewState.Open);
                return;
            }

            if (fromExpand)
            {
                BeginExpandReveal(transition, taskbar, previousRest);
                return;
            }

            var travel = GetTravelDistance(taskbar);
            Trace.WriteLine($"FlyoutWindow Open: edge={taskbar.Location}, travel={travel:0.#}dip, behindTaskbar={IsTaskbarOnFlyoutScreen(taskbar)}");

            _surface.Snap(travel);
            BeginContentEntrance(transition);
            PresentWindow(belowTaskbar: travel > 0 && IsTaskbarOnFlyoutScreen(taskbar));
            _surface.AnimateTo(0, FlyoutMotion.OpenSurfaceMs, FlyoutMotion.Decelerate, null);
        }

        /// <summary>
        /// Uncloaks and takes focus right away, so input works during the entrance instead of
        /// after it. Activation raises the window to the top of the topmost band; it is then
        /// slipped back under the taskbar so the part of the surface that still overlaps the
        /// taskbar emerges from behind it. <see cref="OnOpenSettled"/> raises it again.
        /// </summary>
        private void PresentWindow(bool belowTaskbar)
        {
            Topmost = true;
            Activate();
            Focus();

            _isBelowTaskbar = belowTaskbar;
            if (belowTaskbar)
            {
                this.PlaceBelow(WindowsTaskbar.GetHwnd());
            }

            this.Cloak(false);
        }

        private void BeginContentEntrance(int transition)
        {
            // Base values are the final ones (opacity 1, no offset). The clocks hold the start
            // values from their first frame and simply stop, so nothing keeps animating after.
            var slide = new TranslateTransform();
            LayoutRoot.RenderTransform = slide;
            LayoutRoot.Opacity = 1;
            LayoutRoot.BeginAnimation(OpacityProperty,
                FlyoutMotion.Create(0, 1, FlyoutMotion.OpenContentFadeMs, FlyoutMotion.EaseOut));

            var slideAnimation = FlyoutMotion.Create(FlyoutMotion.OpenContentSlideDip * GetAxisSign(), 0,
                FlyoutMotion.OpenContentSlideMs, FlyoutMotion.Decelerate);
            slideAnimation.Completed += (s, e) =>
            {
                ClearRenderTransform(LayoutRoot, slide);
                OnOpenSettled(transition);
            };
            slide.BeginAnimation(_surface.IsVerticalAxis ? TranslateTransform.YProperty : TranslateTransform.XProperty, slideAnimation);
        }

        /// <summary>
        /// End of the entrance (the surface curve is within a fraction of a pixel of rest by now).
        /// Deactivations are ignored while Opening, so if something took the foreground in the
        /// meantime, take it back exactly as the previous animation did on completion.
        /// </summary>
        private void OnOpenSettled(int transition)
        {
            if (transition != _transition || _viewModel.State != FlyoutViewState.Opening)
            {
                return;
            }

            RaiseAboveTaskbar();
            if (!IsActive)
            {
                Focus();
                User32.SetForegroundWindow(this.GetHandle());
            }

            _viewModel.ChangeState(FlyoutViewState.Open);
        }

        private void BeginCloseTransition()
        {
            var transition = ++_transition;
            DevicesList.FindVisualChild<DeviceView>()?.FocusAndRemoveFocusVisual();

            // A row growing in stays as it is for the exit; it is reset once hidden.
            FreezeRowEntrance();

            if (_viewModel.IsExpandingOrCollapsing)
            {
                BeginExpandSwapOut(transition);
                return;
            }

            if (!Themes.Manager.Current.AnimationsEnabled)
            {
                FinishClose(transition);
                return;
            }

            // Slide back toward the taskbar and behind it, not over it. Without a taskbar on this
            // monitor, leave the topmost band as the previous exit did.
            if (IsTaskbarOnFlyoutScreen(_taskbar))
            {
                _isBelowTaskbar = true;
                this.PlaceBelow(WindowsTaskbar.GetHwnd());
            }
            else
            {
                Topmost = false;
            }

            LayoutRoot.BeginAnimation(OpacityProperty,
                FlyoutMotion.Create(null, 0, FlyoutMotion.CloseContentFadeMs, FlyoutMotion.Accelerate, 0, FillBehavior.HoldEnd));
            _surface.AnimateTo(GetTravelDistance(_taskbar), FlyoutMotion.CloseSurfaceMs, FlyoutMotion.Accelerate, () =>
            {
                if (transition == _transition)
                {
                    FinishClose(transition);
                }
            });
        }

        private void FinishClose(int transition)
        {
            this.Cloak();
            Topmost = false;
            Hide();
            _isBelowTaskbar = false;

            // Park the content transparent: on the next open DWM shows the last presented frame
            // until WPF renders a new one, and that frame must never be stale content.
            LayoutRoot.BeginAnimation(OpacityProperty, null);
            LayoutRoot.Opacity = 0;
            LayoutRoot.RenderTransform = null;
            ResetRowEntrance();

            ChangeStateLater(transition, FlyoutViewState.Closing_Stage1, FlyoutViewState.Closing_Stage2);
        }

        /// <summary>
        /// Requests the next state after the current dispatcher operation instead of from inside
        /// StateChanged. Re-entering ChangeState synchronously used to run the view model's
        /// per-state side effects out of order (with animations off, a close never stopped the
        /// peak meters). Dropped if another transition started in the meantime.
        /// </summary>
        private void ChangeStateLater(int transition, FlyoutViewState expected, FlyoutViewState next)
        {
            _ = Dispatcher.InvokeAsync(() =>
            {
                if (transition == _transition && _viewModel.State == expected)
                {
                    _viewModel.ChangeState(next);
                }
            }, System.Windows.Threading.DispatcherPriority.Normal);
        }

        /// <summary>
        /// Expand/collapse, first half: only the content fades. The surface stays on screen (no
        /// cloak, no acrylic toggle) and Hidden is entered directly to skip the tray debounce.
        /// The ease-out curve makes the content visually gone a frame before the resize.
        /// </summary>
        private void BeginExpandSwapOut(int transition)
        {
            if (!Themes.Manager.Current.AnimationsEnabled)
            {
                ChangeStateLater(transition, FlyoutViewState.Closing_Stage1, FlyoutViewState.Hidden);
                return;
            }

            var fade = FlyoutMotion.Create(null, 0, FlyoutMotion.SwapFadeOutMs, FlyoutMotion.EaseOut, 0, FillBehavior.HoldEnd);
            fade.Completed += (s, e) =>
            {
                if (transition == _transition && _viewModel.State == FlyoutViewState.Closing_Stage1)
                {
                    _viewModel.ChangeState(FlyoutViewState.Hidden);
                }
            };
            LayoutRoot.BeginAnimation(OpacityProperty, fade);
        }

        /// <summary>
        /// Expand/collapse, second half: the window already has its new size. When it grew, the
        /// surface starts with its far edge where the old one was and slides the rest out of the
        /// taskbar edge (bounded by the room behind the taskbar on this monitor); a shrink just
        /// lands, since the content is invisible at that moment. The device blocks cascade in.
        /// </summary>
        private void BeginExpandReveal(int transition, WindowsTaskbar.State taskbar, Int32Rect? previousRest)
        {
            ResetContentVisuals();
            BeginBlockCascade();

            var rest = _surface.Rest;
            double start = Math.Max(0, _surface.Offset);
            if (taskbar.IsHorizontal && previousRest.HasValue && rest.HasValue)
            {
                double growth = (rest.Value.Height - previousRest.Value.Height) / _surface.ScaleY;
                if (growth > 0)
                {
                    start = Math.Min(start + growth, GetRoomBehindTaskbarEdge(taskbar));
                }
            }

            if (start > _surface.Offset)
            {
                _surface.Snap(start);
            }

            Topmost = true;
            if (!IsActive)
            {
                Activate();
            }

            if (_surface.Offset > 0)
            {
                if (IsTaskbarOnFlyoutScreen(taskbar))
                {
                    _isBelowTaskbar = true;
                    this.PlaceBelow(WindowsTaskbar.GetHwnd());
                }

                _surface.AnimateTo(0, FlyoutMotion.SwapSurfaceMs, FlyoutMotion.Decelerate, () =>
                {
                    if (transition == _transition)
                    {
                        RaiseAboveTaskbar();
                    }
                });
            }
            else
            {
                RaiseAboveTaskbar();
            }

            ChangeStateLater(transition, FlyoutViewState.Opening, FlyoutViewState.Open);
        }

        /// <summary>
        /// Compact cascade for expand/collapse: at most five blocks (the header rides with the
        /// first device, extra devices share the last slot), 24 ms apart, opacity plus a 6 DIP
        /// rise over 200 ms. Item containers are animated, never the DeviceView/AppItemView
        /// transforms that their own animations own.
        /// </summary>
        private void BeginBlockCascade()
        {
            int block = 0;
            var generator = DevicesList.ItemContainerGenerator;
            for (int i = 0; i < DevicesList.Items.Count; i++)
            {
                if (generator.ContainerFromIndex(i) is FrameworkElement container)
                {
                    int delay = GetBlockDelay(block);
                    RevealBlock(container, delay);
                    if (block == 0)
                    {
                        RevealBlock(HeaderButtons, delay);
                    }
                    block++;
                }
            }

            if (UpdateBanner.Visibility == Visibility.Visible)
            {
                RevealBlock(UpdateBanner, GetBlockDelay(block));
            }
        }

        private static int GetBlockDelay(int block)
        {
            return Math.Min(block, FlyoutMotion.SwapMaxBlocks - 1) * FlyoutMotion.SwapBlockStaggerMs;
        }

        private static void RevealBlock(FrameworkElement element, int delayMs)
        {
            var slide = new TranslateTransform();
            element.RenderTransform = slide;
            element.BeginAnimation(OpacityProperty,
                FlyoutMotion.Create(0, 1, FlyoutMotion.SwapBlockMs, FlyoutMotion.EaseOut, delayMs));

            var rise = FlyoutMotion.Create(FlyoutMotion.SwapBlockSlideDip, 0, FlyoutMotion.SwapBlockMs, FlyoutMotion.Decelerate, delayMs);
            rise.Completed += (s, e) => ClearRenderTransform(element, slide);
            slide.BeginAnimation(TranslateTransform.YProperty, rise);
        }

        private static void ClearRenderTransform(UIElement element, Transform owned)
        {
            if (ReferenceEquals(element.RenderTransform, owned))
            {
                element.RenderTransform = null;
            }
        }

        private void ResetContentVisuals()
        {
            LayoutRoot.BeginAnimation(OpacityProperty, null);
            LayoutRoot.Opacity = 1;
            LayoutRoot.RenderTransform = null;
        }

        private void RaiseAboveTaskbar()
        {
            if (_isBelowTaskbar)
            {
                _isBelowTaskbar = false;
                this.BringToTopOfTopmostBand();
            }
        }

        private double GetAxisSign()
        {
            return _surface.IsVerticalAxis ? _surface.AxisY : _surface.AxisX;
        }

        /// <summary>
        /// Travel along the taskbar axis: up to 36 DIPs when a taskbar on this monitor hides the
        /// start position, 16 when the surface would be exposed (auto-hide, other monitor), and
        /// never more than a third of the flyout so small flyouts do not fly in from far away.
        /// </summary>
        private double GetTravelDistance(WindowsTaskbar.State taskbar)
        {
            var rest = _surface.Rest;
            if (!rest.HasValue)
            {
                return 0;
            }

            double extent = taskbar.IsHorizontal
                ? rest.Value.Height / _surface.ScaleY
                : rest.Value.Width / _surface.ScaleX;
            double max = !taskbar.IsAutoHideEnabled && IsTaskbarOnFlyoutScreen(taskbar)
                ? FlyoutMotion.MaxTravelDip
                : FlyoutMotion.ExposedTravelDip;
            return Math.Max(0, Math.Min(max, extent / 3));
        }

        /// <summary>
        /// Room between the rest edge facing the taskbar and the monitor edge (gap + taskbar), in
        /// DIPs. Bounding the expand slide by it keeps the surface on its own monitor.
        /// </summary>
        private double GetRoomBehindTaskbarEdge(WindowsTaskbar.State taskbar)
        {
            var rest = _surface.Rest;
            if (!rest.HasValue || _restScreen == null)
            {
                return 0;
            }

            var bounds = _restScreen.Bounds;
            var r = rest.Value;
            switch (taskbar.Location)
            {
                case WindowsTaskbar.Position.Top:
                    return Math.Max(0, (r.Y - bounds.Top) / _surface.ScaleY);
                case WindowsTaskbar.Position.Left:
                    return Math.Max(0, (r.X - bounds.Left) / _surface.ScaleX);
                case WindowsTaskbar.Position.Right:
                    return Math.Max(0, (bounds.Right - (r.X + r.Width)) / _surface.ScaleX);
                default:
                    return Math.Max(0, (bounds.Bottom - (r.Y + r.Height)) / _surface.ScaleY);
            }
        }

        private bool IsTaskbarOnFlyoutScreen(WindowsTaskbar.State taskbar)
        {
            return _restScreen != null &&
                   taskbar.ContainingScreen != null &&
                   taskbar.ContainingScreen.DeviceName == _restScreen.DeviceName;
        }

        private void OnWindowsSizeInvalidated(object sender, object e)
        {
            // Avoid doing extra work in the background, only update the window when we're actually visible.
            switch (_viewModel.State)
            {
                case FlyoutViewState.Open:
                case FlyoutViewState.Opening:
                    try
                    {
                        PositionWindowRelativeToTaskbar(WindowsTaskbar.Current);
                    }
                    catch (InvalidOperationException)
                    {
                        // The VirtualizingStackPanel detected an inconsistent ItemsSource
                        // (e.g. rapid device churn while the flyout is open). Close the
                        // flyout to break the crash-loop — without this, every subsequent
                        // layout pass throws again and the crash dialog reappears in a
                        // loop for ~20 seconds.
                        Trace.WriteLine("FlyoutWindow: Closing due to ItemsSource inconsistency");
                        if (_viewModel is FlyoutViewModel fvm)
                        {
                            fvm.ChangeState(FlyoutViewState.Closing_Stage1);
                        }
                    }
                    break;
            }
        }

        private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == WM_DPICHANGED)
            {
                // WPF's TransformToDevice can lag behind the native DPI after a
                // display-scale change. Reposition after Windows has applied the
                // new monitor DPI to the HWND.
                // OPTIMIZATION: Use InvokeAsync instead of BeginInvoke for better performance
                _ = Dispatcher.InvokeAsync(() =>
                {
                    if (_viewModel.State == FlyoutViewState.Open || _viewModel.State == FlyoutViewState.Opening)
                    {
                        PositionWindowRelativeToTaskbar(WindowsTaskbar.Current);
                    }
                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }

            return IntPtr.Zero;
        }

        private void PositionWindowRelativeToTaskbar(WindowsTaskbar.State taskbar)
        {
            _isRepositioning = true;
            try
            {
                PositionWindowRelativeToTaskbarCore(taskbar);
            }
            finally
            {
                _isRepositioning = false;
            }
        }

        private void PositionWindowRelativeToTaskbarCore(WindowsTaskbar.State taskbar)
        {
            var trayIconBounds = GetTrayIconBoundsForPositioning();
            var containingScreen = GetContainingScreen(taskbar, trayIconBounds);

            // We're not ready if we don't have a taskbar and monitor. (e.g. RDP transition)
            if (containingScreen == null)
            {
                return;
            }

            _restScreen = containingScreen;

            // Force layout so we can be sure lists have created/removed containers.
            UpdateLayout();
            LayoutRoot.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            // Working area accounts for normal taskbar and docked windows.
            var adjustedWorkingAreaRight = containingScreen.WorkingArea.Right;
            var adjustedWorkingAreaLeft = containingScreen.WorkingArea.Left;
            var adjustedWorkingAreaTop = containingScreen.WorkingArea.Top;
            var adjustedWorkingAreaBottom = containingScreen.WorkingArea.Bottom;

            // Taskbar won't carve space out for itself if it's configured to auto-hide, so manually
            // adjust the working area to compensate. This is only done if the working area edge
            // reaches into Taskbar space. This accounts for 0..n docked windows that may not
            // push the working area out far enough.

            if (taskbar.IsAutoHideEnabled && taskbar.ContainingScreen != null && taskbar.ContainingScreen.DeviceName == containingScreen.DeviceName)
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

            uint nativeDpi = User32.GetDpiForWindow(new WindowInteropHelper(this).Handle);
            double dpiX = nativeDpi > 0 ? nativeDpi / (double)96 : this.DpiX();
            double dpiY = nativeDpi > 0 ? nativeDpi / (double)96 : this.DpiY();
            double flyoutWidth = Width * dpiX;

            // Rows growing in count at their natural height: the window takes its final size
            // once and the row entrance releases it as they grow (FlyoutWindow.RowEntrance.cs).
            double contentHeight = LayoutRoot.DesiredSize.Height + GetRemainingRowGrowth();
            double flyoutHeight = contentHeight * dpiY;

            double yOffset = 0;
            double xOffset = 0;
            if(Environment.OSVersion.IsAtLeast(OSVersions.Windows11))
            {
                xOffset += 12 * dpiX;
                yOffset += 12 * dpiY;
            }

            var workingAreaHeight = Math.Abs(adjustedWorkingAreaTop - adjustedWorkingAreaBottom) - (yOffset * 2);

            // Round up to avoid sub-pixel scrollbar from DPI rounding.
            // e.g. content = 523.7 DIPs × 1.25 DPI = 654.625 → truncated to 654 pixels
            // → WPF sees 523.2 DIPs → 0.5 DIP overflow → phantom scrollbar.
            flyoutHeight = Math.Ceiling(flyoutHeight);

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

            left = Clamp(left, adjustedWorkingAreaLeft + xOffset, adjustedWorkingAreaRight - flyoutWidth - xOffset);
            top = Clamp(top, adjustedWorkingAreaTop + yOffset, adjustedWorkingAreaBottom - flyoutHeight - yOffset);

            bool anchorTop = taskbar.Location == WindowsTaskbar.Position.Top;
            int growthRoom = _growingRows.Count > 0
                ? GetRowGrowthRoom(containingScreen, (int)left, (int)top, (int)flyoutWidth, (int)flyoutHeight, anchorTop)
                : 0;
            UpdateRowGrowthLayout(contentHeight, (int)flyoutHeight, dpiY, anchorTop, growthRoom);

            // This is the rest rect. The motion driver places the window at rest + its current
            // offset, so a reposition during an animation moves the target instead of snapping,
            // and nothing is sent to the window manager when the window is already in place.
            _surface.SetRest((int)left, (int)top, (int)flyoutWidth, (int)flyoutHeight, dpiX, dpiY);

            // Sync WPF Height with the Win32 window size so the ScrollViewer
            // knows the actual available space and can scroll properly (GitHub #3).
            // SetWindowPos bypasses WPF, so without this WPF may think it has more
            // space than it actually does → content renders past the window → blank.
            Height = flyoutHeight / dpiY;

            _viewModel.UpdateWindowPos(top, left, flyoutHeight, flyoutWidth);
        }

        private Rect? GetTrayIconBoundsForPositioning()
        {
            if (!WasOpenedFromMouse() || _trayIconBoundsProvider == null)
            {
                return null;
            }

            var bounds = _trayIconBoundsProvider();
            if (!bounds.HasValue || bounds.Value.IsEmpty || bounds.Value.Width <= 0 || bounds.Value.Height <= 0)
            {
                return null;
            }

            return bounds;
        }

        private bool WasOpenedFromMouse()
        {
            var flyoutViewModel = _viewModel as FlyoutViewModel;
            return flyoutViewModel != null && flyoutViewModel.WasLastInputMouse;
        }

        private static Forms.Screen GetContainingScreen(WindowsTaskbar.State taskbar, Rect? trayIconBounds)
        {
            if (trayIconBounds.HasValue)
            {
                return Forms.Screen.FromPoint(new System.Drawing.Point(
                    (int)(trayIconBounds.Value.Left + (trayIconBounds.Value.Width / 2)),
                    (int)(trayIconBounds.Value.Top + (trayIconBounds.Value.Height / 2))));
            }

            return taskbar.ContainingScreen;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (max < min)
            {
                return min;
            }

            return Math.Max(min, Math.Min(max, value));
        }

        private void UpdateBanner_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is FlyoutViewModel fvm)
            {
                fvm.OpenUpdatePage();
            }
        }

        private void RestoreHiddenAppsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.ContextMenu != null)
            {
                element.ContextMenu.PlacementTarget = element;
                element.ContextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        private void RestoreHiddenDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.ContextMenu != null)
            {
                element.ContextMenu.PlacementTarget = element;
                element.ContextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        private void SetAsDefaultDevice_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is DeviceViewModel device)
            {
                device.MakeDefaultDevice();
            }
        }

        /// <summary>
        /// Keeps the acrylic accent enabled across show cycles instead of disabling it on every
        /// close and re-enabling it on every open: between cycles the window is both cloaked and
        /// SW_HIDE'd, so DWM composes nothing for it, and skipping the round trip removes two
        /// accent-policy calls from each cycle and the risk of a first uncloaked frame without
        /// blur. The policy is re-sent only when the resolved tint or the border flags change
        /// (theme change, or a taskbar move on Windows 10), or when forced.
        /// </summary>
        private void ApplyAcrylic(WindowsTaskbar.State taskbar, bool force)
        {
            if (new WindowInteropHelper(this).Handle == IntPtr.Zero)
            {
                return;
            }

            var color = Themes.Manager.Current.ResolveRef(this, "AcrylicColor_Flyout");
            var flags = GetAccentFlags(taskbar);
            if (!force && _isAcrylicApplied && color == _acrylicColor && flags == _acrylicFlags)
            {
                return;
            }

            AccentPolicyLibrary.EnableAcrylic(this, color, flags);
            _isAcrylicApplied = true;
            _acrylicColor = color;
            _acrylicFlags = flags;
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
