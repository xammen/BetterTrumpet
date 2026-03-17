using EarTrumpet.UI.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace EarTrumpet.UI.Views
{
    public partial class OnboardingWindow : Window
    {
        private readonly Duration _slideDuration = new Duration(TimeSpan.FromMilliseconds(320));
        private readonly Duration _fastDuration = new Duration(TimeSpan.FromMilliseconds(200));
        private readonly IEasingFunction _easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
        private int _lastPage = -1;
        private bool _confettiTriggered;

        public OnboardingWindow()
        {
            InitializeComponent();
            VersionText.Text = $"v{App.PackageVersion}";
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(400)))
            {
                EasingFunction = _easeOut
            };
            BeginAnimation(OpacityProperty, fadeIn);

            AnimatePageEntrance(Page0, Page0Translate);
            UpdateProgressBar();

            UpdateStepDots(0);

            if (DataContext is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += OnViewModelPropertyChanged;
            }

            // Cleanup on close to avoid memory leaks
            Closed += (s, ev) =>
            {
                if (DataContext is INotifyPropertyChanged n)
                    n.PropertyChanged -= OnViewModelPropertyChanged;
                ConfettiCanvas.Children.Clear();
            };
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

                if (newPage == 4)
                {
                    AnimateReadyPage();
                }
                else if (newPage == 5)
                {
                    AnimateTrayPinPage();
                }
            }
        }

        private void UpdateProgressBar()
        {
            if (DataContext is OnboardingViewModel vm)
            {
                var anim = new DoubleAnimation(vm.Progress, new Duration(TimeSpan.FromMilliseconds(350)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                };
                ProgressScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            }
        }

        private void UpdateStepDots(int currentPage)
        {
            var dots = new[] { Dot0, Dot1, Dot2, Dot3, Dot4, Dot5 };
            var accentBrush = (SolidColorBrush)FindResource("AccentBrush");
            var dimBrush = new SolidColorBrush(Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF));

            for (int i = 0; i < dots.Length; i++)
            {
                var targetBrush = i == currentPage ? accentBrush : dimBrush;
                var anim = new ColorAnimation(((SolidColorBrush)targetBrush).Color,
                    new Duration(TimeSpan.FromMilliseconds(250)))
                {
                    EasingFunction = _easeOut
                };
                ((SolidColorBrush)dots[i].Fill).BeginAnimation(SolidColorBrush.ColorProperty, anim);

                // Active dot slightly bigger
                var sizeAnim = new DoubleAnimation(i == currentPage ? 7 : 6,
                    new Duration(TimeSpan.FromMilliseconds(200)))
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
                case 4: page = Page4; translate = Page4Translate; break;
                case 5: page = Page5; translate = Page5Translate; break;
                default: return;
            }

            AnimatePageEntrance(page, translate, forward);
        }

        private void AnimatePageEntrance(FrameworkElement page, TranslateTransform translate, bool forward = true)
        {
            double startX = forward ? 50 : -50;

            var slideAnim = new DoubleAnimation(startX, 0, _slideDuration)
            {
                EasingFunction = _easeOut
            };
            translate.BeginAnimation(TranslateTransform.XProperty, slideAnim);

            var fadeAnim = new DoubleAnimation(0, 1, _slideDuration)
            {
                EasingFunction = _easeOut
            };
            page.BeginAnimation(OpacityProperty, fadeAnim);
        }

        private void AnimateReadyPage()
        {
            var items = new[] { CheckItem0, CheckItem1, CheckItem2 };
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                item.Opacity = 0;
                item.RenderTransform = new TranslateTransform(0, 12);

                var delay = TimeSpan.FromMilliseconds(200 + i * 120);

                var fadeIn = new DoubleAnimation(0, 1, _fastDuration)
                {
                    BeginTime = delay,
                    EasingFunction = _easeOut
                };
                item.BeginAnimation(OpacityProperty, fadeIn);

                var slideUp = new DoubleAnimation(12, 0, _fastDuration)
                {
                    BeginTime = delay,
                    EasingFunction = _easeOut
                };
                ((TranslateTransform)item.RenderTransform).BeginAnimation(TranslateTransform.YProperty, slideUp);
            }

            var btnDelay = TimeSpan.FromMilliseconds(700);
            var pulse = new DoubleAnimation(1.0, 1.04, new Duration(TimeSpan.FromMilliseconds(300)))
            {
                BeginTime = btnDelay,
                AutoReverse = true,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            CtaButtonScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
            CtaButtonScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
        }

        private void AnimateTrayPinPage()
        {
            // Load the GIF programmatically (more reliable than pack URI in non-packaged mode)
            try
            {
                var uri = new Uri("pack://application:,,,/Assets/TrayPin.gif", UriKind.Absolute);
                XamlAnimatedGif.AnimationBehavior.SetSourceUri(TrayPinGif, uri);
                XamlAnimatedGif.AnimationBehavior.SetAutoStart(TrayPinGif, true);
                XamlAnimatedGif.AnimationBehavior.SetRepeatBehavior(TrayPinGif, RepeatBehavior.Forever);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"OnboardingWindow: Failed to load TrayPin.gif — {ex.Message}");
            }

            // Bouncing arrow animation — gentle up/down loop
            var bounce = new DoubleAnimation(0, 8, new Duration(TimeSpan.FromMilliseconds(600)))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            ArrowBounce.BeginAnimation(TranslateTransform.YProperty, bounce);

            // CTA button pulse
            var btnDelay = TimeSpan.FromMilliseconds(400);
            var pulse = new DoubleAnimation(1.0, 1.04, new Duration(TimeSpan.FromMilliseconds(300)))
            {
                BeginTime = btnDelay,
                AutoReverse = true,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            CtaButtonScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
            CtaButtonScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
        }

        // ═══ CONFETTI SYSTEM ═══

        private static readonly Color[] ConfettiColors = new[]
        {
            Color.FromRgb(0x3B, 0x9E, 0xFF), // blue (accent)
            Color.FromRgb(0x00, 0xBC, 0xD4), // cyan
            Color.FromRgb(0x7C, 0x4D, 0xFF), // purple
            Color.FromRgb(0xFF, 0x6D, 0x3B), // orange
            Color.FromRgb(0x4C, 0xAF, 0x50), // green
            Color.FromRgb(0xFF, 0x45, 0x6B), // pink-red
            Color.FromRgb(0xFF, 0xC8, 0x57), // gold
        };

        private void LaunchConfetti()
        {
            if (_confettiTriggered) return;
            _confettiTriggered = true;

            var rand = new Random();
            double canvasW = ConfettiCanvas.ActualWidth > 0 ? ConfettiCanvas.ActualWidth : 520;
            double canvasH = ConfettiCanvas.ActualHeight > 0 ? ConfettiCanvas.ActualHeight : 640;

            // Spawn confetti in 3 bursts for a more natural effect
            for (int burst = 0; burst < 3; burst++)
            {
                int burstDelay = burst * 150; // ms between bursts
                int piecesInBurst = burst == 0 ? 28 : (burst == 1 ? 20 : 12);

                for (int i = 0; i < piecesInBurst; i++)
                {
                    SpawnConfettiPiece(rand, canvasW, canvasH, burstDelay, i);
                }
            }
        }

        private void SpawnConfettiPiece(Random rand, double canvasW, double canvasH, int burstDelayMs, int index)
        {
            var color = ConfettiColors[rand.Next(ConfettiColors.Length)];

            // Mix of rectangles and small squares
            bool isSquare = rand.NextDouble() > 0.6;
            double w = isSquare ? 6 + rand.NextDouble() * 3 : 4 + rand.NextDouble() * 3;
            double h = isSquare ? w : 8 + rand.NextDouble() * 6;

            var piece = new Rectangle
            {
                Width = w,
                Height = h,
                RadiusX = 1,
                RadiusY = 1,
                Fill = new SolidColorBrush(color),
                Opacity = 0.85 + rand.NextDouble() * 0.15,
                RenderTransformOrigin = new Point(0.5, 0.5),
                IsHitTestVisible = false
            };

            // Start position: spread across the top, some from edges
            double startX = rand.NextDouble() * canvasW;
            double startY = -10 - rand.NextDouble() * 40;

            Canvas.SetLeft(piece, startX);
            Canvas.SetTop(piece, startY);

            var transformGroup = new TransformGroup();
            var rotate = new RotateTransform(rand.NextDouble() * 360);
            var scale = new ScaleTransform(1, 1);
            transformGroup.Children.Add(rotate);
            transformGroup.Children.Add(scale);
            piece.RenderTransform = transformGroup;

            ConfettiCanvas.Children.Add(piece);

            // Animation timing
            var delay = TimeSpan.FromMilliseconds(burstDelayMs + rand.Next(100));
            double fallDuration = 2800 + rand.NextDouble() * 2200; // 2.8s to 5s — slow gentle fall

            // Fall down (Y)
            double endY = canvasH + 20;
            var fallAnim = new DoubleAnimation(startY, endY, new Duration(TimeSpan.FromMilliseconds(fallDuration)))
            {
                BeginTime = delay,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            fallAnim.Completed += (s, e) =>
            {
                ConfettiCanvas.Children.Remove(piece);
            };

            // Horizontal sway (X) — gentle sine-like drift
            double swayAmount = 30 + rand.NextDouble() * 50;
            double swayDir = rand.NextDouble() > 0.5 ? 1 : -1;
            var swayAnim = new DoubleAnimation(startX, startX + swayAmount * swayDir,
                new Duration(TimeSpan.FromMilliseconds(fallDuration)))
            {
                BeginTime = delay,
                AutoReverse = false,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            // Rotation — continuous spin
            double rotEnd = rotate.Angle + (200 + rand.NextDouble() * 400) * (rand.NextDouble() > 0.5 ? 1 : -1);
            var rotAnim = new DoubleAnimation(rotate.Angle, rotEnd,
                new Duration(TimeSpan.FromMilliseconds(fallDuration)))
            {
                BeginTime = delay
            };

            // Flip effect via ScaleX oscillation (simulates 3D tumble)
            var flipAnim = new DoubleAnimation(1, -1,
                new Duration(TimeSpan.FromMilliseconds(300 + rand.Next(400))))
            {
                BeginTime = delay,
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(TimeSpan.FromMilliseconds(fallDuration))
            };

            // Fade out near the bottom
            var fadeAnim = new DoubleAnimation(piece.Opacity, 0,
                new Duration(TimeSpan.FromMilliseconds(600)))
            {
                BeginTime = delay + TimeSpan.FromMilliseconds(fallDuration - 600)
            };

            piece.BeginAnimation(Canvas.TopProperty, fallAnim);
            piece.BeginAnimation(Canvas.LeftProperty, swayAnim);
            rotate.BeginAnimation(RotateTransform.AngleProperty, rotAnim);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, flipAnim);
            piece.BeginAnimation(OpacityProperty, fadeAnim);
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
                    // Launch confetti, then close after a short delay
                    LaunchConfetti();

                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) };
                    timer.Tick += (s, ev) =>
                    {
                        timer.Stop();
                        var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(400)))
                        {
                            EasingFunction = _easeOut
                        };
                        fadeOut.Completed += (s2, ev2) => vm.SkipCommand.Execute(null);
                        BeginAnimation(OpacityProperty, fadeOut);
                    };
                    timer.Start();
                }
                else
                {
                    vm.NextCommand.Execute(null);
                }
            }
            e.Handled = true;
        }

        private void Back_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm)
                vm.BackCommand.Execute(null);
            e.Handled = true;
        }

        private void Skip_Click(object sender, MouseButtonEventArgs e)
        {
            var fadeOut = new DoubleAnimation(1, 0, _fastDuration);
            fadeOut.Completed += (s, ev) =>
            {
                if (DataContext is OnboardingViewModel vm)
                    vm.SkipCommand.Execute(null);
            };
            BeginAnimation(OpacityProperty, fadeOut);
            e.Handled = true;
        }

        private void DeviceCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm && sender is FrameworkElement fe && fe.Tag is AudioDeviceChoice choice)
            {
                foreach (var dev in vm.AudioDevices)
                    dev.IsDefault = false;
                choice.IsDefault = true;
                vm.SelectedDevice = choice;
            }
            e.Handled = true;
        }

        private void Theme0_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm) vm.SelectedThemeIndex = 0;
            e.Handled = true;
        }

        private void Theme1_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm) vm.SelectedThemeIndex = 1;
            e.Handled = true;
        }

        private void OnboardingUpdateChannel0_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm) vm.UpdateChannelIndex = 0;
            e.Handled = true;
        }

        private void OnboardingUpdateChannel1_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm) vm.UpdateChannelIndex = 1;
            e.Handled = true;
        }

        private void OnboardingUpdateChannel3_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OnboardingViewModel vm) vm.UpdateChannelIndex = 3;
            e.Handled = true;
        }
    }
}
