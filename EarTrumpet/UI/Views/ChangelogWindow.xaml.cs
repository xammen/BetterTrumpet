using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Newtonsoft.Json.Linq;

namespace EarTrumpet.UI.Views
{
    public partial class ChangelogWindow : Window
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/xammen/BetterTrumpet/releases/latest";

        private static readonly Color _accent = Color.FromRgb(0x3B, 0x9E, 0xFF);
        private static readonly Brush _t1 = new SolidColorBrush(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF));
        private static readonly Brush _t2 = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF));
        private static readonly Brush _t3 = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        private static readonly Brush _surfaceBrush = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1E));

        public ChangelogWindow()
        {
            InitializeComponent();
            VersionBadge.Text = $"v{App.PackageVersion}";
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Show loading state
            AddLoadingIndicator();

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "BetterTrumpet-Changelog");
                    client.Timeout = TimeSpan.FromSeconds(10);

                    var response = await client.GetStringAsync(GitHubApiUrl);
                    var json = JObject.Parse(response);

                    var body = json["body"]?.ToString() ?? "";
                    var tagName = json["tag_name"]?.ToString() ?? "";

                    ContentPanel.Children.Clear();

                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        ParseMarkdownToUI(body);
                    }
                    else
                    {
                        AddFallbackContent("Aucune note de version disponible.");
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"ChangelogWindow: Failed to fetch release notes — {ex.Message}");
                ContentPanel.Children.Clear();
                AddFallbackContent("Impossible de charger les notes de version.\nVérifiez votre connexion internet.");
            }

            // Animate content in
            AnimateContentIn();
            ApplyTitleShimmer();
        }

        /// <summary>
        /// Parse GitHub release markdown into WPF UI elements.
        /// Handles: ## H2, ### H3, **bold**, - bullet, plain text
        /// </summary>
        private void ParseMarkdownToUI(string markdown)
        {
            var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                // ## H2 — Main title (e.g. "## BetterTrumpet v3.0.3")
                if (line.StartsWith("## "))
                {
                    // Skip the main title since we already show version in the badge
                    continue;
                }

                // ### H3 — Section header (e.g. "### Fix", "### New")
                if (line.StartsWith("### "))
                {
                    var title = line.Substring(4).Trim();
                    AddSectionHeader(GetSectionGlyph(title), title);
                    continue;
                }

                // - Bullet item → individual card per item
                if (line.StartsWith("- "))
                {
                    var text = line.Substring(2).Trim();
                    AddFeatureCard(text);
                    continue;
                }

                // Plain text paragraph
                ContentPanel.Children.Add(new TextBlock
                {
                    Text = StripMarkdownFormatting(line),
                    FontSize = 13,
                    Foreground = _t2,
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 20,
                    Margin = new Thickness(0, 0, 0, 8),
                });
            }
        }

        /// <summary>
        /// Renders a bullet item as an individual card.
        /// If the text contains **bold** — title, the bold becomes the card title
        /// and the rest becomes the description below it.
        /// </summary>
        private void AddFeatureCard(string markdownText)
        {
            // Parse "**Title** — description" pattern
            string title = null;
            string description = null;

            int boldStart = markdownText.IndexOf("**");
            int boldEnd = boldStart >= 0 ? markdownText.IndexOf("**", boldStart + 2) : -1;

            if (boldStart >= 0 && boldEnd > boldStart)
            {
                title = markdownText.Substring(boldStart + 2, boldEnd - boldStart - 2);
                var rest = markdownText.Substring(boldEnd + 2).Trim();
                // Strip leading dash/emdash separator
                if (rest.StartsWith("—") || rest.StartsWith("-") || rest.StartsWith("–"))
                    rest = rest.Substring(1).Trim();
                description = rest;
            }
            else
            {
                description = StripMarkdownFormatting(markdownText);
            }

            var card = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = _surfaceBrush,
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x08, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 6),
            };

            var stack = new StackPanel();

            if (title != null)
            {
                // Title row with accent dot
                var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                titleRow.Children.Add(new Border
                {
                    Width = 6, Height = 6,
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(_accent),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0),
                });
                titleRow.Children.Add(new TextBlock
                {
                    Text = title,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = _t1,
                });
                stack.Children.Add(titleRow);
            }

            if (!string.IsNullOrEmpty(description))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = description,
                    FontSize = 12,
                    Foreground = _t3,
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 18,
                    Margin = title != null ? new Thickness(14, 0, 0, 0) : new Thickness(0),
                });
            }

            card.Child = stack;
            ContentPanel.Children.Add(card);
        }

        /// <summary>
        /// Parse **bold** and regular text into TextBlock inlines.
        /// </summary>
        private void ParseInlineMarkdown(TextBlock textBlock, string text)
        {
            int pos = 0;
            while (pos < text.Length)
            {
                int boldStart = text.IndexOf("**", pos);
                if (boldStart < 0)
                {
                    // Rest is plain text
                    textBlock.Inlines.Add(new System.Windows.Documents.Run(text.Substring(pos)) { Foreground = _t2 });
                    break;
                }

                // Plain text before bold
                if (boldStart > pos)
                {
                    textBlock.Inlines.Add(new System.Windows.Documents.Run(text.Substring(pos, boldStart - pos)) { Foreground = _t2 });
                }

                int boldEnd = text.IndexOf("**", boldStart + 2);
                if (boldEnd < 0)
                {
                    // Unclosed bold — treat rest as plain
                    textBlock.Inlines.Add(new System.Windows.Documents.Run(text.Substring(boldStart)) { Foreground = _t2 });
                    break;
                }

                var boldText = text.Substring(boldStart + 2, boldEnd - boldStart - 2);
                textBlock.Inlines.Add(new System.Windows.Documents.Run(boldText)
                {
                    FontWeight = FontWeights.SemiBold,
                    Foreground = _t1,
                });

                pos = boldEnd + 2;
            }
        }

        private string StripMarkdownFormatting(string text)
        {
            return text.Replace("**", "");
        }

        private string GetSectionGlyph(string sectionTitle)
        {
            var lower = sectionTitle.ToLowerInvariant();
            if (lower.Contains("fix")) return "\xE90F";       // wrench
            if (lower.Contains("new") || lower.Contains("feat") || lower.Contains("nouveau")) return "\xE710"; // add
            if (lower.Contains("break")) return "\xE7BA";     // warning
            if (lower.Contains("perf")) return "\xE9F5";      // speedometer
            if (lower.Contains("under") || lower.Contains("capot") || lower.Contains("tech")) return "\xE756"; // code
            if (lower.Contains("onboard")) return "\xE7BE";   // graduation
            return "\xE81C"; // bullet list
        }

        private void AddSectionHeader(string glyph, string title)
        {
            var sp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 12, 0, 10),
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
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = _t1,
                VerticalAlignment = VerticalAlignment.Center,
            });

            ContentPanel.Children.Add(sp);
        }

        private void AddLoadingIndicator()
        {
            ContentPanel.Children.Clear();
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "Chargement...",
                FontSize = 13,
                Foreground = _t3,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0),
            });
        }

        private void AddFallbackContent(string message)
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 13,
                Foreground = _t2,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0),
            });
        }

        private void AnimateContentIn()
        {
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
        }

        /// <summary>
        /// A single slow light sweep across "Quoi de neuf", then settles to plain white.
        /// </summary>
        private void ApplyTitleShimmer()
        {
            var baseColor = Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF);
            var glint = Color.FromArgb(0xFF, 0xBB, 0xDD, 0xFF);

            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                MappingMode = BrushMappingMode.RelativeToBoundingBox,
            };
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

            var sweep = new DoubleAnimation
            {
                From = -350,
                To = 500,
                Duration = TimeSpan.FromSeconds(1.8),
                BeginTime = TimeSpan.FromSeconds(0.6),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
            };
            sweep.Completed += (s, ev) =>
            {
                MainTitle.Foreground = _t1;
            };
            transform.BeginAnimation(TranslateTransform.XProperty, sweep);
        }

        // ═══ Events ═══
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
    }
}
