using EarTrumpet.UI.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EarTrumpet.UI.Views
{
    public partial class OnboardingWindow : Window
    {
        private readonly Duration _pageDuration = new Duration(TimeSpan.FromMilliseconds(260));
        private readonly Duration _dotDuration = new Duration(TimeSpan.FromMilliseconds(180));
        private readonly IEasingFunction _ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        private int _lastPage = 0;

        public OnboardingWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += OnViewModelPropertyChanged;
            }

            UpdateProgressBar();
            UpdateDots();
            StartAmbientAnimation();
            AnimatePageIn(Page1Transform, true);
        }

        private void StartAmbientAnimation()
        {
            var anim = new DoubleAnimation
            {
                From = 0.58,
                To = 0.88,
                Duration = TimeSpan.FromSeconds(5),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            AmbientGradient.BeginAnimation(RadialGradientBrush.RadiusXProperty, anim);
            AmbientGradient.BeginAnimation(RadialGradientBrush.RadiusYProperty, anim);
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!(DataContext is OnboardingViewModel vm) || e.PropertyName != nameof(OnboardingViewModel.CurrentPage))
            {
                return;
            }

            var newPage = vm.CurrentPage;
            var forward = newPage > _lastPage;
            _lastPage = newPage;

            UpdateProgressBar();
            UpdateDots();

            switch (newPage)
            {
                case 0:
                    AnimatePageIn(Page1Transform, forward);
                    break;
                case 1:
                    AnimatePageIn(Page2Transform, forward);
                    break;
                case 2:
                    AnimatePageIn(Page3Transform, forward);
                    break;
                case 3:
                    AnimatePageIn(Page4Transform, forward);
                    break;
                case 4:
                    AnimatePageIn(Page5Transform, forward);
                    break;
            }
        }

        private void AnimatePageIn(TranslateTransform transform, bool forward)
        {
            if (transform == null) return;

            var fromX = forward ? 28.0 : -28.0;
            var anim = new DoubleAnimation(fromX, 0, _pageDuration)
            {
                EasingFunction = _ease
            };

            transform.BeginAnimation(TranslateTransform.XProperty, anim);
        }

        private void UpdateProgressBar()
        {
            if (!(DataContext is OnboardingViewModel vm)) return;

            var anim = new DoubleAnimation(vm.Progress, _pageDuration)
            {
                EasingFunction = _ease
            };

            ProgressScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        }

        private void UpdateDots()
        {
            if (!(DataContext is OnboardingViewModel vm)) return;

            var dots = new[] { Dot0, Dot1, Dot2, Dot3, Dot4 };
            for (var i = 0; i < dots.Length; i++)
            {
                var active = i == vm.CurrentPage;
                var color = active ? Color.FromRgb(59, 158, 255) : Color.FromRgb(52, 54, 64);

                var brush = new SolidColorBrush(color);
                dots[i].Background = brush;

                var scale = new ScaleTransform(active ? 1.25 : 1.0, active ? 1.25 : 1.0);
                dots[i].RenderTransform = scale;
                dots[i].RenderTransformOrigin = new Point(0.5, 0.5);

                var opacityAnim = new DoubleAnimation(active ? 1.0 : 0.65, _dotDuration)
                {
                    EasingFunction = _ease
                };
                dots[i].BeginAnimation(OpacityProperty, opacityAnim);
            }
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm)
            {
                vm.NextCommand.Execute(null);
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm)
            {
                vm.BackCommand.Execute(null);
            }
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm)
            {
                vm.SkipCommand.Execute(null);
            }
        }

        private void DeviceCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm && sender is FrameworkElement fe && fe.Tag is AudioDeviceChoice choice)
            {
                vm.SelectedDevice = choice;
            }
        }

        private void ThemeCard_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm && TryGetTagIndex(sender, out var index))
            {
                vm.SelectedThemeIndex = index;
            }
        }

        private static bool TryGetTagIndex(object sender, out int index)
        {
            index = 0;
            return sender is FrameworkElement fe && int.TryParse(fe.Tag?.ToString(), out index);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (DataContext is OnboardingViewModel vm)
                {
                    vm.SkipCommand.Execute(null);
                }
            }
        }
    }
}
