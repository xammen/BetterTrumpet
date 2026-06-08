using EarTrumpet.UI.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EarTrumpet.UI.Views
{
    public partial class OnboardingWindow : Window
    {
        private readonly Duration _transitionDuration = new Duration(TimeSpan.FromMilliseconds(250));
        private readonly IEasingFunction _easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
        private int _lastPage = -1;

        public OnboardingWindow()
        {
            InitializeComponent();
            VersionText.Text = $"v{App.PackageVersion}";
            Loaded += OnLoaded;
            PreviewKeyDown += OnboardingWindow_KeyDown;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Initialize first page
            AnimatePageEntrance(Page0, Page0Translate);
            UpdateProgressBar();
            UpdateStepDots(0);

            if (DataContext is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += OnViewModelPropertyChanged;
            }

            Closed += (s, ev) =>
            {
                if (DataContext is INotifyPropertyChanged n)
                    n.PropertyChanged -= OnViewModelPropertyChanged;
            };
        }

        private void OnboardingWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm)
            {
                if (e.Key == Key.Escape)
                {
                    Skip_Click(null, null);
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter)
                {
                    Next_Click(null, null);
                    e.Handled = true;
                }
            }
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OnboardingViewModel.CurrentPage) && DataContext is OnboardingViewModel vm)
            {
                int newPage = vm.CurrentPage;
                bool forward = newPage > _lastPage;
                AnimatePageTransition(newPage, forward);
                UpdateProgressBar();
                UpdateStepDots(newPage);
                _lastPage = newPage;
            }
        }

        private void UpdateProgressBar()
        {
            if (DataContext is OnboardingViewModel vm)
            {
                var anim = new DoubleAnimation(vm.Progress, _transitionDuration)
                {
                    EasingFunction = _easeOut
                };
                ProgressScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            }
        }

        private void UpdateStepDots(int currentPage)
        {
            var dots = new[] { Dot0, Dot1, Dot2, Dot3 };
            var accentBrush = (SolidColorBrush)FindResource("Onboarding.AccentBrush");
            var dimBrush = new SolidColorBrush(Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF));

            for (int i = 0; i < dots.Length; i++)
            {
                var targetBrush = i == currentPage ? accentBrush : dimBrush;
                var anim = new ColorAnimation(((SolidColorBrush)targetBrush).Color, _transitionDuration)
                {
                    EasingFunction = _easeOut
                };
                ((SolidColorBrush)dots[i].Fill).BeginAnimation(SolidColorBrush.ColorProperty, anim);

                // Active dot slightly bigger
                var sizeAnim = new DoubleAnimation(i == currentPage ? 8 : 7, _transitionDuration)
                {
                    EasingFunction = _easeOut
                };
                dots[i].BeginAnimation(WidthProperty, sizeAnim);
                dots[i].BeginAnimation(HeightProperty, sizeAnim);
            }
        }

        private void AnimatePageTransition(int newPage, bool forward)
        {
            FrameworkElement page;
            TranslateTransform translate;

            switch (newPage)
            {
                case 0: page = Page0; translate = Page0Translate; break;
                case 1: page = Page1; translate = Page1Translate; break;
                case 2: page = Page2; translate = Page2Translate; break;
                case 3: page = Page3; translate = Page3Translate; break;
                default: return;
            }

            AnimatePageEntrance(page, translate, forward);
        }

        private void AnimatePageEntrance(FrameworkElement page, TranslateTransform translate, bool forward = true)
        {
            double startX = forward ? 40 : -40;

            var slideAnim = new DoubleAnimation(startX, 0, _transitionDuration)
            {
                EasingFunction = _easeOut
            };
            translate.BeginAnimation(TranslateTransform.XProperty, slideAnim);

            var fadeAnim = new DoubleAnimation(0, 1, _transitionDuration)
            {
                EasingFunction = _easeOut
            };
            page.BeginAnimation(OpacityProperty, fadeAnim);
        }

        // ═══ EVENT HANDLERS ═══

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private void Next_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm)
            {
                if (vm.IsLastPage)
                {
                    // Simple fade out and close
                    var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(200)))
                    {
                        EasingFunction = _easeOut
                    };
                    fadeOut.Completed += (s, ev) => vm.SkipCommand.Execute(null);
                    BeginAnimation(OpacityProperty, fadeOut);
                }
                else
                {
                    vm.NextCommand.Execute(null);
                }
            }
            if (e != null) e.Handled = true;
        }

        private void Back_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm)
                vm.BackCommand.Execute(null);
            e.Handled = true;
        }

        private void Skip_Click(object sender, MouseButtonEventArgs e)
        {
            var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(200)));
            fadeOut.Completed += (s, ev) =>
            {
                if (DataContext is OnboardingViewModel vm)
                    vm.SkipCommand.Execute(null);
            };
            BeginAnimation(OpacityProperty, fadeOut);
            if (e != null) e.Handled = true;
        }

        private void Theme0_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm)
            {
                vm.SelectedThemeIndex = 0;
                UpdateThemeCardSelection();
            }
            e.Handled = true;
        }

        private void Theme1_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm)
            {
                vm.SelectedThemeIndex = 1;
                UpdateThemeCardSelection();
            }
            e.Handled = true;
        }

        private void UpdateThemeCardSelection()
        {
            if (DataContext is OnboardingViewModel vm)
            {
                Theme0Card.Tag = vm.SelectedThemeIndex == 0 ? "Selected" : null;
                Theme1Card.Tag = vm.SelectedThemeIndex == 1 ? "Selected" : null;
            }
        }

        private void UpdateChannel0_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm) vm.UpdateChannelIndex = 0;
            e.Handled = true;
        }

        private void UpdateChannel1_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm) vm.UpdateChannelIndex = 1;
            e.Handled = true;
        }

        private void UpdateChannel3_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm) vm.UpdateChannelIndex = 3;
            e.Handled = true;
        }
    }
}
