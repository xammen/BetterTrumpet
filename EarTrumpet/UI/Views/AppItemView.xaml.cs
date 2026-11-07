using System;
using System.Runtime.CompilerServices;
using EarTrumpet.Extensions;
using EarTrumpet.UI.Helpers;
using EarTrumpet.UI.ViewModels;
using System.Linq;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EarTrumpet.UI.Views
{
    public partial class AppItemView : UserControl
    {
        private const int MuteStateAnimationDurationMs = 185;
        private const int HideAnimationDurationMs = 140;
        private const int EntranceAnimationDurationMs = 220;
        private const double EntranceSlideOffsetPx = 8;
        private const double HideSlideOffsetPx = -12;

        // View models whose runtime entrance has already been handled by the flyout, so a row
        // re-realized later (another container, a list rebuild) never grows in a second time.
        private static readonly ConditionalWeakTable<IAppItemViewModel, object> _flyoutEntrancesHandled =
            new ConditionalWeakTable<IAppItemViewModel, object>();

        private IAppItemViewModel App => DataContext as IAppItemViewModel;
        private IAppItemViewModel _subscribedApp;
        private FlyoutWindow _entranceHost;
        private ListViewItem _container;
        private bool _lastMutedState;
        private bool _hideAnimationStarted;

        public AppItemView()
        {
            InitializeComponent();

            PreviewMouseRightButtonUp += (_, __) => OpenPopup();
            DataContextChanged += OnDataContextChanged;

            Loaded += (_, __) =>
            {
                _container = this.FindVisualParent<ListViewItem>();
                if (_container != null)
                {
                    _container.PreviewKeyDown -= OnPreviewKeyDown;
                    _container.PreviewKeyDown += OnPreviewKeyDown;
                }

                SubscribeToTooltipSettings();
                SubscribeToAppEvents(App);
                UpdateTooltipVisibility();
                UpdateMutedVisualState(false);
                AnimateInIfNeeded();
            };

            Unloaded += (_, __) =>
            {
                LeaveFlyoutEntrance();

                if (_container != null)
                {
                    _container.PreviewKeyDown -= OnPreviewKeyDown;
                    _container = null;
                }

                UnsubscribeFromTooltipSettings();
                UnsubscribeFromAppEvents();
            };
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var app = App;
            if (app == null)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.M:
                case Key.OemPeriod:
                    app.IsMuted = !app.IsMuted;
                    e.Handled = true;
                    break;
                case Key.Right:
                case Key.OemPlus:
                    app.Volume++;
                    e.Handled = true;
                    break;
                case Key.Left:
                case Key.OemMinus:
                    app.Volume--;
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
            var app = App;
            if (viewModel != null && app != null && !app.IsExpanded)
            {
                viewModel.OpenPopup(app, this);
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

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            LeaveFlyoutEntrance();
            ResetTransientVisualState();

            UnsubscribeFromAppEvents();
            SubscribeToAppEvents(e.NewValue as IAppItemViewModel);
            UpdateMutedVisualState(false);

            if (App?.IsHiding == true)
            {
                BeginHideAnimation();
            }
        }

        private void SubscribeToAppEvents(IAppItemViewModel app)
        {
            if (_subscribedApp == app)
            {
                return;
            }

            UnsubscribeFromAppEvents();

            if (app == null)
            {
                return;
            }

            _subscribedApp = app;
            _lastMutedState = app.IsMuted;
            _subscribedApp.PropertyChanged += App_PropertyChanged;
        }

        private void UnsubscribeFromAppEvents()
        {
            if (_subscribedApp != null)
            {
                _subscribedApp.PropertyChanged -= App_PropertyChanged;
                _subscribedApp = null;
            }
        }

        private void App_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke((Action)(() => App_PropertyChanged(sender, e)));
                return;
            }

            if (e.PropertyName == nameof(IAppItemViewModel.IsMuted))
            {
                UpdateMutedVisualState(true);
            }
            else if (e.PropertyName == nameof(IAppItemViewModel.IsHiding) && App?.IsHiding == true)
            {
                BeginHideAnimation();
            }
        }

        private void UpdateMutedVisualState(bool animate)
        {
            var app = App;
            if (app == null)
            {
                return;
            }

            var isMuted = app.IsMuted;
            var mutedOpacity = GetMutedOpacity();
            var fromMainOpacity = _lastMutedState ? mutedOpacity : 1;
            var toMainOpacity = isMuted ? mutedOpacity : 1;
            var fromMuteOpacity = _lastMutedState ? 1 : 0;
            var toMuteOpacity = isMuted ? 1 : 0;

            if (!animate || !ShouldAnimateStateChange())
            {
                SetOpacity(AppIconHost, toMainOpacity);
                SetOpacity(VolumeControl, toMainOpacity);
                SetOpacity(VolumeText, toMainOpacity);
                SetOpacity(MuteButton, toMuteOpacity);
                _lastMutedState = isMuted;
                return;
            }

            AnimateOpacity(AppIconHost, fromMainOpacity, toMainOpacity, MuteStateAnimationDurationMs);
            AnimateOpacity(VolumeControl, fromMainOpacity, toMainOpacity, MuteStateAnimationDurationMs);
            AnimateOpacity(VolumeText, fromMainOpacity, toMainOpacity, MuteStateAnimationDurationMs);
            AnimateOpacity(MuteButton, fromMuteOpacity, toMuteOpacity, MuteStateAnimationDurationMs);
            _lastMutedState = isMuted;
        }

        private void AnimateInIfNeeded()
        {
            var app = App;
            if (app?.AnimateOnLoad != true)
            {
                return;
            }

            // In the tray flyout the window owns the entrance: the row and the window edge grow
            // together, and the row waits while a slider is being dragged.
            if (Window.GetWindow(this) is FlyoutWindow flyout)
            {
                if (_flyoutEntrancesHandled.TryGetValue(app, out _))
                {
                    return;
                }
                _flyoutEntrancesHandled.AddOrUpdate(app, null);
                if (flyout.TryAdmitEnteringRow(this, _container, ShouldAnimateStateChange()))
                {
                    _entranceHost = flyout;
                }
                return;
            }

            if (!ShouldAnimateStateChange())
            {
                return;
            }

            var duration = new Duration(TimeSpan.FromMilliseconds(EntranceAnimationDurationMs));
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

            AnimateContainerEntrance(duration, easing);

            Opacity = 0;
            RenderTransform = new TranslateTransform(0, EntranceSlideOffsetPx);

            BeginAnimation(OpacityProperty, new DoubleAnimation(1, duration)
            {
                EasingFunction = easing
            });

            ((TranslateTransform)RenderTransform).BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, duration)
            {
                EasingFunction = easing
            });
        }

        private void AnimateContainerEntrance(Duration duration, IEasingFunction easing)
        {
            var container = _container;
            if (container == null)
            {
                return;
            }

            var targetHeight = container.DesiredSize.Height > 0
                ? container.DesiredSize.Height
                : container.ActualHeight;
            if (targetHeight <= 0)
            {
                return;
            }

            container.BeginAnimation(HeightProperty, null);
            container.ClipToBounds = true;
            container.Height = 0;

            var heightAnimation = new DoubleAnimation(targetHeight, duration)
            {
                EasingFunction = easing
            };
            heightAnimation.Completed += (s, e) =>
            {
                container.BeginAnimation(HeightProperty, null);
                container.Height = double.NaN;
            };
            container.BeginAnimation(HeightProperty, heightAnimation);
        }

        /// <summary>
        /// Unloaded (session gone) or reused for another item: the flyout must stop counting this
        /// row. Window.GetWindow is already null in Unloaded, hence the stored host.
        /// </summary>
        private void LeaveFlyoutEntrance()
        {
            var host = _entranceHost;
            _entranceHost = null;
            host?.RemoveEnteringRow(this);
        }

        /// <summary>Flyout row entrance: hidden while the container waits at height 0.</summary>
        internal void PrepareEntrance()
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = 0;
            RenderTransform = null;
        }

        /// <summary>
        /// Flyout row entrance: the content fades in slightly behind the container growth and
        /// rises a few DIPs with it. Base values are final; the clocks simply stop.
        /// </summary>
        internal void BeginEntranceReveal()
        {
            Opacity = 1;
            BeginAnimation(OpacityProperty, FlyoutMotion.Create(0, 1, FlyoutMotion.RowContentFadeMs, FlyoutMotion.EaseOut, FlyoutMotion.RowContentDelayMs));

            var rise = new TranslateTransform();
            RenderTransform = rise;
            var riseAnimation = FlyoutMotion.Create(FlyoutMotion.RowContentRiseDip, 0, FlyoutMotion.RowGrowMs, FlyoutMotion.Decelerate);
            riseAnimation.Completed += (s, e) =>
            {
                if (ReferenceEquals(RenderTransform, rise))
                {
                    RenderTransform = null;
                }
            };
            rise.BeginAnimation(TranslateTransform.YProperty, riseAnimation);
        }

        /// <summary>Flyout row entrance cancelled or skipped: content fully visible, in place.</summary>
        internal void EndEntrance()
        {
            if (_hideAnimationStarted)
            {
                return;
            }

            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
            if (RenderTransform is TranslateTransform)
            {
                RenderTransform = null;
            }
        }

        private void BeginHideAnimation()
        {
            if (_hideAnimationStarted || !ShouldAnimateStateChange())
            {
                return;
            }

            _hideAnimationStarted = true;
            IsHitTestVisible = false;
            RenderTransform = new TranslateTransform(0, 0);
            RenderTransformOrigin = new Point(0.5, 0.5);
            ClipToBounds = true;

            var duration = new Duration(TimeSpan.FromMilliseconds(HideAnimationDurationMs));
            var easing = new QuinticEase { EasingMode = EasingMode.EaseOut };

            BeginAnimation(OpacityProperty, new DoubleAnimation(0, duration)
            {
                EasingFunction = easing
            });

            ((TranslateTransform)RenderTransform).BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(HideSlideOffsetPx, duration)
            {
                EasingFunction = easing
            });

            var rowScale = EnsureGridScaleTransform();
            rowScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.985, duration)
            {
                EasingFunction = easing
            });
            rowScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.985, duration)
            {
                EasingFunction = easing
            });
        }

        private void ResetTransientVisualState()
        {
            _hideAnimationStarted = false;
            IsHitTestVisible = true;
            ClipToBounds = true;
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
            RenderTransform = null;
            if (_container != null)
            {
                _container.BeginAnimation(HeightProperty, null);
                _container.Height = double.NaN;
            }
            var scale = EnsureGridScaleTransform();
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }

        private void BeginSoloFocusAnimation()
        {
            if (!ShouldAnimateStateChange())
            {
                return;
            }

            var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };
            var scale = EnsureGridScaleTransform();

            var compressDuration = TimeSpan.FromMilliseconds(80);
            var releaseDuration = TimeSpan.FromMilliseconds(220);

            var scaleDownX = new DoubleAnimation(0.988, compressDuration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            var scaleDownY = new DoubleAnimation(0.985, compressDuration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            scaleDownY.Completed += (s, e) =>
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, releaseDuration)
                {
                    EasingFunction = ease,
                });
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, releaseDuration)
                {
                    EasingFunction = ease,
                });
            };

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleDownX);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleDownY);
        }

        private bool ShouldAnimateStateChange()
        {
            return EarTrumpet.UI.Themes.Manager.Current?.AnimationsEnabled == true &&
                   (EarTrumpet.App.Settings?.UseSmoothVolumeAnimation ?? true) &&
                   !(EarTrumpet.App.Settings?.IsEffectiveEcoMode ?? false);
        }

        private double GetMutedOpacity()
        {
            return TryFindResource("MutedOpacity") is double opacity ? opacity : 0.4;
        }

        private ScaleTransform EnsureGridScaleTransform()
        {
            if (GridRoot.RenderTransform is ScaleTransform scale)
            {
                return scale;
            }

            scale = new ScaleTransform(1, 1);
            GridRoot.RenderTransform = scale;
            return scale;
        }

        private static void SetOpacity(UIElement element, double opacity)
        {
            element.BeginAnimation(OpacityProperty, null);
            element.Opacity = opacity;
        }

        private static void AnimateOpacity(UIElement element, double from, double to, int durationMs)
        {
            element.BeginAnimation(OpacityProperty, null);
            element.Opacity = from;

            var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };
            animation.Completed += (s, e) => element.Opacity = to;
            element.BeginAnimation(OpacityProperty, animation);
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

            var app = App;
            var betterTrumpetApp = Application.Current as EarTrumpet.App;
            if (betterTrumpetApp?.CollectionViewModel?.CanHideApp(app) == true)
            {
                betterTrumpetApp.CollectionViewModel.HideAppOnDevice(app);
                e.Handled = true;
            }
        }

        private void GridRoot_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var flyoutWindow = Window.GetWindow(this);
            var app = App;
            if (app == null || IsPointerOverVolumeSlider(e) || flyoutWindow == null || !flyoutWindow.IsVisible || !flyoutWindow.IsActive)
            {
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                return;
            }
            if (!(app.Parent is DeviceViewModel))
            {
                return;
            }

            ToggleSoloMuteOnDevice();
            BeginSoloFocusAnimation();

            app.IsMuted = false;
            e.Handled = true;
        }

        private void ToggleSoloMuteOnDevice()
        {
            var app = App;
            if (!(app?.Parent is DeviceViewModel parentDevice))
            {
                return;
            }

            var siblings = parentDevice.Apps.Where(x => x != null).ToList();
            var otherApps = siblings.Where(x => x.Id != app.Id).ToList();

            var isSoloStateForCurrentApp = !app.IsMuted && otherApps.All(x => x.IsMuted);
            if (isSoloStateForCurrentApp)
            {
                foreach (var otherApp in otherApps)
                {
                    otherApp.IsMuted = false;
                }

                app.IsMuted = false;
                return;
            }

            foreach (var otherApp in otherApps)
            {
                otherApp.IsMuted = true;
            }

            app.IsMuted = false;
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
