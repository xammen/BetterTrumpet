using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EarTrumpet.UI.Views
{
    public partial class ChangelogWindow : Window
    {
        private static readonly Color _accent = Color.FromRgb(0x3B, 0x9E, 0xFF);
        private static readonly Brush _t1 = new SolidColorBrush(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF));
        private static readonly Brush _t2 = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF));
        private static readonly Brush _t3 = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        private static readonly Brush _surfaceBrush = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1E));

        public ChangelogWindow()
        {
            InitializeComponent();
            VersionBadge.Text = $"v{App.PackageVersion}";
            BuildContent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Staggered fade-in for content sections
            int i = 0;
            foreach (UIElement child in ContentPanel.Children)
            {
                child.Opacity = 0;
                child.RenderTransform = new TranslateTransform(0, 20);

                var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
                {
                    BeginTime = TimeSpan.FromMilliseconds(80 * i),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var slide = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(350))
                {
                    BeginTime = TimeSpan.FromMilliseconds(80 * i),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                child.BeginAnimation(OpacityProperty, fade);
                ((TranslateTransform)child.RenderTransform).BeginAnimation(TranslateTransform.YProperty, slide);
                i++;
            }

            // Single subtle shimmer on main title — one pass, then stays white
            ApplyTitleShimmer();
        }

        /// <summary>
        /// A single slow light sweep across "Quoi de neuf", then settles to plain white.
        /// Not a loop. Just a moment of polish.
        /// </summary>
        private void ApplyTitleShimmer()
        {
            var baseColor = Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF);
            var glint = Color.FromArgb(0xFF, 0xBB, 0xDD, 0xFF); // very soft blue-white

            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                MappingMode = BrushMappingMode.RelativeToBoundingBox,
            };
            // Tight highlight band — most of the text stays base color
            brush.GradientStops.Add(new GradientStop(baseColor, 0.0));
            brush.GradientStops.Add(new GradientStop(baseColor, 0.4));
            brush.GradientStops.Add(new GradientStop(glint, 0.48));
            brush.GradientStops.Add(new GradientStop(Colors.White, 0.5));
            brush.GradientStops.Add(new GradientStop(glint, 0.52));
            brush.GradientStops.Add(new GradientStop(baseColor, 0.6));
            brush.GradientStops.Add(new GradientStop(baseColor, 1.0));

            var transform = new TranslateTransform(-350, 0);
            brush.Transform = transform;
            MainTitle.Foreground = brush;

            // One pass: slow sweep left to right, ease in/out, no repeat
            var sweep = new DoubleAnimation
            {
                From = -350,
                To = 500,
                Duration = TimeSpan.FromSeconds(1.8),
                BeginTime = TimeSpan.FromSeconds(0.6), // wait for fade-in to finish
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
            };
            // After the sweep, revert to solid foreground (clean)
            sweep.Completed += (s, ev) =>
            {
                MainTitle.Foreground = _t1;
            };
            transform.BeginAnimation(TranslateTransform.XProperty, sweep);
        }

        private void BuildContent()
        {
            // ── HERO FEATURE ──
            AddHeroCard(
                "\xE7BE", "Onboarding premium",
                "Assistant de configuration en 5 pages avec pr\u00e9visualisation audio, choix d'appareil et personnalisation du th\u00e8me.",
                new[] {
                    new MiniTag("5 pages", "#1A3B9EFF"),
                    new MiniTag("Animations", "#1A7C4DFF"),
                    new MiniTag("Confetti", "#1A9C27B0"),
                });

            // ── SECTION: Contr\u00f4le audio ──
            AddSectionHeader("\xE767", "Contr\u00f4le audio");
            AddFeatureRow(
                new Feature("\xE7A7", "Undo / Redo", "Ctrl+Z / Ctrl+Y sur tous les sliders"),
                new Feature("\xE8AB", "Switch rapide", "Raccourci pour changer de p\u00e9riph\u00e9rique")
            );
            AddFeatureRow(
                new Feature("\xE9E9", "Profils volume", "Sauvegarder et restaurer vos configurations"),
                new Feature("\xE718", "Pin Flyout", "Garder le flyout ouvert en permanence")
            );

            // ── SECTION: Exp\u00e9rience ──
            AddSectionHeader("\xE790", "Exp\u00e9rience");
            AddFeatureRow(
                new Feature("\xE790", "Th\u00e8mes complets", "Sliders, fond, texte, accent \u2014 tout custom"),
                new Feature("\xE8D6", "Media popup", "Info now-playing au survol du tray")
            );
            AddFeatureRow(
                new Feature("\xE896", "Mises \u00e0 jour", "V\u00e9rification auto avec badge et banni\u00e8re"),
                new Feature("\xEA80", "Mode \u00e9co", "R\u00e9duit le CPU sur batterie")
            );

            // ── SECTION: Sous le capot ──
            AddSectionHeader("\xE756", "Sous le capot");
            AddCompactList(new[] {
                new CompactItem("\xE756", "CLI", "19 commandes pour tout contr\u00f4ler depuis le terminal"),
                new CompactItem("\xE730", "Crash protection", "Sentry GDPR + health monitoring"),
                new CompactItem("\xE8B7", "Export / Import", "Sauvegardez et restaurez tous vos r\u00e9glages"),
                new CompactItem("\xE7B5", "Animations fluides", "Vitesse d'animation et FPS configurables"),
            });
        }

        // ──────────────────────────────────────────────
        // Hero card — large featured item
        // ──────────────────────────────────────────────
        private void AddHeroCard(string glyph, string title, string description, MiniTag[] tags)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20, 18, 20, 18),
                Margin = new Thickness(0, 0, 0, 24),
                Background = _surfaceBrush,
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
            };

            var sp = new StackPanel();

            // Icon
            var iconBorder = new Border
            {
                Width = 40, Height = 40, CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 0, 14),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromArgb(0x18, _accent.R, _accent.G, _accent.B)),
            };
            iconBorder.Child = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 18,
                Foreground = new SolidColorBrush(_accent),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            sp.Children.Add(iconBorder);

            sp.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = _t1,
                Margin = new Thickness(0, 0, 0, 6),
            });

            sp.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 12.5,
                Foreground = _t2,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 19,
                Margin = new Thickness(0, 0, 0, 14),
            });

            // Tags
            var tagPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var tag in tags)
            {
                var tagBorder = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(0, 0, 6, 0),
                    Background = (Brush)new BrushConverter().ConvertFrom(tag.BgColor),
                };
                tagBorder.Child = new TextBlock
                {
                    Text = tag.Label,
                    FontSize = 11,
                    FontWeight = FontWeights.Medium,
                    Foreground = new SolidColorBrush(_accent),
                };
                tagPanel.Children.Add(tagBorder);
            }
            sp.Children.Add(tagPanel);

            card.Child = sp;
            ContentPanel.Children.Add(card);
        }

        // ──────────────────────────────────────────────
        // Section header
        // ──────────────────────────────────────────────
        private void AddSectionHeader(string glyph, string title)
        {
            var sp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 4, 0, 12),
            };

            sp.Children.Add(new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 13,
                Foreground = new SolidColorBrush(_accent),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
            });

            sp.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = _t2,
                VerticalAlignment = VerticalAlignment.Center,
            });

            ContentPanel.Children.Add(sp);
        }

        // ──────────────────────────────────────────────
        // Feature row — 2 cards side by side
        // ──────────────────────────────────────────────
        private void AddFeatureRow(Feature left, Feature right)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftCard = MakeFeatureCard(left);
            Grid.SetColumn(leftCard, 0);
            grid.Children.Add(leftCard);

            var rightCard = MakeFeatureCard(right);
            Grid.SetColumn(rightCard, 2);
            grid.Children.Add(rightCard);

            ContentPanel.Children.Add(grid);
        }

        private Border MakeFeatureCard(Feature f)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 14, 12),
                Background = _surfaceBrush,
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
            };

            var sp = new StackPanel();

            var iconBorder = new Border
            {
                Width = 28, Height = 28, CornerRadius = new CornerRadius(7),
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromArgb(0x10, _accent.R, _accent.G, _accent.B)),
            };
            iconBorder.Child = new TextBlock
            {
                Text = f.Glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                Foreground = new SolidColorBrush(_accent),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            sp.Children.Add(iconBorder);

            sp.Children.Add(new TextBlock
            {
                Text = f.Title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = _t1,
                Margin = new Thickness(0, 0, 0, 3),
            });

            sp.Children.Add(new TextBlock
            {
                Text = f.Description,
                FontSize = 11.5,
                Foreground = _t3,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 16,
            });

            card.Child = sp;
            return card;
        }

        // ──────────────────────────────────────────────
        // Compact list — "under the hood" items
        // ──────────────────────────────────────────────
        private void AddCompactList(CompactItem[] items)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = _surfaceBrush,
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0, 4, 0, 4),
                Margin = new Thickness(0, 0, 0, 8),
            };

            var sp = new StackPanel();
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var row = new Grid { Margin = new Thickness(16, 8, 16, 8) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var icon = new TextBlock
                {
                    Text = item.Glyph,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 12,
                    Foreground = _t3,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(icon, 0);
                row.Children.Add(icon);

                var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var titleBlock = new TextBlock
                {
                    FontSize = 12.5,
                    Foreground = _t1,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                titleBlock.Inlines.Add(new System.Windows.Documents.Run(item.Title) { FontWeight = FontWeights.Medium });
                titleBlock.Inlines.Add(new System.Windows.Documents.Run($"  \u2014  {item.Description}") { Foreground = _t3 });
                textPanel.Children.Add(titleBlock);
                Grid.SetColumn(textPanel, 1);
                row.Children.Add(textPanel);

                sp.Children.Add(row);

                if (i < items.Length - 1)
                {
                    sp.Children.Add(new Border
                    {
                        Height = 1,
                        Background = new SolidColorBrush(Color.FromArgb(0x08, 0xFF, 0xFF, 0xFF)),
                        Margin = new Thickness(16, 0, 16, 0),
                    });
                }
            }

            card.Child = sp;
            ContentPanel.Children.Add(card);
        }

        // ──────────────────────────────────────────────
        // Events
        // ──────────────────────────────────────────────
        private void TitleBar_Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Close_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Close();
        }

        private void Continue_Click(object sender, MouseButtonEventArgs e)
        {
            Close();
        }

        // ──────────────────────────────────────────────
        // Data types
        // ──────────────────────────────────────────────
        private struct Feature
        {
            public string Glyph, Title, Description;
            public Feature(string glyph, string title, string description)
            { Glyph = glyph; Title = title; Description = description; }
        }

        private struct MiniTag
        {
            public string Label, BgColor;
            public MiniTag(string label, string bgColor)
            { Label = label; BgColor = bgColor; }
        }

        private struct CompactItem
        {
            public string Glyph, Title, Description;
            public CompactItem(string glyph, string title, string description)
            { Glyph = glyph; Title = title; Description = description; }
        }
    }
}
