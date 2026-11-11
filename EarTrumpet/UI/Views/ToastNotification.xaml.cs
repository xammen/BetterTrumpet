using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EarTrumpet.UI.Views
{
    public partial class ToastNotification : Window
    {
        public const int DefaultDisplayDurationMs = 3000;

        private DispatcherTimer _closeTimer;
        private readonly int _displayDurationMs;

        public ToastNotification(string message, string icon = "\xE767", int durationMs = DefaultDisplayDurationMs, string title = null)
        {
            InitializeComponent();

            _displayDurationMs = durationMs > 0 ? durationMs : DefaultDisplayDurationMs;

            TitleText.Text = title;
            TitleText.Visibility = string.IsNullOrEmpty(title) ? Visibility.Collapsed : Visibility.Visible;
            MessageText.Text = message;
            IconText.Text = icon;

            // Start invisible for smooth entrance
            Opacity = 0;

            // Position at bottom-right of screen with margin
            var workingArea = SystemParameters.WorkArea;
            Left = workingArea.Right - 340;
            Top = workingArea.Bottom - 100;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Update position after window is sized
            var workingArea = SystemParameters.WorkArea;
            Left = workingArea.Right - ActualWidth - 20;
            Top = workingArea.Bottom - ActualHeight - 20;

            // Smooth entrance animation - slide up + fade in
            ShowAnimation();

            // Auto-close timer
            _closeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_displayDurationMs)
            };
            _closeTimer.Tick += (s, args) =>
            {
                _closeTimer.Stop();
                CloseWithAnimation();
            };
            _closeTimer.Start();
        }

        private void ShowAnimation()
        {
            // Use Storyboard for better performance and smoother animations
            var storyboard = new Storyboard();

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var slideUp = new DoubleAnimation
            {
                From = 24,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(260),
                EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 6 }
            };

            var scaleX = new DoubleAnimation
            {
                From = 0.96,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(260),
                EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 6 }
            };

            var scaleY = new DoubleAnimation
            {
                From = 0.96,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(260),
                EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 6 }
            };

            Storyboard.SetTarget(fadeIn, this);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));

            Storyboard.SetTarget(slideUp, TranslateTransform);
            Storyboard.SetTargetProperty(slideUp, new PropertyPath("Y"));

            Storyboard.SetTarget(scaleX, ScaleTransform);
            Storyboard.SetTargetProperty(scaleX, new PropertyPath("ScaleX"));

            Storyboard.SetTarget(scaleY, ScaleTransform);
            Storyboard.SetTargetProperty(scaleY, new PropertyPath("ScaleY"));

            storyboard.Children.Add(fadeIn);
            storyboard.Children.Add(slideUp);
            storyboard.Children.Add(scaleX);
            storyboard.Children.Add(scaleY);

            storyboard.Begin();
        }

        private void CloseWithAnimation()
        {
            var storyboard = new Storyboard();

            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            var slideDown = new DoubleAnimation
            {
                From = 0,
                To = 20,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, e) => Close();

            Storyboard.SetTarget(fadeOut, this);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(OpacityProperty));

            Storyboard.SetTarget(slideDown, TranslateTransform);
            Storyboard.SetTargetProperty(slideDown, new PropertyPath("Y"));

            storyboard.Children.Add(fadeOut);
            storyboard.Children.Add(slideDown);

            storyboard.Begin();
        }

        public void AnimateToPosition(double newTop)
        {
            var topAnimation = new DoubleAnimation
            {
                From = Top,
                To = newTop,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 5 }
            };

            topAnimation.Completed += (s, e) => Top = newTop;
            BeginAnimation(TopProperty, topAnimation);
        }

        public static ToastNotification Show(string message, string icon = "\xE767")
        {
            var toast = new ToastNotification(message, icon);
            toast.Show();
            return toast;
        }
    }
}
