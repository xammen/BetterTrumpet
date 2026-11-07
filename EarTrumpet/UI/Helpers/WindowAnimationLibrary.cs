using EarTrumpet.DataModel;
using EarTrumpet.Extensions;
using EarTrumpet.Interop;
using EarTrumpet.Interop.Helpers;
using EarTrumpet.UI.Themes;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EarTrumpet.UI.Helpers
{
    public class WindowAnimationLibrary
    {
        // Fades used by the regular (non-flyout) windows. The tray flyout has its own motion,
        // see FlyoutMotion / FlyoutSurfaceMotion.
        const int _entranceDurationMs = 165;
        const int _exitDurationMs = 105;

        public static void BeginWindowExitAnimation(Window window, Action completed)
        {
            var onCompleted = new EventHandler((s, e) =>
            {
                window.Cloak();
                completed();
            });

            if (!Manager.Current.AnimationsEnabled || !SystemSettings.IsTransparencyEnabled)
            {
                window.Dispatcher.BeginInvoke((Action)(() =>
                {
                    onCompleted(null, null);
                }));
            }
            else
            {
                var fadeAnimation = new DoubleAnimation
                {
                    Duration = new Duration(TimeSpan.FromMilliseconds(_exitDurationMs)),
                    FillBehavior = FillBehavior.Stop,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
                    From = 1,
                    To = 0,
                };
                fadeAnimation.Completed += (s, e) => { window.Opacity = 0; };

                Storyboard.SetTarget(fadeAnimation, window);
                Storyboard.SetTargetProperty(fadeAnimation, new PropertyPath(Window.OpacityProperty));

                var storyboard = new Storyboard();
                storyboard.FillBehavior = FillBehavior.Stop;
                storyboard.Children.Add(fadeAnimation);
                storyboard.Completed += onCompleted;
                storyboard.Begin(window);
            }
        }

        public static void BeginWindowEntranceAnimation(Window window, Action completed, double fromOpacity = 0)
        {
            var onCompleted = new EventHandler((s, e) =>
            {
                completed();
            });

            if (!Manager.Current.AnimationsEnabled)
            {
                window.Cloak(false);
                onCompleted(null, null);
                return;
            }

            var fadeAnimation = new DoubleAnimation
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(_entranceDurationMs)),
                FillBehavior = FillBehavior.Stop,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                From = fromOpacity,
                To = 1,
            };
            fadeAnimation.Completed += (s, e) => { window.Opacity = 1; };

            Storyboard.SetTarget(fadeAnimation, window);
            Storyboard.SetTargetProperty(fadeAnimation, new PropertyPath(Window.OpacityProperty));

            var storyboard = new Storyboard();
            storyboard.FillBehavior = FillBehavior.Stop;
            storyboard.Children.Add(fadeAnimation);
            storyboard.Completed += onCompleted;

            window.Cloak(false);

            storyboard.Begin(window);
        }

        public static void BringTaskbarToFront()
        {
            User32.SetForegroundWindow(WindowsTaskbar.GetHwnd());
        }
    }
}
