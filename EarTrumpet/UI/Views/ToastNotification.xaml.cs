using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EarTrumpet.UI.Views
{
    public partial class ToastNotification : Window
    {
        private DispatcherTimer _closeTimer;
        private const int DisplayDurationMs = 3000;

        public ToastNotification(string message, string icon = "\xE946")
        {
            InitializeComponent();

            MessageText.Text = message;
            IconText.Text = icon;

            // Position at bottom-right of screen
            var workingArea = SystemParameters.WorkArea;
            Left = workingArea.Right - Width - 20;
            Top = workingArea.Bottom - Height - 20;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Update position after window is sized
            var workingArea = SystemParameters.WorkArea;
            Left = workingArea.Right - ActualWidth - 20;
            Top = workingArea.Bottom - ActualHeight - 20;

            // Premium entrance animation
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var scaleX = new DoubleAnimation
            {
                From = 0.8,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
            };

            var scaleY = new DoubleAnimation
            {
                From = 0.8,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
            };

            var slideUp = new DoubleAnimation
            {
                From = 20,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            BeginAnimation(OpacityProperty, fadeIn);
            ScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleX);
            ScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleY);
            TranslateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideUp);

            // Auto-close after delay
            _closeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DisplayDurationMs)
            };
            _closeTimer.Tick += (s, args) =>
            {
                _closeTimer.Stop();
                CloseWithAnimation();
            };
            _closeTimer.Start();
        }

        private void CloseWithAnimation()
        {
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            var slideDown = new DoubleAnimation
            {
                From = 0,
                To = 20,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, e) => Close();

            BeginAnimation(OpacityProperty, fadeOut);
            TranslateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideDown);
        }

        public static ToastNotification Show(string message, string icon = "\xE946")
        {
            var toast = new ToastNotification(message, icon);
            toast.Show();
            return toast;
        }
    }
}
